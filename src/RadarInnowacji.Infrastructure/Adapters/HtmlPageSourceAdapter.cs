using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Adapters;

/// <summary>Adapter dla zwykłych stron HTML z listą aktualności.</summary>
public sealed class HtmlPageSourceAdapter(HttpClient httpClient) : ISourceAdapter
{
    private const int MaxItems = 100;
    private const int MinTitleLength = 12;

    public SourceType SourceType => SourceType.HtmlPage;

    public async Task<IReadOnlyList<SourceItem>> FetchAsync(
        Source source,
        CancellationToken cancellationToken = default)
    {
        var html = await httpClient.GetStringAsync(source.Url, cancellationToken);
        var parser = new HtmlParser();
        using var document = parser.ParseDocument(html);
        var baseUri = new Uri(source.Url);
        var items = new List<SourceItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var links = document.QuerySelectorAll("a[href]");
        foreach (var link in links)
        {
            if (items.Count >= MaxItems)
            {
                break;
            }

            if (IsNavigationOrFooter(link))
            {
                continue;
            }

            var title = AdapterHelpers.CleanText(
                PreferAttribute(link.GetAttribute("title"))
                ?? link.QuerySelector("h1, h2, h3, h4")?.TextContent
                ?? link.TextContent);
            if (title.Length < MinTitleLength)
            {
                continue;
            }

            var href = link.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#'))
            {
                continue;
            }

            if (!Uri.TryCreate(baseUri, href, out var absolute) ||
                absolute.Scheme is not ("http" or "https") ||
                !seen.Add(absolute.ToString()))
            {
                continue;
            }

            var description = AdapterHelpers.CleanText(link.TextContent);
            if (description.Length < title.Length)
            {
                description = title;
            }

            var published = FindPublishedDate(link);

            items.Add(AdapterHelpers.CreateItem(
                absolute.ToString(),
                title,
                description,
                description,
                absolute.ToString(),
                published,
                published,
                null,
                source.Name,
                []));
        }

        return items;
    }

    private static string? PreferAttribute(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= MinTitleLength ? value : null;

    /// <summary>
    /// Data publikacji jest czytana z elementu <c>time</c> albo typowych klas daty
    /// w otaczającym odnośnik artykule. Jeśli jej nie ma, pozostaje NULL.
    /// </summary>
    private static DateTime? FindPublishedDate(IElement link)
    {
        const string selector = "time[datetime], time, .date, .data, .published, .pub-date";
        IElement? node = link;
        for (var level = 0; node is not null && level < 3; level++, node = node.ParentElement)
        {
            var candidate = node.QuerySelector(selector);
            var raw = candidate?.GetAttribute("datetime") ?? candidate?.TextContent;
            if (AdapterHelpers.ParseDate(raw) is { } parsed)
            {
                return parsed;
            }
        }

        return null;
    }

    /// <summary>
    /// Strony uczelni mają rozbudowane menu i stopki; bez tego filtra zapełniłyby limit
    /// wyników linkami nawigacyjnymi zamiast ogłoszeń o projektach.
    /// </summary>
    private static bool IsNavigationOrFooter(IElement link)
    {
        for (var node = link.ParentElement; node is not null; node = node.ParentElement)
        {
            if (node.TagName is "NAV" or "HEADER" or "FOOTER" or "ASIDE")
            {
                return true;
            }

            if (HasMarker(node.Id) || HasMarker(node.ClassName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMarker(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.ToLowerInvariant();
        return text.Contains("nav", StringComparison.Ordinal) ||
               text.Contains("menu", StringComparison.Ordinal) ||
               text.Contains("footer", StringComparison.Ordinal) ||
               text.Contains("breadcrumb", StringComparison.Ordinal) ||
               text.Contains("skip-link", StringComparison.Ordinal) ||
               text.Contains("pager", StringComparison.Ordinal);
    }
}
