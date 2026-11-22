using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Assessment;
using RadarInnowacji.Infrastructure.Configuration;

namespace RadarInnowacji.Tests;

public class AssessmentRulesTests
{
    private static readonly IReadOnlyDictionary<AssessmentCriterion, int> Weights =
        new AssessmentOptions().EffectiveWeights();

    private static readonly DateTime Now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Wagi_DomyslnieSumujaSieDo100()
    {
        Assert.Equal(100, Weights.Values.Sum());
        Assert.Equal(6, Weights.Count);
    }

    [Fact]
    public void Wagi_BrakujaceLubUjemneZastepujeDomyslnymi()
    {
        var options = new AssessmentOptions { Weights = new Dictionary<string, int> { ["Nowosc"] = -5 } };

        var weights = options.EffectiveWeights();

        Assert.Equal(20, weights[AssessmentCriterion.Nowosc]);
        Assert.Equal(10, weights[AssessmentCriterion.Kompletnosc]);
    }

    [Fact]
    public void Nowosc_PunktWyjsciaIWszystkieSygnalyPozytywne()
    {
        var neutral = AssessmentScorer.Score(Input(maxSimilarity: 0.6), Weights);
        var strong = AssessmentScorer.Score(Input(aiNovelty: "Nowa metoda", aiPotential: true, aiConfidence: 80, maxSimilarity: 0.2), Weights);

        Assert.Equal(2, neutral.Score(AssessmentCriterion.Nowosc));
        Assert.Equal(5, strong.Score(AssessmentCriterion.Nowosc));
        Assert.Contains(strong.Criteria.Single(x => x.Criterion == AssessmentCriterion.Nowosc).Signals,
            x => x.Contains("wskazówka, nie potwierdzenie"));
    }

    [Fact]
    public void Nowosc_BardzoPodobnyProjektObnizaOceneIOstrzegaPrzedDublowaniem()
    {
        var outcome = AssessmentScorer.Score(Input(maxSimilarity: 0.85, mostSimilar: "Projekt X"), Weights);

        Assert.Equal(1, outcome.Score(AssessmentCriterion.Nowosc));
        Assert.Contains(outcome.Criteria.Single(x => x.Criterion == AssessmentCriterion.Nowosc).Signals,
            x => x.Contains("Projekt X") && x.Contains("dublowanie"));
    }

    [Theory]
    [InlineData(ProjectStatus.Unknown, 1)]
    [InlineData(ProjectStatus.Concept, 1)]
    [InlineData(ProjectStatus.InProgress, 2)]
    [InlineData(ProjectStatus.Prototype, 3)]
    [InlineData(ProjectStatus.Deployed, 5)]
    public void Dojrzalosc_ZalezyOdEtapu(ProjectStatus stage, int expected)
    {
        var outcome = AssessmentScorer.Score(Input(stage: stage, lastActivity: Now.AddDays(-400)), Weights);

        Assert.Equal(expected, outcome.Score(AssessmentCriterion.Dojrzalosc));
    }

    [Fact]
    public void Dojrzalosc_PrototypZNiedawnaAktywnosciaDostajeDodatkowyPunkt()
    {
        var outcome = AssessmentScorer.Score(Input(stage: ProjectStatus.Prototype, lastActivity: Now.AddDays(-10)), Weights);

        Assert.Equal(4, outcome.Score(AssessmentCriterion.Dojrzalosc));
    }

    [Fact]
    public void Przenoszalnosc_InneInstytucjeLicencjaIRodzajNietechnologiczny()
    {
        var outcome = AssessmentScorer.Score(Input(
            kind: InnovationKind.Organizacyjna,
            otherInstitutions: 3,
            license: "MIT"), Weights);

        Assert.Equal(5, outcome.Score(AssessmentCriterion.Przenoszalnosc));
    }

    [Fact]
    public void Kompletnosc_WskazujeBrakujacePolaIWplywaNaPewnosc()
    {
        var empty = AssessmentScorer.Score(new AssessmentInput { Title = "Projekt", Now = Now }, Weights);
        var full = AssessmentScorer.Score(Input(), Weights);

        Assert.Equal(10, empty.Completeness);
        Assert.Contains("problem", empty.MissingFields);
        Assert.Equal(100, full.Completeness);
        Assert.Empty(full.MissingFields);
        Assert.True(full.Confidence > empty.Confidence);
    }

    [Fact]
    public void Wynik_JestNormalizowanyDo100IUwzglednaOceneEksperta()
    {
        var automatic = AssessmentScorer.Score(Input(), Weights);
        var expert = AssessmentScorer.Score(Input(), Weights, new Dictionary<AssessmentCriterion, int>
        {
            [AssessmentCriterion.Nowosc] = 5,
            [AssessmentCriterion.ProblemIWplyw] = 5,
            [AssessmentCriterion.Przenoszalnosc] = 5,
            [AssessmentCriterion.Dojrzalosc] = 5,
            [AssessmentCriterion.GotowoscWdrozeniowa] = 5,
            [AssessmentCriterion.Kompletnosc] = 5
        });

        Assert.InRange(automatic.TotalScore, 0, 100);
        Assert.Equal(100, expert.TotalScore);
    }

