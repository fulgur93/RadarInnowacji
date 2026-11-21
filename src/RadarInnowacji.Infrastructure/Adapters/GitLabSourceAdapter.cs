using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Adapters;

/// <summary>
/// Adapter dla API GitLab Projects (gitlab.com lub inna instancja, np. uczelniana).
/// Logika wyszukiwania, stronicowania i limitów: <see cref="RepositorySourceAdapterBase"/>.
/// </summary>
public sealed class GitLabSourceAdapter(
    HttpClient httpClient,
    TimeProvider? timeProvider = null,
    RepositoryOptions? options = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null)
    : RepositorySourceAdapterBase(httpClient, timeProvider, options, delay)
{
    public override SourceType SourceType => SourceType.GitLabApi;

    protected override string PlatformName => "GitLab";

    protected override string ApiBase(string sourceUrl) => RepositorySearchUrlBuilder.GitLabApiBase(sourceUrl);

    /// <summary>
    /// API GitLab /projects nie filtruje po dacie utworzenia, ale ID projektów rosną wraz z nią.
    /// <list type="number">
    /// <item>Nowe projekty: rosnąco po ID od ostatnio przetworzonego (<c>id_after</c>).</item>
    /// <item>Zaległe okno czasowe: malejąco po ID (<c>id_before</c>) od najnowszych,
    /// aż do pierwszego projektu utworzonego przed początkiem okna.</item>
    /// </list>
    /// Kursor zapisuje po każdym projekcie zakres przetworzonych ID, więc przerwany skan kontynuuje dokładnie.
    /// </summary>
    protected override async Task SearchTermAsync(
        RepositorySearchContext context,
        RepositorySearchTerm term,
        string? cursor,
        Action<string> saveCursor)
    {
        var state = IdCursor.Parse(cursor);
        var run = context.Run;

        if (state.Top is { } top)
        {
            while (true)
            {
                var page = await context.Client.GetAsync(
                    RepositorySearchUrlBuilder.GitLabSearchUrl(context.ApiBase, term, idAfter: top, idBefore: null,
                        ascending: true, activityAfter: null),
                    context.CancellationToken);
                var items = Items(page.Root).ToList();
                foreach (var item in items)
                {
                    run.Add(ParseItem(item));
                    top = Math.Max(top, ReadId(item) ?? top);
                    state = state with { Top = top };
                    saveCursor(state.ToString());
                }

                if (items.Count < RepositorySearchUrlBuilder.PageSize)
                {
                    break;
                }
            }
        }

        // Bez żadnego trafienia w oknie (Top == null) sprawdzamy okno ponownie przy każdym skanie;
        // to tanie, bo zapytanie zwraca pustą lub krótką listę.
        if (state.BackfillDone && state.Top is not null)
        {
            return;
        }

        while (true)
        {
            var page = await context.Client.GetAsync(
                RepositorySearchUrlBuilder.GitLabSearchUrl(context.ApiBase, term, idAfter: null, idBefore: state.Low,
                    ascending: false, activityAfter: context.WindowStartUtc),
                context.CancellationToken);
            var items = Items(page.Root).ToList();
            foreach (var item in items)
            {
                var createdAt = AdapterHelpers.ParseDate(AdapterHelpers.ReadString(item, "created_at"));
                if (createdAt < context.WindowStartUtc)
                {
                    saveCursor((state = state with { BackfillDone = true }).ToString());
                    return;
                }

                run.Add(ParseItem(item));
                var id = ReadId(item);
                state = state with
                {
                    Top = state.Top ?? id,
                    Low = id is null ? state.Low : Math.Min(state.Low ?? long.MaxValue, id.Value)
                };
                saveCursor(state.ToString());
            }

            if (items.Count < RepositorySearchUrlBuilder.PageSize)
            {
                saveCursor((state = state with { BackfillDone = true }).ToString());
                return;
            }
        }
    }

    private static long? ReadId(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object &&
        item.TryGetProperty("id", out var id) &&
        id.TryGetInt64(out var value)
            ? value
            : null;

    /// <summary>Stan wyszukiwania GitLab: najwyższe i najniższe przetworzone ID, koniec zaległego okna.</summary>
    private sealed record IdCursor(long? Top, long? Low, bool BackfillDone)
    {
        public static IdCursor Parse(string? value)
        {
            long? top = null, low = null;
            var done = false;
            foreach (var part in (value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                if (pair.Length != 2)
                {
                    continue;
                }

                switch (pair[0])
                {
                    case "top" when long.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture, out var t):
                        top = t;
                        break;
                    case "low" when long.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture, out var l):
                        low = l;
                        break;
                    case "done":
                        done = pair[1] == "1";
                        break;
                }
            }

            return new IdCursor(top, low, done);
        }

        public override string ToString() =>
            string.Join(';', new[]
            {
                Top is null ? null : $"top={Top.Value.ToString(CultureInfo.InvariantCulture)}",
                Low is null ? null : $"low={Low.Value.ToString(CultureInfo.InvariantCulture)}",
                $"done={(BackfillDone ? 1 : 0)}"
            }.Where(x => x is not null));
    }

    protected override void PrepareRequest(HttpRequestMessage request, string? token)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Add("PRIVATE-TOKEN", token);
        }
    }

    protected override SourceItem ParseItem(JsonElement item)
    {
        var namespaceName = item.ValueKind == JsonValueKind.Object &&
                            item.TryGetProperty("namespace", out var namespaceElement)
            ? AdapterHelpers.ReadString(namespaceElement, "name", "path")
            : null;

        var topics = AdapterHelpers.ReadStrings(item, "topics", "tag_list").ToList();
        var title = AdapterHelpers.ReadString(item, "name_with_namespace", "name") ?? "Projekt GitLab";
        var description = AdapterHelpers.ReadString(item, "description") ?? string.Empty;
        var externalId = ReadIdentifier(item) ?? title;

        return AdapterHelpers.CreateItem(
            externalId,
            title,
            description,
            description,
            AdapterHelpers.ReadString(item, "web_url", "http_url_to_repo") ?? $"https://gitlab.com/{externalId}",
            AdapterHelpers.ParseDate(AdapterHelpers.ReadString(item, "created_at")),
            AdapterHelpers.ParseDate(AdapterHelpers.ReadString(item, "last_activity_at", "updated_at")),
            namespaceName,
            "GitLab",
            topics.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
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

        return AdapterHelpers.ReadString(item, "path_with_namespace", "name_with_namespace");
    }
}
