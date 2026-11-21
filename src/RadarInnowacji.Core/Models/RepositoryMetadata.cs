namespace RadarInnowacji.Core.Models;

/// <summary>Metadane repozytorium dostępne bezpośrednio w liście wyników wyszukiwania.</summary>
public sealed record RepositoryMetadata(
    string Platform,
    string ProjectId,
    string FullName,
    int Stars,
    int Forks,
    int? OpenIssues,
    string? License,
    string? HomepageUrl,
    string? DefaultBranch,
    string? ReadmePath,
    bool IsFork,
    bool IsArchived);

/// <summary>Szczegóły repozytorium pobierane osobnymi zapytaniami.</summary>
/// <param name="Readme">Treść README (markdown) albo null, gdy repozytorium go nie ma.</param>
/// <param name="Languages">Język → udział w procentach.</param>
/// <param name="License">Licencja, jeśli lista wyników jej nie podała (GitLab).</param>
public sealed record RepositoryDetails(
    string? Readme,
    IReadOnlyDictionary<string, double> Languages,
    string? License,
    string? DefaultBranch);
