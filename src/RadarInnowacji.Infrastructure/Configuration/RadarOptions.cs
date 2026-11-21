using Microsoft.Extensions.Configuration;

namespace RadarInnowacji.Infrastructure.Configuration;

/// <summary>
/// Ustawienia uruchomieniowe aplikacji. Wartości z appsettings.json można nadpisać
/// zmiennymi środowiskowymi (używane przez instalator i testy integracyjne).
/// </summary>
public sealed class RadarOptions
{
    public const string SectionName = "Radar";

    public int Port { get; set; } = 5080;
    public bool OpenBrowser { get; set; } = true;
    public bool SingleInstance { get; set; } = true;
    public string DatabasePath { get; set; } = string.Empty;
    public string LogDirectory { get; set; } = string.Empty;

    public string EffectiveDatabasePath => string.IsNullOrWhiteSpace(DatabasePath)
        ? RadarDataPaths.DatabasePath
        : Path.GetFullPath(Environment.ExpandEnvironmentVariables(DatabasePath));

    public string EffectiveLogDirectory => string.IsNullOrWhiteSpace(LogDirectory)
        ? RadarDataPaths.LogDirectory
        : Path.GetFullPath(Environment.ExpandEnvironmentVariables(LogDirectory));

    public static RadarOptions Load(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<RadarOptions>() ?? new RadarOptions();

        if (int.TryParse(Environment.GetEnvironmentVariable("RADAR_PORT"), out var port))
        {
            options.Port = port;
        }

        if (bool.TryParse(Environment.GetEnvironmentVariable("RADAR_OPEN_BROWSER"), out var openBrowser))
        {
            options.OpenBrowser = openBrowser;
        }

        if (bool.TryParse(Environment.GetEnvironmentVariable("RADAR_SINGLE_INSTANCE"), out var singleInstance))
        {
            options.SingleInstance = singleInstance;
        }

        return options;
    }
}
