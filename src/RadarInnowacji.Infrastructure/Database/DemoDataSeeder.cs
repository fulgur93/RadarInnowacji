using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Enums;
using RadarInnowacji.Core.Interfaces;

namespace RadarInnowacji.Infrastructure.Database;

/// <summary>
/// Ładuje wyraźnie oznaczone dane przykładowe. Nie są one przedstawiane jako rzeczywiste odkrycia.
/// </summary>
public sealed class DemoDataSeeder(RadarDbContext db) : IDemoDataService
{
    private static readonly int[] AgeDays =
        [2, 5, 12, 20, 35, 60, 95, 150, 220, 3, 8, 18, 40, 75, 110, 180, 260, 400, 1, 14, 28, 55, 85, 130, 210, 330, 500];

    public async Task<bool> HasDemoDataAsync(CancellationToken cancellationToken = default) =>
        await db.Sources.AnyAsync(x => x.IsDemo, cancellationToken);

    public async Task<int> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (await HasDemoDataAsync(cancellationToken))
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        var sources = CreateSources(now);
        var discoveries = CreateDiscoveries(sources, now);
        var categories = CreateCategories(now);
        var projects = CreateProjects(discoveries, categories, now);
        var scans = CreateScans(sources, now);
        var aiRecords = CreateAiAnalyses(discoveries, now);

        db.Sources.AddRange(sources);
        db.Discoveries.AddRange(discoveries);
        db.Categories.AddRange(categories);
        db.InnovationProjects.AddRange(projects);
        db.ScanHistory.AddRange(scans);
        db.AiAnalyses.AddRange(aiRecords);

