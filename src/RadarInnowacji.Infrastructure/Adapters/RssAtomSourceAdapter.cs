using System.Xml.Linq;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Adapters;

/// <summary>Adapter dla kanałów RSS 2.0 i Atom</summary>
public sealed class RssAtomSourceAdapter(HttpClient httpClient) : ISourceAdapter
{
    public SourceType SourceType => SourceType.Rss;

    public async Task<IReadOnlyList<SourceItem>> FetchAsync(
        Source source,
        CancellationToken cancellationToken = default)
    {
        var xml = await httpClient.GetStringAsync(source.Url, cancellationToken);
        var document = XDocument.Parse(xml);
        var root = document.Root;
        if (root is null)
        {
            return Array.Empty<SourceItem>();
        }

        var localName = root.Name.LocalName;
        return localName switch
        {
            "rss" => ParseRss(root).ToList(),
            "feed" => ParseAtom(root).ToList(),
            _ => []
        };
    }

    private static IEnumerable<SourceItem> ParseRss(XElement root)
    {
        foreach (var item in Descendants(root, "item"))
        {
            var title = Value(item, "title") ?? "Materiał ze źródła RSS";
            var description = Value(item, "description") ?? Value(item, "encoded") ?? string.Empty;
            string link = Value(item, "link") ?? item.Parent?.Element("link")?.Value ?? string.Empty;
            var published = AdapterHelpers.ParseDate(Value(item, "pubDate") ?? Value(item, "date"));
            var author = Value(item, "creator") ?? Value(item, "author");
            var technologies = Elements(item, "category").Select(x => x.Value).ToList();

            yield return AdapterHelpers.CreateItem(
                Value(item, "guid") ?? link ?? title,
                title,
                description,
                description,
                link ?? string.Empty,
                published,
                published,
                author,
                null,
                technologies);
        }
    }

    private static IEnumerable<SourceItem> ParseAtom(XElement root)
    {
        foreach (var entry in Descendants(root, "entry"))
        {
            var title = Value(entry, "title") ?? "Materiał z kanału Atom";
            var summary = Value(entry, "summary") ?? Value(entry, "content") ?? string.Empty;
            string link = entry.Elements()
                .Where(x => x.Name.LocalName == "link" &&
                            (Attribute(x, "rel") is null || Attribute(x, "rel") == "alternate"))
                .Select(x => Attribute(x, "href"))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                ?? string.Empty;
            var published = AdapterHelpers.ParseDate(Value(entry, "published") ?? Value(entry, "updated"));
            var updated = AdapterHelpers.ParseDate(Value(entry, "updated"));
            var author = Elements(entry, "author").Select(x => Value(x, "name")).FirstOrDefault();
            var technologies = Elements(entry, "category")
                .Select(x => Attribute(x, "term"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToList();

            yield return AdapterHelpers.CreateItem(
                Value(entry, "id") ?? link ?? title,
                title,
                summary,
                summary,
                link ?? string.Empty,
                published,
                updated,
                author,
                null,
                technologies);
        }
    }

    private static IEnumerable<XElement> Descendants(XContainer root, string localName) =>
        root.Descendants().Where(x => x.Name.LocalName == localName);

    private static IEnumerable<XElement> Elements(XContainer root, string localName) =>
        root.Elements().Where(x => x.Name.LocalName == localName);

    private static string? Value(XContainer parent, string localName) =>
        Elements(parent, localName).FirstOrDefault()?.Value;

    private static string? Attribute(XElement element, string name) =>
        element.Attributes().FirstOrDefault(x => x.Name.LocalName == name)?.Value;
}
