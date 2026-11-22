namespace RadarInnowacji.Core.Models;

public sealed class CategoryHistoryItem
{
    public int Id { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Change { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}
