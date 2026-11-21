using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Infrastructure.Services;

public sealed class DiscoveryService(RadarDbContext db) : IDiscoveryService
{
    public async Task<DiscoveryPage> GetPageAsync(DiscoveryFilter filter, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(db.Discoveries, filter);
        var totalCount = await query.CountAsync(cancellationToken);

        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await ApplySorting(query, filter)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new DiscoveryPage
        {
            Items = items.Select(MapToListItem).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<DiscoveryDetailsModel?> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        var discovery = await db.Discoveries
            .Include(x => x.Source)
            .Include(x => x.Repository)
            .Include(x => x.ProjectLinks).ThenInclude(x => x.Project).ThenInclude(x => x.CategoryLinks).ThenInclude(x => x.Category)
            .Include(x => x.AiAnalyses)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (discovery is null)
        {
            return null;
        }

        var categoryIds = discovery.ProjectLinks
            .SelectMany(x => x.Project.CategoryLinks)
            .Select(x => x.CategoryId)
            .Distinct()
            .ToList();

        var projectIds = discovery.ProjectLinks.Select(x => x.InnovationProjectId).ToList();
        var related = projectIds.Count == 0
            ? new List<Discovery>()
            : await db.Discoveries
                .Include(x => x.Source)
                .Include(x => x.ProjectLinks).ThenInclude(x => x.Project).ThenInclude(x => x.CategoryLinks).ThenInclude(x => x.Category)
                .Where(x => x.Id != id && x.ProjectLinks.Any(link => projectIds.Contains(link.InnovationProjectId)))
                .OrderBy(x => x.PublishedAt)
                .Take(10)
                .ToListAsync(cancellationToken);

        var similar = categoryIds.Count == 0
            ? new List<Discovery>()
            : await db.Discoveries
                .Include(x => x.Source)
                .Include(x => x.ProjectLinks).ThenInclude(x => x.Project).ThenInclude(x => x.CategoryLinks).ThenInclude(x => x.Category)
                .Where(x => x.Id != id && x.ProjectLinks.Any(link =>
                    link.Project.CategoryLinks.Any(category => categoryIds.Contains(category.CategoryId))))
                .OrderByDescending(x => x.DiscoveredAt)
                .Take(5)
                .ToListAsync(cancellationToken);

        return new DiscoveryDetailsModel
        {
            Discovery = discovery,
            SourceName = discovery.Source.Name,
            Radar = discovery.Source.Radar,
            Categories = discovery.ProjectLinks
                .SelectMany(x => x.Project.CategoryLinks)
                .Select(x => x.Category.Name)
                .Distinct()
                .OrderBy(x => x)
                .ToList(),
            RelatedDiscoveries = related.Select(MapToListItem).ToList(),
            SimilarDiscoveries = similar.Where(x => related.All(r => r.Id != x.Id)).Select(MapToListItem).ToList(),
            AiHistory = discovery.AiAnalyses.OrderByDescending(x => x.AnalyzedAt).ToList()
        };
    }

    public async Task MarkVerifiedAsync(int id, CancellationToken cancellationToken = default)
    {
        var discovery = await db.Discoveries.FindAsync([id], cancellationToken);
        if (discovery is null)
        {
            return;
        }

        discovery.Status = DiscoveryStatus.Verified;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<Discovery> BuildQuery(IQueryable<Discovery> source, DiscoveryFilter filter)
    {
        var query = source
            .Include(x => x.Source)
            .Include(x => x.ProjectLinks).ThenInclude(x => x.Project).ThenInclude(x => x.CategoryLinks).ThenInclude(x => x.Category)
            .AsQueryable();

        if (filter.Radar is not null)
        {
            query = query.Where(x => x.Source.Radar == filter.Radar.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var term = filter.SearchText.Trim().ToLower();
            query = query.Where(x =>
                x.Title.ToLower().Contains(term) ||
                x.Description.ToLower().Contains(term) ||
                x.RawContent.ToLower().Contains(term) ||
                (x.Author != null && x.Author.ToLower().Contains(term)) ||
                (x.Organization != null && x.Organization.ToLower().Contains(term)));
        }

        var dateSelector = GetDateSelector(filter.DateField);
        if (filter.DateFrom is not null)
        {
            var from = Expression.Constant((DateTime?)filter.DateFrom.Value, typeof(DateTime?));
            var body = Expression.GreaterThanOrEqual(dateSelector.Body, from);
            query = query.Where(Expression.Lambda<Func<Discovery, bool>>(body, dateSelector.Parameters));
        }

        if (filter.DateTo is not null)
        {
            var to = Expression.Constant((DateTime?)filter.DateTo.Value, typeof(DateTime?));
            var body = Expression.LessThanOrEqual(dateSelector.Body, to);
            query = query.Where(Expression.Lambda<Func<Discovery, bool>>(body, dateSelector.Parameters));
        }

        if (filter.CategoryId is not null)
        {
            query = query.Where(x => x.ProjectLinks.Any(link =>
                link.Project.CategoryLinks.Any(category => category.CategoryId == filter.CategoryId.Value)));
        }

        if (filter.SourceId is not null)
        {
            query = query.Where(x => x.SourceId == filter.SourceId.Value);
        }

        if (filter.ProcessingStatus is not null)
        {
            query = query.Where(x => x.ProcessingStatus == filter.ProcessingStatus.Value);
        }

        if (filter.Status is not null)
        {
            query = query.Where(x => x.Status == filter.Status.Value);
        }

        if (filter.ExcludeNoise)
        {
            query = query.Where(x => x.Repository == null || x.Repository.NoiseReason == null);
        }

        if (filter.IsPotentialInnovation is not null)
        {
            query = query.Where(x => x.IsPotentialInnovation == filter.IsPotentialInnovation.Value);
        }

        return query;
    }

    private static Expression<Func<Discovery, DateTime?>> GetDateSelector(DateField field) => field switch
    {
        DateField.PublishedAt => x => x.PublishedAt,
        DateField.EarliestKnownDate => x => x.EarliestKnownDate,
        DateField.LastActivityAt => x => x.LastActivityAt,
        _ => x => x.DiscoveredAt
    };

    private static IQueryable<Discovery> ApplySorting(IQueryable<Discovery> query, DiscoveryFilter filter)
    {
        var descending = filter.SortDescending;

        return filter.SortBy switch
        {
            DiscoverySort.Title => descending ? query.OrderByDescending(x => x.Title) : query.OrderBy(x => x.Title),
            DiscoverySort.PublishedAt => descending
                ? query.OrderByDescending(x => x.PublishedAt)
                : query.OrderBy(x => x.PublishedAt),
            DiscoverySort.AIConfidence => descending
                ? query.OrderByDescending(x => x.AIConfidence)
                : query.OrderBy(x => x.AIConfidence),
            DiscoverySort.Source => descending
                ? query.OrderByDescending(x => x.Source.Name)
                : query.OrderBy(x => x.Source.Name),
            _ => descending
                ? query.OrderByDescending(x => x.DiscoveredAt)
                : query.OrderBy(x => x.DiscoveredAt)
        };
    }

    private static DiscoveryListItem MapToListItem(Discovery discovery) => new()
    {
        Id = discovery.Id,
        Title = discovery.Title,
        Description = discovery.Description,
        Radar = discovery.Source.Radar,
        SourceName = discovery.Source.Name,
        SourceUrl = discovery.SourceUrl,
        PublishedAt = discovery.PublishedAt,
        DiscoveredAt = discovery.DiscoveredAt,
        Author = discovery.Author,
        Organization = discovery.Organization,
        Technologies = ParseList(discovery.Technologies),
        Categories = discovery.ProjectLinks
            .SelectMany(x => x.Project.CategoryLinks)
            .Select(x => x.Category.Name)
            .Distinct()
            .OrderBy(x => x)
            .ToList(),
        Status = discovery.Status,
        ProcessingStatus = discovery.ProcessingStatus,
        IsPotentialInnovation = discovery.IsPotentialInnovation,
        AIConfidence = discovery.AIConfidence,
        IsDemo = discovery.IsDemo
    };

    private static IReadOnlyList<string> ParseList(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
