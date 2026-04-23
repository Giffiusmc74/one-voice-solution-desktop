# ONE Voice Solution — Audio Configuration Script
# Sets VB-Audio CABLE Input and CABLE Output to 16-bit, 48000 Hz
# This prevents sample rate mismatch that causes sloppy/distorted audio on calls

$ErrorActionPreference = "SilentlyContinue"

# Load the Windows Audio API via COM
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator {
    int NotImpl1();
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IntPtr ppDevice);
    int GetDevice(string pwstrId, out IntPtr ppDevice);
    int RegisterEndpointNotificationCallback(IntPtr pClient);
    int UnregisterEndpointNotificationCallback(IntPtr pClient);
    int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
}
"@ 2>$null

# Use registry approach — more reliable across Windows versions
# CABLE Input is a playback (render) device, CABLE Output is a capture (recording) device
# Windows stores audio format preferences in registry

function Set-AudioDeviceFormat {
    param([string]$DeviceName, [string]$Flow)
    
    # Search registry for the device
    $baseKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio"
    $flowKey = if ($Flow -eq "Render") { "$baseKey\Render" } else { "$baseKey\Capture" }
    
    if (-not (Test-Path $flowKey)) { return }
    
    $devices = Get-ChildItem $flowKey -ErrorAction SilentlyContinue
    foreach ($device in $devices) {
        $props = Get-ItemProperty "$($device.PSPath)\Properties" -ErrorAction SilentlyContinue
        if ($props) {
            # Property key for device friendly name: {a45c254e-df1c-4efd-8020-67d146a850e0},14
            $nameProp = $props."{a45c254e-df1c-4efd-8020-67d146a850e0},14"
            if ($nameProp -and $nameProp -like "*$DeviceName*") {
                # Set format: 16-bit, 48000 Hz, 2 channel
                # WAVEFORMATEX binary: wFormatTag=1(PCM), nChannels=2, nSamplesPerSec=48000,
                #   nAvgBytesPerSec=192000, nBlockAlign=4, wBitsPerSample=16, cbSize=0
                # Stored as WAVEFORMATEXTENSIBLE (18 bytes for WAVEFORMATEX + 22 bytes ext = 40 bytes)
                # Simpler: just set the DeviceFormat registry value
                $formatBytes = [byte[]](
                    0xFE, 0xFF,             # wFormatTag = WAVE_FORMAT_EXTENSIBLE (0xFFFE)
                    0x02, 0x00,             # nChannels = 2
                    0x80, 0xBB, 0x00, 0x00, # nSamplesPerSec = 48000
                    0x00, 0xEE, 0x02, 0x00, # nAvgBytesPerSec = 192000
                    0x04, 0x00,             # nBlockAlign = 4
                    0x10, 0x00,             # wBitsPerSample = 16
                    0x16, 0x00,             # cbSize = 22
                    0x10, 0x00,             # wValidBitsPerSample = 16
                    0x03, 0x00, 0x00, 0x00, # dwChannelMask = SPEAKER_FRONT_LEFT | SPEAKER_FRONT_RIGHT
                    0x01, 0x00, 0x00, 0x00, # SubFormat GUID (PCM): 00000001-0000-0010-8000-00aa00389b71
                    0x00, 0x00, 0x10, 0x00,
                    0x80, 0x00, 0x00, 0xAA,
                    0x00, 0x38, 0x9B, 0x71
                )
                
                Set-ItemProperty -Path "$($device.PSPath)" -Name "DeviceFormat" -Value $formatBytes -Type Binary -ErrorAction SilentlyContinue
                Write-Host "Configured $DeviceName ($Flow) to 16-bit 48000 Hz"
            }
        }
    }
}

# Configure CABLE Input (Playback/Render device)
Set-AudioDeviceFormat -DeviceName "CABLE Input" -Flow "Render"

# Configure CABLE Output (Recording/Capture device)  
Set-AudioDeviceFormat -DeviceName "CABLE Output" -Flow "Capture"

Write-Host "ONE Voice audio configuration complete."
