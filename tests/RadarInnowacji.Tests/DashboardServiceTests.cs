using RadarInnowacji.Core.Enums;
using RadarInnowacji.Infrastructure.Database;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Tests;

public class DashboardServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_AgregujeDaneDemo()
    {
        using var database = new SqliteTestDatabase();
        var seeder = new DemoDataSeeder(database.Context);
        await seeder.LoadAsync();

        var discoveryService = new DiscoveryService(database.Context);
        var dashboard = new DashboardService(database.Context, discoveryService);

        var summary = await dashboard.GetSummaryAsync();

        Assert.Equal(28, summary.TotalDiscoveries);
        Assert.Equal(28, summary.DemoCount);
        Assert.Equal(8, summary.ActiveSources);
        Assert.Equal(8, summary.CategoryCount);
        Assert.Equal(6, summary.LatestDiscoveries.Count);
        Assert.Equal(4, summary.RadarCounts.Count);
        Assert.Equal(9, summary.RadarCounts.Single(x => x.Radar == RadarKind.Uczelnia).Count);
        Assert.Equal(9, summary.RadarCounts.Single(x => x.Radar == RadarKind.Repozytorium).Count);
        Assert.Equal(9, summary.RadarCounts.Single(x => x.Radar == RadarKind.Wiadomosci).Count);
        Assert.Equal(1, summary.RadarCounts.Single(x => x.Radar == RadarKind.Crowdfunding).Count);
        Assert.NotEmpty(summary.TopCategories);
        Assert.NotEmpty(summary.Activity);
        Assert.Equal(summary.TotalDiscoveries - summary.AiProcessedCount, summary.AiPendingCount);
    }

    [Fact]
    public async Task GetSummaryAsync_AktywnoscToCiagle30DniZZerami()
    {
        using var database = new SqliteTestDatabase();
        await new DemoDataSeeder(database.Context).LoadAsync();
        var dashboard = new DashboardService(database.Context, new DiscoveryService(database.Context));

        var summary = await dashboard.GetSummaryAsync();

        Assert.Equal(30, summary.Activity.Count);
        Assert.Equal(DateTime.UtcNow.Date.ToString("yyyy-MM-dd"), summary.Activity[^1].Date);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(-29).ToString("yyyy-MM-dd"), summary.Activity[0].Date);
        var dni = summary.Activity.Select(x => DateTime.Parse(x.Date)).ToList();
        Assert.All(dni.Zip(dni.Skip(1)), para => Assert.Equal(1, (para.Second - para.First).TotalDays));
        var wOknie = database.Context.Discoveries.Count(x => x.DiscoveredAt >= DateTime.UtcNow.Date.AddDays(-29));
        Assert.Equal(wOknie, summary.Activity.Sum(x => x.Count));
    }
}
