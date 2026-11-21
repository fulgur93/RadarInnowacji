using System.Globalization;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Adapters;

/// <summary>
/// Składa adresy wyszukiwania GitHub/GitLab z zapytania radaru repozytoriów.
/// Każde słowo kluczowe i temat to osobne zapytanie (opcjonalnie razy każdy język),
/// sortowane po dacie, nigdy po gwiazdkach: małe i nowe projekty nie mogą przepadać.
/// </summary>
public static class RepositorySearchUrlBuilder
{
    /// <summary>Górny limit zapytań na jeden skan źródła (ochrona limitów API).</summary>
    public const int MaxRequestsPerScan = 30;

    public const int PageSize = 100;

    public static IReadOnlyList<string> BuildGitHub(string sourceUrl, RepositoryQuery query, DateTime nowUtc)
    {
        query = query.Normalize();
        var apiBase = GitHubApiBase(sourceUrl);
        var createdSince = CreatedSince(query, nowUtc);

        var terms = query.Keywords.Select(keyword => $"\"{keyword}\" in:name,description,topics,readme")
            .Concat(query.Topics.Select(topic => $"topic:{topic}"));

        return WithLanguages(terms, query.Languages, (term, language) =>
            {
                var q = $"{term} created:>={createdSince} fork:false archived:false";
                if (language is not null)
                {
                    q += $" language:{QuoteIfNeeded(language)}";
                }

                return $"{apiBase}/search/repositories?q={Uri.EscapeDataString(q)}" +
                       $"&sort=updated&order=desc&per_page={PageSize}";
            })
            .Take(MaxRequestsPerScan)
            .ToList();
    }

    public static IReadOnlyList<string> BuildGitLab(string sourceUrl, RepositoryQuery query, DateTime nowUtc)
    {
        query = query.Normalize();
        var apiBase = GitLabApiBase(sourceUrl);
        var createdAfter = Uri.EscapeDataString(
            nowUtc.Date.AddDays(-query.CreatedWithinDays).ToString("yyyy-MM-dd'T'00:00:00'Z'", CultureInfo.InvariantCulture));

        var terms = query.Keywords.Select(keyword => $"search={Uri.EscapeDataString(keyword)}")
            .Concat(query.Topics.Select(topic => $"topic={Uri.EscapeDataString(topic)}"));

        return WithLanguages(terms, query.Languages, (term, language) =>
            {
                var url = $"{apiBase}/projects?{term}&visibility=public&archived=false" +
                          $"&created_after={createdAfter}&order_by=created_at&sort=desc&per_page={PageSize}";
                if (language is not null)
                {
                    url += $"&with_programming_language={Uri.EscapeDataString(language)}";
                }

                return url;
            })
            .Take(MaxRequestsPerScan)
            .ToList();
    }

    /// <summary>github.com i api.github.com → publiczne API; inny host → GitHub Enterprise (/api/v3).</summary>
    public static string GitHubApiBase(string sourceUrl)
    {
        var uri = ParseBase(sourceUrl, "https://api.github.com");
        return uri.Host is "github.com" or "www.github.com" or "api.github.com"
            ? "https://api.github.com"
            : $"{uri.Scheme}://{uri.Authority}/api/v3";
    }

    /// <summary>Dowolny adres instancji GitLab (gitlab.com lub uczelniana) → {instancja}/api/v4.</summary>
    public static string GitLabApiBase(string sourceUrl)
    {
        var uri = ParseBase(sourceUrl, "https://gitlab.com");
        return $"{uri.Scheme}://{uri.Authority}/api/v4";
    }

    private static Uri ParseBase(string sourceUrl, string fallback) =>
        Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri
            : new Uri(fallback);

    private static string CreatedSince(RepositoryQuery query, DateTime nowUtc) =>
        nowUtc.Date.AddDays(-query.CreatedWithinDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static IEnumerable<string> WithLanguages(
        IEnumerable<string> terms,
        IReadOnlyList<string> languages,
        Func<string, string?, string> build) =>
        languages.Count == 0
            ? terms.Select(term => build(term, null))
            : terms.SelectMany(term => languages.Select(language => build(term, language)));

    private static string QuoteIfNeeded(string value) => value.Contains(' ') ? $"\"{value}\"" : value;
}
