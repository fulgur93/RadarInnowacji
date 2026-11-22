using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Models;

public sealed class DiscoveryListItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RadarKind Radar { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public DateTime DiscoveredAt { get; set; }
    public string? Author { get; set; }
    public string? Organization { get; set; }
    public IReadOnlyList<string> Technologies { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();
    public double? FreshnessScore { get; set; }
    public DiscoveryStatus Status { get; set; }
    public ProcessingStatus ProcessingStatus { get; set; }
    public bool IsPotentialInnovation { get; set; }
    public int? AIConfidence { get; set; }
    public bool IsDemo { get; set; }
}
