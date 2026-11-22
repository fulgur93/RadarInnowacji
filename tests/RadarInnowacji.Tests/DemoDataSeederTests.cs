using Microsoft.EntityFrameworkCore;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Infrastructure.Database;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Tests;

public class DemoDataSeederTests
{
    [Fact]
    public async Task LoadAsync_Wstawia28OznaczonychOdkrycIDaneDlaCzterechRadarow()
    {
        using var database = new SqliteTestDatabase();
        var db = database.Context;
        var seeder = new DemoDataSeeder(db);

        var loaded = await seeder.LoadAsync();

        Assert.Equal(28, loaded);
        Assert.True(await seeder.HasDemoDataAsync());
        Assert.Equal(28, await db.Discoveries.CountAsync(x => x.IsDemo));
        Assert.Equal(10, await db.Sources.CountAsync(x => x.IsDemo));
        Assert.Equal(8, await db.Categories.CountAsync(x => x.IsDemo));
        Assert.Equal(10, await db.InnovationProjects.CountAsync(x => x.IsDemo));
        Assert.Equal(10, await db.ScanHistory.CountAsync(x => x.IsDemo));
        Assert.Equal(4, await db.AiAnalyses.CountAsync(x => x.IsDemo));

        // Zgłoszenia instytucji nie są radarem skanującym – dane demo obejmują cztery radary.
        Assert.Equal(Enum.GetValues<RadarKind>().Count(x => x != RadarKind.Zgloszenia),
            await db.Sources.Select(x => x.Radar).Distinct().CountAsync());
        Assert.Equal(9, await db.Discoveries.CountAsync(x => x.IsDemo && x.Source.Radar == RadarKind.Uczelnia));
        Assert.Equal(9, await db.Discoveries.CountAsync(x => x.IsDemo && x.Source.Radar == RadarKind.Repozytorium));
        Assert.Equal(9, await db.Discoveries.CountAsync(x => x.IsDemo && x.Source.Radar == RadarKind.Wiadomosci));
        Assert.Equal(1, await db.Discoveries.CountAsync(x => x.IsDemo && x.Source.Radar == RadarKind.Crowdfunding));

        Assert.True(await db.Discoveries.AllAsync(x => x.IsDemo &&
            x.SourceUrl.StartsWith("https://example.org/demo/")));
    }

    [Fact]
    public async Task LoadAsync_DrugieWolanieNieDuplikujeDanych()
    {
        using var database = new SqliteTestDatabase();
        var db = database.Context;
        var seeder = new DemoDataSeeder(db);

        Assert.Equal(28, await seeder.LoadAsync());
        Assert.Equal(0, await seeder.LoadAsync());

        Assert.Equal(28, await db.Discoveries.CountAsync());
        Assert.Equal(10, await db.Sources.CountAsync());
    }

    [Fact]
    public async Task ClearAsync_UsuwaTylkoDaneDemo()
    {
        using var database = new SqliteTestDatabase();
        var db = database.Context;

        var realSource = new Source
        {
            Name = "Źródło rzeczywiste",
            Url = "https://example.org/real",
            Radar = RadarKind.Uczelnia,
            SourceType = SourceType.Rss,
            FetchMethod = FetchMethod.Rss,
            IsDemo = false
        };
        db.Sources.Add(realSource);
        await db.SaveChangesAsync();

        db.Discoveries.Add(new Discovery
        {
            Title = "Rzeczywiste odkrycie",
            Description = "Wpis nieoznaczony jako dane demonstracyjne.",
            SourceId = realSource.Id,
            SourceUrl = "https://example.org/real/1",
            IsDemo = false
        });
        await db.SaveChangesAsync();

        var seeder = new DemoDataSeeder(db);
        Assert.Equal(28, await seeder.LoadAsync());

        var removed = await seeder.ClearAsync();

        Assert.Equal(10, removed);
        Assert.False(await seeder.HasDemoDataAsync());
        Assert.Equal(0, await db.Sources.CountAsync(x => x.IsDemo));
        Assert.Equal(0, await db.Discoveries.CountAsync(x => x.IsDemo));
        Assert.Equal(0, await db.Categories.CountAsync());
        Assert.Equal(0, await db.InnovationProjects.CountAsync());
        Assert.Equal(0, await db.DiscoveryProjects.CountAsync());
        Assert.Equal(0, await db.ProjectCategories.CountAsync());
        Assert.Equal(0, await db.ScanHistory.CountAsync());
        Assert.Equal(0, await db.AiAnalyses.CountAsync());
        Assert.Equal(1, await db.Sources.CountAsync(x => !x.IsDemo));
        Assert.Equal(1, await db.Discoveries.CountAsync(x => !x.IsDemo));
    }
}
