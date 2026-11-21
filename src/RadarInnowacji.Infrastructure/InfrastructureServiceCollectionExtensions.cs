using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Database;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddRadarInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RadarOptions>(configuration.GetSection(RadarOptions.SectionName));
        services.AddRadarDatabase(configuration);

        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IDiscoveryService, DiscoveryService>();
        services.AddScoped<ISourceService, SourceService>();
        services.AddScoped<IDemoDataService, DemoDataSeeder>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IScanHistoryService, ScanHistoryService>();

        return services;
    }
}
