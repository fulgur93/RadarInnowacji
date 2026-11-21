using System.Net;
using System.Text.Json;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Adapters;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Tests;

public class KickstarterSearchTests
{
    [Fact]
    public void KickstarterQuery_NormalizujeZapytanieIBudujeAdresy()
    {
        var query = new KickstarterQuery
        {
            Keywords = [" robot ", "robot", "nowa metoda", new string('x', 200)],
            CategoryIds = [16, 16, -1, 7],
            State = "nieprawidlowy",
            Sort = "popularity",
            LaunchedWithinDays = 999
        }.Normalize();

        Assert.Equal(["robot", "nowa metoda", new string('x', 100)], query.Keywords);
        Assert.Equal([16, 7], query.CategoryIds);
        Assert.Equal(KickstarterQuery.DefaultState, query.State);
        Assert.Equal(KickstarterQuery.DefaultSort, query.Sort);
        Assert.Equal(KickstarterQuery.MaxDays, query.LaunchedWithinDays);

        var urls = KickstarterSearchUrlBuilder.SearchUrls("https://www.kickstarter.com", query);
        Assert.Equal(5, urls.Count);
        Assert.Contains("format=json", urls[0]);
        Assert.Contains("term=robot", urls[0]);
        Assert.Contains("state=live", urls[0]);
        Assert.Contains("sort=newest", urls[0]);
        Assert.Contains("page=1", urls[0]);
        Assert.Contains("category_id=16", urls[3]);

        var window = query.WindowStart(new DateTime(2026, 11, 22, 18, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2025, 11, 22, 0, 0, 0, DateTimeKind.Utc), window);
    }

    [Fact]
    public void SourceConfiguration_ZapisujeZapytanieKickstarteraIZachowujeToken()
    {
        var query = new KickstarterQuery { Keywords = ["czujnik"], CategoryIds = [16] };
        var configuration = SourceConfiguration.Create("token-1", null, query);

        var read = SourceConfiguration.ReadKickstarterQuery(configuration);
        Assert.NotNull(read);
        Assert.Equal(["czujnik"], read.Keywords);
        Assert.Equal([16], read.CategoryIds);

        var updated = SourceConfiguration.WithKickstarterQuery(
            configuration, query with { Keywords = ["robot"] });
        using var document = JsonDocument.Parse(updated);
        Assert.Equal("token-1", document.RootElement.GetProperty("token").GetString());
        Assert.Equal("robot", SourceConfiguration.ReadKickstarterQuery(updated)!.Keywords[0]);
    }

