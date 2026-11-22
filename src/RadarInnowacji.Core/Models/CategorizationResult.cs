namespace RadarInnowacji.Core.Models;

/// <summary>Podsumowanie jednego cyklu grupowania projektów i aktualizacji kategorii.</summary>
public sealed record CategorizationResult(
    int ProjectsAnalyzed,
    int EmbeddingsGenerated,
    int ClustersFound,
    int LinksAdded,
    int ProposalsCreated,
    string ProviderName,
    string Message);
