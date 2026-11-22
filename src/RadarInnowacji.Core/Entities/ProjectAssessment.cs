namespace RadarInnowacji.Core.Entities;

/// <summary>
/// Migawka oceny potencjału projektu. Nowa migawka powstaje tylko wtedy, gdy zmieniły się dane
/// wejściowe (skrót <see cref="InputHash"/>); poprzednie pozostają jako historia.
/// </summary>
public sealed class ProjectAssessment
{
    public int Id { get; set; }
    public int InnovationProjectId { get; set; }
    public InnovationProject Project { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Wynik ważony 0–100.</summary>
    public double TotalScore { get; set; }

    /// <summary>Pewność oceny 0–100 – zależy od kompletności informacji.</summary>
    public int Confidence { get; set; }
    public string InputHash { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public string Actor { get; set; } = "System";
    public ICollection<AssessmentCriterionScore> Criteria { get; set; } = new List<AssessmentCriterionScore>();
    public ICollection<ProjectRecommendation> Recommendations { get; set; } = new List<ProjectRecommendation>();
}
