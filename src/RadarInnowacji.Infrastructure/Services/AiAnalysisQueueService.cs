using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>Kolejka analiz AI. Materiały bez AI pozostają w bazie i mogą być przetworzone później.</summary>
public sealed class AiAnalysisQueueService(
    RadarDbContext db,
    IAiAnalysisService analyzer,
    IOptions<RadarOptions> options,
    ILogger<AiAnalysisQueueService> logger) : IAiAnalysisQueueService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions ResultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<AiQueueResult> ProcessPendingAsync(
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var limit = Math.Clamp(maxItems, 1, 100);
            var pending = await db.Discoveries
                .Where(x => !x.IsDemo &&
                            (x.ProcessingStatus == ProcessingStatus.Pending ||
                             x.ProcessingStatus == ProcessingStatus.AiQueued))
                .OrderBy(x => x.DiscoveredAt)
                .Take(limit)
                .ToListAsync(cancellationToken);

            var processed = 0;
            var failed = 0;
            string? lastError = null;
            foreach (var discovery in pending)
            {
                var result = await AnalyzeOneAsync(discovery, cancellationToken);
                processed += result.Processed;
                failed += result.Failed;
                lastError = result.LastError ?? lastError;
            }

            if (processed > 0 || failed > 0)
            {
                logger.LogInformation(
                    "Kolejka AI: przeanalizowano {Processed}, błędów {Failed}.",
                    processed,
                    failed);
            }

            return new AiQueueResult(processed, failed, lastError);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<AiQueueResult> AnalyzeDiscoveryAsync(
        int discoveryId,
        CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var discovery = await db.Discoveries
                .FirstOrDefaultAsync(x => x.Id == discoveryId, cancellationToken);
            if (discovery is null)
            {
                return AiQueueResult.Failure("Nie znaleziono odkrycia.");
            }

            return await AnalyzeOneAsync(discovery, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<AiQueueResult> AnalyzeOneAsync(
        Discovery discovery,
        CancellationToken cancellationToken)
    {
        if (discovery.IsDemo)
        {
            return AiQueueResult.Failure("Dane demonstracyjne nie są analizowane.");
        }

        discovery.ProcessingStatus = ProcessingStatus.AiQueued;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await analyzer.AnalyzeAsync(discovery, cancellationToken);
            discovery.AIProcessed = true;
            discovery.AIConfidence = result.Confidence;
            discovery.AIAnalysis = Trim(result.ToDisplayText(), ContentLimits.MaxDescriptionLength);
            discovery.IsPotentialInnovation = result.IsPotentialInnovation;
            if (discovery.Status != DiscoveryStatus.Verified)
            {
                discovery.Status = result.IsPotentialInnovation
                    ? DiscoveryStatus.Interesting
                    : DiscoveryStatus.Candidate;
            }

            discovery.ProcessingStatus = ProcessingStatus.AiProcessed;
            db.AiAnalyses.Add(new AiAnalysisRecord
            {
                DiscoveryId = discovery.Id,
                Model = Trim(options.Value.Ai.Model, 200),
                ResultJson = Trim(JsonSerializer.Serialize(result, ResultJsonOptions), ContentLimits.MaxRawContentLength),
                Confidence = result.Confidence,
                Reason = Trim(result.Reason, ContentLimits.MaxDescriptionLength),
                PromptVersion = AiAnalysisResult.PromptVersion,
                IsSuccess = true
            });
            await db.SaveChangesAsync(cancellationToken);
            return AiQueueResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Analiza AI odkrycia {Id} nie powiodła się.", discovery.Id);
            discovery.ProcessingStatus = ProcessingStatus.AiFailed;
            db.AiAnalyses.Add(new AiAnalysisRecord
            {
                DiscoveryId = discovery.Id,
                Model = Trim(options.Value.Ai.Model, 200),
                ResultJson = "{}",
                Confidence = 0,
                Reason = string.Empty,
                PromptVersion = AiAnalysisResult.PromptVersion,
                IsSuccess = false,
                ErrorMessage = Trim(exception.Message, ContentLimits.MaxDescriptionLength)
            });
            await db.SaveChangesAsync(cancellationToken);
            return AiQueueResult.Failure(exception.Message);
        }
    }

    private static string Trim(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
