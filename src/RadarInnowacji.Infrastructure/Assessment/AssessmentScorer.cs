using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Assessment;

/// <summary>
/// Karta oceny potencjału: sześć kryteriów w skali 0–5 liczonych jawnymi regułami.
/// AI jest tylko jednym z sygnałów; ocenę każdego kryterium może zmienić ekspert.
/// </summary>
public static class AssessmentScorer
{
    public const int MaxScore = 5;
    private const int SimilarityRecentDays = 180;

    public static IReadOnlyList<CriterionInfo> Criteria { get; } =
    [
        new(AssessmentCriterion.Nowosc, "Nowość",
            "Czy rozwiązanie wnosi coś nowego względem znanych projektów?",
            [
                "punkt wyjścia: 2 (brak dowodów za ani przeciw)",
                "+1 – AI wskazało, co może być nowe",
                "+1 – AI: potencjalna innowacja z pewnością ≥ 60%; −1 – AI: nie jest innowacją z pewnością ≥ 60%",
                "+1 – brak bardzo podobnych projektów w bazie (maks. podobieństwo < 0,50)",
                "−1 – istnieje bardzo podobny projekt (podobieństwo ≥ 0,80)"
            ]),
        new(AssessmentCriterion.ProblemIWplyw, "Problem i wpływ",
            "Czy rozwiązuje opisany, istotny problem i dla kogo?",
            [
                "punkt wyjścia: 1",
                "+1 – opisany problem (≥ 30 znaków)",
                "+1 – opisane rozwiązanie (≥ 30 znaków)",
                "+1 – obszar o wpływie społecznym (innowacja społeczna/edukacyjna albo sektor: edukacja, zdrowie, jakość życia, środowisko)",
                "+1 – podobny problem rozwiązują inne instytucje (potwierdzone zapotrzebowanie)"
            ]),
        new(AssessmentCriterion.Przenoszalnosc, "Możliwość przeniesienia i skalowania",
            "Czy rozwiązanie da się zastosować w innych instytucjach?",
            [
                "punkt wyjścia: 1",
                "+1 – podobnym problemem zajmuje się co najmniej 1 inna instytucja; kolejne +1 – co najmniej 3",
                "+1 – otwarta licencja (np. MIT, Apache, GPL, CC)",
                "+1 – rozwiązanie organizacyjne, edukacyjne, społeczne lub procesowe (niski próg przeniesienia) albo dostępna dokumentacja"
            ]),
        new(AssessmentCriterion.Dojrzalosc, "Dojrzałość",
            "Na jakim etapie jest rozwiązanie?",
            [
                "pomysł: 1, w realizacji: 2, prototyp: 3, wdrożone: 5",
                "+1 – prototyp z aktywnością w ostatnich 180 dniach",
                "etap nieznany: 1 (2 przy aktywności w ostatnich 180 dniach)"
            ]),
        new(AssessmentCriterion.GotowoscWdrozeniowa, "Gotowość do wdrożenia",
            "Czy można rozpocząć wdrożenie lub rozmowy z partnerem?",
            [
                "punkt wyjścia: 0",
                "+2 – wdrożone w macierzystej instytucji; +1 – prototyp",
                "+1 – znani twórcy lub zespół",
                "+1 – kontakt do twórcy (podany za zgodą)",
                "+1 – aktywność w ostatnich 180 dniach"
            ]),
        new(AssessmentCriterion.Kompletnosc, "Kompletność informacji",
            "Czy wiemy wystarczająco dużo, by ocena była wiarygodna?",
            [
                "10 kluczowych pól: tytuł, opis (≥ 80 znaków), problem, rozwiązanie, instytucja, rodzaj innowacji, etap, twórcy, region, link",
                "ocena = odsetek wypełnionych pól × 5 (zaokrąglony)",
                "+1 – materiały z co najmniej 2 niezależnych źródeł"
            ])
    ];

