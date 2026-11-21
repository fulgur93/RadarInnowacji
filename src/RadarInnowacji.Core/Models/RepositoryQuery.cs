using System.Text.Json.Serialization;

namespace RadarInnowacji.Core.Models;

/// <summary>
/// Zapytanie radaru repozytoriów (GitHub, GitLab). Zapisywane w konfiguracji źródła
/// pod kluczem "query"; adapter składa z niego adresy wyszukiwania.
/// </summary>
public sealed record RepositoryQuery
{
    public const int MaxTerms = 20;
    public const int MaxTermLength = 100;
    public const int MaxLanguages = 5;
    public const int MinDays = 1;
    public const int MaxDays = 365;
    public const int DefaultCreatedWithinDays = 30;

    /// <summary>Słowa kluczowe szukane w nazwie, opisie i README (PL i EN).</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>Tematy (GitHub topics / GitLab topics).</summary>
    public IReadOnlyList<string> Topics { get; init; } = [];

    /// <summary>Opcjonalne zawężenie do języków programowania; puste = wszystkie.</summary>
    public IReadOnlyList<string> Languages { get; init; } = [];

    /// <summary>Okno czasowe: projekty utworzone w ciągu ostatnich N dni.</summary>
    public int CreatedWithinDays { get; init; } = DefaultCreatedWithinDays;

    /// <summary>Domyślne słowa kluczowe zgodne z filtrem regułowym specyfikacji.</summary>
    public static IReadOnlyList<string> DefaultKeywords { get; } =
    [
        "prototype",
        "proof of concept",
        "novel method",
        "prototyp",
        "nowa metoda",
        "autonomiczny system"
    ];

    /// <summary>Dzieli tekst wpisany przez użytkownika (przecinki, średniki, nowe linie) na listę.</summary>
    public static IReadOnlyList<string> ParseList(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

    /// <summary>
    /// Zwraca znormalizowaną kopię: bez cudzysłowów i znaków sterujących, bez duplikatów,
    /// z ograniczoną liczbą i długością wpisów oraz oknem czasowym w dozwolonym zakresie.
    /// </summary>
    public RepositoryQuery Normalize() => this with
    {
        Keywords = CleanTerms(Keywords, MaxTerms),
        Topics = CleanTerms(Topics, MaxTerms).Select(ToTopic).Where(x => x.Length > 0).Distinct().ToList(),
        Languages = CleanTerms(Languages, MaxLanguages),
        CreatedWithinDays = Math.Clamp(CreatedWithinDays, MinDays, MaxDays)
    };

    /// <summary>Zapytanie musi zawierać przynajmniej jedno słowo kluczowe lub temat.</summary>
    [JsonIgnore]
    public bool HasSearchTerms => Keywords.Count > 0 || Topics.Count > 0;

    private static List<string> CleanTerms(IReadOnlyList<string>? values, int max) =>
        (values ?? [])
            .Select(value => new string((value ?? string.Empty)
                .Where(ch => !char.IsControl(ch) && ch is not ('"' or '\'' or '`'))
                .ToArray()))
            .Select(value => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .Where(value => value.Length > 0)
            .Select(value => value.Length <= MaxTermLength ? value : value[..MaxTermLength])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();

    // Tematy na GitHubie to małe litery, cyfry i myślniki.
    private static string ToTopic(string value) =>
        new string(value.ToLowerInvariant().Replace(' ', '-')
            .Where(ch => char.IsLetterOrDigit(ch) || ch == '-')
            .ToArray()).Trim('-');
}
