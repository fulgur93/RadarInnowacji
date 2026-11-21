using System.Net.Http.Headers;
using System.Text.Json;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Adapters;

/// <summary>Adapter dla API modeli Hugging Face.</summary>
public sealed class HuggingFaceSourceAdapter(HttpClient httpClient) : ISourceAdapter
{
    public SourceType SourceType => SourceType.HuggingFaceApi;

    public async Task<IReadOnlyList<SourceItem>> FetchAsync(
        Source source,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
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

        var root = document.RootElement;
        var items = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray()
            : [];

        return items.Select(Parse).ToList();
    }

    private static SourceItem Parse(JsonElement item)
    {
        var id = AdapterHelpers.ReadString(item, "id", "modelId") ?? "model";
        var title = AdapterHelpers.ReadString(item, "modelId", "id") ?? id;
        var description = AdapterHelpers.ReadString(item, "description", "pipeline_tag") ?? string.Empty;
        var technologies = AdapterHelpers.ReadStrings(item, "tags").ToList();
        var pipeline = AdapterHelpers.ReadString(item, "pipeline_tag");
        if (!string.IsNullOrWhiteSpace(pipeline))
        {
            technologies.Insert(0, pipeline);
        }

        return AdapterHelpers.CreateItem(
            id,
            title,
            description,
            description,
            $"https://huggingface.co/{id}",
            AdapterHelpers.ParseDate(AdapterHelpers.ReadString(item, "createdAt")),
            AdapterHelpers.ParseDate(AdapterHelpers.ReadString(item, "lastModified")),
            AdapterHelpers.ReadString(item, "author"),
            "Hugging Face",
            technologies.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }
}
