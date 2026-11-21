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

        Assert.Equal(27, summary.TotalDiscoveries);
        Assert.Equal(27, summary.DemoCount);
        Assert.Equal(7, summary.ActiveSources);
        Assert.Equal(8, summary.CategoryCount);
        Assert.Equal(6, summary.LatestDiscoveries.Count);
        Assert.Equal(3, summary.RadarCounts.Count);
        Assert.All(summary.RadarCounts, radar => Assert.Equal(9, radar.Count));
        Assert.NotEmpty(summary.TopCategories);
        Assert.NotEmpty(summary.Activity);
        Assert.Equal(summary.TotalDiscoveries - summary.AiProcessedCount, summary.AiPendingCount);
    }
}
