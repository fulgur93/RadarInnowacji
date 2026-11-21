using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Core.Interfaces;

/// <summary>Adapter umie pobrać materiały z jednego typu źródła zewnętrznego.</summary>
public interface ISourceAdapter
{
    SourceType SourceType { get; }

    Task<IReadOnlyList<SourceItem>> FetchAsync(Source source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pobiera najwyżej <paramref name="maxItems"/> materiałów i informuje, czy wynik jest pełny.
    /// Domyślnie: jedno wywołanie <see cref="FetchAsync"/>; adaptery repozytoriów nadpisują to
    /// stronicowaniem, obsługą limitów API i skanowaniem przyrostowym.
    /// </summary>
    async Task<SourceFetchResult> FetchResultAsync(
        Source source,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        var items = await FetchAsync(source, cancellationToken);
        return new SourceFetchResult(items.Take(maxItems).ToList(), true, null, null);
    }
}
