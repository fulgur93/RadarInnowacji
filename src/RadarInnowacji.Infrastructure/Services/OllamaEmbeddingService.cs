using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Infrastructure.Configuration;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>
/// Embeddingi z lokalnego Ollamy (/api/embed). Model embeddingowy jest opcjonalny:
/// bez niego aplikacja używa <see cref="LocalHashEmbeddingService"/>.
/// </summary>
public sealed class OllamaEmbeddingService(
    IHttpClientFactory httpClientFactory,
    IOptions<RadarOptions> options) : IEmbeddingService
{
    private const string ClientName = "RadarEmbeddings";

    public string ProviderName => $"Ollama ({options.Value.Categories.OllamaEmbeddingModel})";

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var categories = options.Value.Categories;
        var payload = new
        {
            model = categories.OllamaEmbeddingModel,
            input = Truncate(text, 8000)
        };

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, categories.EmbeddingTimeoutSeconds)));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TrimBaseUrl(categories.OllamaBaseUrl)}/api/embed")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await httpClientFactory.CreateClient(ClientName).SendAsync(request, linked.Token);
        var body = await response.Content.ReadAsStringAsync(linked.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Ollama zwrócił HTTP {(int)response.StatusCode} dla modelu embeddingowego: {Snippet(body)}");
        }

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("embeddings", out var embeddings) ||
            embeddings.ValueKind != JsonValueKind.Array ||
            embeddings.GetArrayLength() == 0 ||
            embeddings[0].ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Ollama nie zwrócił wektora embeddingowego.");
        }

        var vector = embeddings[0].EnumerateArray()
            .Select(x => x.ValueKind == JsonValueKind.Number && x.TryGetSingle(out var value) ? value : 0f)
            .ToArray();
        if (vector.Length == 0)
        {
            throw new InvalidOperationException("Ollama zwrócił pusty wektor embeddingowy.");
        }

        return EmbeddingMath.Normalize(vector);
    }

    private static string Truncate(string text, int maxLength) =>
        string.IsNullOrEmpty(text) || text.Length <= maxLength ? text ?? string.Empty : text[..maxLength];

    private static string TrimBaseUrl(string value) => value.Trim().TrimEnd('/');

    private static string Snippet(string value) => value.Length <= 300 ? value : value[..300] + "…";
}
