namespace RadarInnowacji.Core.Models;

public sealed class DiscoveryPage
{
    public IReadOnlyList<DiscoveryListItem> Items { get; init; } = Array.Empty<DiscoveryListItem>();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    public int PageCount => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}
