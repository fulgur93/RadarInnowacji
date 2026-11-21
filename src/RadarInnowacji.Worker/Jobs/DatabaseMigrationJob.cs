using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Worker.Jobs;

/// <summary>
/// Stosuje migracje EF Core przy starcie aplikacji, dzięki czemu instalator
/// nie wymaga wykonywania żadnych komend przez użytkownika.
/// </summary>
public sealed class DatabaseMigrationJob(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseMigrationJob> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RadarDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Migracje bazy danych zostały zastosowane.");
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Nie udało się zastosować migracji bazy danych.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
