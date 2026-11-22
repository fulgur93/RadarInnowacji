using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Models;

/// <summary>Filtry wspólnego widoku projektów ze wszystkich radarów i źródeł.</summary>
public sealed class ProjectFilter
{
    public RadarKind? Radar { get; set; }
    public int? SourceId { get; set; }
    public int? CategoryId { get; set; }
    public string? SearchText { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    /// <summary>Minimalna liczba różnych źródeł, w których odnaleziono projekt.</summary>
    public int? MinSourceCount { get; set; }

    public bool IncludeMerged { get; set; }
    public ProjectSort SortBy { get; set; } = ProjectSort.DiscoveredAt;
    public bool SortDescending { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
