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
/// Dodaje do pustej bazy rzeczywiste, polskojęzyczne źródła: uczelnie, instytucje
/// publiczne, portale naukowe i wsparcia innowacji. Źródła nie są oznaczone jako DEMO
/// i nie są nadpisywane, jeśli użytkownik już je zmienił.
/// </summary>
public sealed class PermanentSourcesSeeder(RadarDbContext db) : IPermanentSourcesSeeder
{
    public static IReadOnlyList<PermanentSourceDefinition> Definitions { get; } =
    [
        // Uczelnie – strony transferu wiedzy / innowacji / aktualności
        new("Politechnika Warszawska – aktualności", "https://www.pw.edu.pl/aktualnosci", RadarKind.Uczelnia, SourceType.HtmlPage, 720),
        new("AGH – projekty studenckie", "https://www.agh.edu.pl/slowa-kluczowe/projekty-studenckie", RadarKind.Uczelnia, SourceType.HtmlPage, 720),
        new("Politechnika Gdańska – Uczelnie Przyszłości", "https://pg.edu.pl/studenci/projekty/uczelnie-przyszlosci/aktualnosci", RadarKind.Uczelnia, SourceType.HtmlPage, 720),
        new("Politechnika Wrocławska – aktualności", "https://pwr.edu.pl/uczelnia/aktualnosci", RadarKind.Uczelnia, SourceType.HtmlPage, 720),
        new("Politechnika Krakowska – aktualności", "https://www.pk.edu.pl/index.php?option=com_content&view=category&layout=blog&id=2&Itemid=101&lang=pl", RadarKind.Uczelnia, SourceType.HtmlPage, 720),
        new("Uniwersytet Jagielloński – nauka i innowacje", "https://www.uj.edu.pl/wiadomosci", RadarKind.Uczelnia, SourceType.HtmlPage, 720),
        new("UAM WMiI – projekty badawczo-rozwojowe", "https://wmi.amu.edu.pl/wydzial/projekty/studenckie-projekty-badawczo-rozwojowe", RadarKind.Uczelnia, SourceType.HtmlPage, 720),

        // Instytucje publiczne / programy innowacyjne
        new("NCBR – aktualności", "https://www.gov.pl/web/ncbr/aktualnosci", RadarKind.Wiadomosci, SourceType.HtmlPage, 360),
        new("NCN – aktualności", "https://www.ncn.gov.pl/aktualnosci", RadarKind.Wiadomosci, SourceType.HtmlPage, 360),
        new("PARP – aktualności", "https://www.parp.gov.pl/component/content/article/57887", RadarKind.Wiadomosci, SourceType.HtmlPage, 360),
        new("Ministerstwo Nauki i Szkolnictwa Wyższego – aktualności", "https://www.gov.pl/web/nauka/aktualnosci", RadarKind.Wiadomosci, SourceType.HtmlPage, 360),
        new("NAWA – aktualności", "https://nawa.gov.pl/nawa/aktualnosci", RadarKind.Wiadomosci, SourceType.HtmlPage, 720),
        new("Urząd Patentowy RP – aktualności", "https://uprp.gov.pl/pl/o-urzedzie/aktualnosci", RadarKind.Wiadomosci, SourceType.HtmlPage, 720),
        new("PAIH – aktualności", "https://www.paih.gov.pl/aktualnosci", RadarKind.Wiadomosci, SourceType.HtmlPage, 720),
        new("Horyzont Europa – aktualności", "https://www.gov.pl/web/horyzont-europa/aktualnosci", RadarKind.Wiadomosci, SourceType.HtmlPage, 720),

        // Nauka / innowacje w Polsce – portale informacyjne i RSS
        new("Nauka w Polsce (PAP) – RSS", "https://naukawpolsce.pl/rss.xml", RadarKind.Wiadomosci, SourceType.Rss, 360),
        new("Spider's Web – technologie (RSS)", "https://spidersweb.pl/api/post/feed/feed-gn", RadarKind.Wiadomosci, SourceType.Rss, 360),
        new("Nauka w Polsce – aktualności", "https://naukawpolsce.pl/aktualnosci/news", RadarKind.Wiadomosci, SourceType.HtmlPage, 360),

        // Polskie platformy wsparcia/crowdfunding – obserwowane jako strony publiczne (informacyjnie)
        new("PolakPotrafi.pl – projekty", "https://polakpotrafi.pl/projekty", RadarKind.Wiadomosci, SourceType.HtmlPage, 720),
        new("Wesprzyj.to – zbiórki", "https://wesprzyj.to/wyszukaj", RadarKind.Wiadomosci, SourceType.HtmlPage, 720),

        // Repozytoria kodu – publiczne API, filtrowane po słowach kluczowych innowacji
        new("GitHub – innowacje (PL)", "https://api.github.com", RadarKind.Repozytorium, SourceType.GitHubApi, 360,
            """{"query":{"keywords":["prototype","proof of concept","novel method","prototyp","nowa metoda","autonomiczny system"],"topics":[],"languages":[],"createdWithinDays":30}}"""),
        new("GitLab – innowacje (PL)", "https://gitlab.com", RadarKind.Repozytorium, SourceType.GitLabApi, 360,
            """{"query":{"keywords":["prototype","proof of concept","novel method","prototyp","nowa metoda","autonomiczny system"],"topics":[],"languages":[],"createdWithinDays":30}}"""),
        new("Hugging Face – modele/projekty (PL)", "https://huggingface.co/api/models?sort=lastModified&direction=-1&limit=50&search=polish", RadarKind.Repozytorium, SourceType.HuggingFaceApi, 720)
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
