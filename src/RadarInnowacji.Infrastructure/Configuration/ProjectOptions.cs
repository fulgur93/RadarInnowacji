namespace RadarInnowacji.Infrastructure.Configuration;

/// <summary>Ustawienia wykrywania duplikatów i łączenia materiałów w projekty.</summary>
public sealed class ProjectOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 120;
    public int StartDelaySeconds { get; set; } = 90;
    public int MaxProjectsPerRun { get; set; } = 500;

    /// <summary>Poniżej tego progu para nie jest nawet proponowana użytkownikowi.</summary>
    public double ProposalSimilarity { get; set; } = 0.62;

    /// <summary>
    /// Jednoznaczne sygnały (ExternalId, ContentHash) zawsze łączą. Przy embeddingach
    /// wymagane jest też wysokie podobieństwo tekstu – w przeciwnym razie powstaje propozycja.
    /// </summary>
    public double AutoMergeSimilarity { get; set; } = 0.93;

    public int SimilarProjectsLimit { get; set; } = 8;
    public int TrendMonths { get; set; } = 6;
}
