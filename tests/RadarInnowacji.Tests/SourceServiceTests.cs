using System.Text.Json;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Tests;

public class SourceServiceTests
{
    [Fact]
    public async Task CreateAsync_DodajeRealneZrodloZPoprawnaKonfiguracja()
    {
        using var database = new SqliteTestDatabase();
        var service = new SourceService(database.Context);

        var source = await service.CreateAsync(
            "Kanał AGH",
            "https://example.org/feed.xml",
            RadarKind.Uczelnia,
            SourceType.Rss,
            30,
            true,
            "token-123");

        Assert.Equal("Kanał AGH", source.Name);
        Assert.Equal(SourceType.Rss, source.SourceType);
        Assert.Equal(FetchMethod.Rss, source.FetchMethod);
        Assert.False(source.IsDemo);
        Assert.Equal(30, source.ScanIntervalMinutes);
        using var configuration = JsonDocument.Parse(source.Configuration);
        Assert.Equal("token-123", configuration.RootElement.GetProperty("token").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("ftp://example.org/feed.xml")]
    [InlineData("example.org/feed.xml")]
    public async Task CreateAsync_OdrzucaNiepoprawnyAdresUrl(string url)
    {
        using var database = new SqliteTestDatabase();
        var service = new SourceService(database.Context);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            "Źródło",
            url,
            RadarKind.Uczelnia,
            SourceType.Rss,
            60,
            true,
            null));
    }

    [Fact]
    public async Task CreateAsync_OdrzucaNieobslugiwanyTypZrodla()
    {
        using var database = new SqliteTestDatabase();
        var service = new SourceService(database.Context);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            "Źródło",
            "https://example.org/feed.xml",
            RadarKind.Uczelnia,
            SourceType.Demo,
            60,
            true,
            null));
    }

    [Fact]
    public async Task CreateAsync_OdrzucaPodwojnaNazwe()
    {
        using var database = new SqliteTestDatabase();
        var service = new SourceService(database.Context);

        await service.CreateAsync(
            "Kanał AGH",
            "https://example.org/1.xml",
            RadarKind.Uczelnia,
            SourceType.Rss,
            60,
            true,
            null);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            "kanał agh",
            "https://example.org/2.xml",
            RadarKind.Uczelnia,
            SourceType.Rss,
            60,
            true,
            null));
    }

    [Fact]
    public async Task CreateAsync_OgraniczaInterwalIDodajePustyToken()
    {
        using var database = new SqliteTestDatabase();
        var service = new SourceService(database.Context);

        var source = await service.CreateAsync(
            "Kanał wiadomości",
            "https://example.org/news.xml",
            RadarKind.Wiadomosci,
            SourceType.Atom,
            0,
            false,
            "   ");

        Assert.Equal(5, source.ScanIntervalMinutes);
        Assert.False(source.IsActive);
        Assert.Equal("{}", source.Configuration);
        Assert.Equal(FetchMethod.Atom, source.FetchMethod);
    }
}
