using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Entities;

public sealed class ScanHistory
{
    public int Id { get; set; }
    public int SourceId { get; set; }
    public Source Source { get; set; } = null!;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public ScanStatus Status { get; set; } = ScanStatus.Started;
    public int ItemsFound { get; set; }
    public int ItemsNew { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsDemo { get; set; }
}
