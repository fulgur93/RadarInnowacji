using RadarInnowacji.Core.Entities;

namespace RadarInnowacji.Core.Interfaces;

public interface ISourceService
{
    Task<IReadOnlyList<Source>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Source?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task UpdateAsync(int id, bool isActive, int scanIntervalMinutes, CancellationToken cancellationToken = default);
}
