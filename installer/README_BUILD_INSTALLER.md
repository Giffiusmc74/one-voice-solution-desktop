# ONE Voice Solution v5.0 — Installer Build Guide

## Prerequisites

| Tool | Download |
|---|---|
| Inno Setup 6.x | https://jrsoftware.org/isinfo.php |
| VB-Audio Virtual Cable driver | https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip |

## Steps to Build the Installer

### 1. Build the ONE Voice Solution app in Release mode
In Visual Studio: **Build → Build Solution** (set to `Release | x64`)

Output will be in: `..\bin\Release\`

### 2. Download and extract VB-Audio Cable driver
1. Download `VBCABLE_Driver_Pack45.zip` from the link above
2. Extract it into: `installer\vbcable\`
3. Confirm these files exist:
   - `installer\vbcable\VBCABLE_Setup_x64.exe` (64-bit)
   - `installer\vbcable\VBCABLE_Setup.exe` (32-bit fallback)

### 3. Confirm Resources folder has these files
```
Resources\
  one_logo.ico          ← Multi-resolution icon (16/32/48/256px)
  one_logo.png          ← ONE logo PNG
  1ONEDigitalVideo.mp4  ← Looping brand video
  installer_banner.bmp  ← 497x314 installer banner (optional)
  installer_small.bmp   ← 55x58 small installer image (optional)
```

### 4. Build the installer
1. Open `ONEVoiceSolution_Setup.iss` in Inno Setup Compiler
2. Press **F9** (or Build → Compile)
3. Output: `installer\output\ONEVoiceSolution_Setup_v5.exe`

## What the Installer Does (Agent Experience)

1. Agent downloads `ONEVoiceSolution_Setup_v5.exe` — one file, ~50MB
2. Runs it — sees ONE-branded wizard
3. Clicks **Next → Install**
4. Installer silently installs VB-Audio Virtual Cable in the background
5. Installs ONE Voice Solution to `C:\Program Files\ONEVoiceSolution\`
6. Creates desktop shortcut with ONE logo icon
7. Launches ONE Voice Solution automatically
8. **Agent never sees a separate driver install step**

## VB-Audio License Note
VB-Audio Virtual Cable is freeware and may be bundled in third-party installers at no cost.
See: https://vb-audio.com/Cable/index.htm

## Uninstall
Standard Windows Add/Remove Programs uninstall. VB-Audio Cable is left installed
(other apps may depend on it). To force-remove it, uncomment the `[UninstallRun]`
section in the `.iss` file.
