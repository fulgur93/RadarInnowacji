using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Core.Interfaces;

/// <summary>Adapter umie pobrać materiały z jednego typu źródła zewnętrznego.</summary>
public interface ISourceAdapter
{
    SourceType SourceType { get; }

    Task<IReadOnlyList<SourceItem>> FetchAsync(Source source, CancellationToken cancellationToken = default);
}
