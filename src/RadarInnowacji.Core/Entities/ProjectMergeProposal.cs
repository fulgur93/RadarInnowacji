using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Entities;

/// <summary>
/// Propozycja połączenia dwóch projektów wykryta na podstawie sygnałów duplikacji.
/// Niejednoznaczne przypadki nigdy nie są łączone automatycznie – wymagają decyzji użytkownika.
/// </summary>
public sealed class ProjectMergeProposal
{
    public int Id { get; set; }
    public int SourceProjectId { get; set; }
    public InnovationProject SourceProject { get; set; } = null!;
    public int TargetProjectId { get; set; }
    public InnovationProject TargetProject { get; set; } = null!;

    /// <summary>Łączny, szacunkowy podobieństwo (0–1). Nie jest dowodem tożsamości projektów.</summary>
    public double Similarity { get; set; }

    /// <summary>Opis sygnałów (ExternalId, ContentHash, tytuł, opis, embedding).</summary>
    public string Signals { get; set; } = string.Empty;

    /// <summary>Stabilny podpis pary projektów – zapobiega powtarzaniu odrzuconych propozycji.</summary>
    public string Signature { get; set; } = string.Empty;

    public MergeProposalStatus Status { get; set; } = MergeProposalStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? DecisionNote { get; set; }
}
