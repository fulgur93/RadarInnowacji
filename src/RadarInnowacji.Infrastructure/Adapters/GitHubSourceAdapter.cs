using System.Net.Http.Headers;
using System.Text.Json;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Adapters;

/// <summary>Adapter dla API GitHub (repozytoria wyszukane przez URL źródła).</summary>
public sealed class GitHubSourceAdapter(HttpClient httpClient) : ISourceAdapter
{
    public SourceType SourceType => SourceType.GitHubApi;

    public async Task<IReadOnlyList<SourceItem>> FetchAsync(
        Source source,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
        request.Headers.UserAgent.ParseAdd("RadarInnowacji/1.0 (prototyp)");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var token = AdapterHelpers.GetConfigurationValue(source, "token");
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var items = JsonItems(document.RootElement).ToList();
        return items.Select(Parse).ToList();
    }

    private static IEnumerable<JsonElement> JsonItems(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray();
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("items", out var items) &&
            items.ValueKind == JsonValueKind.Array)
        {
            return items.EnumerateArray();
        }

        return [];
    }

    private static SourceItem Parse(JsonElement item)
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
