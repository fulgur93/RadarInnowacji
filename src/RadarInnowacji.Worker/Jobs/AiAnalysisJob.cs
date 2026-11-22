using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Infrastructure.Configuration;

namespace RadarInnowacji.Worker.Jobs;

/// <summary>Cyklicznie przetwarza kolejkę odkryć oczekujących na analizę AI.</summary>
public sealed class AiAnalysisJob(
    IServiceScopeFactory scopeFactory,
    IOptions<RadarOptions> options,
    ILogger<AiAnalysisJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ai = options.Value.Ai;
        if (!ai.Enabled)
        {
            logger.LogInformation("Analiza AI jest wyłączona.");
            return;
        }

        logger.LogInformation(
            "Analiza AI uruchomiona: model {Model}, adres {BaseUrl}.",
            ai.Model,
            ai.BaseUrl);

        var delay = TimeSpan.FromSeconds(Math.Max(0, ai.StartDelaySeconds));
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, stoppingToken);
        }

        await RunCycleAsync(ai.BatchSize, stoppingToken);

        var interval = TimeSpan.FromMinutes(Math.Max(1, ai.IntervalMinutes));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCycleAsync(ai.BatchSize, stoppingToken);
        }
    }

    private async Task RunCycleAsync(int batchSize, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var analyzer = scope.ServiceProvider.GetRequiredService<IAiAnalysisService>();
            var connection = await analyzer.TestConnectionAsync(stoppingToken);
            if (!connection.IsAvailable)
            {
                logger.LogWarning("Analiza AI wstrzymana: {Message}", connection.Message);
                return;
            }

            var queue = scope.ServiceProvider.GetRequiredService<IAiAnalysisQueueService>();
            var result = await queue.ProcessPendingAsync(batchSize, stoppingToken);
            if (result.Processed > 0 || result.Failed > 0)
            {
                logger.LogInformation(
                    "Zakończono cykl analizy AI: przeanalizowano {Processed}, błędów {Failed}.",
                    result.Processed,
                    result.Failed);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Błąd podczas cyklicznej analizy AI.");
        }
    }
}
