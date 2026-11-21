using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Infrastructure.Adapters;
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
        services.PostConfigure<RadarOptions>(RadarOptions.ApplyEnvironmentOverrides);
        services.AddRadarDatabase(configuration);

        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IDiscoveryService, DiscoveryService>();
        services.AddScoped<ISourceService, SourceService>();
        services.AddScoped<IDemoDataService, DemoDataSeeder>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IScanHistoryService, ScanHistoryService>();
        services.AddScoped<IScanService, ScanService>();

        services.AddHttpClient("RadarSources", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.MaxResponseContentBufferSize = 2_000_000;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RadarInnowacji/1.0 (prototyp)");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rss+xml"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atom+xml"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        });

        services.AddTransient<ISourceAdapter>(provider => new GitHubSourceAdapter(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("RadarSources"),
            TimeProvider.System,
            provider.GetRequiredService<IOptions<RadarOptions>>().Value.Repositories));
        services.AddTransient<ISourceAdapter>(provider => new GitLabSourceAdapter(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("RadarSources"),
            TimeProvider.System,
            provider.GetRequiredService<IOptions<RadarOptions>>().Value.Repositories));
        services.AddTransient<ISourceAdapter>(provider => new HuggingFaceSourceAdapter(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("RadarSources")));
        services.AddTransient<ISourceAdapter>(provider => new DspaceSourceAdapter(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("RadarSources")));
        services.AddTransient<ISourceAdapter>(provider => new RssAtomSourceAdapter(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("RadarSources")));
        services.AddTransient<ISourceAdapter>(provider => new OaiPmhSourceAdapter(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("RadarSources")));
        services.AddTransient<ISourceAdapter>(provider => new HtmlPageSourceAdapter(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("RadarSources")));

        services.AddSingleton<ISourceAdapterResolver, SourceAdapterResolver>();

        return services;
    }
}
