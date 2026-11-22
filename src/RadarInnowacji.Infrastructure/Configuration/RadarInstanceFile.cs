namespace RadarInnowacji.Infrastructure.Configuration;

/// <summary>
/// Plik z adresem działającej instancji. Druga próba uruchomienia (np. ponowne kliknięcie skrótu)
/// odczytuje go i otwiera przeglądarkę na już działającym serwerze zamiast uruchamiać kolejny.
/// Akceptowane są wyłącznie adresy http na interfejsie loopback.
/// </summary>
public sealed class RadarInstanceFile(string filePath)
{
    public string FilePath { get; } = filePath;

    public void Write(string url)
    {
        if (!IsLoopbackHttpUrl(url))
        {
            throw new ArgumentException("Adres instancji musi wskazywać na http://127.0.0.1 lub localhost.", nameof(url));
        }

        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(FilePath, url);
    }

    public string? TryRead()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var url = File.ReadAllText(FilePath).Trim();
            return IsLoopbackHttpUrl(url) ? url : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Delete()
    {
        try
        {
            File.Delete(FilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static bool IsLoopbackHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttp &&
        uri.IsLoopback &&
        string.IsNullOrEmpty(uri.UserInfo);
}
