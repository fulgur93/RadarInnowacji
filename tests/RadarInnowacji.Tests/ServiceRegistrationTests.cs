using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RadarInnowacji.Infrastructure;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Tests;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddRadarInfrastructure_RejestrujeKontekstIOpcje()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Radar:Port"] = "5099",
                ["Radar:DatabasePath"] = Path.Combine(Path.GetTempPath(), "radar-di-test", "test.db")
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRadarInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<RadarDbContext>());
        Assert.Equal(5099, provider.GetRequiredService<IOptions<RadarOptions>>().Value.Port);
    }
}
