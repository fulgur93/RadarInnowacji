using Microsoft.EntityFrameworkCore;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Infrastructure.Services;

public sealed class DashboardService(RadarDbContext db, IDiscoveryService discoveryService)
    : IDashboardService
{
    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var total = await db.Discoveries.CountAsync(cancellationToken);
        var potential = await db.Discoveries.CountAsync(x => x.IsPotentialInnovation, cancellationToken);
        var newLast7Days = await db.Discoveries.CountAsync(x => x.DiscoveredAt >= now.AddDays(-7), cancellationToken);
        var activeSources = await db.Sources.CountAsync(x => x.IsActive, cancellationToken);
        var categories = await db.Categories.CountAsync(x => x.IsActive, cancellationToken);
        var demoCount = await db.Discoveries.CountAsync(x => x.IsDemo, cancellationToken);
        var interesting = await db.Discoveries.CountAsync(x => x.Status == DiscoveryStatus.Interesting, cancellationToken);
        var verified = await db.Discoveries.CountAsync(x => x.Status == DiscoveryStatus.Verified, cancellationToken);
        var aiProcessed = await db.Discoveries.CountAsync(x => x.AIProcessed, cancellationToken);

        var latest = await discoveryService.GetPageAsync(
            new DiscoveryFilter { PageSize = 6, SortBy = DiscoverySort.DiscoveredAt },
            cancellationToken);

        var topCategoryRows = await db.Categories
            .Where(x => x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Name,
                Count = db.ProjectCategories.Count(p => p.CategoryId == x.Id)
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync(cancellationToken);

        var topCategories = topCategoryRows
            .Select(x => new CategoryCount(x.Id, x.Name, x.Count))
            .ToList();

        var discoveredDates = await db.Discoveries
            .Select(x => x.DiscoveredAt)
            .ToListAsync(cancellationToken);

        var activity = discoveredDates
            .GroupBy(x => x.Date)
            .Select(x => new ActivityPoint(x.Key.ToString("yyyy-MM-dd"), x.Count()))
            .OrderBy(x => x.Date)
            .TakeLast(30)
            .ToList();

        var radarCounts = await db.Discoveries
            .GroupBy(x => x.Source.Radar)
            .Select(x => new RadarCount(x.Key, x.Count()))
            .ToListAsync(cancellationToken);

        return new DashboardSummary
        {
            TotalDiscoveries = total,
            PotentialInnovations = potential,
            NewLast7Days = newLast7Days,
            ActiveSources = activeSources,
            CategoryCount = categories,
            DemoCount = demoCount,
            InterestingCount = interesting,
            VerifiedCount = verified,
            AiProcessedCount = aiProcessed,
            AiPendingCount = total - aiProcessed,
            LatestDiscoveries = latest.Items,
            TopCategories = topCategories,
            Activity = activity,
            RadarCounts = radarCounts
        };
    }
}
