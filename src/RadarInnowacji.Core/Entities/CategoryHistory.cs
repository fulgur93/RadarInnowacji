namespace RadarInnowacji.Core.Entities;

/// <summary>Zapis zmian w katalogu kategorii (wykrycie, przypisanie, decyzja użytkownika).</summary>
public sealed class CategoryHistory
{
    public int Id { get; set; }
    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Change { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Actor { get; set; } = "System";
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
