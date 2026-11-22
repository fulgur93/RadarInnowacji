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
        Assert.True(allSources.All(x => x.FetchMethod == SourceDefaults.FetchMethodFor(x.SourceType)));
        Assert.True(allSources.All(x => !string.IsNullOrWhiteSpace(x.Url) && x.Url.StartsWith("https://")));

        // No English-only/default foreign news feeds in permanent seed
        Assert.DoesNotContain(allSources, x => x.Url.Contains("sciencedaily.com", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(allSources, x => x.Url.Contains("sciencealert.com", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(allSources, x => x.Url.Contains("newscientist.com", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(allSources, x => x.SourceType == SourceType.KickstarterApi);

        // Key Polish institutions/sources are present
        Assert.True(allSources.Any(x => x.Url.Contains("ncbr", StringComparison.OrdinalIgnoreCase)), string.Join("|", allSources.Select(x=>x.Url)));
        Assert.Contains(allSources, x => x.Url.Contains("ncn.gov.pl", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(allSources, x => x.Url.Contains("parp.gov.pl", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(allSources, x => x.Url.Contains("naukawpolsce.pl", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(allSources, x => x.Url.Contains("polakpotrafi.pl", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(allSources, x => x.Radar == RadarKind.Uczelnia);
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
