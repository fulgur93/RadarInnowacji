namespace RadarInnowacji.Core.Models;

/// <summary>Wynik testu połączenia z lokalnym silnikiem AI.</summary>
public sealed record AiConnectionTest(
    bool IsAvailable,
    string Model,
    string Message,
    IReadOnlyList<string> InstalledModels);
