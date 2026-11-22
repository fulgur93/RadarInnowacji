# Docker / Docker Compose

## Wymagania

- Docker Desktop / Docker Engine z Compose v2

## Uruchomienie

```bash
docker compose up -d --build
```

Aplikacja: http://localhost:5080  
Ollama API: http://localhost:11434

## Co uruchamia stack

- `radar` – aplikacja Blazor + skanowanie w tle (`RADAR_CONTINUOUS_SCANNING=true`)
- `ollama` – lokalny serwer Ollama z wolumenem `ollama-data`
- `ollama-pull` – jednorazowy kontener pobierający model AI (domyślnie `SpeakLeash/bielik-1.5b-v3.0-instruct:Q8_0`)

Dane aplikacji są w wolumenie `radar-data` (SQLite, logi). Nie są kasowane przy `docker compose down`.

## Konfiguracja modelu

Domyślnie pobierany jest polski model `SpeakLeash/bielik-1.5b-v3.0-instruct:Q8_0` (lekki, szybki do odsiewu szumu). Możesz zmienić model:

```bash
# .env
RADAR_AI_MODEL=SpeakLeash/bielik-1.5b-v3.0-instruct:Q8_0
```

albo jednorazowo:

```bash
RADAR_AI_MODEL=SpeakLeash/bielik-1.5b-v3.0-instruct:Q8_0 docker compose up -d --build
```

## Sprawdzenie

```bash
docker compose ps
docker compose logs -f radar
docker compose logs -f ollama-pull
```

Test AI w UI: Ustawienia -> „Test połączenia”.

## Reset danych

```bash
docker compose down
docker volume rm radarinowacji_radar-data
docker compose up -d --build
```

Nazwa wolumenu może mieć prefiks katalogu projektu; sprawdź `docker volume ls`.

## Uwagi

- Pierwsze uruchomienie może potrwać dłużej, bo `ollama-pull` pobiera model.
- Model `SpeakLeash/bielik-1.5b-v3.0-instruct:Q8_0` zajmuje ok. 1.7 GB; większe modele wymagają więcej RAM/VRAM.
- Aplikacja startuje, gdy Ollama jest healthy; pobieranie modelu nie blokuje UI (`ollama-pull` działa w tle).
- Bez GPU Ollama też zadziała, ale wolniej (CPU).
- Źródła seedują się automatycznie przy czystej bazie (`PermanentSourcesSeedJob`).
