# Radar Innowacji

Inteligentny system automatycznego wykrywania potencjalnych innowacji w uczelniach,
repozytoriach kodu źródłowego, wiadomościach internetowych oraz na portalach crowdfundingowych.

**Status projektu: ETAP 2 – ADAPTERY ŹRÓDEŁ I SKANOWANIE W TLE (zaimplementowany, oczekuje na akceptację).**
Etapy 0 i 1 zostały wcześniej zaakceptowane przez zamawiającego.
Etap 3 (radar repozytoriów) jest w toku – dodano w nim moduł wyszukiwania Kickstartera,
który na życzenie zamawiającego został przeniesiony do osobnego, czwartego radaru
portali crowdfundingowych (zmiana wobec pierwotnie trójradarowego interfejsu z `src/MainJob.txt`).

## Stos technologiczny

- ASP.NET Core 10 / C# / Blazor Web App (Interactive Server)
- Entity Framework Core 10 + SQLite (możliwość przejścia na PostgreSQL)
- BackgroundService (zadania cykliczne, od Etapu 0 jako biblioteka `Jobs` uruchamiana w procesie Web)
- HttpClientFactory, AngleSharp, RSS/Atom, OAI-PMH (adaptery źródeł), Ollama (opcjonalnie, od kolejnego etapu)
- Bootstrap 5 (lokalnie w `wwwroot/lib/bootstrap`, bez zależności od CDN)
- Wykresy Razor/SVG bez własnego JavaScript-u

## Struktura rozwiązania

```
RadarInnowacji/
├── RadarInnowacji.sln
├── global.json                       # SDK 10.0.404
├── .config/dotnet-tools.json         # lokalny dotnet-ef 10.0.15
├── src/
│   ├── RadarInnowacji.Core/          # encje, enums, interfejsy, modele (bez zależności)
│   ├── RadarInnowacji.Infrastructure/# EF Core, migracje, serwisy, logowanie, integracje
│   ├── RadarInnowacji.Worker/        # Jobs – BackgroundService w procesie aplikacji
│   └── RadarInnowacji.Web/           # UI Blazor, strony, komponenty, zasoby statyczne
├── tests/
│   ├── RadarInnowacji.Tests/         # testy jednostkowe usług i danych demo
│   └── RadarInnowacji.IntegrationTests/ # testy hosta HTTP i stron
├── docs/
└── installer/
```

Uzasadnienie odstępstwa: `Worker` jest biblioteką klas z implementacjami `BackgroundService`
rejestrowanymi w procesie `Web`. Dzięki temu instalator uruchamia tylko jeden proces,
co upraszcza zamykanie aplikacji i obsługę portu.

## Wymagania wstępne (na tym etapie)

- .NET SDK **10.0.404** (zainstalowany lokalnie w `/home/fulgur/.dotnet`)
- `dotnet` musi być w `PATH`: `export PATH=/home/fulgur/.dotnet:$PATH`
- Przeglądarka internetowa (otwierana automatycznie po starcie)

Nie są wymagane: Visual Studio, Rider, PostgreSQL, Ollama, zewnętrzne chmury.

## Uruchomienie

```bash
export PATH=/home/fulgur/.dotnet:$PATH
dotnet run --project src/RadarInnowacji.Web
```

Aplikacja nasłuchuje wyłącznie na `127.0.0.1`. Port domyślny to `5080`;
jeśli jest zajęty, zostanie użyty pierwszy wolny port z zakresu `5080-5089`.
Adres jest wypisywany w logu i w konsoli. Przeglądarka otwiera się automatycznie.

Migracje EF Core są stosowane przy starcie przez `DatabaseMigrationJob` – użytkownik
nie musi wywoływać `dotnet ef database update`.

## Konfiguracja

`src/RadarInnowacji.Web/appsettings.json`, sekcja `Radar`:

