using System.Net.Http.Headers;
using System.Text.Json;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Adapters;

/// <summary>
/// Adapter dla API GitLab Projects (gitlab.com lub inna instancja, np. uczelniana).
/// Gdy źródło ma zapytanie (klucz "query" w konfiguracji), adapter sam buduje adresy wyszukiwania.
/// </summary>
public sealed class GitLabSourceAdapter(HttpClient httpClient, TimeProvider? timeProvider = null) : ISourceAdapter
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public SourceType SourceType => SourceType.GitLabApi;

    public async Task<IReadOnlyList<SourceItem>> FetchAsync(
        Source source,
        CancellationToken cancellationToken = default)
    {
        var query = SourceConfiguration.ReadRepositoryQuery(source.Configuration);
        if (query is null || !query.HasSearchTerms)
        {
            return await FetchUrlAsync(source, source.Url, cancellationToken);
        }

        var results = new List<SourceItem>();
        foreach (var url in RepositorySearchUrlBuilder.BuildGitLab(source.Url, query, _time.GetUtcNow().UtcDateTime))
        {
            results.AddRange(await FetchUrlAsync(source, url, cancellationToken));
        }

        return results.DistinctBy(x => x.ExternalId).ToList();
    }

    private async Task<IReadOnlyList<SourceItem>> FetchUrlAsync(
        Source source,
        string url,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var token = AdapterHelpers.GetConfigurationValue(source, "token");
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Add("PRIVATE-TOKEN", token);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var root = document.RootElement;
        var items = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray()
            : root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out var nested) &&
              nested.ValueKind == JsonValueKind.Array
                ? nested.EnumerateArray()
                : [];

        return items.Select(Parse).ToList();
    }

    private static SourceItem Parse(JsonElement item)
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
