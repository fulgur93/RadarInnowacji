using System.Net;
using System.Text;
using System.Text.Json;
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

/// <summary>Metadane, README, języki i link do demo (Etap 3, krok 3).</summary>
public class RepositoryDetailsTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FixedTime(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc);
    }

    private const string GitHubSearch = """
        {"total_count":1,"items":[{"id":11,"full_name":"lab/robo-arm","description":"Prototyp ramienia",
        "html_url":"https://github.com/lab/robo-arm","created_at":"2026-11-20T10:00:00Z","pushed_at":"2026-11-21T10:00:00Z",
        "owner":{"login":"lab"},"language":"Python","topics":["robotics"],"stargazers_count":3,"forks_count":1,
        "open_issues_count":2,"license":{"spdx_id":"MIT","name":"MIT License"},"homepage":"",
        "default_branch":"main","fork":false,"archived":false}]}
        """;

    [Fact]
    public void GitHub_ParsujeMetadaneZListyWynikow()
    {
        var source = SourceTestFactory.Create(SourceType.GitHubApi, "https://api.github.com/search/repositories?q=x");
        var adapter = new GitHubSourceAdapter(new HttpClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(GitHubSearch))));

        var item = Assert.Single(adapter.FetchAsync(source).Result);

        var repo = Assert.IsType<RepositoryMetadata>(item.Repository);
        Assert.Equal(("GitHub", "lab/robo-arm", 3, 1, 2, "MIT", "main"),
            (repo.Platform, repo.ProjectId, repo.Stars, repo.Forks, repo.OpenIssues, repo.License, repo.DefaultBranch));
        Assert.False(repo.IsFork);
    }

    [Fact]
    public void GitHub_NierozpoznanaLicencjaUzywaNazwy()
    {
        var json = GitHubSearch.Replace("\"spdx_id\":\"MIT\",\"name\":\"MIT License\"", "\"spdx_id\":\"NOASSERTION\",\"name\":\"Other\"");
        var source = SourceTestFactory.Create(SourceType.GitHubApi, "https://api.github.com/search/repositories?q=x");
        var adapter = new GitHubSourceAdapter(new HttpClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(json))));

        Assert.Equal("Other", Assert.Single(adapter.FetchAsync(source).Result).Repository!.License);
    }

    [Fact]
    public void GitLab_ParsujeMetadaneForkIReadme()
    {
        var source = SourceTestFactory.Create(SourceType.GitLabApi, "https://gitlab.com/api/v4/projects");
        var adapter = new GitLabSourceAdapter(new HttpClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json("""
            [{"id":42,"name":"drone","path_with_namespace":"uni/lab/drone","star_count":5,"forks_count":0,
              "default_branch":"release/1.0","readme_url":"https://gitlab.com/uni/lab/drone/-/blob/release/1.0/docs/README.md",
              "forked_from_project":{"id":7},"archived":true}]
            """))));

        var repo = Assert.Single(adapter.FetchAsync(source).Result).Repository!;

        Assert.Equal(("GitLab", "42", "uni/lab/drone", 5), (repo.Platform, repo.ProjectId, repo.FullName, repo.Stars));
        Assert.Equal("docs/README.md", repo.ReadmePath);
        Assert.True(repo.IsFork);
        Assert.True(repo.IsArchived);
    }

    [Theory]
    [InlineData("https://gitlab.com/g/p/-/blob/main/README.md", "main", "README.md")]
    [InlineData("https://gitlab.com/g/p/-/blob/feature/x/docs/README.md", "feature/x", "docs/README.md")]
    [InlineData("https://gitlab.com/g/p/-/blob/main/READ%20ME.md", null, "READ ME.md")]
    [InlineData("https://gitlab.com/g/p", "main", null)]
    [InlineData(null, "main", null)]
    public void GitLab_ReadmePathZReadmeUrl(string? url, string? branch, string? expected) =>
        Assert.Equal(expected, GitLabSourceAdapter.ReadmePath(url, branch));

    [Theory]
    [InlineData("https://robo.example.org", null, "https://robo.example.org")]
    [InlineData("https://github.com/lab/robo-arm", "brak linków", null)]
    [InlineData(null, "![build](https://img.shields.io/badge/x.svg) [Live](https://lab.github.io/robo/)", "https://lab.github.io/robo/")]
    [InlineData(null, "Zobacz [demo aplikacji](https://example.org/app) oraz [dokumentację](https://example.org/docs)", "https://example.org/app")]
    [InlineData(null, "Model: https://huggingface.co/spaces/lab/robo działa online.", "https://huggingface.co/spaces/lab/robo")]
    [InlineData(null, "[Paper](https://arxiv.org/abs/2611.00001) [PyPI](https://pypi.org/project/robo)", null)]
    [InlineData("https://doi.org/10.5281/zenodo.1", null, null)]
    [InlineData(null, "Wymaga [arduino-cli](https://arduino.github.io/arduino-cli/)", null)]
    public void DemoLinkDetector_ZnajdujeDemoWHomepageLubReadme(string? homepage, string? readme, string? expected) =>
        Assert.Equal(expected, DemoLinkDetector.Find(homepage, readme, "lab"));

    [Fact]
    public async Task GitHub_SzczegolyPobierajaSuroweReadmeIUdzialJezykow()
    {
        string? readmeAccept = null;
        var adapter = new GitHubSourceAdapter(new HttpClient(new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/repos/lab/robo-arm/readme")
            {
                readmeAccept = request.Headers.Accept.ToString();
                return Text("# Robo arm\nNowa metoda chwytania.");
            }

            return path == "/repos/lab/robo-arm/languages"
                ? FakeHttpMessageHandler.Json("""{"Python":750,"C++":250}""")
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        })));
        var repo = new RepositoryInfo { Platform = "GitHub", ProjectId = "lab/robo-arm", FullName = "lab/robo-arm" };

        var batch = await adapter.FetchDetailsAsync(SourceTestFactory.Create(SourceType.GitHubApi, "https://api.github.com"), [repo]);

        var details = Assert.Single(batch.Results).Details!;
        Assert.Equal("application/vnd.github.raw+json", readmeAccept);
        Assert.StartsWith("# Robo arm", details.Readme);
        Assert.Equal(75, details.Languages["Python"]);
        Assert.Equal(25, details.Languages["C++"]);
        Assert.Null(batch.StopReason);
    }

    [Fact]
    public async Task GitHub_BrakReadmeTo404BezBledu()
    {
        var adapter = new GitHubSourceAdapter(new HttpClient(new FakeHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/languages")
                ? FakeHttpMessageHandler.Json("{}")
                : new HttpResponseMessage(HttpStatusCode.NotFound))));
        var repo = new RepositoryInfo { Platform = "GitHub", ProjectId = "a/b", FullName = "a/b" };

        var result = Assert.Single((await adapter.FetchDetailsAsync(
            SourceTestFactory.Create(SourceType.GitHubApi, "https://api.github.com"), [repo])).Results);

        Assert.Null(result.Error);
        Assert.Null(result.Details!.Readme);
    }

    [Fact]
    public async Task GitLab_SzczegolyPobierajaLicencjeReadmeIJezyki()
    {
        var requested = new List<string>();
        var adapter = new GitLabSourceAdapter(new HttpClient(new FakeHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            requested.Add(url);
            if (url.EndsWith("/projects/42?license=true"))
            {
                return FakeHttpMessageHandler.Json("""
                    {"id":42,"default_branch":"main","readme_url":"https://gitlab.com/uni/drone/-/blob/main/docs/README.md",
                     "license":{"key":"apache-2.0","name":"Apache License 2.0","nickname":null}}
                    """);
            }

            if (url.Contains("/repository/files/"))
            {
                return Text("Autonomiczny dron.");
            }

            return url.EndsWith("/languages")
                ? FakeHttpMessageHandler.Json("""{"C":60.25,"Python":39.75}""")
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        })));
        var repo = new RepositoryInfo { Platform = "GitLab", ProjectId = "42", FullName = "uni/drone" };

        var details = Assert.Single((await adapter.FetchDetailsAsync(
            SourceTestFactory.Create(SourceType.GitLabApi, "https://gitlab.com"), [repo])).Results).Details!;

        Assert.Equal("Apache License 2.0", details.License);
        Assert.Equal("Autonomiczny dron.", details.Readme);
        Assert.Equal(60.3, details.Languages["C"]);
        Assert.Contains("https://gitlab.com/api/v4/projects/42/repository/files/docs%2FREADME.md/raw?ref=main", requested);
    }

    [Fact]
    public async Task ScanService_ZapisujeMetadaneReadmeIJezykiINieNadpisujeReadmePrzyPonownymSkanie()
    {
        using var database = new SqliteTestDatabase();
        var source = RepoSource();
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();
        var readmeRequests = 0;
        var service = CreateService(database, GitHubAdapter(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/search/repositories") return FakeHttpMessageHandler.Json(GitHubSearch);
            if (path.EndsWith("/readme"))
            {
                readmeRequests++;
                return Text("# Robo arm\n[Demo](https://lab.github.io/robo-arm/)");
            }

            return FakeHttpMessageHandler.Json("""{"Python":900,"Shell":60,"Makefile":40}""");
        }));

        var first = await service.ScanSourceAsync(source.Id);
        var second = await service.ScanSourceAsync(source.Id);

        Assert.Equal(ScanStatus.Succeeded, first.Status);
        Assert.Equal(ScanStatus.Succeeded, second.Status);
        Assert.Equal(1, readmeRequests);
        var discovery = await database.Context.Discoveries.Include(x => x.Repository).SingleAsync();
        var repo = discovery.Repository!;
        Assert.Equal((3, "MIT", true), (repo.Stars, repo.License, repo.HasReadme));
        Assert.Equal("https://lab.github.io/robo-arm/", repo.DemoUrl);
        Assert.NotNull(repo.DetailsFetchedAt);
        Assert.StartsWith("Prototyp ramienia\n\n# Robo arm", discovery.RawContent);
        var technologies = JsonSerializer.Deserialize<List<string>>(discovery.Technologies)!;
        Assert.Contains("Shell", technologies);       // 6% ≥ próg 5%
        Assert.DoesNotContain("Makefile", technologies); // 4% < próg
    }

    [Fact]
    public async Task ScanService_LimitApiPodczasSzczegolowNiePsujeSkanuIZostawiaResztePoZniej()
    {
        using var database = new SqliteTestDatabase();
        var source = RepoSource();
        database.Context.Sources.Add(source);
        await database.Context.SaveChangesAsync();
        var items = string.Join(",", Enumerable.Range(1, 3).Select(i =>
            $$"""{"id":{{i}},"full_name":"lab/p{{i}}","html_url":"https://github.com/lab/p{{i}}","created_at":"2026-11-2{{i}}T00:00:00Z"}"""));
        var service = CreateService(database, GitHubAdapter(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/search/repositories") return FakeHttpMessageHandler.Json($$"""{"total_count":3,"items":[{{items}}]}""");
            if (path.StartsWith("/repos/lab/p3/")) return FakeHttpMessageHandler.Json("{}");
            var limited = new HttpResponseMessage(HttpStatusCode.Forbidden);
            limited.Headers.Add("x-ratelimit-remaining", "0");
            limited.Headers.Add("x-ratelimit-reset", new DateTimeOffset(Now.AddMinutes(30)).ToUnixTimeSeconds().ToString());
            return limited;
        }));

        var result = await service.ScanSourceAsync(source.Id);

        Assert.Equal(ScanStatus.Succeeded, result.Status);
        Assert.Equal(3, result.ItemsNew);
        Assert.Contains("README pobrano dla 1 z 3 projektów", result.ErrorMessage);
        Assert.Null(database.Context.Sources.Single().LastError);
        var fetched = database.Context.Repositories.Where(x => x.DetailsFetchedAt != null).Select(x => x.FullName).ToList();
        Assert.Equal(["lab/p3"], fetched); // najnowszy projekt pierwszy
    }

    [Fact]
    public void NeedsDetailsRefresh_PoTygodniuAktywnosciOdPobraniaReadme()
    {
        var info = new RepositoryInfo { DetailsFetchedAt = Now };

        Assert.False(RepositoryContent.NeedsDetailsRefresh(info, Now.AddDays(3)));
        Assert.True(RepositoryContent.NeedsDetailsRefresh(info, Now.AddDays(8)));
        Assert.False(RepositoryContent.NeedsDetailsRefresh(new RepositoryInfo(), Now.AddDays(8)));
    }

    [Fact]
    public void ApplyDetails_SkracaDlugieReadmeIOznaczaSkrocenie()
    {
        var discovery = new Discovery { Description = "Opis", Technologies = "[]" };
        var info = new RepositoryInfo { Discovery = discovery };

        RepositoryContent.ApplyDetails(info, new RepositoryDetails(new string('x', 30_000),
            new Dictionary<string, double>(), "GPL-3.0", null), null, Now);

        Assert.Equal(ContentLimits.MaxRawContentLength, discovery.RawContent.Length);
        Assert.True(discovery.IsContentTruncated);
        Assert.Equal(30_006, discovery.ContentLength);
        Assert.Equal("GPL-3.0", info.License);
    }

    private static Source RepoSource()
    {
        var source = SourceTestFactory.Create(SourceType.GitHubApi, "https://api.github.com");
        source.Radar = RadarKind.Repozytorium;
        source.Configuration = SourceConfiguration.Create(null, new RepositoryQuery { Keywords = ["prototyp"], CreatedWithinDays = 7 });
        return source;
    }

    private static GitHubSourceAdapter GitHubAdapter(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new HttpClient(new FakeHttpMessageHandler(responder)), new FixedTime(Now), null, (_, _) => Task.CompletedTask);

    private static ScanService CreateService(SqliteTestDatabase database, ISourceAdapter adapter) =>
        new(database.Context,
            new SingleAdapterResolver(adapter),
            Options.Create(new RadarOptions()),
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
