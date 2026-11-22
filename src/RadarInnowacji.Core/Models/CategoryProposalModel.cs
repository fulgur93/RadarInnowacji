using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Models;

/// <summary>Propozycja kategorii przygotowana do wyświetlenia w interfejsie.</summary>
public sealed class CategoryProposalModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> Keywords { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Projects { get; set; } = Array.Empty<string>();
    public double Similarity { get; set; }
    public CategoryProposalStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