| Ustawienie | Domyślnie | Opis |
|---|---|---|
| `Port` | `5080` | Port lokalny; `0` – port dynamiczny (testy) |
| `OpenBrowser` | `true` | Otwarcie przeglądarki po starcie |
| `SingleInstance` | `true` | Blokada drugiej instancji (plik `radar.lock` z PID) |
| `DatabasePath` | `""` | Ścieżka do SQLite; pusta = katalog danych użytkownika |
| `LogDirectory` | `""` | Katalog logów; pusta = katalog danych użytkownika |
| `Scanning:Enabled` | `true` | Włączenie cyklicznego skanowania w tle |
| `Scanning:IntervalMinutes` | `15` | Częstotliwość uruchamiania cyklu skanowania |
| `Scanning:StartDelaySeconds` | `10` | Opóźnienie pierwszego skanu po starcie |
| `Scanning:MaxItemsPerSource` | `50` | Maksymalna liczba materiałów zapisanych z jednego źródła |
| `Repositories:MaxItemsPerScan` | `300` | Radar repozytoriów: maks. liczba projektów z jednego skanu źródła GitHub/GitLab |
| `Repositories:MaxPagesPerQuery` | `10` | Maks. liczba stron (po 100) na jeden zakres dat (GitHub pozwala na 10) |
| `Repositories:MaxRequestsPerScan` | `60` | Maks. liczba wywołań API w jednym skanie źródła |
| `Repositories:MaxRateLimitWaitSeconds` | `65` | Jak długo czekać na odnowienie limitu API; dłuższy limit kończy skan jako częściowy |
| `Repositories:OverlapHours` | `2` | GitHub: ile ostatnich godzin sprawdzać ponownie (opóźnienie indeksowania) |
| `Repositories:MaxDetailsPerScan` | `30` | Ile projektów na skan uzupełnić o README, języki i licencję (2–3 zapytania na projekt) |
| `Repositories:FilterNoise` | `true` | Pomijanie forków, kopii, szablonów, list „awesome", dotfiles, zadań z kursów i projektów do usunięcia |
| `Kickstarter:MaxItemsPerScan` | `120` | Maksymalna liczba kampanii zapisanych z jednego skanu źródła Kickstarter |
| `Kickstarter:MaxPagesPerQuery` | `8` | Maksymalna liczba stron (po 12 wyników) jednego wyszukiwania Kickstartera |

Zmienne środowiskowe nadpisują ustawienia uruchomieniowe:
`RADAR_PORT`, `RADAR_OPEN_BROWSER`, `RADAR_SINGLE_INSTANCE`,
`RADAR_SCANNING_ENABLED`, `RADAR_SCAN_INTERVAL_MINUTES`,
`RADAR_SCAN_START_DELAY_SECONDS`, `RADAR_SCAN_MAX_ITEMS`.
Ścieżki danych można nadpisać przez `RADAR_DATA_DIR`, `RADAR_DB_PATH`, `RADAR_LOG_DIR`.

Dane użytkownika (SQLite i logi) są zapisywane poza katalogiem aplikacji:
`%LOCALAPPDATA%\RadarInnowacji` na Windows oraz `~/.local/share/RadarInnowacji` na Linuxie.

## Kompilacja i testy

```bash
export PATH=/home/fulgur/.dotnet:$PATH
dotnet tool restore
dotnet build RadarInnowacji.sln
dotnet test RadarInnowacji.sln
```

Wynik weryfikacji Etapu 1:

- `dotnet build RadarInnowacji.sln -v minimal` – **0 ostrzeżeń, 0 błędów**,
- `dotnet test RadarInnowacji.sln --no-build` – **29 testów zaliczonych**
  (17 jednostkowych + 12 integracyjnych),
- migracja `20261121125947_InitialCreate` stosowana automatycznie przy starcie,
- test runtime na `127.0.0.1:15181` z załadowanymi przykładami: wszystkie strony zwróciły
  HTTP 200, daty wyświetlane w formacie `dd.MM.yyyy`/`dd.MM.yyyy HH:mm`, zamknięcie
  z sygnałem SIGTERM zostało zapisane w logu jako `Application is shutting down...`.

Wynik weryfikacji Etapu 2:

- `dotnet build RadarInnowacji.sln -v minimal` – **0 ostrzeżeń, 0 błędów**,
- `dotnet test RadarInnowacji.sln --no-build` – **51 testów zaliczonych**
  (38 jednostkowych + 13 integracyjnych),
- test runtime z lokalnym kanałem RSS (`127.0.0.1:15189`) i aplikacją na `127.0.0.1:15190`:
  skanowanie w tle pobrało 1 rzeczywisty materiał, zapisało wpis `ScanHistory`,
  zaktualizowało `LastScanDate`/`LastSuccessfulScan` i wyświetliło odkrycie bez badge’u `DEMO`,
- daty w interfejsie pozostały w formatach `dd.MM.yyyy` i `dd.MM.yyyy HH:mm`,
- logi nie zawierały `fail:`, `crit:` ani nieobsłużonych wyjątków; zamknięcie SIGTERM
  zostało odnotowane w logu,
- Etap 2 nie wymagał nowej migracji – wykorzystuje istniejące tabele `Sources`,
  `Discoveries` i `ScanHistory`.

