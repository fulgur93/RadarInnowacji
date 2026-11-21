using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Interfaces;

public interface ISourceService
{
    Task<IReadOnlyList<Source>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Source?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<Source> CreateAsync(
        string name,
        string url,
        RadarKind radar,
        SourceType sourceType,
        int scanIntervalMinutes,
        bool isActive,
        string? token,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(int id, bool isActive, int scanIntervalMinutes, CancellationToken cancellationToken = default);
}
