using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Adapters;

/// <summary>
/// Adapter dla API GitHub (github.com lub GitHub Enterprise). Logika wyszukiwania, stronicowania
/// i limitów: <see cref="RepositorySourceAdapterBase"/>.
/// </summary>
public sealed class GitHubSourceAdapter(
    HttpClient httpClient,
    TimeProvider? timeProvider = null,
    RepositoryOptions? options = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null)
    : RepositorySourceAdapterBase(httpClient, timeProvider, options, delay)
{
    public override SourceType SourceType => SourceType.GitHubApi;

    protected override string PlatformName => "GitHub";

    // Wyszukiwarka GitHub zwraca najwyżej 1000 wyników (10 stron po 100).
    private const int PlatformMaxPages = 10;
    private static readonly TimeSpan MinSlice = TimeSpan.FromHours(1);

    protected override string ApiBase(string sourceUrl) => RepositorySearchUrlBuilder.GitHubApiBase(sourceUrl);

    /// <summary>
    /// Przegląda okno dat utworzenia od najstarszych. Zakres z większą liczbą wyników, niż można pobrać
    /// (limit 1000 wyników wyszukiwarki lub limit projektów na skan), dzieli na części dobrane do
    /// total_count, nie mniejsze niż godzina.
    /// Po pełnym pobraniu zakresu kursor (data) przesuwa się na jego koniec, więc przerwany skan
    /// kontynuuje od tego miejsca, a kolejne skany pobierają tylko nowe projekty
    /// (z zakładką <see cref="RepositoryOptions.OverlapHours"/> na opóźnienie indeksowania).
    /// </summary>
    protected override async Task SearchTermAsync(
        RepositorySearchContext context,
        RepositorySearchTerm term,
        string? cursor,
        Action<string> saveCursor)
    {
        var windowStart = context.WindowStartUtc;
        var now = context.NowUtc;
        if (!TryParseDate(cursor, out var covered) || covered <= windowStart)
        {
            await SearchRangeAsync(context, term, windowStart, now, DateTime.MinValue, saveCursor);
            return;
        }

        // Najpierw nowy zakres, żeby ponowne sprawdzanie nigdy nie blokowało postępu.
        if (covered < now)
        {
            await SearchRangeAsync(context, term, covered, now, covered, saveCursor);
        }

        // Potem ostatnie godziny przed kursorem: wyszukiwarka GitHub indeksuje nowe repozytoria
        // z opóźnieniem. Dotyczy tylko końca okna, nie zaległego przeszukiwania starszych dat.
        var recheckFrom = Max(windowStart, now - TimeSpan.FromHours(Math.Max(0, context.Options.OverlapHours)));
        if (recheckFrom < covered)
        {
            await SearchRangeAsync(context, term, recheckFrom, Min(covered, now), DateTime.MaxValue, saveCursor);
        }
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private async Task SearchRangeAsync(
        RepositorySearchContext context,
        RepositorySearchTerm term,
        DateTime from,
        DateTime to,
        DateTime covered,
        Action<string> saveCursor)
    {
        var run = context.Run;
        var maxPages = Math.Clamp(context.Options.MaxPagesPerQuery, 1, PlatformMaxPages);
        var capacity = Math.Min(run.MaxItems, maxPages * RepositorySearchUrlBuilder.PageSize);

        var first = await context.Client.GetAsync(Url(context, term, from, to, 1), context.CancellationToken);
        var total = first.TotalCount ?? 0;
        var tooBig = total > capacity;

        if (tooBig && to - from > MinSlice)
        {
            // total_count pozwala od razu dobrać wielkość części (z zapasem na nierówny rozkład),
            // zamiast połowić zakres i tracić zapytania na kolejne poziomy podziału.
            var span = to - from;
            var byDensity = Math.Ceiling(total / (capacity * 0.8));
            var byMinSlice = Math.Ceiling(span / MinSlice);
            var pieces = (int)Math.Max(2, Math.Min(byDensity, byMinSlice));
            var step = TimeSpan.FromTicks(span.Ticks / pieces);
            for (var i = 0; i < pieces; i++)
            {
                var sliceFrom = from + step * i;
                var sliceTo = i == pieces - 1 ? to : sliceFrom + step;
                await SearchRangeAsync(context, term, sliceFrom, sliceTo, covered, saveCursor);
            }

            return;
        }

        if (tooBig)
        {
            // Godzinny zakres nadal za duży: bierzemy, ile się zmieści, i idziemy dalej,
            // żeby jeden gęsty zakres nie blokował postępu w kolejnych skanach.
            run.Warn($"GitHub: wyszukiwanie „{term.Value}” ma w godzinie od {from:yyyy-MM-dd HH:mm} UTC " +
                     $"więcej wyników ({total}), niż można pobrać; pobrano część.");
        }

        var page = first;
        var pageNumber = 1;
        while (true)
        {
            foreach (var item in Items(page.Root))
            {
                if (!tooBig)
                {
                    // Zakres ma się zmieścić w całości; brak miejsca przerywa skan bez przesuwania kursora.
                    run.Add(ParseItem(item));
                }
                else if (!run.TryAdd(ParseItem(item)))
                {
                    break;
                }
            }

            if (!page.HasNextPage || pageNumber >= maxPages || (tooBig && run.IsFull))
            {
                break;
            }

            pageNumber++;
            page = await context.Client.GetAsync(Url(context, term, from, to, pageNumber), context.CancellationToken);
        }

        if (to > covered)
        {
            saveCursor(to.ToString("O", CultureInfo.InvariantCulture));
        }

        if (tooBig && run.IsFull)
        {
            throw new RepositoryFetchStoppedException(run.FullMessage);
        }
    }

    private static string Url(RepositorySearchContext context, RepositorySearchTerm term, DateTime from, DateTime to, int page) =>
        RepositorySearchUrlBuilder.GitHubSearchUrl(context.ApiBase, term, from, to, page);

    private static bool TryParseDate(string? value, out DateTime result)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            result = parsed.UtcDateTime;
            return true;
        }

        result = DateTime.MinValue;
        return false;
    }

    protected override void PrepareRequest(HttpRequestMessage request, string? token)
    {
        request.Headers.UserAgent.ParseAdd("RadarInnowacji/1.0 (prototyp)");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    protected override SourceItem ParseItem(JsonElement item)
    {
        var owner = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("owner", out var ownerElement)
            ? AdapterHelpers.ReadString(ownerElement, "login")
            : null;

        var technologies = AdapterHelpers.ReadStrings(item, "topics").ToList();
        var language = AdapterHelpers.ReadString(item, "language");
        if (!string.IsNullOrWhiteSpace(language))
        {
            technologies.Insert(0, language);
        }

        var title = AdapterHelpers.ReadString(item, "full_name", "name") ?? "Repozytorium GitHub";
        var description = AdapterHelpers.ReadString(item, "description") ?? string.Empty;
        var externalId = ReadIdentifier(item) ?? title;

        return AdapterHelpers.CreateItem(
            externalId,
            title,
            description,
            description,
            AdapterHelpers.ReadString(item, "html_url", "clone_url") ?? sourceUrlFallback(externalId),
            AdapterHelpers.ParseDate(AdapterHelpers.ReadString(item, "created_at")),
            AdapterHelpers.ParseDate(AdapterHelpers.ReadString(item, "pushed_at", "updated_at")),
            owner,
            "GitHub",
            technologies.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static string? ReadIdentifier(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number)
        {
            return id.GetRawText();
        }

        return AdapterHelpers.ReadString(item, "full_name", "name");
    }

    private static string sourceUrlFallback(string externalId) =>
        externalId.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? externalId
            : $"https://github.com/{externalId}";
}
