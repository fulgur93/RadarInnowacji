namespace RadarInnowacji.Core.Entities;

/// <summary>Projekt wchodzący w skład grupy, na podstawie której powstała propozycja kategorii.</summary>
public sealed class CategoryProposalProject
{
    public int CategoryProposalId { get; set; }
    public CategoryProposal Proposal { get; set; } = null!;

    public int InnovationProjectId { get; set; }
    public InnovationProject Project { get; set; } = null!;

    public double Similarity { get; set; }
}
