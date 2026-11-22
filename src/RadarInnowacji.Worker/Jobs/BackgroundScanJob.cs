using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Infrastructure.Configuration;

namespace RadarInnowacji.Worker.Jobs;

/// <summary>Cyklicznie uruchamia skanowanie źródeł, których interwał już upłynął.</summary>
public sealed class BackgroundScanJob(
    IServiceScopeFactory scopeFactory,
    IOptions<RadarOptions> options,
    ILogger<BackgroundScanJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scanning = options.Value.Scanning;
        if (!scanning.Enabled)
        {
            logger.LogInformation("Skanowanie w tle jest wyłączone.");
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(0, scanning.StartDelaySeconds));
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, stoppingToken);
        }

        if (scanning.Continuous)
        {
            logger.LogInformation("Tryb ciągłego skanowania aktywny (przerwa {Delay}s).", scanning.ContinuousDelaySeconds);
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunScanCycleAsync(stoppingToken);
                var pause = TimeSpan.FromSeconds(Math.Clamp(scanning.ContinuousDelaySeconds, 5, 3600));
                await Task.Delay(pause, stoppingToken);
            }

            return;
        }

        await RunScanCycleAsync(stoppingToken);
        var interval = TimeSpan.FromMinutes(Math.Max(1, scanning.IntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunScanCycleAsync(stoppingToken);
        }
    }

    private async Task RunScanCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var scanService = scope.ServiceProvider.GetRequiredService<IScanService>();
            var results = await scanService.ScanDueSourcesAsync(stoppingToken);

            logger.LogInformation(
                "Zakończono cykl skanowania: {Count} źródeł, {New} nowych odkryć.",
                results.Count,
                results.Sum(x => x.ItemsNew));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Błąd podczas cyklicznego skanowania źródeł.");
        }
    }
}
