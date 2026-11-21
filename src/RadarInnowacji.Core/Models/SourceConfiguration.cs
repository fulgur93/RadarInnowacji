using System.Text.Json;
using System.Text.Json.Nodes;

namespace RadarInnowacji.Core.Models;

/// <summary>Odczyt i zapis pól konfiguracji źródła (kolumna Source.Configuration, JSON).</summary>
public static class SourceConfiguration
{
    public const string TokenKey = "token";
    public const string QueryKey = "query";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Create(string? token, RepositoryQuery? query)
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

        return root.ToJsonString();
    }

    /// <summary>Podmienia zapytanie w istniejącej konfiguracji, zachowując pozostałe klucze (np. token).</summary>
    public static string WithRepositoryQuery(string? configuration, RepositoryQuery query)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(string.IsNullOrWhiteSpace(configuration) ? "{}" : configuration) as JsonObject
                   ?? new JsonObject();
        }
        catch (JsonException)
        {
            root = new JsonObject();
        }

        root[QueryKey] = JsonSerializer.SerializeToNode(query, JsonOptions);
        return root.ToJsonString();
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
}
