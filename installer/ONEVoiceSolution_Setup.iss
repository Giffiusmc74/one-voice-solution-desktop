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
#define AppVersion   "BUILD_VERSION_PLACEHOLDER"
#define AppPublisher "ONE United Global"
#define AppURL       "https://www.onevoicesolution.com"
#define AppExeName   "OneApp2025.exe"
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
OutputBaseFilename=ONEVoiceSolution_Setup
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
Source: "..\Resources\one_logo.png";                DestDir: "{app}\Resources";    Flags: ignoreversion
Source: "..\Resources\installer_banner.bmp";        DestDir: "{app}\Resources";    Flags: ignoreversion
Source: "..\Resources\1ONEDigitalVideo.mp4";        DestDir: "{app}\Resources";    Flags: ignoreversion skipifsourcedoesntexist

; ── Logo for in-app header (res folder) ───────────────────────────────────────
Source: "..\res\logo.png";                          DestDir: "{app}\res";          Flags: ignoreversion skipifsourcedoesntexist

; ── Audio configuration script ────────────────────────────────────────────────
Source: ".\ConfigureAudio.ps1";                     DestDir: "{tmp}";              Flags: ignoreversion deleteafterinstall

; ── VB-Audio Virtual Cable driver (bundled, silent install) ───────────────────
; Extract VBCABLE_Driver_Pack45.zip into installer\vbcable\ before building
; ALL files in the vbcable folder must be bundled — the .inf and driver files
; must be present alongside the .exe or the installer will error with "Missing inf"
Source: ".\vbcable\*";                              DestDir: "{tmp}\vbcable";      Flags: ignoreversion recursesubdirs createallsubdirs deleteafterinstall

[Icons]
; Desktop shortcut
Name: "{autodesktop}\{#AppName}";  Filename: "{app}\{#AppExeName}";  IconFilename: "{app}\Resources\one_logo.ico";  Tasks: desktopicon
; Start Menu
Name: "{group}\{#AppName}";        Filename: "{app}\{#AppExeName}";  IconFilename: "{app}\Resources\one_logo.ico"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
; Startup shortcut removed — registry Run key (below) is used instead to prevent dual-launch

[Run]
; ── Step 1: Install VB-Audio Virtual Cable ────────────────────────────────────
;
; §Giff 08-31 — THIS STEP USED TO HANG FOREVER, and did so on every install.
;
; It ran the driver setup with `Parameters: "/S /norestart"` and `Flags: waituntilterminated runhidden`.
; Two problems, and together they deadlock:
;   1. `/S` is the NSIS silent switch. VB-CABLE's setup is not NSIS and ignores it, so it opens its normal
;      GUI and waits for the user to click "Install Driver". The vendor's own switches are `-i` (install)
;      and `-h` (hidden/automatic).
;   2. `runhidden` then HID that GUI. So Inno hid a dialog that requires a click, and `waituntilterminated`
;      blocked on it indefinitely. Observed live on 2026-08-31: VBCABLE_Setup_x64.exe sitting at 0.03s CPU
;      with an invisible window titled "VB-Audio Virtual Cable Driver Installation (Version 2.1.5.8)".
;      VB-Cable was never actually installed by ANY previous run of this installer.
;
; Now runs through a PowerShell wrapper that enforces a hard timeout, so this step can never hang again:
; if the driver setup has not finished in 3 minutes it is killed, the installer continues, and the user is
; told rather than staring at a frozen progress bar. Exit code 3 means "timed out" — see CurStepChanged.
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -NonInteractive -WindowStyle Hidden -Command ""$p = Start-Process -FilePath '{tmp}\vbcable\VBCABLE_Setup_x64.exe' -ArgumentList '-i','-h' -PassThru; if (-not $p.WaitForExit(180000)) {{ try {{ $p.Kill() }} catch {{}} ; exit 3 }} ; exit $p.ExitCode"""; \
  StatusMsg: "Installing audio components (VB-Audio Virtual Cable)..."; \
  Flags: waituntilterminated runhidden; \
  Check: ShouldInstallVBCable and Is64BitInstallMode

; 32-bit Windows fallback
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -NonInteractive -WindowStyle Hidden -Command ""$p = Start-Process -FilePath '{tmp}\vbcable\VBCABLE_Setup.exe' -ArgumentList '-i','-h' -PassThru; if (-not $p.WaitForExit(180000)) {{ try {{ $p.Kill() }} catch {{}} ; exit 3 }} ; exit $p.ExitCode"""; \
  StatusMsg: "Installing audio components (VB-Audio Virtual Cable)..."; \
  Flags: waituntilterminated runhidden; \
  Check: ShouldInstallVBCable and not Is64BitInstallMode

; ── Step 2: Configure VB-Audio CABLE to 16-bit 48000 Hz ──────────────────────
Filename: "powershell.exe"; \
  Parameters: "-ExecutionPolicy Bypass -NonInteractive -WindowStyle Hidden -File ""{tmp}\ConfigureAudio.ps1"""; \
  StatusMsg: "Configuring audio settings..."; \
  Flags: waituntilterminated runhidden

