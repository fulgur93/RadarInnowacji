using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;

namespace RadarInnowacji.Infrastructure.Adapters;

public sealed class SourceAdapterResolver : ISourceAdapterResolver
{
    private readonly IReadOnlyDictionary<SourceType, ISourceAdapter> _adapters;

    public SourceAdapterResolver(IEnumerable<ISourceAdapter> adapters)
    {
        var map = new Dictionary<SourceType, ISourceAdapter>();
        foreach (var adapter in adapters)
        {
            map[adapter.SourceType] = adapter;
        }

        if (map.TryGetValue(SourceType.Rss, out var rss))
        {
            map[SourceType.Atom] = rss;
        }

        _adapters = map;
    }

    public ISourceAdapter? Resolve(Source source) =>
        _adapters.TryGetValue(source.SourceType, out var adapter) ? adapter : null;
}
