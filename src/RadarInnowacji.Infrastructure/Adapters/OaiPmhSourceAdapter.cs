using System.Web;
using System.Xml.Linq;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Adapters;

/// <summary>Adapter dla protokołu OAI-PMH (ListRecords, metadataPrefix oai_dc).</summary>
public sealed class OaiPmhSourceAdapter(HttpClient httpClient) : ISourceAdapter
{
    private const int MaxPages = 5;

    public SourceType SourceType => SourceType.OaiPmh;

    public async Task<IReadOnlyList<SourceItem>> FetchAsync(
        Source source,
        CancellationToken cancellationToken = default)
    {
        var metadataPrefix = AdapterHelpers.GetConfigurationValue(source, "metadataPrefix") ?? "oai_dc";
        var url = BuildUrl(source.Url, $"?verb=ListRecords&metadataPrefix={HttpUtility.UrlEncode(metadataPrefix)}");
        var items = new List<SourceItem>();

        for (var page = 0; page < MaxPages; page++)
        {
            var xml = await httpClient.GetStringAsync(url, cancellationToken);
            var document = XDocument.Parse(xml);
            var root = document.Root;
            if (root is null)
            {
                break;
            }

            foreach (var record in root.Descendants().Where(x => x.Name.LocalName == "record"))
            {
                var header = Elements(record, "header").FirstOrDefault();
                if (header is not null && Attribute(header, "status") == "deleted")
                {
                    continue;
                }

                var metadata = Elements(record, "metadata").FirstOrDefault();
                if (metadata is null)
                {
                    continue;
                }

                items.Add(ParseRecord(header, metadata));
            }

            var token = root.Descendants()
                .FirstOrDefault(x => x.Name.LocalName == "resumptionToken")?.Value;
            if (string.IsNullOrWhiteSpace(token))
            {
                break;
            }

            url = BuildUrl(source.Url, $"?verb=ListRecords&resumptionToken={HttpUtility.UrlEncode(token)}");
        }

        return items;
    }

    private static SourceItem ParseRecord(XElement? header, XElement metadata)
    {
        var identifier = Value(header, "identifier") ?? Guid.NewGuid().ToString("N");
        var title = DescendantValue(metadata, "title") ?? identifier;
        var description = DescendantValues(metadata, "description").FirstOrDefault() ?? string.Empty;
        var published = AdapterHelpers.ParseDate(DescendantValues(metadata, "date").FirstOrDefault());
        var author = DescendantValues(metadata, "creator").FirstOrDefault();

        return AdapterHelpers.CreateItem(
            identifier,
            title,
            description,
            description,
            identifier,
            published,
            published,
            author,
            null,
            DescendantValues(metadata, "subject"));
    }

    private static string BuildUrl(string baseUrl, string query) =>
        baseUrl.Contains('?') ? $"{baseUrl}&{query[1..]}" : $"{baseUrl}{query}";

    private static IEnumerable<XElement> Elements(XContainer root, string localName) =>
        root.Elements().Where(x => x.Name.LocalName == localName);

    private static string? Value(XContainer? root, string localName) =>
        root is null ? null : Elements(root, localName).FirstOrDefault()?.Value;

    private static string? DescendantValue(XContainer root, string localName) =>
        root.Descendants().FirstOrDefault(x => x.Name.LocalName == localName)?.Value;

    private static IReadOnlyList<string> DescendantValues(XContainer root, string localName) =>
        root.Descendants()
            .Where(x => x.Name.LocalName == localName)
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

    private static string? Attribute(XElement element, string name) =>
        element.Attributes().FirstOrDefault(x => x.Name.LocalName == name)?.Value;
}
