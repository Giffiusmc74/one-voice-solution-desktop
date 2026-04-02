; ============================================================
; ONE Voice Solution v5.0 — Inno Setup Installer Script
; ONE United Global  2026
;
; What this installer does:
;   1. Silently installs VB-Audio Virtual Cable driver (bundled)
;   2. Installs the ONE Voice Solution application
;   3. Installs the ONE Digital Video resource
;   4. Creates desktop shortcut with ONE logo icon
;   5. Creates Start Menu entry
;   6. Registers uninstaller
;   7. Launches the app after install
;
; Build requirements:
;   - Inno Setup 6.x  (https://jrsoftware.org/isinfo.php)
;   - VBCABLE_Driver_Pack45.zip extracted to installer\vbcable\
;   - ONE app build output in ..\bin\Release\
;   - Resources folder with one_logo.ico, one_logo.png, 1ONEDigitalVideo.mp4
; ============================================================

#define AppName      "ONE Voice Solution"
#define AppVersion   "5.0"
#define AppPublisher "ONE United Global"
#define AppURL       "https://www.onevoicesolution.com"
#define AppExeName   "WindowsFormsApp1.exe"
#define AppId        "{{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\ONEVoiceSolution
DefaultGroupName={#AppName}
AllowNoIcons=no
; Require admin so the VB-Audio driver can install
PrivilegesRequired=admin
OutputDir=.\output
OutputBaseFilename=ONEVoiceSolution_Setup_v5
SetupIconFile=..\Resources\one_logo.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardImageFile=..\Resources\installer_banner.bmp
WizardSmallImageFile=..\Resources\installer_small.bmp
; Show ONE brand colors
WizardImageStretch=no
; Minimum Windows 10
MinVersion=10.0
; 64-bit only
ArchitecturesInstallIn64BitMode=x64
; Uninstall display icon
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName} {#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";    Description: "Create a &desktop shortcut";    GroupDescription: "Additional icons:"; Flags: checkedonce
Name: "startupicon";   Description: "Launch ONE Voice at &Windows startup"; GroupDescription: "Additional icons:"

[Files]
; ── Main application ──────────────────────────────────────────────────────────
Source: "..\bin\Release\{#AppExeName}";             DestDir: "{app}";              Flags: ignoreversion
Source: "..\bin\Release\*.dll";                     DestDir: "{app}";              Flags: ignoreversion recursesubdirs
Source: "..\bin\Release\*.config";                  DestDir: "{app}";              Flags: ignoreversion
Source: "..\bin\Release\NLog.config";               DestDir: "{app}";              Flags: ignoreversion

; ── Resources ─────────────────────────────────────────────────────────────────
Source: "..\Resources\one_logo.ico";                DestDir: "{app}\Resources";    Flags: ignoreversion
Source: "..\Resources\one_logo.png";               DestDir: "{app}\Resources";    Flags: ignoreversion
Source: "..\Resources\installer_banner.bmp";       DestDir: "{app}\Resources";    Flags: ignoreversion
Source: "..\Resources\1ONEDigitalVideo.mp4";       DestDir: "{app}\Resources";    Flags: ignoreversion skipifsourcedoesntexist

; ── VB-Audio Virtual Cable driver (bundled, silent install) ───────────────────
; Extract VBCABLE_Driver_Pack45.zip into installer\vbcable\ before building
Source: ".\vbcable\VBCABLE_Setup_x64.exe";          DestDir: "{tmp}\vbcable";      Flags: ignoreversion deleteafterinstall
Source: ".\vbcable\VBCABLE_Setup.exe";              DestDir: "{tmp}\vbcable";      Flags: ignoreversion deleteafterinstall

[Icons]
; Desktop shortcut
Name: "{autodesktop}\{#AppName}";  Filename: "{app}\{#AppExeName}";  IconFilename: "{app}\Resources\one_logo.ico";  Tasks: desktopicon
; Start Menu
Name: "{group}\{#AppName}";        Filename: "{app}\{#AppExeName}";  IconFilename: "{app}\Resources\one_logo.ico"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
; Startup (optional)
Name: "{userstartup}\{#AppName}";  Filename: "{app}\{#AppExeName}";  Tasks: startupicon

[Run]
; ── Step 1: Silently install VB-Audio Virtual Cable ───────────────────────────
; 64-bit Windows
Filename: "{tmp}\vbcable\VBCABLE_Setup_x64.exe"; \
  Parameters: "/S /norestart"; \
  StatusMsg: "Installing audio components (VB-Audio Virtual Cable)..."; \
  Flags: waituntilterminated runhidden; \
  Check: Is64BitInstallMode
; 32-bit Windows fallback
Filename: "{tmp}\vbcable\VBCABLE_Setup.exe"; \
  Parameters: "/S /norestart"; \
  StatusMsg: "Installing audio components (VB-Audio Virtual Cable)..."; \
  Flags: waituntilterminated runhidden; \
  Check: not Is64BitInstallMode

; ── Step 2: Launch ONE Voice Solution after install ───────────────────────────
Filename: "{app}\{#AppExeName}"; \
  Description: "Launch ONE Voice Solution now"; \
  Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove VB-Audio on uninstall (optional — commented out by default since
; other apps may use it; uncomment if you want full cleanup)
; Filename: "{tmp}\vbcable\VBCABLE_Setup_x64.exe"; Parameters: "/S /uninstall"; Flags: runhidden

[Registry]
; Store install path for the app to find its resources
Root: HKLM; Subkey: "SOFTWARE\ONEUnitedGlobal\OneVoiceSolution"; \
  ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; \
  Flags: uninsdeletekey

; Auto-start registry entry (only if user chose startup task)
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "ONEVoiceSolution"; \
  ValueData: """{app}\{#AppExeName}"""; \
  Tasks: startupicon; Flags: uninsdeletevalue

[Code]
// ── Check if VB-Audio Cable is already installed ──────────────────────────────
function IsVBAudioInstalled: Boolean;
var
  regValue: String;
begin
  Result := RegQueryStringValue(HKLM, 'SOFTWARE\VB-Audio\CABLE', 'DriverVersion', regValue)
         or RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\VB-Audio\CABLE', 'DriverVersion', regValue);
end;

// ── Skip VB-Audio install if already present ──────────────────────────────────
function ShouldInstallVBCable: Boolean;
begin
  Result := not IsVBAudioInstalled;
end;

// ── Custom welcome message ─────────────────────────────────────────────────────
function InitializeSetup: Boolean;
begin
  Result := True;
end;

procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel2.Caption :=
    'This will install ONE Voice Solution v5.0 on your computer.' + #13#10 + #13#10 +
    'The installer will automatically set up all required audio components.' + #13#10 +
    'No additional downloads or manual steps are needed.' + #13#10 + #13#10 +
    'Click Next to continue.';
end;

// ── Show progress message during VB-Audio install ─────────────────────────────
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    if not IsVBAudioInstalled then
      WizardForm.StatusLabel.Caption := 'Setting up audio components — this may take a moment...';
  end;
end;
