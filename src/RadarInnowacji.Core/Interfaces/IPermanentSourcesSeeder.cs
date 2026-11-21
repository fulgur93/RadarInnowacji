namespace RadarInnowacji.Core.Interfaces;

/// <summary>
/// Uzupełnia bazę o stałe, rzeczywiste źródła dostępne już po czystej instalacji.
/// </summary>
public interface IPermanentSourcesSeeder
{
    /// <summary>Dodaje brakujące źródła i zwraca ich liczbę.</summary>
    Task<int> SeedAsync(CancellationToken cancellationToken = default);
}