## Moduł wyszukiwania Kickstartera i radar portali crowdfundingowych (Etap 3)

- nowy typ źródła `KickstarterApi` (metoda `Api`, radar portali crowdfundingowych) z adapterem korzystającym
  z publicznego wyszukiwania `GET /discover/advanced?format=json` – bez klucza API i bez proxy,
- zapytanie w konfiguracji źródła (`kickstarterQuery`): słowa kluczowe, identyfikatory kategorii
  Kickstartera, stan kampanii (`live`, `upcoming`, `successful`, `failed`, `canceled`), sortowanie (`newest`,
  `end_date`) oraz okno dat rozpoczęcia kampanii (1–365 dni),
- formularz „Nowe źródło" i edycja źródła pokazują pola zapytania wraz z podglądem adresów
  (12 wyników na stronę, wiele stron i wiele wyszukiwań w jednym skanie),
- adapter filtruje wyniki starsze niż okno dat, deduplikuje kampanie w ramach skanu,
  ogranicza liczbę stron i zapisuje kampanie jako odkrycia `Candidate` / „Oczekuje na AI",
- informacje o kategorii, lokalizacji, stanie i finansowaniu trafiają do `RawContent` jako kontekst;
  dane o finansowaniu nie wpływają na wybór projektów (brak sortowania po popularności),
- błąd HTTP 403 jest zapisywany w `LastError` i historii skanowania z komunikatem wskazującym,
  że adapter nie obchodzi ochrony serwisu,
