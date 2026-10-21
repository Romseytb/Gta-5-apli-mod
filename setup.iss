; Script Inno Setup : installateur classique, un seul clic sur « Installer ».
#define AppName "GTA Mods Manager"
#define AppVersion "1.0.0"

[Setup]
AppId={{B7C1D2E4-5A6F-4F0B-9C21-7D3E8A1F4C55}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=GTA Mods Manager
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=yes
PrivilegesRequired=lowest
OutputDir=dist
OutputBaseFilename=GTA-Mods-Manager-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\GtaModsManager.exe

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\GtaModsManager.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\GtaModsManager.exe"

[Run]
Filename: "{app}\GtaModsManager.exe"; Description: "Lancer GTA Mods Manager"; Flags: nowait postinstall skipifsilent
