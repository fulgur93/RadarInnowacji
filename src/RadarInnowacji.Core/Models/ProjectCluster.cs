namespace RadarInnowacji.Core.Models;

/// <summary>Grupa projektów wykryta na podstawie podobieństwa wektorów.</summary>
public sealed class ProjectCluster
{
    public int Id { get; set; }
    public int ProjectCount { get; set; }
    public double AverageSimilarity { get; set; }
    public IReadOnlyList<ClusterProjectItem> Projects { get; set; } = Array.Empty<ClusterProjectItem>();
}

public sealed class ClusterProjectItem
{
    public int Id { get; set; }
    public int? DiscoveryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public double FreshnessScore { get; set; }
    public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();
}
