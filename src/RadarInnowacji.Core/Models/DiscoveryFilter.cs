using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Core.Models;

public sealed class DiscoveryFilter
{
    public RadarKind? Radar { get; set; }
    public string? SearchText { get; set; }
    public DateField DateField { get; set; } = DateField.DiscoveredAt;
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int? CategoryId { get; set; }
    public double? MinFreshnessScore { get; set; }
    public int? SourceId { get; set; }
    public ProcessingStatus? ProcessingStatus { get; set; }
    public DiscoveryStatus? Status { get; set; }
    public bool? IsPotentialInnovation { get; set; }

    /// <summary>Ukrywa repozytoria uznane za szum (szablony, szczątkowe README itp.).</summary>
    public bool ExcludeNoise { get; set; } = true;
    public DiscoverySort SortBy { get; set; } = DiscoverySort.DiscoveredAt;
    public bool SortDescending { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
