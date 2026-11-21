using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace RadarInnowacji.IntegrationTests;

/// <summary>
/// Host testowy bez otwierania przeglądarki, bez mutexu jednorazowego uruchomienia
/// i z bazą w katalogu tymczasowym.
/// </summary>
public sealed class RadarWebApplicationFactory : WebApplicationFactory<Program>
{
    static RadarWebApplicationFactory()
    {
        var testDataDirectory = Path.Combine(Path.GetTempPath(), "radar-integration-tests");
        Environment.SetEnvironmentVariable("RADAR_PORT", "0");
        Environment.SetEnvironmentVariable("RADAR_OPEN_BROWSER", "false");
        Environment.SetEnvironmentVariable("RADAR_SINGLE_INSTANCE", "false");
        Environment.SetEnvironmentVariable("RADAR_DATA_DIR", testDataDirectory);
        Environment.SetEnvironmentVariable("RADAR_LOG_DIR", Path.Combine(testDataDirectory, "logs"));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Radar:Port"] = "0",
                ["Radar:OpenBrowser"] = "false",
                ["Radar:SingleInstance"] = "false"
            }));
    }
}
