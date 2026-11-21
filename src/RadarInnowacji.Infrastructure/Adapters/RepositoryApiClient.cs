using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using RadarInnowacji.Infrastructure.Configuration;

namespace RadarInnowacji.Infrastructure.Adapters;

/// <summary>Pobieranie przerwane przez limit (API, zapytań lub materiałów); dotychczasowe wyniki są zachowane.</summary>
public sealed class RepositoryFetchStoppedException(string message) : Exception(message);

/// <summary>Jedna odpowiedź API: treść JSON i informacje o stronicowaniu.</summary>
/// <param name="TotalCount">Liczba wszystkich wyników, jeśli API ją podaje (GitHub total_count, GitLab X-Total).</param>
public sealed record RepositoryApiPage(JsonElement Root, bool HasNextPage, int? TotalCount);

/// <summary>
/// Klient API GitHub/GitLab na czas jednego skanu: pilnuje budżetu zapytań, czyta nagłówki limitów
/// (GitHub x-ratelimit-*, GitLab RateLimit-*), czeka na odnowienie krótkich limitów, a przy długich
/// przerywa skan wyjątkiem <see cref="RepositoryFetchStoppedException"/>.
/// </summary>
public sealed class RepositoryApiClient(
    HttpClient httpClient,
    string platformName,
    Action<HttpRequestMessage> prepareRequest,
    RepositoryOptions options,
    TimeProvider timeProvider,
    Func<TimeSpan, CancellationToken, Task> delay)
{
    private const int MaxAttempts = 3;
    private static readonly Regex NextLink = new("<([^>]+)>\\s*;\\s*rel=\"next\"", RegexOptions.Compiled);

    private int? _remaining;
    private DateTime? _resetAt;

    public int RequestCount { get; private set; }

    public async Task<RepositoryApiPage> GetAsync(string url, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            await WaitForQuotaAsync(cancellationToken);

            if (RequestCount >= Math.Max(1, options.MaxRequestsPerScan))
            {
                throw new RepositoryFetchStoppedException(
                    $"Wykorzystano limit {options.MaxRequestsPerScan} zapytań do {platformName} na skan; " +
                    "pozostałe wyniki zostaną pobrane w kolejnym skanie.");
            }

            RequestCount++;
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            prepareRequest(request);
            if (request.Headers.UserAgent.Count == 0)
            {
                request.Headers.UserAgent.ParseAdd("RadarInnowacji/1.0 (prototyp)");
            }

            using var response = await httpClient.SendAsync(request, cancellationToken);
            ReadRateLimit(response.Headers);

            if (IsRateLimited(response))
            {
                var wait = RetryDelay(response);
                if (attempt < MaxAttempts && wait <= MaxWait)
                {
                    await delay(wait, cancellationToken);
                    continue;
                }

                throw new RepositoryFetchStoppedException(RateLimitMessage(Now + wait));
            }

            if ((int)response.StatusCode >= 500 && attempt < MaxAttempts)
            {
                await delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
                continue;
            }

            response.EnsureSuccessStatusCode();

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            var root = document.RootElement.Clone();

            return new RepositoryApiPage(root, HasNextPage(response.Headers), TotalCount(root, response.Headers));
        }
    }

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    private TimeSpan MaxWait => TimeSpan.FromSeconds(Math.Max(0, options.MaxRateLimitWaitSeconds));

    /// <summary>Gdy poprzednia odpowiedź zgłosiła wyczerpanie limitu, czeka do resetu albo kończy skan.</summary>
    private async Task WaitForQuotaAsync(CancellationToken cancellationToken)
    {
        if (_remaining is not 0 || _resetAt is null || _resetAt <= Now)
        {
            return;
        }

        var wait = _resetAt.Value - Now + TimeSpan.FromSeconds(1);
        if (wait > MaxWait)
        {
            throw new RepositoryFetchStoppedException(RateLimitMessage(_resetAt.Value));
        }

        await delay(wait, cancellationToken);
        _remaining = null;
    }

    private void ReadRateLimit(HttpResponseHeaders headers)
    {
        if (ReadInt(headers, "x-ratelimit-remaining", "RateLimit-Remaining") is { } remaining)
        {
            _remaining = (int)Math.Clamp(remaining, 0, int.MaxValue);
        }

        if (ReadInt(headers, "x-ratelimit-reset", "RateLimit-Reset") is { } reset)
        {
            _resetAt = DateTimeOffset.FromUnixTimeSeconds(reset).UtcDateTime;
        }
    }

    private bool IsRateLimited(HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.TooManyRequests ||
        (response.StatusCode == HttpStatusCode.Forbidden &&
         (_remaining == 0 || response.Headers.RetryAfter is not null));

    private TimeSpan RetryDelay(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            return Positive(date.UtcDateTime - Now);
        }

        return _resetAt is { } reset && reset > Now
            ? reset - Now + TimeSpan.FromSeconds(1)
            : TimeSpan.FromSeconds(60);
    }

    private string RateLimitMessage(DateTime resetAt) =>
        $"Limit API {platformName} wyczerpany do {resetAt:HH:mm} UTC; skan zakończono częściowo. " +
        "Token API zwiększa limit.";

    private static bool HasNextPage(HttpResponseHeaders headers)
    {
        if (headers.TryGetValues("Link", out var links) && links.Any(link => NextLink.IsMatch(link)))
        {
            return true;
        }

        return headers.TryGetValues("X-Next-Page", out var next) && next.Any(x => !string.IsNullOrWhiteSpace(x));
    }

    private static int? TotalCount(JsonElement root, HttpResponseHeaders headers)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("total_count", out var total) &&
            total.TryGetInt32(out var value))
        {
            return value;
        }

        return ReadInt(headers, "X-Total") is { } header ? (int)Math.Min(header, int.MaxValue) : null;
    }

    private static long? ReadInt(HttpResponseHeaders headers, params string[] names)
    {
        foreach (var name in names)
        {
            if (headers.TryGetValues(name, out var values) &&
                long.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static TimeSpan Positive(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;
}
