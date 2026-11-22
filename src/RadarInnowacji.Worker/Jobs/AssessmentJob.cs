using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Infrastructure.Configuration;

namespace RadarInnowacji.Worker.Jobs;

/// <summary>
/// Cyklicznie ocenia projekty bez oceny lub z nieaktualną oceną (nowe materiały, zmieniony profil).
/// Projekty bez zmian nie tworzą nowych migawek oceny.
/// </summary>
public sealed class AssessmentJob(
    IServiceScopeFactory scopeFactory,
    IOptions<RadarOptions> options,
    ILogger<AssessmentJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value.Assessment;
        if (!settings.Enabled)
        {
            logger.LogInformation("Automatyczna ocena projektów jest wyłączona.");
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(0, settings.StartDelaySeconds));
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, stoppingToken);
        }

        await RunCycleAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, settings.IntervalMinutes)));
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
            var service = scope.ServiceProvider.GetRequiredService<IAssessmentService>();
            var changed = await service.AssessPendingAsync(stoppingToken);
            logger.LogInformation("Ocena projektów: nowych lub zmienionych ocen {Changed}.", changed);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Zamykanie aplikacji.
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cykl oceny projektów nie powiódł się; zostanie ponowiony.");
        }
    }
}
