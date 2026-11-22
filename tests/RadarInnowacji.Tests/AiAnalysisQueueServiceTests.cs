using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Tests;

public class AiAnalysisQueueServiceTests
{
    [Fact]
    public async Task KolejkaAI_ZapisujeWynikIHistorieAnalizy()
    {
        using var database = new SqliteTestDatabase();
        var source = SourceTestFactory.Create(SourceType.Rss, "https://example.org/feed");
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();
        await ScanOneItemAsync(database, source.Id);

        var before = await database.Context.Discoveries.SingleAsync();
        Assert.Equal(ProcessingStatus.Pending, before.ProcessingStatus);

        var result = await CreateQueue(database, SuccessfulResult()).AnalyzeDiscoveryAsync(before.Id);

        Assert.Equal(1, result.Processed);
        Assert.Equal(0, result.Failed);
        var after = await database.Context.Discoveries.Include(x => x.AiAnalyses).SingleAsync();
        Assert.True(after.AIProcessed);
        Assert.True(after.IsPotentialInnovation);
        Assert.Equal(85, after.AIConfidence);
        Assert.Equal(ProcessingStatus.AiProcessed, after.ProcessingStatus);
        Assert.Equal(DiscoveryStatus.Interesting, after.Status);
        var record = Assert.Single(after.AiAnalyses);
        Assert.True(record.IsSuccess);
        Assert.Equal("SpeakLeash/bielik-1.5b-v3.0-instruct:Q8_0", record.Model);
        Assert.Contains("isPotentialInnovation", record.ResultJson);
    }

