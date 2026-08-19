#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef AppArch
  #define AppArch "x64"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\win-x64"
#endif

[Setup]
AppId={{D38F24BA-79F8-4D6F-9AC8-35639148B92A}
AppName=AirTun
AppVersion={#AppVersion}
AppVerName=AirTun {#AppVersion}
AppPublisher=Omid Zaferi
AppPublisherURL=https://github.com/omid-io/AirTun
AppSupportURL=https://github.com/omid-io/AirTun/issues
AppUpdatesURL=https://github.com/omid-io/AirTun/releases
DefaultDirName={autopf}\AirTun
DefaultGroupName=AirTun
DisableProgramGroupPage=yes
LicenseFile=..\..\LICENSE
OutputDir=..\..\artifacts
OutputBaseFilename=AirTun-v{#AppVersion}-windows-{#AppArch}-Setup
SetupIconFile=..\AirTun.App\Assets\app.ico
UninstallDisplayIcon={app}\AirTun.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
#if AppArch == "x64"
ArchitecturesInstallIn64BitMode=x64compatible
#endif
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\AirTun"; Filename: "{app}\AirTun.exe"
Name: "{group}\{cm:UninstallProgram,AirTun}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\AirTun"; Filename: "{app}\AirTun.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\AirTun.exe"; Description: "{cm:LaunchProgram,AirTun}"; Flags: nowait postinstall skipifsilent shellexec

