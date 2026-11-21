namespace RadarInnowacji.Infrastructure.Configuration;

/// <summary>
/// Ścieżki danych użytkownika. Dane nie są zapisywane w katalogu instalacyjnym aplikacji.
/// Zmienne środowiskowe pozwalają nadpisać lokalizacje na potrzeby testów i instalacji.
/// </summary>
public static class RadarDataPaths
{
    public const string DataDirectoryEnvironmentVariable = "RADAR_DATA_DIR";
    public const string DatabaseEnvironmentVariable = "RADAR_DB_PATH";
    public const string LogDirectoryEnvironmentVariable = "RADAR_LOG_DIR";

    public static string AppDataDirectory
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);
            var root = string.IsNullOrWhiteSpace(configured)
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Environment.ExpandEnvironmentVariables(configured);

            return Path.GetFullPath(Path.Combine(root, "RadarInnowacji"));
        }
    }

    public static string DatabasePath
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable(DatabaseEnvironmentVariable);
            return string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(AppDataDirectory, "radar.db")
                : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured));
        }
    }

    public static string LogDirectory
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable(LogDirectoryEnvironmentVariable);
            return string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(AppDataDirectory, "logs")
                : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured));
        }
    }

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataDirectory);
        var databaseDirectory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        Directory.CreateDirectory(LogDirectory);
    }
}