    [Fact]
    public void Rekomendacja_NiskaKompletnoscNajpierwUzupelnienieInformacji()
    {
        var input = new AssessmentInput { Title = "Krótki", Description = "Opis", Now = Now };
        var outcome = AssessmentScorer.Score(input, Weights);

        var result = RecommendationEngine.Recommend(input, outcome);

        Assert.Equal(DevelopmentPath.UzupelnienieInformacji, result[0].Path);
        Assert.Equal(1, result[0].Rank);
        Assert.Contains(result[0].Reasons, x => x.Contains("reguła 1"));
    }

    [Fact]
    public void Rekomendacja_InnowacjaEdukacyjnaWdrozonaWSzkoleToTransferIWdrozenie()
    {
        var input = Input(
            kind: InnovationKind.Edukacyjna,
            institutionType: InstitutionType.Szkola,
            stage: ProjectStatus.Deployed,
            otherInstitutions: 1);
        var outcome = AssessmentScorer.Score(input, Weights);

        var result = RecommendationEngine.Recommend(input, outcome);

        Assert.Equal(
            [DevelopmentPath.TransferDoInstytucji, DevelopmentPath.WdrozenieWSektorzePublicznym],
            result.Select(x => x.Path).ToArray());
    }

    [Fact]
    public void Rekomendacja_NowaTechnologiaNaWczesnymEtapieToOchronaIpIRozwoj()
    {
        var input = Input(
            kind: InnovationKind.Technologiczna,
            stage: ProjectStatus.InProgress,
            aiNovelty: "Nowa metoda",
            aiPotential: true,
            aiConfidence: 80,
            maxSimilarity: 0.3);
        var outcome = AssessmentScorer.Score(input, Weights);

        var result = RecommendationEngine.Recommend(input, outcome);

        Assert.Equal(DevelopmentPath.OchronaIpIAkceleracja, result[0].Path);
        Assert.Equal(DevelopmentPath.DalszyRozwoj, result[1].Path);
    }

    [Fact]
    public void Rekomendacja_DojrzalaTechnologiaZWysokimWynikiemToKomercjalizacja()
    {
        var input = Input(
            kind: InnovationKind.Technologiczna,
            stage: ProjectStatus.Deployed,
            aiNovelty: "Nowa metoda",
            aiPotential: true,
            aiConfidence: 80,
            maxSimilarity: 0.3,
            otherInstitutions: 2,
            license: "Apache-2.0");
        var outcome = AssessmentScorer.Score(input, Weights);

        var result = RecommendationEngine.Recommend(input, outcome);

        Assert.True(outcome.TotalScore >= 65);
        Assert.Contains(result, x => x.Path == DevelopmentPath.Komercjalizacja);
    }

    [Fact]
    public void Rekomendacja_NiskiWynikPrzyWysokiejPewnosciToTylkoObserwacja()
    {
        var input = Input(stage: ProjectStatus.Concept, aiPotential: false, aiConfidence: 90, maxSimilarity: 0.9,
            lastActivity: Now.AddDays(-900));
        var outcome = AssessmentScorer.Score(input, Weights, new Dictionary<AssessmentCriterion, int>
        {
            [AssessmentCriterion.ProblemIWplyw] = 0,
            [AssessmentCriterion.Przenoszalnosc] = 0
        });

        var result = RecommendationEngine.Recommend(input, outcome);

        Assert.Equal(DevelopmentPath.Obserwacja, Assert.Single(result).Path);
    }

    [Fact]
    public void Rekomendacja_ZawszeNajwyzejDwieSciezkiIKazdaMaKatalogKrokowIMiernikow()
    {
        foreach (var path in Enum.GetValues<DevelopmentPath>())
        {
            var info = DevelopmentPathCatalog.Get(path);
            Assert.NotEmpty(info.NextSteps);
            Assert.NotEmpty(info.Metrics);
        }

        var input = Input(kind: InnovationKind.Edukacyjna, institutionType: InstitutionType.Szkola,
            stage: ProjectStatus.Deployed, otherInstitutions: 3);
        Assert.True(RecommendationEngine.Recommend(input, AssessmentScorer.Score(input, Weights)).Count <= 2);
    }

    [Fact]
    public void Sektory_RozpoznawanePoPoczatkuSlowa()
    {
        var sectors = SectorDictionary.Detect("Dron do inspekcji dla szkół i aplikacja dla uczniów");
        var notEducation = SectorDictionary.Detect("Klasyfikacja obrazów");

        Assert.Contains("lotnictwo", sectors);
        Assert.Contains("edukacja", sectors);
        Assert.Contains("ICT", sectors);
        Assert.DoesNotContain("edukacja", notEducation);
    }

