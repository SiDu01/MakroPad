; MakroPad Windows installer (Inno Setup 6).
; Built by installer/build-release.ps1, which passes /DAppVersion and /DPublishDir.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\release\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\release"
#endif

#define AppName "MakroPad"
#define AppExe "MakroPad.App.exe"

[Setup]
; Keep this AppId stable across versions so updates replace the existing install.
AppId={{6F1C2A7E-4B8D-4C3A-9E51-2D7B0A6C4F13}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Simon D
AppPublisherURL=https://github.com/SiDu01/MakroPad
AppSupportURL=https://github.com/SiDu01/MakroPad/issues
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Per-user install by default, no admin rights needed; the user can still choose all users.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#OutputDir}
OutputBaseFilename=MakroPad-Setup-v{#AppVersion}
SetupIconFile=..\app\MakroPad.App\Assets\makropad.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Closes a running MakroPad before files are replaced.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
german.AutoStart=MakroPad beim Windows-Start automatisch starten
english.AutoStart=Start MakroPad automatically with Windows

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "{cm:AutoStart}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; Settings in %APPDATA%\MakroPad are kept on uninstall on purpose.
