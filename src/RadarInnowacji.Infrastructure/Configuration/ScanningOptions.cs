namespace RadarInnowacji.Infrastructure.Configuration;

/// <summary>Ustawienia cyklicznego skanowania źródeł w tle.</summary>
public sealed class ScanningOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 15;
    public int StartDelaySeconds { get; set; } = 10;
    public int MaxItemsPerSource { get; set; } = 50;
    /// <summary>Tryb ciągły: po zakończeniu cyklu czeka krótko i uruchamia kolejny.</summary>
    public bool Continuous { get; set; } = false;
    public int ContinuousDelaySeconds { get; set; } = 30;
}
