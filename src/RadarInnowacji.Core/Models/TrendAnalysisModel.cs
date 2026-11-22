namespace RadarInnowacji.Core.Models;

/// <summary>
/// Analiza zmian liczby unikalnych projektów w kategoriach. Nie wykorzystuje liczby
/// pobranych materiałów jako miary innowacyjności.
/// </summary>
public sealed class TrendAnalysisModel
{
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public string PeriodLabel { get; init; } = string.Empty;
    public int TotalNewProjects { get; init; }
    public int TotalProjects { get; init; }
    public int MonitoredSources { get; init; }
    public int ScanIntervalMinutes { get; init; }
    public IReadOnlyList<CategoryTrendPoint> Points { get; init; } = Array.Empty<CategoryTrendPoint>();
    public IReadOnlyList<CategoryTrendSummary> Categories { get; init; } = Array.Empty<CategoryTrendSummary>();
    public IReadOnlyList<CategoryTrendSummary> MostActive { get; init; } = Array.Empty<CategoryTrendSummary>();
    public IReadOnlyList<CategoryTrendSummary> FastestGrowing { get; init; } = Array.Empty<CategoryTrendSummary>();
    public IReadOnlyList<CategoryTrendSummary> NewCategories { get; init; } = Array.Empty<CategoryTrendSummary>();
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record CategoryTrendPoint(string Period, int CategoryId, string CategoryName, int NewProjects);

public sealed class CategoryTrendSummary
{
    public int CategoryId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int CurrentCount { get; init; }
    public int PreviousCount { get; init; }
    public double? GrowthPercent { get; init; }
    public bool IsNew { get; init; }
    public int TotalProjects { get; init; }
    public int SourceCount { get; init; }
}

public sealed class CategoryComparison
{
    public int CategoryId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int ProjectCount { get; init; }
    public int DiscoveryCount { get; init; }
    public int SourceCount { get; init; }
    public int NewProjectsLastPeriod { get; init; }
    public double AverageFreshness { get; init; }
}

public sealed record ProjectActivityPoint(string Date, int NewProjects, int NewDiscoveries);
