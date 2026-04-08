#ifndef AppVersion
  #define AppVersion "1.0.0-dev"
#endif

[Setup]
AppId={{B0A2C1D4-E3F5-4A6B-9C8D-7E1F2A3B4C5D}}
AppName=BASpark
AppVersion={#AppVersion}
AppPublisher="BASpark Project"
DefaultDirName={autopf}\BASpark
DefaultGroupName=BASpark
AllowNoIcons=yes
AppMutex=Global\BASpark_SingleInstance_Mutex
CloseApplications=yes
SetupIconFile=src\app.ico
UninstallDisplayIcon={app}\BASpark.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
OutputDir=dist
OutputBaseFilename=BASpark_Installer_{#AppVersion}_x64

[Languages]
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "src\publish_full\BASpark.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "src\publish_full\Web\*"; DestDir: "{app}\Web"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\BASpark"; Filename: "{app}\BASpark.exe"
Name: "{autodesktop}\BASpark"; Filename: "{app}\BASpark.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\BASpark"; Flags: uninsdeletekey

[UninstallRun]
Filename: "taskkill"; Parameters: "/F /IM BASpark.exe /T"; Flags: runhidden

[Run]
Filename: "{app}\BASpark.exe"; Description: "{cm:LaunchProgram,BASpark}"; Flags: nowait postinstall skipifsilent
