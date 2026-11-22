using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Assessment;
using RadarInnowacji.Infrastructure.Configuration;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>
/// Karta oceny potencjału, rekomendacje i dopasowanie odbiorców (Etap 9).
/// Ocena jest liczona jawnymi regułami (<see cref="AssessmentScorer"/>, <see cref="RecommendationEngine"/>);
/// nowa migawka jest zapisywana tylko wtedy, gdy zmienił się wynik – niezmienione projekty nie generują historii.
/// </summary>
public sealed class AssessmentService(
    RadarDbContext db,
    IProjectService projectService,
    IOptions<RadarOptions> options,
    ILogger<AssessmentService> logger) : IAssessmentService
{
    private const int MaxTextLength = ContentLimits.MaxDescriptionLength;
    private const int RefreshAfterDays = 7;

    private static readonly string[] OpenLicenses =
        ["mit", "apache", "gpl", "lgpl", "agpl", "bsd", "mpl", "cc-by", "cc by", "cc0", "unlicense", "isc", "eupl"];

    public async Task<ProjectAssessmentCard?> GetCardAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var context = await BuildContextAsync(projectId, cancellationToken);
        if (context is null)
        {
            return null;
        }

        await SaveIfChangedAsync(context, "System", cancellationToken);
        var project = context.Project;
        var recipients = await db.Recipients.AsNoTracking().ToListAsync(cancellationToken);
        var paths = context.Recommendations.Select(x => x.Path).ToList();
        var matches = RecipientMatcher.Match(new RecipientMatchInput
        {
            Sectors = context.Input.Sectors,
            Text = ProjectText(context.Input),
            Stage = context.Input.Stage,
            Kind = context.Input.Kind,
            Region = context.Input.Region,
            Paths = paths
        }, recipients);

        var events = await db.ProjectPipelineEvents
            .AsNoTracking()
            .Where(x => x.InnovationProjectId == projectId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PipelineEventItem(x.FromStage, x.ToStage, x.Comment, x.Actor, x.CreatedAt))
            .ToListAsync(cancellationToken);
        var referrals = await db.ProjectReferrals
            .AsNoTracking()
            .Where(x => x.InnovationProjectId == projectId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ReferralItem(x.RecipientId, x.Recipient.Name, x.Note, x.CreatedAt))
            .ToListAsync(cancellationToken);
        var history = await db.ProjectAssessments
            .AsNoTracking()
            .Where(x => x.InnovationProjectId == projectId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .Select(x => new
            {
                x.CreatedAt,
                x.TotalScore,
                x.Confidence,
                x.Actor,
                Paths = x.Recommendations.OrderBy(r => r.Rank).Select(r => r.Path).ToList()
            })
            .ToListAsync(cancellationToken);
        var current = await db.ProjectAssessments
            .AsNoTracking()
            .Where(x => x.InnovationProjectId == projectId && x.IsCurrent)
            .Select(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new ProjectAssessmentCard
        {
            ProjectId = project.Id,
            Title = project.Title,
            IsDemo = project.IsDemo,
            Profile = new ProjectProfile
            {
                Institution = project.Institution,
                InstitutionType = project.InstitutionType,
                InnovationKind = project.InnovationKind,
                Stage = project.ProjectStatus,
                Region = project.Region,
                Creators = project.Creators,
                Problem = project.Problem,
                Solution = project.ProposedSolution,
                ContactEmail = project.ContactConsent ? project.ContactEmail : null,
                ContactConsent = project.ContactConsent
            },
            InferredNotes = context.InferredNotes,
            TotalScore = context.Outcome.TotalScore,
            Confidence = context.Outcome.Confidence,
            Completeness = context.Outcome.Completeness,
            MissingFields = context.Outcome.MissingFields,
            AssessedAt = current == default ? DateTime.UtcNow : current,
            Criteria = context.Outcome.Criteria.Select(result =>
            {
                var info = AssessmentScorer.Info(result.Criterion);
                context.Overrides.TryGetValue(result.Criterion, out var expert);
                return new CriterionCardItem
                {
                    Criterion = result.Criterion,
                    Name = info.Name,
                    Question = info.Question,
                    AutomaticScore = context.Automatic.Score(result.Criterion),
                    FinalScore = result.Score,
                    Weight = result.Weight,
                    Origin = expert is null ? CriterionOrigin.Regula : CriterionOrigin.Ekspert,
                    Signals = context.Automatic.Criteria.Single(x => x.Criterion == result.Criterion).Signals,
                    ExpertComment = expert?.Comment
                };
            }).ToList(),
            Recommendations = context.Recommendations.Select(recommendation =>
            {
                var info = DevelopmentPathCatalog.Get(recommendation.Path);
                return new RecommendationCardItem
                {
                    Path = recommendation.Path,
                    Rank = recommendation.Rank,
                    Name = info.Name,
                    Description = info.Description,
                    Reasons = recommendation.Reasons,
                    NextSteps = info.NextSteps,
                    Metrics = info.Metrics
                };
            }).ToList(),
            Sectors = context.Input.Sectors,
            RecipientMatches = matches,
            SimilarInstitutions = context.SimilarInstitutions,
            Stage = project.PipelineStage,
            AllowedStages = PipelineRules.Allowed(project.PipelineStage),
            Events = events,
            Referrals = referrals,
            History = history
                .Select(x => new AssessmentHistoryItem(
                    x.CreatedAt,
                    x.TotalScore,
                    x.Confidence,
                    x.Actor,
                    string.Join(", ", x.Paths.Select(path => DevelopmentPathCatalog.Get(path).Name))))
                .ToList()
        };
    }

    public async Task<bool> AssessAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var context = await BuildContextAsync(projectId, cancellationToken);
        return context is not null && await SaveIfChangedAsync(context, "System", cancellationToken);
    }

    public async Task<int> AssessPendingAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value.Assessment;
        if (!settings.Enabled)
        {
            return 0;
        }

        var refreshBefore = DateTime.UtcNow.AddDays(-RefreshAfterDays);
        var ids = await db.InnovationProjects
            .AsNoTracking()
            .Where(x => x.MergedIntoProjectId == null)
            .Select(x => new
            {
                x.Id,
                x.ProfileUpdatedAt,
                Current = x.Assessments.Where(a => a.IsCurrent).Select(a => (DateTime?)a.CreatedAt).FirstOrDefault(),
                LastMaterial = x.DiscoveryLinks.Max(link => (DateTime?)link.Discovery.DiscoveredAt)
            })
            .Where(x => x.Current == null ||
                        x.Current < refreshBefore ||
                        (x.ProfileUpdatedAt != null && x.ProfileUpdatedAt > x.Current) ||
                        (x.LastMaterial != null && x.LastMaterial > x.Current))
            .OrderBy(x => x.Current ?? DateTime.MinValue)
            .Take(Math.Clamp(settings.MaxProjectsPerRun, 1, 5000))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var saved = 0;
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var context = await BuildContextAsync(id, cancellationToken);
                if (context is null)
                {
                    continue;
                }

                // Odświeżenie daty oceny, gdy wynik się nie zmienił – projekt nie wraca w kolejnym cyklu.
                if (await SaveIfChangedAsync(context, "System", cancellationToken, touchWhenUnchanged: true))
                {
                    saved++;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Nie udało się ocenić projektu {ProjectId}.", id);
                db.ChangeTracker.Clear();
            }
        }

        return saved;
    }

    public async Task<OperationResult> UpdateProfileAsync(
        int projectId,
        ProjectProfile profile,
        CancellationToken cancellationToken = default)
    {
        var project = await db.InnovationProjects.FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        if (project is null)
        {
            return OperationResult.Fail("Nie znaleziono projektu.");
        }

        var errors = ValidateProfile(profile);
        if (errors.Count > 0)
        {
            return OperationResult.Fail(string.Join(" ", errors));
        }

        project.Institution = Clean(profile.Institution, ContentLimits.MaxTitleLength);
        project.InstitutionType = profile.InstitutionType;
        project.InnovationKind = profile.InnovationKind;
        project.ProjectStatus = profile.Stage;
        project.Region = Clean(profile.Region, 100);
        project.Creators = Clean(profile.Creators, 1000);
        project.Problem = Clean(profile.Problem, MaxTextLength);
        project.ProposedSolution = Clean(profile.Solution, MaxTextLength);
        project.ContactConsent = profile.ContactConsent && !string.IsNullOrWhiteSpace(profile.ContactEmail);
        project.ContactEmail = project.ContactConsent ? Clean(profile.ContactEmail, 254) : null;
        project.ProfileUpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var context = await BuildContextAsync(projectId, cancellationToken);
        if (context is not null)
        {
            await SaveIfChangedAsync(context, "Użytkownik", cancellationToken);
        }

        return OperationResult.Ok("Zapisano profil projektu i przeliczono ocenę.");
    }

    public async Task<OperationResult> SetExpertScoreAsync(
        int projectId,
        AssessmentCriterion criterion,
        int? score,
        string comment,
        CancellationToken cancellationToken = default)
    {
        if (!await db.InnovationProjects.AnyAsync(x => x.Id == projectId, cancellationToken))
        {
            return OperationResult.Fail("Nie znaleziono projektu.");
        }

        if (score is < 0 or > AssessmentScorer.MaxScore)
        {
            return OperationResult.Fail("Ocena musi mieć wartość od 0 do 5.");
        }

        var trimmed = comment?.Trim() ?? string.Empty;
        if (score is not null && trimmed.Length < 5)
        {
            return OperationResult.Fail("Uzasadnij ocenę eksperta (co najmniej 5 znaków).");
        }

        var active = await db.CriterionOverrides
            .Where(x => x.InnovationProjectId == projectId && x.Criterion == criterion && x.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var item in active)
        {
            item.IsActive = false;
        }

        if (score is not null)
        {
            db.CriterionOverrides.Add(new CriterionOverride
            {
                InnovationProjectId = projectId,
                Criterion = criterion,
                Score = score.Value,
                Comment = trimmed.Length > ContentLimits.MaxErrorLength ? trimmed[..ContentLimits.MaxErrorLength] : trimmed,
                Author = "Ekspert",
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        var context = await BuildContextAsync(projectId, cancellationToken);
        if (context is not null)
        {
            await SaveIfChangedAsync(context, "Ekspert", cancellationToken);
        }

        var name = AssessmentScorer.Info(criterion).Name;
        return OperationResult.Ok(score is null
            ? $"Przywrócono ocenę automatyczną kryterium „{name}”."
            : $"Zapisano ocenę eksperta kryterium „{name}”: {score}/5.");
    }

    /// <summary>Profil zgłoszenia/projektu – wspólna walidacja z formularzem zgłoszeń.</summary>
    internal static List<string> ValidateProfile(ProjectProfile profile)
    {
        var errors = new List<string>();
        if (profile.Institution?.Length > ContentLimits.MaxTitleLength)
        {
            errors.Add("Nazwa instytucji jest za długa.");
        }

        if (profile.Region?.Length > 100)
        {
            errors.Add("Region może mieć najwyżej 100 znaków.");
        }

        if (profile.Creators?.Length > 1000)
        {
            errors.Add("Lista twórców może mieć najwyżej 1000 znaków.");
        }

        if (profile.Problem?.Length > MaxTextLength || profile.Solution?.Length > MaxTextLength)
        {
            errors.Add($"Opis problemu i rozwiązania może mieć najwyżej {MaxTextLength} znaków.");
        }

        if (!string.IsNullOrWhiteSpace(profile.ContactEmail))
        {
            if (!IsEmail(profile.ContactEmail))
            {
                errors.Add("Podaj poprawny adres e-mail.");
            }
            else if (!profile.ContactConsent)
            {
                errors.Add("Adres e-mail można zapisać tylko po zaznaczeniu zgody na kontakt.");
            }
        }

        return errors;
    }

    internal static bool IsEmail(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length is < 6 or > 254 || trimmed.Contains(' '))
        {
            return false;
        }

        var at = trimmed.IndexOf('@');
        return at > 0 && at == trimmed.LastIndexOf('@') && trimmed.IndexOf('.', at) > at + 1 && !trimmed.EndsWith('.');
    }

    private async Task<bool> SaveIfChangedAsync(
        AssessmentContext context,
        string actor,
        CancellationToken cancellationToken,
        bool touchWhenUnchanged = false)
    {
        var hash = Hash(context);
        var current = await db.ProjectAssessments
            .Where(x => x.InnovationProjectId == context.Project.Id && x.IsCurrent)
            .ToListAsync(cancellationToken);
        if (current.Count == 1 && current[0].InputHash == hash)
        {
            if (touchWhenUnchanged)
            {
                current[0].CreatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }

            return false;
        }

        foreach (var item in current)
        {
            item.IsCurrent = false;
        }

        var assessment = new ProjectAssessment
        {
            InnovationProjectId = context.Project.Id,
            CreatedAt = DateTime.UtcNow,
            TotalScore = context.Outcome.TotalScore,
            Confidence = context.Outcome.Confidence,
            InputHash = hash,
            IsCurrent = true,
            Actor = actor
        };
        foreach (var result in context.Outcome.Criteria)
        {
            context.Overrides.TryGetValue(result.Criterion, out var expert);
            assessment.Criteria.Add(new AssessmentCriterionScore
            {
                Criterion = result.Criterion,
                AutomaticScore = context.Automatic.Score(result.Criterion),
                FinalScore = result.Score,
                Weight = result.Weight,
                Origin = expert is null ? CriterionOrigin.Regula : CriterionOrigin.Ekspert,
                Justification = Limit(string.Join("; ",
                    context.Automatic.Criteria.Single(x => x.Criterion == result.Criterion).Signals), ContentLimits.MaxErrorLength),
                ExpertComment = expert?.Comment
            });
        }

        foreach (var recommendation in context.Recommendations)
        {
            assessment.Recommendations.Add(new ProjectRecommendation
            {
                Path = recommendation.Path,
                Rank = recommendation.Rank,
                Justification = Limit(JsonSerializer.Serialize(recommendation.Reasons), ContentLimits.MaxDescriptionLength)
            });
        }

        db.ProjectAssessments.Add(assessment);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<AssessmentContext?> BuildContextAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await db.InnovationProjects
            .Include(x => x.CategoryLinks).ThenInclude(x => x.Category)
            .Include(x => x.DiscoveryLinks).ThenInclude(x => x.Discovery).ThenInclude(x => x.Source)
            .Include(x => x.DiscoveryLinks).ThenInclude(x => x.Discovery).ThenInclude(x => x.Repository)
            .Include(x => x.DiscoveryLinks).ThenInclude(x => x.Discovery).ThenInclude(x => x.AiAnalyses)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var discoveries = project.DiscoveryLinks.Select(x => x.Discovery).ToList();
        var ai = LatestAi(discoveries);
        var notes = new List<string>();

        var kind = project.InnovationKind;
        string? kindSource = kind == InnovationKind.Nieokreslona ? null : "profil projektu";
        if (kind == InnovationKind.Nieokreslona)
        {
            kind = KindFromText(ai.InnovationType);
            if (kind != InnovationKind.Nieokreslona)
            {
                kindSource = "analiza AI";
                notes.Add($"Rodzaj innowacji „{AssessmentScorer.KindName(kind)}” wywnioskowano z analizy AI – potwierdź w profilu.");
            }
            else if (discoveries.Any(x => x.Source.Radar == RadarKind.Repozytorium))
            {
                kind = InnovationKind.Technologiczna;
                kindSource = "radar repozytoriów";
                notes.Add("Rodzaj innowacji „technologiczna” przyjęto, bo projekt pochodzi z repozytorium kodu – potwierdź w profilu.");
            }
        }

        var institutionType = project.InstitutionType;
        if (institutionType == InstitutionType.Nieznany && discoveries.Any(x => x.Source.Radar == RadarKind.Uczelnia))
        {
            institutionType = InstitutionType.Uczelnia;
            notes.Add("Typ instytucji „uczelnia” przyjęto na podstawie radaru uczelni – potwierdź w profilu.");
        }

        var institution = InstitutionOf(project);
        var similar = await projectService.GetRelatedProjectsAsync(projectId, 8, cancellationToken);
        var similarInstitutions = await SimilarInstitutionsAsync(similar, institution, cancellationToken);
        var repositories = discoveries.Select(x => x.Repository).Where(x => x is not null).ToList();
        var license = repositories.Select(x => x!.License).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        var lastActivity = new[] { project.LastActivityAt }
            .Concat(discoveries.Select(x => x.LastActivityAt))
            .Concat(discoveries.Select(x => x.PublishedAt))
            .Where(x => x is not null)
            .Max();
        var creators = !string.IsNullOrWhiteSpace(project.Creators)
            ? project.Creators
            : string.Join(", ", discoveries
                .Select(x => x.Author)
                .Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith("Redakcja", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5));
        var problem = FirstText(project.Problem, ai.Problem);
        var solution = FirstText(project.ProposedSolution, ai.Solution);
        var technologies = discoveries.SelectMany(x => ParseList(x.Technologies));
        var sectorText = string.Join(' ', new[] { project.Title, project.Description, problem, solution }
            .Concat(project.CategoryLinks.Select(x => x.Category.Name))
            .Concat(technologies)
            .Concat(discoveries.Take(5).Select(x => x.Title + " " + x.Description)));

        var input = new AssessmentInput
        {
            Title = project.Title,
            Description = FirstText(project.Description, discoveries.Select(x => x.Description).FirstOrDefault()) ?? string.Empty,
            Problem = problem,
            Solution = solution,
            Kind = kind,
            KindSource = kindSource,
            InstitutionType = institutionType,
            Institution = institution,
            Stage = project.ProjectStatus,
            Region = project.Region,
            Creators = string.IsNullOrWhiteSpace(creators) ? null : creators,
            HasContact = project.ContactConsent && !string.IsNullOrWhiteSpace(project.ContactEmail),
            HasLink = discoveries.Any(x => x.SourceUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)),
            AiPotential = ai.Potential,
            AiConfidence = ai.Confidence,
            AiNovelty = ai.Novelty,
            MaxSimilarity = similar.Count == 0 ? 0 : similar.Max(x => x.Similarity),
            MostSimilarTitle = similar.OrderByDescending(x => x.Similarity).Select(x => x.Title).FirstOrDefault(),
            SimilarInOtherInstitutions = similarInstitutions.Select(x => x.Institution).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            OtherInstitutions = similarInstitutions.Select(x => x.Institution).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            DistinctSources = discoveries.Select(x => x.SourceId).Distinct().Count(),
            License = license,
            HasOpenLicense = license is not null && OpenLicenses.Any(x => license.Contains(x, StringComparison.OrdinalIgnoreCase)),
            HasDocumentation = repositories.Any(x => x!.HasReadme == true) || discoveries.Any(x => x.RawContent.Length > 500),
            LastActivityAt = lastActivity,
            Sectors = SectorDictionary.Detect(sectorText),
            Now = DateTime.UtcNow
        };

        var overrides = await db.CriterionOverrides
            .AsNoTracking()
            .Where(x => x.InnovationProjectId == projectId && x.IsActive)
            .ToListAsync(cancellationToken);
        var overrideMap = overrides
            .GroupBy(x => x.Criterion)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(item => item.CreatedAt).First());
        var weights = options.Value.Assessment.EffectiveWeights();
        var automatic = AssessmentScorer.Score(input, weights);
        var outcome = overrideMap.Count == 0
            ? automatic
            : AssessmentScorer.Score(input, weights, overrideMap.ToDictionary(x => x.Key, x => x.Value.Score));

        return new AssessmentContext(
            project,
            input,
            automatic,
            outcome,
            RecommendationEngine.Recommend(input, outcome),
            overrideMap,
            similarInstitutions,
            notes);
    }

    private async Task<IReadOnlyList<SimilarInstitutionItem>> SimilarInstitutionsAsync(
        IReadOnlyList<SimilarProjectItem> similar,
        string? ownInstitution,
        CancellationToken cancellationToken)
    {
        if (similar.Count == 0)
        {
            return Array.Empty<SimilarInstitutionItem>();
        }

        var ids = similar.Select(x => x.ProjectId).ToList();
        var projects = await db.InnovationProjects
            .AsNoTracking()
            .Include(x => x.DiscoveryLinks).ThenInclude(x => x.Discovery).ThenInclude(x => x.Source)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var result = new List<SimilarInstitutionItem>();
        foreach (var item in similar.OrderByDescending(x => x.Similarity))
        {
            var project = projects.FirstOrDefault(x => x.Id == item.ProjectId);
            var institution = project is null ? null : InstitutionOf(project);
            if (string.IsNullOrWhiteSpace(institution) ||
                string.Equals(institution, ownInstitution, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(new SimilarInstitutionItem
            {
                Institution = institution,
                ProjectId = item.ProjectId,
                ProjectTitle = item.Title,
                Similarity = item.Similarity
            });
        }

        return result;
    }

    /// <summary>
    /// Instytucja projektu: z profilu, a w przeciwnym razie z organizacji podanej w materiałach
    /// spoza repozytoriów kodu (tam „organizacją” bywa nazwa serwisu, np. GitHub).
    /// </summary>
    private static string? InstitutionOf(InnovationProject project)
    {
        if (!string.IsNullOrWhiteSpace(project.Institution))
        {
            return project.Institution.Trim();
        }

        return project.DiscoveryLinks
            .Select(x => x.Discovery)
            .Where(x => x.Source.Radar is RadarKind.Uczelnia or RadarKind.Zgloszenia)
            .Select(x => x.Organization?.Trim())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }

    private static AiFacts LatestAi(IReadOnlyList<Discovery> discoveries)
    {
        var processed = discoveries.Where(x => x.AIProcessed).ToList();
        bool? potential = processed.Count == 0 ? null : processed.Any(x => x.IsPotentialInnovation);
        var confidence = processed.Count == 0
            ? (int?)null
            : processed.Where(x => x.IsPotentialInnovation == potential).Max(x => x.AIConfidence);

        var record = discoveries
            .SelectMany(x => x.AiAnalyses)
            .Where(x => x.IsSuccess)
            .OrderByDescending(x => x.AnalyzedAt)
            .FirstOrDefault();
        string? novelty = null, type = null, problem = null, solution = null;
        if (record is not null)
        {
            try
            {
                using var json = JsonDocument.Parse(record.ResultJson);
                novelty = Text(json.RootElement, "noveltyDescription");
                type = Text(json.RootElement, "innovationType");
                problem = Text(json.RootElement, "problem");
                solution = Text(json.RootElement, "solution");
            }
            catch (JsonException)
            {
                // Uszkodzony zapis historii AI nie blokuje oceny.
            }
        }

        return new AiFacts(potential, confidence, novelty, type, problem, solution);

        static string? Text(JsonElement root, string name) =>
            root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()!.Trim()
                : null;
    }

    private static InnovationKind KindFromText(string? text)
    {
        var normalized = SectorDictionary.Normalize(text);
        if (normalized.Length == 0)
        {
            return InnovationKind.Nieokreslona;
        }

        if (normalized.Contains(" spolecz", StringComparison.Ordinal) || normalized.Contains(" social", StringComparison.Ordinal))
        {
            return InnovationKind.Spoleczna;
        }

        if (normalized.Contains(" eduk", StringComparison.Ordinal) || normalized.Contains(" dydakt", StringComparison.Ordinal))
        {
            return InnovationKind.Edukacyjna;
        }

        if (normalized.Contains(" organiz", StringComparison.Ordinal))
        {
            return InnovationKind.Organizacyjna;
        }

        if (normalized.Contains(" proces", StringComparison.Ordinal))
        {
            return InnovationKind.Procesowa;
        }

        return normalized.Contains(" technolog", StringComparison.Ordinal) ||
               normalized.Contains(" produkt", StringComparison.Ordinal) ||
               normalized.Contains(" oprogramow", StringComparison.Ordinal) ||
               normalized.Contains(" urzadz", StringComparison.Ordinal)
            ? InnovationKind.Technologiczna
            : InnovationKind.Nieokreslona;
    }

    private static string ProjectText(AssessmentInput input) =>
        string.Join(' ', input.Title, input.Description, input.Problem, input.Solution, string.Join(' ', input.Sectors));

    private static string Hash(AssessmentContext context)
    {
        var payload = JsonSerializer.Serialize(new
        {
            context.Outcome.TotalScore,
            context.Outcome.Confidence,
            Criteria = context.Outcome.Criteria.Select(x => new { x.Criterion, x.Score, x.Weight }),
            Signals = context.Automatic.Criteria.Select(x => x.Signals),
            Paths = context.Recommendations.Select(x => new { x.Path, x.Rank, x.Reasons }),
            Overrides = context.Overrides.Values.Select(x => new { x.Criterion, x.Score, x.Comment })
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static IEnumerable<string> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? FirstText(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) &&
                                   !x.Trim().StartsWith("brak danych", StringComparison.OrdinalIgnoreCase))?.Trim();

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static string Limit(string value, int maxLength) =>
        value.Length > maxLength ? value[..maxLength] : value;

    private sealed record AiFacts(
        bool? Potential,
        int? Confidence,
        string? Novelty,
        string? InnovationType,
        string? Problem,
        string? Solution);

    private sealed record AssessmentContext(
        InnovationProject Project,
        AssessmentInput Input,
        AssessmentOutcome Automatic,
        AssessmentOutcome Outcome,
        IReadOnlyList<RecommendationResult> Recommendations,
        IReadOnlyDictionary<AssessmentCriterion, CriterionOverride> Overrides,
        IReadOnlyList<SimilarInstitutionItem> SimilarInstitutions,
        IReadOnlyList<string> InferredNotes);
}
