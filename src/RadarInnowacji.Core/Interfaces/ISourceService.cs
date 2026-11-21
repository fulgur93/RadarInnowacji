using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;

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
        CancellationToken cancellationToken = default,
        RepositoryQuery? repositoryQuery = null,
        KickstarterQuery? kickstarterQuery = null);

    /// <param name="repositoryQuery">Nowe zapytanie radaru repozytoriów; null = bez zmian.</param>
    /// <param name="kickstarterQuery">Nowe zapytanie Kickstartera; null = bez zmian.</param>
    Task UpdateAsync(
        int id,
        bool isActive,
        int scanIntervalMinutes,
        CancellationToken cancellationToken = default,
        RepositoryQuery? repositoryQuery = null,
        KickstarterQuery? kickstarterQuery = null);
}
