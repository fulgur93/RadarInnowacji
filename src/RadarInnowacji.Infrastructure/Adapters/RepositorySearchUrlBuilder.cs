using System.Globalization;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Adapters;

/// <summary>Jedno wyszukiwanie: słowo kluczowe albo temat, opcjonalnie zawężone do języka.</summary>
public sealed record RepositorySearchTerm(RepositorySearchTermKind Kind, string Value, string? Language)
{
    /// <summary>Stały klucz, pod którym zapisywany jest postęp skanowania przyrostowego.</summary>
    public string Key =>
        $"{(Kind == RepositorySearchTermKind.Keyword ? "kw" : "topic")}:{Value.ToLowerInvariant()}" +
        (Language is null ? string.Empty : $"|lang:{Language.ToLowerInvariant()}");
}

public enum RepositorySearchTermKind
{
    Keyword,
    Topic
}

/// <summary>
/// Składa adresy wyszukiwania GitHub/GitLab z zapytania radaru repozytoriów.
/// Każde słowo kluczowe i temat to osobne wyszukiwanie (opcjonalnie razy każdy język),
/// filtrowane po dacie utworzenia, nigdy po gwiazdkach: małe i nowe projekty nie mogą przepadać.
/// </summary>
public static class RepositorySearchUrlBuilder
{
    /// <summary>Górny limit wyszukiwań (słowo/temat × język) na jeden skan źródła.</summary>
    public const int MaxTermsPerScan = 30;

    public const int PageSize = 100;

    public static IReadOnlyList<RepositorySearchTerm> Terms(RepositoryQuery query)
    {
        query = query.Normalize();
        var terms = query.Keywords.Select(x => (Kind: RepositorySearchTermKind.Keyword, Value: x))
            .Concat(query.Topics.Select(x => (Kind: RepositorySearchTermKind.Topic, Value: x)));

        return (query.Languages.Count == 0
                ? terms.Select(t => new RepositorySearchTerm(t.Kind, t.Value, null))
                : terms.SelectMany(t => query.Languages.Select(l => new RepositorySearchTerm(t.Kind, t.Value, l))))
            .Take(MaxTermsPerScan)
            .ToList();
    }

    /// <summary>Początek okna czasowego zapytania (północ UTC N dni temu).</summary>
    public static DateTime WindowStart(RepositoryQuery query, DateTime nowUtc) =>
        DateTime.SpecifyKind(nowUtc.Date.AddDays(-query.Normalize().CreatedWithinDays), DateTimeKind.Utc);

    /// <summary>Podgląd: pierwsza strona każdego wyszukiwania dla całego okna czasowego.</summary>
    public static IReadOnlyList<string> BuildGitHub(string sourceUrl, RepositoryQuery query, DateTime nowUtc)
    {
        var apiBase = GitHubApiBase(sourceUrl);
        var from = WindowStart(query, nowUtc);
        return Terms(query).Select(t => GitHubSearchUrl(apiBase, t, from, nowUtc, 1)).ToList();
    }

    /// <summary>Podgląd: pierwsza strona każdego wyszukiwania (od najnowszych projektów).</summary>
    public static IReadOnlyList<string> BuildGitLab(string sourceUrl, RepositoryQuery query, DateTime nowUtc)
    {
        var apiBase = GitLabApiBase(sourceUrl);
        var from = WindowStart(query, nowUtc);
        return Terms(query)
            .Select(t => GitLabSearchUrl(apiBase, t, idAfter: null, idBefore: null, ascending: false, activityAfter: from))
            .ToList();
    }

    /// <summary>Wyszukiwanie GitHub dla projektów utworzonych w zakresie [from, to] (obustronnie domkniętym).</summary>
    public static string GitHubSearchUrl(string apiBase, RepositorySearchTerm term, DateTime from, DateTime to, int page)
    {
        var q = term.Kind == RepositorySearchTermKind.Keyword
            ? $"\"{term.Value}\" in:name,description,topics,readme"
            : $"topic:{term.Value}";
        q += $" created:{Iso(from)}..{Iso(to)} fork:false archived:false";
        if (term.Language is not null)
        {
            q += $" language:{(term.Language.Contains(' ') ? $"\"{term.Language}\"" : term.Language)}";
        }

        return $"{apiBase}/search/repositories?q={Uri.EscapeDataString(q)}" +
               $"&sort=updated&order=desc&per_page={PageSize}&page={page}";
    }

    /// <summary>
    /// Lista publicznych projektów GitLab sortowana po ID (GitLab nie filtruje po dacie utworzenia,
    /// a ID rośnie wraz z nią). Zakres ustala się przez id_after/id_before; activityAfter odsiewa
    /// projekty nieaktywne od początku okna (projekt utworzony w oknie zawsze ma w nim aktywność).
    /// </summary>
    public static string GitLabSearchUrl(
        string apiBase,
        RepositorySearchTerm term,
        long? idAfter,
        long? idBefore,
        bool ascending,
        DateTime? activityAfter)
    {
        var filter = term.Kind == RepositorySearchTermKind.Keyword
            ? $"search={Uri.EscapeDataString(term.Value)}"
            : $"topic={Uri.EscapeDataString(term.Value)}";
        // active=true pomija też projekty zaplanowane do usunięcia (bez tego to większość wyników gitlab.com).
        var url = $"{apiBase}/projects?{filter}&visibility=public&archived=false&active=true" +
                  $"&order_by=id&sort={(ascending ? "asc" : "desc")}&per_page={PageSize}";
        if (idAfter is not null)
        {
            url += $"&id_after={idAfter.Value.ToString(CultureInfo.InvariantCulture)}";
        }

        if (idBefore is not null)
        {
            url += $"&id_before={idBefore.Value.ToString(CultureInfo.InvariantCulture)}";
        }

        if (activityAfter is not null)
        {
            url += $"&last_activity_after={Uri.EscapeDataString(Iso(activityAfter.Value))}";
        }

        if (term.Language is not null)
        {
            url += $"&with_programming_language={Uri.EscapeDataString(term.Language)}";
        }

        return url;
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

    private static string Iso(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
