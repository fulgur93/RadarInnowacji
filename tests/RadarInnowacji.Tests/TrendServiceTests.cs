using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Tests;

public class TrendServiceTests
{
    [Fact]
    public async Task Trendy_LiczaUnikalneProjektyIoznaczajaZrodlaICzestotliwoscSkanowania()
    {
        using var database = new SqliteTestDatabase();
        var (current, previous, periodFrom) = Months();
        var source = SourceTestFactory.Create(SourceType.Rss, "https://example.org/feed", "Źródło testowe");
        database.Context.Sources.Add(source);
        var category = new Category
        {
            Name = "Czujniki",
            CreatedAt = periodFrom.AddMonths(-1),
            LastUpdatedAt = periodFrom.AddMonths(-1)
        };
        database.Context.Categories.Add(category);
        await database.Context.SaveChangesAsync();

        await AddProjectAsync(database, source, category, current.AddDays(5), "Projekt A");
        await AddProjectAsync(database, source, category, current.AddDays(7), "Projekt B");
        await AddProjectAsync(database, source, category, previous.AddDays(5), "Projekt C");
        await AddProjectAsync(database, source, category, periodFrom.AddDays(-10), "Projekt spoza okresu");

        var service = CreateService(database);
        var trends = await service.GetTrendsAsync(6);

        Assert.Equal(3, trends.TotalNewProjects);
        Assert.Equal(4, trends.TotalProjects);
        Assert.Equal(1, trends.MonitoredSources);
        Assert.Equal(15, trends.ScanIntervalMinutes);
        Assert.Equal(2, trends.Points.Count);
        Assert.Equal(2, Assert.Single(trends.Points, x => x.Period == current.ToString("yyyy-MM")).NewProjects);
        var summary = Assert.Single(trends.Categories);
        Assert.Equal(2, summary.CurrentCount);
        Assert.Equal(1, summary.PreviousCount);
        Assert.Equal(100, summary.GrowthPercent);
        Assert.False(summary.IsNew);
        Assert.Contains(trends.Notes, x => x.Contains("15 min"));
        Assert.Contains(trends.Notes, x => x.Contains("monitorowanych źródeł (1)"));
        Assert.Contains(trends.Notes, x => x.Contains("unikalne projekty"));
    }

    [Fact]
    public async Task PorownanieKategorii_LiczyProjektyOdkryciaIZrodlaOsobno()
    {
        using var database = new SqliteTestDatabase();
        var (current, _, _) = Months();
        var firstSource = SourceTestFactory.Create(SourceType.Rss, "https://a.example/feed", "Źródło A");
        var secondSource = SourceTestFactory.Create(SourceType.GitHubApi, "https://b.example/api", "Źródło B");
        database.Context.Sources.AddRange(firstSource, secondSource);
        var category = new Category { Name = "Czujniki" };
        var empty = new Category { Name = "Pusta kategoria" };
        database.Context.Categories.AddRange(category, empty);
        await database.Context.SaveChangesAsync();

        var first = await AddProjectAsync(database, firstSource, category, current.AddDays(2), "Projekt A");
        await AddDiscoveryAsync(database, first, secondSource, "Drugie odkrycie", current.AddDays(3));

        var comparison = await CreateService(database).GetCategoryComparisonAsync(6);

        var sensors = Assert.Single(comparison, x => x.Name == "Czujniki");
        Assert.Equal(1, sensors.ProjectCount);
        Assert.Equal(2, sensors.DiscoveryCount);
        Assert.Equal(2, sensors.SourceCount);
        Assert.Equal(1, sensors.NewProjectsLastPeriod);
        Assert.Equal(0, Assert.Single(comparison, x => x.Name == "Pusta kategoria").ProjectCount);
    }

    [Fact]
    public async Task Aktywnosc_ZwracaDaneDlaKazdegoDniaOkna()
    {
        using var database = new SqliteTestDatabase();
        var source = SourceTestFactory.Create(SourceType.Rss, "https://example.org/feed", "Źródło testowe");
        database.Context.Sources.Add(source);
        var category = new Category { Name = "Czujniki" };
        database.Context.Categories.Add(category);
        await database.Context.SaveChangesAsync();
        await AddProjectAsync(database, source, category, DateTime.UtcNow, "Projekt dzisiejszy");

        var activity = await CreateService(database).GetActivityAsync(7);

        Assert.Equal(7, activity.Count);
        Assert.Contains(activity, x => x.NewProjects >= 1);
        Assert.Contains(activity, x => x.NewDiscoveries >= 1);
    }

    private static (DateTime Current, DateTime Previous, DateTime PeriodFrom) Months()
    {
        // Bieżący, niepełny miesiąc jest pomijany – ostatnim oknem jest poprzedni pełny miesiąc.
        var now = DateTime.UtcNow;
        var periodTo = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var current = periodTo.AddMonths(-1);
        return (current, current.AddMonths(-1), periodTo.AddMonths(-6));
    }

    private static async Task<InnovationProject> AddProjectAsync(
        SqliteTestDatabase database,
        Source source,
        Category category,
        DateTime date,
        string title)
    {
        var project = new InnovationProject
        {
            Title = title,
            Description = "Opis projektu",
            FirstDiscoveredAt = date,
            CreatedAt = date,
            FreshnessScore = 70
        };
        database.Context.InnovationProjects.Add(project);
        await database.Context.SaveChangesAsync();
        database.Context.ProjectCategories.Add(new ProjectCategory
        {
            InnovationProjectId = project.Id,
            CategoryId = category.Id
        });
        await AddDiscoveryAsync(database, project, source, title, date);
        return project;
    }

    private static async Task AddDiscoveryAsync(
        SqliteTestDatabase database,
        InnovationProject project,
        Source source,
        string title,
        DateTime date)
    {
        var discovery = new Discovery
        {
            Title = title,
            Description = "Opis materiału",
            Source = source,
            SourceUrl = $"https://example.org/{Guid.NewGuid():N}",
            DiscoveredAt = date,
            PublishedAt = date,
            ProcessingStatus = ProcessingStatus.AiProcessed
        };
        database.Context.DiscoveryProjects.Add(new DiscoveryProject
        {
            Discovery = discovery,
            Project = project,
            Similarity = 1
        });
        await database.Context.SaveChangesAsync();
    }

    private static TrendService CreateService(SqliteTestDatabase database) => new(
        database.Context,
        Options.Create(new RadarOptions
        {
            Scanning = new ScanningOptions { IntervalMinutes = 15 },
            Projects = new ProjectOptions { TrendMonths = 6 }
        }));
}
