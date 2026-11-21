using RadarInnowacji.Core.Enums;
using RadarInnowacji.Infrastructure.Adapters;

namespace RadarInnowacji.Tests;

public class SourceAdapterTests
{
    [Fact]
    public async Task GitHubAdapter_ParsujeRepozytoriaINaglowekAutoryzacji()
    {
        var source = SourceTestFactory.Create(SourceType.GitHubApi, "https://api.github.com/search/repositories?q=iot");
        source.Configuration = "{\"token\":\"abc123\"}";
        var handler = new FakeHttpMessageHandler(request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("abc123", request.Headers.Authorization?.Parameter);
            return FakeHttpMessageHandler.Json(
                """
                {"items":[{"id":11,"full_name":"org/iot-probe","description":"Prototyp czujnika","html_url":"https://github.com/org/iot-probe","created_at":"2026-01-02T10:00:00Z","pushed_at":"2026-11-01T10:00:00Z","owner":{"login":"org"},"language":"C#","topics":["iot"]}]}
                """);
        });
        var adapter = new GitHubSourceAdapter(new HttpClient(handler));

        var items = await adapter.FetchAsync(source);

        var item = Assert.Single(items);
        Assert.Equal("11", item.ExternalId);
        Assert.Equal("org/iot-probe", item.Title);
        Assert.Contains("C#", item.Technologies);
        Assert.Equal(new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc), item.PublishedAt);
    }

    [Fact]
    public async Task GitLabAdapter_ParsujeProjekty()
    {
        var source = SourceTestFactory.Create(SourceType.GitLabApi, "https://gitlab.com/api/v4/projects");
        var adapter = new GitLabSourceAdapter(new HttpClient(new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Json(
                """
                [{"id":22,"name_with_namespace":"Grupa / System","description":"Opis systemu","web_url":"https://gitlab.com/g/s","created_at":"2026-02-01T00:00:00Z","last_activity_at":"2026-10-01T00:00:00Z","namespace":{"name":"Grupa"},"topics":["web"]}]
                """))));

        var items = await adapter.FetchAsync(source);

        var item = Assert.Single(items);
        Assert.Equal("22", item.ExternalId);
        Assert.Equal("Grupa", item.Author);
        Assert.Contains("web", item.Technologies);
    }

    [Fact]
    public async Task HuggingFaceAdapter_ParsujeModele()
    {
        var source = SourceTestFactory.Create(SourceType.HuggingFaceApi, "https://huggingface.co/api/models");
        var adapter = new HuggingFaceSourceAdapter(new HttpClient(new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Json(
                """
                [{"id":"org/model","modelId":"org/model","pipeline_tag":"text-classification","tags":["transformers"],"createdAt":"2026-03-01T00:00:00Z","lastModified":"2026-11-01T00:00:00Z","author":"org"}]
                """))));

        var items = await adapter.FetchAsync(source);

        var item = Assert.Single(items);
        Assert.Equal("org/model", item.ExternalId);
        Assert.Equal("https://huggingface.co/org/model", item.SourceUrl);
        Assert.Contains("text-classification", item.Technologies);
    }

    [Fact]
    public async Task RssAdapter_ParsujeKanalRSS()
    {
        var source = SourceTestFactory.Create(SourceType.Rss, "https://example.org/feed.xml");
        const string xml =
            """
            <rss version="2.0"><channel><title>Test</title><item>
            <title>Nowy projekt badawczy</title><description>Opis projektu</description>
            <link>https://example.org/material/1</link><guid>guid-1</guid>
            <pubDate>Wed, 18 Nov 2026 10:00:00 GMT</pubDate><category>IoT</category>
            </item></channel></rss>
            """;
        var adapter = new RssAtomSourceAdapter(new HttpClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(xml))));

        var items = await adapter.FetchAsync(source);

        var item = Assert.Single(items);
        Assert.Equal("Nowy projekt badawczy", item.Title);
        Assert.Equal("https://example.org/material/1", item.SourceUrl);
        Assert.Contains("IoT", item.Technologies);
        Assert.NotNull(item.PublishedAt);
    }

    [Fact]
    public async Task RssAdapter_ParsujeKanalAtom()
    {
        var source = SourceTestFactory.Create(SourceType.Atom, "https://example.org/atom.xml");
        const string xml =
            """
            <feed xmlns="http://www.w3.org/2005/Atom"><title>Test</title><entry>
            <id>urn:article:1</id><title>Aktualność uczelni</title><summary>Podsumowanie</summary>
            <link href="https://example.org/atom/1" rel="alternate"/>
            <published>2026-10-05T08:00:00Z</published><updated>2026-10-06T08:00:00Z</updated>
            <author><name>Redakcja</name></author><category term="Edukacja"/>
            </entry></feed>
            """;
        var adapter = new RssAtomSourceAdapter(new HttpClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Xml(xml))));

        var items = await adapter.FetchAsync(source);

        var item = Assert.Single(items);
        Assert.Equal("Aktualność uczelni", item.Title);
        Assert.Equal("https://example.org/atom/1", item.SourceUrl);
        Assert.Equal("Redakcja", item.Author);
        Assert.Contains("Edukacja", item.Technologies);
    }

    [Fact]
    public async Task OaiPmhAdapter_ParsujeRekordyZMetadanymiDublinCore()
    {
        var source = SourceTestFactory.Create(SourceType.OaiPmh, "https://example.org/oai");
        const string xml =
            """
            <OAI-PMH xmlns="http://www.openarchives.org/OAI/2.0/"><ListRecords><record>
            <header><identifier>oai:agh:1</identifier></header>
            <metadata><oai_dc:dc xmlns:oai_dc="http://www.openarchives.org/OAI/2.0/oai_dc/" xmlns:dc="http://purl.org/dc/elements/1.1/">
            <dc:title>Praca dyplomowa</dc:title><dc:creator>Jan Kowalski</dc:creator>
            <dc:date>2026-09-01</dc:date><dc:description>Opis pracy</dc:description><dc:subject>Robotyzacja</dc:subject>
            </oai_dc:dc></metadata></record></ListRecords></OAI-PMH>
            """;
        var adapter = new OaiPmhSourceAdapter(new HttpClient(new FakeHttpMessageHandler(request =>
        {
            Assert.Contains("verb=ListRecords", request.RequestUri!.Query);
            Assert.Contains("metadataPrefix=oai_dc", request.RequestUri.Query);
            return FakeHttpMessageHandler.Xml(xml);
        })));

        var items = await adapter.FetchAsync(source);

        var item = Assert.Single(items);
        Assert.Equal("oai:agh:1", item.ExternalId);
        Assert.Equal("Praca dyplomowa", item.Title);
        Assert.Equal("Jan Kowalski", item.Author);
        Assert.Contains("Robotyzacja", item.Technologies);
    }

    [Fact]
    public async Task DspaceAdapter_ParsujeMetadaneItemow()
    {
        var source = SourceTestFactory.Create(SourceType.Dspace, "https://example.org/server/api/core/items?size=20");
        var adapter = new DspaceSourceAdapter(new HttpClient(new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Json(
                """
                {"_embedded":{"items":[{"uuid":"uuid-1","handle":"12345/1","metadata":{
                "dc.title":[{"value":"Materiał DSpace"}],"dc.description.abstract":[{"value":"Opis materiału"}],
                "dc.creator":[{"value":"Anna Nowak"}],"dc.date.issued":[{"value":"2026-08-01"}],"dc.subject":[{"value":"AI"}]}}]}}
                """))));

        var items = await adapter.FetchAsync(source);

        var item = Assert.Single(items);
        Assert.Equal("uuid-1", item.ExternalId);
        Assert.Equal("Materiał DSpace", item.Title);
        Assert.Equal("Anna Nowak", item.Author);
        Assert.Equal("https://hdl.handle.net/12345/1", item.SourceUrl);
    }

    [Fact]
    public async Task HtmlAdapter_ParsujeNapisyZStrony()
    {
        var source = SourceTestFactory.Create(SourceType.HtmlPage, "https://example.org/aktualnosci");
        const string html =
            """
            <html><body><main><article><h2><a href="/news/1">Nowy prototyp czujnika powietrza</a></h2><p>Opis projektu</p></article></main></body></html>
            """;
        var adapter = new HtmlPageSourceAdapter(new HttpClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Html(html))));

        var items = await adapter.FetchAsync(source);

        var item = Assert.Single(items);
        Assert.Equal("Nowy prototyp czujnika powietrza", item.Title);
        Assert.Equal("https://example.org/news/1", item.SourceUrl);
    }

    [Fact]
    public async Task HtmlAdapter_PomijaNawigacjeICzytaNaglowekZOgloszenia()
    {
        var source = SourceTestFactory.Create(SourceType.HtmlPage, "https://example.org/projekty");
        const string html =
            """
            <html><body>
              <nav><ul><li><a href="/kontakt">Bardzo długi link nawigacyjny</a></li></ul></nav>
              <div class="list-news">
                <a title="Studenci zbudowali rower z rekuperacją" href="/news/1">
                  <article><h2>Studenci zbudowali rower z rekuperacją</h2><p>Opis projektu</p></article>
                </a>
              </div>
              <div id="pageFooter"><a href="/prywatnosc">Polityka prywatności serwisu</a></div>
            </body></html>
            """;
        var adapter = new HtmlPageSourceAdapter(new HttpClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Html(html))));

        var items = await adapter.FetchAsync(source);

        var item = Assert.Single(items);
        Assert.Equal("Studenci zbudowali rower z rekuperacją", item.Title);
        Assert.Equal("https://example.org/news/1", item.SourceUrl);
    }
}