    public static CriterionInfo Info(AssessmentCriterion criterion) => Criteria.Single(x => x.Criterion == criterion);

    /// <summary>Ocena automatyczna wszystkich kryteriów.</summary>
    public static AssessmentOutcome Score(
        AssessmentInput input,
        IReadOnlyDictionary<AssessmentCriterion, int> weights,
        IReadOnlyDictionary<AssessmentCriterion, int>? expertScores = null)
    {
        var (completeness, missing) = Completeness(input);
        var results = new List<CriterionResult>
        {
            Novelty(input, weights),
            ProblemAndImpact(input, weights),
            Transferability(input, weights),
            Maturity(input, weights),
            Readiness(input, weights),
            CompletenessCriterion(input, completeness, missing, weights)
        };

        if (expertScores is not null)
        {
            results = results
                .Select(result => expertScores.TryGetValue(result.Criterion, out var expert)
                    ? result with { Score = Math.Clamp(expert, 0, MaxScore) }
                    : result)
                .ToList();
        }

        var confidence = completeness +
                         (input.AiPotential.HasValue ? 10 : 0) +
                         (input.DistinctSources >= 2 ? 10 : 0);
        return new AssessmentOutcome
        {
            Criteria = results,
            TotalScore = Total(results),
            Completeness = completeness,
            MissingFields = missing,
            Confidence = Math.Clamp(confidence, 0, 100)
        };
    }

    /// <summary>Wynik ważony 0–100 (normalizowany sumą wag).</summary>
    public static double Total(IReadOnlyList<CriterionResult> results)
    {
        var weightSum = results.Sum(x => x.Weight);
        if (weightSum <= 0)
        {
            return 0;
        }

        var weighted = results.Sum(x => (double)x.Score / MaxScore * x.Weight);
        return Math.Round(100.0 * weighted / weightSum, 1);
    }

    private static CriterionResult Novelty(AssessmentInput input, IReadOnlyDictionary<AssessmentCriterion, int> weights)
    {
        var score = 2;
        var signals = new List<string> { "punkt wyjścia 2 – brak dowodów za ani przeciw" };
        if (HasValue(input.AiNovelty))
        {
            score++;
            signals.Add($"+1 AI wskazało, co może być nowe: „{Shorten(input.AiNovelty!, 90)}”");
        }

        if (input.AiPotential is not null && input.AiConfidence >= 60)
        {
            if (input.AiPotential.Value)
            {
                score++;
                signals.Add($"+1 AI: potencjalna innowacja (pewność {input.AiConfidence}%) – wskazówka, nie potwierdzenie");
            }
            else
            {
                score--;
                signals.Add($"−1 AI: raczej nie jest innowacją (pewność {input.AiConfidence}%)");
            }
        }

        if (input.MaxSimilarity < 0.5)
        {
            score++;
            signals.Add(input.MaxSimilarity <= 0
                ? "+1 brak podobnych projektów w bazie"
                : $"+1 brak bardzo podobnych projektów w bazie (maks. podobieństwo {input.MaxSimilarity:0.00})");
        }
        else if (input.MaxSimilarity >= 0.8)
        {
            score--;
            signals.Add($"−1 bardzo podobny projekt w bazie: „{input.MostSimilarTitle}” ({input.MaxSimilarity:0.00}) – sprawdź, czy to nie dublowanie prac");
        }
        else
        {
            signals.Add($"0 umiarkowanie podobny projekt: „{input.MostSimilarTitle}” ({input.MaxSimilarity:0.00})");
        }

        return Result(AssessmentCriterion.Nowosc, score, signals, weights);
    }

