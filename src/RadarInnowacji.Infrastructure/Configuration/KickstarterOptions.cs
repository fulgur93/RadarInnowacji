namespace RadarInnowacji.Infrastructure.Configuration;

/// <summary>Limity skanowania publicznego wyszukiwania Kickstartera.</summary>
public sealed class KickstarterOptions
{
    /// <summary>Maksymalna liczba kampanii zapisanych z jednego skanu źródła.</summary>
    public int MaxItemsPerScan { get; set; } = 120;

    /// <summary>Maksymalna liczba stron (po 12 wyników) dla jednego wyszukiwania.</summary>
    public int MaxPagesPerQuery { get; set; } = 8;
}
