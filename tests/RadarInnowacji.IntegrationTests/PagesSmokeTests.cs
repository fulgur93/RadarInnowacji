using System.Net;
using Microsoft.Extensions.DependencyInjection;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.IntegrationTests;

public class PagesSmokeTests : IClassFixture<RadarWebApplicationFactory>
{
    private readonly RadarWebApplicationFactory _factory;

    public PagesSmokeTests(RadarWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/", "Dashboard")]
    [InlineData("/odkrycia", "Wszystkie odkrycia")]
    [InlineData("/radar-uczelni", "Radar uczelni")]
    [InlineData("/radar-repozytoriow", "Radar repozytoriów")]
    [InlineData("/radar-wiadomosci", "Radar wiadomości")]
    [InlineData("/zrodla", "Źródła danych")]
    [InlineData("/historia", "Historia skanowania")]
    [InlineData("/kategorie", "Kategorie i trendy")]
    [InlineData("/ustawienia", "Ustawienia")]
    public async Task Strona_Zwraca200IPolskiNaglowek(string route, string heading)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(route);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(heading, html);
    }

    [Fact]
    public async Task SzczegolyNieistniejacegoOdkrycia_ZwracaPolskiKomunikat()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/odkrycia/999999");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Nie znaleziono odkrycia", html);
    }

    [Fact]
    public async Task Zrodla_ZawieraFormularzDodawaniaIPrzyciskSkanowania()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/zrodla");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Nowe źródło", html);
        Assert.Contains("Dodaj źródło", html);
        Assert.Contains("Token API", html);

        using var scope = _factory.Services.CreateScope();
        var sourceService = scope.ServiceProvider.GetRequiredService<ISourceService>();
        var source = await sourceService.CreateAsync(
            "Testowe źródło API",
            "https://example.org/integration.xml",
            RadarKind.Wiadomosci,
            SourceType.Rss,
            60,
            true,
            null);

        try
        {
            var page = await client.GetAsync("/zrodla");
            var pageHtml = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Contains("Testowe źródło API", pageHtml);
            Assert.Contains("Skanuj", pageHtml);
        }
        finally
        {
            var db = scope.ServiceProvider.GetRequiredService<RadarDbContext>();
            db.Sources.Remove(db.Sources.Single(x => x.Id == source.Id));
            await db.SaveChangesAsync();
        }
    }
}
