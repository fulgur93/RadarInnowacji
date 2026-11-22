using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Infrastructure.Assessment;

/// <summary>
/// Rozpoznawanie sektorów na podstawie rdzeni słów (polskich i angielskich) w tekście projektu.
/// Rdzeń musi wystąpić na początku słowa. Słownik jest jawny, więc łatwo sprawdzić,
/// dlaczego projekt trafił do danego sektora.
/// </summary>
public static class SectorDictionary
{
    public static IReadOnlyDictionary<string, string[]> Sectors { get; } = new Dictionary<string, string[]>
    {
        ["motoryzacja"] = ["motoryz", "samochod", "pojazd", "automotive", "elektromobil", "akumulator", "ladowar"],
        ["ICT"] = ["oprogramow", "aplikacj", "software", "algorytm", "sztuczn", "uczenie maszyn", "machine learning",
            "dane", "data", "chmur", "cloud", "platform", "web", "internet rzeczy", "iot", "cyberbezpiecz"],
        ["lotnictwo"] = ["lotnic", "dron", "samolot", "aero", "aviation", "bezzalogow"],
        ["kosmos"] = ["kosmos", "kosmicz", "satelit", "orbit", "space", "rakiet"],
        ["jakość życia"] = ["jakosc zycia", "senior", "niepelnospraw", "dostepnosc", "smog", "powietrz",
            "mieszkan", "zdrowe", "dobrostan"],
        ["zdrowie"] = ["zdrow", "medycz", "medyc", "diagnost", "rehabilit", "szpital", "pacjent", "terapi"],
        ["edukacja"] = ["edukac", "szkol", "uczni", "nauczyc", "dydakty", "lekcj", "student", "nauczan"],
        ["energia"] = ["energ", "fotowolt", "oze", "mikrosie", "magazyn energii", "cieplo", "wodor"],
        ["środowisko"] = ["srodowisk", "ekolog", "recykl", "odpad", "klimat", "gleb", "wod", "zanieczyszcz", "emisj"],
        ["administracja publiczna"] = ["urzad", "gmin", "samorzad", "powiat", "administrac", "mieszkanc", "obywatel",
            "e-uslug", "budzet obywatel"],
        ["przemysł"] = ["przemysl", "produkcj", "fabryk", "robot", "automatyk", "inspekcj", "logistyk", "dostaw"]
    };

    public static string Normalize(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : " " + TextFeatures.Normalize(text).Replace('ł', 'l') + " ";

    /// <summary>Sektory rozpoznane w tekście (kolejność jak w słowniku).</summary>
    public static IReadOnlyList<string> Detect(string? text)
    {
        var normalized = Normalize(text);
        if (normalized.Length == 0)
        {
            return Array.Empty<string>();
        }

        return Sectors
            .Where(sector => sector.Value.Any(stem => normalized.Contains(" " + stem, StringComparison.Ordinal)))
            .Select(sector => sector.Key)
            .ToList();
    }

    /// <summary>Lista elementów rozdzielonych przecinkami lub średnikami.</summary>
    public static IReadOnlyList<string> SplitList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
}
