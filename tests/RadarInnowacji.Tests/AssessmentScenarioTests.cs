using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Database;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Tests;

/// <summary>
/// Oba przypadki demonstracyjne z docs/DEMO.md przeprowadzone przez cały proces:
/// zgłoszenie/wykrycie → ocena → rekomendacja → odbiorcy → skierowanie.
/// </summary>
public class AssessmentScenarioTests
{
    /// <summary>Tekst zgłoszenia z przypadku 1 w docs/DEMO.md (ten sam, który wstawia przycisk w formularzu).</summary>
    public static InnovationSubmission SchoolCase() => SubmissionService.DemoExample();

    [Fact]
    public async Task Przypadek1_ZgloszenieSzkolyProwadziDoTransferuIOdbiorcow()
    {
        using var database = new SqliteTestDatabase();
        var services = await PrepareAsync(database);

        var similar = await services.Submission.FindSimilarAsync(SchoolCase().Title, SchoolCase().Description);
        Assert.Contains(similar, x => x.Title == "Interaktywny materiał edukacyjny z fizyki");

        var result = await services.Submission.SubmitAsync(SchoolCase());
        Assert.True(result.Success, string.Join(" ", result.Errors));

        var card = await services.Assessment.GetCardAsync(result.ProjectId!.Value);
        Assert.NotNull(card);
        Assert.Equal(PipelineStage.Zgloszona, card.Stage);
        Assert.Equal(
            [DevelopmentPath.TransferDoInstytucji, DevelopmentPath.WdrozenieWSektorzePublicznym],
            card.Recommendations.Select(x => x.Path).ToArray());
        Assert.Contains(card.SimilarInstitutions, x => x.Institution == "Uniwersytet Kazimierza Wielkiego");
        Assert.Contains("edukacja", card.Sectors);
        Assert.NotEmpty(card.Recommendations[0].Metrics);
        var top = card.RecipientMatches.Take(3).Select(x => x.Name).ToList();
        Assert.Contains("Szkoły ponadpodstawowe w regionie", top);
        Assert.Contains("Samorządy – wydziały edukacji", top);
        Assert.All(card.Criteria, x => Assert.NotEmpty(x.Signals));
    }

    [Fact]
    public async Task Przypadek2_WykrytaTechnologiaUczelniProwadziDoOchronyIpIAkceleracji()
    {
        using var database = new SqliteTestDatabase();
        var services = await PrepareAsync(database);
        var projectId = await database.Context.InnovationProjects
            .Where(x => x.Title == "System wspomagania diagnostyki obrazowej")
            .Select(x => x.Id)
            .SingleAsync();

        var card = await services.Assessment.GetCardAsync(projectId);

        Assert.NotNull(card);
        Assert.Equal(PipelineStage.Wykryta, card.Stage);
        Assert.Equal(DevelopmentPath.OchronaIpIAkceleracja, card.Recommendations[0].Path);
        Assert.True(card.Criteria.Single(x => x.Criterion == AssessmentCriterion.Nowosc).FinalScore >= 4);
        Assert.Contains("zdrowie", card.Sectors);
        Assert.Contains(card.RecipientMatches, x => x.Name == "Centra transferu technologii uczelni");
    }

    [Fact]
    public async Task Proces_OdZgloszeniaDoSkierowaniaZHistoriaDecyzji()
    {
        using var database = new SqliteTestDatabase();
        var services = await PrepareAsync(database);
        var projectId = (await services.Submission.SubmitAsync(SchoolCase())).ProjectId!.Value;

        Assert.False((await services.Pipeline.ChangeStageAsync(projectId, PipelineStage.Skierowana, "")).Success);
        Assert.False((await services.Pipeline.ChangeStageAsync(projectId, PipelineStage.Odrzucona, "")).Success);
        Assert.True((await services.Pipeline.ChangeStageAsync(projectId, PipelineStage.WOcenie, "Weryfikacja")).Success);
        Assert.True((await services.Pipeline.ChangeStageAsync(projectId, PipelineStage.Oceniona, "Ocena OK")).Success);
        Assert.True((await services.Pipeline.ChangeStageAsync(projectId, PipelineStage.Rekomendowana, "Transfer")).Success);

        var card = await services.Assessment.GetCardAsync(projectId);
        var referral = await services.Pipeline.ReferAsync(projectId, card!.RecipientMatches[0].RecipientId, "Prezentacja na sieci szkół");

        Assert.True(referral.Success);
        card = await services.Assessment.GetCardAsync(projectId);
        Assert.Equal(PipelineStage.Skierowana, card!.Stage);
        Assert.Single(card.Referrals);
        Assert.Equal(5, card.Events.Count);
        Assert.Contains(PipelineStage.Pilotaz, card.AllowedStages);
    }

