using System.Text.Json;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Adapters;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Tests;

public class RepositoryQueryTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 15, 30, 0, DateTimeKind.Utc);

    private sealed class FixedTime(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc);
    }

    [Fact]
    public void Normalize_CzysciDuplikatyCudzyslowyIZakresDni()
    {
        var query = new RepositoryQuery
        {
            Keywords = ["  nowa   metoda ", "Nowa metoda", "\"prototyp\"", ""],
            Topics = ["Computer Vision", "IoT!"],
            CreatedWithinDays = 9999
        }.Normalize();

        Assert.Equal(["nowa metoda", "prototyp"], query.Keywords);
        Assert.Equal(["computer-vision", "iot"], query.Topics);
        Assert.Equal(RepositoryQuery.MaxDays, query.CreatedWithinDays);
    }

    [Fact]
    public void ParseList_DzieliPoPrzecinkachSrednikachINowychLiniach()
    {
        Assert.Equal(["a", "b c", "d"], RepositoryQuery.ParseList("a, b c;\n d,,"));
    }

    [Fact]
    public void BuildGitHub_ZapytanieNaKazdeSlowoITematSortowanePoDacie()
    {
        var query = new RepositoryQuery { Keywords = ["proof of concept"], Topics = ["robotics"], CreatedWithinDays = 30 };

        var urls = RepositorySearchUrlBuilder.BuildGitHub("https://api.github.com", query, Now);

        Assert.Equal(2, urls.Count);
        var first = Uri.UnescapeDataString(urls[0]);
        Assert.StartsWith("https://api.github.com/search/repositories?q=", urls[0]);
        Assert.Contains("\"proof of concept\" in:name,description,topics,readme", first);
        Assert.Contains("created:2026-10-22T00:00:00Z..2026-11-21T15:30:00Z", first);
        Assert.Contains("fork:false archived:false", first);
        Assert.Contains("sort=updated&order=desc&per_page=100&page=1", first);
        Assert.DoesNotContain("stars", first);
        Assert.Contains("topic:robotics", Uri.UnescapeDataString(urls[1]));
    }

    [Fact]
    public void BuildGitHub_JezykiMnozaZapytaniaAleZLimitem()
    {
        var query = new RepositoryQuery
        {
            Keywords = Enumerable.Range(1, 20).Select(i => $"slowo{i}").ToList(),
            Languages = ["Python", "Rust", "C#"]
        };

        var urls = RepositorySearchUrlBuilder.BuildGitHub("https://github.com", query, Now);

        Assert.Equal(RepositorySearchUrlBuilder.MaxTermsPerScan, urls.Count);
        Assert.Contains("language:Python", Uri.UnescapeDataString(urls[0]));
        Assert.Contains("language:Rust", Uri.UnescapeDataString(urls[1]));
    }

    [Theory]
    [InlineData("https://github.com", "https://api.github.com")]
    [InlineData("https://api.github.com/search/repositories?q=x", "https://api.github.com")]
    [InlineData("https://github.firma.pl", "https://github.firma.pl/api/v3")]
    [InlineData("nie-url", "https://api.github.com")]
    public void GitHubApiBase_RozpoznajeInstancje(string url, string expected) =>
        Assert.Equal(expected, RepositorySearchUrlBuilder.GitHubApiBase(url));

    [Theory]
    [InlineData("https://gitlab.com", "https://gitlab.com/api/v4")]
    [InlineData("https://gitlab.com/api/v4/projects?search=x", "https://gitlab.com/api/v4")]
    [InlineData("https://gitlab.uczelnia.edu.pl:8443/", "https://gitlab.uczelnia.edu.pl:8443/api/v4")]
    public void GitLabApiBase_RozpoznajeInstancje(string url, string expected) =>
        Assert.Equal(expected, RepositorySearchUrlBuilder.GitLabApiBase(url));

    [Fact]
    public void BuildGitLab_ParametryPublicznychNowychProjektow()
    {
        var query = new RepositoryQuery { Keywords = ["nowa metoda"], Topics = ["iot"], Languages = ["Python"], CreatedWithinDays = 7 };

        var urls = RepositorySearchUrlBuilder.BuildGitLab("https://gitlab.uczelnia.edu.pl", query, Now);

        Assert.Equal(2, urls.Count);
        Assert.StartsWith("https://gitlab.uczelnia.edu.pl/api/v4/projects?search=nowa%20metoda&", urls[0]);
        Assert.Contains("visibility=public&archived=false", urls[0]);
        Assert.Contains("order_by=id&sort=desc&per_page=100", urls[0]);
        Assert.Contains("last_activity_after=2026-11-14T00%3A00%3A00Z", urls[0]);
        Assert.DoesNotContain("created_after", urls[0]);
        Assert.Contains("with_programming_language=Python", urls[0]);
        Assert.Contains("/projects?topic=iot&", urls[1]);
    }

    [Fact]
    public async Task GitHubAdapter_WTrybieZapytaniaWywolujeZbudowaneAdresyIDeduplikuje()
    {
        var source = SourceTestFactory.Create(SourceType.GitHubApi, "https://api.github.com");
        source.Configuration = SourceConfiguration.Create(
            "abc123",
            new RepositoryQuery { Keywords = ["prototyp"], Topics = ["iot"] });
        var requested = new List<string>();
        var handler = new FakeHttpMessageHandler(request =>
        {
            requested.Add(request.RequestUri!.ToString());
            Assert.Equal("abc123", request.Headers.Authorization?.Parameter);
            return FakeHttpMessageHandler.Json(
                """{"items":[{"id":11,"full_name":"org/iot-probe","html_url":"https://github.com/org/iot-probe"}]}""");
        });
        var adapter = new GitHubSourceAdapter(new HttpClient(handler), new FixedTime(Now));

        var items = await adapter.FetchAsync(source);

        Assert.Equal(2, requested.Count);
        Assert.All(requested, url => Assert.StartsWith("https://api.github.com/search/repositories?q=", url));
        Assert.Single(items);
    }

    [Fact]
    public async Task GitLabAdapter_WTrybieZapytaniaUzywaInstancjiZAdresuZrodla()
    {
        var source = SourceTestFactory.Create(SourceType.GitLabApi, "https://gitlab.uczelnia.edu.pl");
        source.Configuration = SourceConfiguration.Create(null, new RepositoryQuery { Keywords = ["robot"] });
        string? requested = null;
        var adapter = new GitLabSourceAdapter(new HttpClient(new FakeHttpMessageHandler(request =>
        {
            requested = request.RequestUri!.ToString();
            return FakeHttpMessageHandler.Json("""[{"id":5,"name":"robot","web_url":"https://gitlab.uczelnia.edu.pl/a/robot"}]""");
        })), new FixedTime(Now));

        var items = await adapter.FetchAsync(source);

        Assert.StartsWith("https://gitlab.uczelnia.edu.pl/api/v4/projects?search=robot&", requested);
        Assert.Equal("5", Assert.Single(items).ExternalId);
    }

    [Fact]
    public async Task SourceService_ZapisujeZapytanieITokenWKonfiguracji()
    {
        using var database = new SqliteTestDatabase();
        var service = new SourceService(database.Context);

        var source = await service.CreateAsync(
            "GitHub – prototypy",
            "https://api.github.com",
            RadarKind.Repozytorium,
            SourceType.GitHubApi,
            60,
            true,
            "tok",
            repositoryQuery: new RepositoryQuery { Keywords = ["prototyp", "prototyp"] });

        using var configuration = JsonDocument.Parse(source.Configuration);
        Assert.Equal("tok", configuration.RootElement.GetProperty("token").GetString());
        var query = SourceConfiguration.ReadRepositoryQuery(source.Configuration);
        Assert.NotNull(query);
        Assert.Equal(["prototyp"], query.Keywords);
    }

    [Fact]
    public async Task SourceService_EdycjaZapytaniaZachowujeToken()
    {
        using var database = new SqliteTestDatabase();
        var service = new SourceService(database.Context);
        var source = await service.CreateAsync(
            "GitLab", "https://gitlab.com", RadarKind.Repozytorium, SourceType.GitLabApi, 60, true, "tok",
            repositoryQuery: new RepositoryQuery { Keywords = ["a1"] });

        await service.UpdateAsync(source.Id, true, 60,
            repositoryQuery: new RepositoryQuery { Topics = ["robotics"], CreatedWithinDays = 14 });

        var updated = await service.GetAsync(source.Id);
        using var configuration = JsonDocument.Parse(updated!.Configuration);
        Assert.Equal("tok", configuration.RootElement.GetProperty("token").GetString());
        var query = SourceConfiguration.ReadRepositoryQuery(updated.Configuration)!;
        Assert.Empty(query.Keywords);
        Assert.Equal(["robotics"], query.Topics);
        Assert.Equal(14, query.CreatedWithinDays);
    }

    [Fact]
    public async Task SourceService_OdrzucaPusteZapytanieIZapytanieDlaInnegoTypu()
    {
        using var database = new SqliteTestDatabase();
        var service = new SourceService(database.Context);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            "Pusty", "https://api.github.com", RadarKind.Repozytorium, SourceType.GitHubApi, 60, true, null,
            repositoryQuery: new RepositoryQuery { Keywords = ["  ", "\"\""] }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            "RSS", "https://example.org/feed.xml", RadarKind.Wiadomosci, SourceType.Rss, 60, true, null,
            repositoryQuery: new RepositoryQuery { Keywords = ["x"] }));
    }
}