    private static CriterionResult ProblemAndImpact(AssessmentInput input, IReadOnlyDictionary<AssessmentCriterion, int> weights)
    {
        var score = 1;
        var signals = new List<string> { "punkt wyjścia 1" };
        if (Length(input.Problem) >= 30)
        {
            score++;
            signals.Add("+1 opisany problem");
        }
        else
        {
            signals.Add("0 brak opisu problemu");
        }

        if (Length(input.Solution) >= 30)
        {
            score++;
            signals.Add("+1 opisane rozwiązanie");
        }

        var impactSectors = input.Sectors
            .Where(x => x is "edukacja" or "zdrowie" or "jakość życia" or "środowisko")
            .ToList();
        if (input.Kind is InnovationKind.Spoleczna or InnovationKind.Edukacyjna || impactSectors.Count > 0)
        {
            score++;
            signals.Add(impactSectors.Count > 0
                ? $"+1 obszar o wpływie społecznym: {string.Join(", ", impactSectors)}"
                : $"+1 innowacja {KindName(input.Kind)} – wpływ społeczny");
        }

        if (input.SimilarInOtherInstitutions >= 1)
        {
            score++;
            signals.Add($"+1 podobny problem w {input.SimilarInOtherInstitutions} innych instytucjach – potwierdzone zapotrzebowanie");
        }

        return Result(AssessmentCriterion.ProblemIWplyw, score, signals, weights);
    }

    private static CriterionResult Transferability(AssessmentInput input, IReadOnlyDictionary<AssessmentCriterion, int> weights)
    {
        var score = 1;
        var signals = new List<string> { "punkt wyjścia 1" };
        if (input.SimilarInOtherInstitutions >= 1)
        {
            score++;
            var names = input.OtherInstitutions.Count > 0
                ? $" ({string.Join(", ", input.OtherInstitutions.Take(3))})"
                : string.Empty;
            signals.Add($"+1 podobnym problemem zajmują się inne instytucje: {input.SimilarInOtherInstitutions}{names}");
        }

        if (input.SimilarInOtherInstitutions >= 3)
        {
            score++;
            signals.Add("+1 co najmniej 3 instytucje z podobnym problemem");
        }

        if (input.HasOpenLicense)
        {
            score++;
            signals.Add($"+1 otwarta licencja ({input.License})");
        }

        if (input.Kind is InnovationKind.Organizacyjna or InnovationKind.Edukacyjna or InnovationKind.Spoleczna
            or InnovationKind.Procesowa)
        {
            score++;
            signals.Add($"+1 innowacja {KindName(input.Kind)} – nie wymaga specjalistycznej infrastruktury");
        }
        else if (input.HasDocumentation)
        {
            score++;
            signals.Add("+1 dostępna dokumentacja (README)");
        }

        return Result(AssessmentCriterion.Przenoszalnosc, score, signals, weights);
    }

    private static CriterionResult Maturity(AssessmentInput input, IReadOnlyDictionary<AssessmentCriterion, int> weights)
    {
        var active = IsRecentlyActive(input);
        var signals = new List<string>();
        int score;
        switch (input.Stage)
        {
            case ProjectStatus.Concept:
                score = 1;
                signals.Add("etap: pomysł – 1");
                break;
            case ProjectStatus.InProgress:
                score = 2;
                signals.Add("etap: w realizacji – 2");
                break;
            case ProjectStatus.Prototype:
                score = 3;
                signals.Add("etap: prototyp – 3");
                if (active)
                {
                    score++;
                    signals.Add("+1 prototyp z aktywnością w ostatnich 180 dniach");
                }

                break;
            case ProjectStatus.Deployed:
                score = 5;
                signals.Add("etap: wdrożone w macierzystej instytucji – 5");
                break;
            default:
                score = active ? 2 : 1;
                signals.Add(active
                    ? "etap nieznany, ale projekt był aktywny w ostatnich 180 dniach – 2"
                    : "etap nieznany – 1 (uzupełnij profil projektu)");
                break;
        }

        return Result(AssessmentCriterion.Dojrzalosc, score, signals, weights);
    }

