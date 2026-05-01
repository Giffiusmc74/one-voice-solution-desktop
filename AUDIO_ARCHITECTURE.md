# ONE Voice Solution — Master Audio Architecture

**Last updated: v7.66**
**This document is the source of truth for all audio routing decisions.**
Do not change audio routing without reading this document first.

---

## The Final Working Architecture (v7.66)

### Device Assignments

| Component | Device | Purpose |
|---|---|---|
| Desktop App — Mic | Agent headset (e.g., Jabra) | Mic pass-through |
| Desktop App — Speaker | Agent headset (e.g., Jabra) | `waveO` monitor output |
| Portal (browser) — Mic | Agent headset (e.g., Jabra) | Recording input |
| Portal (browser) — Output | VB Audio Cable (CABLE Input) | Card playback to customer |
| Softphone — Mic | VB Audio Cable (CABLE Output) | Receives card audio from portal |
| Softphone — Speaker | Agent headset (e.g., Jabra) | Agent hears customer |
| Windows Default Playback | Agent headset | System audio |
| Windows Default Recording | Agent headset | System mic |

---

## Audio Flow — Card Playback

```
Portal (browser)
    │
    │  plays card audio OUT through selected output device
    ▼
VB Audio Cable (CABLE Input)
    │
    │  VB Cable loops internally
    ▼
CABLE Output  ←── Softphone mic is set to CABLE Output
    │
    │  softphone sends it into the active call
    ▼
Customer hears the recording ✅

LocalBridgeServer.cs — waveOut (VB Cable path)
    │
    │  WaveOutEvent { DeviceNumber = _cableDeviceNumber }
    │  MeteringSampleProvider fires StreamVolume every 1024 samples
    ▼
Green meter moves (agent script level) ✅
Blue meter moves (customer script level) ✅

LocalBridgeServer.cs — waveO (Agent monitor path)
    │
    │  WaveOutEvent { DeviceNumber = _agentDeviceNumber }
    │  AudioFileReader2 — same temp file, separate reader
    ▼
Agent headset speaker — agent hears the card ✅
```

---

## Audio Flow — Red Meter (Customer Voice)

```
Customer speaks
    │
    │  audio travels through the phone call
    ▼
Softphone speaker output → Agent headset (Jabra)
    │
    │  Jabra has a SEPARATE mic input (USB headset)
    ▼
WasapiCapture(_activeMicDevice, false)
    │
    │  Captures ONLY what comes INTO the Jabra mic
    │  (customer voice from softphone)
    │  WaveFormat forced to IEEE float 16000Hz mono
    ▼
DataAvailable handler → _customerVoiceLevel
    │
    ▼
Red meter moves ✅

Card audio plays OUT through Jabra speaker
    │
    │  This is NOT captured by WasapiCapture (mic-only)
    ▼
Red meter stays at 0 during card playback ✅
```

---

## Why WasapiCapture (NOT WasapiLoopbackCapture)

**WasapiLoopbackCapture** captures everything playing OUT through a render device (speaker).
This means it captures BOTH customer voice (via softphone) AND card audio (via waveO).
Result: red meter bleeds during card playback. ❌

**WasapiCapture** on the Jabra mic device captures ONLY what comes INTO the mic.
The softphone sends customer voice to the Jabra speaker → the agent speaks into the mic → the mic captures customer voice only.
Card audio playing OUT through the Jabra speaker is never captured by the mic.
Result: red meter = customer voice only. ✅

**Critical requirement:** The Jabra (or any headset used) MUST be a USB headset with SEPARATE mic and speaker devices in Windows. A single combined device would not work.

---

## Why waveO is Necessary

The card plays to VB Cable (customer hears it). The softphone's speaker plays back what comes FROM the customer — not what goes INTO the mic. Most softphones do not echo your own mic input back to your speaker. So without waveO, the agent never hears the card.

waveO → Jabra speaker is safe because the red meter uses WasapiCapture (mic-only), not loopback. The speaker output is never captured.

