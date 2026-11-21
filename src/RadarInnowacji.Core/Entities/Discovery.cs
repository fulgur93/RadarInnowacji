using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Entities;

public sealed class Discovery
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Tekst źródłowy ograniczony do ContentLimits.MaxRawContentLength.</summary>
    public string RawContent { get; set; } = string.Empty;
    public int ContentLength { get; set; }
    public bool IsContentTruncated { get; set; }

    public int SourceId { get; set; }
    public Source Source { get; set; } = null!;
    public string SourceUrl { get; set; } = string.Empty;
    public string? ExternalId { get; set; }

    public DateTime? PublishedAt { get; set; }
    public DateTime? EarliestKnownDate { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;

    public string? ContentHash { get; set; }
    public string? Author { get; set; }
    public string? Organization { get; set; }
    public string Languages { get; set; } = "pl";
    public string Technologies { get; set; } = "[]";
    public string CategoryIds { get; set; } = "[]";

    public bool AIProcessed { get; set; }
    public bool IsPotentialInnovation { get; set; }
    public int? AIConfidence { get; set; }
    public string? AIAnalysis { get; set; }
    public ProcessingStatus ProcessingStatus { get; set; } = ProcessingStatus.Pending;
    public DiscoveryStatus Status { get; set; } = DiscoveryStatus.Candidate;
    public bool IsDemo { get; set; }

    public ICollection<DiscoveryProject> ProjectLinks { get; set; } = new List<DiscoveryProject>();
    public ICollection<AiAnalysisRecord> AiAnalyses { get; set; } = new List<AiAnalysisRecord>();
}
