using RadarInnowacji.Infrastructure.Configuration;

namespace RadarInnowacji.Tests;

public class RadarInstanceFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "radar-instance-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ZapisIOdczyt_ZwracaAdresDzialajacejInstancji()
    {
        var file = new RadarInstanceFile(Path.Combine(_directory, "radar.url"));

        file.Write("http://127.0.0.1:5081");

        Assert.Equal("http://127.0.0.1:5081", file.TryRead());
    }

    [Fact]
    public void Odczyt_BrakPlikuLubUsunietyPlik_ZwracaNull()
    {
        var file = new RadarInstanceFile(Path.Combine(_directory, "radar.url"));
        Assert.Null(file.TryRead());

        file.Write("http://127.0.0.1:5080");
        file.Delete();

        Assert.Null(file.TryRead());
    }

    [Theory]
    [InlineData("http://192.168.1.10:5080")]
    [InlineData("https://127.0.0.1:5080")]
    [InlineData("file:///C:/Windows/system32/calc.exe")]
    [InlineData("http://user@127.0.0.1:5080")]
    [InlineData("nie-adres")]
    public void Odczyt_OdrzucaAdresSpozaLoopbackHttp(string url)
    {
        // Zmodyfikowany plik nie może doprowadzić do otwarcia dowolnego adresu ani programu.
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "radar.url");
        File.WriteAllText(path, url);

        Assert.Null(new RadarInstanceFile(path).TryRead());
        Assert.Throws<ArgumentException>(() => new RadarInstanceFile(path).Write(url));
    }

    [Theory]
    [InlineData("http://127.0.0.1:5080")]
    [InlineData("http://localhost:5085")]
    public void AdresLoopbackHttp_JestAkceptowany(string url) =>
        Assert.True(RadarInstanceFile.IsLoopbackHttpUrl(url));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
