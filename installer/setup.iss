; Instalador Inno Setup para SisLab-Topo (versión C#/.NET).
;
; Adaptado del instalador original de la versión Java
; (sislab-topo\installer\setup.iss), NO es una copia literal. Cambios respecto
; al original:
;   - Ya no se anuncia ninguna contraseña por defecto en el texto de bienvenida:
;     la Fase 6 de la migración eliminó la contraseña hardcodeada "admin123" y la
;     reemplazó por un asistente de primer arranque que obliga a crear la
;     contraseña de administrador y muestra un código de recuperación de un solo
;     uso. Anunciar una contraseña conocida de antemano en el instalador habría
;     reabierto exactamente el hueco de seguridad que se cerró.
;   - Ya no se empaqueta "template_db.xlsx": la base SQLite (un solo archivo,
;     transaccional) se crea y migra sola en el primer arranque bajo
;     %APPDATA%\SisLabTopo — ver SisLabTopo.Data\DatabasePathProvider.cs.
;   - [Files] apunta al resultado de "dotnet publish" (self-contained,
;     single-file, win-x64) en vez del target\sislab-topo.exe de Maven/launch4j.
;     En C# no hace falta empaquetar una JRE aparte: el publish autocontenido ya
;     incluye el runtime .NET completo dentro del propio .exe.
;
; Antes de compilar este script, genera el publish con (desde la raíz del repo):
;   powershell -ExecutionPolicy Bypass -File installer\publish.ps1
; (o el comando dotnet publish equivalente documentado en ese script), que deja
; el .exe autocontenido en publish\win-x64\SisLabTopo.UI.exe.

#define MyAppName "SisLab-Topo"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Universidad Nacional del Altiplano - FIM"
#define MyAppURL "https://www.unap.edu.pe"
#define MyAppExeName "SisLabTopo.UI.exe"
#define PublishDir "..\publish\win-x64"

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\SisLab-Topo
DefaultGroupName=SisLab-Topo
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputBaseFilename=SisLab-Topo-Setup-{#MyAppVersion}
OutputDir=..\dist
Compression=lzma2/ultra64
SolidCompression=yes
SetupIconFile=..\src\SisLabTopo.UI\Assets\app_icon.ico
WizardStyle=modern
LicenseFile=..\LICENSE.txt
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
; Publish self-contained/single-file: un único .exe con el runtime .NET
; embebido, más los .pdb de depuración (excluidos aquí porque no aportan nada
; al usuario final y solo aumentan el tamaño del instalador).
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{group}\SisLab-Topo"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar SisLab-Topo"; Filename: "{uninstallexe}"
Name: "{commondesktop}\SisLab-Topo"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el Escritorio"; GroupDescription: "Íconos adicionales:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Iniciar SisLab-Topo"; Flags: nowait postinstall skipifsilent

[Code]
procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel2.Caption :=
    'Este asistente instalará SisLab-Topo v{#MyAppVersion} en su computadora.' + #13#10 + #13#10 +
    'Sistema de Gestión del Laboratorio de Topografía Minera' + #13#10 +
    'Facultad de Ingeniería de Minas - UNA Puno' + #13#10 + #13#10 +
    'Este programa no trae ninguna contraseña predefinida. Al abrirlo por ' +
    'primera vez, el propio sistema le pedirá crear la contraseña del ' +
    'administrador y le mostrará un código de recuperación de un solo uso: ' +
    'guárdelo en un lugar seguro, ya que permite restablecer el acceso si la ' +
    'contraseña se olvida.';
end;
