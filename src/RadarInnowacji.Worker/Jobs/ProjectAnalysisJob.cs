using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Infrastructure.Configuration;

namespace RadarInnowacji.Worker.Jobs;

/// <summary>
/// Cyklicznie szuka tego samego projektu w różnych źródłach. Jednoznaczne duplikaty
/// łączy bez usuwania odkryć, niejednoznaczne zostawia jako propozycje dla użytkownika.
/// </summary>
public sealed class ProjectAnalysisJob(
    IServiceScopeFactory scopeFactory,
    IOptions<RadarOptions> options,
    ILogger<ProjectAnalysisJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var projects = options.Value.Projects;
        if (!projects.Enabled)
        {
            logger.LogInformation("Wykrywanie duplikatów projektów jest wyłączone.");
            return;
        }

        logger.LogInformation(
            "Wykrywanie duplikatów projektów uruchomione: próg propozycji {Proposal:0.00}, auto {Auto:0.00}.",
            projects.ProposalSimilarity,
            projects.AutoMergeSimilarity);

        var delay = TimeSpan.FromSeconds(Math.Max(0, projects.StartDelaySeconds));
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, stoppingToken);
        }

        await RunCycleAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, projects.IntervalMinutes)));
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
            var service = scope.ServiceProvider.GetRequiredService<IProjectService>();
            var result = await service.DetectDuplicatesAsync(stoppingToken);
            logger.LogInformation(
                "Deduplikacja projektów: przeanalizowano {Analyzed}, połączono {Linked}, propozycje {Proposals}.",
                result.ProjectsAnalyzed,
                result.AutoLinked,
                result.ProposalsCreated);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown.
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cykl wykrywania duplikatów nie powiódł się; zostanie ponowiony.");
        }
    }
}