    [Fact]
    public async Task KolejkaAI_BladModeluOznaczaOdkrycieJakoNieprzeanalizowane()
    {
        using var database = new SqliteTestDatabase();
        var source = SourceTestFactory.Create(SourceType.Rss, "https://example.org/feed");
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();
        await ScanOneItemAsync(database, source.Id);
        var discovery = await database.Context.Discoveries.SingleAsync();

        var result = await CreateQueue(database, SuccessfulResult(), fails: true)
            .AnalyzeDiscoveryAsync(discovery.Id);

        Assert.Equal(1, result.Failed);
        var after = await database.Context.Discoveries.Include(x => x.AiAnalyses).SingleAsync();
        Assert.False(after.AIProcessed);
        Assert.Equal(ProcessingStatus.AiFailed, after.ProcessingStatus);
        var record = Assert.Single(after.AiAnalyses);
        Assert.False(record.IsSuccess);
        Assert.Contains("awaria modelu", record.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task KolejkaAI_PomijaMaterialyDemoIDaneJuzPrzetworzone()
    {
        using var database = new SqliteTestDatabase();
        var source = SourceTestFactory.Create(SourceType.Rss, "https://example.org/feed");
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();
        await ScanOneItemAsync(database, source.Id);
        var discovery = await database.Context.Discoveries.SingleAsync();
        discovery.AIProcessed = true;
        discovery.ProcessingStatus = ProcessingStatus.AiProcessed;
        await database.Context.SaveChangesAsync();

        var result = await CreateQueue(database, SuccessfulResult()).ProcessPendingAsync(10);

        Assert.Equal(0, result.Processed);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task KolejkaAI_UsuwaOczywistySzumPrzedAnaliza()
    {
        using var database = new SqliteTestDatabase();
        var source = SourceTestFactory.Create(SourceType.Rss, "https://example.org/feed");
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();
        database.Context.Discoveries.Add(new Discovery
        {
            SourceId = source.Id,
            Title = "Instrukcja włączenia obsługi JavaScript w przeglądarkach",
            Description = "Czytaj więcej",
            RawContent = "Aby korzystać z serwisu, włącz JavaScript.",
            SourceUrl = "https://example.org/js",
            ExternalId = "junk-1",
            ProcessingStatus = ProcessingStatus.Pending,
            Status = DiscoveryStatus.Candidate
        });
        await database.Context.SaveChangesAsync();

        var result = await CreateQueue(database, SuccessfulResult()).ProcessPendingAsync(10);

        Assert.Equal(1, result.Processed);
        Assert.Empty(await database.Context.Discoveries.ToListAsync());
    }

    [Fact]
    public async Task KolejkaAI_UsuwaNietrafneOdkryciePoAnalizie()
    {
        using var database = new SqliteTestDatabase();
        var source = SourceTestFactory.Create(SourceType.Rss, "https://example.org/feed");
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();
        database.Context.Discoveries.Add(new Discovery
        {
            SourceId = source.Id,
            Title = "Aktualności firmy",
            Description = "Krótka notatka prasowa bez rozwiązania.",
            RawContent = "Brak szczegółów technicznych.",
            SourceUrl = "https://example.org/news",
            ExternalId = "news-1",
            ProcessingStatus = ProcessingStatus.Pending,
            Status = DiscoveryStatus.Candidate
        });
        await database.Context.SaveChangesAsync();

        var notInnovation = new AiAnalysisResult(false, 90, "Brak", "Brak danych", "Brak danych",
            "Brak danych", [], "To nie jest innowacja.", [], "Brak danych");
        var result = await CreateQueue(database, notInnovation).ProcessPendingAsync(10);

        Assert.Equal(1, result.Processed);
        Assert.Empty(await database.Context.Discoveries.ToListAsync());
    }

    private static async Task ScanOneItemAsync(SqliteTestDatabase database, int sourceId)
    {
        var adapter = new FakeSourceAdapter();
        var scan = new ScanService(
            database.Context,
            new FakeAdapterResolver(adapter),
            Options.Create(new RadarOptions { Scanning = new ScanningOptions { MaxItemsPerSource = 5 } }),
            NullLogger<ScanService>.Instance);

        var result = await scan.ScanSourceAsync(sourceId);
        Assert.Equal(ScanStatus.Succeeded, result.Status);
    }

    private static AiAnalysisQueueService CreateQueue(
        SqliteTestDatabase database,
        AiAnalysisResult result,
        bool fails = false)
    {
        IAiAnalysisService analyzer = fails ? new FailingAnalyzer() : new FakeAnalyzer(result);
        return new AiAnalysisQueueService(
            database.Context,
            analyzer,
            Options.Create(new RadarOptions { Ai = new AiOptions { Model = "SpeakLeash/bielik-1.5b-v3.0-instruct:Q8_0" } }),
            NullLogger<AiAnalysisQueueService>.Instance);
    }

    private static AiAnalysisResult SuccessfulResult() => new(
        true,
        85,
        "Technologiczna",
        "Brak taniego monitoringu",
        "Czujnik IoT",
        "Nowa metoda pomiaru",
        ["IoT"],
        "Materiał opisuje nowe rozwiązanie.",
        ["AI"],
        "Brak danych o kosztach");

    private sealed class FakeAnalyzer(AiAnalysisResult result) : IAiAnalysisService
    {
        public Task<AiAnalysisResult> AnalyzeAsync(Discovery discovery, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);

        public Task<AiConnectionTest> TestConnectionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiConnectionTest(true, "SpeakLeash/bielik-1.5b-v3.0-instruct:Q8_0", "OK", ["SpeakLeash/bielik-1.5b-v3.0-instruct:Q8_0"]));
    }

    private sealed class FailingAnalyzer : IAiAnalysisService
    {
        public Task<AiAnalysisResult> AnalyzeAsync(Discovery discovery, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Awaria modelu testowego.");

        public Task<AiConnectionTest> TestConnectionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiConnectionTest(true, "SpeakLeash/bielik-1.5b-v3.0-instruct:Q8_0", "OK", ["SpeakLeash/bielik-1.5b-v3.0-instruct:Q8_0"]));
    }

    private sealed class FakeSourceAdapter : ISourceAdapter
    {
        public SourceType SourceType => SourceType.Rss;

        public Task<IReadOnlyList<SourceItem>> FetchAsync(
            Source source,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceItem>>(
            [
                new SourceItem(
                    "ext-1",
                    "Nowy projekt studencki",
                    "Opis projektu",
                    "Treść projektu",
                    "https://example.org/project/1",
                    DateTime.UtcNow.AddDays(-1),
                    DateTime.UtcNow.AddDays(-1),
                    null,
                    null,
                    ["IoT"])
            ]);
    }

    private sealed class FakeAdapterResolver(ISourceAdapter adapter) : ISourceAdapterResolver
    {
        public ISourceAdapter? Resolve(Source source) => adapter;
    }
}