    [Fact]
    public async Task OcenaEksperta_ZmieniaWynikZachowujeHistorieIMoznaJaCofnac()
    {
        using var database = new SqliteTestDatabase();
        var services = await PrepareAsync(database);
        var projectId = (await services.Submission.SubmitAsync(SchoolCase())).ProjectId!.Value;
        var before = (await services.Assessment.GetCardAsync(projectId))!;

        Assert.False((await services.Assessment.SetExpertScoreAsync(projectId, AssessmentCriterion.Nowosc, 5, "")).Success);
        Assert.True((await services.Assessment.SetExpertScoreAsync(
            projectId, AssessmentCriterion.Nowosc, 5, "Brak podobnych programów w regionie")).Success);
        var expert = (await services.Assessment.GetCardAsync(projectId))!;
        var criterion = expert.Criteria.Single(x => x.Criterion == AssessmentCriterion.Nowosc);

        Assert.Equal(CriterionOrigin.Ekspert, criterion.Origin);
        Assert.Equal(5, criterion.FinalScore);
        Assert.Equal(before.Criteria.Single(x => x.Criterion == AssessmentCriterion.Nowosc).AutomaticScore,
            criterion.AutomaticScore);
        Assert.True(expert.TotalScore > before.TotalScore);
        Assert.Equal("Brak podobnych programów w regionie", criterion.ExpertComment);

        Assert.True((await services.Assessment.SetExpertScoreAsync(projectId, AssessmentCriterion.Nowosc, null, "")).Success);
        var restored = (await services.Assessment.GetCardAsync(projectId))!;
        Assert.Equal(before.TotalScore, restored.TotalScore);
        // Cofnięcie nie usuwa wpisu eksperta – zostaje w historii jako nieaktywny.
        var stored = Assert.Single(await database.Context.CriterionOverrides.ToListAsync());
        Assert.False(stored.IsActive);
        Assert.True(restored.History.Count >= 3);
    }

    [Fact]
    public async Task Ocena_NiezmienionyProjektNieTworzyNowejMigawki()
    {
        using var database = new SqliteTestDatabase();
        var services = await PrepareAsync(database);
        var projectId = (await services.Submission.SubmitAsync(SchoolCase())).ProjectId!.Value;

        Assert.False(await services.Assessment.AssessAsync(projectId));
        Assert.Equal(1, await database.Context.ProjectAssessments.CountAsync(x => x.InnovationProjectId == projectId));
        Assert.Equal(1, await database.Context.ProjectAssessments.CountAsync(x => x.InnovationProjectId == projectId && x.IsCurrent));
    }

    [Fact]
    public async Task OcenaWTle_OceniaWszystkieProjektyIPonownieNicNieZmienia()
    {
        using var database = new SqliteTestDatabase();
        var services = await PrepareAsync(database);

        var first = await services.Assessment.AssessPendingAsync();
        var second = await services.Assessment.AssessPendingAsync();

        Assert.Equal(10, first);
        Assert.Equal(0, second);
    }

    [Fact]
    public async Task Profil_UzupelnienieZwiekszaKompletnoscIPrzeliczaOcene()
    {
        using var database = new SqliteTestDatabase();
        var services = await PrepareAsync(database);
        var projectId = await database.Context.InnovationProjects
            .Where(x => x.Title == "Algorytm optymalizacji tras dostaw")
            .Select(x => x.Id)
            .SingleAsync();
        var before = (await services.Assessment.GetCardAsync(projectId))!;
        var profile = before.Profile;
        profile.Creators = "Zespół algorytmiczny";
        profile.Problem = "Firmy kurierskie tracą czas przez trasy nieuwzględniające ruchu.";

        var bad = await services.Assessment.UpdateProfileAsync(projectId, new ProjectProfile
        {
            ContactEmail = "zespol@example.org",
            ContactConsent = false
        });
        var ok = await services.Assessment.UpdateProfileAsync(projectId, profile);
        var after = (await services.Assessment.GetCardAsync(projectId))!;

        Assert.False(bad.Success);
        Assert.True(ok.Success);
        Assert.True(after.Completeness >= before.Completeness);
    }

