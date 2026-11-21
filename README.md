# Radar Innowacji

Inteligentny system automatycznego wykrywania potencjalnych innowacji w uczelniach,
repozytoriach kodu źródłowego oraz wiadomościach internetowych.

**Status projektu: ETAP 1 – INTERFEJS I BAZA DANYCH (zaimplementowany, oczekuje na akceptację).**
Etap 0 został wcześniej zaakceptowany przez zamawiającego.

## Stos technologiczny

- ASP.NET Core 10 / C# / Blazor Web App (Interactive Server)
- Entity Framework Core 10 + SQLite (możliwość przejścia na PostgreSQL)
- BackgroundService (zadania cykliczne, od Etapu 0 jako biblioteka `Jobs` uruchamiana w procesie Web)
- HttpClientFactory, Ollama (opcjonalnie), AngleSharp, RSS/Atom, OAI-PMH (od kolejnych etapów)
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

Zmienne środowiskowe nadpisują ustawienia uruchomieniowe:
`RADAR_PORT`, `RADAR_OPEN_BROWSER`, `RADAR_SINGLE_INSTANCE`.
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
- w pełni aktywne menu i strony: dashboard, trzy radary, wszystkie odkrycia, szczegóły,
  źródła, historia skanowania, kategorie i ustawienia,
- dashboard z KPI, liczbnościami radarów, aktywnością (SVG), najpopularniejszymi kategoriami
  i najnowszymi odkryciami,
- wspólny eksplorator odkryć z filtrami: tekst, pole daty i zakres, kategoria, źródło, status,
  status AI, radar; sortowanie i stronicowanie,
- szczegóły odkrycia z metadanymi, kategoriami, historią analiz AI, podobnymi odkryciami
  i akcją „Oznacz jako zweryfikowane”,
- zarządzanie źródłami: lista, edycja flagi aktywności i interwału skanowania,
- **27 wyraźnie oznaczonych (`DEMO`) przykładowych odkryć** dla trzech radarów,
  9 źródeł, 8 kategorii, 10 projektów, 9 skanów i 4 przykładowe analizy AI,
- przyciski „Załaduj przykłady” i „Usuń przykłady” na stronie Źródła; czyszczenie usuwa
  wyłącznie wiersze oznaczone jako `IsDemo`,
- polskie etykiety i formatowanie dat w całym interfejsie.

## Znane ograniczenia Etapu 1

- Dane demonstracyjne nie są rzeczywistymi odkryciami – są oznaczone badge’em `DEMO`
  i usuwane w całości przez „Usuń przykłady”.
- Nie istnieją jeszcze prawdziwe adaptery źródeł (uczelnie, GitHub/GitLab/Hugging Face, RSS/Atom),
  skanowanie w tle, analiza Ollama, wektory, deduplikacja ani trendy.
- Strona „Kategorie i trendy” pokazuje liczności kategorii; trendy zostaną dodane później.
- Instalator EXE nie jest jeszcze zbudowany; na tym środowisku (Linux) zostanie przygotowany
  skrypt Inno Setup, a kompilacja `RadarInnowacji-Setup.exe` odbędzie się na maszynie Windows (Etap 8).

## Dalsze etapy

Etap 2 – rzeczywiste adaptery źródeł i skanowanie w tle.
Każdy etap jest wykonywany wyłącznie po akceptacji poprzedniego.
