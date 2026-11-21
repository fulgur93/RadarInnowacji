using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Models;

public sealed record ScanHistoryItem(
    int Id,
    string SourceName,
    RadarKind Radar,
    DateTime StartedAt,
    DateTime? FinishedAt,
    ScanStatus Status,
    int ItemsFound,
    int ItemsNew,
    string? ErrorMessage,
    bool IsDemo);
