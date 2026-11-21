namespace RadarInnowacji.Core.Models;

/// <summary>
/// Wynik pobierania ze źródła.
/// </summary>
/// <param name="Items">Pobrane materiały (już ograniczone do limitu na skan).</param>
/// <param name="IsComplete">false, gdy pobieranie przerwał limit API, limit zapytań lub materiałów.</param>
/// <param name="Warning">Powód niepełnego wyniku lub inna uwaga dla użytkownika.</param>
/// <param name="Cursors">
/// Postęp skanowania przyrostowego (klucz wyszukiwania → stan zależny od adaptera,
/// np. data dla GitHub, zakres ID dla GitLab); null = adapter nie korzysta z kursorów.
/// </param>
public sealed record SourceFetchResult(
    IReadOnlyList<SourceItem> Items,
    bool IsComplete,
    string? Warning,
    IReadOnlyDictionary<string, string>? Cursors);
