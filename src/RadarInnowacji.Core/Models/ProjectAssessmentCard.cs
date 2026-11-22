using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Models;

/// <summary>Wynik prostej operacji wraz z komunikatem dla użytkownika.</summary>
public sealed record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message) => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}

/// <summary>Karta oceny projektu: profil, ocena, rekomendacje, odbiorcy i przebieg procesu.</summary>
public sealed class ProjectAssessmentCard
{
    public int ProjectId { get; init; }
    public string Title { get; init; } = string.Empty;
    public bool IsDemo { get; init; }
    public ProjectProfile Profile { get; init; } = new();

    /// <summary>Wartości wywnioskowane, gdy profil nie został uzupełniony (np. rodzaj z analizy AI).</summary>
    public IReadOnlyList<string> InferredNotes { get; init; } = Array.Empty<string>();
    public double TotalScore { get; init; }
    public int Confidence { get; init; }
    public int Completeness { get; init; }
    public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();
    public DateTime AssessedAt { get; init; }
    public IReadOnlyList<CriterionCardItem> Criteria { get; init; } = Array.Empty<CriterionCardItem>();
    public IReadOnlyList<RecommendationCardItem> Recommendations { get; init; } = Array.Empty<RecommendationCardItem>();
    public IReadOnlyList<string> Sectors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RecipientMatch> RecipientMatches { get; init; } = Array.Empty<RecipientMatch>();
    public IReadOnlyList<SimilarInstitutionItem> SimilarInstitutions { get; init; } = Array.Empty<SimilarInstitutionItem>();
    public PipelineStage Stage { get; init; }
    public IReadOnlyList<PipelineStage> AllowedStages { get; init; } = Array.Empty<PipelineStage>();
    public IReadOnlyList<PipelineEventItem> Events { get; init; } = Array.Empty<PipelineEventItem>();
    public IReadOnlyList<ReferralItem> Referrals { get; init; } = Array.Empty<ReferralItem>();
    public IReadOnlyList<AssessmentHistoryItem> History { get; init; } = Array.Empty<AssessmentHistoryItem>();
}

public sealed class ProjectProfile
{
    public string? Institution { get; set; }
    public InstitutionType InstitutionType { get; set; }
    public InnovationKind InnovationKind { get; set; }
    public ProjectStatus Stage { get; set; }
    public string? Region { get; set; }
    public string? Creators { get; set; }
    public string? Problem { get; set; }
    public string? Solution { get; set; }
    public string? ContactEmail { get; set; }
    public bool ContactConsent { get; set; }
}

public sealed class CriterionCardItem
{
    public AssessmentCriterion Criterion { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public int AutomaticScore { get; init; }
    public int FinalScore { get; init; }
    public int Weight { get; init; }
    public CriterionOrigin Origin { get; init; }
    public IReadOnlyList<string> Signals { get; init; } = Array.Empty<string>();
    public string? ExpertComment { get; init; }
}

public sealed class RecommendationCardItem
{
    public DevelopmentPath Path { get; init; }
    public int Rank { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NextSteps { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Metrics { get; init; } = Array.Empty<string>();
}

/// <summary>Instytucja, która w bazie pracuje nad podobnym problemem – potencjalny partner lub odbiorca.</summary>
public sealed class SimilarInstitutionItem
{
    public string Institution { get; init; } = string.Empty;
    public int ProjectId { get; init; }
    public string ProjectTitle { get; init; } = string.Empty;
    public double Similarity { get; init; }
}

public sealed record PipelineEventItem(PipelineStage From, PipelineStage To, string Comment, string Actor, DateTime CreatedAt);

public sealed record ReferralItem(int RecipientId, string RecipientName, string Note, DateTime CreatedAt);

public sealed record AssessmentHistoryItem(DateTime CreatedAt, double TotalScore, int Confidence, string Actor, string Paths);

/// <summary>Wiersz lejka innowacji i eksportu.</summary>
public sealed class FunnelItem
{
    public int ProjectId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Institution { get; init; }
    public InstitutionType InstitutionType { get; init; }
    public InnovationKind Kind { get; init; }
    public ProjectStatus Stage { get; init; }
    public PipelineStage PipelineStage { get; init; }
    public double? TotalScore { get; init; }
    public int? Confidence { get; init; }
    public IReadOnlyList<DevelopmentPath> Paths { get; init; } = Array.Empty<DevelopmentPath>();
    public int SourceCount { get; init; }
    public bool IsDemo { get; init; }
    public DateTime FirstDiscoveredAt { get; init; }
}

public sealed class FunnelFilter
{
    public PipelineStage? Stage { get; set; }
    public DevelopmentPath? Path { get; set; }
    public InnovationKind? Kind { get; set; }
    public InstitutionType? InstitutionType { get; set; }
    public double? MinScore { get; set; }
    public bool OnlyAssessed { get; set; }
    public string? Text { get; set; }
    public int Limit { get; set; } = 200;
}

public sealed class FunnelModel
{
    public IReadOnlyDictionary<PipelineStage, int> StageCounts { get; init; } = new Dictionary<PipelineStage, int>();
    public IReadOnlyDictionary<DevelopmentPath, int> PathCounts { get; init; } = new Dictionary<DevelopmentPath, int>();
    public int TotalProjects { get; init; }
    public int AssessedProjects { get; init; }
    public int Referrals { get; init; }
    public IReadOnlyList<FunnelItem> Items { get; init; } = Array.Empty<FunnelItem>();
}
