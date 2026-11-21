using Microsoft.EntityFrameworkCore;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;
using RadarInnowacji.Infrastructure.Database;

namespace RadarInnowacji.Infrastructure.Services;

public sealed class SourceService(RadarDbContext db) : ISourceService
{
    public async Task<IReadOnlyList<Source>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.Sources
            .Include(x => x.Discoveries)
            .OrderBy(x => x.Radar).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<Source?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await db.Sources.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Source> CreateAsync(
        string name,
        string url,
        RadarKind radar,
        SourceType sourceType,
        int scanIntervalMinutes,
        bool isActive,
        string? token,
        CancellationToken cancellationToken = default,
        RepositoryQuery? repositoryQuery = null)
    {
        name = name.Trim();
        url = url.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nazwa źródła jest wymagana.", nameof(name));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Adres URL musi być pełnym adresem http lub https.", nameof(url));
        }

        if (!SourceDefaults.SupportedTypes.Contains(sourceType))
        {
            throw new ArgumentException("Wybrany typ źródła nie jest obsługiwany.", nameof(sourceType));
        }

        var query = repositoryQuery is null ? null : ValidateRepositoryQuery(sourceType, repositoryQuery);

        var exists = await db.Sources.AnyAsync(
            x => x.Name.ToLower() == name.ToLower(),
            cancellationToken);
        if (exists)
        {
            throw new ArgumentException($"Źródło o nazwie \"{name}\" już istnieje.", nameof(name));
        }

        var source = new Source
        {
            Name = name[..Math.Min(name.Length, ContentLimits.MaxTitleLength)],
            Url = url[..Math.Min(url.Length, ContentLimits.MaxSourceUrlLength)],
            Radar = radar,
            SourceType = sourceType,
            FetchMethod = SourceDefaults.FetchMethodFor(sourceType),
            IsActive = isActive,
            IsDemo = false,
            ScanIntervalMinutes = Math.Clamp(scanIntervalMinutes, 5, 10080),
            Configuration = SourceConfiguration.Create(token, query)
        };

        db.Sources.Add(source);
        await db.SaveChangesAsync(cancellationToken);
        return source;
    }

    public async Task UpdateAsync(
        int id,
        bool isActive,
        int scanIntervalMinutes,
        CancellationToken cancellationToken = default,
        RepositoryQuery? repositoryQuery = null)
    {
        var source = await db.Sources.FindAsync([id], cancellationToken);
        if (source is null)
        {
            return;
        }

        if (repositoryQuery is not null)
        {
            var query = ValidateRepositoryQuery(source.SourceType, repositoryQuery);
            source.Configuration = SourceConfiguration.WithRepositoryQuery(source.Configuration, query);
        }

        source.IsActive = isActive;
        source.ScanIntervalMinutes = Math.Clamp(scanIntervalMinutes, 5, 10080);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static RepositoryQuery ValidateRepositoryQuery(SourceType sourceType, RepositoryQuery repositoryQuery)
    {
        if (sourceType is not (SourceType.GitHubApi or SourceType.GitLabApi))
        {
            throw new ArgumentException(
                "Zapytanie repozytoriów jest dostępne tylko dla źródeł GitHub i GitLab.",
                nameof(repositoryQuery));
        }

        var query = repositoryQuery.Normalize();
        if (!query.HasSearchTerms)
        {
            throw new ArgumentException(
                "Podaj przynajmniej jedno słowo kluczowe lub temat.",
                nameof(repositoryQuery));
        }

        return query;
    }
}
