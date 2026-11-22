# Buduje RadarInnowacji-Setup.exe na Windows.
# Wymagania (tylko na maszynie budującej): .NET SDK 10.0.404, Inno Setup 6 (ISCC.exe).
# Użycie:  powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
param(
    [string]$Iscc = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $PSScriptRoot "publish\win-x64"

Write-Host "1/3 Testy..."
dotnet test (Join-Path $root "RadarInnowacji.sln") -c Release
if ($LASTEXITCODE -ne 0) { throw "Testy zakończone niepowodzeniem." }

Write-Host "2/3 Publikacja self-contained win-x64..."
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish (Join-Path $root "src\RadarInnowacji.Web\RadarInnowacji.Web.csproj") `
    -c Release -r win-x64 --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "Publikacja zakończona niepowodzeniem." }

Write-Host "3/3 Kompilacja instalatora..."
if (-not $Iscc) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $Iscc = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}
if (-not $Iscc) { throw "Nie znaleziono ISCC.exe. Zainstaluj Inno Setup 6 lub podaj -Iscc <ścieżka>." }

& $Iscc (Join-Path $PSScriptRoot "RadarInnowacji.iss")
if ($LASTEXITCODE -ne 0) { throw "Kompilacja Inno Setup zakończona niepowodzeniem." }

Write-Host "Gotowe: $(Join-Path $PSScriptRoot 'Output\RadarInnowacji-Setup.exe')"
