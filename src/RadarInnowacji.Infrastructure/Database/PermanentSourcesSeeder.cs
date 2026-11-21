using Microsoft.EntityFrameworkCore;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Infrastructure.Database;

/// <summary>Opis jednego stałego źródła instalowanego wraz z aplikacją.</summary>
public sealed record PermanentSourceDefinition(
    string Name,
    string Url,
    RadarKind Radar,
    SourceType SourceType,
    int ScanIntervalMinutes,
    string Configuration = "{}");

/// <summary>
/// Dodaje do pustej bazy rzeczywiste źródła: wyszukiwanie Kickstartera oraz strony,
/// na których uczelnie publikują projekty studentów. Źródła nie są oznaczone jako DEMO
/// i nie są nadpisywane, jeśli użytkownik już je zmienił.
/// </summary>
public sealed class PermanentSourcesSeeder(RadarDbContext db) : IPermanentSourcesSeeder
{
    private const string KickstarterSearchBaseUrl = "https://www.kickstarter.com";
    private const int TechnologyCategoryId = 16;

    public static IReadOnlyList<PermanentSourceDefinition> Definitions { get; } =
    [
        new(
            "Kickstarter – wyszukiwanie projektów",
            KickstarterSearchBaseUrl,
            RadarKind.Crowdfunding,
            SourceType.KickstarterApi,
            180,
            SourceConfiguration.Create(null, null, new KickstarterQuery
            {
                Keywords = KickstarterQuery.DefaultKeywords,
                CategoryIds = [TechnologyCategoryId],
                State = KickstarterQuery.DefaultState,
                Sort = KickstarterQuery.DefaultSort,
                LaunchedWithinDays = KickstarterQuery.DefaultLaunchedWithinDays
            })),
        new(
            "AGH – projekty studenckie",
            "https://www.agh.edu.pl/slowa-kluczowe/projekty-studenckie",
            RadarKind.Uczelnia,
            SourceType.HtmlPage,
            720),
        new(
            "Politechnika Gdańska – Uczelnie Przyszłości",
            "https://pg.edu.pl/studenci/projekty/uczelnie-przyszlosci/aktualnosci",
            RadarKind.Uczelnia,
            SourceType.HtmlPage,
            720),
        new(
            "UAM WMiI – studenckie projekty badawczo-rozwojowe",
            "https://wmi.amu.edu.pl/wydzial/projekty/studenckie-projekty-badawczo-rozwojowe",
            RadarKind.Uczelnia,
            SourceType.HtmlPage,
            720)
    ];

    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        var names = Definitions.Select(x => x.Name).ToList();
        var existingNames = await db.Sources
            .Where(x => names.Contains(x.Name))
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);
        var existing = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var definition in Definitions)
        {
            if (existing.Contains(definition.Name))
            {
                continue;
            }

            db.Sources.Add(new Source
            {
                Name = definition.Name,
                Url = definition.Url,
                Radar = definition.Radar,
                SourceType = definition.SourceType,
                FetchMethod = SourceDefaults.FetchMethodFor(definition.SourceType),
                IsActive = true,
                IsDemo = false,
                ScanIntervalMinutes = definition.ScanIntervalMinutes,
                Configuration = definition.Configuration
            });
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return added;
    }
}
