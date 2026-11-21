using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RadarInnowacji.Core.Interfaces;

namespace RadarInnowacji.Worker.Jobs;

/// <summary>
/// Uzupełnia bazę o stałe źródła zaraz po migracjach, jeszcze przed pierwszym skanowaniem.
/// Dzięki temu świeżo zainstalowana aplikacja ma gotowe źródła bez importu danych.
/// </summary>
public sealed class PermanentSourcesSeedJob(
    IServiceScopeFactory scopeFactory,
    ILogger<PermanentSourcesSeedJob> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<IPermanentSourcesSeeder>();
            var added = await seeder.SeedAsync(cancellationToken);
            logger.LogInformation("Stałe źródła są gotowe (nowych: {Added}).", added);
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Nie udało się dodać stałych źródeł do bazy danych.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
