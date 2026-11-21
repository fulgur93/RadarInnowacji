using System.Security.Cryptography;
using System.Text;
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

/// <summary>Pobiera materiały z aktywnych źródeł i zapisuje je w ujednoliconym modelu.</summary>
public sealed class ScanService(
    RadarDbContext db,
    ISourceAdapterResolver adapterResolver,
    IOptions<RadarOptions> options,
    ILogger<ScanService> logger) : IScanService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<ScanResult> ScanSourceAsync(int sourceId, CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var source = await db.Sources.FirstOrDefaultAsync(x => x.Id == sourceId, cancellationToken);
            if (source is null)
            {
                return new ScanResult(sourceId, "Nieznane źródło", ScanStatus.Failed, 0, 0, "Nie znaleziono źródła.");
            }

            if (source.IsDemo)
            {
                return new ScanResult(source.Id, source.Name, ScanStatus.Succeeded, 0, 0, null);
            }

            var startedAt = DateTime.UtcNow;
            var history = new ScanHistory
            {
                SourceId = source.Id,
                StartedAt = startedAt,
                Status = ScanStatus.Started,
                IsDemo = false
            };
            db.ScanHistory.Add(history);
            await db.SaveChangesAsync(cancellationToken);

            try
            {
                var adapter = adapterResolver.Resolve(source);
                if (adapter is null)
                {
                    throw new InvalidOperationException(
                        $"Brak adaptera dla typu źródła {source.SourceType}.");
                }

                var maxItems = source.SourceType is SourceType.GitHubApi or SourceType.GitLabApi
                    ? Math.Clamp(options.Value.Repositories.MaxItemsPerScan, 1, 2000)
                    : Math.Clamp(options.Value.Scanning.MaxItemsPerSource, 1, 500);
                var fetched = await adapter.FetchResultAsync(source, maxItems, cancellationToken);
                var limited = fetched.Items.Take(maxItems).ToList();
                var added = 0;

                foreach (var item in limited)
                {
                    var hash = ComputeHash(source.Id, item);
                    var existing = await db.Discoveries.FirstOrDefaultAsync(
                        x => x.SourceId == source.Id && x.ExternalId == item.ExternalId,
                        cancellationToken);

                    if (existing is null)
                    {
                        db.Discoveries.Add(CreateDiscovery(source, item, hash, startedAt));
                        added++;
                    }
                    else
                    {
                        UpdateDiscovery(existing, item, hash);
                    }
                }

                // Postęp skanowania przyrostowego zapisujemy także po skanie częściowym.
                if (fetched.Cursors is not null)
                {
                    source.Configuration = SourceConfiguration.WithCursors(source.Configuration, fetched.Cursors);
                }

                var status = fetched.IsComplete ? ScanStatus.Succeeded : ScanStatus.Partial;
                var warning = fetched.Warning is null
                    ? null
                    : fetched.Warning[..Math.Min(fetched.Warning.Length, ContentLimits.MaxErrorLength)];

                var finishedAt = DateTime.UtcNow;
                source.LastScanDate = finishedAt;
                source.LastSuccessfulScan = finishedAt;
                source.LastError = fetched.IsComplete ? null : warning;
                history.Status = status;
                history.ItemsFound = limited.Count;
                history.ItemsNew = added;
                history.FinishedAt = finishedAt;
                history.ErrorMessage = warning;

                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Skan źródła {Source} zakończony ({Status}): znaleziono {Found}, nowych {New}. {Warning}",
                    source.Name,
                    status,
                    limited.Count,
                    added,
                    warning);

                return new ScanResult(source.Id, source.Name, status, limited.Count, added, warning);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                history.Status = ScanStatus.Failed;
                history.ErrorMessage = "Skanowanie zostało przerwane.";
                history.FinishedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(CancellationToken.None);
                throw;
            }
            catch (Exception exception)
            {
                var message = exception.Message.Length <= ContentLimits.MaxErrorLength
                    ? exception.Message
                    : exception.Message[..ContentLimits.MaxErrorLength];

                var finishedAt = DateTime.UtcNow;
                source.LastScanDate = finishedAt;
                source.LastError = message;
                history.Status = ScanStatus.Failed;
                history.ErrorMessage = message;
                history.FinishedAt = finishedAt;

                await db.SaveChangesAsync(CancellationToken.None);
                logger.LogWarning(exception, "Skan źródła {Source} nie powiódł się.", source.Name);

                return new ScanResult(source.Id, source.Name, ScanStatus.Failed, 0, 0, message);
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<IReadOnlyList<ScanResult>> ScanDueSourcesAsync(CancellationToken cancellationToken = default)
    {
        var sources = await db.Sources
            .Where(x => x.IsActive && !x.IsDemo)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var results = new List<ScanResult>();

        foreach (var source in sources)
        {
            var interval = Math.Max(1, source.ScanIntervalMinutes);
            if (source.LastScanDate is not null && source.LastScanDate.Value.AddMinutes(interval) > now)
            {
                continue;
            }

            results.Add(await ScanSourceAsync(source.Id, cancellationToken));
        }

        return results;
    }

    private static Discovery CreateDiscovery(Source source, SourceItem item, string hash, DateTime now) => new()
    {
        Title = item.Title,
        Description = item.Description,
        RawContent = item.Content,
        ContentLength = item.Content.Length,
        IsContentTruncated = item.Content.Length >= ContentLimits.MaxRawContentLength,
        SourceId = source.Id,
        SourceUrl = item.SourceUrl,
        ExternalId = item.ExternalId,
        PublishedAt = item.PublishedAt,
        LastActivityAt = item.LastActivityAt,
        CreatedAt = now,
        DiscoveredAt = now,
        ContentHash = hash,
        Author = item.Author,
        Organization = item.Organization,
        Technologies = JsonSerializer.Serialize(item.Technologies),
        CategoryIds = "[]",
        AIProcessed = false,
        IsPotentialInnovation = false,
        ProcessingStatus = ProcessingStatus.Pending,
        Status = DiscoveryStatus.Candidate,
        IsDemo = false
    };

    private static void UpdateDiscovery(Discovery discovery, SourceItem item, string hash)
    {
        discovery.Title = item.Title;
        discovery.Description = item.Description;
        discovery.RawContent = item.Content;
        discovery.ContentLength = item.Content.Length;
        discovery.IsContentTruncated = item.Content.Length >= ContentLimits.MaxRawContentLength;
        discovery.SourceUrl = item.SourceUrl;
        discovery.PublishedAt = item.PublishedAt ?? discovery.PublishedAt;
        discovery.LastActivityAt = item.LastActivityAt ?? discovery.LastActivityAt;
        discovery.Author = item.Author ?? discovery.Author;
        discovery.Organization = item.Organization ?? discovery.Organization;
        discovery.Technologies = JsonSerializer.Serialize(item.Technologies);
        discovery.ContentHash = hash;
    }

    private static string ComputeHash(int sourceId, SourceItem item)
    {
        var input = $"{sourceId}\n{item.ExternalId}\n{item.Title}\n{item.Content}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }
}
