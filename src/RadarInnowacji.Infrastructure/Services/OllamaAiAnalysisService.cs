using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Configuration;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>Analiza odkryć z użyciem lokalnego Ollamy; bez chmury i bez klucza API.</summary>
public sealed class OllamaAiAnalysisService(
    IHttpClientFactory httpClientFactory,
    IOptions<RadarOptions> options,
    ILogger<OllamaAiAnalysisService> logger) : IAiAnalysisService
{
    private const string ClientName = "RadarAi";

    private const string SystemPrompt =
        "Jesteś szybkim filtrem innowacji. Przeanalizuj materiał i zwróć WYŁĄCZNIE poprawny JSON " +
        "z kluczami: isPotentialInnovation (true/false), confidence (liczba 0-100), innovationType, problem, " +
        "solution, noveltyDescription, technologies (tablica), reason, suggestedCategories (tablica), " +
        "missingInformation. Odpowiadaj po polsku, krótko i rzeczowo. " +
        "Ustaw isPotentialInnovation=false i confidence>=80 dla oczywistego szumu: strony błędów, przekierowania, " +
        "wymóg włączenia JavaScript/cookies, 'Czytaj więcej', puste/placeholderowe strony, brak opisu rozwiązania. " +
        "Nie przedstawiaj przypuszczeń jako faktów; brak danych oznacz jako 'Brak danych'. " +
        "Nie odrzucaj realnego projektu tylko dlatego, że nie zawiera słowa 'innowacja'.";

    public async Task<AiAnalysisResult> AnalyzeAsync(
        Discovery discovery,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        var ai = options.Value.Ai;
        var payload = new
        {
            model = ai.Model,
            stream = false,
            format = "json",
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = BuildUserPrompt(discovery, ai.MaxContentCharacters) }
            },
            options = new { temperature = 0.1 }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TrimBaseUrl(ai.BaseUrl)}/api/chat")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, ai.TimeoutSeconds)));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            using var response = await httpClientFactory.CreateClient(ClientName)
                .SendAsync(request, linked.Token);
            var body = await response.Content.ReadAsStringAsync(linked.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Ollama zwrócił HTTP {(int)response.StatusCode}: {Snippet(body)}");
            }

            return ParseResult(ExtractModelContent(body));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Analiza AI przekroczyła limit czasu ({Timeout}s).", ai.TimeoutSeconds);
            throw new InvalidOperationException(
                $"Przekroczono limit czasu ({ai.TimeoutSeconds} s) podczas analizy AI.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Model {ai.Model} zwrócił nieprawidłowy JSON: {exception.Message}");
        }
    }

    public async Task<AiConnectionTest> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var ai = options.Value.Ai;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, ai.TimeoutSeconds)));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            using var response = await httpClientFactory.CreateClient(ClientName)
                .GetAsync($"{TrimBaseUrl(ai.BaseUrl)}/api/tags", linked.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new AiConnectionTest(
                    false,
                    ai.Model,
                    $"Ollama zwrócił HTTP {(int)response.StatusCode}. Sprawdź, czy serwer jest uruchomiony.",
                    []);
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(linked.Token));
            var installed = document.RootElement.TryGetProperty("models", out var models) &&
                            models.ValueKind == JsonValueKind.Array
                ? models.EnumerateArray()
                    .Select(x => x.TryGetProperty("name", out var name) ? name.GetString() : null)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!)
                    .ToList()
                : [];

            var found = installed.Any(x => x == ai.Model || x.StartsWith(ai.Model + ":", StringComparison.Ordinal));
            var message = found
                ? $"Połączenie z Ollamą działa. Model {ai.Model} jest dostępny."
                : installed.Count == 0
                    ? $"Ollama działa, ale nie ma zainstalowanego modelu {ai.Model}."
                    : $"Model {ai.Model} nie jest zainstalowany. Dostępne: {string.Join(", ", installed)}.";

            return new AiConnectionTest(found, ai.Model, message, installed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AiConnectionTest(false, ai.Model, "Przekroczono limit czasu połączenia z Ollamą.", []);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            return new AiConnectionTest(false, ai.Model, $"Nie można połączyć się z Ollamą: {exception.Message}", []);
        }
    }

    private static string BuildUserPrompt(Discovery discovery, int maxContentCharacters)
    {
        var content = discovery.RawContent ?? string.Empty;
        if (content.Length > maxContentCharacters)
        {
            content = content[..maxContentCharacters] + "…";
        }

        return new StringBuilder()
            .AppendLine("Materiał do analizy:")
            .AppendLine("Tytuł: ").AppendLine(discovery.Title)
            .AppendLine("Opis: ").AppendLine(discovery.Description)
            .AppendLine("Adres źródła: ").AppendLine(discovery.SourceUrl)
            .Append("Data publikacji: ").AppendLine(
                discovery.PublishedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "brak danych")
            .AppendLine("Treść:")
            .AppendLine(content)
            .ToString();
    }

    private static string ExtractModelContent(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("message", out var message) ||
            message.ValueKind != JsonValueKind.Object ||
            !message.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Odpowiedź Ollamy nie zawiera treści wiadomości.");
        }

        var text = (content.GetString() ?? string.Empty).Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var start = text.IndexOf('\n');
            var end = text.LastIndexOf("```", StringComparison.Ordinal);
            if (start >= 0 && end > start)
            {
                text = text[(start + 1)..end].Trim();
            }
        }

        return text;
    }

    private static AiAnalysisResult ParseResult(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Model zwrócił JSON, który nie jest obiektem.");
        }

        var fields = document.RootElement.EnumerateObject()
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last().Value, StringComparer.OrdinalIgnoreCase);

        return new AiAnalysisResult(
            IsPotentialInnovation: GetBool(fields, "isPotentialInnovation"),
            Confidence: GetConfidence(fields, "confidence"),
            InnovationType: GetString(fields, "innovationType"),
            Problem: GetString(fields, "problem"),
            Solution: GetString(fields, "solution"),
            NoveltyDescription: GetString(fields, "noveltyDescription"),
            Technologies: GetArray(fields, "technologies"),
            Reason: GetString(fields, "reason"),
            SuggestedCategories: GetArray(fields, "suggestedCategories"),
            MissingInformation: GetString(fields, "missingInformation"));
    }

    private static string GetString(IReadOnlyDictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
            ? (value.GetString() ?? string.Empty).Trim()
            : string.Empty;

    private static bool GetBool(IReadOnlyDictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static int GetConfidence(IReadOnlyDictionary<string, JsonElement> fields, string name)
    {
        if (!fields.TryGetValue(name, out var value))
        {
            return 0;
        }

        var number = value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var parsed) => parsed,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => 0
        };
        return Math.Clamp(number, 0, 100);
    }

    private static IReadOnlyList<string> GetArray(IReadOnlyDictionary<string, JsonElement> fields, string name)
    {
        if (!fields.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => (x.GetString() ?? string.Empty).Trim())
            .Where(x => x.Length > 0)
            .ToList();
    }

    private static string TrimBaseUrl(string value) => value.Trim().TrimEnd('/');

    private static string Snippet(string value) =>
        value.Length <= 300 ? value : value[..300] + "…";
}
