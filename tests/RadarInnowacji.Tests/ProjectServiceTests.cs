using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Tests;

public class ProjectServiceTests
{
    [Fact]
    public async Task SzczegolyProjektu_PokazujeOdkryciaZWieluZrodel()
    {
        using var database = new SqliteTestDatabase();
        var first = SourceTestFactory.Create(SourceType.Rss, "https://a.example/feed", "Źródło A");
        var second = SourceTestFactory.Create(SourceType.GitHubApi, "https://b.example/api", "Źródło B");
        second.Radar = RadarKind.Repozytorium;
        database.Context.Sources.AddRange(first, second);
        await database.Context.SaveChangesAsync();

        var project = await AddProjectAsync(database, "Czujniki jakości powietrza", "Monitoring powietrza w szkołach.",
            (first, "Odkrycie uczelni", "external-1"), (second, "Kopia w repozytorium", "external-2"));

        var model = await CreateService(database).GetDetailsAsync(project.Id);

        Assert.NotNull(model);
        Assert.Equal(2, model.SourceCount);
        Assert.Equal(2, model.Discoveries.Count);
        Assert.Contains(model.Radars, x => x == RadarKind.Uczelnia);
        Assert.Contains(model.Radars, x => x == RadarKind.Repozytorium);
        Assert.Contains(model.Discoveries, x => x.SourceName == "Źródło A");
        Assert.Contains(model.Discoveries, x => x.SourceName == "Źródło B");
    }

    [Fact]
    public async Task Deduplikacja_POddanymExternalIdLaczyProjektyIBezUsuwaniaOdkryc()
    {
        using var database = new SqliteTestDatabase();
        var source = SourceTestFactory.Create(SourceType.Rss, "https://a.example/feed", "Źródło A");
        var secondSource = SourceTestFactory.Create(SourceType.GitHubApi, "https://b.example/api", "Źródło B");
        database.Context.Sources.AddRange(source, secondSource);
        await database.Context.SaveChangesAsync();

        var older = await AddProjectAsync(database, "Czujnik powietrza", "Pomiar jakości powietrza.",
            daysAgo: 10, (source, "Pierwsze odkrycie", "wspolny-id"));
        var newer = await AddProjectAsync(database, "Czujnik powietrza kopia", "Pomiar jakości powietrza.",
            daysAgo: 5, (secondSource, "Drugie odkrycie", "wspolny-id"));

        var result = await CreateService(database).DetectDuplicatesAsync();

        Assert.True(result.AutoLinked >= 1);
        Assert.Empty(await database.Context.ProjectMergeProposals.ToListAsync());
        Assert.Equal(2, await database.Context.Discoveries.CountAsync());
        var merged = await database.Context.InnovationProjects.Include(x => x.DiscoveryLinks)
            .SingleAsync(x => x.Id == newer.Id);
        Assert.Equal(older.Id, merged.MergedIntoProjectId);
        var target = await database.Context.InnovationProjects.Include(x => x.DiscoveryLinks)
            .SingleAsync(x => x.Id == older.Id);
        Assert.Equal(2, target.DiscoveryLinks.Count);
        Assert.Single(await database.Context.ProjectMergeHistories.ToListAsync());
    }

    [Fact]
    public async Task Deduplikacja_NiejednoznacznePodobienstwoTworzyPropozycjeZamiastLaczyc()
    {
        using var database = new SqliteTestDatabase();
        var (first, second) = await AddAmbiguousPairAsync(database);

        var result = await CreateService(database).DetectDuplicatesAsync();

        Assert.Equal(0, result.AutoLinked);
        Assert.Equal(1, result.ProposalsCreated);
        var proposal = Assert.Single(await database.Context.ProjectMergeProposals.ToListAsync());
        Assert.Equal(MergeProposalStatus.Pending, proposal.Status);
        Assert.Null((await database.Context.InnovationProjects.SingleAsync(x => x.Id == first.Id)).MergedIntoProjectId);
        Assert.Null((await database.Context.InnovationProjects.SingleAsync(x => x.Id == second.Id)).MergedIntoProjectId);
    }