---

## Code Locations

| What | File | Key lines |
|---|---|---|
| VB Cable playback (waveOut) | `src/LocalBridgeServer.cs` | `HandlePlay()` — VB-Cable block |
| Agent monitor playback (waveO) | `src/LocalBridgeServer.cs` | `HandlePlay()` — Agent headset block |
| Green/blue meter source | `src/LocalBridgeServer.cs` | `MeteringSampleProvider.StreamVolume` |
| Red meter source | `src/MainFormV5.cs` | `StartLoopbackCapture()` |
| Device assignment — cable | `src/MainFormV5.cs` | `PopulateDevices()` → `SetCableDevice()` |
| Device assignment — agent | `src/MainFormV5.cs` | `PopulateDevices()` → `SetAgentDevice()` |
| Device assignment — mic | `src/MainFormV5.cs` | `PopulateDevices()` → `_activeMicDevice` |

---

## Volume Controls

| Slider | Channel | Controls |
|---|---|---|
| Customer volume | `"customer"` | `audioFileReader.Volume` (VB Cable stream) |
| Agent volume | `"agent"` | `audioFileReader2.Volume` (waveO stream) |
| Speaker volume | `_customerVoiceVolume` | Multiplier on red meter level |

Volume is applied via `AudioFileReader.Volume` (software-level float 0.0–1.0).
`WaveOutEvent.Volume` is NOT used — it is ignored by many drivers including Jabra.

---

## Version History of Audio Changes

| Version | Change | Result |
|---|---|---|
| v7.58 | `waveO` → Jabra, `WasapiLoopbackCapture()` (default device) | Working but `IsPlaying` guard needed to suppress red bleed |
| v7.64 | Removed `waveO`; `WasapiLoopbackCapture(_activeSpeakerDevice)` | Red clean ✅ but agent can't hear cards ❌ |
| v7.65 | Added `MeteringSampleProvider` for green meter | Green meter fix attempt; agent still can't hear cards ❌ |
| v7.66 | `waveO` back → Jabra; `WasapiCapture(_activeMicDevice)` for red; forced float format | Red clean ✅, agent hears cards ✅, green meter ✅ |

---

## Rules — Never Break These

1. **Card audio must go to VB Cable.** `waveOut` always targets `_cableDeviceNumber`. Never fall back to device -1 (default = Jabra).
2. **Red meter = WasapiCapture on Jabra mic.** Never switch back to WasapiLoopbackCapture unless waveO is removed.
3. **waveO targets agent headset only.** Never target VB Cable or any device that is also the loopback source.
4. **WaveFormat forced to IEEE float 16000Hz mono** on WasapiCapture. Do not remove this — it eliminates format branching.
5. **IsPlaying guard is NOT present.** It was a band-aid. Do not add it back.
6. **VB Cable is installed automatically** by the ONE Voice Solution installer. It is always present. The "not found" abort path is a safety net only.

---

## 1-Computer Personal Setup (Gold Standard)

This is the verified device configuration for an agent running everything on one personal computer.

| Component | Mic | Output/Speaker |
|---|---|---|
| Desktop App | Your headset (e.g., Jabra) | Your headset (e.g., Jabra) |
| Portal (browser) | Your headset (e.g., Jabra) | VB Audio Cable (CABLE Input) |
| Softphone | VB Audio Cable (CABLE Output) | Your headset (e.g., Jabra) |
| Windows | Your headset | Your headset |

**One line:** "Portal records with your headset, plays through VB Cable — everything else stays on your headset."

---

## 2-Computer Workaround Setup

For agents whose employer's IT department blocks software installs on the work computer.
See `chatRouter.ts` → `=== WORKAROUND SETUP — FULL GUIDE ===` for full details.
Equipment: Focusrite Scarlett 4i4 (4th Gen only) + Plugable USB Audio Adapter + 1/4" TRS to 3.5mm cable.

---

*This document must be updated whenever audio routing changes are made.*
