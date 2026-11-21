using RadarInnowacji.Infrastructure.Configuration;

namespace RadarInnowacji.Tests;

public class RadarDataPathsTests
{
    [Fact]
    public void DatabasePath_UwzgledniaZmiennaSrodowiskowa()
    {
        var expected = Path.Combine(Path.GetTempPath(), "radar-sciezka-test.db");
        Environment.SetEnvironmentVariable(RadarDataPaths.DatabaseEnvironmentVariable, expected);

        try
        {
            Assert.Equal(Path.GetFullPath(expected), RadarDataPaths.DatabasePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable(RadarDataPaths.DatabaseEnvironmentVariable, null);
        }
    }

    [Fact]
    public void AppDataDirectory_ZawieraPodkatalogAplikacji()
    {
        var root = Path.Combine(Path.GetTempPath(), "radar-dane-test");
        Environment.SetEnvironmentVariable(RadarDataPaths.DataDirectoryEnvironmentVariable, root);

        try
        {
            Assert.Equal("RadarInnowacji", Path.GetFileName(RadarDataPaths.AppDataDirectory));
        }
        finally
        {
            Environment.SetEnvironmentVariable(RadarDataPaths.DataDirectoryEnvironmentVariable, null);
        }
    }
}