    [Fact]
    public async Task AkceptacjaPropozycji_PrzenosiOdkryciaIZachowujeMaterialy()
    {
        using var database = new SqliteTestDatabase();
        await AddAmbiguousPairAsync(database);
        var service = CreateService(database);
        await service.DetectDuplicatesAsync();
        var proposal = Assert.Single(await database.Context.ProjectMergeProposals.ToListAsync());

        Assert.True(await service.AcceptProposalAsync(proposal.Id));

        Assert.Equal(2, await database.Context.Discoveries.CountAsync());
        var source = await database.Context.InnovationProjects.Include(x => x.DiscoveryLinks)
            .SingleAsync(x => x.Id == proposal.SourceProjectId);
        var target = await database.Context.InnovationProjects.Include(x => x.DiscoveryLinks)
            .SingleAsync(x => x.Id == proposal.TargetProjectId);
        Assert.Equal(proposal.TargetProjectId, source.MergedIntoProjectId);
        Assert.Empty(source.DiscoveryLinks);
        Assert.Equal(2, target.DiscoveryLinks.Count);
        Assert.Equal(MergeProposalStatus.Accepted,
            (await database.Context.ProjectMergeProposals.SingleAsync()).Status);
        Assert.Single(await database.Context.ProjectMergeHistories.ToListAsync());
    }

    [Fact]
    public async Task OdrzuconaPropozycja_NieJestPonawianaPrzyKolejnejDeduplikacji()
    {
        using var database = new SqliteTestDatabase();
        await AddAmbiguousPairAsync(database);
        var service = CreateService(database);
        await service.DetectDuplicatesAsync();
        var proposal = Assert.Single(await database.Context.ProjectMergeProposals.ToListAsync());

        Assert.True(await service.RejectProposalAsync(proposal.Id));
        var secondRun = await service.DetectDuplicatesAsync();

        Assert.Equal(0, secondRun.ProposalsCreated);
        var stored = Assert.Single(await database.Context.ProjectMergeProposals.ToListAsync());
        Assert.Equal(MergeProposalStatus.Rejected, stored.Status);
    }

    [Fact]
    public async Task GetPageAsync_FiltrujeWedlugZrodelRadarowILiczbyZrodel()
    {
        using var database = new SqliteTestDatabase();
        var university = SourceTestFactory.Create(SourceType.Rss, "https://a.example/feed", "Uczelnia A");
        var repository = SourceTestFactory.Create(SourceType.GitHubApi, "https://b.example/api", "Repo B");
        repository.Radar = RadarKind.Repozytorium;
        database.Context.Sources.AddRange(university, repository);
        await database.Context.SaveChangesAsync();

        await AddProjectAsync(database, "Projekt Jednoźródłowy", "Opis.",
            (university, "Odkrycie A", "id-1"));
        var multi = await AddProjectAsync(database, "Projekt Wielu Źródeł", "Opis.",
            (university, "Odkrycie B", "id-2"), (repository, "Odkrycie C", "id-3"));

        var service = CreateService(database);
        var bySources = await service.GetPageAsync(new ProjectFilter { MinSourceCount = 2 });
        var bySource = await service.GetPageAsync(new ProjectFilter { SourceId = repository.Id });
        var byRadar = await service.GetPageAsync(new ProjectFilter { Radar = RadarKind.Repozytorium });

        Assert.Equal(1, bySources.TotalCount);
        Assert.Equal(multi.Id, Assert.Single(bySources.Items).Id);
        Assert.Equal(multi.Id, Assert.Single(bySource.Items).Id);
        Assert.Equal(multi.Id, Assert.Single(byRadar.Items).Id);
    }

