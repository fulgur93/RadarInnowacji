using System.Text.Json;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Configuration;

namespace RadarInnowacji.Infrastructure.Adapters;

/// <summary>Stan jednego skanu przekazywany do strategii wyszukiwania platformy.</summary>
public sealed record RepositorySearchContext(
    RepositoryApiClient Client,
    string ApiBase,
    RepositorySearchRun Run,
    RepositoryOptions Options,
    DateTime NowUtc,
    DateTime WindowStartUtc,
    CancellationToken CancellationToken);

/// <summary>
/// Wspólna logika adapterów GitHub i GitLab: klient API z budżetem i limitami, lista wyszukiwań,
/// deduplikacja, limit projektów na skan i kursory skanowania przyrostowego.
/// Sposób przeszukiwania okna czasowego (zależny od możliwości API) implementują klasy pochodne.
/// Bez zapytania w konfiguracji adapter pobiera jedną stronę surowego adresu źródła.
/// </summary>
public abstract class RepositorySourceAdapterBase(
    HttpClient httpClient,
    TimeProvider? timeProvider,
    RepositoryOptions? options,
    Func<TimeSpan, CancellationToken, Task>? delay) : ISourceAdapter
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly RepositoryOptions _options = options ?? new RepositoryOptions();
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;

    public abstract SourceType SourceType { get; }

    protected abstract string PlatformName { get; }

    protected abstract string ApiBase(string sourceUrl);

    /// <summary>Nagłówki Accept i autoryzacji (token może być pusty).</summary>
    protected abstract void PrepareRequest(HttpRequestMessage request, string? token);

    protected abstract SourceItem ParseItem(JsonElement item);

    /// <summary>
    /// Przeszukuje okno czasowe jednego wyszukiwania. Postęp zapisuje przez <paramref name="saveCursor"/>
    /// na bieżąco, bo skan może zostać przerwany wyjątkiem <see cref="RepositoryFetchStoppedException"/>.
    /// </summary>
    protected abstract Task SearchTermAsync(
        RepositorySearchContext context,
        RepositorySearchTerm term,
        string? cursor,
        Action<string> saveCursor);

    public async Task<IReadOnlyList<SourceItem>> FetchAsync(Source source, CancellationToken cancellationToken = default) =>
        (await FetchResultAsync(source, _options.MaxItemsPerScan, cancellationToken)).Items;

    public async Task<SourceFetchResult> FetchResultAsync(
        Source source,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        var token = AdapterHelpers.GetConfigurationValue(source, SourceConfiguration.TokenKey);
        var client = new RepositoryApiClient(
            httpClient, PlatformName, request => PrepareRequest(request, token), _options, _time, _delay);
        var run = new RepositorySearchRun(Math.Max(1, maxItems));

        var query = SourceConfiguration.ReadRepositoryQuery(source.Configuration);
        if (query is null || !query.HasSearchTerms)
        {
            try
            {
                var page = await client.GetAsync(source.Url, cancellationToken);
                foreach (var item in Items(page.Root))
                {
                    if (!run.TryAdd(ParseItem(item)))
                    {
                        break;
                    }
                }
            }
            catch (RepositoryFetchStoppedException stopped)
            {
                run.Stop(stopped.Message);
            }

            return new SourceFetchResult(run.Items, run.IsComplete, run.Warning, null);
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var context = new RepositorySearchContext(
            client,
            ApiBase(source.Url),
            run,
            _options,
            now,
            RepositorySearchUrlBuilder.WindowStart(query, now),
            cancellationToken);
        var previous = SourceConfiguration.ReadCursors(source.Configuration);
        var terms = RepositorySearchUrlBuilder.Terms(query);

        // Zostają tylko kursory bieżących wyszukiwań; usunięte słowa nie zaśmiecają konfiguracji.
        var cursors = terms
            .Where(t => previous.ContainsKey(t.Key))
            .ToDictionary(t => t.Key, t => previous[t.Key], StringComparer.Ordinal);

        try
        {
            foreach (var term in terms)
            {
                await SearchTermAsync(
                    context,
                    term,
                    cursors.GetValueOrDefault(term.Key),
                    value => cursors[term.Key] = value);
            }
        }
        catch (RepositoryFetchStoppedException stopped)
        {
            run.Stop(stopped.Message);
        }

        return new SourceFetchResult(run.Items, run.IsComplete, run.Warning, cursors);
    }

    /// <summary>Elementy odpowiedzi: tablica (GitLab) albo obiekt z polem items (GitHub search).</summary>
    protected static IEnumerable<JsonElement> Items(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray();
        }

        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty("items", out var items) &&
               items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray()
            : [];
    }
}

/// <summary>Wyniki jednego skanu: deduplikacja po ExternalId, limit projektów i ostrzeżenia.</summary>
public sealed class RepositorySearchRun(int maxItems)
{
    private readonly Dictionary<string, SourceItem> _items = new(StringComparer.Ordinal);
    private readonly List<string> _warnings = [];
    private string? _stopReason;

    public int MaxItems => maxItems;
    public IReadOnlyList<SourceItem> Items => _items.Values.ToList();
    public bool IsComplete => _stopReason is null;
    public bool IsFull => _items.Count >= maxItems;

    public string FullMessage =>
        $"Osiągnięto limit {maxItems} projektów na skan; pozostałe zostaną pobrane w kolejnym skanie.";

    public string? Warning
    {
        get
        {
            var all = (_stopReason is null ? _warnings : _warnings.Prepend(_stopReason)).Distinct().ToList();
            return all.Count == 0 ? null : string.Join(" ", all);
        }
    }

    /// <summary>Dodaje element (duplikaty są pomijane); false, gdy nowy element nie zmieścił się w limicie.</summary>
    public bool TryAdd(SourceItem item)
    {
        if (_items.ContainsKey(item.ExternalId))
        {
            return true;
        }

        if (IsFull)
        {
            return false;
        }

        _items[item.ExternalId] = item;
        return true;
    }

    /// <summary>Dodaje element albo przerywa skan, gdy limit projektów jest wyczerpany.</summary>
    public void Add(SourceItem item)
    {
        if (!TryAdd(item))
        {
            throw new RepositoryFetchStoppedException(FullMessage);
        }
    }

    public void Warn(string message) => _warnings.Add(message);

    public void Stop(string reason) => _stopReason ??= reason;
}
