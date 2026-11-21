using System.Net;
using System.Net.Sockets;

namespace RadarInnowacji.Web.Services;

/// <summary>
/// Wybiera wolny port, aby uruchomienie aplikacji nie kończyło się błędem
/// w przypadku zajęcia portu domyślnego.
/// </summary>
internal static class PortSelector
{
    internal static int FindAvailable(int preferredPort, int attempts = 10)
    {
        if (preferredPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(preferredPort), "Port musi mieć wartość od 1 do 65535.");
        }

        for (var i = 0; i < attempts; i++)
        {
            var port = preferredPort + i;
            if (port > 65535)
            {
                break;
            }

            var listener = new TcpListener(IPAddress.Loopback, port);
            try
            {
                listener.Start();
                return port;
            }
            catch (SocketException)
            {
                // Port zajęty – sprawdzamy kolejny.
            }
            finally
            {
                listener.Stop();
            }
        }

        throw new InvalidOperationException(
            $"Nie znaleziono wolnego portu w zakresie {preferredPort}-{preferredPort + attempts - 1}.");
    }
}
