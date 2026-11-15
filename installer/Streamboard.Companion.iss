#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif

#define AppName "Streamboard Companion"
#define AppPublisher "Webvibecoding"
#define AppExeName "Streamboard.Companion.exe"

[Setup]
AppId={{8D04CF4E-89DF-47F3-A36C-BBB14FBC5D72}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/webvibecoding/streamboard-companion
AppSupportURL=https://github.com/webvibecoding/streamboard-companion/issues
DefaultDirName={localappdata}\Programs\Streamboard Companion
DefaultGroupName=Streamboard Companion
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts
OutputBaseFilename=Streamboard.Companion-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\Streamboard.Companion.Windows\Assets\Streamboard.ico
UninstallDisplayIcon={app}\{#AppExeName}
UsePreviousTasks=yes
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} installer
VersionInfoProductName={#AppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Streamboard Companion"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\Streamboard Companion"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch Streamboard Companion"; Flags: nowait postinstall skipifsilent
