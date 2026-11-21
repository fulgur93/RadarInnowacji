using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Entities;

public sealed class InnovationProject
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Problem { get; set; }
    public string? ProposedSolution { get; set; }
    public ProjectStatus ProjectStatus { get; set; } = ProjectStatus.Unknown;

    public DateTime FirstDiscoveredAt { get; set; } = DateTime.UtcNow;
    public DateTime? EarliestKnownDate { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public double FreshnessScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDemo { get; set; }

    public ICollection<DiscoveryProject> DiscoveryLinks { get; set; } = new List<DiscoveryProject>();
    public ICollection<ProjectCategory> CategoryLinks { get; set; } = new List<ProjectCategory>();
}
