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
    public ScanningOptions Scanning { get; set; } = new();
    public RepositoryOptions Repositories { get; set; } = new();
    public KickstarterOptions Kickstarter { get; set; } = new();
    public AiOptions Ai { get; set; } = new();
    public CategoryOptions Categories { get; set; } = new();
    public ProjectOptions Projects { get; set; } = new();
    public AssessmentOptions Assessment { get; set; } = new();

    public string EffectiveDatabasePath => string.IsNullOrWhiteSpace(DatabasePath)
        ? RadarDataPaths.DatabasePath
        : Path.GetFullPath(Environment.ExpandEnvironmentVariables(DatabasePath));

    public string EffectiveLogDirectory => string.IsNullOrWhiteSpace(LogDirectory)
        ? RadarDataPaths.LogDirectory
        : Path.GetFullPath(Environment.ExpandEnvironmentVariables(LogDirectory));

    public static RadarOptions Load(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<RadarOptions>() ?? new RadarOptions();
        ApplyEnvironmentOverrides(options);
        return options;
    }

    public static void ApplyEnvironmentOverrides(RadarOptions options)
    {
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

        if (bool.TryParse(Environment.GetEnvironmentVariable("RADAR_SCANNING_ENABLED"), out var scanningEnabled))
        {
            options.Scanning.Enabled = scanningEnabled;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("RADAR_SCAN_INTERVAL_MINUTES"), out var scanInterval))
        {
            options.Scanning.IntervalMinutes = scanInterval;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("RADAR_SCAN_START_DELAY_SECONDS"), out var scanDelay))
        {
            options.Scanning.StartDelaySeconds = scanDelay;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("RADAR_SCAN_MAX_ITEMS"), out var maxItems))
        {
            options.Scanning.MaxItemsPerSource = maxItems;
        }

        if (bool.TryParse(Environment.GetEnvironmentVariable("RADAR_AI_ENABLED"), out var aiEnabled))
        {
            options.Ai.Enabled = aiEnabled;
        }

        if (Environment.GetEnvironmentVariable("RADAR_AI_MODEL") is { Length: > 0 } aiModel)
        {
            options.Ai.Model = aiModel;
        }

        if (Environment.GetEnvironmentVariable("RADAR_AI_URL") is { Length: > 0 } aiUrl)
        {
            options.Ai.BaseUrl = aiUrl;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("RADAR_AI_TIMEOUT"), out var aiTimeout))
        {
            options.Ai.TimeoutSeconds = aiTimeout;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("RADAR_AI_BATCH"), out var aiBatch))
        {
            options.Ai.BatchSize = aiBatch;
        }

        if (bool.TryParse(Environment.GetEnvironmentVariable("RADAR_CATEGORIES_ENABLED"), out var categoriesEnabled))
        {
            options.Categories.Enabled = categoriesEnabled;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("RADAR_CATEGORIES_INTERVAL"), out var categoriesInterval))
        {
            options.Categories.IntervalMinutes = categoriesInterval;
        }

        if (double.TryParse(
                Environment.GetEnvironmentVariable("RADAR_CATEGORIES_SIMILARITY"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var similarity))
        {
            options.Categories.MinSimilarity = similarity;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("RADAR_CATEGORIES_MIN_PROJECTS"), out var minProjects))
        {
            options.Categories.MinClusterSize = minProjects;
        }

        if (Environment.GetEnvironmentVariable("RADAR_CATEGORY_EMBEDDING_PROVIDER") is { Length: > 0 } provider)
        {
            options.Categories.EmbeddingProvider = provider;
        }

        if (Environment.GetEnvironmentVariable("RADAR_CATEGORY_EMBEDDING_MODEL") is { Length: > 0 } embeddingModel)
        {
            options.Categories.OllamaEmbeddingModel = embeddingModel;
        }

        if (Environment.GetEnvironmentVariable("RADAR_CATEGORY_EMBEDDING_URL") is { Length: > 0 } embeddingUrl)
        {
            options.Categories.OllamaBaseUrl = embeddingUrl;
        }

        if (bool.TryParse(Environment.GetEnvironmentVariable("RADAR_PROJECTS_ENABLED"), out var projectsEnabled))
        {
            options.Projects.Enabled = projectsEnabled;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("RADAR_PROJECTS_INTERVAL"), out var projectsInterval))
        {
            options.Projects.IntervalMinutes = projectsInterval;
        }

        if (bool.TryParse(Environment.GetEnvironmentVariable("RADAR_ASSESSMENT_ENABLED"), out var assessmentEnabled))
        {
            options.Assessment.Enabled = assessmentEnabled;
        }
    }
}
