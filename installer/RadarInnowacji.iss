; Radar Innowacji – skrypt instalatora Inno Setup 6
; Budowanie: installer\build-installer.ps1 (Windows) lub installer/build-installer.sh (Linux + wine).
; Wejście: installer\publish\win-x64 (dotnet publish -c Release -r win-x64 --self-contained).
; Wynik:   installer\Output\RadarInnowacji-Setup.exe

#define AppName "Radar Innowacji"
#define AppVersion "1.0.0"
#define AppPublisher "Zespół Radar Innowacji"
#define AppExeName "RadarInnowacji.Web.exe"
#define PublishDir "publish\win-x64"

[Setup]
AppId={{6F1C7E0A-4B8E-4C61-9C1D-52A1E3B7D8F4}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
; Instalacja dla bieżącego użytkownika – bez uprawnień administratora i bez UAC.
PrivilegesRequired=lowest
DefaultDirName={autopf}\RadarInnowacji
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=RadarInnowacji-Setup
SetupIconFile=..\src\RadarInnowacji.Web\radar.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
; Instalator i deinstalator wykrywają działającą aplikację (mutex tworzony w Program.cs).
AppMutex=RadarInnowacji_AppMutex
CloseApplications=no

[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

[Tasks]
Name: "desktopicon"; Description: "Utwórz skrót na pulpicie"; GroupDescription: "Skróty:"

[InstallDelete]
; Aktualizacja: usuwa pliki poprzedniej wersji programu. Dane użytkownika są w %LOCALAPPDATA%\RadarInnowacji.
Type: filesandordirs; Name: "{app}\*"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Instrukcje jako .txt – na czystym Windows otworzy je Notatnik.
Source: "..\docs\INSTALACJA.md"; DestDir: "{app}\docs"; DestName: "Instalacja.txt"; Flags: ignoreversion
Source: "..\docs\DEMO.md"; DestDir: "{app}\docs"; DestName: "Instrukcja-demonstracji.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\Instrukcja demonstracji"; Filename: "{app}\docs\Instrukcja-demonstracji.txt"
Name: "{group}\Odinstaluj {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Uruchom {#AppName}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
const
  DataFolderName = 'RadarInnowacji';

function DataDirectory: String;
begin
  Result := ExpandConstant('{localappdata}\') + DataFolderName;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and DirExists(DataDirectory) then
  begin
    // Domyślnie (także przy deinstalacji cichej) dane są zachowywane.
    if (not UninstallSilent) and
       (MsgBox('Czy usunąć również dane użytkownika Radaru Innowacji?' + #13#10 + #13#10 +
               'Folder: ' + DataDirectory + #13#10 +
               '(baza odkryć, logi, ustawienia lokalne)' + #13#10 + #13#10 +
               'Wybierz „Nie”, aby zachować dane na potrzeby ponownej instalacji.',
               mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then
    begin
      if DelTree(DataDirectory, True, True, True) then
        Log('Usunięto dane użytkownika: ' + DataDirectory)
      else
        MsgBox('Nie udało się usunąć wszystkich danych. Folder możesz usunąć ręcznie: ' + DataDirectory,
               mbError, MB_OK);
    end;
  end;
end;