    [Fact]
    public void Odbiorcy_DopasowaniePoSektorzeSciezceIEtapieZUzasadnieniem()
    {
        var schools = new Recipient
        {
            Id = 1, Name = "Szkoły", Type = RecipientType.Szkoly, IsActive = true,
            Sectors = "edukacja", Keywords = "szkoła, uczniowie",
            Paths = "TransferDoInstytucji", MinimumStage = ProjectStatus.Prototype
        };
        var investors = new Recipient
        {
            Id = 2, Name = "Inwestorzy", Type = RecipientType.Inwestorzy, IsActive = true,
            Sectors = "lotnictwo", Paths = "Komercjalizacja", MinimumStage = ProjectStatus.Prototype
        };
        var inactive = new Recipient { Id = 3, Name = "Wyłączony", Sectors = "edukacja", IsActive = false };

        var matches = RecipientMatcher.Match(new RecipientMatchInput
        {
            Sectors = ["edukacja"],
            Text = "Uczniowie prowadzą lekcje w szkole",
            Stage = ProjectStatus.Deployed,
            Paths = [DevelopmentPath.TransferDoInstytucji]
        }, [schools, investors, inactive]);

        var match = Assert.Single(matches);
        Assert.Equal("Szkoły", match.Name);
        Assert.Equal(80, match.Score); // 20 sektor + 10 słowa kluczowe + 30 ścieżka + 10 typ + 10 etap
        Assert.Contains(match.Reasons, x => x.Contains("słowa kluczowe: szkoła, uczniowie"));
    }

    [Fact]
    public void Odbiorcy_ProjektZaWczesnyDlaOdbiorcyNieDostajePunktowZaEtap()
    {
        var recipient = new Recipient
        {
            Id = 1, Name = "Firmy ICT", Type = RecipientType.Firmy, IsActive = true,
            Sectors = "ICT", Paths = "Komercjalizacja", MinimumStage = ProjectStatus.Prototype
        };

        var match = Assert.Single(RecipientMatcher.Match(new RecipientMatchInput
        {
            Sectors = ["ICT"],
            Stage = ProjectStatus.Concept,
            Paths = [DevelopmentPath.Komercjalizacja]
        }, [recipient]));

        Assert.Contains(match.Reasons, x => x.StartsWith("0 odbiorca zajmuje się projektami od etapu"));
    }

    [Theory]
    [InlineData(PipelineStage.Zgloszona, PipelineStage.WOcenie, true)]
    [InlineData(PipelineStage.Oceniona, PipelineStage.Rekomendowana, true)]
    [InlineData(PipelineStage.Rekomendowana, PipelineStage.Skierowana, true)]
    [InlineData(PipelineStage.Wykryta, PipelineStage.Wdrozona, false)]
    [InlineData(PipelineStage.Zgloszona, PipelineStage.Skierowana, false)]
    [InlineData(PipelineStage.Wdrozona, PipelineStage.WOcenie, false)]
    [InlineData(PipelineStage.Odrzucona, PipelineStage.WOcenie, true)]
    public void Etapy_DozwolonePrzejscia(PipelineStage from, PipelineStage to, bool allowed) =>
        Assert.Equal(allowed, PipelineRules.CanMove(from, to));

    private static AssessmentInput Input(
        InnovationKind kind = InnovationKind.Technologiczna,
        InstitutionType institutionType = InstitutionType.Uczelnia,
        ProjectStatus stage = ProjectStatus.Prototype,
        string? aiNovelty = null,
        bool? aiPotential = null,
        int? aiConfidence = null,
        double maxSimilarity = 0.6,
        string? mostSimilar = "Podobny projekt",
        int otherInstitutions = 0,
        string? license = null,
        DateTime? lastActivity = null) => new()
    {
        Title = "Testowy projekt",
        Description = new string('a', 100),
        Problem = "Opis problemu, który projekt rozwiązuje w instytucji.",
        Solution = "Opis rozwiązania, które zostało przygotowane przez zespół.",
        Kind = kind,
        InstitutionType = institutionType,
        Institution = "Instytucja testowa",
        Stage = stage,
        Region = "podkarpackie",
        Creators = "Zespół testowy",
        HasLink = true,
        AiNovelty = aiNovelty,
        AiPotential = aiPotential,
        AiConfidence = aiConfidence,
        MaxSimilarity = maxSimilarity,
        MostSimilarTitle = mostSimilar,
        SimilarInOtherInstitutions = otherInstitutions,
        OtherInstitutions = Enumerable.Range(1, otherInstitutions).Select(x => $"Instytucja {x}").ToList(),
        License = license,
        HasOpenLicense = license is not null,
        LastActivityAt = lastActivity ?? Now.AddDays(-30),
        Now = Now
    };
}
