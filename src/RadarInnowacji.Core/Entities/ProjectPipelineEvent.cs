using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Entities;

/// <summary>Zmiana etapu procesu wraz z komentarzem – historia decyzji o innowacji.</summary>
public sealed class ProjectPipelineEvent
{
    public int Id { get; set; }
    public int InnovationProjectId { get; set; }
    public InnovationProject Project { get; set; } = null!;
    public PipelineStage FromStage { get; set; }
    public PipelineStage ToStage { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string Actor { get; set; } = "Użytkownik";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
