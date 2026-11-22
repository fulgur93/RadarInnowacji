# Dziki gon do innowacji – instalacja, uruchomienie i odinstalowanie

## Wymagania

- Windows 10 (1809 lub nowszy) albo Windows 11, 64-bitowy.
- Przeglądarka internetowa (Edge, Chrome, Firefox).
- Około 300 MB wolnego miejsca.
- Dostęp do internetu – tylko do skanowania rzeczywistych źródeł. Dane demonstracyjne działają offline.

NIE są potrzebne: Visual Studio, Rider, .NET SDK ani runtime .NET, PostgreSQL, uprawnienia administratora.
Ollama (lokalne AI) jest opcjonalna – bez niej aplikacja działa w pełni, a materiały czekają w kolejce
na analizę AI.

## Instalacja

1. Uruchom `RadarInnowacji-Setup.exe`.
   Windows SmartScreen może wyświetlić ostrzeżenie „System Windows ochronił Twój komputer”, ponieważ
   instalator nie jest podpisany cyfrowo. Wybierz „Więcej informacji” → „Uruchom mimo to”.
2. Instalator nie wymaga uprawnień administratora – program trafia do
   `%LOCALAPPDATA%\Programs\RadarInnowacji`.
3. Zostaw zaznaczoną opcję „Utwórz skrót na pulpicie” i kliknij „Zainstaluj”.
4. Na ostatniej stronie zaznacz „Uruchom Dziki gon do innowacji” i kliknij „Zakończ”.

## Uruchomienie

1. Kliknij dwukrotnie skrót **Dziki gon do innowacji** na pulpicie (lub w menu Start).
2. Otworzy się okno konsoli z informacją o adresie, np. `http://127.0.0.1:5080`.
3. Po kilku sekundach przeglądarka automatycznie otworzy dashboard.
   Jeśli tak się nie stanie, wpisz w przeglądarce adres widoczny w oknie konsoli.

Zasady działania:

- aplikacja nasłuchuje wyłącznie na `127.0.0.1` – nie jest widoczna w sieci lokalnej,
- domyślny port to `5080`; jeśli jest zajęty, zostanie użyty kolejny wolny (`5081`–`5089`),
- ponowne kliknięcie skrótu nie uruchamia drugiej kopii – otwiera przeglądarkę na działającej aplikacji,
- pierwsze uruchomienie tworzy bazę danych i dodaje stałe, rzeczywiste źródła; skanowanie w tle
  rozpoczyna się po około 10 sekundach.

## Zamykanie

Dowolny z poniższych sposobów zamyka aplikację poprawnie (baza danych zostaje zapisana):

- przycisk **Zamknij aplikację** na dole menu bocznego → „Zamknij”,
- zamknięcie okna konsoli „Dziki gon do innowacji”,
- `Ctrl+C` w oknie konsoli.

Zamknięcie samej karty przeglądarki NIE zatrzymuje serwera.

## Gdzie są dane

Dane użytkownika są przechowywane poza katalogiem instalacji:

| Co | Lokalizacja |
|---|---|
| Baza SQLite | `%LOCALAPPDATA%\RadarInnowacji\radar.db` |
| Logi | `%LOCALAPPDATA%\RadarInnowacji\logs\` |
| Program | `%LOCALAPPDATA%\Programs\RadarInnowacji\` |

Ścieżki można zmienić zmiennymi środowiskowymi `RADAR_DATA_DIR`, `RADAR_DB_PATH`, `RADAR_LOG_DIR`.
Aktualne ścieżki i adres aplikacji pokazuje strona **Ustawienia**.

## Opcjonalnie: AI (Ollama)

1. Zainstaluj Ollamę ze strony https://ollama.com.
2. W wierszu poleceń: `ollama pull qwen3:1.7b`.
3. W aplikacji: **Ustawienia → Test połączenia**.

Ollama nie jest częścią instalatora (zgodnie z wymaganiami – brak dużych modeli w pakiecie).

## Odinstalowanie

1. Zamknij aplikację (patrz „Zamykanie”). Jeśli działa, deinstalator poprosi o jej zamknięcie.
2. **Ustawienia systemu → Aplikacje → Zainstalowane aplikacje → Dziki gon do innowacji → Odinstaluj**
   (albo menu Start → Dziki gon do innowacji → „Odinstaluj Dziki gon do innowacji”).
3. Deinstalator zapyta: **„Czy usunąć również dane użytkownika?”**
   - **Nie** (domyślnie) – baza i logi zostają w `%LOCALAPPDATA%\RadarInnowacji`
     i zostaną użyte po ponownej instalacji,
   - **Tak** – folder danych zostanie usunięty w całości.

Deinstalacja cicha (`unins000.exe /VERYSILENT`) zawsze zachowuje dane.

## Rozwiązywanie problemów

| Objaw | Rozwiązanie |
|---|---|
| Przeglądarka się nie otworzyła | Otwórz ręcznie adres z okna konsoli lub ze strony Ustawienia. |
| „Nie znaleziono wolnego portu” | Porty 5080–5089 są zajęte – zamknij inne programy lub ustaw `RADAR_PORT`. |
| Komunikat „Dziki gon do innowacji jest już uruchomiony” | Aplikacja już działa – użyj otwartej karty lub adresu z komunikatu. |
| Status AI „niedostępne” | Normalne bez Ollamy; materiały czekają w kolejce na analizę. |
| Inne błędy | Sprawdź najnowszy plik w `%LOCALAPPDATA%\RadarInnowacji\logs\`. |
