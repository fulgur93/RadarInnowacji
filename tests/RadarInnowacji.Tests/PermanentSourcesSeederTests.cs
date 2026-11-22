using Microsoft.EntityFrameworkCore;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Tests;

public class PermanentSourcesSeederTests
{
    [Fact]
    public async Task SeedAsync_CzystaBaza_DodajeRzeczywisteZrodla()
    {
        using var database = new SqliteTestDatabase();
        var db = database.Context;
        var seeder = new PermanentSourcesSeeder(db);

        var added = await seeder.SeedAsync();

        Assert.Equal(PermanentSourcesSeeder.Definitions.Count, added);
        Assert.Equal(PermanentSourcesSeeder.Definitions.Count, await db.Sources.CountAsync());
        Assert.True(await db.Sources.AllAsync(x => !x.IsDemo && x.IsActive));
        var allSources = await db.Sources.ToListAsync();
        Assert.True(allSources.All(x =>
            x.FetchMethod == SourceDefaults.FetchMethodFor(x.SourceType)));

        var kickstarter = await db.Sources.SingleAsync(
            x => x.SourceType == SourceType.KickstarterApi);
        Assert.Equal(RadarKind.Crowdfunding, kickstarter.Radar);
        Assert.Equal("https://www.kickstarter.com", kickstarter.Url);
        var query = SourceConfiguration.ReadKickstarterQuery(kickstarter.Configuration);
        Assert.NotNull(query);
        Assert.NotEmpty(query.Keywords);
        Assert.All(KickstarterQuery.DefaultKeywords, keyword => Assert.Contains(keyword, query.Keywords));
        Assert.Contains(16, query.CategoryIds);
        Assert.Equal(KickstarterQuery.DefaultState, query.State);

        var uczelnie = await db.Sources
            .Where(x => x.Radar == RadarKind.Uczelnia)
            .ToListAsync();
        Assert.Equal(3, uczelnie.Count);
        Assert.True(uczelnie.All(x => x.SourceType == SourceType.HtmlPage && x.Url.StartsWith("https://")));
        Assert.Contains(uczelnie, x => x.Url.Contains("agh.edu.pl"));
        Assert.Contains(uczelnie, x => x.Url.Contains("pg.edu.pl"));
        Assert.Contains(uczelnie, x => x.Url.Contains("amu.edu.pl"));

        var wiadomosci = await db.Sources
            .Where(x => x.Radar == RadarKind.Wiadomosci)
            .ToListAsync();
        Assert.Equal(5, wiadomosci.Count);
        Assert.True(wiadomosci.All(x => x.SourceType == SourceType.Rss && x.Url.StartsWith("https://")));
        Assert.Contains(wiadomosci, x => x.Url.Contains("spidersweb.pl"));
        Assert.Contains(wiadomosci, x => x.Url.Contains("naukawpolsce.pl"));
        Assert.Contains(wiadomosci, x => x.Url.Contains("sciencedaily.com"));
        Assert.Contains(wiadomosci, x => x.Url.Contains("sciencealert.com"));
        Assert.Contains(wiadomosci, x => x.Url.Contains("newscientist.com"));
    }

    [Fact]
    public async Task SeedAsync_PowtorneUruchomienieNieDuplikujeZrodel()
    {
        using var database = new SqliteTestDatabase();
        var db = database.Context;
        var seeder = new PermanentSourcesSeeder(db);

        var count = PermanentSourcesSeeder.Definitions.Count;
        Assert.Equal(count, await seeder.SeedAsync());
        Assert.Equal(0, await seeder.SeedAsync());

        Assert.Equal(count, await db.Sources.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_NieNadpisujeZmianUzytkownika()
    {
        using var database = new SqliteTestDatabase();
        var db = database.Context;
        var definition = PermanentSourcesSeeder.Definitions[0];
        db.Sources.Add(new Source
        {
            Name = definition.Name,
            Url = "https://example.org/wlasny",
            Radar = RadarKind.Wiadomosci,
            SourceType = SourceType.Rss,
            FetchMethod = FetchMethod.Rss,
            IsActive = false,
            IsDemo = false
        });
        await db.SaveChangesAsync();

        var seeder = new PermanentSourcesSeeder(db);
        var added = await seeder.SeedAsync();

        Assert.Equal(PermanentSourcesSeeder.Definitions.Count - 1, added);
        var existing = await db.Sources.SingleAsync(x => x.Name == definition.Name);
        Assert.Equal("https://example.org/wlasny", existing.Url);
        Assert.Equal(RadarKind.Wiadomosci, existing.Radar);
        Assert.False(existing.IsActive);
    }
}
