using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Assessment;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Infrastructure.Services;

/// <summary>
/// Proces po zidentyfikowaniu innowacji: etapy z historią decyzji, skierowania do odbiorców,
/// lejek innowacji oraz eksport do CSV/JSON (punkt integracji z innymi systemami).
/// </summary>
public sealed class PipelineService(RadarDbContext db) : IPipelineService
{
    public async Task<OperationResult> ChangeStageAsync(
        int projectId,
        PipelineStage stage,
        string comment,
        CancellationToken cancellationToken = default)
    {
        var project = await db.InnovationProjects.FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        if (project is null)
        {
            return OperationResult.Fail("Nie znaleziono projektu.");
        }

        if (!PipelineRules.CanMove(project.PipelineStage, stage))
        {
            return OperationResult.Fail(
                $"Nie można przejść z etapu „{PipelineRules.Name(project.PipelineStage)}” do „{PipelineRules.Name(stage)}”.");
        }

        var trimmed = comment?.Trim() ?? string.Empty;
        if (stage is PipelineStage.Odrzucona or PipelineStage.Odlozona && trimmed.Length < 5)
        {
            return OperationResult.Fail("Podaj uzasadnienie odrzucenia lub odłożenia (co najmniej 5 znaków).");
        }

        AddEvent(project, stage, trimmed, "Użytkownik");
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok($"Etap zmieniony na „{PipelineRules.Name(stage)}”.");
    }

    public async Task<OperationResult> ReferAsync(
        int projectId,
        int recipientId,
        string note,
        CancellationToken cancellationToken = default)
    {
        var project = await db.InnovationProjects.FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        var recipient = await db.Recipients.FirstOrDefaultAsync(x => x.Id == recipientId, cancellationToken);
        if (project is null || recipient is null)
        {
            return OperationResult.Fail("Nie znaleziono projektu lub odbiorcy.");
        }

        if (project.PipelineStage is PipelineStage.Odrzucona or PipelineStage.Wdrozona)
        {
            return OperationResult.Fail($"Projekt jest na etapie „{PipelineRules.Name(project.PipelineStage)}” – nie można go skierować.");
        }

        var trimmed = note?.Trim() ?? string.Empty;
        db.ProjectReferrals.Add(new ProjectReferral
        {
            InnovationProjectId = project.Id,
            RecipientId = recipient.Id,
            Note = trimmed.Length > ContentLimits.MaxErrorLength ? trimmed[..ContentLimits.MaxErrorLength] : trimmed,
            Actor = "Użytkownik",
            CreatedAt = DateTime.UtcNow
        });

        // Skierowanie z etapu „Rekomendowana” przesuwa projekt dalej; na innych etapach jest tylko odnotowane.
        var moved = PipelineRules.CanMove(project.PipelineStage, PipelineStage.Skierowana);
        if (moved)
        {
            AddEvent(project, PipelineStage.Skierowana, $"Skierowano do: {recipient.Name}. {trimmed}".Trim(), "Użytkownik");
        }

        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok(moved
            ? $"Skierowano do „{recipient.Name}” – etap: {PipelineRules.Name(PipelineStage.Skierowana)}."
            : $"Odnotowano skierowanie do „{recipient.Name}”. Etap projektu nie zmienił się " +
              $"(„{PipelineRules.Name(project.PipelineStage)}”) – przyjmij najpierw rekomendację.");
    }

    public async Task<FunnelModel> GetFunnelAsync(FunnelFilter filter, CancellationToken cancellationToken = default)
    {
        var all = await LoadItemsAsync(cancellationToken);
        var referrals = await db.ProjectReferrals.CountAsync(cancellationToken);
        var filtered = all.AsEnumerable();
        if (filter.Stage is not null)
        {
            filtered = filtered.Where(x => x.PipelineStage == filter.Stage);
        }

        if (filter.Path is not null)
        {
            filtered = filtered.Where(x => x.Paths.Contains(filter.Path.Value));
        }

        if (filter.Kind is not null)
        {
            filtered = filtered.Where(x => x.Kind == filter.Kind);
        }

        if (filter.InstitutionType is not null)
        {
            filtered = filtered.Where(x => x.InstitutionType == filter.InstitutionType);
        }

        if (filter.MinScore is not null)
        {
            filtered = filtered.Where(x => x.TotalScore >= filter.MinScore);
        }

        if (filter.OnlyAssessed)
        {
            filtered = filtered.Where(x => x.TotalScore is not null);
        }

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var text = filter.Text.Trim();
            filtered = filtered.Where(x =>
                x.Title.Contains(text, StringComparison.CurrentCultureIgnoreCase) ||
                (x.Institution?.Contains(text, StringComparison.CurrentCultureIgnoreCase) ?? false));
        }

