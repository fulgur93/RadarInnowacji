using System.Text.Json;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Adapters;

/// <summary>Adapter dla REST API DSpace 7 (bez warstwy prezentacyjnej).</summary>
public sealed class DspaceSourceAdapter(HttpClient httpClient) : ISourceAdapter
{
    public SourceType SourceType => SourceType.Dspace;

    public async Task<IReadOnlyList<SourceItem>> FetchAsync(
        Source source,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(source.Url, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("_embedded", out var embedded) ||
            !embedded.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<SourceItem>();
        }

        return items.EnumerateArray().Select(Parse).ToList();
    }

    private static SourceItem Parse(JsonElement item)
    {
        var id = ReadString(item, "uuid") ?? Guid.NewGuid().ToString("N");
        var title = ReadMetadata(item, "dc.title") ?? "Materiał DSpace";
        var description = ReadMetadata(item, "dc.description.abstract", "dc.description") ?? string.Empty;
        var published = AdapterHelpers.ParseDate(ReadMetadata(item, "dc.date.issued", "dc.date"));
        var author = ReadMetadata(item, "dc.creator", "dc.contributor.author");
        var subjects = ReadMetadataValues(item, "dc.subject");
        var handle = ReadString(item, "handle");

        return AdapterHelpers.CreateItem(
            id,
            title,
            description,
            description,
            string.IsNullOrWhiteSpace(handle) ? $"https://dspace.example.org/handle/{id}" : $"https://hdl.handle.net/{handle}",
            published,
            AdapterHelpers.ParseDate(ReadMetadata(item, "dc.date.accessioned")),
            author,
            "DSpace",
            subjects);
    }

    private static string? ReadString(JsonElement parent, string propertyName) =>
        AdapterHelpers.ReadString(parent, propertyName);

    private static string? ReadMetadata(JsonElement item, params string[] keys)
    {
        var values = ReadMetadataValues(item, keys);
        return values.Count == 0 ? null : values[0];
    }

    private static IReadOnlyList<string> ReadMetadataValues(JsonElement item, params string[] keys)
    {
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("metadata", out var metadata) ||
            metadata.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<string>();
        }

        var values = new List<string>();
        foreach (var key in keys)
        {
            if (!metadata.TryGetProperty(key, out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                var value = AdapterHelpers.ReadString(entry, "value");
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }
        }

        return values;
    }
}
