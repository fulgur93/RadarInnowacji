using RadarInnowacji.Core.Entities;

namespace RadarInnowacji.Core.Interfaces;

public interface ISourceAdapterResolver
{
    ISourceAdapter? Resolve(Source source);
}
