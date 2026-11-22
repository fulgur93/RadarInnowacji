#!/usr/bin/env bash
# Buduje RadarInnowacji-Setup.exe na Linuksie: publikacja win-x64 + Inno Setup 6 uruchamiany przez wine.
# Wymagania: .NET SDK 10.0.404, wine, Inno Setup 6 zainstalowany w prefiksie wine
# (domyślnie szukany w "Program Files (x86)/Inno Setup 6/ISCC.exe"; można podać ISCC=/ścieżka/ISCC.exe).
# Użycie: installer/build-installer.sh [--skip-tests]
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(dirname "$script_dir")"
publish_dir="$script_dir/publish/win-x64"
wine_prefix="${WINEPREFIX:-$HOME/.wine}"
iscc="${ISCC:-$wine_prefix/drive_c/Program Files (x86)/Inno Setup 6/ISCC.exe}"

if [[ "${1:-}" != "--skip-tests" ]]; then
  echo "1/3 Testy..."
  dotnet test "$root/RadarInnowacji.sln" -c Release
fi

echo "2/3 Publikacja self-contained win-x64..."
rm -rf "$publish_dir"
dotnet publish "$root/src/RadarInnowacji.Web/RadarInnowacji.Web.csproj" \
  -c Release -r win-x64 --self-contained true -o "$publish_dir"

echo "3/3 Kompilacja instalatora (wine + ISCC)..."
if [[ ! -f "$iscc" ]]; then
  echo "Nie znaleziono ISCC.exe: $iscc" >&2
  echo "Zainstaluj Inno Setup 6 w wine lub ustaw zmienną ISCC." >&2
  exit 1
fi
(cd "$script_dir" && WINEDEBUG=-all wine "$iscc" /Q RadarInnowacji.iss)

echo "Gotowe: $script_dir/Output/RadarInnowacji-Setup.exe"
