using System.Globalization;
using System.Net;
using System.Text.Json;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Configuration;

namespace RadarInnowacji.Infrastructure.Adapters;

/// <summary>
/// Adapter publicznego wyszukiwania Kickstartera (GET /discover/advanced?format=json).
/// Kampanie są traktowane jako kandydaci na innowacje: bez tokena, bez sortowania po
/// popularności, z oknem dat stosowanym na dacie rozpoczęcia kampanii.
/// </summary>
public sealed class KickstarterSourceAdapter(
    HttpClient httpClient,
    TimeProvider? timeProvider = null,
    KickstarterOptions? options = null) : ISourceAdapter
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly KickstarterOptions _options = options ?? new KickstarterOptions();

    public SourceType SourceType => SourceType.KickstarterApi;

    public async Task<IReadOnlyList<SourceItem>> FetchAsync(
        Source source,
        CancellationToken cancellationToken = default) =>
        (await FetchResultAsync(source, _options.MaxItemsPerScan, cancellationToken)).Items;

    public async Task<SourceFetchResult> FetchResultAsync(
        Source source,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        var query = SourceConfiguration.ReadKickstarterQuery(source.Configuration);
        if (query is null || !query.HasSearchTerms)
        {
            return new SourceFetchResult(
                [], true, "Brak zapytania Kickstartera: dodaj słowa kluczowe lub kategorię w konfiguracji źródła.", null);
        }

        var run = new RepositorySearchRun(Math.Max(1, maxItems));
        var windowStart = query.WindowStart(_time.GetUtcNow().UtcDateTime);
        var maxPages = Math.Clamp(_options.MaxPagesPerQuery, 1, 20);
        var apiBase = KickstarterSearchUrlBuilder.ApiBase(source.Url);
        var terms = KickstarterSearchUrlBuilder.Terms(query);
        var full = false;

        foreach (var term in terms)
        {
            var totalPages = maxPages;
            for (var page = 1; page <= totalPages && !full; page++)
            {
                var url = KickstarterSearchUrlBuilder.PageUrl(apiBase, query, term, page);
                using var response = await httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(ErrorMessage(response.StatusCode));
                }

                using var document = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken),
                    cancellationToken: cancellationToken);

                var root = document.RootElement;
                var projects = root.ValueKind == JsonValueKind.Object &&
                               root.TryGetProperty("projects", out var value) &&
                               value.ValueKind == JsonValueKind.Array
                    ? value.EnumerateArray().ToList()
                    : [];

                if (projects.Count == 0)
                {
                    break;
                }

                var totalHits = root.ValueKind == JsonValueKind.Object &&
                                root.TryGetProperty("total_hits", out var hits) &&
                                hits.ValueKind == JsonValueKind.Number &&
                                hits.TryGetInt64(out var count)
                    ? count
                    : 0;
                if (totalHits > 0)
                {
                    var available = (int)Math.Ceiling(totalHits / (double)KickstarterSearchUrlBuilder.PageSize);
                    totalPages = Math.Min(maxPages, Math.Max(1, available));
                }

                foreach (var project in projects)
                {
                    var item = Parse(project);
                    if (item is null || item.PublishedAt is null || item.PublishedAt < windowStart)
                    {
                        continue;
                    }

                    if (!run.TryAdd(item))
                    {
                        full = true;
                        break;
                    }
                }
            }
        }

        if (full)
        {
            run.Warn(run.FullMessage);
        }

        return new SourceFetchResult(run.Items, !full, run.Warning, null);
    }

    private static SourceItem? Parse(JsonElement project)
    {
        if (project.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = ReadIdentifier(project);
        var name = AdapterHelpers.ReadString(project, "name", "title");
        if (id is null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var blurb = AdapterHelpers.ReadString(project, "blurb", "description") ?? string.Empty;
        var slug = AdapterHelpers.ReadString(project, "slug");
        var category = ReadNestedString(project, "category", "name");
        var parentCategory = ReadNestedString(project, "category", "parent_name");
        var location = ReadNestedString(project, "location", "display_name");
        var state = AdapterHelpers.ReadString(project, "state");
        var launchedAt = ReadEpochDate(project, "launched_at");
        var deadline = ReadEpochDate(project, "deadline");

        var details = new List<string> { AdapterHelpers.CleanText(blurb) };
        if (category is not null) details.Add($"Kategoria: {category}");
        if (location is not null) details.Add($"Lokalizacja: {location}");
        if (state is not null) details.Add($"Stan kampanii: {state}");
        if (deadline is not null) details.Add($"Zakończenie: {UiDate(deadline.Value)}");
        var goal = ReadAmount(project, "goal");
        var pledged = ReadAmount(project, "pledged");
        if (goal is not null) details.Add($"Cel: {goal.Value.ToString("0.##", CultureInfo.InvariantCulture)}");
        if (pledged is not null) details.Add($"Zebrano: {pledged.Value.ToString("0.##", CultureInfo.InvariantCulture)}");

        var technologies = new[] { category, parentCategory }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return AdapterHelpers.CreateItem(
            id,
            name,
            blurb,
            string.Join("\n", details),
            ReadProjectUrl(project, slug),
            launchedAt,
            null,
            ReadNestedString(project, "creator", "name"),
            null,
            technologies);
    }

    private static string? ReadIdentifier(JsonElement project)
    {
        if (project.TryGetProperty("id", out var id))
        {
            if (id.ValueKind == JsonValueKind.Number) return id.GetRawText();
            if (id.ValueKind == JsonValueKind.String) return id.GetString();
        }

        return AdapterHelpers.ReadString(project, "slug");
    }

    private static string ReadProjectUrl(JsonElement project, string? slug)
    {
        var url = ReadNestedString(project, "urls", "project")
                  ?? ReadNestedString(project, "urls", "web", "project")
                  ?? AdapterHelpers.ReadString(project, "web_url");
        if (!string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        return string.IsNullOrWhiteSpace(slug)
            ? KickstarterSearchUrlBuilder.DefaultBaseUrl
            : $"{KickstarterSearchUrlBuilder.DefaultBaseUrl}/projects/{Uri.EscapeDataString(slug)}";
    }

    private static string? ReadNestedString(JsonElement parent, params string[] path)
    {
        var current = parent;
        foreach (var name in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static double? ReadAmount(JsonElement project, string name) =>
        project.ValueKind == JsonValueKind.Object &&
        project.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var amount)
            ? amount
            : null;

    private static DateTime? ReadEpochDate(JsonElement parent, params string[] names)
    {
        foreach (var name in names)
        {
            if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var value))
            {
                continue;
            }

            long? seconds = value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
                ? number
                : value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var parsed)
                    ? parsed
                    : null;
            if (seconds is null)
            {
                continue;
            }

            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(seconds.Value).UtcDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return null;
    }

    private static string UiDate(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string ErrorMessage(HttpStatusCode status) =>
        status == HttpStatusCode.Forbidden
            ? "Kickstarter zwrócił HTTP 403 i blokuje automatyczne zapytania z tego adresu. " +
              "Adapter nie obchodzi ochrony serwisu – sprawdź dostęp do publicznego wyszukiwania z sieci, " +
              "w której uruchomiona jest aplikacja, lub użyj własnego hosta w adresie źródła."
            : $"Kickstarter zwrócił HTTP {(int)status} ({status}).";
}
