# Dziki gon do innowacji (Radar Innowacji)

Nazwa wyświetlana aplikacji to **Dziki gon do innowacji** (ikona: dzik). Nazwy techniczne
(repozytorium, projekty .NET, `RadarInnowacji.Web.exe`, katalog danych `RadarInnowacji`,
plik `RadarInnowacji-Setup.exe`) pozostały bez zmian, aby zachować dane i zgodność aktualizacji.

Inteligentny system automatycznego wykrywania potencjalnych innowacji w uczelniach,
repozytoriach kodu źródłowego, wiadomościach internetowych oraz na portalach crowdfundingowych.

Dzik Gon to inteligentny system automatycznego wykrywania potencjalnych innowacji w trzech źródłach: uczelniach, repozytoriach kodu (GitHub, GitLab, Hugging Face) oraz wiadomościach internetowych (RSS/Atom).
Aplikacja zbudowana jest na ASP.NET Core 10 / Blazor Web App z lokalną bazą SQLite, działa wyłącznie na loopbacku (127.0.0.1) i nie wymaga żadnych zewnętrznych usług.
Architektura jest czysta i rozdzielenia na cztery warstwy: Core (modele), Infrastructure (EF Core, integracje), Worker (zadania w tle jako BackgroundService) oraz Web (interfejs Blazor).

„Dziki gon do innowacji" to lokalna aplikacja webowa, która automatycznie przeszukuje cztery typy źródeł: uczelnie, repozytoria kodu, wiadomości internetowe i portale crowdfundingowe.
Aplikacja cyklicznie pobiera nowe materiały w tle i zapisuje je jako odkrycia.
Wbudowany silnik AI (lokalny, bez chmury) analizuje każde odkrycie i ocenia, czy może stanowić potencjalną innowację.
Projekty są automatycznie grupowane w kategorie na podstawie podobieństwa treści.
Materiały dotyczące tego samego projektu z różnych źródeł są łączone w jeden spójny projekt.
Aplikacja pokazuje trendy – które kategorie rosną najszybciej i gdzie jest największa aktywność.
Każdy projekt otrzymuje ocenę punktową według sześciu kryteriów (nowość, wpływ, skalowalność, dojrzałość, gotowość wdrożeniowa, kompletność).
Na podstawie oceny system rekomenduje dalszą ścieżkę rozwoju projektu (np. komercjalizacja, ochrona IP, obserwacja).
Projekty można kierować do konkretnych odbiorców z katalogu partnerów.
Lejek innowacji śledzi każdy projekt na dziesięciu etapach – od wykrycia po wdrożenie.
Instytucje mogą zgłaszać własne innowacje przez formularz, które trafiają od razu do procesu oceny.
Aplikacja działa wyłącznie lokalnie, instaluje się jednym plikiem na Windowsie i nie wymaga internetu poza pobieraniem danych ze źródeł.

Dalsze pomysły: - wyszukiwanie stron internetowych z innowacjami przy użyciu publicznych modeli AI np. ChatGP, grok, gemini, perplexity (nisko kosztowa automatyczna aktualizacja);
- obsługa google books w zakresie monografii z konferencji naukowych;
- dopracowanie kodu i dalsze rozwiązania (testy);
- dopracowanie kodu AI oceniającego innowacje znalezione przez crawler.

Dla osoby oceniającej:

- instalacja i uruchomienie – [`docs/INSTALACJA.md`](docs/INSTALACJA.md),
- włączanie aplikacji i szybki test dla testujących – [`docs/INSTRUKCJA_TESTOWANIA.txt`](docs/INSTRUKCJA_TESTOWANIA.txt),
- opis wszystkich funkcji – [`docs/INSTRUKCJA_UZYCIA.txt`](docs/INSTRUKCJA_UZYCIA.txt),
- scenariusz pokazu – [`docs/DEMO.md`](docs/DEMO.md).

## Stos technologiczny

- ASP.NET Core 10 / C# / Blazor Web App (Interactive Server)
- Entity Framework Core 10 + SQLite (możliwość przejścia na PostgreSQL)
- BackgroundService (zadania cykliczne, od Etapu 0 jako biblioteka `Jobs` uruchamiana w procesie Web)
- HttpClientFactory, AngleSharp, RSS/Atom, OAI-PMH (adaptery źródeł), Ollama (opcjonalny lokalny silnik AI)
- Bootstrap 5 (lokalnie w `wwwroot/lib/bootstrap`, bez zależności od CDN)
- Wykresy Razor/SVG bez własnego JavaScript-u


