using AngleSharp.Html.Parser;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Adapters;

/// <summary>Adapter dla zwykłych stron HTML z listą aktualności.</summary>
public sealed class HtmlPageSourceAdapter(HttpClient httpClient) : ISourceAdapter
{
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

        var links = document.QuerySelectorAll(
            "article a[href], main a[href], .news a[href], .aktualnosc a[href], li a[href], h2 a[href], h3 a[href]");

        foreach (var link in links)
        {
            if (items.Count >= 100)
            {
                break;
            }

            var title = AdapterHelpers.CleanText(link.TextContent);
            if (title.Length < 12)
            {
                continue;
            }

            var href = link.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href) ||
                !Uri.TryCreate(baseUri, href, out var absolute) ||
                absolute.Scheme is not ("http" or "https") ||
                !seen.Add(absolute.ToString()))
            {
                continue;
            }

            var description = AdapterHelpers.CleanText(link.ParentElement?.TextContent ?? title);
            items.Add(AdapterHelpers.CreateItem(
                absolute.ToString(),
                title,
                description,
                description,
                absolute.ToString(),
                null,
                null,
                null,
                source.Name,
                []));
        }

        return items;
    }
}
