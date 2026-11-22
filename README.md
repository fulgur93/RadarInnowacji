# Radar Innowacji

Inteligentny system automatycznego wykrywania potencjalnych innowacji w uczelniach,
repozytoriach kodu źródłowego, wiadomościach internetowych oraz na portalach crowdfundingowych.

**Status projektu: ETAP 8 – INSTALATOR I FINALIZACJA (zaimplementowany, oczekuje na akceptację).**
Etapy 0–7 zostały zaakceptowane przez zamawiającego (Etap 7 – wraz z poleceniem rozpoczęcia Etapu 8).

Dla osoby oceniającej: instalacja i uruchomienie – [`docs/INSTALACJA.md`](docs/INSTALACJA.md),
scenariusz pokazu – [`docs/DEMO.md`](docs/DEMO.md). W Etapie 3, na wyraźne
życzenie zamawiającego, dodano też osobny, czwarty radar portali crowdfundingowych
(zmiana wobec pierwotnie trójradarowego interfejsu z `src/MainJob.txt`).

## Stos technologiczny

- ASP.NET Core 10 / C# / Blazor Web App (Interactive Server)
- Entity Framework Core 10 + SQLite (możliwość przejścia na PostgreSQL)
- BackgroundService (zadania cykliczne, od Etapu 0 jako biblioteka `Jobs` uruchamiana w procesie Web)
- HttpClientFactory, AngleSharp, RSS/Atom, OAI-PMH (adaptery źródeł), Ollama (opcjonalny lokalny silnik AI)
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
│   ├── INSTALACJA.md                 # instalacja, uruchomienie, zamykanie, deinstalacja
│   └── DEMO.md                       # scenariusz demonstracji (~10 min)
└── installer/
    ├── RadarInnowacji.iss            # skrypt Inno Setup 6
    ├── build-installer.ps1           # budowanie na Windows (dotnet publish + ISCC)
    ├── build-installer.sh            # budowanie na Linuksie (dotnet publish + ISCC przez wine)
    ├── publish/win-x64/              # wynik dotnet publish (ignorowany przez git)
    └── Output/RadarInnowacji-Setup.exe  # wynik kompilacji instalatora (ignorowany przez git)
```

Uzasadnienie odstępstwa: `Worker` jest biblioteką klas z implementacjami `BackgroundService`
rejestrowanymi w procesie `Web`. Dzięki temu instalator uruchamia tylko jeden proces,
co upraszcza zamykanie aplikacji i obsługę portu.

## Wymagania wstępne (środowisko programistyczne)

Zainstalowana aplikacja (instalator) nie wymaga niczego poza Windows 10/11 x64 – zob. `docs/INSTALACJA.md`.
Do budowania ze źródeł:

- .NET SDK **10.0.404** (zainstalowany lokalnie w `/home/fulgur/.dotnet`)
- `dotnet` musi być w `PATH`: `export PATH=/home/fulgur/.dotnet:$PATH`
- Przeglądarka internetowa (otwierana automatycznie po starcie)

Nie są wymagane: Visual Studio, Rider, PostgreSQL, zewnętrzne chmury. Ollama jest opcjonalna – bez niej
aplikacja działa w trybie bez AI, a materiały czekają w kolejce na późniejszą analizę.

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
| `Ai:Enabled` | `true` | Włączenie analizy AI; `false` = tryb bez AI |
| `Ai:BaseUrl` | `http://127.0.0.1:11434` | Adres lokalnego serwera Ollama |
| `Ai:Model` | `qwen3:1.7b` | Model używany do analizy (domyślny według specyfikacji) |
| `Ai:TimeoutSeconds` | `90` | Limit czasu jednego zapytania do modelu |
| `Ai:IntervalMinutes` | `30` | Częstotliwość przetwarzania kolejki AI |
| `Ai:StartDelaySeconds` | `20` | Opóźnienie pierwszego cyklu analizy po starcie |
| `Ai:BatchSize` | `5` | Liczba odkryć analizowanych w jednym cyklu |
| `Ai:MaxContentCharacters` | `4000` | Maksymalna liczba znaków treści wysyłanej do modelu |
| `Categories:Enabled` | `true` | Włączenie cyklicznego grupowania projektów |
| `Categories:MinSimilarity` | `0.40` | Próg podobieństwa kosinusowego dla dwóch projektów |
| `Categories:ExistingCategorySimilarity` | `0.26` | Próg przypisania projektu do istniejącej kategorii |
| `Categories:MinClusterSize` | `2` | Najmniejsza grupa, która może stać się propozycją kategorii |
| `Categories:MaxCategoriesPerProject` | `2` | Liczba nowych przypisań do istniejących kategorii na cykl |
| `Categories:EmbeddingProvider` | `Lokalny` | `Lokalny` (bez modelu) albo `Ollama` (model embeddingowy) |
| `Categories:OllamaEmbeddingModel` | `multilingual-e5-small` | Model używany przez dostawcę `Ollama` |
| `Categories:OllamaBaseUrl` | `http://127.0.0.1:11434` | Adres lokalnego Ollamy dla embeddingów |
| `Categories:IntervalMinutes` | `60` | Częstotliwość grupowania |

