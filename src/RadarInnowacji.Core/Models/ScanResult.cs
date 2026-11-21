using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Models;

/// <summary>Podsumowanie jednego przebiegu skanowania źródła.</summary>
public sealed record ScanResult(
    int SourceId,
    string SourceName,
    ScanStatus Status,
    int ItemsFound,
    int ItemsNew,
    string? ErrorMessage);
