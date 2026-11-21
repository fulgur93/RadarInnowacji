using Microsoft.EntityFrameworkCore;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Database;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Tests;

public class DiscoveryServiceTests
{
    [Fact]
    public async Task GetPageAsync_ZwracaWszystkieDaneDemo()
    {
        using var database = new SqliteTestDatabase();
        var service = await CreateServiceAsync(database);

        var page = await service.GetPageAsync(new DiscoveryFilter { PageSize = 100 });

        Assert.Equal(27, page.TotalCount);
        Assert.Equal(27, page.Items.Count);
        Assert.All(page.Items, item => Assert.True(item.IsDemo));
        Assert.Equal(3, page.Items.Select(x => x.Radar).Distinct().Count());
    }

    [Theory]
    [InlineData(RadarKind.Uczelnia, 9)]
    [InlineData(RadarKind.Repozytorium, 9)]
    [InlineData(RadarKind.Wiadomosci, 9)]
    public async Task GetPageAsync_FiltrujeWedlutRadaru(RadarKind radar, int expectedCount)
    {
        using var database = new SqliteTestDatabase();
        var service = await CreateServiceAsync(database);

        var page = await service.GetPageAsync(new DiscoveryFilter
        {
            Radar = radar,
            PageSize = 100
        });

        Assert.Equal(expectedCount, page.TotalCount);
        Assert.All(page.Items, item => Assert.Equal(radar, item.Radar));
    }

    [Fact]
    public async Task GetPageAsync_FiltrujeWedlutZrodla()
    {
        using var database = new SqliteTestDatabase();
        var service = await CreateServiceAsync(database);
        var source = await database.Context.Sources
            .SingleAsync(x => x.Name == "AGH – Repozytorium (DEMO)");

        var page = await service.GetPageAsync(new DiscoveryFilter
        {
            SourceId = source.Id,
            PageSize = 100
        });

        Assert.Equal(3, page.TotalCount);
        Assert.All(page.Items, item => Assert.Equal(source.Name, item.SourceName));
    }

    [Fact]
    public async Task GetPageAsync_FiltrujeWedlutKategorii()
    {
        using var database = new SqliteTestDatabase();
        var service = await CreateServiceAsync(database);
        var category = await database.Context.Categories
            .SingleAsync(x => x.Name == "Internet Rzeczy");

        var page = await service.GetPageAsync(new DiscoveryFilter
        {
            CategoryId = category.Id,
            PageSize = 100
        });

        Assert.Equal(9, page.TotalCount);
        Assert.All(page.Items, item => Assert.Contains(category.Name, item.Categories));
    }

    [Fact]
    public async Task GetPageAsync_FiltrujeWedlutZakresuDatOdkrycia()
    {
        using var database = new SqliteTestDatabase();
        var service = await CreateServiceAsync(database);
        var from = DateTime.UtcNow.AddDays(-21);
        var to = DateTime.UtcNow;

        var page = await service.GetPageAsync(new DiscoveryFilter
        {
            DateField = DateField.DiscoveredAt,
            DateFrom = from,
            DateTo = to,
            PageSize = 100
        });

        Assert.Equal(9, page.TotalCount);
        Assert.All(page.Items, item => Assert.InRange(item.DiscoveredAt, from, to));
    }

    [Fact]
    public async Task GetDetailsAsync_ZwracaPowiazaneDaneIOdswiezStatus()
    {
        using var database = new SqliteTestDatabase();
        var service = await CreateServiceAsync(database);
        var discovery = await database.Context.Discoveries
            .SingleAsync(x => x.Title == "System wspomagania diagnostyki obrazowej");

        var details = await service.GetDetailsAsync(discovery.Id);

        Assert.NotNull(details);
        Assert.Equal("UMK – RUMAK (DEMO)", details.SourceName);
        Assert.Contains("Technologie medyczne", details.Categories);
        Assert.Contains("Sztuczna inteligencja", details.Categories);
        Assert.NotEmpty(details.AiHistory);
        Assert.NotEmpty(details.SimilarDiscoveries);

        await service.MarkVerifiedAsync(discovery.Id);

        Assert.Equal(DiscoveryStatus.Verified,
            await database.Context.Discoveries
                .Where(x => x.Id == discovery.Id)
                .Select(x => x.Status)
                .SingleAsync());
    }

    private static async Task<DiscoveryService> CreateServiceAsync(SqliteTestDatabase database)
    {
        var seeder = new DemoDataSeeder(database.Context);
        await seeder.LoadAsync();
        return new DiscoveryService(database.Context);
    }
}
