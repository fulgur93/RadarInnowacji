using System.ComponentModel;
using System.Diagnostics;

namespace RadarInnowacji.Web.Services;

internal static class BrowserLauncher
{
    internal static bool TryOpen(string url, ILogger logger)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            logger.LogInformation("Otwarto przeglądarkę pod adresem {Url}", url);
            return true;
        }
        catch (Exception exception) when (exception is Win32Exception
                                          or InvalidOperationException
                                          or PlatformNotSupportedException)
        {
            logger.LogWarning(exception, "Nie udało się otworzyć przeglądarki. Otwórz ją ręcznie pod adresem {Url}", url);
            return false;
        }
    }
}
