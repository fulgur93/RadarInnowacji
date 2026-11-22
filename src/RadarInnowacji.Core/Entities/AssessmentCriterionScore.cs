using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Entities;

/// <summary>Ocena jednego kryterium w migawce oceny wraz z uzasadnieniem.</summary>
public sealed class AssessmentCriterionScore
{
    public int Id { get; set; }
    public int ProjectAssessmentId { get; set; }
    public ProjectAssessment Assessment { get; set; } = null!;
    public AssessmentCriterion Criterion { get; set; }

    /// <summary>Ocena wyliczona regułami (0–5).</summary>
    public int AutomaticScore { get; set; }

    /// <summary>Ocena użyta w wyniku: eksperta, jeśli ją podał, w przeciwnym razie automatyczna.</summary>
    public int FinalScore { get; set; }
    public int Weight { get; set; }
    public CriterionOrigin Origin { get; set; }
    public string Justification { get; set; } = string.Empty;
    public string? ExpertComment { get; set; }
}
