using Microsoft.EntityFrameworkCore;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Infrastructure.Services;

public sealed class SourceService(RadarDbContext db) : ISourceService
{
    public async Task<IReadOnlyList<Source>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.Sources
            .Include(x => x.Discoveries)
            .OrderBy(x => x.Radar).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<Source?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await db.Sources.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task UpdateAsync(int id, bool isActive, int scanIntervalMinutes, CancellationToken cancellationToken = default)
    {
        var source = await db.Sources.FindAsync([id], cancellationToken);
        if (source is null)
        {
            return;
        }

        source.IsActive = isActive;
        source.ScanIntervalMinutes = Math.Clamp(scanIntervalMinutes, 5, 10080);
        await db.SaveChangesAsync(cancellationToken);
    }
}
