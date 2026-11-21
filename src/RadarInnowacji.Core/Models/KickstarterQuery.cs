using System.Text.Json.Serialization;

namespace RadarInnowacji.Core.Models;

/// <summary>
/// Zapytanie wyszukiwania kampanii Kickstartera. Zapisywane w konfiguracji źródła
/// pod kluczem "kickstarterQuery"; adapter składa z niego adresy publicznego wyszukiwania.
/// </summary>
public sealed record KickstarterQuery
{
    public const int MaxKeywords = 10;
    public const int MaxTermLength = 100;
    public const int MaxCategories = 10;
    public const int MinDays = 1;
    public const int MaxDays = 365;
    public const int DefaultLaunchedWithinDays = 30;
    public const string DefaultState = "live";
    public const string DefaultSort = "newest";

    /// <summary>Słowa kluczowe szukane w nazwie i opisie kampanii.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>Identyfikatory kategorii Kickstartera (np. 16 = Technologia).</summary>
    public IReadOnlyList<int> CategoryIds { get; init; } = [];

    /// <summary>Stan kampanii zgodny z filtrami Kickstartera.</summary>
    public string State { get; init; } = DefaultState;

    /// <summary>Sortowanie: newest albo end_date. Celowo bez sortowania po popularności.</summary>
    public string Sort { get; init; } = DefaultSort;

    /// <summary>Okno czasowe: kampanie rozpoczęte w ciągu ostatnich N dni.</summary>
    public int LaunchedWithinDays { get; init; } = DefaultLaunchedWithinDays;

    public static IReadOnlyList<string> DefaultKeywords { get; } =
    [
        "prototype",
        "innowacyjny",
        "nowa metoda",
        "sensor",
        "robot"
    ];

    public static IReadOnlyList<string> AllowedStates { get; } =
        ["live", "upcoming", "successful", "failed", "canceled"];

    public static IReadOnlyList<string> AllowedSorts { get; } = [DefaultSort, "end_date"];

    /// <summary>Dzieli tekst wpisany przez użytkownika (przecinki, średniki, nowe linie) na listę.</summary>
    public static IReadOnlyList<string> ParseList(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

    /// <summary>Wyciąga dodatnie identyfikatory kategorii, pomijając wpisy niebędące liczbą.</summary>
    public static IReadOnlyList<int> ParseCategoryIds(string? text) =>
        ParseList(text)
            .Select(value => int.TryParse(value, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .Take(MaxCategories)
            .ToList();

    /// <summary>Zwraca znormalizowaną kopię zapytania (limity, unikalność, dozwolone wartości).</summary>
    public KickstarterQuery Normalize() => this with
    {
        Keywords = CleanKeywords(Keywords),
        CategoryIds = CategoryIds.Where(id => id > 0).Distinct().Take(MaxCategories).ToList(),
        State = AllowedStates.Contains(State?.ToLowerInvariant() ?? string.Empty)
            ? State!.ToLowerInvariant()
            : DefaultState,
        Sort = AllowedSorts.Contains(Sort?.ToLowerInvariant() ?? string.Empty)
            ? Sort!.ToLowerInvariant()
            : DefaultSort,
        LaunchedWithinDays = Math.Clamp(LaunchedWithinDays, MinDays, MaxDays)
    };

    /// <summary>Zapytanie musi zawierać przynajmniej jedno słowo kluczowe lub kategorię.</summary>
    [JsonIgnore]
    public bool HasSearchTerms => Keywords.Count > 0 || CategoryIds.Count > 0;

    /// <summary>Początek okna czasowego (północ UTC N dni temu).</summary>
    public DateTime WindowStart(DateTime nowUtc) =>
        DateTime.SpecifyKind(nowUtc.Date.AddDays(-Normalize().LaunchedWithinDays), DateTimeKind.Utc);

    private static List<string> CleanKeywords(IReadOnlyList<string>? values) =>
        (values ?? [])
            .Select(value => new string((value ?? string.Empty)
                .Where(ch => !char.IsControl(ch) && ch is not ('"' or '\'' or '`'))
                .ToArray()))
            .Select(value => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .Where(value => value.Length > 0)
            .Select(value => value.Length <= MaxTermLength ? value : value[..MaxTermLength])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxKeywords)
            .ToList();
}
