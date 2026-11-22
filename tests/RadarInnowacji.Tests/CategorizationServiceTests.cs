using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Tests;

public class CategorizationServiceTests
{
    [Fact]
    public async Task Grupowanie_LaczyPodobneProjektyIProponujeNowaKategorie()
    {
        using var database = new SqliteTestDatabase();
        await AddSourceAndDiscoveriesAsync(database,
            ("Inteligentna stacja pomiaru jakości powietrza",
                "Czujniki PM2,5 i CO2 monitorują jakość powietrza w szkołach."),
            ("Czujniki zanieczyszczeń dla szkół",
                "Niskokosztowe czujniki zanieczyszczeń powietrza i monitoring jakości powietrza."),
            ("Autonomiczny robot inspekcyjny",
                "Robot z wizją komputerową do inspekcji infrastruktury przemysłowej."),
            ("System wspomagania diagnostyki obrazowej",
                "Analiza obrazów medycznych z wskazaniem podejrzanych obszarów."));

        var service = CreateService(database);
        var result = await service.AnalyzeAsync();

        Assert.Equal(4, result.ProjectsAnalyzed);
        Assert.True(result.ClustersFound >= 1, "Nie wykryto grupy podobnych projektów.");
        Assert.True(result.ProposalsCreated >= 1, "Nie zaproponowano nowej kategorii.");
        Assert.Equal(4, await database.Context.ProjectEmbeddings.CountAsync());

        var proposals = await service.GetProposalsAsync();
        var proposal = Assert.Single(proposals, x => x.Status == CategoryProposalStatus.Pending);
        Assert.True(proposal.Projects.Count >= 2, "Propozycja nie obejmuje co najmniej dwóch projektów.");
        Assert.False(string.IsNullOrWhiteSpace(proposal.Name));

        var clusters = await service.GetClustersAsync();
        Assert.Contains(clusters, x => x.ProjectCount >= 2);

        var accepted = await service.AcceptProposalAsync(proposal.Id);
        Assert.True(accepted);
        Assert.True(await database.Context.Categories.AnyAsync(x => x.Name == proposal.Name && !x.IsDemo));
        var links = await database.Context.ProjectCategories.CountAsync();
        Assert.True(links >= proposal.Projects.Count);
        Assert.Contains(await service.GetHistoryAsync(), x => x.Change == "Utworzenie kategorii");

        var secondRun = await service.AnalyzeAsync();
        Assert.Equal(0, secondRun.ProposalsCreated);
    }

    [Fact]
    public async Task OdrzuconaPropozycja_NieJestPowtarzanaWKolejnymCyku()
    {
        using var database = new SqliteTestDatabase();
        await AddSourceAndDiscoveriesAsync(database,
            ("Czujniki wilgotności gleby", "Czujniki wilgotności gleby monitorują uprawy rolnicze."),
            ("System monitorowania gleby", "System czujników gleby do precyzyjnego monitorowania wilgotności."));

        var service = CreateService(database);
        var first = await service.AnalyzeAsync();
        Assert.Equal(1, first.ProposalsCreated);
        var proposal = Assert.Single(await service.GetProposalsAsync());

        Assert.True(await service.RejectProposalAsync(proposal.Id));
        var second = await service.AnalyzeAsync();

        Assert.Equal(0, second.ProposalsCreated);
        Assert.All(await service.GetProposalsAsync(), x => Assert.Equal(CategoryProposalStatus.Rejected, x.Status));
        Assert.Contains(await service.GetHistoryAsync(), x => x.Change == "Odrzucenie propozycji");
    }

    [Fact]
    public async Task AkceptacjaPropozycji_ZachowujeIstniejacePrzypisaniaWieluKategorii()
    {
        using var database = new SqliteTestDatabase();
        var source = SourceTestFactory.Create(SourceType.Rss, "https://example.org/feed", "Źródło testowe");
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();
        var existing = new Category { Name = "Edukacja i społeczeństwo", Description = "Narzędzia edukacyjne" };
        database.Context.Categories.Add(existing);
        var discovery = NewDiscovery(source, "Czujniki powietrza w klasach", "Czujniki monitorują powietrze w szkołach.", 1);
        database.Context.Discoveries.Add(discovery);
        database.Context.InnovationProjects.Add(new InnovationProject
        {
            Title = "Czujniki powietrza w klasach",
            DiscoveryLinks = [new DiscoveryProject { Discovery = discovery }],
            CategoryLinks = [new ProjectCategory { Category = existing }]
        });
        await database.Context.SaveChangesAsync();

        var proposal = new CategoryProposal
        {
            Name = "Monitoring powietrza",
            Description = "Propozycja testowa",
            Signature = "test-monitoring",
            Similarity = 0.7,
            Status = CategoryProposalStatus.Pending
        };
        proposal.ProjectLinks.Add(new CategoryProposalProject
        {
            Project = database.Context.InnovationProjects.Single(),
            Similarity = 0.7
        });
        database.Context.CategoryProposals.Add(proposal);
        await database.Context.SaveChangesAsync();

        var service = CreateService(database);
        Assert.True(await service.AcceptProposalAsync(proposal.Id));

        var project = await database.Context.InnovationProjects
            .Include(x => x.CategoryLinks)
            .SingleAsync();
        Assert.Equal(2, project.CategoryLinks.Count);
        Assert.Contains(project.CategoryLinks, x => x.CategoryId == existing.Id);
    }

    [Fact]
    public async Task PojedynczyProjekt_NiePowodujePropozycjiKategorii()
    {
        using var database = new SqliteTestDatabase();
        await AddSourceAndDiscoveriesAsync(database,
            ("Nowy egzoszkielet rehabilitacyjny", "Urządzenie wspomagające ruch kończyn dolnych."));

        var result = await CreateService(database).AnalyzeAsync();

        Assert.Equal(0, result.ClustersFound);
        Assert.Equal(0, result.ProposalsCreated);
        Assert.Empty(await database.Context.CategoryProposals.ToListAsync());
    }

    private static CategorizationService CreateService(SqliteTestDatabase database) => new(
        database.Context,
        new LocalHashEmbeddingService(),
        Options.Create(new RadarOptions
        {
            Categories = new CategoryOptions
            {
                Enabled = true,
                MinSimilarity = 0.32,
                ExistingCategorySimilarity = 0.26,
                MinClusterSize = 2,
                MaxCategoriesPerProject = 2,
                MaxProjectsPerRun = 100
            }
        }),
        NullLogger<CategorizationService>.Instance);

    private static async Task AddSourceAndDiscoveriesAsync(
        SqliteTestDatabase database,
        params (string Title, string Description)[] items)
    {
        var source = SourceTestFactory.Create(SourceType.Rss, "https://example.org/feed", "Źródło testowe");
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();
        for (var i = 0; i < items.Length; i++)
        {
            database.Context.Discoveries.Add(NewDiscovery(source, items[i].Title, items[i].Description, i + 1));
        }

        await database.Context.SaveChangesAsync();
    }

    private static Discovery NewDiscovery(Source source, string title, string description, int number)
    {
        var now = DateTime.UtcNow;
        return new Discovery
        {
            Title = title,
            Description = description,
            Source = source,
            SourceUrl = $"https://example.org/material/{number}",
            ExternalId = $"test-{number}",
            PublishedAt = now.AddDays(-number),
            DiscoveredAt = now.AddDays(-number),
            LastActivityAt = now.AddDays(-number),
            ProcessingStatus = ProcessingStatus.AiProcessed
        };
    }
}
