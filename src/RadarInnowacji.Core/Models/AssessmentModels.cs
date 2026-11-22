using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Models;

/// <summary>
/// Dane wejściowe karty oceny – zebrane z projektu, jego materiałów, analizy AI i bazy podobnych projektów.
/// Ocena jest funkcją wyłącznie tych danych, dzięki czemu jest powtarzalna i testowalna.
/// </summary>
public sealed class AssessmentInput
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Problem { get; init; }
    public string? Solution { get; init; }
    public InnovationKind Kind { get; init; }

    /// <summary>Skąd wiadomo, jaki to rodzaj innowacji (np. „profil projektu”, „analiza AI”).</summary>
    public string? KindSource { get; init; }
    public InstitutionType InstitutionType { get; init; }
    public string? Institution { get; init; }
    public ProjectStatus Stage { get; init; }
    public string? Region { get; init; }
    public string? Creators { get; init; }
    public bool HasContact { get; init; }
    public bool HasLink { get; init; }
    public bool? AiPotential { get; init; }
    public int? AiConfidence { get; init; }
    public string? AiNovelty { get; init; }
    public double MaxSimilarity { get; init; }
    public string? MostSimilarTitle { get; init; }
    public int SimilarInOtherInstitutions { get; init; }
    public IReadOnlyList<string> OtherInstitutions { get; init; } = Array.Empty<string>();
    public int DistinctSources { get; init; }
    public string? License { get; init; }
    public bool HasOpenLicense { get; init; }
    public bool HasDocumentation { get; init; }
    public DateTime? LastActivityAt { get; init; }
    public IReadOnlyList<string> Sectors { get; init; } = Array.Empty<string>();
    public DateTime Now { get; init; } = DateTime.UtcNow;
}

/// <summary>Wynik jednego kryterium: ocena 0–5 i sygnały, które do niej doprowadziły.</summary>
public sealed record CriterionResult(
    AssessmentCriterion Criterion,
    int Score,
    int Weight,
    IReadOnlyList<string> Signals);

public sealed class AssessmentOutcome
{
    public IReadOnlyList<CriterionResult> Criteria { get; init; } = Array.Empty<CriterionResult>();
    public double TotalScore { get; init; }

    /// <summary>Odsetek wypełnionych kluczowych informacji (0–100).</summary>
    public int Completeness { get; init; }
    public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();
    public int Confidence { get; init; }

    public int Score(AssessmentCriterion criterion) =>
        Criteria.FirstOrDefault(x => x.Criterion == criterion)?.Score ?? 0;
}

/// <summary>Rekomendowana ścieżka wraz z regułami, które zadziałały.</summary>
public sealed record RecommendationResult(DevelopmentPath Path, int Rank, IReadOnlyList<string> Reasons);

/// <summary>Opis ścieżki: następne kroki, mierniki skuteczności i typowi odbiorcy.</summary>
public sealed record DevelopmentPathInfo(
    DevelopmentPath Path,
    string Name,
    string Description,
    IReadOnlyList<string> NextSteps,
    IReadOnlyList<string> Metrics,
    IReadOnlyList<RecipientType> TypicalRecipients);

/// <summary>Opis reguły rekomendacji prezentowany na stronie „Metodyka oceny”.</summary>
public sealed record RecommendationRuleInfo(int Order, DevelopmentPath Path, string Condition);

/// <summary>Opis kryterium prezentowany w karcie oceny i na stronie „Metodyka oceny”.</summary>
public sealed record CriterionInfo(AssessmentCriterion Criterion, string Name, string Question, IReadOnlyList<string> Rules);

/// <summary>Dopasowanie odbiorcy z katalogu do projektu.</summary>
public sealed class RecipientMatch
{
    public int RecipientId { get; init; }
    public string Name { get; init; } = string.Empty;
    public RecipientType Type { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? Website { get; init; }
    public bool IsExample { get; init; }
    public int Score { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}

/// <summary>Profil projektu do dopasowania odbiorców.</summary>
public sealed class RecipientMatchInput
{
    public IReadOnlyList<string> Sectors { get; init; } = Array.Empty<string>();
    public string Text { get; init; } = string.Empty;
    public ProjectStatus Stage { get; init; }
    public InnovationKind Kind { get; init; }
    public string? Region { get; init; }
    public IReadOnlyList<DevelopmentPath> Paths { get; init; } = Array.Empty<DevelopmentPath>();
}
