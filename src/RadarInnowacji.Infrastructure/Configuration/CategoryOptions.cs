namespace RadarInnowacji.Infrastructure.Configuration;

/// <summary>Ustawienia lokalnego grupowania projektów i wykrywania nowych kategorii.</summary>
public sealed class CategoryOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
    public int StartDelaySeconds { get; set; } = 30;
    public int MaxProjectsPerRun { get; set; } = 300;

    /// <summary>Minimalne podobieństwo kosinusowe, by uznać dwa projekty za podobne.</summary>
    public double MinSimilarity { get; set; } = 0.40;

    /// <summary>Minimalne podobieństwo projektu do istniejącej kategorii.</summary>
    public double ExistingCategorySimilarity { get; set; } = 0.26;

    /// <summary>Najmniejsza liczba projektów w grupie, która może stać się nową kategorią.</summary>
    public int MinClusterSize { get; set; } = 2;

    /// <summary>Liczba istniejących kategorii, które można dodać do jednego projektu w jednym cyklu.</summary>
    public int MaxCategoriesPerProject { get; set; } = 2;

    /// <summary>"Lokalny" (deterministyczny, bez modelu) albo "Ollama" (model embeddingowy).</summary>
    public string EmbeddingProvider { get; set; } = "Lokalny";
    public string OllamaEmbeddingModel { get; set; } = "multilingual-e5-small";
    public string OllamaBaseUrl { get; set; } = "http://127.0.0.1:11434";
    public int EmbeddingTimeoutSeconds { get; set; } = 120;
}
