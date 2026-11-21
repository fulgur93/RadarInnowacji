using System.Text.Json;
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
        CancellationToken cancellationToken = default)
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
            Configuration = string.IsNullOrWhiteSpace(token)
                ? "{}"
                : JsonSerializer.Serialize(new Dictionary<string, string> { ["token"] = token.Trim() })
        };

        db.Sources.Add(source);
        await db.SaveChangesAsync(cancellationToken);
        return source;
    }

    public async Task UpdateAsync(int id, bool isActive, int scanIntervalMinutes, CancellationToken cancellationToken = default)
    {
        var source = await db.Sources.FindAsync([id], cancellationToken);
        if (source is null)
        {
            return;
        }

        source.IsActive = isActive;
        source.ScanIntervalMinutes = Math.Clamp(scanIntervalMinutes, 5, 10080);
        await db.SaveChangesAsync(cancellationToken);
    }
}
