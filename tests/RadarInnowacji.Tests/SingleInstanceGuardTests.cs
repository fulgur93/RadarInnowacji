using RadarInnowacji.Infrastructure.Configuration;

namespace RadarInnowacji.Tests;

public class SingleInstanceGuardTests
{
    [Fact]
    public void Guard_BlokujeDrugaInstancje()
    {
        var lockPath = CreateLockPath("blokada");

        using var first = new SingleInstanceGuard(lockPath);
        using var second = new SingleInstanceGuard(lockPath);

        Assert.True(first.TryAcquire());
        Assert.False(second.TryAcquire());
    }

    [Fact]
    public void Guard_PonowniePrzejmujeOsieroconyLock()
    {
        var lockPath = CreateLockPath("osierocony");
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        File.WriteAllText(lockPath, int.MaxValue.ToString());

        using var guard = new SingleInstanceGuard(lockPath);

        Assert.True(guard.TryAcquire());
    }

    private static string CreateLockPath(string name) =>
        Path.Combine(Path.GetTempPath(), "radar-guard-tests", name, "radar.lock");
}