    private static async Task<(InnovationProject First, InnovationProject Second)> AddAmbiguousPairAsync(
        SqliteTestDatabase database)
    {
        var firstSource = SourceTestFactory.Create(SourceType.Rss, "https://a.example/feed", "Źródło A");
        var secondSource = SourceTestFactory.Create(SourceType.GitHubApi, "https://b.example/api", "Źródło B");
        database.Context.Sources.AddRange(firstSource, secondSource);
        await database.Context.SaveChangesAsync();

        var first = await AddProjectAsync(database, "Czujnik jakości powietrza w szkołach",
            "Czujnik mierzy jakość powietrza w szkołach i klasach.",
            daysAgo: 10, (firstSource, "Czujnik jakości powietrza w szkołach", "id-A"));
        var second = await AddProjectAsync(database, "Czujnik jakości powietrza w klasach",
            "Czujnik mierzy jakość powietrza w klasach i szkołach.",
            daysAgo: 9, (secondSource, "Czujnik jakości powietrza w klasach", "id-B"));

        // Kontrolowane wektory dają podobieństwo 0,75 – poniżej progu automatycznego łączenia.
        AddVector(database, first, 1f, 0f);
        AddVector(database, second, 0.75f, 0.6614378f);
        await database.Context.SaveChangesAsync();
        return (first, second);
    }

    private static Task<InnovationProject> AddProjectAsync(
        SqliteTestDatabase database,
        string title,
        string description,
        params (Source Source, string Title, string ExternalId)[] discoveries) =>
        AddProjectAsync(database, title, description, 7, discoveries);

    private static async Task<InnovationProject> AddProjectAsync(
        SqliteTestDatabase database,
        string title,
        string description,
        int daysAgo,
        params (Source Source, string Title, string ExternalId)[] discoveries)
    {
        var project = new InnovationProject
        {
            Title = title,
            Description = description,
            FirstDiscoveredAt = DateTime.UtcNow.AddDays(-daysAgo),
            CreatedAt = DateTime.UtcNow.AddDays(-daysAgo)
        };
        database.Context.InnovationProjects.Add(project);
        await database.Context.SaveChangesAsync();

        for (var index = 0; index < discoveries.Length; index++)
        {
            var (source, discoveryTitle, externalId) = discoveries[index];
            var discovery = new Discovery
            {
                Title = discoveryTitle,
                Description = description,
                Source = source,
                SourceUrl = $"https://example.org/{project.Id}/{index}",
                ExternalId = externalId,
                ContentHash = $"hash-{project.Id}-{index}",
                DiscoveredAt = DateTime.UtcNow.AddDays(-daysAgo),
                PublishedAt = DateTime.UtcNow.AddDays(-daysAgo),
                ProcessingStatus = ProcessingStatus.AiProcessed
            };
            database.Context.DiscoveryProjects.Add(new DiscoveryProject
            {
                Discovery = discovery,
                Project = project,
                Similarity = 1
            });
        }

        await database.Context.SaveChangesAsync();
        return project;
    }

    private static void AddVector(SqliteTestDatabase database, InnovationProject project, float first, float second)
    {
        var vector = new float[TextFeatures.EmbeddingDimensions];
        vector[0] = first;
        vector[1] = second;
        database.Context.ProjectEmbeddings.Add(new ProjectEmbedding
        {
            Project = project,
            Vector = JsonSerializer.Serialize(vector),
            Model = "test",
            GeneratedAt = DateTime.UtcNow
        });
    }

    private static ProjectService CreateService(SqliteTestDatabase database) => new(
        database.Context,
        new LocalHashEmbeddingService(),
        Options.Create(new RadarOptions
        {
            Projects = new ProjectOptions
            {
                Enabled = true,
                ProposalSimilarity = 0.62,
                AutoMergeSimilarity = 0.93,
                MaxProjectsPerRun = 100,
                SimilarProjectsLimit = 8
            }
        }),
        NullLogger<ProjectService>.Instance);
}
