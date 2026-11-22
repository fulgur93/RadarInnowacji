using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>
/// Analiza trendów oparta na unikalnych projektach (nie na liczbie pobranych materiałów).
/// Wyniki zawsze zawierają informację o liczbie monitorowanych źródeł i częstotliwości skanowania.
/// </summary>
public sealed class TrendService(RadarDbContext db, IOptions<RadarOptions> options) : ITrendService
{
    public async Task<TrendAnalysisModel> GetTrendsAsync(int months = 6, CancellationToken cancellationToken = default)
    {
        var count = Math.Clamp(months, 1, 24);
        var periodTo = FirstDayOfMonth(DateTime.UtcNow);
        var periodFrom = periodTo.AddMonths(-count);
        var categories = await db.Categories
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        var projects = await LoadProjectsAsync(cancellationToken);
        var active = projects.Where(x => x.MergedIntoProjectId == null).ToList();
        var monitoredSources = await db.Sources.CountAsync(x => x.IsActive, cancellationToken);
        var scanInterval = Math.Max(1, options.Value.Scanning.IntervalMinutes);

        var points = new List<CategoryTrendPoint>();
        for (var index = 0; index < count; index++)
        {
            var start = periodFrom.AddMonths(index);
            var end = start.AddMonths(1);
            foreach (var category in categories)
            {
                var newProjects = active.Count(project =>
                    InPeriod(project.FirstDiscoveredAt, start, end) &&
                    project.CategoryLinks.Any(link => link.CategoryId == category.Id));
                if (newProjects == 0)
                {
                    continue;
                }

                points.Add(new CategoryTrendPoint(
                    start.ToString("yyyy-MM"),
                    category.Id,
                    category.Name,
                    newProjects));
            }
        }

        var currentStart = periodTo.AddMonths(-1);
        var summaries = new List<CategoryTrendSummary>();
        foreach (var category in categories)
        {
            var currentProjects = active
                .Where(x => InPeriod(x.FirstDiscoveredAt, currentStart, periodTo) &&
                            x.CategoryLinks.Any(link => link.CategoryId == category.Id))
                .ToList();
            var previousCount = active.Count(x =>
                InPeriod(x.FirstDiscoveredAt, currentStart.AddMonths(-1), currentStart) &&
                x.CategoryLinks.Any(link => link.CategoryId == category.Id));
            var allProjects = active
                .Where(x => x.CategoryLinks.Any(link => link.CategoryId == category.Id))
                .ToList();
            var isNew = (category.CreatedAt >= periodFrom && category.CreatedAt < periodTo) ||
                        (allProjects.Count > 0 && allProjects.All(x => x.FirstDiscoveredAt >= periodFrom));
            summaries.Add(new CategoryTrendSummary
            {
                CategoryId = category.Id,
                Name = category.Name,
                CurrentCount = currentProjects.Count,
                PreviousCount = previousCount,
                GrowthPercent = previousCount == 0
                    ? null
                    : Math.Round(100.0 * (currentProjects.Count - previousCount) / previousCount, 1),
                IsNew = isNew,
                TotalProjects = allProjects.Count,
                SourceCount = currentProjects
                    .SelectMany(x => x.DiscoveryLinks.Select(link => link.Discovery.SourceId))
                    .Distinct()
                    .Count()
            });
        }

        var totalNew = active.Count(x => InPeriod(x.FirstDiscoveredAt, periodFrom, periodTo));
        return new TrendAnalysisModel
        {
            PeriodFrom = periodFrom,
            PeriodTo = periodTo,
            PeriodLabel = $"{periodFrom:yyyy-MM} – {periodTo.AddMonths(-1):yyyy-MM}",
            TotalNewProjects = totalNew,
            TotalProjects = active.Count,
            MonitoredSources = monitoredSources,
            ScanIntervalMinutes = scanInterval,
            Points = points,
            Categories = summaries.OrderByDescending(x => x.CurrentCount).ToList(),
            MostActive = summaries
                .Where(x => x.CurrentCount > 0)
                .OrderByDescending(x => x.CurrentCount)
                .ThenByDescending(x => x.GrowthPercent ?? 0)
                .Take(5)
                .ToList(),
            FastestGrowing = summaries
                .Where(x => x.GrowthPercent is not null)
                .OrderByDescending(x => x.GrowthPercent!.Value)
                .ThenByDescending(x => x.CurrentCount)
                .Take(5)
                .ToList(),
            NewCategories = summaries.Where(x => x.IsNew).OrderBy(x => x.Name).ToList(),
            Notes = BuildNotes(monitoredSources, scanInterval)
        };
    }

