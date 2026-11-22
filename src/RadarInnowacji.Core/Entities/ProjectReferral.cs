namespace RadarInnowacji.Core.Entities;

/// <summary>Skierowanie projektu do odbiorcy z katalogu.</summary>
public sealed class ProjectReferral
{
    public int Id { get; set; }
    public int InnovationProjectId { get; set; }
    public InnovationProject Project { get; set; } = null!;
    public int RecipientId { get; set; }
    public Recipient Recipient { get; set; } = null!;
    public string Note { get; set; } = string.Empty;
    public string Actor { get; set; } = "Użytkownik";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
