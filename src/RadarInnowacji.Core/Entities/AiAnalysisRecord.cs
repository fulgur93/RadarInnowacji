namespace RadarInnowacji.Core.Entities;

/// <summary>Historia analiz AI jednego odkrycia.</summary>
public sealed class AiAnalysisRecord
{
    public int Id { get; set; }
    public int DiscoveryId { get; set; }
    public Discovery Discovery { get; set; } = null!;
    public string Model { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public string ResultJson { get; set; } = "{}";
    public int Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = "v1";
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsDemo { get; set; }
}
