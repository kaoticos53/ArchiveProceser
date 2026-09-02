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
spanish.BeginnerManual=Guía para Principiantes
spanish.ScriptManual=Manual de Scripting Personalizado
spanish.ExampleFlows=Ejemplos de Flujos
spanish.UserManualFile=Docs\manual_de_usuario.pdf
spanish.BeginnerManualFile=Docs\manual_usuario_principiantes.pdf
spanish.ScriptManualFile=Docs\manual_nodo_scripting.pdf

english.UserManual=User Manual
english.BeginnerManual=Beginner's Guide
english.ScriptManual=Custom Scripting Manual
english.ExampleFlows=Example Flows
english.UserManualFile=Docs\user_manual.pdf
english.BeginnerManualFile=Docs\beginner_user_guide.pdf
english.ScriptManualFile=Docs\scripting_node_manual.pdf

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UserManual}"; Filename: "{app}\{cm:UserManualFile}"
Name: "{group}\{cm:BeginnerManual}"; Filename: "{app}\{cm:BeginnerManualFile}"
Name: "{group}\{cm:ScriptManual}"; Filename: "{app}\{cm:ScriptManualFile}"
Name: "{group}\{cm:ExampleFlows}"; Filename: "{app}\Examples"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

