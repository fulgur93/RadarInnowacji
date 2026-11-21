namespace RadarInnowacji.Core.Models;

/// <summary>Pojedynczy materiał pobrany przez adapter ze źródła zewnętrznego.</summary>
public sealed record SourceItem(
    string ExternalId,
    string Title,
    string Description,
    string Content,
    string SourceUrl,
    DateTime? PublishedAt,
    DateTime? LastActivityAt,
    string? Author,
    string? Organization,
    IReadOnlyList<string> Technologies)
{
    /// <summary>Metadane repozytorium kodu; null dla źródeł innych niż GitHub/GitLab.</summary>
    public RepositoryMetadata? Repository { get; init; }
}
