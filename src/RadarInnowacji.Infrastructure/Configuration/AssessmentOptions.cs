using RadarInnowacji.Core.Enums;

namespace RadarInnowacji.Infrastructure.Configuration;

/// <summary>Ustawienia karty oceny potencjału i rekomendacji (Etap 9).</summary>
public sealed class AssessmentOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
    public int StartDelaySeconds { get; set; } = 120;

    /// <summary>Ile projektów ocenić w jednym cyklu zadania w tle.</summary>
    public int MaxProjectsPerRun { get; set; } = 300;

    /// <summary>Wagi kryteriów; wynik jest normalizowany do 0–100 niezależnie od sumy wag.</summary>
    public Dictionary<string, int> Weights { get; set; } = new()
    {
        [nameof(AssessmentCriterion.Nowosc)] = 20,
        [nameof(AssessmentCriterion.ProblemIWplyw)] = 20,
        [nameof(AssessmentCriterion.Przenoszalnosc)] = 20,
        [nameof(AssessmentCriterion.Dojrzalosc)] = 15,
        [nameof(AssessmentCriterion.GotowoscWdrozeniowa)] = 15,
        [nameof(AssessmentCriterion.Kompletnosc)] = 10
    };

    /// <summary>Wagi dla wszystkich kryteriów; brakujące lub ujemne wartości zastępuje domyślnymi.</summary>
    public IReadOnlyDictionary<AssessmentCriterion, int> EffectiveWeights()
    {
        var defaults = new AssessmentOptions().Weights;
        var result = new Dictionary<AssessmentCriterion, int>();
        foreach (var criterion in Enum.GetValues<AssessmentCriterion>())
        {
            var name = criterion.ToString();
            result[criterion] = Weights.TryGetValue(name, out var weight) && weight >= 0
                ? Math.Min(weight, 100)
                : defaults[name];
        }

        return result;
    }
}
