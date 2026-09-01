; Script de Inno Setup para FileFlow Studio
; Compilar con: iscc.exe installer\FileFlow.iss /DSourceDir="installer\publish\win-x64" /DAppVersion="1.0.0"
; (el script build-installer.ps1 se encarga de pasar estos parámetros automáticamente)

#ifndef SourceDir
  #define SourceDir "publish\win-x64"
#endif

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName "FileFlow Studio"
#define AppPublisher "FileFlow Studio"
#define AppExeName "FileFlow.App.exe"
#define AppId "{A8C9D6E1-4F2B-4E7A-9C3D-1B2E3F4A5D6C}"

[Setup]
AppId={{#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=FileFlowStudio-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
SetupIconFile=..\assets\FileFlow.ico
; Descomenta y ajusta si añades licencia propia:
; LicenseFile=..\LICENSE

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
spanish.UserManual=Manual de Usuario
spanish.ExampleFlows=Ejemplos de Flujos
english.UserManual=User Manual
english.ExampleFlows=Example Flows

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UserManual}"; Filename: "{app}\Docs\manual_de_usuario.pdf"
Name: "{group}\{cm:ExampleFlows}"; Filename: "{app}\Examples"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

