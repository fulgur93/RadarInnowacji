using System.Text.Json;
using System.Text.Json.Nodes;

namespace RadarInnowacji.Core.Models;

/// <summary>Odczyt i zapis pól konfiguracji źródła (kolumna Source.Configuration, JSON).</summary>
public static class SourceConfiguration
{
    public const string TokenKey = "token";
    public const string QueryKey = "query";
    public const string KickstarterQueryKey = "kickstarterQuery";
    public const string CursorsKey = "cursors";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Create(
        string? token,
        RepositoryQuery? query,
        KickstarterQuery? kickstarterQuery = null)
    {
        var root = new JsonObject();
        if (!string.IsNullOrWhiteSpace(token))
        {
            root[TokenKey] = token.Trim();
        }

        if (query is not null)
        {
            root[QueryKey] = JsonSerializer.SerializeToNode(query, JsonOptions);
        }

        if (kickstarterQuery is not null)
        {
            root[KickstarterQueryKey] = JsonSerializer.SerializeToNode(kickstarterQuery, JsonOptions);
        }

        return root.ToJsonString();
    }

    /// <summary>
    /// Podmienia zapytanie w istniejącej konfiguracji, zachowując pozostałe klucze (np. token).
    /// Zmiana zapytania kasuje postęp skanowania przyrostowego, żeby nowy zakres został przeszukany od początku.
    /// </summary>
    public static string WithRepositoryQuery(string? configuration, RepositoryQuery query)
    {
        var root = ParseObject(configuration);
        var newQuery = JsonSerializer.SerializeToNode(query, JsonOptions);
        if (!JsonNode.DeepEquals(root[QueryKey], newQuery))
        {
            root.Remove(CursorsKey);
        }

        root[QueryKey] = newQuery;
        return root.ToJsonString();
    }

    /// <summary>Podmienia zapytanie Kickstartera, zachowując pozostałe klucze konfiguracji.</summary>
    public static string WithKickstarterQuery(string? configuration, KickstarterQuery query)
    {
        var root = ParseObject(configuration);
        var newQuery = JsonSerializer.SerializeToNode(query, JsonOptions);
        if (!JsonNode.DeepEquals(root[KickstarterQueryKey], newQuery))
        {
            root.Remove(CursorsKey);
        }

        root[KickstarterQueryKey] = newQuery;
        return root.ToJsonString();
    }

    /// <summary>Zapisuje postęp skanowania przyrostowego (zastępuje poprzednie kursory).</summary>
    public static string WithCursors(string? configuration, IReadOnlyDictionary<string, string> cursors)
    {
        var root = ParseObject(configuration);
        var node = new JsonObject();
        foreach (var (key, value) in cursors.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            node[key] = value;
        }

        root[CursorsKey] = node;
        return root.ToJsonString();
    }

    public static IReadOnlyDictionary<string, string> ReadCursors(string? configuration)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ParseObject(configuration)[CursorsKey] is not JsonObject cursors)
        {
            return result;
        }

        foreach (var (key, value) in cursors)
        {
            if (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
            {
                result[key] = text;
            }
        }

        return result;
    }

    private static JsonObject ParseObject(string? configuration)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(configuration) ? "{}" : configuration) as JsonObject
                   ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    /// <summary>Zwraca zapytanie repozytoriów albo null, gdy źródło używa surowego adresu URL.</summary>
    public static RepositoryQuery? ReadRepositoryQuery(string? configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(configuration);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(QueryKey, out var query) ||
                query.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return query.Deserialize<RepositoryQuery>(JsonOptions)?.Normalize();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Zwraca zapytanie Kickstartera albo null, gdy źródło go nie ma.</summary>
    public static KickstarterQuery? ReadKickstarterQuery(string? configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(configuration);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(KickstarterQueryKey, out var query) ||
                query.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return query.Deserialize<KickstarterQuery>(JsonOptions)?.Normalize();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
