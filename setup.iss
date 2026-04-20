; Transcription Tools - Inno Setup Script
; Installer bundles whisper.cpp CUDA binaries from whisper-bin/

#define MyAppName "Transcription Tools"
#define MyAppVersion GetEnv("APP_VERSION")
#if MyAppVersion == ""
#define MyAppVersion "0.0.0"
#endif
#define MyAppPublisher "Leapfreak"
#define MyAppExeName "TranscriptionTools.exe"
#define MyAppURL "https://github.com/Leapfreak/TranscriptionTools"

; Source directory for published app files
#define AppPublishDir "TranscriptionTools\bin\Publish"
; Whisper CUDA binaries (place whisper-cli.exe, whisper-stream.exe, DLLs here)
#define WhisperBinDir "whisper-bin"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=Output
OutputBaseFilename=TranscriptionTools_Setup_{#MyAppVersion}
LicenseFile=LICENSE
SetupIconFile=TranscriptionTools\Resources\AppIcon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
DisableDirPage=no
UsePreviousAppDir=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "catalan"; MessagesFile: "compiler:Languages\Catalan.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; --- Transcription Tools application ---
Source: "{#AppPublishDir}\TranscriptionTools.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppPublishDir}\TranscriptionTools.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppPublishDir}\TranscriptionTools.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppPublishDir}\TranscriptionTools.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppPublishDir}\component-versions.json"; DestDir: "{app}"; Flags: ignoreversion
; Help files
Source: "{#AppPublishDir}\Help\*"; DestDir: "{app}\Help"; Flags: ignoreversion recursesubdirs
; --- Whisper CUDA binaries ---
Source: "{#WhisperBinDir}\whisper-cli.exe"; DestDir: "{app}\whisper"; Flags: ignoreversion
Source: "{#WhisperBinDir}\whisper-stream.exe"; DestDir: "{app}\whisper"; Flags: ignoreversion
Source: "{#WhisperBinDir}\*.dll"; DestDir: "{app}\whisper"; Flags: ignoreversion
; --- NLLB translation server ---
Source: "{#AppPublishDir}\nllb-server\*"; DestDir: "{app}\nllb-server"; Flags: ignoreversion
; --- Live transcription server (faster-whisper + VAD) ---
Source: "{#AppPublishDir}\live-server\*"; DestDir: "{app}\live-server"; Flags: ignoreversion
; Locale satellite assemblies
Source: "{#AppPublishDir}\ca\*"; DestDir: "{app}\ca"; Flags: ignoreversion
Source: "{#AppPublishDir}\de\*"; DestDir: "{app}\de"; Flags: ignoreversion
Source: "{#AppPublishDir}\es\*"; DestDir: "{app}\es"; Flags: ignoreversion
Source: "{#AppPublishDir}\fr\*"; DestDir: "{app}\fr"; Flags: ignoreversion
Source: "{#AppPublishDir}\ja\*"; DestDir: "{app}\ja"; Flags: ignoreversion
Source: "{#AppPublishDir}\pt\*"; DestDir: "{app}\pt"; Flags: ignoreversion
Source: "{#AppPublishDir}\zh-Hans\*"; DestDir: "{app}\zh-Hans"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function CheckDotNetRuntime: Boolean;
var
  TmpFile: String;
  ResultCode: Integer;
  Output: AnsiString;
begin
  Result := False;
  TmpFile := ExpandConstant('{tmp}\dotnet_check.txt');

  if Exec('cmd.exe', '/c dotnet --list-runtimes > "' + TmpFile + '" 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if LoadStringFromFile(TmpFile, Output) then
    begin
      if Pos('Microsoft.WindowsDesktop.App 8.', String(Output)) > 0 then
        Result := True;
    end;
  end;

  DeleteFile(TmpFile);
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;

  if not CheckDotNetRuntime then
  begin
    if MsgBox(
      '.NET 8 Desktop Runtime is required but was not found.' + #13#10 + #13#10 +
      'Would you like to download it now?' + #13#10 + #13#10 +
      'Click Yes to open the download page, then run the installer.' + #13#10 +
      'Click No to continue setup anyway (the app will not run without it).',
      mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/en-us/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
  end;
end;
