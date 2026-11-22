using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>
/// Cykliczne grupowanie projektów według lokalnych embeddingów. Nowe kategorie są
/// wyłącznie proponowane – powstają dopiero po akceptacji w interfejsie.
/// </summary>
public sealed class CategorizationService(
    RadarDbContext db,
    IEmbeddingService embedder,
    IOptions<RadarOptions> options,
    ILogger<CategorizationService> logger) : ICategorizationService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions ListJsonOptions = new() { WriteIndented = false };
    private const string EmbeddingVersion = "v1";

    public async Task<CategorizationResult> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Categories.Enabled)
        {
            return new CategorizationResult(0, 0, 0, 0, 0, embedder.ProviderName,
                "Grupowanie kategorii jest wyłączone w konfiguracji.");
        }

        await Gate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var createdProjects = await CreateProjectsForOrphanDiscoveriesAsync(now, cancellationToken);
            var projects = await LoadProjectsAsync(cancellationToken);
            if (projects.Count == 0)
            {
                return new CategorizationResult(0, 0, 0, 0, 0, embedder.ProviderName,
                    "Brak projektów do grupowania. Dodaj odkrycia przez skanowanie źródeł.");
            }

            var vectors = await UpdateEmbeddingsAsync(projects, now, cancellationToken);
            foreach (var project in projects)
            {
                project.FreshnessScore = FreshnessCalculator.Calculate(project, now);
            }

            var categories = await db.Categories.Where(x => x.IsActive && !x.IsDemo).ToListAsync(cancellationToken);
            var linksAdded = await AssignToExistingCategoriesAsync(projects, vectors, categories, now, cancellationToken);
            var clusters = BuildClusters(projects, vectors);
            var proposalsCreated = await CreateProposalsAsync(clusters, projects, vectors, categories, now, cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            var message = $"Przeanalizowano {projects.Count} projektów, utworzono {createdProjects} nowych projektów, " +
                          $"znaleziono {clusters.Count} grup, dodano {linksAdded} przypisań " +
                          $"i {proposalsCreated} propozycji kategorii.";
            logger.LogInformation("Grupowanie kategorii: {Message}", message);
            return new CategorizationResult(
                projects.Count,
                vectors.Count,
                clusters.Count,
                linksAdded,
                proposalsCreated,
                embedder.ProviderName,
                message);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<IReadOnlyList<CategoryProposalModel>> GetProposalsAsync(CancellationToken cancellationToken = default)
    {
        var proposals = await db.CategoryProposals
            .Include(x => x.ProjectLinks).ThenInclude(x => x.Project)
            .OrderBy(x => x.Status == CategoryProposalStatus.Pending ? 0 : 1)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return proposals.Select(x => new CategoryProposalModel
        {
            Id = x.Id,
            Name = x.Name,
            Description = x.Description,
            Keywords = ParseList(x.Keywords),
            Projects = x.ProjectLinks.Select(link => link.Project.Title).Distinct().ToList(),
            Similarity = x.Similarity,
            Status = x.Status,
            CreatedAt = x.CreatedAt,
            ResolvedAt = x.ResolvedAt
        }).ToList();
    }

    public async Task<IReadOnlyList<ProjectCluster>> GetClustersAsync(CancellationToken cancellationToken = default)
    {
        var embeddings = await db.ProjectEmbeddings
            .Where(x => x.ClusterId != null)
            .Include(x => x.Project).ThenInclude(x => x.CategoryLinks).ThenInclude(x => x.Category)
            .Include(x => x.Project).ThenInclude(x => x.DiscoveryLinks)
            .OrderBy(x => x.ClusterId)
            .Take(200)
            .ToListAsync(cancellationToken);

        return embeddings
            .GroupBy(x => x.ClusterId!.Value)
            .Select(group => new ProjectCluster
            {
                Id = group.Key,
                ProjectCount = group.Count(),
                AverageSimilarity = Math.Round(group.Average(x => x.ClusterSimilarity), 3),
                Projects = group.Select(x => new ClusterProjectItem
                {
                    Id = x.InnovationProjectId,
                    DiscoveryId = x.Project.DiscoveryLinks.FirstOrDefault()?.DiscoveryId,
                    Title = x.Project.Title,
                    FreshnessScore = x.Project.FreshnessScore,
                    Categories = x.Project.CategoryLinks
                        .Select(link => link.Category.Name)
                        .Distinct()
                        .OrderBy(name => name)
                        .ToList()
                }).ToList()
            })
            .ToList();
    }

    public async Task<IReadOnlyList<CategoryHistoryItem>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.CategoryHistories
            .OrderByDescending(x => x.ChangedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return rows.Select(x => new CategoryHistoryItem
        {
            Id = x.Id,
            CategoryName = x.CategoryName,
            Change = x.Change,
            Details = x.Details,
            Actor = x.Actor,
            ChangedAt = x.ChangedAt
        }).ToList();
    }

    public async Task<bool> AcceptProposalAsync(int proposalId, CancellationToken cancellationToken = default)
    {
        var proposal = await db.CategoryProposals
            .Include(x => x.ProjectLinks).ThenInclude(x => x.Project).ThenInclude(x => x.CategoryLinks)
            .FirstOrDefaultAsync(x => x.Id == proposalId, cancellationToken);
        if (proposal is null || proposal.Status != CategoryProposalStatus.Pending)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var normalizedName = TextFeatures.Normalize(proposal.Name).Trim();
        var categories = await db.Categories.Where(x => !x.IsDemo).ToListAsync(cancellationToken);
        var category = categories.FirstOrDefault(x =>
            x.IsActive && TextFeatures.Normalize(x.Name).Trim() == normalizedName);
        if (category is null)
        {
            category = new Category
            {
                Name = proposal.Name,
                Description = proposal.Description,
                CreatedAt = now,
                LastUpdatedAt = now,
                IsActive = true,
                IsDemo = false
            };
            db.Categories.Add(category);
            AddHistory(category, category.Name, "Utworzenie kategorii",
                "Zaakceptowano propozycję wykrytą na podstawie podobieństwa projektów.", "Użytkownik");
        }

        foreach (var link in proposal.ProjectLinks)
        {
            var project = link.Project;
            if (project.CategoryLinks.Any(x => x.Category == category))
            {
                continue;
            }

            project.CategoryLinks.Add(new ProjectCategory
            {
                Category = category,
                Confidence = Math.Clamp(proposal.Similarity, 0, 1),
                CreatedAt = now
            });
            AddHistory(category, category.Name, "Przypisanie projektu",
                $"Projekt „{project.Title}” został przypisany do kategorii po akceptacji propozycji.", "Użytkownik");
        }

        proposal.Status = CategoryProposalStatus.Accepted;
        proposal.ResolvedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RejectProposalAsync(int proposalId, CancellationToken cancellationToken = default)
    {
        var proposal = await db.CategoryProposals
            .FirstOrDefaultAsync(x => x.Id == proposalId, cancellationToken);
        if (proposal is null || proposal.Status != CategoryProposalStatus.Pending)
        {
            return false;
        }

        proposal.Status = CategoryProposalStatus.Rejected;
        proposal.ResolvedAt = DateTime.UtcNow;
        AddHistory(null, proposal.Name, "Odrzucenie propozycji",
            "Użytkownik odrzucił automatyczną propozycję nowej kategorii.", "Użytkownik");
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<int> CreateProjectsForOrphanDiscoveriesAsync(DateTime now, CancellationToken cancellationToken)
    {
        var maxItems = Math.Clamp(options.Value.Categories.MaxProjectsPerRun, 1, 2000);
        var orphans = await db.Discoveries
            .Include(x => x.Repository)
            .Where(x => !x.IsDemo && !x.ProjectLinks.Any() &&
                        (x.Repository == null || x.Repository.NoiseReason == null))
            .OrderBy(x => x.DiscoveredAt)
            .Take(maxItems)
            .ToListAsync(cancellationToken);

        foreach (var discovery in orphans)
        {
            var project = new InnovationProject
            {
                Title = Trim(discovery.Title, ContentLimits.MaxTitleLength),
                Description = Trim(discovery.Description, ContentLimits.MaxDescriptionLength),
                FirstDiscoveredAt = discovery.DiscoveredAt,
                EarliestKnownDate = discovery.EarliestKnownDate ?? discovery.PublishedAt,
                LastActivityAt = discovery.LastActivityAt ?? discovery.PublishedAt,
                CreatedAt = now,
                IsDemo = false
            };
            project.FreshnessScore = FreshnessCalculator.Calculate(project, now);
            db.InnovationProjects.Add(project);
            discovery.ProjectLinks.Add(new DiscoveryProject
            {
                Discovery = discovery,
                Project = project,
                Similarity = 1,
                CreatedAt = now
            });
        }

        if (orphans.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return orphans.Count;
    }

    private async Task<List<InnovationProject>> LoadProjectsAsync(CancellationToken cancellationToken)
    {
        var maxItems = Math.Clamp(options.Value.Categories.MaxProjectsPerRun, 1, 2000);
        return await db.InnovationProjects
            .Where(x => !x.IsDemo)
            .Include(x => x.CategoryLinks)
            .Include(x => x.DiscoveryLinks).ThenInclude(x => x.Discovery).ThenInclude(x => x.AiAnalyses)
            .OrderBy(x => x.Id)
            .Take(maxItems)
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<int, float[]>> UpdateEmbeddingsAsync(
        IReadOnlyList<InnovationProject> projects,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var ids = projects.Select(x => x.Id).ToHashSet();
        var existing = await db.ProjectEmbeddings
            .Where(x => ids.Contains(x.InnovationProjectId))
            .ToListAsync(cancellationToken);
        var byProject = existing.ToDictionary(x => x.InnovationProjectId);
        var vectors = new Dictionary<int, float[]>();

        foreach (var project in projects)
        {
            var text = ProjectText(project);
            var vector = await embedder.EmbedAsync(text, cancellationToken);
            vectors[project.Id] = vector;
            if (byProject.TryGetValue(project.Id, out var row))
            {
                row.Vector = Serialize(vector);
                row.Model = embedder.ProviderName;
                row.GeneratedAt = now;
                row.ClusterId = null;
                row.ClusterSimilarity = 0;
            }
            else
            {
                db.ProjectEmbeddings.Add(new ProjectEmbedding
                {
                    InnovationProjectId = project.Id,
                    Vector = Serialize(vector),
                    Model = embedder.ProviderName,
                    GeneratedAt = now
                });
            }
        }

        return vectors;
    }

    private async Task<int> AssignToExistingCategoriesAsync(
        IReadOnlyList<InnovationProject> projects,
        IReadOnlyDictionary<int, float[]> projectVectors,
        IReadOnlyList<Category> categories,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (categories.Count == 0)
        {
            return 0;
        }

        var categoryVectors = new List<(Category Category, float[] Vector)>();
        foreach (var category in categories)
        {
            categoryVectors.Add((category,
                await embedder.EmbedAsync($"{category.Name}. {category.Description}", cancellationToken)));
        }

        var threshold = options.Value.Categories.ExistingCategorySimilarity;
        var limit = Math.Clamp(options.Value.Categories.MaxCategoriesPerProject, 0, 5);
        var added = 0;
        foreach (var project in projects)
        {
            var candidates = categoryVectors
                .Select(x => (x.Category, Similarity: EmbeddingMath.CosineSimilarity(
                    projectVectors[project.Id], x.Vector)))
                .Where(x => x.Similarity >= threshold && x.Similarity > 0)
                .OrderByDescending(x => x.Similarity)
                .Take(limit);

            foreach (var candidate in candidates)
            {
                if (project.CategoryLinks.Any(x => x.CategoryId == candidate.Category.Id))
                {
                    continue;
                }

                project.CategoryLinks.Add(new ProjectCategory
                {
                    Category = candidate.Category,
                    Confidence = Math.Round(candidate.Similarity, 3),
                    CreatedAt = now
                });
                AddHistory(candidate.Category, candidate.Category.Name, "Przypisanie automatyczne",
                    $"Projekt „{project.Title}” – podobieństwo {candidate.Similarity:P0}.", "System");
                added++;
            }
        }

        return added;
    }

    private List<Cluster> BuildClusters(IReadOnlyList<InnovationProject> projects, Dictionary<int, float[]> vectors)
    {
        var parent = Enumerable.Range(0, projects.Count).ToArray();
        var threshold = options.Value.Categories.MinSimilarity;

        int Find(int index)
        {
            while (parent[index] != index)
            {
                parent[index] = parent[parent[index]];
                index = parent[index];
            }

            return index;
        }

        void Union(int first, int second)
        {
            var firstRoot = Find(first);
            var secondRoot = Find(second);
            if (firstRoot != secondRoot)
            {
                parent[secondRoot] = firstRoot;
            }
        }

        for (var i = 0; i < projects.Count; i++)
        {
            for (var j = i + 1; j < projects.Count; j++)
            {
                if (EmbeddingMath.CosineSimilarity(vectors[projects[i].Id], vectors[projects[j].Id]) >= threshold)
                {
                    Union(i, j);
                }
            }
        }

        var minSize = Math.Max(2, options.Value.Categories.MinClusterSize);
        var clusters = new List<Cluster>();
        foreach (var group in Enumerable.Range(0, projects.Count).GroupBy(Find))
        {
            var members = group.ToList();
            if (members.Count < minSize)
            {
                continue;
            }

            var centroid = EmbeddingMath.Average(members.Select(x => vectors[projects[x].Id]).ToList());
            var stable = members
                .Where(x => EmbeddingMath.CosineSimilarity(vectors[projects[x].Id], centroid) >= threshold)
                .ToList();
            if (stable.Count < minSize)
            {
                continue;
            }

            clusters.Add(new Cluster(
                stable.Select(x => projects[x]).ToList(),
                stable.Average(x => EmbeddingMath.CosineSimilarity(vectors[projects[x].Id], centroid)),
                centroid));
        }

        return clusters;
    }

    private async Task<int> CreateProposalsAsync(
        IReadOnlyList<Cluster> clusters,
        IReadOnlyList<InnovationProject> projects,
        IReadOnlyDictionary<int, float[]> projectVectors,
        IReadOnlyList<Category> categories,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (clusters.Count == 0)
        {
            return 0;
        }

        var existingProposals = await db.CategoryProposals.ToListAsync(cancellationToken);
        var categoryVectors = new List<float[]>();
        foreach (var category in categories)
        {
            categoryVectors.Add(await embedder.EmbedAsync($"{category.Name}. {category.Description}", cancellationToken));
        }

        var threshold = options.Value.Categories.ExistingCategorySimilarity;
        var created = 0;
        var clusterNumber = 1;
        foreach (var cluster in clusters)
        {
            var similarity = Math.Round(Math.Clamp(cluster.Similarity, 0, 1), 3);
            foreach (var project in cluster.Projects)
            {
                var embedding = db.ProjectEmbeddings.Local.FirstOrDefault(x => x.InnovationProjectId == project.Id);
                if (embedding is not null)
                {
                    embedding.ClusterId = clusterNumber;
                    embedding.ClusterSimilarity = similarity;
                }
            }

            var isCovered = categoryVectors.Any(vector =>
                EmbeddingMath.CosineSimilarity(cluster.Centroid, vector) >= threshold);
            clusterNumber++;
            if (isCovered)
            {
                continue;
            }

            var keywords = ExtractKeywords(cluster.Projects, projects);
            if (keywords.Count == 0)
            {
                continue;
            }

            var signature = string.Join("|", keywords.Select(TextFeatures.Normalize).OrderBy(x => x));
            if (existingProposals.Any(x => x.Signature == signature))
            {
                continue;
            }

            var name = string.Join(" ", keywords.Take(3));
            name = char.ToUpperInvariant(name[0]) + name[1..];
            var normalizedName = TextFeatures.Normalize(name).Trim();
            if (categories.Any(x => x.IsActive && TextFeatures.Normalize(x.Name).Trim() == normalizedName))
            {
                continue;
            }

            var description = $"Propozycja wykryta na podstawie {cluster.Projects.Count} podobnych projektów. " +
                              "Wymaga weryfikacji i akceptacji użytkownika.";
            var proposal = new CategoryProposal
            {
                Name = Trim(name, ContentLimits.MaxTitleLength),
                Description = description,
                Keywords = JsonSerializer.Serialize(keywords, ListJsonOptions),
                Signature = signature,
                Similarity = similarity,
                Status = CategoryProposalStatus.Pending,
                CreatedAt = now
            };
            foreach (var project in cluster.Projects)
            {
                proposal.ProjectLinks.Add(new CategoryProposalProject
                {
                    Project = project,
                    Similarity = Math.Round(
                        EmbeddingMath.CosineSimilarity(
                            projectVectors[project.Id],
                            cluster.Centroid),
                        3)
                });
            }

            db.CategoryProposals.Add(proposal);
            existingProposals.Add(proposal);
            AddHistory(null, proposal.Name, "Nowa propozycja kategorii",
                $"Grupa {clusterNumber - 1}: {cluster.Projects.Count} podobnych projektów. " +
                "Kategoria nie została jeszcze utworzona.", "System");
            created++;
        }

        return created;
    }

    private static List<string> ExtractKeywords(
        IReadOnlyList<InnovationProject> clusterProjects,
        IReadOnlyList<InnovationProject> allProjects)
    {
        // Preferujemy słowa z opisów i treści materiałów. Jeśli materiały nie mają opisu
        // (np. część repozytoriów), używamy jako słabego sygnału także tytułów.
        var keywords = ExtractKeywordsFrom(clusterProjects, allProjects, KeywordTokens);
        return keywords.Count > 0
            ? keywords
            : ExtractKeywordsFrom(clusterProjects, allProjects, ProjectTokens);
    }

    private static List<string> ExtractKeywordsFrom(
        IReadOnlyList<InnovationProject> clusterProjects,
        IReadOnlyList<InnovationProject> allProjects,
        Func<InnovationProject, IEnumerable<string>> tokenSelector)
    {
        // Słowa obecne w dużej części całej bazy (np. „project”, „repository”) nie mogą
        // nadawać nazwy nowej kategorii – są zbyt ogólne, by opisywać temat grupy.
        var documentFrequency = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var project in allProjects)
        {
            foreach (var key in tokenSelector(project).Select(TextFeatures.Normalize).Distinct(StringComparer.Ordinal))
            {
                documentFrequency.TryGetValue(key, out var count);
                documentFrequency[key] = count + 1;
            }
        }

        var maxDocumentFrequency = Math.Max(2, (int)Math.Ceiling(allProjects.Count * 0.35));
        var counts = new Dictionary<string, (string Display, int Count)>(StringComparer.Ordinal);
        foreach (var project in clusterProjects)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in tokenSelector(project))
            {
                var key = TextFeatures.Normalize(token);
                if (!seen.Add(key))
                {
                    continue;
                }

                counts.TryGetValue(key, out var current);
                counts[key] = (current.Display ?? token, current.Count + 1);
            }
        }

        var minCount = Math.Max(2, (int)Math.Ceiling(clusterProjects.Count * 0.6));
        return counts
            .Where(x => x.Value.Count >= minCount &&
                        (!documentFrequency.TryGetValue(x.Key, out var frequency) || frequency <= maxDocumentFrequency))
            .OrderByDescending(x => x.Value.Count)
            .ThenByDescending(x => x.Key.Length)
            .Take(3)
            .Select(x => x.Value.Display)
            .ToList();
    }

    private static IEnumerable<string> ProjectTokens(InnovationProject project) =>
        TextFeatures.Tokenize(ProjectText(project)).Where(x => x.Length >= 4);

    /// <summary>
    /// Słowa do nazwy kategorii: z opisów i treści materiałów, bez nazw właściciel/repozytorium
    /// i bez tokenów z cyframi (zwykle identyfikatory, nie tematy).
    /// </summary>
    private static IEnumerable<string> KeywordTokens(InnovationProject project)
    {
        var text = new StringBuilder().AppendLine(project.Description);
        foreach (var link in project.DiscoveryLinks.Take(12))
        {
            text.AppendLine(link.Discovery.Description);
            var content = link.Discovery.RawContent;
            if (!string.IsNullOrWhiteSpace(content))
            {
                text.AppendLine(content.Length <= 2500 ? content : content[..2500]);
            }
        }

        return TextFeatures.Tokenize(text.ToString())
            .Where(x => x.Length >= 4 && !x.Any(char.IsDigit));
    }

    private static string ProjectText(InnovationProject project)
    {
        var text = new StringBuilder()
            .AppendLine(project.Title)
            .AppendLine(project.Description);

        foreach (var link in project.DiscoveryLinks.Take(12))
        {
            var discovery = link.Discovery;
            text.AppendLine(discovery.Title).AppendLine(discovery.Description);
            // RawContent zawiera np. README repozytorium; bez niego grupowanie repozytoriów
            // opierałoby się wyłącznie na nazwach właściciel/repozytorium.
            if (!string.IsNullOrWhiteSpace(discovery.RawContent))
            {
                var content = discovery.RawContent;
                text.AppendLine(content.Length <= 2500 ? content : content[..2500]);
            }

            foreach (var suggestion in SuggestedCategories(discovery).Take(5))
            {
                text.Append("proponowana kategoria AI: ").AppendLine(suggestion);
            }
        }

        return text.ToString();
    }

    private static IReadOnlyList<string> SuggestedCategories(Discovery discovery)
    {
        var record = discovery.AiAnalyses
            .Where(x => x.IsSuccess)
            .OrderByDescending(x => x.AnalyzedAt)
            .FirstOrDefault();
        if (record?.ResultJson is null)
        {
            return Array.Empty<string>();
        }

        try
        {
            using var document = JsonDocument.Parse(record.ResultJson);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!property.Name.Equals("suggestedCategories", StringComparison.OrdinalIgnoreCase) ||
                    property.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                return property.Value.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(x => x.Length > 0)
                    .ToList();
            }
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }

        return Array.Empty<string>();
    }

    private void AddHistory(Category? category, string name, string change, string details, string actor = "System")
    {
        db.CategoryHistories.Add(new CategoryHistory
        {
            CategoryId = category?.Id > 0 ? category.Id : null,
            CategoryName = name,
            Change = change,
            Details = Trim(details, 500),
            Actor = actor,
            ChangedAt = DateTime.UtcNow
        });
    }

    private static IReadOnlyList<string> ParseList(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static string Serialize(float[] vector) => JsonSerializer.Serialize(vector, ListJsonOptions);

    private static string Trim(string value, int maxLength) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= maxLength ? value : value[..maxLength];

    private sealed record Cluster(IReadOnlyList<InnovationProject> Projects, double Similarity, float[] Centroid);
}
