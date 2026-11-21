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
}
