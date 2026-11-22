using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Assessment;

/// <summary>
/// Rekomendacja ścieżki na podstawie jawnej, uporządkowanej tabeli reguł.
/// Zwraca najwyżej dwie ścieżki; każda zawiera reguły, które zadziałały, z wartościami.
/// </summary>
public static class RecommendationEngine
{
    public const int MaxRecommendations = 2;

    public static IReadOnlyList<RecommendationRuleInfo> Rules { get; } =
    [
        new(1, DevelopmentPath.UzupelnienieInformacji, "kompletność informacji < 40% (sprawdzana zawsze jako pierwsza)"),
        new(2, DevelopmentPath.Obserwacja, "wynik < 35 przy pewności oceny ≥ 60% (wtedy nie są proponowane inne ścieżki)"),
        new(3, DevelopmentPath.TransferDoInstytucji, "innowacja organizacyjna, edukacyjna, społeczna lub procesowa; przenoszalność ≥ 3; etap: prototyp lub wdrożone"),
        new(4, DevelopmentPath.WdrozenieWSektorzePublicznym, "instytucja: szkoła, samorząd lub inna instytucja publiczna; etap: wdrożone"),
        new(5, DevelopmentPath.Komercjalizacja, "innowacja technologiczna lub nieokreślona; dojrzałość ≥ 4; przenoszalność ≥ 3; wynik ≥ 65"),
        new(6, DevelopmentPath.OchronaIpIAkceleracja, "innowacja technologiczna; nowość ≥ 4; etap najwyżej prototyp"),
        new(7, DevelopmentPath.DalszyRozwoj, "nowość ≥ 3 i dojrzałość ≤ 2"),
        new(8, DevelopmentPath.DalszyRozwoj, "żadna z reguł 2–7 nie zadziałała (ścieżka domyślna)")
    ];

    public static IReadOnlyList<RecommendationResult> Recommend(AssessmentInput input, AssessmentOutcome outcome)
    {
        var novelty = outcome.Score(AssessmentCriterion.Nowosc);
        var transfer = outcome.Score(AssessmentCriterion.Przenoszalnosc);
        var maturity = outcome.Score(AssessmentCriterion.Dojrzalosc);
        var paths = new List<(DevelopmentPath Path, List<string> Reasons)>();

        if (outcome.Completeness < 40)
        {
            paths.Add((DevelopmentPath.UzupelnienieInformacji,
            [
                $"reguła 1: kompletność {outcome.Completeness}% < 40%",
                outcome.MissingFields.Count > 0 ? "brakuje: " + string.Join(", ", outcome.MissingFields) : "uzupełnij opis"
            ]));
        }

        if (outcome.TotalScore < 35 && outcome.Confidence >= 60)
        {
            paths.Add((DevelopmentPath.Obserwacja,
                [$"reguła 2: wynik {outcome.TotalScore:0.0} < 35 przy pewności {outcome.Confidence}% ≥ 60%"]));
            return Ranked(paths);
        }

        var main = new List<(DevelopmentPath Path, List<string> Reasons)>();
        var nonTechnical = input.Kind is InnovationKind.Organizacyjna or InnovationKind.Edukacyjna
            or InnovationKind.Spoleczna or InnovationKind.Procesowa;
        if (nonTechnical && transfer >= 3 && input.Stage is ProjectStatus.Prototype or ProjectStatus.Deployed)
        {
            main.Add((DevelopmentPath.TransferDoInstytucji,
            [
                $"reguła 3: innowacja {AssessmentScorer.KindName(input.Kind)}, przenoszalność {transfer}/5 ≥ 3, etap: {StageName(input.Stage)}",
                input.SimilarInOtherInstitutions > 0
                    ? $"podobny problem w {input.SimilarInOtherInstitutions} innych instytucjach"
                    : "brak jeszcze znanych instytucji z podobnym problemem – warto ich poszukać"
            ]));
        }

        if (input.InstitutionType is InstitutionType.Szkola or InstitutionType.Samorzad or InstitutionType.InstytucjaPubliczna &&
            input.Stage == ProjectStatus.Deployed)
        {
            main.Add((DevelopmentPath.WdrozenieWSektorzePublicznym,
                [$"reguła 4: instytucja „{InstitutionName(input.InstitutionType)}”, rozwiązanie już wdrożone u siebie"]));
        }

        if (input.Kind is InnovationKind.Technologiczna or InnovationKind.Nieokreslona &&
            maturity >= 4 && transfer >= 3 && outcome.TotalScore >= 65)
        {
            main.Add((DevelopmentPath.Komercjalizacja,
                [$"reguła 5: dojrzałość {maturity}/5 ≥ 4, przenoszalność {transfer}/5 ≥ 3, wynik {outcome.TotalScore:0.0} ≥ 65"]));
        }

        if (input.Kind == InnovationKind.Technologiczna && novelty >= 4 &&
            input.Stage is not ProjectStatus.Deployed)
        {
            main.Add((DevelopmentPath.OchronaIpIAkceleracja,
                [$"reguła 6: innowacja technologiczna, nowość {novelty}/5 ≥ 4, etap: {StageName(input.Stage)}"]));
        }

        if (novelty >= 3 && maturity <= 2)
        {
            main.Add((DevelopmentPath.DalszyRozwoj, [$"reguła 7: nowość {novelty}/5 ≥ 3 przy dojrzałości {maturity}/5 ≤ 2"]));
        }

        if (main.Count == 0)
        {
            main.Add((DevelopmentPath.DalszyRozwoj, ["reguła 8: brak warunków dla innych ścieżek – ścieżka domyślna"]));
        }

        paths.AddRange(main.DistinctBy(x => x.Path));
        return Ranked(paths);
    }

    private static IReadOnlyList<RecommendationResult> Ranked(List<(DevelopmentPath Path, List<string> Reasons)> paths) =>
        paths
            .DistinctBy(x => x.Path)
            .Take(MaxRecommendations)
            .Select((x, index) => new RecommendationResult(x.Path, index + 1, x.Reasons))
            .ToList();

    public static string StageName(ProjectStatus stage) => stage switch
    {
        ProjectStatus.Concept => "pomysł",
        ProjectStatus.InProgress => "w realizacji",
        ProjectStatus.Prototype => "prototyp",
        ProjectStatus.Deployed => "wdrożone",
        _ => "nieznany"
    };

    public static string InstitutionName(InstitutionType type) => type switch
    {
        InstitutionType.Szkola => "szkoła",
        InstitutionType.Uczelnia => "uczelnia",
        InstitutionType.Samorzad => "samorząd",
        InstitutionType.InstytucjaPubliczna => "instytucja publiczna",
        InstitutionType.OrganizacjaPozarzadowa => "organizacja pozarządowa",
        InstitutionType.Firma => "firma",
        InstitutionType.Inna => "inna",
        _ => "nieznany"
    };
}
