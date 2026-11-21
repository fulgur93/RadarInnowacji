using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Models;

public sealed class DashboardSummary
{
    public int TotalDiscoveries { get; init; }
    public int PotentialInnovations { get; init; }
    public int NewLast7Days { get; init; }
    public int ActiveSources { get; init; }
    public int CategoryCount { get; init; }
    public int DemoCount { get; init; }
    public int InterestingCount { get; init; }
    public int VerifiedCount { get; init; }
    public int AiProcessedCount { get; init; }
    public int AiPendingCount { get; init; }
    public IReadOnlyList<DiscoveryListItem> LatestDiscoveries { get; init; } = Array.Empty<DiscoveryListItem>();
    public IReadOnlyList<CategoryCount> TopCategories { get; init; } = Array.Empty<CategoryCount>();
    public IReadOnlyList<ActivityPoint> Activity { get; init; } = Array.Empty<ActivityPoint>();
    public IReadOnlyList<RadarCount> RadarCounts { get; init; } = Array.Empty<RadarCount>();
}

public sealed record CategoryCount(int CategoryId, string Name, int Count);

public sealed record ActivityPoint(string Date, int Count);

public sealed record RadarCount(RadarKind Radar, int Count);
