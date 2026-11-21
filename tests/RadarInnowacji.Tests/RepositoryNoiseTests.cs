using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Adapters;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Services;

namespace RadarInnowacji.Tests;

/// <summary>Filtry szumu i deduplikacja repozytoriów (Etap 3, krok 4).</summary>
public class RepositoryNoiseTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private const string LongReadme = """
        # Autonomous greenhouse controller
        ![build](https://img.shields.io/badge/build-passing-green.svg)
        System sterowania szklarnią, który przewiduje zapotrzebowanie roślin na wodę na podstawie
        pomiarów wilgotności gleby, prognozy pogody i zdjęć z kamery. Model uczy się na danych
        z czujników i steruje zaworami bez udziału człowieka. Repozytorium zawiera firmware dla
        ESP32, serwer z modelem predykcyjnym oraz panel do podglądu pomiarów i ręcznego sterowania.
        Projekt powstał w ramach badań nad ograniczeniem zużycia wody w uprawach szklarniowych.
        """;

    private sealed class FixedTime(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc);
    }

    [Theory]
    [InlineData("lab/robo", false, false, false, null)]
    [InlineData("lab/robo", true, false, false, "fork")]
    [InlineData("lab/robo", false, true, false, "mirror")]
    [InlineData("lab/robo", false, false, true, "template")]
    [InlineData("lab/awesome-robotics", false, false, false, "awesome")]
    [InlineData("lab/AwesomeLists", false, false, false, "awesome")]
    [InlineData("jan/dotfiles", false, false, false, "dotfiles")]
    [InlineData("jan/.github", false, false, false, "dotfiles")]
    [InlineData("jan/jan", false, false, false, "personal")]
    [InlineData("jan/jan.github.io", false, false, false, "personal")]
    [InlineData("jan/portfolio", false, false, false, "personal")]
    [InlineData("jan/homework-3", false, false, false, "coursework")]
    [InlineData("jan/sieci-lab-02", false, false, false, "coursework")]
    [InlineData("jan/hw3", false, false, false, "coursework")]
    [InlineData("jan/cs50-problem-sets", false, false, false, "coursework")]
    [InlineData("jan/pytorch-tutorial", false, false, false, "coursework")]
    [InlineData("chandler.bing/ecoscan-1046147-deletion_scheduled-89289060", false, false, false, "deleted")]
    [InlineData("lab/machine-learning-robot", false, false, false, null)]
    [InlineData("lab/hw-accelerator", false, false, false, null)]
    [InlineData("lab/odin-compiler", false, false, false, null)]
    [InlineData("lab/drone-lab", false, false, false, null)]
    [InlineData("grupa/podgrupa/learning-to-grasp", false, false, false, null)]
    public void ClassifyListing_OdrzucaTylkoOczywistySzum(string fullName, bool fork, bool mirror, bool template, string? expected)
    {
        var item = Item(fullName, "Opis projektu") with
        {
            Repository = Metadata(fullName) with { IsMirror = mirror, IsTemplate = template }
        };
        if (fork)
        {
            item = item with { Repository = item.Repository! with { IsFork = true } };
        }

        Assert.Equal(expected, RepositoryNoiseFilter.ClassifyListing(item)?.Code);
    }

    [Fact]
    public void ClassifyListing_RozpoznajeZadanieZOpisu() =>
        Assert.Equal("coursework", RepositoryNoiseFilter.ClassifyListing(Item("jan/projekt", "Homework for CS101 course"))?.Code);

    [Theory]
    [InlineData(null, null, "no-readme")]
    [InlineData("Krótki opis, ale wystarczający do oceny.", null, null)]
    [InlineData(null, "# test\n\n![x](https://img.shields.io/x.svg)", "readme-trivial")]
    [InlineData("Sterownik szklarni przewidujący podlewanie roślin.", "# test", null)]
    [InlineData(null, "# Projekt\n\n## Getting started\n\nTo make it easy for you to get started with GitLab, here's a list", "readme-template")]
    [InlineData(null, "This project was bootstrapped with [Create React App](https://github.com/facebook/create-react-app).", "readme-template")]
    [InlineData(null, LongReadme, null)]
    public void ClassifyReadme_SzablonyISzczatkoweReadme(string? description, string? readme, string? expected) =>
        Assert.Equal(expected, RepositoryNoiseFilter.ClassifyReadme(description, readme)?.Code);

    [Fact]
    public void ReadmeFingerprint_IgnorujeLinkiIOdznakiAleNieTresc()
    {
        var mirrored = LongReadme
            .Replace("https://img.shields.io/badge/build-passing-green.svg", "https://gitlab.com/lab/x/badges/main/pipeline.svg")
            .Replace("\n", "\r\n");

        Assert.NotNull(RepositoryContent.ReadmeFingerprint(LongReadme));
        Assert.Equal(RepositoryContent.ReadmeFingerprint(LongReadme), RepositoryContent.ReadmeFingerprint(mirrored));
        Assert.NotEqual(RepositoryContent.ReadmeFingerprint(LongReadme),
            RepositoryContent.ReadmeFingerprint(LongReadme.Replace("ESP32", "STM32")));
        Assert.Null(RepositoryContent.ReadmeFingerprint("# Krótkie README"));
    }

    [Fact]
    public void ReferencedRepositories_ParsujeLinkiGitHubIGitLab()
    {
        var refs = RepositoryDeduplicator.ReferencedRepositories(
            "Mirror: https://github.com/Lab/Robo.git, issues: https://gitlab.com/uni/lab/robo/-/issues), [x](https://github.com/lab)",
            null);

        Assert.Equal([("GitHub", "lab/robo"), ("GitLab", "uni/lab/robo")], refs);
    }

    [Fact]
    public async Task ScanService_PomijaSzumZListyIPodajePowody()
    {
        using var database = new SqliteTestDatabase();
        var source = GitHubSource("GitHub A");
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();
        var search = SearchJson(
            (1, "lab/greenhouse", false),
            (2, "jan/robo-fork", true),
            (3, "lab/awesome-iot", false));

        var result = await Service(database, GitHub(search)).ScanSourceAsync(source.Id);

        Assert.Equal(1, result.ItemsNew);
        Assert.Contains("Pominięto 2", result.ErrorMessage);
        Assert.Contains("fork innego repozytorium: 1", result.ErrorMessage);
        Assert.Equal("lab/greenhouse", database.Context.Discoveries.Single().Title);
    }

    [Fact]
    public async Task ScanService_FiltrSzumuMoznaWylaczyc()
    {
        using var database = new SqliteTestDatabase();
        var source = GitHubSource("GitHub A");
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();
        var search = SearchJson((1, "lab/greenhouse", false), (2, "jan/robo-fork", true));

        var result = await Service(database, GitHub(search), new RepositoryOptions { FilterNoise = false })
            .ScanSourceAsync(source.Id);

        Assert.Equal(2, result.ItemsNew);
    }

    [Fact]
    public async Task ScanService_TenSamProjektZDwochZrodelZapisujeRazOdkrycie()
    {
        using var database = new SqliteTestDatabase();
        var first = GitHubSource("GitHub A");
        var second = GitHubSource("GitHub B");
        database.Context.Sources.AddRange(first, second);
        await database.Context.SaveChangesAsync();
        var adapter = GitHub(SearchJson((1, "lab/greenhouse", false)));

        await Service(database, adapter).ScanSourceAsync(first.Id);
        var result = await Service(database, adapter).ScanSourceAsync(second.Id);

        Assert.Equal(0, result.ItemsNew);
        Assert.Contains("Duplikaty projektów z innych źródeł: 1", result.ErrorMessage);
        Assert.Single(database.Context.Discoveries);
    }

    [Fact]
    public async Task ScanService_SzablonReadmeOznaczaSzumIUkrywaNaLiscie()
    {
        using var database = new SqliteTestDatabase();
        var source = GitHubSource("GitHub A");
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();
        var adapter = GitHub(SearchJson((1, "jan/my-app", false)),
            readme: "# my-app\n\nThis template provides a minimal setup to get React working in Vite with HMR.");

        var result = await Service(database, adapter).ScanSourceAsync(source.Id);

        var discovery = await database.Context.Discoveries.Include(x => x.Repository).SingleAsync();
        Assert.Equal("niezmieniony szablon Vite + React README", discovery.Repository!.NoiseReason);
        Assert.Equal(ProcessingStatus.Skipped, discovery.ProcessingStatus);
        Assert.Contains("Oznaczono jako szum po README: 1", result.ErrorMessage);

        var discoveries = new DiscoveryService(database.Context);
        Assert.Equal(0, (await discoveries.GetPageAsync(new DiscoveryFilter())).TotalCount);
        Assert.Equal(1, (await discoveries.GetPageAsync(new DiscoveryFilter { ExcludeNoise = false })).TotalCount);
    }

    [Fact]
    public async Task ScanService_TenSamProjektNaGitHubIGitLabLaczyWJedenProjekt()
    {
        using var database = new SqliteTestDatabase();
        var github = GitHubSource("GitHub A");
        var gitlab = SourceTestFactory.Create(SourceType.GitLabApi, "https://gitlab.com", "GitLab A");
        gitlab.Radar = RadarKind.Repozytorium;
        gitlab.Configuration = SourceConfiguration.Create(null, new RepositoryQuery { Keywords = ["greenhouse"] });
        database.Context.Sources.AddRange(github, gitlab);
        await database.Context.SaveChangesAsync();

        await Service(database, GitHub(SearchJson((1, "lab/greenhouse", false)), readme: LongReadme)).ScanSourceAsync(github.Id);
        // Inna nazwa na GitLabie, to samo README (z innymi odznakami): łączy skrót README.
        var gitlabReadme = LongReadme.Replace("https://img.shields.io/badge/build-passing-green.svg", "https://gitlab.com/x.svg");
        var result = await Service(database, GitLab(gitlabReadme)).ScanSourceAsync(gitlab.Id);

        Assert.Contains("Połączono z tym samym projektem na innej platformie: 1", result.ErrorMessage);
        var project = await database.Context.InnovationProjects.Include(x => x.DiscoveryLinks).SingleAsync();
        Assert.Equal(2, project.DiscoveryLinks.Count);
        Assert.Equal(new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc), project.EarliestKnownDate);

        var githubDiscovery = database.Context.Discoveries.Single(x => x.SourceId == github.Id);
        var details = await new DiscoveryService(database.Context).GetDetailsAsync(githubDiscovery.Id);
        Assert.Equal("Uni / Szklarnia", Assert.Single(details!.RelatedDiscoveries).Title);
    }

    private static SourceItem Item(string fullName, string description) =>
        new SourceItem("1", fullName, description, description, $"https://github.com/{fullName}", Now, Now, null, null, [])
        {
            Repository = Metadata(fullName)
        };

    private static RepositoryMetadata Metadata(string fullName) =>
        new("GitHub", fullName, fullName, 0, 0, null, null, null, "main", null, false, false);

    private static Source GitHubSource(string name)
    {
        var source = SourceTestFactory.Create(SourceType.GitHubApi, "https://api.github.com", name);
        source.Radar = RadarKind.Repozytorium;
        source.Configuration = SourceConfiguration.Create(null, new RepositoryQuery { Keywords = ["greenhouse"] });
        return source;
    }

    private static string SearchJson(params (int Id, string FullName, bool Fork)[] repos)
    {
        var items = string.Join(",", repos.Select(r =>
            $$"""{"id":{{r.Id}},"full_name":"{{r.FullName}}","html_url":"https://github.com/{{r.FullName}}","created_at":"2026-11-20T10:00:00Z","fork":{{(r.Fork ? "true" : "false")}}}"""));
        return $$"""{"total_count":{{repos.Length}},"items":[{{items}}]}""";
    }

    private static GitHubSourceAdapter GitHub(string search, string readme = LongReadme) =>
        new(new HttpClient(new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/search/repositories") return FakeHttpMessageHandler.Json(search);
            return path.EndsWith("/readme") ? Text(readme) : FakeHttpMessageHandler.Json("{}");
        })), new FixedTime(Now), null, (_, _) => Task.CompletedTask);

    private static GitLabSourceAdapter GitLab(string readme) =>
        new(new HttpClient(new FakeHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("/projects?"))
            {
                return FakeHttpMessageHandler.Json(url.Contains("id_after") || url.Contains("id_before")
                    ? "[]"
                    : """[{"id":77,"name_with_namespace":"Uni / Szklarnia","path_with_namespace":"uni/szklarnia","web_url":"https://gitlab.com/uni/szklarnia","created_at":"2026-11-21T08:00:00Z","default_branch":"main","readme_url":"https://gitlab.com/uni/szklarnia/-/blob/main/README.md"}]""");
            }

            if (url.Contains("/repository/files/")) return Text(readme);
            return url.EndsWith("/languages") ? FakeHttpMessageHandler.Json("{}") : FakeHttpMessageHandler.Json("""{"id":77}""");
        })), new FixedTime(Now), null, (_, _) => Task.CompletedTask);

    private static ScanService Service(SqliteTestDatabase database, ISourceAdapter adapter, RepositoryOptions? options = null) =>
        new(database.Context,
            new SingleAdapterResolver(adapter),
            Options.Create(new RadarOptions { Repositories = options ?? new RepositoryOptions() }),
            NullLogger<ScanService>.Instance);

    private static HttpResponseMessage Text(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "text/plain")
    };

    private sealed class SingleAdapterResolver(ISourceAdapter adapter) : ISourceAdapterResolver
    {
        public ISourceAdapter? Resolve(Source source) => adapter;
    }
}
