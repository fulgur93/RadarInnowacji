using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Entities;

public sealed class InnovationProject
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Problem { get; set; }
    public string? ProposedSolution { get; set; }
    public ProjectStatus ProjectStatus { get; set; } = ProjectStatus.Unknown;

    public DateTime FirstDiscoveredAt { get; set; } = DateTime.UtcNow;
    public DateTime? EarliestKnownDate { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public double FreshnessScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDemo { get; set; }

    // Profil innowacji (Etap 9) – uzupełniany w zgłoszeniu albo przez eksperta.
    public string? Institution { get; set; }
    public InstitutionType InstitutionType { get; set; } = InstitutionType.Nieznany;
    public InnovationKind InnovationKind { get; set; } = InnovationKind.Nieokreslona;
    public string? Region { get; set; }
    public string? Creators { get; set; }

    /// <summary>Kontakt do twórcy – zapisywany wyłącznie przy <see cref="ContactConsent"/>.</summary>
    public string? ContactEmail { get; set; }
    public bool ContactConsent { get; set; }
    public PipelineStage PipelineStage { get; set; } = PipelineStage.Wykryta;
    public DateTime? ProfileUpdatedAt { get; set; }

    /// <summary>
    /// Po akceptacji połączenia projekt źródłowy wskazuje projekt docelowy.
    /// Sam projekt i powiązane odkrycia nie są usuwane – zachowana jest historia.
    /// </summary>
    public int? MergedIntoProjectId { get; set; }
    public InnovationProject? MergedInto { get; set; }

    public ICollection<DiscoveryProject> DiscoveryLinks { get; set; } = new List<DiscoveryProject>();
    public ICollection<ProjectCategory> CategoryLinks { get; set; } = new List<ProjectCategory>();
    public ICollection<ProjectAssessment> Assessments { get; set; } = new List<ProjectAssessment>();
    public ICollection<ProjectPipelineEvent> PipelineEvents { get; set; } = new List<ProjectPipelineEvent>();
}
