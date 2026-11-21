using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Tests;

public class ScanServiceTests
{
    [Fact]
    public async Task Skanowanie_TworzyOdkryciaIHistorieOrazJestIdempotentneWZrodle()
    {
        using var database = new SqliteTestDatabase();
        var source = SourceTestFactory.Create(SourceType.Rss, "https://example.org/feed.xml", "Kanał RSS");
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();

        var adapter = new FakeSourceAdapter(
            Item("ext-1", "Pierwszy materiał", 100),
            Item("ext-2", "Drugi materiał", 200));
        var service = CreateService(database, adapter);

        var first = await service.ScanSourceAsync(source.Id);
        var second = await service.ScanSourceAsync(source.Id);

        Assert.Equal(ScanStatus.Succeeded, first.Status);
        Assert.Equal(2, first.ItemsFound);
        Assert.Equal(2, first.ItemsNew);
        Assert.Equal(0, second.ItemsNew);
        Assert.Equal(2, database.Context.Discoveries.Count());
        Assert.Equal(2, database.Context.ScanHistory.Count());
        Assert.All(database.Context.Discoveries, discovery =>
        {
            Assert.False(discovery.IsDemo);
            Assert.Equal(DiscoveryStatus.Candidate, discovery.Status);
            Assert.Equal(ProcessingStatus.Pending, discovery.ProcessingStatus);
            Assert.NotNull(discovery.ContentHash);
        });
        Assert.NotNull(database.Context.Sources.Single().LastSuccessfulScan);
    }

    [Fact]
    public async Task Skanowanie_AktualizujeIstniejaceOdkrycieBezDuplikatu()
    {
        using var database = new SqliteTestDatabase();
        var source = SourceTestFactory.Create(SourceType.Rss, "https://example.org/feed.xml", "Kanał RSS");
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();

        var adapter = new FakeSourceAdapter(Item("ext-1", "Wersja pierwsza", 100));
        var service = CreateService(database, adapter);
        await service.ScanSourceAsync(source.Id);

        adapter.Items = [Item("ext-1", "Wersja druga", 300)];
        var result = await service.ScanSourceAsync(source.Id);

        var discovery = Assert.Single(database.Context.Discoveries);
        Assert.Equal(0, result.ItemsNew);
        Assert.Equal("Wersja druga", discovery.Title);
        Assert.Equal(300, discovery.ContentLength);
        Assert.NotNull(discovery.LastActivityAt);
        Assert.Equal(2, database.Context.ScanHistory.Count());
    }

    [Fact]
    public async Task Skanowanie_PoBledzieAdapteraZapisujeBladWHistoriiIZrodle()
    {
        using var database = new SqliteTestDatabase();
        var source = SourceTestFactory.Create(SourceType.Rss, "https://example.org/feed.xml", "Kanał RSS");
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();

        var adapter = new FakeSourceAdapter { Exception = new HttpRequestException("Serwer nie odpowiada") };
        var service = CreateService(database, adapter);

        var result = await service.ScanSourceAsync(source.Id);

        Assert.Equal(ScanStatus.Failed, result.Status);
        Assert.Contains("Serwer nie odpowiada", result.ErrorMessage);
        var history = Assert.Single(database.Context.ScanHistory);
        Assert.Equal(ScanStatus.Failed, history.Status);
        Assert.NotNull(history.FinishedAt);
        Assert.Equal("Serwer nie odpowiada", database.Context.Sources.Single().LastError);
        Assert.Empty(database.Context.Discoveries);
    }

    [Fact]
    public async Task Skanowanie_PomijaZrodlaDemo()
    {
        using var database = new SqliteTestDatabase();
        var source = SourceTestFactory.Create(SourceType.Rss, "https://example.org/feed.xml", "Źródło demo");
        source.IsDemo = true;
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();

        var service = CreateService(database, new FakeSourceAdapter(Item("ext-1", "Materiał", 10)));

        var result = await service.ScanSourceAsync(source.Id);

        Assert.Equal(ScanStatus.Succeeded, result.Status);
        Assert.Equal(0, result.ItemsFound);
        Assert.Empty(database.Context.ScanHistory);
        Assert.Empty(database.Context.Discoveries);
    }

    [Fact]
    public async Task SkanDueSources_SkanujeTylkoAktywneIZalegleZrodla()
    {
        using var database = new SqliteTestDatabase();
        var due = SourceTestFactory.Create(SourceType.Rss, "https://example.org/due.xml", "Zaległe");
        var recent = SourceTestFactory.Create(SourceType.Rss, "https://example.org/recent.xml", "Aktualne");
        recent.LastScanDate = DateTime.UtcNow;
        var inactive = SourceTestFactory.Create(SourceType.Rss, "https://example.org/inactive.xml", "Nieaktywne");
        inactive.IsActive = false;
        var demo = SourceTestFactory.Create(SourceType.Rss, "https://example.org/demo.xml", "Demo");
        demo.IsDemo = true;
        database.Context.Sources.AddRange(due, recent, inactive, demo);
        await database.Context.SaveChangesAsync();

        var adapter = new FakeSourceAdapter(Item("ext-1", "Materiał", 10));
        var service = CreateService(database, adapter);

        var results = await service.ScanDueSourcesAsync();

        var result = Assert.Single(results);
        Assert.Equal(due.Id, result.SourceId);
        Assert.Equal(1, database.Context.ScanHistory.Count());
    }

    [Fact]
    public async Task Skanowanie_StosujeLimitLiczbyMaterialow()
    {
        using var database = new SqliteTestDatabase();
        var source = SourceTestFactory.Create(SourceType.Rss, "https://example.org/feed.xml", "Kanał RSS");
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();

        var adapter = new FakeSourceAdapter(
            Item("ext-1", "Materiał 1", 10),
            Item("ext-2", "Materiał 2", 20));
        var service = CreateService(database, adapter, maxItems: 1);

        var result = await service.ScanSourceAsync(source.Id);

        Assert.Equal(1, result.ItemsFound);
        Assert.Single(database.Context.Discoveries);
    }

    private static ScanService CreateService(
        SqliteTestDatabase database,
        FakeSourceAdapter adapter,
        int maxItems = 50)
    {
        var options = Options.Create(new RadarOptions
        {
            Scanning = new ScanningOptions { MaxItemsPerSource = maxItems }
        });
        return new ScanService(
            database.Context,
            new FakeAdapterResolver(adapter),
            options,
            NullLogger<ScanService>.Instance);
    }

    private static SourceItem Item(string externalId, string title, int contentLength) => new(
        externalId,
        title,
        $"Opis {title}",
        new string('x', contentLength),
        $"https://example.org/{externalId}",
        DateTime.UtcNow.AddDays(-1),
        DateTime.UtcNow,
        "Autor",
        "Organizacja",
        ["IoT"]);

    private sealed class FakeSourceAdapter : ISourceAdapter
    {
        public SourceType SourceType => SourceType.Rss;
        public IReadOnlyList<SourceItem> Items { get; set; } = [];
        public Exception? Exception { get; set; }

        public FakeSourceAdapter(params SourceItem[] items) => Items = items;

        public Task<IReadOnlyList<SourceItem>> FetchAsync(
            Source source,
            CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Items);
        }
    }

    private sealed class FakeAdapterResolver(ISourceAdapter adapter) : ISourceAdapterResolver
    {
        public ISourceAdapter? Resolve(Source source) => adapter;
    }
}
