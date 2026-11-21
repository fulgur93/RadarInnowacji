namespace RadarInnowacji.Core.Entities;

public sealed class ProjectCategory
{
    public int InnovationProjectId { get; set; }
    public InnovationProject Project { get; set; } = null!;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public double Confidence { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
