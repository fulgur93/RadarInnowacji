using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Entities;

/// <summary>Automatycznie wykryta propozycja nowej kategorii; wymaga decyzji użytkownika.</summary>
public sealed class CategoryProposal
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>JSON z listą słów kluczowych.</summary>
    public string Keywords { get; set; } = "[]";

    /// <summary>Podpis grupy używany do unikania powtarzania tych samych propozycji.</summary>
    public string Signature { get; set; } = string.Empty;
    public double Similarity { get; set; }
    public CategoryProposalStatus Status { get; set; } = CategoryProposalStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    public ICollection<CategoryProposalProject> ProjectLinks { get; set; } = new List<CategoryProposalProject>();
}
