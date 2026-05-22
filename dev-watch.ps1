# ============================================================
#  ONE Voice Solution - Local Dev Watcher
#  Run this once.  Every time you save any .cs or .config file
#  it auto-rebuilds and relaunches the app.  No manual steps.
#
#  Usage:  powershell -ExecutionPolicy Bypass -File dev-watch.ps1
# ============================================================

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$msbuild     = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
$cscPath     = "$projectRoot\packages\Microsoft.Net.Compilers.3.11.0\tools"
$outExe      = "$projectRoot\bin\Release\OneApp2025.exe"
$appName     = "OneApp2025"

function Write-Color($msg, $color = "Cyan") {
    Write-Host $msg -ForegroundColor $color
}

function Build {
    Write-Color "" Yellow
    Write-Color "[DEV] Building..." Yellow
    $result = & $msbuild "$projectRoot\OneApplication.csproj" `
        /p:Configuration=Release `
        "/p:Platform=Any CPU" `
        /p:OutputPath=bin\Release\ `
        /p:CscToolPath="$cscPath" `
        /p:CscToolExe=csc.exe `
        /m /verbosity:minimal 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Color "[DEV] Build SUCCEEDED" Green
        return $true
    } else {
        Write-Color "[DEV] Build FAILED:" Red
        $result | Where-Object { $_ -match ": error " } | ForEach-Object {
            Write-Color "  $_" Red
        }
        return $false
    }
}

function Launch {
    $old = Get-Process -Name $appName -ErrorAction SilentlyContinue
    if ($old) {
        Write-Color "[DEV] Stopping old instance..." Yellow
        $old | Stop-Process -Force
        Start-Sleep -Milliseconds 800
    }
    Write-Color "[DEV] Launching app..." Green
    Start-Process $outExe -WorkingDirectory "$projectRoot\bin\Release"
}

# ── Initial build + launch ────────────────────────────────────
Write-Color "=== ONE Voice Solution - Dev Watcher ===" Magenta
Write-Color "Watching: $projectRoot" Cyan
Write-Color "Save any .cs or .config file to auto-rebuild and relaunch." Cyan
Write-Color ""

# Kill any already-running instance before the first build so logo.png is not locked
$running = Get-Process -Name $appName -ErrorAction SilentlyContinue
if ($running) {
    Write-Color "[DEV] Closing existing instance before build..." Yellow
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 800
}

if (Build) { Launch }

# ── File watcher ──────────────────────────────────────────────
$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = $projectRoot
$watcher.Filter = "*.*"
$watcher.IncludeSubdirectories = $true
$watcher.NotifyFilter = [System.IO.NotifyFilters]::LastWrite

$script:lastBuild = [datetime]::MinValue

$onChange = {
    $path = $Event.SourceEventArgs.FullPath

    # Only react to .cs and .config files; ignore bin/, packages/, obj/
    if ($path -notmatch '\.(cs|config)$') { return }
    if ($path -match '\\(bin|packages|obj)\\') { return }
    if ($path -match 'dev-watch\.ps1') { return }

    $now = [datetime]::Now
    if (($now - $script:lastBuild).TotalSeconds -lt 2) { return }
    $script:lastBuild = $now

    Write-Color "[DEV] Changed: $($Event.SourceEventArgs.Name)" Yellow

    if (Build) { Launch }
}

Register-ObjectEvent $watcher Changed -Action $onChange | Out-Null
Register-ObjectEvent $watcher Created -Action $onChange | Out-Null

$watcher.EnableRaisingEvents = $true

Write-Color ""
Write-Color "[DEV] Watcher active. Edit any .cs or .config file to trigger a rebuild." Green
Write-Color "[DEV] Press Ctrl+C to stop." Gray
Write-Color ""

try {
    while ($true) { Start-Sleep -Seconds 1 }
} finally {
    $watcher.EnableRaisingEvents = $false
    $watcher.Dispose()
    Write-Color "[DEV] Watcher stopped." Yellow
}
