namespace RadarInnowacji.Core.Entities;

/// <summary>Wektor projektu oraz przypisanie do grupy wykrytej podczas ostatniego grupowania.</summary>
public sealed class ProjectEmbedding
{
    public int InnovationProjectId { get; set; }
    public InnovationProject Project { get; set; } = null!;

    /// <summary>JSON z tablicą liczb zmiennoprzecinkowych.</summary>
    public string Vector { get; set; } = "[]";
    public string Model { get; set; } = string.Empty;
    public int? ClusterId { get; set; }
    public double ClusterSimilarity { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
