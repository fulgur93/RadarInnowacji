using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RadarInnowacji.Infrastructure.Configuration;

namespace RadarInnowacji.Infrastructure.Database;

internal static class DatabaseServiceCollectionExtensions
{
    internal static IServiceCollection AddRadarDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var options = RadarOptions.Load(configuration);
        RadarDataPaths.EnsureDirectories();

        var connectionString = $"Data Source={options.EffectiveDatabasePath}";
        services.AddDbContext<RadarDbContext>(dbOptions => dbOptions.UseSqlite(connectionString));

        return services;
    }
}
