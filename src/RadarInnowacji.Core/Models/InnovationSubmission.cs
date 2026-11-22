using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Models;

/// <summary>Formularz zgłoszenia innowacji przez instytucję lub twórcę.</summary>
public sealed class InnovationSubmission
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Problem { get; set; }
    public string? Solution { get; set; }
    public InnovationKind Kind { get; set; } = InnovationKind.Nieokreslona;
    public string Institution { get; set; } = string.Empty;
    public InstitutionType InstitutionType { get; set; } = InstitutionType.Nieznany;
    public string? Region { get; set; }
    public ProjectStatus Stage { get; set; } = ProjectStatus.Unknown;
    public string? Creators { get; set; }
    public string? Link { get; set; }
    public string? ContactEmail { get; set; }
    public bool ContactConsent { get; set; }
}

public sealed class SubmissionResult
{
    public bool Success { get; init; }
    public int? ProjectId { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

/// <summary>Formularz pozycji katalogu odbiorców.</summary>
public sealed class RecipientForm
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public RecipientType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Sectors { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public List<DevelopmentPath> Paths { get; set; } = [];
    public ProjectStatus MinimumStage { get; set; } = ProjectStatus.Unknown;
    public string? Region { get; set; }
    public string? Website { get; set; }
    public bool IsActive { get; set; } = true;
}