    private static CriterionResult Readiness(AssessmentInput input, IReadOnlyDictionary<AssessmentCriterion, int> weights)
    {
        var score = 0;
        var signals = new List<string> { "punkt wyjścia 0" };
        if (input.Stage == ProjectStatus.Deployed)
        {
            score += 2;
            signals.Add("+2 wdrożone w macierzystej instytucji");
        }
        else if (input.Stage == ProjectStatus.Prototype)
        {
            score++;
            signals.Add("+1 istnieje prototyp");
        }

        if (HasValue(input.Creators))
        {
            score++;
            signals.Add("+1 znani twórcy lub zespół");
        }

        if (input.HasContact)
        {
            score++;
            signals.Add("+1 dostępny kontakt do twórcy (podany za zgodą)");
        }

        if (IsRecentlyActive(input))
        {
            score++;
            signals.Add("+1 aktywność w ostatnich 180 dniach");
        }

        return Result(AssessmentCriterion.GotowoscWdrozeniowa, score, signals, weights);
    }

    private static CriterionResult CompletenessCriterion(
        AssessmentInput input,
        int completeness,
        IReadOnlyList<string> missing,
        IReadOnlyDictionary<AssessmentCriterion, int> weights)
    {
        var score = (int)Math.Round(completeness / 100.0 * MaxScore, MidpointRounding.AwayFromZero);
        var signals = new List<string>
        {
            missing.Count == 0
                ? $"wypełnione wszystkie kluczowe pola ({completeness}%)"
                : $"wypełnione {completeness}% pól; brak: {string.Join(", ", missing)}"
        };
        if (input.DistinctSources >= 2)
        {
            score++;
            signals.Add($"+1 materiały z {input.DistinctSources} niezależnych źródeł");
        }

        return Result(AssessmentCriterion.Kompletnosc, score, signals, weights);
    }

    private static (int Percent, IReadOnlyList<string> Missing) Completeness(AssessmentInput input)
    {
        (string Name, bool Filled)[] fields =
        [
            ("tytuł", Length(input.Title) >= 5),
            ("opis (≥ 80 znaków)", Length(input.Description) >= 80),
            ("problem", Length(input.Problem) >= 20),
            ("rozwiązanie", Length(input.Solution) >= 20),
            ("instytucja", HasValue(input.Institution) || input.InstitutionType != InstitutionType.Nieznany),
            ("rodzaj innowacji", input.Kind != InnovationKind.Nieokreslona),
            ("etap", input.Stage != ProjectStatus.Unknown),
            ("twórcy", HasValue(input.Creators)),
            ("region", HasValue(input.Region)),
            ("link", input.HasLink)
        ];
        var filled = fields.Count(x => x.Filled);
        return ((int)Math.Round(100.0 * filled / fields.Length), fields.Where(x => !x.Filled).Select(x => x.Name).ToList());
    }

    private static CriterionResult Result(
        AssessmentCriterion criterion,
        int score,
        IReadOnlyList<string> signals,
        IReadOnlyDictionary<AssessmentCriterion, int> weights) =>
        new(criterion, Math.Clamp(score, 0, MaxScore), weights.TryGetValue(criterion, out var weight) ? weight : 0, signals);

    private static bool IsRecentlyActive(AssessmentInput input) =>
        input.LastActivityAt is not null && (input.Now - input.LastActivityAt.Value).TotalDays <= SimilarityRecentDays;

    private static bool HasValue(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Trim().StartsWith("brak danych", StringComparison.OrdinalIgnoreCase);

    private static int Length(string? value) => HasValue(value) ? value!.Trim().Length : 0;

    private static string Shorten(string value, int max)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max].TrimEnd() + "…";
    }

    public static string KindName(InnovationKind kind) => kind switch
    {
        InnovationKind.Technologiczna => "technologiczna",
        InnovationKind.Organizacyjna => "organizacyjna",
        InnovationKind.Edukacyjna => "edukacyjna",
        InnovationKind.Spoleczna => "społeczna",
        InnovationKind.Procesowa => "procesowa",
        _ => "nieokreślona"
    };
}
