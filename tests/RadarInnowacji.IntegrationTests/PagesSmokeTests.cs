using System.Net;

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
}
