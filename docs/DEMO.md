# Radar Innowacji – instrukcja demonstracji (ok. 10 minut)

Scenariusz dla osoby oceniającej. Zakłada, że aplikacja jest zainstalowana (zob. `INSTALACJA`)
i uruchomiona ze skrótu na pulpicie. Internet jest potrzebny tylko w kroku 6 (rzeczywiste źródła).

Ważne: dane demonstracyjne są przykładowe i każde z nich ma badge **DEMO**. Nie są przedstawiane
jako rzeczywiste odkrycia. Wynik AI nie jest potwierdzeniem innowacyjności – to wskazówka do weryfikacji.

## 1. Załadowanie przykładów (30 s)

1. Menu **Źródła danych** → karta **Dane demonstracyjne** → **Załaduj przykłady**.
2. Powstaje 28 materiałów `DEMO` z czterech radarów, 10 projektów i 8 kategorii.

## 2. Dashboard (1 min)

Menu **Dashboard**. Pokaż:

- liczniki: wszystkie materiały, potencjalne innowacje, nowe odkrycia, aktywne źródła, kategorie,
- wykres aktywności w czasie,
- najnowsze odkrycia i najpopularniejsze kategorie,
- status AI (bez Ollamy AI jest niedostępne, a aplikacja działa dalej) i status skanowania.

## 3. Radary i filtrowanie (2 min)

1. **Radar uczelni**, **Radar repozytoriów**, **Radar wiadomości**, **Radar portali crowdfundingowych** –
   każdy pokazuje tylko swoje materiały.
2. **Wszystkie odkrycia**: wpisz w wyszukiwarkę np. `powietrza`, ustaw zakres dat (np. ostatnie 30 dni),
   wybierz kategorię, zmień sortowanie na „Świeżość”.
3. Kliknij tytuł odkrycia → karta odkrycia: źródło, daty publikacji i odkrycia, autor, organizacja,
   technologie, kategorie, wynik i uzasadnienie AI, podobne odkrycia, link do oryginału.

## 4. Wspólny radar – jeden projekt w wielu źródłach (2 min)

To główny warunek odbioru Etapu 7.

1. **Wspólny radar projektów** → ustaw filtr **Min. liczba źródeł** na `2` → **Szukaj**.
2. Otwórz projekt **„Inteligentna stacja jakości powietrza”** (DEMO).
3. Pokaż: **Ścieżkę powiązań projektu** (radary i źródła), **Materiały źródłowe tego projektu**
   (każdy z linkiem do oryginału), **Podobne projekty**, profil projektu i **Historię połączeń**.
4. **Duplikaty i powiązania** → **Wyszukaj duplikaty**: niejednoznaczne pary trafiają do propozycji
   (Akceptuj/Odrzuć) – system nie łączy ich bez decyzji użytkownika.

## 5. Kategorie i trendy (2 min)

Menu **Kategorie i trendy**:

- trendy liczone na unikalnych projektach (nie na liczbie pobranych materiałów), z informacją
  o liczbie monitorowanych źródeł i częstotliwości skanowania,
- porównanie kategorii (projekty, odkrycia, źródła, średnia świeżość),
- propozycje nowych kategorii wymagające akceptacji oraz historia zmian kategorii.

Uwaga: grupowanie pomija dane `DEMO` – propozycje nowych kategorii powstają z rzeczywistych materiałów
(krok 6).

## 6. Rzeczywiste źródła (2 min, wymaga internetu)

1. **Źródła danych** – lista stałych, rzeczywistych źródeł (uczelnie, GitHub, GitLab, Hugging Face,
   RSS/Atom, Kickstarter).
2. Kliknij **Skanuj** przy wybranym źródle (np. GitHub). Interfejs nie jest blokowany.
3. **Historia skanowania** – wynik, liczba nowych materiałów, ewentualne błędy.
   Awaria jednego źródła nie zatrzymuje pozostałych.
4. Nowe materiały nie mają badge’a DEMO i mają status „Oczekuje na AI”.
5. **Kategorie i trendy** → **Przelicz kategorie** – grupowanie i propozycje kategorii z rzeczywistych danych.

Skanowanie w tle uruchamia się samo (co 15 minut dla zaległych źródeł).

## 7. Ustawienia i zakończenie (1 min)

1. **Ustawienia**: adres aplikacji (tylko `127.0.0.1`), lokalizacja bazy i logów
   (`%LOCALAPPDATA%\RadarInnowacji`), konfiguracja AI i **Test połączenia** z Ollamą.
2. Opcjonalnie: **Źródła danych → Usuń przykłady** – usuwa wyłącznie dane `DEMO`.
3. **Zamknij aplikację** (dół menu bocznego) → **Zamknij**.

## Opcjonalnie: AI

Z zainstalowaną Ollamą i modelem `qwen3:1.7b` (zob. `INSTALACJA`) materiały ze statusem
„Oczekuje na AI” są analizowane w tle partiami; wynik obejmuje klasyfikację, uzasadnienie
i wyekstrahowane informacje. Dane `DEMO` nie są analizowane.
