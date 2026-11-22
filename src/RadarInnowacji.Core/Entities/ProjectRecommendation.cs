using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Entities;

/// <summary>Rekomendowana ścieżka wraz z uzasadnieniem, krokami i miernikami skuteczności.</summary>
public sealed class ProjectRecommendation
{
    public int Id { get; set; }
    public int ProjectAssessmentId { get; set; }
    public ProjectAssessment Assessment { get; set; } = null!;
    public DevelopmentPath Path { get; set; }
    public int Rank { get; set; }

    /// <summary>Reguły, które zadziałały, z wartościami (JSON: tablica tekstów).</summary>
    public string Justification { get; set; } = "[]";
}
