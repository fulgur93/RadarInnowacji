namespace RadarInnowacji.Infrastructure.Configuration;

/// <summary>Ustawienia radaru repozytoriów (GitHub, GitLab): limity na jeden skan źródła.</summary>
public sealed class RepositoryOptions
{
    /// <summary>Maksymalna liczba projektów zapisanych z jednego skanu źródła.</summary>
    public int MaxItemsPerScan { get; set; } = 300;

    /// <summary>Maksymalna liczba stron (po 100 wyników) dla jednego zakresu dat; GitHub pozwala na 10.</summary>
    public int MaxPagesPerQuery { get; set; } = 10;

    /// <summary>Maksymalna liczba wywołań API w jednym skanie źródła.</summary>
    public int MaxRequestsPerScan { get; set; } = 60;

    /// <summary>Jak długo skan może czekać na odnowienie limitu API, zanim zakończy się częściowo.</summary>
    public int MaxRateLimitWaitSeconds { get; set; } = 65;

    /// <summary>Ile ostatnich godzin GitHub sprawdzać ponownie (opóźnienie indeksowania wyszukiwarki).</summary>
    public int OverlapHours { get; set; } = 2;
}
