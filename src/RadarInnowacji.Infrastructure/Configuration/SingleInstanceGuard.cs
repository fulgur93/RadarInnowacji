using System.Diagnostics;
using System.Text;

namespace RadarInnowacji.Infrastructure.Configuration;

/// <summary>
/// Blokada oparta na pliku z wyłącznym dostępem. Działa między procesami
/// na Windows i Linuxie, niezależnie od implementacji nazwanych mutexów.
/// Przy osieroconym pliku (po awarii) wykrywa martwy proces i zwalnia blokadę.
/// </summary>
public sealed class SingleInstanceGuard(string lockFilePath) : IDisposable
{
    private FileStream? _lockStream;

    public bool TryAcquire()
    {
        var directory = Path.GetDirectoryName(lockFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                _lockStream = new FileStream(
                    lockFilePath,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.None);

                var pid = Encoding.UTF8.GetBytes(Environment.ProcessId.ToString());
                _lockStream.Write(pid);
                _lockStream.Flush();
                return true;
            }
            catch (IOException)
            {
                if (!IsLockOwnerRunning())
                {
                    DeleteLockFile();
                }
            }
        }

        return false;
    }

    private bool IsLockOwnerRunning()
    {
        try
        {
            var pidText = File.ReadAllText(lockFilePath).Trim();
            if (!int.TryParse(pidText, out var pid) || pid <= 0)
            {
                return false;
            }

            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (IOException)
        {
            // Nie udało się odczytać pliku – bezpieczniej założyć, że właściciel istnieje.
            return true;
        }
    }

    private void DeleteLockFile()
    {
        try
        {
            File.Delete(lockFilePath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public void Dispose()
    {
        _lockStream?.Dispose();
        _lockStream = null;
        DeleteLockFile();
    }
}
