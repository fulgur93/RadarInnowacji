using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Adapters;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Tests;

/// <summary>Stronicowanie, dzielenie zakresów dat, limity API i skanowanie przyrostowe (Etap 3, krok 2).</summary>
public class RepositoryPagingTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Regex CreatedRange = new(@"created:(\S+)\.\.(\S+?) ", RegexOptions.Compiled);

    private sealed class FixedTime(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc);
    }

    private sealed class RecordingDelay
    {
        public List<TimeSpan> Calls { get; } = [];

        public Task Invoke(TimeSpan wait, CancellationToken _)
        {
            Calls.Add(wait);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GitHub_PobieraKolejneStronyPoNaglowkuLink()
    {
        var source = GitHubSource(new RepositoryQuery { Keywords = ["prototyp"], CreatedWithinDays = 7 });
        var pages = new List<int>();
        var adapter = GitHub(request =>
        {
            var page = Page(request);
            pages.Add(page);
            var ids = Enumerable.Range((page - 1) * 100, page < 3 ? 100 : 50);
            var response = GitHubJson(250, ids);
            if (page < 3)
            {
                response.Headers.Add("Link", $"<https://api.github.com/search/repositories?page={page + 1}>; rel=\"next\"");
            }

            return response;
        });

        var result = await adapter.FetchResultAsync(source, 300);

        Assert.Equal([1, 2, 3], pages);
        Assert.Equal(250, result.Items.Count);
        Assert.True(result.IsComplete);
        Assert.Equal(Now, CursorDate(Assert.Single(result.Cursors!).Value));
    }

    [Fact]
    public async Task GitHub_DzieliZakresDatGdyWynikowJestWiecejNizMoznaPobrac()
    {
        var source = GitHubSource(new RepositoryQuery { Keywords = ["prototyp"], CreatedWithinDays = 1 });
        var ranges = new List<(DateTime From, DateTime To)>();
        var adapter = GitHub(request =>
        {
            var (from, to) = Range(request);
            ranges.Add((from, to));
            // Ponad 1000 wyników w zakresach dłuższych niż 12 godzin.
            return to - from > TimeSpan.FromHours(12)
                ? GitHubJson(5000, [1, 2])
                : GitHubJson(1, [from.Ticks.GetHashCode()]);
        });

        var result = await adapter.FetchResultAsync(source, 300);

        // Okno 36 h, 5000 wyników, pojemność 300 → ceil(5000 / 240) = 21 części po ~1,7 h, od najstarszych.
        var leaves = ranges.Skip(1).ToList();
        Assert.Equal(22, ranges.Count);
        Assert.Equal(21, leaves.Count);
        Assert.Equal(new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc), leaves[0].From);
        Assert.Equal(Now, leaves[^1].To);
        Assert.True(leaves.Zip(leaves.Skip(1)).All(pair => pair.First.To == pair.Second.From));
        Assert.Equal(21, result.Items.Count);
        Assert.True(result.IsComplete);
        Assert.Equal(Now, CursorDate(Assert.Single(result.Cursors!).Value));
    }

    [Fact]
    public async Task GitHub_WyczerpanyLimitApiKonczySkanCzesciowoIZachowujePostep()
    {
        var source = GitHubSource(new RepositoryQuery { Keywords = ["pierwsze", "drugie"] });
        var calls = 0;
        var adapter = GitHub(request =>
        {
            calls++;
            if (calls == 1)
            {
                return GitHubJson(1, [7]);
            }

            var limited = new HttpResponseMessage(HttpStatusCode.Forbidden);
            limited.Headers.Add("x-ratelimit-remaining", "0");
            limited.Headers.Add("x-ratelimit-reset",
                new DateTimeOffset(Now.AddMinutes(40)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
            return limited;
        });

        var result = await adapter.FetchResultAsync(source, 300);

        Assert.False(result.IsComplete);
        Assert.Contains("Limit API GitHub wyczerpany do 12:40 UTC", result.Warning);
        Assert.Single(result.Items);
        var cursor = Assert.Single(result.Cursors!);
        Assert.StartsWith("kw:pierwsze", cursor.Key);
    }

    [Fact]
    public async Task GitHub_KrotkiLimitCzekaIPowtarzaZapytanie()
    {
        var source = GitHubSource(new RepositoryQuery { Keywords = ["prototyp"] });
        var delay = new RecordingDelay();
        var calls = 0;
        var adapter = GitHub(_ =>
        {
            if (++calls == 1)
            {
                var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                limited.Headers.Add("Retry-After", "20");
                return limited;
            }

            return GitHubJson(1, [1]);
        }, delay: delay);

        var result = await adapter.FetchResultAsync(source, 300);

        Assert.True(result.IsComplete);
        Assert.Single(result.Items);
        Assert.Equal([TimeSpan.FromSeconds(20)], delay.Calls);
    }

    [Fact]
    public async Task GitHub_PozostaleZapytanieZerowePrzedResetemCzekaNaOdnowienie()
    {
        var source = GitHubSource(new RepositoryQuery { Keywords = ["a1", "b2"] });
        var delay = new RecordingDelay();
        var adapter = GitHub(_ =>
        {
            var response = GitHubJson(1, [1]);
            response.Headers.Add("x-ratelimit-remaining", "0");
            response.Headers.Add("x-ratelimit-reset",
                new DateTimeOffset(Now.AddSeconds(30)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
            return response;
        }, delay: delay);

        var result = await adapter.FetchResultAsync(source, 300);

        Assert.True(result.IsComplete);
        Assert.Equal([TimeSpan.FromSeconds(31)], delay.Calls);
    }

    [Fact]
    public async Task GitHub_LimitZapytanNaSkanKonczySkanCzesciowo()
    {
        var source = GitHubSource(new RepositoryQuery { Keywords = ["a1", "b2", "c3"] });
        var adapter = GitHub(_ => GitHubJson(1, [Random.Shared.Next()]),
            new RepositoryOptions { MaxRequestsPerScan = 2 });

        var result = await adapter.FetchResultAsync(source, 300);

        Assert.False(result.IsComplete);
        Assert.Contains("limit 2 zapytań", result.Warning);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.Cursors!.Count);
    }

    [Fact]
    public async Task GitHub_LimitProjektowNaSkanPrzerywaBezPrzesuwaniaKursora()
    {
        var source = GitHubSource(new RepositoryQuery { Keywords = ["a1", "b2"] });
        var term = 0;
        var adapter = GitHub(_ =>
        {
            term++;
            return GitHubJson(60, Enumerable.Range(term * 1000, 60));
        });

        var result = await adapter.FetchResultAsync(source, 100);

        Assert.False(result.IsComplete);
        Assert.Contains("limit 100 projektów", result.Warning);
        Assert.Equal(100, result.Items.Count);
        // Pierwsze wyszukiwanie zakończone, drugie przerwane w połowie: kursor tylko dla pierwszego.
        Assert.StartsWith("kw:a1", Assert.Single(result.Cursors!).Key);
    }

    [Theory]
    [InlineData(-2, true)]   // kursor przy końcu okna: nowy zakres, potem ponowne sprawdzenie zakładki
    [InlineData(-120, false)] // zaległy backfill: tylko od kursora, bez cofania (inaczej brak postępu)
    public async Task GitHub_SkanPrzyrostowyNajpierwNowyZakresPotemZakladka(int cursorHours, bool expectRecheck)
    {
        var query = new RepositoryQuery { Keywords = ["prototyp"], CreatedWithinDays = 30 };
        var source = GitHubSource(query);
        var key = RepositorySearchUrlBuilder.Terms(query)[0].Key;
        var cursor = Now.AddHours(cursorHours);
        source.Configuration = SourceConfiguration.WithCursors(source.Configuration,
            new Dictionary<string, string> { [key] = cursor.ToString("O"), ["kw:usuniete"] = "x" });
        var ranges = new List<(DateTime From, DateTime To)>();
        var adapter = GitHub(request =>
        {
            ranges.Add(Range(request));
            return GitHubJson(0, []);
        }, new RepositoryOptions { OverlapHours = 6 });

        var result = await adapter.FetchResultAsync(source, 300);

        Assert.Equal((cursor, Now), ranges[0]);
        if (expectRecheck)
        {
            Assert.Equal((Now.AddHours(-6), cursor), ranges[1]);
        }

        Assert.Equal(expectRecheck ? 2 : 1, ranges.Count);
        Assert.Equal(Now, CursorDate(result.Cursors![key]));
        Assert.False(result.Cursors.ContainsKey("kw:usuniete"));
    }

    [Fact]
    public async Task GitHub_GestyBackfillPostepujeMiedzySkanami()
    {
        // 400 projektów na godzinę, limit 300 na skan: każdy skan musi przesunąć kursor do przodu.
        var source = GitHubSource(new RepositoryQuery { Keywords = ["prototype"], CreatedWithinDays = 1 });
        var adapter = GitHub(request =>
        {
            var (from, to) = Range(request);
            var total = (int)((to - from).TotalHours * 400);
            var seed = (int)(from.Ticks / TimeSpan.TicksPerMinute % 1_000_000) * 1000;
            return GitHubJson(total, Enumerable.Range(seed + (Page(request) - 1) * 100, Math.Min(100, total)));
        }, new RepositoryOptions { MaxRequestsPerScan = 1000, OverlapHours = 2 });

        var cursors = new List<DateTime>();
        for (var scan = 0; scan < 3; scan++)
        {
            var result = await adapter.FetchResultAsync(source, 300);
            source.Configuration = SourceConfiguration.WithCursors(source.Configuration, result.Cursors!);
            cursors.Add(CursorDate(Assert.Single(result.Cursors!).Value));
        }

        Assert.True(cursors[0] < cursors[1] && cursors[1] < cursors[2], string.Join(", ", cursors));
    }

    [Fact]
    public async Task GitLab_PierwszySkanIdzieOdNajnowszychDoPoczatkuOkna()
    {
        // Okno 7 dni: od 2026-11-14. Projekty 300..201 w oknie, 200 i starsze przed oknem.
        var gitlab = new FakeGitLab(Enumerable.Range(150, 151).ToDictionary(
            id => (long)id,
            id => id > 200 ? Now.AddHours(-(300 - id)) : new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)));
        var source = GitLabSource(new RepositoryQuery { Keywords = ["robot"], CreatedWithinDays = 7 });

        var result = await GitLab(gitlab).FetchResultAsync(source, 300);

        Assert.True(result.IsComplete);
        Assert.Equal(100, result.Items.Count);
        Assert.Equal("top=300;low=201;done=1", Assert.Single(result.Cursors!).Value);
        Assert.All(gitlab.Requests, url => Assert.Contains("order_by=id&sort=desc", url));
        Assert.Contains("last_activity_after=2026-11-14T00%3A00%3A00Z", gitlab.Requests[0]);
        Assert.Contains("id_before=201", gitlab.Requests[1]);
    }

    [Fact]
    public async Task GitLab_PrzerwanyPierwszySkanKontynuujeOdNajnizszegoId()
    {
        var gitlab = new FakeGitLab(Enumerable.Range(1, 250).ToDictionary(id => (long)id, id => Now.AddMinutes(-id)));
        var source = GitLabSource(new RepositoryQuery { Keywords = ["robot"], CreatedWithinDays = 7 });

        var first = await GitLab(gitlab).FetchResultAsync(source, 120);

        Assert.False(first.IsComplete);
        Assert.Equal(120, first.Items.Count);
        Assert.Equal("top=250;low=131;done=0", Assert.Single(first.Cursors!).Value);

        source.Configuration = SourceConfiguration.WithCursors(source.Configuration, first.Cursors!);
        gitlab.Requests.Clear();
        var second = await GitLab(gitlab).FetchResultAsync(source, 300);

        Assert.True(second.IsComplete);
        Assert.Equal(130, second.Items.Count);
        Assert.Equal("top=250;low=1;done=1", Assert.Single(second.Cursors!).Value);
        Assert.Contains("id_after=250", gitlab.Requests[0]);
        Assert.Contains("id_before=131", gitlab.Requests[1]);
    }

    [Fact]
    public async Task GitLab_KolejneSkanyPobierajaTylkoNoweProjektyRosnacoPoId()
    {
        var gitlab = new FakeGitLab(Enumerable.Range(1, 260).ToDictionary(id => (long)id, id => Now.AddMinutes(-id)));
        var source = GitLabSource(new RepositoryQuery { Keywords = ["robot"] });
        source.Configuration = SourceConfiguration.WithCursors(source.Configuration,
            new Dictionary<string, string> { ["kw:robot"] = "top=100;low=1;done=1" });

        var result = await GitLab(gitlab).FetchResultAsync(source, 300);

        Assert.Equal(160, result.Items.Count);
        Assert.Equal("top=260;low=1;done=1", Assert.Single(result.Cursors!).Value);
        Assert.Equal(2, gitlab.Requests.Count);
        Assert.Contains("order_by=id&sort=asc", gitlab.Requests[0]);
        Assert.Contains("id_after=100", gitlab.Requests[0]);
        Assert.Contains("id_after=200", gitlab.Requests[1]);
    }

    /// <summary>Symulacja GitLab /projects: filtrowanie i sortowanie po ID, last_activity_after = created_at.</summary>
    private sealed class FakeGitLab(Dictionary<long, DateTime> projects)
    {
        public List<string> Requests { get; } = [];

        public HttpResponseMessage Respond(HttpRequestMessage request)
        {
            Requests.Add(request.RequestUri!.ToString());
            var query = HttpUtility.ParseQueryString(request.RequestUri.Query);
            IEnumerable<KeyValuePair<long, DateTime>> items = projects;
            if (long.TryParse(query["id_after"], out var after)) items = items.Where(x => x.Key > after);
            if (long.TryParse(query["id_before"], out var before)) items = items.Where(x => x.Key < before);
            if (query["last_activity_after"] is { } activity)
            {
                var since = DateTime.Parse(activity, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
                // Projekty sprzed okna nadal bywają aktywne; zostawiamy je, adapter sam przerywa po created_at.
                items = items.Where(x => x.Value >= since || x.Key % 2 == 0);
            }

            items = query["sort"] == "asc" ? items.OrderBy(x => x.Key) : items.OrderByDescending(x => x.Key);
            var json = string.Join(",", items.Take(100).Select(x =>
                $$"""{"id":{{x.Key}},"name":"p{{x.Key}}","created_at":"{{x.Value:O}}"}"""));
            return FakeHttpMessageHandler.Json($"[{json}]");
        }
    }

    private static Source GitLabSource(RepositoryQuery query)
    {
        var source = SourceTestFactory.Create(SourceType.GitLabApi, "https://gitlab.com");
        source.Radar = RadarKind.Repozytorium;
        source.Configuration = SourceConfiguration.Create(null, query);
        return source;
    }

    private static GitLabSourceAdapter GitLab(FakeGitLab gitlab) =>
        new(new HttpClient(new FakeHttpMessageHandler(gitlab.Respond)), new FixedTime(Now), null, new RecordingDelay().Invoke);

    private static DateTime CursorDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture).UtcDateTime;

    [Fact]
    public async Task ScanService_ZapisujeSkanCzesciowyIKursory()
    {
        using var database = new SqliteTestDatabase();
        var source = GitHubSource(new RepositoryQuery { Keywords = ["a1", "b2"] });
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();
        var calls = 0;
        var adapter = GitHub(_ => ++calls == 1
            ? GitHubJson(1, [5])
            : new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Headers = { { "Retry-After", "3600" } } });
        var service = new ScanService(
            database.Context,
            new SingleAdapterResolver(adapter),
            Options.Create(new RadarOptions()),
            NullLogger<ScanService>.Instance);

        var result = await service.ScanSourceAsync(source.Id);

        Assert.Equal(ScanStatus.Partial, result.Status);
        Assert.Equal(1, result.ItemsNew);
        var saved = database.Context.Sources.Single();
        Assert.Contains("Limit API GitHub", saved.LastError);
        Assert.Single(SourceConfiguration.ReadCursors(saved.Configuration));
        Assert.Equal(ScanStatus.Partial, database.Context.ScanHistory.Single().Status);
        Assert.Single(database.Context.Discoveries);
    }

    private static Source GitHubSource(RepositoryQuery query)
    {
        var source = SourceTestFactory.Create(SourceType.GitHubApi, "https://api.github.com");
        source.Radar = RadarKind.Repozytorium;
        source.Configuration = SourceConfiguration.Create(null, query);
        return source;
    }

    private static GitHubSourceAdapter GitHub(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        RepositoryOptions? options = null,
        RecordingDelay? delay = null) =>
        new(new HttpClient(new FakeHttpMessageHandler(responder)),
            new FixedTime(Now),
            options,
            (delay ?? new RecordingDelay()).Invoke);

    private static HttpResponseMessage GitHubJson(int total, IEnumerable<int> ids)
    {
        var items = string.Join(",", ids.Select(id =>
            $$"""{"id":{{id}},"full_name":"org/repo-{{id}}","html_url":"https://github.com/org/repo-{{id}}"}"""));
        return FakeHttpMessageHandler.Json($$"""{"total_count":{{total}},"incomplete_results":false,"items":[{{items}}]}""");
    }

    private static int Page(HttpRequestMessage request) =>
        int.Parse(HttpUtility.ParseQueryString(request.RequestUri!.Query)["page"]!, CultureInfo.InvariantCulture);

    private static (DateTime From, DateTime To) Range(HttpRequestMessage request)
    {
        var q = HttpUtility.ParseQueryString(request.RequestUri!.Query)["q"]!;
        var match = CreatedRange.Match(q);
        return (Parse(match.Groups[1].Value), Parse(match.Groups[2].Value));

        static DateTime Parse(string value) =>
            DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
    }

    private sealed class SingleAdapterResolver(ISourceAdapter adapter) : ISourceAdapterResolver
    {
        public ISourceAdapter? Resolve(Source source) => adapter;
    }
}