Zmienne środowiskowe nadpisują ustawienia uruchomieniowe:
`RADAR_PORT`, `RADAR_OPEN_BROWSER`, `RADAR_SINGLE_INSTANCE`,
`RADAR_SCANNING_ENABLED`, `RADAR_SCAN_INTERVAL_MINUTES`,
`RADAR_SCAN_START_DELAY_SECONDS`, `RADAR_SCAN_MAX_ITEMS`,
`RADAR_AI_ENABLED`, `RADAR_AI_MODEL`, `RADAR_AI_URL`,
`RADAR_AI_TIMEOUT`, `RADAR_AI_BATCH`,
`RADAR_CATEGORIES_ENABLED`, `RADAR_CATEGORIES_INTERVAL`,
`RADAR_CATEGORIES_SIMILARITY`, `RADAR_CATEGORIES_MIN_PROJECTS`,
`RADAR_CATEGORY_EMBEDDING_PROVIDER`, `RADAR_CATEGORY_EMBEDDING_MODEL`,
`RADAR_CATEGORY_EMBEDDING_URL`.
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
- `dotnet test RadarInnowacji.sln --no-build` – **159 testów zaliczonych**
  (143 jednostkowych + 16 integracyjnych, w tym przypadki dla `/radar-crowdfunding`
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
dodaje dziewięć aktywnych źródeł. Nie są one oznaczone jako `DEMO` i nie wymagają importu ani
ręcznej konfiguracji:

| Źródło | Radar | Typ | Adres |
| --- | --- | --- | --- |
| Kickstarter – wyszukiwanie projektów | portale crowdfundingowe | `KickstarterApi` | `https://www.kickstarter.com` |
| AGH – projekty studenckie | uczelnie | `HtmlPage` | `https://www.agh.edu.pl/slowa-kluczowe/projekty-studenckie` |
| Politechnika Gdańska – Uczelnie Przyszłości | uczelnie | `HtmlPage` | `https://pg.edu.pl/studenci/projekty/uczelnie-przyszlosci/aktualnosci` |
| UAM WMiI – studenckie projekty badawczo-rozwojowe | uczelnie | `HtmlPage` | `https://wmi.amu.edu.pl/wydzial/projekty/studenckie-projekty-badawczo-rozwojowe` |
| Spider's Web – technologie | wiadomości | `Rss` | `https://www.spidersweb.pl/feed` |
| Nauka w Polsce – aktualności | wiadomości | `Rss` | `https://naukawpolsce.pl/rss.xml` |
| ScienceDaily – najnowsze badania | wiadomości | `Rss` | `https://www.sciencedaily.com/rss/all.xml` |
| ScienceAlert – odkrycia naukowe | wiadomości | `Rss` | `https://www.sciencealert.com/feed` |
| New Scientist – wiadomości naukowe | wiadomości | `Rss` | `https://www.newscientist.com/feed/` |

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
- `dotnet test RadarInnowacji.sln` – **166 testów zaliczonych** (150 jednostkowych + 16 integracyjnych),
  w tym testy seedera (czysta baza, idempotencja, brak nadpisywania zmian), adapteru HTML
  oraz test hosta sprawdzający obecność dziewięciu stałych źródeł na `/zrodla`,
- logi startu ze świeżego katalogu zawierają `Migracje bazy danych zostały zastosowane.`
  oraz `Stałe źródła są gotowe (nowych: 9).`

## Radar wiadomości (Etap 4)

- pięć rzeczywistych kanałów wiadomości naukowo-technologicznych (tabela wyżej) jest dodawanych
  przy starcie na czystą bazę jako aktywne źródła radaru `Wiadomosci`, typu `Rss` (metoda `Rss`),
  z interwałem 360 min; nie wymagają kluczy API ani ręcznej konfiguracji,
- adapter RSS/Atom pobiera tytuł, opis/treść, odnośnik i datę publikacji z `pubDate`/`dc:date`;
  obsługuje też skróty strefy czasowej, których .NET nie rozpoznaje (np. `EST`),
- adapter HTML odczytuje datę publikacji z elementu `<time datetime>` oraz typowych klas
  (`date`, `data`, `published`, `pub-date`) w odnośniku lub jego najbliższym otoczeniu;
  gdy strona nie udostępnia daty, pole pozostaje `NULL` (bez domysłów),
- formaty `dd.MM.yyyy`, `d.M.yyyy` i `dd/MM/yyyy` są rozpoznawane w datach z polskich stron,
- wyniki są zapisywane jako odkrycia z `PublishedAt`, wyświetlane na `/radar-wiadomosci`
  i wspólnym widoku odkryć; każdy skan ma wpis w historii (`/historia`) i aktualizuje
  `LastScanDate`/`LastSuccessfulScan` oraz `LastError`,
- seeder pozostaje idempotentny: nie duplikuje i nie nadpisuje zmian użytkownika; Etap 4
  nie wymagał nowej migracji (`RadarKind` jest zapisywany jako liczba całkowita).

Jednorazowa weryfikacja na żywo (poza zestawem testów, 22.11.2026):

- pobranie kanałów przez adapter: Spider's Web 50/50 pozycji z datą, Nauka w Polsce 10/10,
  ScienceDaily 60/60, ScienceAlert 10/10, New Scientist 10/10; AGH (HTML) 26 odnośników,
  z czego 5 z datą publikacji,
- pełny skan źródła ScienceAlert dodanego przez seeder przez `ScanService`: status `Succeeded`,
  znaleziono i zapisano 10 odkryć, wszystkie z datą publikacji,
- test runtime na `127.0.0.1:15195` ze świeżym katalogiem danych: `/`, `/radar-wiadomosci`,
  `/radar-uczelni`, `/radar-repozytoriow`, `/radar-crowdfunding`, `/odkrycia`, `/historia`
  i `/zrodla` zwróciły HTTP 200; `/zrodla` pokazało wszystkie dziewięć stałych źródeł;
  w logu `Stałe źródła są gotowe (nowych: 9).`, brak wpisów `fail:`/`crit:`, zamknięcie
  SIGTERM odnotowane jako `Application is shutting down...`.

Znane ograniczenia Etapu 4:

- kanał Nauka w Polsce zawiera również starszą pozycję (2018) obok aktualnych wiadomości;
  data jest zachowywana tak, jak podał ją wydawca – nie jest „odświeżana",
- część kanałów (ScienceDaily, ScienceAlert, New Scientist) publikuje treści po angielsku;
  interfejs aplikacji pozostaje w języku polskim,
- data z HTML jest ustawiana tylko wtedy, gdy strona udostępnia rozpoznawalny element daty,
- źródła nie omijają zabezpieczeń serwisów; ograniczenie Kickstartera (HTTP 403) pozostaje
  bez zmian (opisane powyżej).

## Silnik AI (Etap 5)

- integracja z lokalnym Ollamą przez HTTP (`/api/chat` z `format=json` oraz `/api/tags`
  do testu połączenia); bez chmury, kluczy API i wysyłania danych na zewnątrz,
- model domyślny `qwen3:1.7b` (konfigurowalny; na maszynie weryfikacyjnej użyto
  zainstalowanego `qwen2.5-coder:latest` przez `RADAR_AI_MODEL`),
- analiza treści odkrycia (tytuł, opis, adres, data, fragment treści) zwraca ustrukturyzowany
  JSON: `isPotentialInnovation`, `confidence`, `innovationType`, `problem`, `solution`,
  `noveltyDescription`, `technologies`, `reason`, `suggestedCategories`, `missingInformation`,
- prompt wyraźnie zakazuje przedstawiania przypuszczeń jako faktów – brakujące dane są
  oznaczane jako „Brak danych”, a wynik AI jest zawsze oznaczony jako wymagający weryfikacji człowieka,
- odkrycie otrzymuje status `Interesting`, gdy AI wskaże potencjalną innowację, albo `Candidate`
  w przeciwnym razie; status `Verified` nigdy nie jest obniżany,
- kolejka analiz (`Pending`/`AiQueued` → `AiProcessed`/`AiFailed`) działa w tle
  jako `AiAnalysisJob`; każda próba zapisuje `AiAnalysisRecord` (model, pełny JSON, pewność,
  uzasadnienie, wersja promptu, błąd), więc historia analiz jest dostępna na stronie odkrycia,
- błędy modelu (HTTP, nieprawidłowy JSON, przekroczenie czasu) są logowane i zapisywane
  jako `AiFailed` – pozwalają na ręczną ponowną analizę przyciskiem „Uruchom analizę AI”,
- strona Ustawienia pokazuje konfigurację AI i przycisk „Test połączenia”,
- tryb bez AI: `RADAR_AI_ENABLED=false` (albo brak działającego Ollamy) wyłącza job;
  skanowanie i wszystkie pozostałe funkcje działają normalnie, a odkrycia pozostają w kolejce,
- dane demonstracyjne (`IsDemo`) nie są analizowane; ETAP 5 nie wymagał nowej migracji.

Jednorazowa weryfikacja na żywo (poza zestawem testów, 22.11.2026):

- rzeczywiste źródła: AGH (HTML, uczelnia), ScienceDaily (RSS, wiadomości) i GitHub API
  (zapytanie `prototype`, repozytorium) – każde pobrało po 1 rzeczywistym materiale,
- test połączenia z Ollamą: model `qwen2.5-coder:latest` dostępny,
- kolejka AI przeanalizowała wszystkie trzy materiały bez błędów; w bazie zapisano trzy wpisy
  `AiAnalysisRecord` z pełnym JSON, a każde odkrycie ma `AIProcessed=true`,
- przykładowe wyniki: „czysta energia” (uczelnie) – 0% i wyraźny brak danych,
  „AI may not work like the human brain after all…” (wiadomości) – 85% i wykrycie
  nowej metody, „jkoehler11/stargate-alliance-decomp” (repozytoria) – 20% i brak
  potwierdzonych informacji o innowacji,
- model na CPU potrzebował 30–75 s na jedno zapytanie, więc weryfikacja użyła
  `TimeoutSeconds=180`; domyślny limit 90 s można w razie potrzeby zwiększyć.

Znane ograniczenia Etapu 5:

- jakość oceny zależy od modelu: część pól może pozostać „Brak danych”, a model może
  zwrócić pojedyncze określenia po angielsku mimo instrukcji po polsku – interfejs pozostaje polski,
- niskie pewności (np. 0–20%) są zapisywane, ale nadal wymagają weryfikacji człowieka,
- bez zainstalowanego modelu `qwen3:1.7b` job wstrzymuje analizę z ostrzeżeniem w logu
  i ponawia test połączenia w kolejnym cyklu; należy zainstalować model albo ustawić
  `RADAR_AI_MODEL` na już dostępny.

## Dynamiczne kategorie i świeżość (Etap 6)

- nowe encje i migracja `20261122011402_DynamicCategories`: `ProjectEmbedding`,
  `CategoryProposal`, `CategoryProposalProject`, `CategoryHistory`,
- embeddingi: interfejs `IEmbeddingService`. Domyślny `LocalHashEmbeddingService` jest
  deterministyczny, działa lokalnie bez modelu i bez sieci (feature hashing, 256 wymiarów:
  słowa, prefiksy 5 znaków i bigramy). Tekst projektu obejmuje tytuł, opis, `RawContent`
  (np. README) oraz sugerowane kategorie z AI jako **słaby sygnał**, nie jako fakt.
  Opcjonalnie `OllamaEmbeddingService` wywołuje lokalny `/api/embed` z modelem
  `multilingual-e5-small`; wybór przez `Radar:Categories:EmbeddingProvider`
  (`RADAR_CATEGORY_EMBEDDING_PROVIDER=Ollama`). Brak modelu embeddingowego nie blokuje aplikacji,
- grupowanie: union-find na podobieństwie kosinusowym z filtrem stabilności względem centroidu.
  Zamiast HDBSCAN użyto prostego algorytmu CPU – w ekosystemie .NET nie znaleziono
  kompatybilnej biblioteki, a prototyp nie wymaga klastra. Cykl uruchamia `CategoryAnalysisJob`
  albo przycisk „Przelicz kategorie” na stronie „Kategorie i trendy” (nie przy każdym skanie),
- odkrycia bez projektu otrzymują prosty projekt 1:1, aby mogły wejść do grupowania;
  łączenie materiałów z wielu źródeł w jeden projekt pozostaje w Etapie 7. Dane `DEMO` są wyłączone,
- nowa kategoria powstaje wyłącznie jako `CategoryProposal` (status `Pending`). Akceptacja
  tworzy `Category` i przypisuje projekty (projekt może należeć do wielu kategorii);
  odrzucenie zapisuje podpis grupy, więc ta sama propozycja nie jest powtarzana.
  Nazwy są wydobywane z opisów/treści z filtrem słów ogólnych i zawsze są oznaczone
  jako propozycja wymagająca weryfikacji,
- istniejące kategorie: automatyczne przypisanie przy progu `ExistingCategorySimilarity`
  (maks. 2 nowe kategorie na projekt w cyklu), bez usuwania wcześniejszych przypisań,
- `FreshnessScore` (0–100) jest liczony wyłącznie z dat:
  `0.60 × 100e^(-dniOdAktywności/90) + 0.25 × składnikPochodzenia + 0.15 × 100e^(-dniOdkrycia/730)`.
  Składnik pochodzenia to `100e^(-wiek/365)`, a gdy data powstania jest nieznana – `50`
  zamiast wymyślonej daty. Ocena AI i status innowacyjności nie wpływają na świeżość,
- eksplorator odkryć ma filtr `MinFreshnessScore`, sortowanie po świeżości i badge procentowy;
  strona „Kategorie i trendy” pokazuje propozycje (Akceptuj/Odrzuć), grupy projektów
  i historię zmian, a „Ustawienia” – konfigurację kategorii,
- testy: 163 jednostkowe + 16 integracyjne (179 łącznie), 0 niepowodzeń; pozostały
  3 wcześniejsze ostrzeżenia `xUnit1031` w `RepositoryDetailsTests.cs`.

Jednorazowa weryfikacja na żywo (poza zestawem testów, 22.11.2026):

- kopia rzeczywistej bazy z 853 odkryciami z 10 rzeczywistych źródeł (GitHub, GitLab,
  AGH, Politechnika Gdańska, UAM, RSS); skanowanie i analiza AI były wyłączone,
  aby nie modyfikować danych źródłowych,
- pierwszy cykl: 300 projektów (limit na cykl), 300 embeddingów, 11 grup podobnych projektów
  i 4 propozycje nowych kategorii; `GET /kategorie` zwróciło HTTP 200 i pokazało grupy,
  propozycje oraz historię,
- przykładowe propozycje: „Microsoft ignite26 foundry” (2 projekty, podobieństwo 0.885),
  „Durga0sandeep gameengine3d” (2, 1.0), „Ism3113 group” (2, 0.853), „Streamline” (3, 0.783).
  Żadna z nich nie utworzyła kategorii bez akceptacji,
- świeżość rzeczywistych projektów: 70.0–87.5 (średnia 74.3).

Znane ograniczenia Etapu 6:

- domyślny embedder hashingowy nie rozumie znaczeń słów – grupuje na podstawie
  współwystępowania cech tekstu. Model wielojęzyczny wymaga instalacji modelu w Ollamie
  i przełączenia dostawcy na `Ollama`,
- gdy materiał nie ma opisu, nazwa propozycji może odziedziczyć nazwę repozytorium;
  dlatego propozycje zawsze wymagają weryfikacji człowieka,
- przy 300 projektach porównywanie par jest O(n²) na CPU; większa skala wymagałaby
  indeksu wektorowego (poza zakresem prototypu),
- trendy i łączenie materiałów z kilku źródeł w jeden projekt pozostają w Etapie 7.

## Wspólny radar i trendy (Etap 7)

- migracja `20261122013820_Etap7ProjectLinks`: `InnovationProject.MergedIntoProjectId`,
  encje `ProjectMergeProposal` (status `Pending`/`Accepted`/`Rejected`) i `ProjectMergeHistory`,
- **Wspólny radar projektów** (`/projekty`): jeden widok projektów ze wszystkich radarów i źródeł;
  filtry tekst, radar, źródło, kategoria, **min. liczba źródeł**, sortowanie i stronicowanie,
- **szczegóły projektu** (`/projekty/{id}`): ścieżka powiązań (radary i źródła), wszystkie
  materiały źródłowe z linkami do oryginałów, podobne projekty, propozycje i historia połączeń.
  Z karty odkrycia prowadzi link do projektu,
- **deduplikacja** (`IProjectService.DetectDuplicatesAsync`, cyklicznie `ProjectAnalysisJob`
  co `Projects:IntervalMinutes`, ręcznie na stronie **Duplikaty i powiązania**): jednoznaczne
  sygnały (wspólny `ExternalId`/`ContentHash`) łączą projekty automatycznie; poza nimi automatyczne
  połączenie wymaga jednocześnie embeddingu `≥ AutoMergeSimilarity` (0,93), podobieństwa tytułu ≥ 0,70
  i opisu ≥ 0,45. Pozostałe pary z wynikiem ważonym (0,5 embedding + 0,3 tytuł + 0,2 opis)
  `≥ ProposalSimilarity` (0,62) trafiają wyłącznie do propozycji wymagających akceptacji. Łączenie przenosi powiązania odkryć do projektu
  docelowego, **nie usuwa żadnych materiałów** i zapisuje historię; odrzucona para nie jest proponowana ponownie,
- **trendy** (`ITrendService`): liczone na unikalnych projektach, a nie na liczbie pobranych
  materiałów; bieżący, niepełny miesiąc jest pomijany. Każdy wynik zawiera liczbę monitorowanych
  źródeł i częstotliwość skanowania. Strona „Kategorie i trendy” pokazuje najaktywniejsze
  i najszybciej rosnące kategorie, nowe kategorie, wykres aktywności (SVG) oraz porównanie kategorii
  (projekty, odkrycia, źródła, średnia świeżość),
- konfiguracja w sekcji `Radar:Projects` (`Enabled`, `IntervalMinutes`, `ProposalSimilarity`,
  `AutoMergeSimilarity`, `SimilarProjectsLimit`, `TrendMonths`).

Znane ograniczenia Etapu 7:

- podobieństwo projektów korzysta z tego samego embeddera co Etap 6 (domyślnie hashingowego),
  więc parafrazy bez wspólnych słów nie są rozpoznawane jako duplikaty,
- połączenie projektów nie ma operacji „cofnij” w interfejsie (historia jest zapisywana w bazie),
- trendy miesięczne są wiarygodne dopiero po kilku miesiącach regularnego skanowania.

## Instalator i finalizacja (Etap 8)

- **publikacja self-contained win-x64** (`dotnet publish -c Release -r win-x64`, ok. 117 MB):
  zawiera runtime .NET 10 i natywny SQLite; nie wymaga SDK ani runtime na maszynie docelowej.
  Plik wykonywalny `RadarInnowacji.Web.exe` ma własną ikonę (`radar.ico`),
- **Inno Setup** (`installer/RadarInnowacji.iss`): instalacja bez uprawnień administratora do
  `%LOCALAPPDATA%\Programs\RadarInnowacji`, język polski, skrót na pulpicie i w menu Start,
  instrukcje jako `.txt`, opcja „Uruchom po instalacji”. Instalator i deinstalator wykrywają
  działającą aplikację (`AppMutex=RadarInnowacji_AppMutex`). Deinstalator pyta, czy usunąć dane
  użytkownika (domyślnie **Nie**; deinstalacja cicha zawsze zachowuje dane),
- **uruchamianie**: tylko `127.0.0.1`, obsługa zajętego portu (5080–5089), automatyczne
  otwarcie przeglądarki, katalog programu jako katalog zawartości (działa także przy starcie
  z innym katalogiem roboczym),
- **druga instancja**: ponowne kliknięcie skrótu nie uruchamia drugiego serwera – odczytuje
  adres działającej instancji z `radar.url` (akceptowany wyłącznie adres `http` na loopback)
  i otwiera na nim przeglądarkę,
- **zamykanie**: przycisk „Zamknij aplikację” w menu bocznym (z potwierdzeniem), zamknięcie okna
  konsoli lub `Ctrl+C`. Przy zamknięciu usuwane są `radar.lock` i `radar.url`, baza pozostaje,
- **dane użytkownika** poza katalogiem instalacji: `%LOCALAPPDATA%\RadarInnowacji`
  (`radar.db`, `logs\`); strona Ustawienia pokazuje rzeczywisty adres aplikacji i ścieżki,
- dokumentacja: `docs/INSTALACJA.md` (instalacja, uruchomienie, zamykanie, deinstalacja,
  rozwiązywanie problemów) i `docs/DEMO.md` (scenariusz pokazu ok. 10 min).

### Budowanie instalatora

Windows (wymaga .NET SDK 10.0.404 i Inno Setup 6 – tylko na maszynie budującej):

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
```

Linux (wymaga wine i Inno Setup 6 zainstalowanego w prefiksie wine):

```bash
installer/build-installer.sh            # testy + publikacja + ISCC
ISCC="/ścieżka/ISCC.exe" WINEPREFIX=~/.wine-inno installer/build-installer.sh --skip-tests
```

Wynik: `installer/Output/RadarInnowacji-Setup.exe`.

### Test na czystym Windows (lista kontrolna)

Maszyna bez .NET i bez narzędzi programistycznych (np. Windows Sandbox lub świeża maszyna wirtualna):

1. `RadarInnowacji-Setup.exe` → instalacja bez UAC, skrót na pulpicie istnieje.
2. Skrót → okno konsoli z adresem, przeglądarka otwiera dashboard.
3. Źródła danych → Załaduj przykłady → dashboard pokazuje dane DEMO.
4. Ponowne kliknięcie skrótu → otwiera się karta z tą samą instancją (bez drugiego serwera).
5. „Zamknij aplikację” → okno konsoli znika; `%LOCALAPPDATA%\RadarInnowacji\radar.db` istnieje.
6. Ponowny start → dane DEMO nadal są widoczne.
7. Deinstalacja z odpowiedzią „Nie” → folder danych zostaje; ponowna instalacja widzi dane.
8. Deinstalacja z odpowiedzią „Tak” → folder `%LOCALAPPDATA%\RadarInnowacji` usunięty.

Wynik weryfikacji Etapu 8 (Linux, 22.11.2026):

- `dotnet build`: 0 błędów; testy: 181 jednostkowych + 16 integracyjnych, 0 niepowodzeń,
- publikacja win-x64 self-contained: OK (`RadarInnowacji.Web.exe`, `e_sqlite3.dll`, `wwwroot`),
- ta sama logika startowa zweryfikowana na publikacji self-contained linux-x64 uruchomionej
  z katalogu `/`: dashboard, `/projekty`, `/duplikaty`, `/kategorie`, `/ustawienia`, zasoby
  statyczne – HTTP 200; druga instancja wypisała adres działającej i zakończyła się kodem 0;
  po SIGTERM proces zakończył się, a `radar.lock`/`radar.url` zostały usunięte,
- `RadarInnowacji-Setup.exe` (ok. 38 MB) skompilowany przez `installer/build-installer.sh`
  z Inno Setup 6.7.4 w osobnym prefiksie wine (`~/.wine-inno`),
- cicha instalacja pod wine: pliki w `%LOCALAPPDATA%\Programs\RadarInnowacji`, instrukcje `.txt`,
  skróty w menu Start i na pulpicie; cicha deinstalacja usunęła katalog programu,
- **nie wykonano**: uruchomienia aplikacji pod wine ani testu instalacji na czystym Windows –
  do wykonania według listy kontrolnej powyżej.

Znane ograniczenia Etapu 8:

- instalator nie jest podpisany cyfrowo – Windows SmartScreen pokaże ostrzeżenie,
- aplikacja działa w oknie konsoli (widoczny adres i logi; zamknięcie okna zamyka aplikację);
  brak ikony w zasobniku systemowym,
- nazwa pliku wykonywalnego to `RadarInnowacji.Web.exe` (zmiana nazwy zestawu wymagałaby
  zmiany nazw zasobów statycznych Blazora); skróty mają nazwę „Radar Innowacji”.

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

## Znane ograniczenia Etapu 2 (historyczne)

- W momencie Etapu 2 nie było jeszcze analizy AI/Ollamy, wektorów, oceny potencjału
  innowacji, trendów ani deduplikacji między źródłami; analizę AI dodano w Etapie 5.
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

Zgodnie z `src/MainJob.txt` Etap 8 jest ostatnim etapem planu. Po jego akceptacji możliwe
są wyłącznie poprawki zgłoszone po teście na czystym Windows.
Każdy etap jest wykonywany wyłącznie po akceptacji poprzedniego.
