using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Infrastructure.Configuration;

namespace RadarInnowacji.Worker.Jobs;

/// <summary>Cyklicznie grupuje projekty i wykrywa propozycje nowych kategorii (nie przy każdym skanie).</summary>
public sealed class CategoryAnalysisJob(
    IServiceScopeFactory scopeFactory,
    IOptions<RadarOptions> options,
    ILogger<CategoryAnalysisJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var categories = options.Value.Categories;
        if (!categories.Enabled)
        {
            logger.LogInformation("Grupowanie kategorii jest wyłączone.");
            return;
        }

        logger.LogInformation(
            "Grupowanie kategorii uruchomione: dostawca {Provider}, próg podobieństwa {Similarity:0.00}.",
            categories.EmbeddingProvider,
            categories.MinSimilarity);

        var delay = TimeSpan.FromSeconds(Math.Max(0, categories.StartDelaySeconds));
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, stoppingToken);
        }

        await RunCycleAsync(stoppingToken);
        var interval = TimeSpan.FromMinutes(Math.Max(1, categories.IntervalMinutes));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCycleAsync(stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ICategorizationService>();
            var result = await service.AnalyzeAsync(stoppingToken);
            logger.LogInformation(
                "Grupowanie kategorii: projekty {Projects}, grupy {Clusters}, nowe propozycje {Proposals}.",
                result.ProjectsAnalyzed,
                result.ClustersFound,
                result.ProposalsCreated);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown.
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cykl grupowania kategorii nie powiódł się; zostanie ponowiony.");
        }
    }
}
