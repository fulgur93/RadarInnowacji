using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Entities;

/// <summary>
/// Ocena kryterium nadana przez eksperta. Zastąpione wpisy nie są usuwane (<see cref="IsActive"/> = false),
/// dzięki czemu zachowana jest historia decyzji.
/// </summary>
public sealed class CriterionOverride
{
    public int Id { get; set; }
    public int InnovationProjectId { get; set; }
    public InnovationProject Project { get; set; } = null!;
    public AssessmentCriterion Criterion { get; set; }
    public int Score { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string Author { get; set; } = "Ekspert";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
