using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Adapters;

internal static class AdapterHelpers
{
    public static string? GetConfigurationValue(Source source, string key)
    {
        if (string.IsNullOrWhiteSpace(source.Configuration))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(source.Configuration);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty(key, out var value) &&
                   value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsedOffset))
        {
            return parsedOffset.UtcDateTime;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    public static string CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var withoutTags = Regex.Replace(value, "<[^>]+>", " ", RegexOptions.Compiled);
        return Regex.Replace(WebUtility.HtmlDecode(withoutTags), @"\s+", " ", RegexOptions.Compiled).Trim();
    }

    public static string Truncate(string? value, int maxLength)
    {
        var text = value ?? string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    public static IReadOnlyList<string> ReadStrings(JsonElement parent, params string[] propertyNames)
    {
        if (parent.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();
        foreach (var propertyName in propertyNames)
        {
            if (!parent.TryGetProperty(propertyName, out var values) || values.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            result.AddRange(values.EnumerateArray()
                .Select(value => value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Object when value.TryGetProperty("name", out var name) => name.GetString(),
                    _ => null
                })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!));
        }

        return result;
    }

    public static string? ReadString(JsonElement parent, params string[] propertyNames)
    {
        if (parent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in propertyNames)
        {
            if (parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    public static SourceItem CreateItem(
        string externalId,
        string title,
        string description,
        string content,
        string sourceUrl,
        DateTime? publishedAt,
        DateTime? lastActivityAt,
        string? author,
        string? organization,
        IReadOnlyList<string>? technologies = null) => new(
            Truncate(externalId, ContentLimits.MaxSourceUrlLength),
            Truncate(CleanText(title), ContentLimits.MaxTitleLength),
            Truncate(CleanText(description), ContentLimits.MaxDescriptionLength),
            Truncate(content, ContentLimits.MaxRawContentLength),
            Truncate(sourceUrl, ContentLimits.MaxSourceUrlLength),
            publishedAt,
            lastActivityAt,
            Truncate(author, 200),
            Truncate(organization, ContentLimits.MaxTitleLength),
            technologies ?? Array.Empty<string>());
}
