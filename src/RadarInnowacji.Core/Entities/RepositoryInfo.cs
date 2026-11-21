namespace RadarInnowacji.Core.Entities;

/// <summary>
/// Metadane repozytorium kodu (GitHub, GitLab) powiązane 1:1 z odkryciem.
/// Pola z listy wyników aktualizowane są przy każdym skanie; szczegóły (README, języki)
/// pobierane są osobno, z własnym budżetem zapytań.
/// </summary>
public sealed class RepositoryInfo
{
    public int Id { get; set; }
    public int DiscoveryId { get; set; }
    public Discovery Discovery { get; set; } = null!;

    public string Platform { get; set; } = string.Empty;
    /// <summary>Identyfikator projektu w API platformy (GitHub: owner/name, GitLab: liczbowe ID).</summary>
    public string ProjectId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    public int Stars { get; set; }
    public int Forks { get; set; }
    public int? OpenIssues { get; set; }
    public string? License { get; set; }
    public string? HomepageUrl { get; set; }
    /// <summary>Link do demonstracji: homepage albo link z README wyglądający na demo.</summary>
    public string? DemoUrl { get; set; }
    public string? DefaultBranch { get; set; }
    public string? ReadmePath { get; set; }
    public bool IsFork { get; set; }
    public bool IsArchived { get; set; }

    /// <summary>Języki i ich udział w procentach, JSON: {"Python": 81.5, ...}.</summary>
    public string Languages { get; set; } = "{}";
    public bool? HasReadme { get; set; }
    public int? ReadmeLength { get; set; }

    public DateTime MetadataUpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DetailsFetchedAt { get; set; }
    public string? DetailsError { get; set; }
}