    [Fact]
    public async Task Zgloszenie_WalidacjaIPonowneWyslanieTegoSamego()
    {
        using var database = new SqliteTestDatabase();
        var services = await PrepareAsync(database);

        var empty = await services.Submission.SubmitAsync(new InnovationSubmission());
        var noConsent = SchoolCase();
        noConsent.ContactEmail = "nauczyciel@example.org";
        var withoutConsent = await services.Submission.SubmitAsync(noConsent);
        var badLink = SchoolCase();
        badLink.Link = "javascript:alert(1)";
        var linkResult = await services.Submission.SubmitAsync(badLink);
        var first = await services.Submission.SubmitAsync(SchoolCase());
        var repeated = await services.Submission.SubmitAsync(SchoolCase());

        Assert.False(empty.Success);
        Assert.Contains(empty.Errors, x => x.Contains("typ instytucji"));
        Assert.Contains(withoutConsent.Errors, x => x.Contains("zgody"));
        Assert.Contains(linkResult.Errors, x => x.Contains("http"));
        Assert.True(first.Success);
        Assert.False(repeated.Success);
        Assert.Equal(first.ProjectId, repeated.ProjectId);

        var discovery = await database.Context.Discoveries.SingleAsync(x => x.Source.Name == SubmissionService.SourceName);
        Assert.Equal($"/projekty/{first.ProjectId}", discovery.SourceUrl);
        Assert.Equal(ProcessingStatus.Pending, discovery.ProcessingStatus);
        Assert.False(discovery.IsDemo);
        var source = await database.Context.Sources.SingleAsync(x => x.Name == SubmissionService.SourceName);
        Assert.Equal(RadarKind.Zgloszenia, source.Radar);
        Assert.Equal(SourceType.Manual, source.SourceType);
    }

    [Fact]
    public async Task Lejek_IEksport_ZawierajaOcenyISciezki()
    {
        using var database = new SqliteTestDatabase();
        var services = await PrepareAsync(database);
        var submitted = await services.Submission.SubmitAsync(SchoolCase());
        await services.Assessment.AssessPendingAsync();

        var funnel = await services.Pipeline.GetFunnelAsync(new FunnelFilter { Path = DevelopmentPath.TransferDoInstytucji });
        var csv = await services.Pipeline.ExportCsvAsync();

        Assert.Equal(11, funnel.TotalProjects);
        Assert.Equal(11, funnel.AssessedProjects);
        Assert.Equal(1, funnel.StageCounts[PipelineStage.Zgloszona]);
        Assert.Contains(funnel.Items, x => x.ProjectId == submitted.ProjectId);
        Assert.StartsWith("Id;Tytuł;Instytucja", csv);
        Assert.Contains("Transfer do innych instytucji", csv);
        Assert.Equal("'=SUMA(A1)", PipelineService.Csv("=SUMA(A1)"));
        Assert.Equal("\"a;b\"", PipelineService.Csv("a;b"));
    }

    [Fact]
    public async Task UsunieciePrzykladow_UsuwaOcenyProjektowDemoIZachowujeZgloszenia()
    {
        using var database = new SqliteTestDatabase();
        var services = await PrepareAsync(database);
        var submitted = await services.Submission.SubmitAsync(SchoolCase());
        await services.Assessment.AssessPendingAsync();

        await new DemoDataSeeder(database.Context).ClearAsync();

        database.Context.ChangeTracker.Clear();
        Assert.Equal(submitted.ProjectId, (await database.Context.InnovationProjects.SingleAsync()).Id);
        Assert.Equal(1, await database.Context.ProjectAssessments.CountAsync());
        Assert.Equal(13, await database.Context.Recipients.CountAsync());
    }

    private static async Task<Services> PrepareAsync(SqliteTestDatabase database)
    {
        await new DemoDataSeeder(database.Context).LoadAsync();
        var recipients = new RecipientService(database.Context);
        Assert.Equal(13, await recipients.SeedExamplesAsync());
        Assert.Equal(0, await recipients.SeedExamplesAsync());

        var options = Options.Create(new RadarOptions());
        var projects = new ProjectService(
            database.Context,
            new LocalHashEmbeddingService(),
            options,
            NullLogger<ProjectService>.Instance);
        var assessment = new AssessmentService(database.Context, projects, options, NullLogger<AssessmentService>.Instance);
        return new Services(
            new SubmissionService(database.Context, projects, assessment, NullLogger<SubmissionService>.Instance),
            assessment,
            new PipelineService(database.Context));
    }

    private sealed record Services(SubmissionService Submission, AssessmentService Assessment, PipelineService Pipeline);
}
