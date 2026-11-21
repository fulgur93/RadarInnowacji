using System.Globalization;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Adapters;

/// <summary>Jedno wyszukiwanie Kickstartera: słowo kluczowe albo identyfikator kategorii.</summary>
public sealed record KickstarterSearchTerm(KickstarterSearchTermKind Kind, string Value);

public enum KickstarterSearchTermKind
{
    Keyword,
    Category
}

/// <summary>
/// Składa adresy publicznego wyszukiwania Kickstartera (/discover/advanced?format=json).
/// Sortowanie domyślne jest chronologiczne – popularność kampanii nie decyduje o tym,
/// co Radar zobaczy. Okno dat jest stosowane przez adapter na polu launched_at.
/// </summary>
public static class KickstarterSearchUrlBuilder
{
    public const string DefaultBaseUrl = "https://www.kickstarter.com";
    public const string SearchPath = "/discover/advanced";
    public const int PageSize = 12;
    public const int MaxTermsPerScan = 20;

    public static IReadOnlyList<KickstarterSearchTerm> Terms(KickstarterQuery query)
    {
        query = query.Normalize();
        return query.Keywords
            .Select(value => new KickstarterSearchTerm(KickstarterSearchTermKind.Keyword, value))
            .Concat(query.CategoryIds.Select(id => new KickstarterSearchTerm(
                KickstarterSearchTermKind.Category, id.ToString(CultureInfo.InvariantCulture))))
            .Take(MaxTermsPerScan)
            .ToList();
    }

    /// <summary>Podgląd: pierwsza strona każdego wyszukiwania z zapytania.</summary>
    public static IReadOnlyList<string> SearchUrls(string sourceUrl, KickstarterQuery query) =>
        Terms(query).Select(term => PageUrl(ApiBase(sourceUrl), query, term, 1)).ToList();

    public static string PageUrl(
        string apiBase,
        KickstarterQuery query,
        KickstarterSearchTerm term,
        int page)
    {
        query = query.Normalize();
        var url = $"{apiBase}{SearchPath}?format=json";
        url += term.Kind == KickstarterSearchTermKind.Keyword
            ? $"&term={Uri.EscapeDataString(term.Value)}"
            : $"&category_id={Uri.EscapeDataString(term.Value)}";
        url += $"&state={Uri.EscapeDataString(query.State)}";
        url += $"&sort={Uri.EscapeDataString(query.Sort)}";
        url += $"&page={Math.Max(1, page).ToString(CultureInfo.InvariantCulture)}";
        return url;
    }

    /// <summary>Domyślny serwis Kickstartera albo inny host wskazany w konfiguracji źródła.</summary>
    public static string ApiBase(string sourceUrl) =>
        Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? $"{uri.Scheme}://{uri.Authority}"
            : DefaultBaseUrl;
}
