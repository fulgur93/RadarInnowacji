using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Entities;

public sealed class Source
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public RadarKind Radar { get; set; }
    public SourceType SourceType { get; set; }
    public FetchMethod FetchMethod { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDemo { get; set; }
    public int ScanIntervalMinutes { get; set; } = 60;
    public DateTime? LastScanDate { get; set; }
    public DateTime? LastSuccessfulScan { get; set; }
    public string? LastError { get; set; }
    public string Configuration { get; set; } = "{}";

    public ICollection<Discovery> Discoveries { get; set; } = new List<Discovery>();
    public ICollection<ScanHistory> Scans { get; set; } = new List<ScanHistory>();
}
