using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Core.Interfaces;

/// <summary>Adapter repozytoriów, który umie dociągnąć szczegóły projektu (README, języki, licencję).</summary>
public interface IRepositoryDetailsAdapter
{
    SourceType SourceType { get; }

    /// <summary>
    /// Pobiera szczegóły kolejnych repozytoriów w ramach jednego budżetu zapytań.
    /// Błąd pojedynczego repozytorium trafia do wyniku; limit API kończy partię (StopReason).
    /// </summary>
    Task<RepositoryDetailsBatch> FetchDetailsAsync(
        Source source,
        IReadOnlyList<RepositoryInfo> repositories,
        CancellationToken cancellationToken = default);
}

public sealed record RepositoryDetailsResult(RepositoryInfo Repository, RepositoryDetails? Details, string? Error);

public sealed record RepositoryDetailsBatch(IReadOnlyList<RepositoryDetailsResult> Results, string? StopReason);
