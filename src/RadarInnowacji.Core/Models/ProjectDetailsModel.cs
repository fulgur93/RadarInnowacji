using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Models;

/// <summary>Szczegóły projektu wraz ze wszystkimi powiązaniami z różnych źródeł.</summary>
public sealed class ProjectDetailsModel
{
    public required InnovationProject Project { get; init; }
    public InnovationProject? MergedInto { get; init; }
    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ProjectDiscoveryItem> Discoveries { get; init; } = Array.Empty<ProjectDiscoveryItem>();
    public IReadOnlyList<SimilarProjectItem> SimilarProjects { get; init; } = Array.Empty<SimilarProjectItem>();
    public IReadOnlyList<ProjectMergeProposalModel> MergeProposals { get; init; } = Array.Empty<ProjectMergeProposalModel>();
    public IReadOnlyList<ProjectMergeHistoryItem> History { get; init; } = Array.Empty<ProjectMergeHistoryItem>();
    public IReadOnlyList<RadarKind> Radars { get; init; } = Array.Empty<RadarKind>();
    public int SourceCount { get; init; }
    public int AiProcessedCount { get; init; }
    public int PotentialInnovationCount { get; init; }
}

public sealed class ProjectDiscoveryItem
{
    public int DiscoveryId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public RadarKind Radar { get; init; }
    public string SourceUrl { get; init; } = string.Empty;
    public DateTime? PublishedAt { get; init; }
    public DateTime DiscoveredAt { get; init; }
    public double Similarity { get; init; }
    public DiscoveryStatus Status { get; init; }
    public ProcessingStatus ProcessingStatus { get; init; }
    public bool AIProcessed { get; init; }
    public bool IsPotentialInnovation { get; init; }
    public int? AIConfidence { get; init; }
    public bool IsDemo { get; init; }
}

public sealed class SimilarProjectItem
{
    public int ProjectId { get; init; }
    public string Title { get; init; } = string.Empty;
    public double Similarity { get; init; }
    public string Reason { get; init; } = string.Empty;
    public int DiscoveryCount { get; init; }
    public int SourceCount { get; init; }
    public IReadOnlyList<RadarKind> Radars { get; init; } = Array.Empty<RadarKind>();
    public bool HasPendingProposal { get; init; }
}

public sealed class ProjectMergeProposalModel
{
    public int Id { get; init; }
    public int SourceProjectId { get; init; }
    public string SourceTitle { get; init; } = string.Empty;
    public int TargetProjectId { get; init; }
    public string TargetTitle { get; init; } = string.Empty;
    public double Similarity { get; init; }
    public string Signals { get; init; } = string.Empty;
    public MergeProposalStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
}

public sealed class ProjectMergeHistoryItem
{
    public int Id { get; init; }
    public int SourceProjectId { get; init; }
    public int TargetProjectId { get; init; }
    public string SourceTitle { get; init; } = string.Empty;
    public string TargetTitle { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public string Actor { get; init; } = string.Empty;
    public DateTime ChangedAt { get; init; }
}
