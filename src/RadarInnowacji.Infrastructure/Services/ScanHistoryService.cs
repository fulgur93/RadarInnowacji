using Microsoft.EntityFrameworkCore;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Infrastructure.Services;

public sealed class ScanHistoryService(RadarDbContext db) : IScanHistoryService
{
    public async Task<IReadOnlyList<ScanHistoryItem>> GetRecentAsync(
        int count = 50,
        CancellationToken cancellationToken = default) =>
        await db.ScanHistory
            .Include(x => x.Source)
            .OrderByDescending(x => x.StartedAt)
            .Take(Math.Clamp(count, 1, 200))
            .Select(x => new ScanHistoryItem(
                x.Id,
                x.Source.Name,
                x.Source.Radar,
                x.StartedAt,
                x.FinishedAt,
                x.Status,
                x.ItemsFound,
                x.ItemsNew,
                x.ErrorMessage,
                x.IsDemo))
            .ToListAsync(cancellationToken);
}
