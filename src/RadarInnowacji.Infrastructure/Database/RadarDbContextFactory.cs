using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RadarInnowacji.Infrastructure.Configuration;

namespace RadarInnowacji.Infrastructure.Database;

/// <summary>
/// Fabryka dla narzędzi EF Core. Zapobiega uruchamianiu hosta WWW (i blokady instancji)
/// podczas tworzenia i stosowania migracji.
/// </summary>
public sealed class RadarDbContextFactory : IDesignTimeDbContextFactory<RadarDbContext>
{
    public RadarDbContext CreateDbContext(string[] args)
    {
        RadarDataPaths.EnsureDirectories();
        var options = new DbContextOptionsBuilder<RadarDbContext>()
            .UseSqlite($"Data Source={RadarDataPaths.DatabasePath}")
            .Options;

        return new RadarDbContext(options);
    }
}
