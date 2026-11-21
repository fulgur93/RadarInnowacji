namespace RadarInnowacji.Core.Models;

/// <summary>Główne kategorie Kickstartera (Id używane przez parametr category_id).</summary>
public sealed record KickstarterCategory(int Id, string Name);

public static class KickstarterCategories
{
    public static IReadOnlyList<KickstarterCategory> All { get; } =
    [
        new(16, "Technologia"),
        new(7, "Design"),
        new(26, "Rękodzieło"),
        new(12, "Gry"),
        new(10, "Jedzenie"),
        new(11, "Film i wideo"),
        new(1, "Sztuka"),
        new(3, "Komiksy"),
        new(6, "Taniec"),
        new(9, "Moda"),
        new(13, "Dziennikarstwo"),
        new(14, "Muzyka"),
        new(15, "Fotografia"),
        new(17, "Teatr"),
        new(18, "Wydawnictwa")
    ];

    public static string Label(int id) => All.FirstOrDefault(x => x.Id == id)?.Name ?? id.ToString();
}
