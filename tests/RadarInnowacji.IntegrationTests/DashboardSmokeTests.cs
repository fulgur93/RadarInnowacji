using System.Net;

namespace RadarInnowacji.IntegrationTests;

public class DashboardSmokeTests : IClassFixture<RadarWebApplicationFactory>
{
    private readonly RadarWebApplicationFactory _factory;

    public DashboardSmokeTests(RadarWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Dashboard_Zwraca200IPolskiNaglowek()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Radar Innowacji", html);
        Assert.Contains("Dashboard", html);
    }

    [Fact]
    public async Task NieznanaStrona_Zwraca404IPolskiKomunikat()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/nie-istnieje");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Nie znaleziono strony", html);
    }
}