        return new FunnelModel
        {
            StageCounts = Enum.GetValues<PipelineStage>()
                .ToDictionary(stage => stage, stage => all.Count(x => x.PipelineStage == stage)),
            PathCounts = Enum.GetValues<DevelopmentPath>()
                .ToDictionary(path => path, path => all.Count(x => x.Paths.FirstOrDefault() == path && x.Paths.Count > 0)),
            TotalProjects = all.Count,
            AssessedProjects = all.Count(x => x.TotalScore is not null),
            Referrals = referrals,
            Items = filtered
                .OrderByDescending(x => x.TotalScore ?? -1)
                .ThenByDescending(x => x.FirstDiscoveredAt)
                .Take(Math.Clamp(filter.Limit, 1, 2000))
                .ToList()
        };
    }

    public async Task<IReadOnlyList<FunnelItem>> ExportItemsAsync(CancellationToken cancellationToken = default) =>
        (await LoadItemsAsync(cancellationToken))
            .OrderByDescending(x => x.TotalScore ?? -1)
            .ToList();

    public async Task<string> ExportCsvAsync(CancellationToken cancellationToken = default)
    {
        var items = await ExportItemsAsync(cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine("Id;Tytuł;Instytucja;Typ instytucji;Rodzaj innowacji;Etap rozwoju;Etap procesu;Wynik;Pewność;Ścieżki;Liczba źródeł;Pierwsze wykrycie;Dane DEMO");
        foreach (var item in items)
        {
            builder.AppendJoin(';',
                    item.ProjectId.ToString(CultureInfo.InvariantCulture),
                    Csv(item.Title),
                    Csv(item.Institution),
                    item.InstitutionType,
                    item.Kind,
                    item.Stage,
                    item.PipelineStage,
                    item.TotalScore?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty,
                    item.Confidence?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    Csv(string.Join(", ", item.Paths.Select(path => DevelopmentPathCatalog.Get(path).Name))),
                    item.SourceCount.ToString(CultureInfo.InvariantCulture),
                    item.FirstDiscoveredAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    item.IsDemo ? "tak" : "nie")
                .AppendLine();
        }

        return builder.ToString();
    }

    private async Task<List<FunnelItem>> LoadItemsAsync(CancellationToken cancellationToken)
    {
        var rows = await db.InnovationProjects
            .AsNoTracking()
            .Where(x => x.MergedIntoProjectId == null)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Institution,
                x.InstitutionType,
                x.InnovationKind,
                x.ProjectStatus,
                x.PipelineStage,
                x.IsDemo,
                x.FirstDiscoveredAt,
                SourceCount = x.DiscoveryLinks.Select(link => link.Discovery.SourceId).Distinct().Count(),
                Organization = x.DiscoveryLinks
                    .Where(link => link.Discovery.Source.Radar == RadarKind.Uczelnia ||
                                   link.Discovery.Source.Radar == RadarKind.Zgloszenia)
                    .Select(link => link.Discovery.Organization)
                    .FirstOrDefault(),
                Assessment = x.Assessments
                    .Where(a => a.IsCurrent)
                    .Select(a => new
                    {
                        a.TotalScore,
                        a.Confidence,
                        Paths = a.Recommendations.OrderBy(r => r.Rank).Select(r => r.Path).ToList()
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new FunnelItem
        {
            ProjectId = x.Id,
            Title = x.Title,
            Institution = x.Institution ?? x.Organization,
            InstitutionType = x.InstitutionType,
            Kind = x.InnovationKind,
            Stage = x.ProjectStatus,
            PipelineStage = x.PipelineStage,
            TotalScore = x.Assessment?.TotalScore,
            Confidence = x.Assessment?.Confidence,
            Paths = x.Assessment?.Paths ?? [],
            SourceCount = x.SourceCount,
            IsDemo = x.IsDemo,
            FirstDiscoveredAt = x.FirstDiscoveredAt
        }).ToList();
    }

    private void AddEvent(InnovationProject project, PipelineStage stage, string comment, string actor)
    {
        db.ProjectPipelineEvents.Add(new ProjectPipelineEvent
        {
            InnovationProjectId = project.Id,
            FromStage = project.PipelineStage,
            ToStage = stage,
            Comment = comment.Length > ContentLimits.MaxErrorLength ? comment[..ContentLimits.MaxErrorLength] : comment,
            Actor = actor,
            CreatedAt = DateTime.UtcNow
        });
        project.PipelineStage = stage;
    }

    /// <summary>Pole CSV z ochroną przed wstrzykiwaniem formuł w arkuszu kalkulacyjnym.</summary>
    public static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var text = value.Replace("\r", " ").Replace("\n", " ");
        if (text[0] is '=' or '+' or '-' or '@' or '\t')
        {
            text = "'" + text;
        }

        return text.Contains(';') || text.Contains('"')
            ? "\"" + text.Replace("\"", "\"\"") + "\""
            : text;
    }
}
