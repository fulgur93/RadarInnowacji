using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RadarInnowacji.Core.Interfaces;

namespace RadarInnowacji.Worker.Jobs;

/// <summary>
/// Uzupełnia bazę o stałe źródła zaraz po migracjach, jeszcze przed pierwszym skanowaniem.
/// Dzięki temu świeżo zainstalowana aplikacja ma gotowe źródła bez importu danych.
/// Przy pustym katalogu odbiorców dodaje też przykładowe typy odbiorców (Etap 9).
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

            var recipients = scope.ServiceProvider.GetRequiredService<IRecipientService>();
            var examples = await recipients.SeedExamplesAsync(cancellationToken);
            if (examples > 0)
            {
                logger.LogInformation("Dodano {Count} przykładowych typów odbiorców do katalogu.", examples);
            }
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Nie udało się dodać stałych źródeł do bazy danych.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
