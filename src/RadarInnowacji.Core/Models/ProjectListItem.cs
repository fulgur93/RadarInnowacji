using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Models;

public sealed class ProjectListItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();
    public int DiscoveryCount { get; set; }
    public int SourceCount { get; set; }
    public IReadOnlyList<string> SourceNames { get; set; } = Array.Empty<string>();
    public IReadOnlyList<RadarKind> Radars { get; set; } = Array.Empty<RadarKind>();
    public double FreshnessScore { get; set; }
    public DateTime FirstDiscoveredAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public bool IsDemo { get; set; }
    public int? MergedIntoProjectId { get; set; }
}
