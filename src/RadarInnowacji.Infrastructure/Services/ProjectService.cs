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
/// Wspólny radar projektów. Dane z różnych źródeł pozostają jako osobne odkrycia,
/// a propozycje połączeń niejednoznacznych przypadków wymagają akceptacji użytkownika.
/// </summary>
public sealed class ProjectService(
    RadarDbContext db,
    IEmbeddingService embedder,
    IOptions<RadarOptions> options,
    ILogger<ProjectService> logger) : IProjectService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions VectorJsonOptions = new() { WriteIndented = false };

    public async Task<ProjectPage> GetPageAsync(ProjectFilter filter, CancellationToken cancellationToken = default)
    {
        var query = db.InnovationProjects
            .Include(x => x.DiscoveryLinks).ThenInclude(x => x.Discovery).ThenInclude(x => x.Source)
            .Include(x => x.CategoryLinks).ThenInclude(x => x.Category)
            .AsQueryable();

        if (!filter.IncludeMerged)
        {
            query = query.Where(x => x.MergedIntoProjectId == null);
        }

        if (filter.Radar is { } radar)
        {
            query = query.Where(x => x.DiscoveryLinks.Any(link => link.Discovery.Source.Radar == radar));
        }

        if (filter.SourceId is { } sourceId)
        {
            query = query.Where(x => x.DiscoveryLinks.Any(link => link.Discovery.SourceId == sourceId));
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(x => x.CategoryLinks.Any(link => link.CategoryId == categoryId));
        }

        if (filter.DateFrom is { } from)
        {
            query = query.Where(x => x.FirstDiscoveredAt >= from);
        }

        if (filter.DateTo is { } to)
        {
            query = query.Where(x => x.FirstDiscoveredAt <= to);
        }

        var items = await query.ToListAsync(cancellationToken);
        var search = filter.SearchText?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            items = items.Where(project =>
                    Contains(project.Title, search) ||
                    Contains(project.Description, search) ||
                    project.DiscoveryLinks.Any(link =>
                        Contains(link.Discovery.Title, search) ||
                        Contains(link.Discovery.Description, search)))
                .ToList();
        }

        if (filter.MinSourceCount is { } minSources)
        {
            items = items.Where(x => DistinctSourceIds(x).Count >= minSources).ToList();
        }

        var sorted = Sort(items, filter).ToList();
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var pageItems = sorted
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(MapListItem)
            .ToList();

        return new ProjectPage
        {
            Items = pageItems,
            TotalCount = sorted.Count,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<ProjectDetailsModel?> GetDetailsAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var project = await LoadProjectAsync(projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var discoveries = project.DiscoveryLinks
            .Select(link => new ProjectDiscoveryItem
            {
                DiscoveryId = link.DiscoveryId,
                Title = link.Discovery.Title,
                SourceName = link.Discovery.Source.Name,
                Radar = link.Discovery.Source.Radar,
                SourceUrl = link.Discovery.SourceUrl,
                PublishedAt = link.Discovery.PublishedAt,
                DiscoveredAt = link.Discovery.DiscoveredAt,
                Similarity = link.Similarity,
                Status = link.Discovery.Status,
                ProcessingStatus = link.Discovery.ProcessingStatus,
                AIProcessed = link.Discovery.AIProcessed,
                IsPotentialInnovation = link.Discovery.IsPotentialInnovation,
                AIConfidence = link.Discovery.AIConfidence,
                IsDemo = link.Discovery.IsDemo
            })
            .OrderByDescending(x => x.PublishedAt ?? x.DiscoveredAt)
            .ToList();

        var proposals = await ProposalQuery()
            .Where(x => x.SourceProjectId == projectId || x.TargetProjectId == projectId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var history = await db.ProjectMergeHistories
            .Where(x => x.SourceProjectId == projectId || x.TargetProjectId == projectId)
            .OrderByDescending(x => x.ChangedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var similar = await FindSimilarAsync(project, proposals, cancellationToken);
        var sources = discoveries.Select(x => x.SourceName).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        return new ProjectDetailsModel
        {
            Project = project,
            MergedInto = project.MergedInto,
            Categories = project.CategoryLinks
                .Select(x => x.Category.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList(),
            Discoveries = discoveries,
            SimilarProjects = similar,
            MergeProposals = proposals.Select(MapProposal).ToList(),
            History = history.Select(x => new ProjectMergeHistoryItem
            {
                Id = x.Id,
                SourceProjectId = x.SourceProjectId,
                TargetProjectId = x.TargetProjectId,
                SourceTitle = x.SourceTitle,
                TargetTitle = x.TargetTitle,
                Details = x.Details,
                Actor = x.Actor,
                ChangedAt = x.ChangedAt
            }).ToList(),
            Radars = discoveries.Select(x => x.Radar).Distinct().OrderBy(x => x).ToList(),
            SourceCount = sources,
            AiProcessedCount = discoveries.Count(x => x.AIProcessed),
            PotentialInnovationCount = discoveries.Count(x => x.IsPotentialInnovation)
        };
    }

    public async Task<IReadOnlyList<SimilarProjectItem>> GetSimilarProjectsAsync(
        int projectId,
        int limit = 8,
        CancellationToken cancellationToken = default)
    {
        var project = await LoadProjectAsync(projectId, cancellationToken);
        if (project is null)
        {
            return Array.Empty<SimilarProjectItem>();
        }

        var proposals = await ProposalQuery()
            .Where(x => x.SourceProjectId == projectId || x.TargetProjectId == projectId)
            .ToListAsync(cancellationToken);
        return await FindSimilarAsync(project, proposals, cancellationToken, Math.Clamp(limit, 1, 30));
    }

    public async Task<IReadOnlyList<SimilarProjectItem>> FindSimilarToTextAsync(
        string title,
        string description,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description))
        {
            return Array.Empty<SimilarProjectItem>();
        }

        // Projekt tymczasowy (Id = 0) nie jest zapisywany – służy tylko do porównania tekstu.
        var draft = new InnovationProject
        {
            Title = title.Trim(),
            Description = description.Trim()
        };
        return await FindSimilarAsync(
            draft, Array.Empty<ProjectMergeProposal>(), cancellationToken, Math.Clamp(limit, 1, 30), related: true);
    }

    public async Task<IReadOnlyList<SimilarProjectItem>> GetRelatedProjectsAsync(
        int projectId,
        int limit = 8,
        CancellationToken cancellationToken = default)
    {
        var project = await LoadProjectAsync(projectId, cancellationToken);
        return project is null
            ? Array.Empty<SimilarProjectItem>()
            : await FindSimilarAsync(
                project, Array.Empty<ProjectMergeProposal>(), cancellationToken, Math.Clamp(limit, 1, 30), related: true);
    }

    public async Task<DuplicateDetectionResult> DetectDuplicatesAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Projects.Enabled)
        {
            return new DuplicateDetectionResult
            {
                Message = "Wykrywanie duplikatów jest wyłączone w konfiguracji.",
                Provider = embedder.ProviderName
            };
        }

        await Gate.WaitAsync(cancellationToken);
        try
        {
            var maxProjects = Math.Clamp(options.Value.Projects.MaxProjectsPerRun, 1, 5000);
            var projects = await LoadActiveProjectsAsync(maxProjects, cancellationToken);
            var vectors = await LoadVectorsAsync(projects, cancellationToken);
            var facts = projects.ToDictionary(
                x => x.Id,
                x => new ProjectFacts(
                    ExternalIds: x.DiscoveryLinks
                        .Select(link => ProjectSimilarity.NormalizeIdentifier(link.Discovery.ExternalId))
                        .Where(value => value.Length > 0)
                        .ToHashSet(StringComparer.Ordinal),
                    ContentHashes: x.DiscoveryLinks
                        .Select(link => link.Discovery.ContentHash)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!.Trim())
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)));

            var existingSignatures = (await db.ProjectMergeProposals
                    .Select(x => x.Signature)
                    .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);

            var autoMerged = new HashSet<int>();
            var candidates = new List<DuplicateCandidate>();
            var proposalsCreated = 0;

            for (var i = 0; i < projects.Count; i++)
            {
                for (var j = i + 1; j < projects.Count; j++)
                {
                    var first = projects[i];
                    var second = projects[j];
                    if (autoMerged.Contains(first.Id) || autoMerged.Contains(second.Id))
                    {
                        continue;
                    }

                    var signals = new List<string>();
                    var automatic = false;
                    var sharedExternalId = facts[first.Id].ExternalIds
                        .FirstOrDefault(x => facts[second.Id].ExternalIds.Contains(x));
                    if (sharedExternalId is not null)
                    {
                        signals.Add($"ExternalId: {Truncate(sharedExternalId, 60)}");
                        automatic = true;
                    }

                    var sharedHash = facts[first.Id].ContentHashes
                        .FirstOrDefault(x => facts[second.Id].ContentHashes.Contains(x));
                    if (sharedHash is not null)
                    {
                        signals.Add($"ContentHash: {Truncate(sharedHash, 24)}");
                        automatic = true;
                    }

                    var embedding = vectors.TryGetValue(first.Id, out var firstVector) &&
                                    vectors.TryGetValue(second.Id, out var secondVector)
                        ? EmbeddingMath.CosineSimilarity(firstVector, secondVector)
                        : 0;
                    var title = ProjectSimilarity.TitleSimilarity(first, second);
                    var description = ProjectSimilarity.DescriptionSimilarity(first, second);
                    signals.Add($"embedding {embedding:0.00}");
                    signals.Add($"tytuł {title:0.00}");
                    signals.Add($"opis {description:0.00}");

                    var score = 0.50 * Math.Max(0, embedding) + 0.30 * title + 0.20 * description;
                    if (automatic)
                    {
                        score = Math.Max(score, 0.98);
                    }
                    else if (embedding >= options.Value.Projects.AutoMergeSimilarity &&
                             title >= 0.70 && description >= 0.45)
                    {
                        automatic = true;
                        signals.Add("wysokie podobieństwo tekstu i embeddingu");
                    }

                    if (!automatic && score < options.Value.Projects.ProposalSimilarity)
                    {
                        continue;
                    }

                    var source = FirstDiscoveredAt(first) <= FirstDiscoveredAt(second) ? second : first;
                    var target = ReferenceEquals(source, first) ? second : first;
                    var signalText = string.Join("; ", signals);
                    candidates.Add(new DuplicateCandidate
                    {
                        SourceProjectId = source.Id,
                        SourceTitle = source.Title,
                        TargetProjectId = target.Id,
                        TargetTitle = target.Title,
                        Similarity = Math.Round(Math.Clamp(score, 0, 1), 3),
                        Signals = signalText,
                        Decision = automatic ? "Połączono automatycznie" : "Propozycja dla użytkownika",
                        IsAutomatic = automatic
                    });

                    if (automatic)
                    {
                        await MergeProjectsAsync(source, target, "System", signalText, cancellationToken);
                        autoMerged.Add(first.Id);
                        autoMerged.Add(second.Id);
                    }
                    else
                    {
                        var signature = MergeSignature(source.Id, target.Id);
                        if (!existingSignatures.Contains(signature))
                        {
                            db.ProjectMergeProposals.Add(new ProjectMergeProposal
                            {
                                SourceProjectId = source.Id,
                                TargetProjectId = target.Id,
                                Similarity = Math.Round(Math.Clamp(score, 0, 1), 3),
                                Signals = Truncate(signalText, ContentLimits.MaxDescriptionLength),
                                Signature = signature,
                                Status = MergeProposalStatus.Pending,
                                CreatedAt = DateTime.UtcNow
                            });
                            existingSignatures.Add(signature);
                            proposalsCreated++;
                        }
                    }
                }
            }

            if (candidates.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            var message = $"Przeanalizowano {projects.Count} projektów. " +
                          $"Jednoznaczne duplikaty: {autoMerged.Count / 2}. " +
                          $"Propozycje do decyzji: {proposalsCreated}. " +
                          "Materiały źródłowe nie zostały usunięte.";
            logger.LogInformation("Deduplikacja projektów: {Message}", message);
            return new DuplicateDetectionResult
            {
                ProjectsAnalyzed = projects.Count,
                CandidatesFound = candidates.Count,
                AutoLinked = autoMerged.Count / 2,
                ProposalsCreated = proposalsCreated,
                Provider = embedder.ProviderName,
                Message = message,
                Candidates = candidates.Take(100).ToList()
            };
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<IReadOnlyList<ProjectMergeProposalModel>> GetProposalsAsync(CancellationToken cancellationToken = default)
    {
        var proposals = await ProposalQuery()
            .OrderBy(x => x.Status == MergeProposalStatus.Pending ? 0 : 1)
            .ThenByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return proposals.Select(MapProposal).ToList();
    }

    public async Task<ProjectMergeProposalModel?> ProposeMergeAsync(
        int sourceProjectId,
        int targetProjectId,
        CancellationToken cancellationToken = default)
    {
        if (sourceProjectId == targetProjectId)
        {
            return null;
        }

        var projects = await db.InnovationProjects
            .Where(x => x.Id == sourceProjectId || x.Id == targetProjectId)
            .ToListAsync(cancellationToken);
        var source = projects.FirstOrDefault(x => x.Id == sourceProjectId);
        var target = projects.FirstOrDefault(x => x.Id == targetProjectId);
        if (source is null || target is null ||
            source.MergedIntoProjectId is not null || target.MergedIntoProjectId is not null)
        {
            return null;
        }

        var signature = MergeSignature(sourceProjectId, targetProjectId);
        if (await db.ProjectMergeProposals.AnyAsync(x => x.Signature == signature, cancellationToken))
        {
            return null;
        }

        var proposal = new ProjectMergeProposal
        {
            SourceProjectId = sourceProjectId,
            TargetProjectId = targetProjectId,
            Similarity = TextScore(source, target),
            Signals = "Propozycja zgłoszona przez użytkownika na podstawie listy podobnych projektów.",
            Signature = signature,
            Status = MergeProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.ProjectMergeProposals.Add(proposal);
        await db.SaveChangesAsync(cancellationToken);
        return MapProposal(proposal);
    }

    public async Task<bool> AcceptProposalAsync(int proposalId, CancellationToken cancellationToken = default)
    {
        var proposal = await ProposalQuery()
            .FirstOrDefaultAsync(x => x.Id == proposalId, cancellationToken);
        if (proposal is null || proposal.Status != MergeProposalStatus.Pending ||
            proposal.SourceProject.MergedIntoProjectId is not null ||
            proposal.TargetProject.MergedIntoProjectId is not null)
        {
            return false;
        }

        await MergeProjectsAsync(
            proposal.SourceProject,
            proposal.TargetProject,
            "Użytkownik",
            $"Zaakceptowano propozycję: {proposal.Signals}",
            cancellationToken);
        proposal.Status = MergeProposalStatus.Accepted;
        proposal.ResolvedAt = DateTime.UtcNow;
        proposal.DecisionNote = "Połączenie zaakceptowane przez użytkownika.";
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RejectProposalAsync(int proposalId, CancellationToken cancellationToken = default)
    {
        var proposal = await db.ProjectMergeProposals
            .FirstOrDefaultAsync(x => x.Id == proposalId, cancellationToken);
        if (proposal is null || proposal.Status != MergeProposalStatus.Pending)
        {
            return false;
        }

        proposal.Status = MergeProposalStatus.Rejected;
        proposal.ResolvedAt = DateTime.UtcNow;
        proposal.DecisionNote = "Użytkownik odrzucił propozycję; nie będzie ponawiana.";
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task MergeProjectsAsync(
        InnovationProject source,
        InnovationProject target,
        string actor,
        string details,
        CancellationToken cancellationToken)
    {
        if (source.Id == target.Id)
        {
            return;
        }

        await LoadLinksAsync(source, cancellationToken);
        await LoadLinksAsync(target, cancellationToken);

        var targetDiscoveryIds = target.DiscoveryLinks
            .Select(x => x.DiscoveryId)
            .ToHashSet();
        foreach (var link in source.DiscoveryLinks.ToList())
        {
            db.DiscoveryProjects.Remove(link);
            if (targetDiscoveryIds.Contains(link.DiscoveryId))
            {
                continue;
            }

            target.DiscoveryLinks.Add(new DiscoveryProject
            {
                Discovery = link.Discovery,
                Project = target,
                Similarity = link.Similarity,
                CreatedAt = link.CreatedAt
            });
        }

        foreach (var link in source.CategoryLinks.ToList())
        {
            if (target.CategoryLinks.Any(x => x.CategoryId == link.CategoryId))
            {
                continue;
            }

            db.ProjectCategories.Add(new ProjectCategory
            {
                InnovationProjectId = target.Id,
                CategoryId = link.CategoryId,
                Confidence = link.Confidence,
                CreatedAt = link.CreatedAt
            });
        }

        target.FirstDiscoveredAt = Min(target.FirstDiscoveredAt, source.FirstDiscoveredAt);
        target.EarliestKnownDate = MinNullable(target.EarliestKnownDate, source.EarliestKnownDate);
        target.LastActivityAt = MaxNullable(target.LastActivityAt, source.LastActivityAt);
        target.FreshnessScore = FreshnessCalculator.Calculate(target, DateTime.UtcNow);

        source.MergedIntoProjectId = target.Id;
        source.MergedInto = target;
        db.ProjectMergeHistories.Add(new ProjectMergeHistory
        {
            SourceProjectId = source.Id,
            TargetProjectId = target.Id,
            SourceTitle = source.Title,
            TargetTitle = target.Title,
            Details = Truncate(details, ContentLimits.MaxDescriptionLength),
            Actor = actor,
            ChangedAt = DateTime.UtcNow
        });
    }

    private async Task LoadLinksAsync(InnovationProject project, CancellationToken cancellationToken)
    {
        var entry = db.Entry(project);
        if (!entry.Collection(x => x.DiscoveryLinks).IsLoaded)
        {
            await entry.Collection(x => x.DiscoveryLinks)
                .Query()
                .Include(x => x.Discovery)
                .LoadAsync(cancellationToken);
            entry.Collection(x => x.DiscoveryLinks).IsLoaded = true;
        }

        if (!entry.Collection(x => x.CategoryLinks).IsLoaded)
        {
            await entry.Collection(x => x.CategoryLinks).LoadAsync(cancellationToken);
            entry.Collection(x => x.CategoryLinks).IsLoaded = true;
        }
    }

    private async Task<List<InnovationProject>> LoadActiveProjectsAsync(int limit, CancellationToken cancellationToken) =>
        await db.InnovationProjects
            .Where(x => x.MergedIntoProjectId == null)
            .Include(x => x.DiscoveryLinks).ThenInclude(x => x.Discovery).ThenInclude(x => x.Source)
            .Include(x => x.DiscoveryLinks).ThenInclude(x => x.Discovery).ThenInclude(x => x.AiAnalyses)
            .Include(x => x.CategoryLinks).ThenInclude(x => x.Category)
            .OrderBy(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    private async Task<InnovationProject?> LoadProjectAsync(int projectId, CancellationToken cancellationToken) =>
        await db.InnovationProjects
            .Include(x => x.MergedInto)
            .Include(x => x.CategoryLinks).ThenInclude(x => x.Category)
            .Include(x => x.DiscoveryLinks).ThenInclude(x => x.Discovery).ThenInclude(x => x.Source)
            .Include(x => x.DiscoveryLinks).ThenInclude(x => x.Discovery).ThenInclude(x => x.AiAnalyses)
            .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);

    private IQueryable<ProjectMergeProposal> ProposalQuery() => db.ProjectMergeProposals
        .Include(x => x.SourceProject)
        .Include(x => x.TargetProject);

    private async Task<Dictionary<int, float[]>> LoadVectorsAsync(
        IReadOnlyList<InnovationProject> projects,
        CancellationToken cancellationToken)
    {
        var ids = projects.Select(x => x.Id).ToList();
        var rows = await db.ProjectEmbeddings
            .Where(x => ids.Contains(x.InnovationProjectId))
            .ToListAsync(cancellationToken);
        var byProject = rows.ToDictionary(x => x.InnovationProjectId);
        var vectors = new Dictionary<int, float[]>();
        foreach (var project in projects)
        {
            if (byProject.TryGetValue(project.Id, out var row))
            {
                try
                {
                    var stored = JsonSerializer.Deserialize<float[]>(row.Vector, VectorJsonOptions);
                    if (stored is { Length: > 0 })
                    {
                        vectors[project.Id] = stored;
                        continue;
                    }
                }
                catch (JsonException)
                {
                    // Uszkodzony wektor jest przeliczany poniżej.
                }
            }

            vectors[project.Id] = await embedder.EmbedAsync(ProjectTextBuilder.Build(project), cancellationToken);
        }

        return vectors;
    }

    private async Task<IReadOnlyList<SimilarProjectItem>> FindSimilarAsync(
        InnovationProject project,
        IReadOnlyList<ProjectMergeProposal> proposals,
        CancellationToken cancellationToken,
        int? limit = null,
        bool related = false)
    {
        var max = limit ?? Math.Clamp(options.Value.Projects.SimilarProjectsLimit, 1, 30);
        var candidates = await LoadActiveProjectsAsync(
            Math.Clamp(options.Value.Projects.MaxProjectsPerRun, 1, 5000),
            cancellationToken);
        var vectors = await LoadVectorsAsync(candidates.Append(project).DistinctBy(x => x.Id).ToList(), cancellationToken);
        vectors.TryGetValue(project.Id, out var vector);
        var minimum = Math.Max(0.25, options.Value.Projects.ProposalSimilarity - 0.22);

        var result = new List<SimilarProjectItem>();
        foreach (var candidate in candidates.Where(x => x.Id != project.Id))
        {
            var similarity = vector is not null && vectors.TryGetValue(candidate.Id, out var other)
                ? EmbeddingMath.CosineSimilarity(vector, other)
                : TextScore(project, candidate);
            var textScore = related && similarity < minimum ? TextScore(project, candidate) : 0;
            if (similarity < minimum &&
                !(related && similarity >= RelatedMinimumSimilarity && textScore >= RelatedMinimumTextScore))
            {
                continue;
            }

            var categoryNames = candidate.CategoryLinks
                .Select(x => x.Category.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var sharedCategories = project.CategoryLinks
                .Select(x => x.Category.Name)
                .Where(categoryNames.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var sourceIds = DistinctSourceIds(candidate);
            var sourceNames = candidate.DiscoveryLinks
                .Select(x => x.Discovery.Source.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var reasons = new List<string> { $"podobieństwo wektorów {similarity:0.00}" };
            if (textScore > 0)
            {
                reasons.Add($"wspólne słownictwo {textScore:0.00}");
            }

            if (sharedCategories.Count > 0)
            {
                reasons.Add("wspólne kategorie: " + string.Join(", ", sharedCategories.Take(3)));
            }

            if (sourceNames.Count > 0)
            {
                reasons.Add("źródła: " + string.Join(", ", sourceNames.Take(3)));
            }

            var hasPending = proposals.Any(proposal =>
                proposal.Status == MergeProposalStatus.Pending &&
                ((proposal.SourceProjectId == project.Id && proposal.TargetProjectId == candidate.Id) ||
                 (proposal.SourceProjectId == candidate.Id && proposal.TargetProjectId == project.Id)));
            result.Add(new SimilarProjectItem
            {
                ProjectId = candidate.Id,
                Title = candidate.Title,
                Similarity = Math.Round(Math.Clamp(similarity, 0, 1), 3),
                Reason = string.Join("; ", reasons),
                DiscoveryCount = candidate.DiscoveryLinks.Count,
                SourceCount = sourceIds.Count,
                Radars = candidate.DiscoveryLinks
                    .Select(x => x.Discovery.Source.Radar)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList(),
                HasPendingProposal = hasPending
            });
        }

        return result
            .OrderByDescending(x => x.Similarity)
            .ThenByDescending(x => x.SourceCount)
            .Take(max)
            .ToList();
    }

    /// <summary>
    /// Tryb „powiązanych projektów” (podobny problem, nie duplikat): poza progiem wektorów przyjmuje
    /// pary o niższym podobieństwie wektorów, jeśli mają też wspólne słownictwo. Lokalny embedder
    /// hashingowy ma szum tła ok. 0,2–0,27, więc samo obniżenie progu wektorów dawałoby fałszywe trafienia.
    /// </summary>
    public const double RelatedMinimumSimilarity = 0.30;
    public const double RelatedMinimumTextScore = 0.10;

    private static double TextScore(InnovationProject first, InnovationProject second) =>
        Math.Max(0,
            0.40 * ProjectSimilarity.TokenSimilarity(
                ProjectTextBuilder.Build(first), ProjectTextBuilder.Build(second)) +
            0.35 * ProjectSimilarity.TitleSimilarity(first, second) +
            0.25 * ProjectSimilarity.DescriptionSimilarity(first, second));

    private static IEnumerable<InnovationProject> Sort(IReadOnlyList<InnovationProject> items, ProjectFilter filter)
    {
        IOrderedEnumerable<InnovationProject> ordered = filter.SortBy switch
        {
            ProjectSort.FreshnessScore => filter.SortDescending
                ? items.OrderByDescending(x => x.FreshnessScore)
                : items.OrderBy(x => x.FreshnessScore),
            ProjectSort.SourceCount => filter.SortDescending
                ? items.OrderByDescending(x => DistinctSourceIds(x).Count)
                : items.OrderBy(x => DistinctSourceIds(x).Count),
            ProjectSort.Title => filter.SortDescending
                ? items.OrderByDescending(x => x.Title, StringComparer.OrdinalIgnoreCase)
                : items.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase),
            _ => filter.SortDescending
                ? items.OrderByDescending(x => x.FirstDiscoveredAt)
                : items.OrderBy(x => x.FirstDiscoveredAt)
        };
        return ordered.ThenBy(x => x.Id);
    }

    private static ProjectListItem MapListItem(InnovationProject project) => new()
    {
        Id = project.Id,
        Title = project.Title,
        Description = project.Description,
        Categories = project.CategoryLinks
            .Select(x => x.Category.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList(),
        DiscoveryCount = project.DiscoveryLinks.Count,
        SourceCount = DistinctSourceIds(project).Count,
        SourceNames = project.DiscoveryLinks
            .Select(x => x.Discovery.Source.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList(),
        Radars = project.DiscoveryLinks
            .Select(x => x.Discovery.Source.Radar)
            .Distinct()
            .OrderBy(x => x)
            .ToList(),
        FreshnessScore = project.FreshnessScore,
        FirstDiscoveredAt = project.FirstDiscoveredAt,
        LastActivityAt = project.LastActivityAt,
        IsDemo = project.IsDemo,
        MergedIntoProjectId = project.MergedIntoProjectId
    };

    private static ProjectMergeProposalModel MapProposal(ProjectMergeProposal proposal) => new()
    {
        Id = proposal.Id,
        SourceProjectId = proposal.SourceProjectId,
        SourceTitle = proposal.SourceProject?.Title ?? $"Projekt {proposal.SourceProjectId}",
        TargetProjectId = proposal.TargetProjectId,
        TargetTitle = proposal.TargetProject?.Title ?? $"Projekt {proposal.TargetProjectId}",
        Similarity = proposal.Similarity,
        Signals = proposal.Signals,
        Status = proposal.Status,
        CreatedAt = proposal.CreatedAt,
        ResolvedAt = proposal.ResolvedAt
    };

    private static HashSet<int> DistinctSourceIds(InnovationProject project) => project.DiscoveryLinks
        .Select(x => x.Discovery.SourceId)
        .ToHashSet();

    private static DateTime FirstDiscoveredAt(InnovationProject project) => project.FirstDiscoveredAt;

    private static string MergeSignature(int first, int second) => $"{Math.Min(first, second)}:{Math.Max(first, second)}";

    private static bool Contains(string? value, string search) =>
        !string.IsNullOrEmpty(value) && value.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static DateTime Min(DateTime first, DateTime second) => first <= second ? first : second;

    private static DateTime? MinNullable(DateTime? first, DateTime? second) =>
        first is null ? second : second is null ? first : first <= second ? first : second;

    private static DateTime? MaxNullable(DateTime? first, DateTime? second) =>
        first is null ? second : second is null ? first : first >= second ? first : second;

    private static string Truncate(string value, int maxLength) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= maxLength ? value : value[..maxLength];

    private sealed record ProjectFacts(HashSet<string> ExternalIds, HashSet<string> ContentHashes);
}