    [Fact]
    public async Task KickstarterAdapter_ParsujeKampanieIFiltrujeStareWyniki()
    {
        var source = SourceTestFactory.Create(
            RadarInnowacji.Core.Enums.SourceType.KickstarterApi,
            "https://www.kickstarter.com",
            "Kickstarter — technologia");
        source.Configuration = SourceConfiguration.Create(null, null, new KickstarterQuery
        {
            Keywords = ["robot"],
            CategoryIds = [16],
            LaunchedWithinDays = 30
        });

        var now = DateTime.UtcNow;
        var json = $$$"""
            {
              "total_hits": 2,
              "projects": [
                {
                  "id": 12345,
                  "slug": "smart-czujnik",
                  "name": "Inteligentny czujnik",
                  "blurb": "Prototyp czujnika jakości powietrza",
                  "creator": {"name": "Anna Nowak"},
                  "category": {"name": "Gadżety", "parent_name": "Technologia"},
                  "location": {"display_name": "Kraków, Polska"},
                  "state": "live",
                  "launched_at": {{{Epoch(now.AddDays(-5))}}},
                  "deadline": {{{Epoch(now.AddDays(20))}}},
                  "goal": 10000,
                  "pledged": 12500.5,
                  "backers_count": 120,
                  "urls": {"web": {"project": "https://www.kickstarter.com/projects/maker/smart-czujnik"}}
                },
                {
                  "id": 999,
                  "slug": "stary-projekt",
                  "name": "Stary projekt",
                  "blurb": "Poza oknem dat",
                  "launched_at": {{{Epoch(now.AddDays(-100))}}}
                }
              ]
            }
            """;
        var requests = new List<Uri>();
        var handler = new FakeHttpMessageHandler(request =>
        {
            requests.Add(request.RequestUri!);
            Assert.Contains("/discover/advanced?format=json", request.RequestUri!.ToString());
            return FakeHttpMessageHandler.Json(json);
        });
        var adapter = new KickstarterSourceAdapter(new HttpClient(handler));

        var result = await adapter.FetchResultAsync(source, 50);

        var item = Assert.Single(result.Items);
        Assert.Equal("12345", item.ExternalId);
        Assert.Equal("Inteligentny czujnik", item.Title);
        Assert.Equal("Prototyp czujnika jakości powietrza", item.Description);
        Assert.Contains("Kategoria: Gadżety", item.Content);
        Assert.Contains("Lokalizacja: Kraków, Polska", item.Content);
        Assert.Equal("Anna Nowak", item.Author);
        Assert.Equal("https://www.kickstarter.com/projects/maker/smart-czujnik", item.SourceUrl);
        Assert.Contains("Technologia", item.Technologies);
        Assert.Equal(Epoch(now.AddDays(-5)), Epoch(item.PublishedAt!.Value));
        Assert.Contains(requests, x => x.Query.Contains("term=robot"));
        Assert.Contains(requests, x => x.Query.Contains("category_id=16"));
        Assert.True(result.IsComplete);
        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task KickstarterAdapter_StronicujeIDeduplikujeKampanie()
    {
        var source = SourceTestFactory.Create(
            RadarInnowacji.Core.Enums.SourceType.KickstarterApi,
            "https://www.kickstarter.com");
        source.Configuration = SourceConfiguration.Create(null, null, new KickstarterQuery
        {
            Keywords = ["sensor"],
            CategoryIds = []
        });

        var launched = Epoch(DateTime.UtcNow.AddDays(-2));
        var pages = new Dictionary<int, string>
        {
            [1] = $$$"""{"total_hits":30,"projects":[{"id":1,"name":"Pierwszy","blurb":"a","launched_at":{{{launched}}}},{"id":2,"name":"Drugi","blurb":"b","launched_at":{{{launched}}}}]}""",
            [2] = $$$"""{"total_hits":30,"projects":[{"id":2,"name":"Duplikat","blurb":"b","launched_at":{{{launched}}}},{"id":3,"name":"Trzeci","blurb":"c","launched_at":{{{launched}}}}]}"""
        };
        var requests = new List<Uri>();
        var handler = new FakeHttpMessageHandler(request =>
        {
            requests.Add(request.RequestUri!);
            var page = int.Parse(request.RequestUri!.Query.Split("page=")[1].Split('&')[0]);
            return FakeHttpMessageHandler.Json(pages[page]);
        });
        var adapter = new KickstarterSourceAdapter(new HttpClient(handler));

        var result = await adapter.FetchResultAsync(source, 2);

        Assert.Equal(2, requests.Count);
        Assert.Equal(["1", "2"], result.Items.Select(x => x.ExternalId));
        Assert.False(result.IsComplete);
        Assert.Contains("limit 2", result.Warning);
    }

    [Fact]
    public async Task KickstarterAdapter_Blad403ZawieraJasnyKomunikat()
    {
        var source = SourceTestFactory.Create(
            RadarInnowacji.Core.Enums.SourceType.KickstarterApi,
            "https://www.kickstarter.com");
        source.Configuration = SourceConfiguration.Create(null, null, new KickstarterQuery { Keywords = ["robot"] });
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var adapter = new KickstarterSourceAdapter(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => adapter.FetchResultAsync(source, 10));

        Assert.Contains("HTTP 403", exception.Message);
        Assert.Contains("blokuje automatyczne zapytania", exception.Message);
    }

    [Fact]
    public async Task SourceService_TworzyZrodloKickstarterZPoprawnymZapytaniem()
    {
        using var database = new SqliteTestDatabase();
        var service = new SourceService(database.Context);

        var source = await service.CreateAsync(
            "Kickstarter — technologia",
            "https://www.kickstarter.com",
            RadarInnowacji.Core.Enums.RadarKind.Crowdfunding,
            RadarInnowacji.Core.Enums.SourceType.KickstarterApi,
            60,
            true,
            null,
            kickstarterQuery: new KickstarterQuery { Keywords = ["prototyp"], CategoryIds = [16] });

        Assert.Equal(RadarInnowacji.Core.Enums.FetchMethod.Api, source.FetchMethod);
        Assert.Equal(["prototyp"], SourceConfiguration.ReadKickstarterQuery(source.Configuration)!.Keywords);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            "Kickstarter — puste",
            "https://www.kickstarter.com",
            RadarInnowacji.Core.Enums.RadarKind.Crowdfunding,
            RadarInnowacji.Core.Enums.SourceType.KickstarterApi,
            60,
            true,
            null,
            kickstarterQuery: new KickstarterQuery()));

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            "Kickstarter — zly typ",
            "https://www.kickstarter.com",
            RadarInnowacji.Core.Enums.RadarKind.Crowdfunding,
            RadarInnowacji.Core.Enums.SourceType.Rss,
            60,
            true,
            null,
            kickstarterQuery: new KickstarterQuery { Keywords = ["prototyp"] }));
    }

    private static long Epoch(DateTime value) =>
        new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeSeconds();
}