    public async Task<IReadOnlyList<CategoryComparison>> GetCategoryComparisonAsync(
        int months = 6,
        CancellationToken cancellationToken = default)
    {
        var count = Math.Clamp(months, 1, 24);
        var periodTo = FirstDayOfMonth(DateTime.UtcNow);
        var currentStart = periodTo.AddMonths(-1);
        var categories = await db.Categories
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var projects = (await LoadProjectsAsync(cancellationToken))
            .Where(x => x.MergedIntoProjectId == null)
            .ToList();

        return categories.Select(category =>
        {
            var categoryProjects = projects
                .Where(x => x.CategoryLinks.Any(link => link.CategoryId == category.Id))
                .ToList();
            return new CategoryComparison
            {
                CategoryId = category.Id,
                Name = category.Name,
                ProjectCount = categoryProjects.Count,
                DiscoveryCount = categoryProjects.Sum(x => x.DiscoveryLinks.Count),
                SourceCount = categoryProjects
                    .SelectMany(x => x.DiscoveryLinks.Select(link => link.Discovery.SourceId))
                    .Distinct()
                    .Count(),
                NewProjectsLastPeriod = categoryProjects.Count(x => InPeriod(x.FirstDiscoveredAt, currentStart, periodTo)),
                AverageFreshness = categoryProjects.Count == 0
                    ? 0
                    : Math.Round(categoryProjects.Average(x => x.FreshnessScore), 1)
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<ProjectActivityPoint>> GetActivityAsync(
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        var count = Math.Clamp(days, 1, 365);
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-count + 1);
        var projects = (await LoadProjectsAsync(cancellationToken))
            .Where(x => x.MergedIntoProjectId == null)
            .ToList();
        var discoveryDates = await db.Discoveries
            .Where(x => x.DiscoveredAt >= start && x.DiscoveredAt < end.AddDays(1))
            .Select(x => x.DiscoveredAt)
            .ToListAsync(cancellationToken);

        return Enumerable.Range(0, count)
            .Select(index => start.AddDays(index))
            .Select(day => new ProjectActivityPoint(
                day.ToString("yyyy-MM-dd"),
                projects.Count(x => x.FirstDiscoveredAt.Date == day),
                discoveryDates.Count(x => x.Date == day)))
            .ToList();
    }

    private async Task<List<InnovationProject>> LoadProjectsAsync(CancellationToken cancellationToken) =>
        await db.InnovationProjects
            .Include(x => x.DiscoveryLinks).ThenInclude(x => x.Discovery).ThenInclude(x => x.Source)
            .Include(x => x.CategoryLinks).ThenInclude(x => x.Category)
            .ToListAsync(cancellationToken);

    private static IReadOnlyList<string> BuildNotes(int monitoredSources, int scanInterval) =>
    [
        "Trendy liczą unikalne projekty, a nie liczbę pobranych materiałów.",
        $"Liczba odkryć zależy od monitorowanych źródeł ({monitoredSources}) oraz częstotliwości skanowania (co {scanInterval} min).",
        "Więcej pobranych materiałów nie oznacza automatycznie wzrostu rzeczywistej innowacyjności.",
        "Dane z bieżącego miesiąca są prezentowane jako pełne ostatnie okno miesięczne, aby uniknąć wypaczeń przez niepełny miesiąc."
    ];

    private static bool InPeriod(DateTime date, DateTime from, DateTime to) => date >= from && date < to;

    private static DateTime FirstDayOfMonth(DateTime value) =>
        new(value.Year, value.Month, 1, 0, 0, 0, DateTimeKind.Utc);
}