- osobna strona `/radar-crowdfunding` („Radar portali crowdfundingowych") w menu, na dashboardzie
  i we wspólnym widoku odkryć; wybór typu `KickstarterApi` automatycznie ustawia ten radar,
- jedno wyraźnie oznaczone (`DEMO`) odkrycie kampanii crowdfundingowej, dzięki któremu nowy radar
  jest widoczny po załadowaniu danych przykładowych.

Wynik weryfikacji modułu:

- `dotnet build RadarInnowacji.sln -v minimal` – **0 błędów, 0 nowych ostrzeżeń**
  (pozostają 3 wcześniejsze ostrzeżenia `xUnit1031` w `RepositoryDetailsTests`),
- `dotnet test RadarInnowacji.sln --no-build` – **158 testów zaliczonych**
  (142 jednostkowych + 16 integracyjnych, w tym przypadki dla `/radar-crowdfunding`
  i stałych źródeł dostępnych po czystym starcie),
- test runtime na `127.0.0.1:15194` ze świeżym katalogiem danych: strony `/`, `/zrodla`,
  `/radar-uczelni`, `/radar-repozytoriow`, `/radar-wiadomosci`, `/radar-crowdfunding`,
  `/odkrycia` i `/historia` zwróciły HTTP 200; nowy radar jest widoczny w menu
  i na dashboardzie, lista typów źródła zawiera „Kickstarter (wyszukiwanie)", brak wpisów
  `fail:`/`crit:` w logu, zamknięcie z sygnałem SIGTERM zostało odnotowane jako
  `Application is shutting down...`.

Znane ograniczenia modułu Kickstarter:

- Kickstarter nie udostępnia oficjalnego publicznego API – używany jest endpoint, którego używa
  jego własna strona. Z tego środowiska Kickstarter odpowiada HTTP 403 (ochrona Cloudflare),
  więc pobieranie na żywo nie zostało tam zweryfikowane; parser, stronicowanie i obsługa błędu
  403 są pokryte testami jednostkowymi na przykładowych odpowiedziach JSON.
- Adapter nie omija zabezpieczeń ani blokad; aby sprawdzić integrację na żywo, należy uruchomić
  aplikację w sieci, z której endpoint jest dostępny.
- Skan nie zapisuje kursora przyrostowego: ponawia wyszukiwanie pierwszych stron w oknie dat
  (dane idempotentne w ramach źródła dzięki `ExternalId`).
- Lista głównych kategorii w formularzu jest pomocą dla użytkownika; można wpisać dowolny
  identyfikator kategorii Kickstartera.
- Czwarty radar to rozszerzenie interfejsu względem trójradarowego opisu w `src/MainJob.txt`,
  wprowadzone na wyraźne życzenie zamawiającego. Nie wymagało zmiany modelu danych ani nowej
  migracji (`RadarKind` jest zapisywany jako liczba całkowita).

## Stałe, rzeczywiste źródła dostępne po czystej instalacji

Bezpośrednio po uruchomieniu na pustej bazie (jeszcze przed pierwszym skanowaniem) startupowy
`PermanentSourcesSeedJob` – uruchamiany po `DatabaseMigrationJob`, a przed `BackgroundScanJob` –
dodaje cztery aktywne źródła. Nie są one oznaczone jako `DEMO` i nie wymagają importu ani
ręcznej konfiguracji:

| Źródło | Radar | Typ | Adres |
| --- | --- | --- | --- |
| Kickstarter – wyszukiwanie projektów | portale crowdfundingowe | `KickstarterApi` | `https://www.kickstarter.com` |
| AGH – projekty studenckie | uczelnie | `HtmlPage` | `https://www.agh.edu.pl/slowa-kluczowe/projekty-studenckie` |
| Politechnika Gdańska – Uczelnie Przyszłości | uczelnie | `HtmlPage` | `https://pg.edu.pl/studenci/projekty/uczelnie-przyszlosci/aktualnosci` |
| UAM WMiI – studenckie projekty badawczo-rozwojowe | uczelnie | `HtmlPage` | `https://wmi.amu.edu.pl/wydzial/projekty/studenckie-projekty-badawczo-rozwojowe` |

Seeder dodaje tylko brakujące źródła (porównanie nazw bez uwzględniania wielkości liter),
więc powtórne uruchomienia nie duplikują wpisów i nie nadpisują zmian użytkownika (np. wyłączonego
źródła lub zmienionego adresu). Usunięte źródło zostanie uzupełnione przy następnym starcie.
Nie jest wymagana nowa migracja – `RadarKind` jest zapisywany jako liczba całkowita.

Adapter HTML został przy okazji uogólniony: pomija linki z menu, nagłówka, stopki i nawigacji,
odrzuca odnośniki tylko do kotwicy oraz czyta tytuł z atrybutu `title` albo nagłówka otaczającego
odnośnik. Dzięki temu z rzeczywistych stron pobierane są ogłoszenia o projektach, a nie elementy
menu. Jednorazowa weryfikacja na żywo (poza zestawem testów) dała: AGH 12 ogłoszeń o projektach
studenckich, Politechnika Gdańska 12 aktualności „Uczelnie Przyszłości”, UAM WMiI 18 projektów.
Źródło Kickstartera pozostaje rzeczywiste, ale w tym środowisku serwis zwraca HTTP 403 (ochrona
Cloudflare) – patrz ograniczenia powyżej.

Weryfikacja:

- `dotnet build RadarInnowacji.sln -v minimal` – **0 błędów**, tylko 3 wcześniejsze ostrzeżenia
  `xUnit1031` w `RepositoryDetailsTests`,
- `dotnet test RadarInnowacji.sln` – **158 testów zaliczonych** (142 jednostkowe + 16 integracyjnych),
  w tym testy seedera (czysta baza, idempotencja, brak nadpisywania zmian), adapteru HTML
  oraz test hosta sprawdzający obecność czterech stałych źródeł na `/zrodla`,
- logi startu ze świeżego katalogu zawierają `Migracje bazy danych zostały zastosowane.`
  oraz `Stałe źródła są gotowe (nowych: 4).`

## Co działa w Etapie 0 (zaakceptowanym)

- uruchamialna aplikacja Blazor Web App z Interactive Server,
- pusty dashboard z polskimi komunikatami i formatowaniem dat,
- menu główne z pełną listą planowanych sekcji,
- logowanie do pliku w katalogu danych użytkownika,
- nasłuch wyłącznie na loopback, wybór wolnego portu, blokada drugiej instancji,
- otwarcie przeglądarki po uruchomieniu i uprzejme zamknięcie aplikacji.

## Co działa w Etapie 1

- jednoznaczny model danych: `Source`, `Discovery`, `InnovationProject`, `DiscoveryProject`,
  `Category`, `ProjectCategory`, `ScanHistory`, `AiAnalysisRecord`,
- migracja `InitialCreate` z indeksami i relacjami; kontekst EF Core nadal przygotowany do zmiany dostawcy na PostgreSQL,
- w pełni aktywne menu i strony: dashboard, cztery radary (w tym crowdfundingowy), wszystkie
  odkrycia, szczegóły, źródła, historia skanowania, kategorie i ustawienia,
- dashboard z KPI, liczbnościami radarów, aktywnością (SVG), najpopularniejszymi kategoriami
  i najnowszymi odkryciami,
- wspólny eksplorator odkryć z filtrami: tekst, pole daty i zakres, kategoria, źródło, status,
  status AI, radar; sortowanie i stronicowanie,
- szczegóły odkrycia z metadanymi, kategoriami, historią analiz AI, podobnymi odkryciami
  i akcją „Oznacz jako zweryfikowane”,
- zarządzanie źródłami: lista, edycja flagi aktywności i interwału skanowania,
- **28 wyraźnie oznaczonych (`DEMO`) przykładowych odkryć** dla czterech radarów,
  10 źródeł, 8 kategorii, 10 projektów, 10 skanów i 4 przykładowe analizy AI,
- przyciski „Załaduj przykłady” i „Usuń przykłady” na stronie Źródła; czyszczenie usuwa
  wyłącznie wiersze oznaczone jako `IsDemo`,
- polskie etykiety i formatowanie dat w całym interfejsie.

## Znane ograniczenia Etapu 1 (historyczne)

- Dane demonstracyjne nie są rzeczywistymi odkryciami – są oznaczone badge’em `DEMO`
  i usuwane w całości przez „Usuń przykłady”.
- W Etapie 1 nie istniały jeszcze prawdziwe adaptery źródeł ani skanowanie w tle.
- Strona „Kategorie i trendy” pokazuje liczności kategorii; trendy zostaną dodane później.

## Co działa w Etapie 2

- adaptery źródeł: GitHub API, GitLab API, Hugging Face API, DSpace REST API,
  RSS 2.0, Atom, OAI-PMH (`ListRecords`, `oai_dc`) oraz strony HTML z listą odnośników,
- rejestr adapterów `ISourceAdapterResolver` wybierany na podstawie `Source.SourceType`
  (typ `Atom` wykorzystuje wspólny adapter RSS/Atom),
- `IHttpClientFactory` z nazwanym klientem `RadarSources`: timeout 30 s, limit 2 MB,
  nagłówki `User-Agent` i `Accept`; token API jest czytany z `Source.Configuration`
  (`{"token":"..."}`) i opcjonalnie przekazywany jako `Bearer`/`PRIVATE-TOKEN`,
- formularz „Nowe źródło” na stronie Źródła: walidacja adresu http/https, unikalna nazwa,
  automatyczne mapowanie typu źródła na metodę pobierania, ograniczenie interwału (5–10080 min)
  oraz przycisk „Skanuj” przy każdym źródle rzeczywistym,
- `ScanService`: zapis `ScanHistory`, aktualizacja `LastScanDate`, `LastSuccessfulScan`
  i `LastError`, pomijanie źródeł `IsDemo`, limit materiałów na źródło,
- idempotentny zapis w ramach jednego źródła po kluczu `(SourceId, ExternalId)`
  z aktualizacją istniejącego odkrycia i skrótem SHA256 treści,
- `BackgroundScanJob` (`BackgroundService`) uruchamiany w procesie Web: pierwszy skan
  po `StartDelaySeconds`, kolejne co `IntervalMinutes`, tylko dla aktywnych i zaległych źródeł,
- nowe odkrycia są oznaczane jako `Candidate` / `Oczekuje na AI` i **nie** mają badge’u `DEMO`,
- testy jednostkowe parserów wszystkich adapterów, testy `ScanService`, `SourceService.CreateAsync`
  oraz test integracyjny formularza i przycisku skanowania.

## Znane ograniczenia Etapu 2

- Nie ma jeszcze analizy AI/Ollama, wektorów, oceny potencjału innowacji, trendów
  ani deduplikacji między źródłami.
- Idempotencja obowiązuje wyłącznie w ramach jednego źródła; ten sam materiał
  z dwóch różnych źródeł może zostać zapisany dwukrotnie.
- Adapter HTML jest heurystyką opartą na odnośnikach w obszarach `article`, `main`,
  `li`, `h2` i `h3`; najlepiej działa na prostych listach aktualności.
- OAI-PMH pobiera maksymalnie 5 stron wyników (`resumptionToken`).
- Token API jest przechowywany jako zwykły tekst w `Source.Configuration`; w produkcji
  należy zabezpieczyć bazę danych.
- Instalator EXE nie jest jeszcze zbudowany; na tym środowisku (Linux) zostanie przygotowany
  skrypt Inno Setup, a kompilacja `RadarInnowacji-Setup.exe` odbędzie się na maszynie Windows (Etap 8).

## Dalsze etapy

Zgodnie z `src/MainJob.txt`: Etap 3 – Radar repozytoriów, Etap 4 – Radar wiadomości,
Etap 5 – analiza AI (Ollama) i ocena potencjału innowacji.
Każdy etap jest wykonywany wyłącznie po akceptacji poprzedniego.
