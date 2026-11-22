namespace RadarInnowacji.Core.Entities;

/// <summary>Ślad po połączeniu projektów. Materiały (odkrycia) pozostają w bazie.</summary>
public sealed class ProjectMergeHistory
{
    public int Id { get; set; }
    public int SourceProjectId { get; set; }
    public int TargetProjectId { get; set; }
    public string SourceTitle { get; set; } = string.Empty;
    public string TargetTitle { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Actor { get; set; } = "System";
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
