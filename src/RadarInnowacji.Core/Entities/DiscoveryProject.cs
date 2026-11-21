namespace RadarInnowacji.Core.Entities;

/// <summary>Relacja wiele-do-wielu między odkryciem a projektem innowacyjnym.</summary>
public sealed class DiscoveryProject
{
    public int DiscoveryId { get; set; }
    public Discovery Discovery { get; set; } = null!;

    public int InnovationProjectId { get; set; }
    public InnovationProject Project { get; set; } = null!;

    public double Similarity { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
