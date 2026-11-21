using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>
/// Łączy w jeden <see cref="InnovationProject"/> odkrycia, które są tym samym projektem
/// opublikowanym w kilku miejscach (np. GitHub i GitLab, kopia bez flagi fork).
/// Sygnały: identyczne znormalizowane README, link z README do repozytorium na innej platformie,
/// ta sama nazwa właściciel/repozytorium na innej platformie.
/// </summary>
public sealed partial class RepositoryDeduplicator(RadarDbContext db)
{
    public const double SameReadmeSimilarity = 1.0;
    public const double LinkedSimilarity = 1.0;
    public const double SameNameSimilarity = 0.8;

    /// <summary>Szuka duplikatów dla podanych (zapisanych) repozytoriów; zwraca liczbę nowych powiązań.</summary>
    public async Task<int> LinkDuplicatesAsync(IReadOnlyList<RepositoryInfo> repositories, CancellationToken cancellationToken)
    {
        var linked = 0;
        foreach (var repository in repositories.Where(x => x.NoiseReason is null && x.Id != 0))
        {
            var fullName = repository.FullName.ToLower();
            var hash = repository.ReadmeHash;
            var references = ReferencedRepositories(repository.Discovery.RawContent, repository.HomepageUrl)
                .Where(x => !x.Platform.Equals(repository.Platform, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.FullName)
                .Distinct()
                .ToList();

            var candidates = await db.Repositories
                .Include(x => x.Discovery)
                .Where(x => x.Id != repository.Id && x.NoiseReason == null && x.DiscoveryId != repository.DiscoveryId)
                .Where(x =>
                    (hash != null && x.ReadmeHash == hash) ||
                    (x.Platform != repository.Platform && x.FullName.ToLower() == fullName) ||
                    (x.Platform != repository.Platform && references.Contains(x.FullName.ToLower())))
                .ToListAsync(cancellationToken);

            foreach (var candidate in candidates)
            {
                var similarity = hash is not null && candidate.ReadmeHash == hash
                    ? SameReadmeSimilarity
                    : references.Contains(candidate.FullName.ToLower())
                        ? LinkedSimilarity
                        : SameNameSimilarity;
                if (await LinkAsync(repository.Discovery, candidate.Discovery, similarity, cancellationToken))
                {
                    linked++;
                }
            }
        }

        if (linked > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return linked;
    }

    /// <summary>Repozytoria GitHub/GitLab wskazane linkami w README lub homepage (platforma, właściciel/nazwa małymi literami).</summary>
    public static IReadOnlyList<(string Platform, string FullName)> ReferencedRepositories(string? readme, string? homepage)
    {
        var result = new List<(string, string)>();
        foreach (Match match in RepositoryLink().Matches($"{readme}\n{homepage}"))
        {
            var platform = match.Groups["host"].Value.StartsWith("github", StringComparison.OrdinalIgnoreCase) ? "GitHub" : "GitLab";
            var path = match.Groups["path"].Value.TrimEnd('/', '.', ')');
            if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                path = path[..^4];
            }

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            // GitHub: właściciel/nazwa; GitLab: grupy/…/nazwa (bez części po „/-/”).
            var name = platform == "GitHub"
                ? string.Join('/', segments.Take(2))
                : string.Join('/', segments.TakeWhile(x => x != "-"));
            if (name.Count(c => c == '/') >= 1)
            {
                result.Add((platform, name.ToLowerInvariant()));
            }
        }

        return result;
    }

    private async Task<bool> LinkAsync(Discovery first, Discovery second, double similarity, CancellationToken cancellationToken)
    {
        await LoadLinksAsync(first, cancellationToken);
        await LoadLinksAsync(second, cancellationToken);

        var project = first.ProjectLinks.Select(x => x.Project).FirstOrDefault()
                      ?? second.ProjectLinks.Select(x => x.Project).FirstOrDefault();
        if (project is not null &&
            first.ProjectLinks.Any(x => x.Project == project) &&
            second.ProjectLinks.Any(x => x.Project == project))
        {
            return false;
        }

        if (project is null)
        {
            var older = (first.PublishedAt ?? DateTime.MaxValue) <= (second.PublishedAt ?? DateTime.MaxValue) ? first : second;
            project = new InnovationProject
            {
                Title = older.Title,
                Description = older.Description,
                FirstDiscoveredAt = Min(first.DiscoveredAt, second.DiscoveredAt),
                EarliestKnownDate = MinNullable(first.PublishedAt, second.PublishedAt),
                LastActivityAt = MaxNullable(first.LastActivityAt, second.LastActivityAt),
                IsDemo = false
            };
            db.InnovationProjects.Add(project);
        }
        else
        {
            project.EarliestKnownDate = MinNullable(project.EarliestKnownDate, MinNullable(first.PublishedAt, second.PublishedAt));
            project.LastActivityAt = MaxNullable(project.LastActivityAt, MaxNullable(first.LastActivityAt, second.LastActivityAt));
        }

        var changed = false;
        foreach (var discovery in new[] { first, second })
        {
            if (discovery.ProjectLinks.All(x => x.Project != project))
            {
                discovery.ProjectLinks.Add(new DiscoveryProject
                {
                    Discovery = discovery,
                    Project = project,
                    Similarity = similarity,
                    CreatedAt = DateTime.UtcNow
                });
                changed = true;
            }
        }

        return changed;
    }

    private async Task LoadLinksAsync(Discovery discovery, CancellationToken cancellationToken)
    {
        var entry = db.Entry(discovery);
        if (!entry.Collection(x => x.ProjectLinks).IsLoaded)
        {
            await entry.Collection(x => x.ProjectLinks).Query().Include(x => x.Project).LoadAsync(cancellationToken);
            entry.Collection(x => x.ProjectLinks).IsLoaded = true;
        }
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static DateTime? MinNullable(DateTime? a, DateTime? b) =>
        a is null ? b : b is null ? a : a < b ? a : b;

    private static DateTime? MaxNullable(DateTime? a, DateTime? b) =>
        a is null ? b : b is null ? a : a > b ? a : b;

    [GeneratedRegex(@"https?://(?<host>github\.com|gitlab\.com)/(?<path>[A-Za-z0-9_.\-/]+)", RegexOptions.IgnoreCase)]
    private static partial Regex RepositoryLink();
}
