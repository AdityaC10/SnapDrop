#ifndef PublishDir
  #error PublishDir must be supplied by scripts/publish.ps1
#endif
#ifndef ReleaseDir
  #error ReleaseDir must be supplied by scripts/publish.ps1
#endif
#ifndef ArtifactLabel
  #define ArtifactLabel "unsigned"
#endif

[Setup]
AppId={{A721B886-99C2-483B-BCDA-AD8D56C5EE03}
AppName=SnapDrop
AppVersion=0.1.0
AppPublisher=SnapDrop contributors
DefaultDirName={localappdata}\Programs\SnapDrop
DefaultGroupName=SnapDrop
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#ReleaseDir}
OutputBaseFilename=SnapDrop-0.1.0-win-x64-setup-{#ArtifactLabel}
SetupIconFile=..\assets\SnapDrop.ico
UninstallDisplayIcon={app}\SnapDrop.App.exe
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
WizardStyle=modern

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\SnapDrop"; Filename: "{app}\SnapDrop.App.exe"

[Run]
Filename: "{app}\SnapDrop.App.exe"; Description: "Start SnapDrop"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{userstartup}\SnapDrop.lnk"
; Recovery storage and user exports are deliberately not uninstall deletion targets.
