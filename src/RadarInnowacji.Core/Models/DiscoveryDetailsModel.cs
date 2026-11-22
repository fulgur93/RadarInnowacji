using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Models;

public sealed class DiscoveryDetailsModel
{
    public required Discovery Discovery { get; init; }
    public string SourceName { get; init; } = string.Empty;
    public RadarKind Radar { get; init; }
    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();
    /// <summary>Projekty, do których należy odkrycie (z przejściem do wspólnego widoku).</summary>
    public IReadOnlyList<DiscoveryProjectLinkItem> Projects { get; init; } = Array.Empty<DiscoveryProjectLinkItem>();
    /// <summary>Odkrycia tego samego projektu (np. kopia na innej platformie).</summary>
    public IReadOnlyList<DiscoveryListItem> RelatedDiscoveries { get; init; } = Array.Empty<DiscoveryListItem>();
    public IReadOnlyList<DiscoveryListItem> SimilarDiscoveries { get; init; } = Array.Empty<DiscoveryListItem>();
    public IReadOnlyList<AiAnalysisRecord> AiHistory { get; init; } = Array.Empty<AiAnalysisRecord>();
}

public sealed class DiscoveryProjectLinkItem
{
    public int ProjectId { get; init; }
    public string Title { get; init; } = string.Empty;
    public double FreshnessScore { get; init; }
    public int SourceCount { get; init; }
    public int? MergedIntoProjectId { get; init; }
}
