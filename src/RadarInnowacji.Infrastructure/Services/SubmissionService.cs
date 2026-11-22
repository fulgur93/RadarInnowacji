using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>
/// Zgłoszenia innowacji przez instytucje. Zgłoszenie staje się zwykłym odkryciem w źródle
/// „Zgłoszenia instytucji” i przechodzi tę samą drogę co materiały wykryte automatycznie
/// (analiza AI, kategorie, wykrywanie duplikatów), a dodatkowo od razu otrzymuje kartę oceny.
/// </summary>
public sealed class SubmissionService(
    RadarDbContext db,
    IProjectService projectService,
    IAssessmentService assessmentService,
    ILogger<SubmissionService> logger) : ISubmissionService
{
    public const string SourceName = "Zgłoszenia instytucji";
    /// <summary>Adres wewnętrzny – zgłoszenia nie mają zewnętrznego źródła do skanowania.</summary>
    public const string SourceUrl = "/zglos";
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(15);
    private const int RateLimitMaxSubmissions = 5;
    private readonly object _rateLimitLock = new();
    private readonly Dictionary<string, List<DateTime>> _submissionAttempts = new(StringComparer.Ordinal);

    /// <summary>
    /// Przykładowe zgłoszenie (przypadek demonstracyjny 1 z docs/DEMO.md). Instytucja i twórcy
    /// są oznaczeni jako DEMO – to nie jest rzeczywiste zgłoszenie.
    /// </summary>
    public static InnovationSubmission DemoExample() => new()
    {
        Title = "Interaktywne symulacje z fizyki prowadzone przez uczniów",
        Description = "Uczniowie klas starszych przygotowują i prowadzą dla młodszych klas lekcje z interaktywnymi " +
                      "symulacjami z fizyki (mechanika, optyka) uruchamianymi w przeglądarce. Nauczyciel koordynuje " +
                      "harmonogram, a materiały do lekcji są udostępniane innym szkołom.",
        Problem = "Szkoła nie ma wyposażonej pracowni fizycznej, a uczniowie rzadko wykonują doświadczenia samodzielnie.",
        Solution = "Symulacje w przeglądarce i lekcje prowadzone przez starszych uczniów zastępują brakujące doświadczenia.",
        Kind = InnovationKind.Edukacyjna,
        Institution = "Zespół Szkół – przykład (DEMO)",
        InstitutionType = InstitutionType.Szkola,
        Region = "podkarpackie",
        Stage = ProjectStatus.Deployed,
        Creators = "Nauczyciel fizyki i koło naukowe (DEMO)"
    };

    public async Task<IReadOnlyList<SimilarProjectItem>> FindSimilarAsync(
        string title,
        string description,
        CancellationToken cancellationToken = default) =>
        await projectService.FindSimilarToTextAsync(title ?? string.Empty, description ?? string.Empty, 5, cancellationToken);

    public async Task<SubmissionResult> SubmitAsync(
        InnovationSubmission submission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        if (!string.IsNullOrWhiteSpace(submission.Website))
        {
            logger.LogWarning("Odrzucono zgłoszenie: wypełnione pole honeypot.");
            return new SubmissionResult { Success = false, Errors = ["Nie udało się wysłać zgłoszenia. Spróbuj ponownie."] };
        }

        var rateLimitError = CheckRateLimit(submission.Institution, submission.ContactEmail);
        if (rateLimitError is not null)
        {
            return new SubmissionResult { Success = false, Errors = [rateLimitError] };
        }

        var errors = Validate(submission);
        if (errors.Count > 0)
        {
            return new SubmissionResult { Success = false, Errors = errors };
        }

        var source = await EnsureSourceAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var title = submission.Title.Trim();
        var description = submission.Description.Trim();
        var link = string.IsNullOrWhiteSpace(submission.Link) ? null : submission.Link.Trim();
        var content = BuildContent(submission);
        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{title}\n{description}\n{submission.Institution.Trim()}")));
        var existing = await db.DiscoveryProjects
            .Where(x => x.Discovery.SourceId == source.Id && x.Discovery.ContentHash == contentHash)
            .Select(x => (int?)x.InnovationProjectId)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return new SubmissionResult
            {
                Success = false,
                ProjectId = existing,
                Errors = ["To zgłoszenie zostało już przyjęte – otwórz istniejący projekt."]
            };
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var discovery = new Discovery
        {
            Title = title,
            Description = description,
            RawContent = content,
            ContentLength = content.Length,
            Source = source,
            // Bez linku materiał wskazuje stronę projektu w aplikacji (uzupełniane po zapisie).
            SourceUrl = link ?? SourceUrl,
            ExternalId = $"zgloszenie-{Guid.NewGuid():N}",
            ContentHash = contentHash,
            PublishedAt = now,
            DiscoveredAt = now,
            CreatedAt = now,
            LastActivityAt = now,
            Author = Clean(submission.Creators, 1000),
            Organization = submission.Institution.Trim(),
            ProcessingStatus = ProcessingStatus.Pending,
            Status = DiscoveryStatus.Candidate,
            IsDemo = false
        };
        var project = new InnovationProject
        {
            Title = title,
            Description = description,
            Problem = Clean(submission.Problem, ContentLimits.MaxDescriptionLength),
            ProposedSolution = Clean(submission.Solution, ContentLimits.MaxDescriptionLength),
            ProjectStatus = submission.Stage,
            FirstDiscoveredAt = now,
            LastActivityAt = now,
            CreatedAt = now,
            Institution = submission.Institution.Trim(),
            InstitutionType = submission.InstitutionType,
            InnovationKind = submission.Kind,
            Region = Clean(submission.Region, 100),
            Creators = Clean(submission.Creators, 1000),
            ContactConsent = submission.ContactConsent && !string.IsNullOrWhiteSpace(submission.ContactEmail),
            ContactEmail = submission.ContactConsent ? Clean(submission.ContactEmail, 254) : null,
            PipelineStage = PipelineStage.Zgloszona,
            ProfileUpdatedAt = now
        };
        project.FreshnessScore = FreshnessCalculator.Calculate(project, now);
        db.Discoveries.Add(discovery);
        db.InnovationProjects.Add(project);
        db.DiscoveryProjects.Add(new DiscoveryProject { Discovery = discovery, Project = project, Similarity = 1 });
        db.ProjectPipelineEvents.Add(new ProjectPipelineEvent
        {
            Project = project,
            FromStage = PipelineStage.Zgloszona,
            ToStage = PipelineStage.Zgloszona,
            Comment = $"Zgłoszenie przyjęte od: {submission.Institution.Trim()}",
            Actor = "Formularz zgłoszeń",
            CreatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
        if (link is null)
        {
            discovery.SourceUrl = $"/projekty/{project.Id}";
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Przyjęto zgłoszenie innowacji {ProjectId}: {Title}", project.Id, title);
        await assessmentService.AssessAsync(project.Id, cancellationToken);
        return new SubmissionResult { Success = true, ProjectId = project.Id };
    }

    internal static List<string> Validate(InnovationSubmission submission)
    {
        var errors = new List<string>();
        var title = submission.Title?.Trim() ?? string.Empty;
        var description = submission.Description?.Trim() ?? string.Empty;
        if (title.Length is < 5 or > 300)
        {
            errors.Add("Tytuł musi mieć od 5 do 300 znaków.");
        }

        if (description.Length < 30)
        {
            errors.Add("Opis musi mieć co najmniej 30 znaków – napisz, co to jest i jak działa.");
        }
        else if (description.Length > ContentLimits.MaxDescriptionLength)
        {
            errors.Add($"Opis może mieć najwyżej {ContentLimits.MaxDescriptionLength} znaków.");
        }

        var institution = submission.Institution?.Trim() ?? string.Empty;
        if (institution.Length is < 2 or > ContentLimits.MaxTitleLength)
        {
            errors.Add("Podaj nazwę instytucji, w której powstała innowacja.");
        }

        if (submission.InstitutionType == InstitutionType.Nieznany)
        {
            errors.Add("Wybierz typ instytucji.");
        }

        if (submission.Kind == InnovationKind.Nieokreslona)
        {
            errors.Add("Wybierz rodzaj innowacji.");
        }

        if (submission.Stage == ProjectStatus.Unknown)
        {
            errors.Add("Wybierz etap rozwoju.");
        }

        if (!string.IsNullOrWhiteSpace(submission.Link))
        {
            var link = submission.Link.Trim();
            if (link.Length > ContentLimits.MaxSourceUrlLength ||
                !Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                errors.Add("Link musi być pełnym adresem http lub https.");
            }
        }

        errors.AddRange(AssessmentService.ValidateProfile(new ProjectProfile
        {
            Region = submission.Region,
            Creators = submission.Creators,
            Problem = submission.Problem,
            Solution = submission.Solution,
            ContactEmail = submission.ContactEmail,
            ContactConsent = submission.ContactConsent
        }));
        return errors;
    }

    private async Task<Source> EnsureSourceAsync(CancellationToken cancellationToken)
    {
        var source = await db.Sources.FirstOrDefaultAsync(x => x.Name == SourceName, cancellationToken);
        if (source is not null)
        {
            return source;
        }

        source = new Source
        {
            Name = SourceName,
            Url = SourceUrl,
            Radar = RadarKind.Zgloszenia,
            SourceType = SourceType.Manual,
            FetchMethod = SourceDefaults.FetchMethodFor(SourceType.Manual),
            IsActive = true,
            IsDemo = false,
            ScanIntervalMinutes = 10080,
            Configuration = "{}"
        };
        db.Sources.Add(source);
        await db.SaveChangesAsync(cancellationToken);
        return source;
    }

    private static string BuildContent(InnovationSubmission submission)
    {
        var builder = new StringBuilder();
        builder.AppendLine(submission.Description.Trim());
        Append(builder, "Problem", submission.Problem);
        Append(builder, "Rozwiązanie", submission.Solution);
        Append(builder, "Instytucja", submission.Institution);
        Append(builder, "Region", submission.Region);
        Append(builder, "Twórcy", submission.Creators);
        var content = builder.ToString().Trim();
        return content.Length > ContentLimits.MaxRawContentLength ? content[..ContentLimits.MaxRawContentLength] : content;

        static void Append(StringBuilder builder, string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                builder.AppendLine().Append(label).Append(": ").Append(value.Trim());
            }
        }
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private string? CheckRateLimit(string? institution, string? email)
    {
        var key = BuildRateLimitKey(institution, email);
        var now = DateTime.UtcNow;
        lock (_rateLimitLock)
        {
            if (!_submissionAttempts.TryGetValue(key, out var attempts))
            {
                attempts = [];
                _submissionAttempts[key] = attempts;
            }

            attempts.RemoveAll(x => now - x > RateLimitWindow);
            if (attempts.Count >= RateLimitMaxSubmissions)
            {
                return "Zbyt wiele zgłoszeń w krótkim czasie. Spróbuj ponownie później.";
            }

            attempts.Add(now);

            // Proste sprzątanie starych kluczy, żeby słownik nie rósł bez końca.
            if (_submissionAttempts.Count > 200)
            {
                var stale = _submissionAttempts.Where(x => x.Value.Count == 0).Select(x => x.Key).ToList();
                foreach (var staleKey in stale)
                {
                    _submissionAttempts.Remove(staleKey);
                }
            }
        }

        return null;
    }

    private static string BuildRateLimitKey(string? institution, string? email)
    {
        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
        if (normalizedEmail.Length > 0)
        {
            return "email:" + normalizedEmail;
        }

        var normalizedInstitution = string.IsNullOrWhiteSpace(institution) ? "unknown" : institution.Trim().ToLowerInvariant();
        return "inst:" + normalizedInstitution;
    }
}