        await db.SaveChangesAsync(cancellationToken);
        return discoveries.Count;
    }

    public async Task<int> ClearAsync(CancellationToken cancellationToken = default)
    {
        await db.AiAnalyses.Where(x => x.IsDemo).ExecuteDeleteAsync(cancellationToken);
        await db.DiscoveryProjects.Where(x => x.Discovery.IsDemo || x.Project.IsDemo).ExecuteDeleteAsync(cancellationToken);
        await db.ProjectCategories.Where(x => x.Project.IsDemo || x.Category.IsDemo).ExecuteDeleteAsync(cancellationToken);
        await db.Discoveries.Where(x => x.IsDemo).ExecuteDeleteAsync(cancellationToken);
        await db.InnovationProjects.Where(x => x.IsDemo).ExecuteDeleteAsync(cancellationToken);
        await db.Categories.Where(x => x.IsDemo).ExecuteDeleteAsync(cancellationToken);
        await db.ScanHistory.Where(x => x.IsDemo).ExecuteDeleteAsync(cancellationToken);
        return await db.Sources.Where(x => x.IsDemo).ExecuteDeleteAsync(cancellationToken);
    }

    private static List<Source> CreateSources(DateTime now)
    {
        (string Name, RadarKind Radar, SourceType Type, FetchMethod Method, bool IsActive)[] specs =
        [
            ("AGH – Repozytorium (DEMO)", RadarKind.Uczelnia, SourceType.Dspace, FetchMethod.Api, true),
            ("Politechnika Gdańska – Most Wiedzy (DEMO)", RadarKind.Uczelnia, SourceType.OaiPmh, FetchMethod.OaiPmh, true),
            ("UMK – RUMAK (DEMO)", RadarKind.Uczelnia, SourceType.OaiPmh, FetchMethod.OaiPmh, false),
            ("GitHub – projekty studenckie (DEMO)", RadarKind.Repozytorium, SourceType.GitHubApi, FetchMethod.Api, true),
            ("GitLab – prototypy (DEMO)", RadarKind.Repozytorium, SourceType.GitLabApi, FetchMethod.Api, true),
            ("Hugging Face – modele (DEMO)", RadarKind.Repozytorium, SourceType.HuggingFaceApi, FetchMethod.Api, true),
            ("Portal technologiczny (DEMO)", RadarKind.Wiadomosci, SourceType.Rss, FetchMethod.Rss, true),
            ("Aktualności uczelni (DEMO)", RadarKind.Wiadomosci, SourceType.HtmlPage, FetchMethod.Html, true),
            ("Serwis informacji naukowej (DEMO)", RadarKind.Wiadomosci, SourceType.Atom, FetchMethod.Atom, false)
        ];

        return specs.Select((spec, index) => new Source
        {
            Name = spec.Name,
            Url = $"https://example.org/demo/zrodlo/{index + 1}",
            Radar = spec.Radar,
            SourceType = spec.Type,
            FetchMethod = spec.Method,
            IsActive = spec.IsActive,
            IsDemo = true,
            ScanIntervalMinutes = 60 + index * 15,
            LastScanDate = now.AddHours(-(index + 1)),
            LastSuccessfulScan = spec.IsActive ? now.AddHours(-(index + 2)) : now.AddDays(-30),
            LastError = spec.IsActive ? null : "Przykładowy błąd połączenia (dane demonstracyjne).",
            Configuration = "{\"demo\":true}"
        }).ToList();
    }

    private static List<Discovery> CreateDiscoveries(List<Source> sources, DateTime now)
    {
        (string Title, string Description, string Author, string Organization, string[] Technologies)[] specs =
        [
            ("Inteligentna stacja pomiaru jakości powietrza", "Opracowano prototyp stacji mierzącej PM2,5, CO2 i temperaturę z transmisją danych do panelu szkolnego.", "A. Kowalska", "AGH", ["IoT", "Czujniki"]),
            ("Autonomiczny robot inspekcyjny", "Zaprojektowano kołowego robota do inspekcji infrastruktury z nawigacją opartą na wizji komputerowej.", "M. Lewandowski", "Politechnika Gdańska", ["Robotyzacja", "CV"]),
            ("System wspomagania diagnostyki obrazowej", "Nowa metoda analizy obrazów medycznych z wskazaniem podejrzanych obszarów dla lekarza.", "K. Nowicka", "Uniwersytet Jagielloński", ["AI", "Medycyna"]),
            ("Platforma mikroenergetyki lokalnej", "Stworzono prototyp systemu zarządzania mikrosiecią z fotowoltaiką i magazynem energii.", "P. Zieliński", "Politechnika Warszawska", ["Energia", "IoT"]),
            ("Narzędzie do analizy danych badawczych", "Autorski system wizualizacji wyników badań z eksportem do raportów i obsługą metadanych.", "J. Wiśniewska", "UMK", ["Analiza danych", "Web"]),
            ("Interaktywny materiał edukacyjny z fizyki", "Zaprojektowano symulację do nauki podstaw mechaniki przeznaczoną dla szkół średnich.", "T. Mazur", "Uniwersytet Kazimierza Wielkiego", ["Edukacja", "Web"]),
            ("Czujniki zanieczyszczeń dla szkół", "Skonstruowano niskokosztowe czujniki zanieczyszczeń do montażu w klasach.", "E. Dąbrowska", "AGH", ["Czujniki", "Edukacja"]),
            ("Algorytm optymalizacji tras dostaw", "Nowa metoda wyznaczania tras z uwzględnieniem ruchu i ograniczeń czasowych.", "B. Szymański", "Politechnika Warszawska", ["Algorytmy", "Optymalizacja"]),
            ("Prototyp egzoszkieletu rehabilitacyjnego", "Zaprojektowano urządzenie wspomagające ruch kończyn dolnych w rehabilitacji.", "A. Wójcik", "Politechnika Gdańska", ["Robotyzacja", "Medycyna"]),
            ("sensor-air-quality (repozytorium)", "Open Source: oprogramowanie stacji pomiarowej jakości powietrza i dashboard.", "student-agh", "GitHub", ["IoT", "C#", "Czujniki"]),
            ("autonomous-inspector-bot", "Repozytorium z kodem robota inspekcyjnego i zestawem testów symulacyjnych.", "robotics-team", "GitHub", ["Robotyzacja", "Python"]),
            ("med-image-assist", "Model i notebooki do wspomagania analizy obrazów diagnostycznych.", "med-ai-lab", "Hugging Face", ["AI", "Medycyna"]),
            ("microgrid-controller", "Kontroler mikrosieci z algorytmem prognozowania zapotrzebowania na energię.", "energy-hackers", "GitLab", ["Energia", "IoT"]),
            ("research-viz-toolkit", "Biblioteka do tworzenia wykresów i raportów z wyników badań naukowych.", "open-science", "GitHub", ["Analiza danych", "TypeScript"]),
            ("physics-sim-starter", "Interaktywna symulacja mechaniki w przeglądarce, materiały do lekcji.", "edu-dev", "GitHub", ["Edukacja", "Web"]),
            ("low-cost-air-sensors", "Projekt płytek i firmware'u tanich czujników zanieczyszczeń dla klas szkolnych.", "sensors-community", "GitLab", ["Czujniki", "Edukacja"]),
            ("route-optimizer-core", "Implementacja algorytmu optymalizacji tras z testami wydajnościowymi.", "algo-lab", "GitHub", ["Algorytmy", "Go"]),
            ("exoskeleton-firmware", "Oprogramowanie sterownika egzoszkieletu rehabilitacyjnego w wersji demonstracyjnej.", "rehab-robotics", "GitLab", ["Robotyzacja", "Medycyna"]),
            ("Nowa stacja pomiarowa wraca do szkoły", "Artykuł o wdrożeniu uczelnianego prototypu stacji pomiarowej w szkole.", "Redakcja", "Portal technologiczny (DEMO)", ["IoT", "Edukacja"]),
            ("Robot inspekcyjny w testach zakładu energetycznego", "Informacja o testach autonomicznego robota w warunkach przemysłowych.", "Redakcja", "Serwis informacji naukowej (DEMO)", ["Robotyzacja", "Energia"]),
            ("AI w diagnostyce: wyniki badań z uczelni", "Relacja z prezentacji systemu wspomagania diagnostyki obrazowej.", "Dział prasy", "Aktualności uczelni (DEMO)", ["AI", "Medycyna"]),
            ("Mikrosieć w pilotażu lokalnej społeczności", "Wiadomość o projekcie zarządzania lokalną mikrosiecią energetyczną.", "Redakcja", "Portal technologiczny (DEMO)", ["Energia", "IoT"]),
            ("Narzędzie do raportowania badań dostępne publicznie", "Informacja o udostępnieniu narzędzia do wizualizacji wyników badań.", "Redakcja", "Serwis informacji naukowej (DEMO)", ["Analiza danych", "Web"]),
            ("Symulacja fizyki trafia do klas", "Aktualność o wdrożeniu interaktywnej symulacji w szkołach.", "Dział prasy", "Aktualności uczelni (DEMO)", ["Edukacja", "Web"]),
            ("Czujniki gleby monitorują uprawy", "Opis projektu czujników wilgotności gleby opracowanego na uczelni.", "A. Nowak", "AGH", ["Czujniki", "Energia"]),
            ("soil-sense-monitor", "Repozytorium z oprogramowaniem czujników gleby i prostym serwerem danych.", "agri-iot", "Hugging Face", ["IoT", "Czujniki"]),
            ("Rolnicy testują inteligentne czujniki", "Wiadomość o pilotażu systemu monitorowania gleby w gospodarstwach.", "Redakcja", "Portal technologiczny (DEMO)", ["IoT", "Edukacja"])
        ];

        var discoveries = new List<Discovery>(specs.Length);
        for (var i = 0; i < specs.Length; i++)
        {
            var spec = specs[i];
            var number = i + 1;
            var age = AgeDays[i];
            var isAiProcessed = number % 3 == 0;
            var status = number % 11 == 0
                ? DiscoveryStatus.Verified
                : number % 3 == 0 ? DiscoveryStatus.Interesting : DiscoveryStatus.Candidate;

            discoveries.Add(new Discovery
            {
                Title = spec.Title,
                Description = spec.Description,
                RawContent = number <= 5
                    ? $"{spec.Description}\n\nPrzykładowa treść źródłowa (dane demonstracyjne)."
                    : string.Empty,
                ContentLength = number == 1 ? 12340 : number <= 5 ? spec.Description.Length : 0,
                IsContentTruncated = number == 1,
                Source = sources[i % sources.Count],
                SourceUrl = $"https://example.org/demo/material/{number}",
                ExternalId = $"demo-{number:D3}",
                PublishedAt = number % 7 == 0 ? null : now.AddDays(-(age + 2)),
                EarliestKnownDate = number % 3 == 0 ? now.AddDays(-(age + 45)) : null,
                LastActivityAt = number % 9 == 0 ? null : now.AddDays(-Math.Max(0, age - 4)),
                DiscoveredAt = now.AddDays(-age),
                ContentHash = $"demo-hash-{number:D3}",
                Author = spec.Author,
                Organization = spec.Organization,
                Technologies = JsonSerializer.Serialize(spec.Technologies),
                CategoryIds = "[]",
                AIProcessed = isAiProcessed,
                IsPotentialInnovation = isAiProcessed || number % 5 == 0,
                AIConfidence = isAiProcessed ? 55 + (number * 7) % 40 : null,
                AIAnalysis = isAiProcessed
                    ? "Przykładowa analiza: opis wskazuje na nowe rozwiązanie; część szczegółów nie została potwierdzona."
                    : null,
                ProcessingStatus = isAiProcessed
                    ? ProcessingStatus.AiProcessed
                    : number % 3 == 1 ? ProcessingStatus.AiQueued : ProcessingStatus.Pending,
                Status = status,
                IsDemo = true
            });
        }

        return discoveries;
    }

    private static List<Category> CreateCategories(DateTime now)
    {
        (string Name, string Description)[] specs =
        [
            ("Inteligentne monitorowanie środowiska", "Czujniki, stacje pomiarowe i systemy monitoringu środowiska."),
            ("Robotyka i automatyka", "Roboty, urządzenia autonomiczne i systemy sterowania."),
            ("Sztuczna inteligencja", "Modele, algorytmy uczenia maszynowego i aplikacje AI."),
            ("Internet Rzeczy", "Urządzenia połączone, czujniki i platformy IoT."),
            ("Technologie medyczne", "Rozwiązania diagnostyczne, rehabilitacyjne i zdrowotne."),
            ("Edukacja i społeczeństwo", "Narzędzia edukacyjne i rozwiązania społeczne."),
            ("Energia i klimat", "Odnawialne źródła energii, mikrosiecie i efektywność."),
            ("Analiza danych i wizualizacja", "Narzędzia do przetwarzania i prezentacji danych.")
        ];

        return specs.Select((spec, index) => new Category
        {
            Name = spec.Name,
            Description = spec.Description,
            CreatedAt = now.AddDays(-200),
            LastUpdatedAt = now.AddDays(-index),
            IsActive = true,
            IsDemo = true
        }).ToList();
    }

    private static List<InnovationProject> CreateProjects(
        List<Discovery> discoveries,
        List<Category> categories,
        DateTime now)
    {
        (string Title, string Description, int[] CategoryIndexes, int[] DiscoveryNumbers)[] specs =
        [
            ("Inteligentna stacja jakości powietrza", "Prototyp stacji, oprogramowanie i wdrożenie w szkole.", [0, 3], [1, 10, 19]),
            ("Autonomiczny robot inspekcyjny", "Robot, kod źródłowy i testy przemysłowe.", [1, 2], [2, 11, 20]),
            ("System wspomagania diagnostyki obrazowej", "Model, badania i prezentacja wyników.", [4, 2], [3, 12, 21]),
            ("Platforma mikroenergetyki lokalnej", "Kontroler mikrosieci i pilotaż lokalny.", [6, 3], [4, 13, 22]),
            ("Narzędzie do analizy danych badawczych", "Biblioteka, raporty i informacja o udostępnieniu.", [7, 2], [5, 14, 23]),
            ("Interaktywny materiał edukacyjny z fizyki", "Symulacja, repozytorium i wdrożenie w klasach.", [5, 7], [6, 15, 24]),
            ("Czujniki zanieczyszczeń dla szkół", "Niskokosztowe czujniki i ich oprogramowanie.", [0, 5], [7, 16]),
            ("Algorytm optymalizacji tras dostaw", "Nowa metoda i implementacja referencyjna.", [7, 1], [8, 17]),
            ("Prototyp egzoszkieletu rehabilitacyjnego", "Urządzenie i oprogramowanie sterujące.", [1, 4], [9, 18]),
            ("System monitorowania gleby", "Czujniki, repozytorium i pilotaż rolniczy.", [0, 3], [25, 26, 27])
        ];

        return specs.Select((spec, index) =>
        {
            var project = new InnovationProject
            {
                Title = spec.Title,
                Description = spec.Description,
                Problem = "Przykładowy problem opisany w danych demonstracyjnych.",
                ProposedSolution = "Przykładowe rozwiązanie opisane w danych demonstracyjnych.",
                ProjectStatus = (ProjectStatus)(index % 5),
                FirstDiscoveredAt = now.AddDays(-Math.Max(1, AgeDays[spec.DiscoveryNumbers[0] - 1])),
                EarliestKnownDate = now.AddDays(-Math.Max(30, AgeDays[spec.DiscoveryNumbers[0] - 1] + 60)),
                LastActivityAt = now.AddDays(-(index + 1)),
                FreshnessScore = Math.Round(100 - index * 6.5, 1),
                CreatedAt = now.AddDays(-120),
                IsDemo = true
            };

            project.DiscoveryLinks = spec.DiscoveryNumbers
                .Select(number => new DiscoveryProject
                {
                    Discovery = discoveries[number - 1],
                    Project = project,
                    Similarity = 0.8
                })
                .ToList();

            project.CategoryLinks = spec.CategoryIndexes
                .Select(categoryIndex => new ProjectCategory
                {
                    Category = categories[categoryIndex],
                    Project = project
                })
                .ToList();

            return project;
        }).ToList();
    }

    private static List<ScanHistory> CreateScans(List<Source> sources, DateTime now) =>
        sources.Select((source, index) => new ScanHistory
        {
            Source = source,
            StartedAt = now.AddHours(-(index + 1)),
            FinishedAt = now.AddHours(-(index + 1)).AddMinutes(3),
            Status = index == 8 ? ScanStatus.Failed : index == 2 ? ScanStatus.Partial : ScanStatus.Succeeded,
            ItemsFound = (index + 1) * 3,
            ItemsNew = (index + 1),
            ErrorMessage = index == 8 ? "Przykładowy błąd skanowania (dane demonstracyjne)." : null,
            IsDemo = true
        }).ToList();

    private static List<AiAnalysisRecord> CreateAiAnalyses(List<Discovery> discoveries, DateTime now) =>
        new[] { 3, 6, 9, 12 }.Select(number => new AiAnalysisRecord
        {
            Discovery = discoveries[number - 1],
            Model = "qwen3:1.7b (DEMO)",
            AnalyzedAt = now.AddHours(-number),
            ResultJson = "{\"isPotentialInnovation\":true,\"confidence\":78}",
            Confidence = 78,
            Reason = "Przykładowe uzasadnienie wygenerowane dla danych demonstracyjnych.",
            PromptVersion = "v1-demo",
            IsSuccess = true,
            IsDemo = true
        }).ToList();
}