; ── Step 3: Launch ONE Voice Solution after install ───────────────────────────
; NOTE: No skipifsilent flag — app ALWAYS relaunches after silent auto-update
Filename: "{app}\{#AppExeName}"; \
  Description: "Launch ONE Voice Solution now"; \
  Flags: nowait postinstall

[UninstallRun]
; Remove VB-Audio on uninstall (optional — commented out by default since
; other apps may use it; uncomment if you want full cleanup)
; Filename: "{tmp}\vbcable\VBCABLE_Setup_x64.exe"; Parameters: "/S /uninstall"; Flags: runhidden

[Registry]
; Store install path for the app to find its resources
Root: HKLM; Subkey: "SOFTWARE\ONEUnitedGlobal\OneVoiceSolution"; \
  ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; \
  Flags: uninsdeletekey

; ── one-voice:// custom URL protocol handler ─────────────────────────────────
; Allows the web portal to launch the app via one-voice://launch?key=...
Root: HKCR; Subkey: "one-voice"; \
  ValueType: string; ValueName: ""; ValueData: "URL:ONE Voice Solution"; \
  Flags: uninsdeletekey
Root: HKCR; Subkey: "one-voice"; \
  ValueType: string; ValueName: "URL Protocol"; ValueData: ""; \
  Flags: uninsdeletevalue
Root: HKCR; Subkey: "one-voice\DefaultIcon"; \
  ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName},0"; \
  Flags: uninsdeletekey
Root: HKCR; Subkey: "one-voice\shell\open\command"; \
  ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; \
  Flags: uninsdeletekey

; Auto-start registry entry (only if user chose startup task)
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "ONEVoiceSolution"; \
  ValueData: """{app}\{#AppExeName}"""; \
  Tasks: startupicon; Flags: uninsdeletevalue

[Code]
// ── Check if VB-Audio Cable is already installed ──────────────────────────────
// §Giff 08-31 — widened beyond the registry. VB-CABLE 2.1.5.8 does not reliably write
// SOFTWARE\VB-Audio\CABLE, so a machine that already had the driver would run the installer again anyway.
// The driver .sys in the system directory is present on every real install, so check that too.
function IsVBAudioInstalled: Boolean;
var
  regValue: String;
begin
  Result := RegQueryStringValue(HKLM, 'SOFTWARE\VB-Audio\CABLE', 'DriverVersion', regValue)
         or RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\VB-Audio\CABLE', 'DriverVersion', regValue)
         or FileExists(ExpandConstant('{sys}\drivers\vbaudio_cable64_win10.sys'))
         or FileExists(ExpandConstant('{sys}\drivers\vbaudio_cable64_win7.sys'))
         or FileExists(ExpandConstant('{sys}\drivers\vbaudio_cable_win7.sys'));
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
    'Welcome to ONE Voice Solution!' + #13#10 + #13#10 +

    '*** IMPORTANT — IF WINDOWS SHOWS A WARNING SCREEN ***' + #13#10 +
    'Windows may show a blue screen saying "Windows protected your PC".' + #13#10 +
    'This is normal for new software. Here is exactly what to do:' + #13#10 +
    '  1. Click "More info" (small link in the middle of the screen)' + #13#10 +
    '  2. Click "Run anyway" (button that appears at the bottom)' + #13#10 +
    '  DO NOT click "Don''t run" — that will cancel the install.' + #13#10 + #13#10 +

    'The installer will automatically set up all required audio' + #13#10 +
    'components. No additional downloads or manual steps are needed.' + #13#10 + #13#10 +
    'Click Next to continue.';
end;

// ── Show progress message during VB-Audio install ─────────────────────────────
procedure CurStepChanged(CurStep: TSetupStep);
var
  startupShortcut: String;
begin
  if CurStep = ssInstall then
  begin
    if not IsVBAudioInstalled then
      WizardForm.StatusLabel.Caption := 'Setting up audio components — this may take a moment...';

    // Remove old Startup folder shortcut that caused dual-instance launches.
    // The registry Run key is used instead (single entry = single launch).
    startupShortcut := ExpandConstant('{userstartup}\ONE Voice Solution.lnk');
    if FileExists(startupShortcut) then
      DeleteFile(startupShortcut);
  end;

  // ── Tell the truth about the driver at the end (§Giff 08-31) ────────────────
  // The VB-Cable step is now time-bounded, so it can legitimately finish WITHOUT having installed the
  // driver. Silence would be worse than the old hang: the agent opens the app, finds no CABLE device, and
  // has nothing to go on. Re-check and say plainly what happened.
  if CurStep = ssPostInstall then
  begin
    if not IsVBAudioInstalled then
      MsgBox('ONE Voice Solution installed successfully.' + #13#10 + #13#10 +
             'The VB-Audio Virtual Cable driver did NOT finish installing. The app is ready to use, but ' +
             'card audio will not reach your dialer until that driver is present.' + #13#10 + #13#10 +
             'To finish it, run this once and click "Install Driver":' + #13#10 +
             ExpandConstant('{app}\vbcable\VBCABLE_Setup_x64.exe') + #13#10 + #13#10 +
             'Then reboot. If it still fails, send this message to support.',
             mbInformation, MB_OK);
  end;
end;
