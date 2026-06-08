using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using NLog;
namespace WindowsFormsApp1
{
    /// <summary>
    /// Checks the ONE Voice portal for a newer version of ONE Voice Solution.
    /// Uses the portal's /api/desktop/version endpoint (returns { version, downloadUrl })
    /// so the download URL always points to the Manus CDN — no GitHub redirects,
    /// no SmartScreen issues, no rate limits.
    ///
    /// If a newer version is found, shows a visible update dialog so the user
    /// can approve the update. The installer handles its own UAC elevation.
    /// </summary>
    internal static class AutoUpdater
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        // Portal version endpoint — returns { version, downloadUrl, releaseName }
        private const string VERSION_URL = "https://api.onevoicesolution.com/api/desktop/version";
        // Shared HttpClient — reuse across calls (thread-safe)
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        static AutoUpdater()
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "ONEVoiceSolution-AutoUpdater/1.0");
        }
        /// <summary>
        /// Fire-and-forget — safe to call from the UI thread.
        /// All network I/O runs on a background thread.
        /// </summary>
        public static void CheckAndUpdate(string currentVersion)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    // ── 1. Fetch version info from portal ──────────────────────────────────
                    string json;
                    try
                    {
                        var task = _http.GetStringAsync(VERSION_URL);
                        task.Wait();
                        json = task.Result;
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("[AutoUpdater] Version check failed: " + ex.Message);
                        return;
                    }
                    JObject info;
                    try { info = JObject.Parse(json); }
                    catch { Log.Warn("[AutoUpdater] Could not parse version response."); return; }
                    string remoteVer  = ((string)info["version"]     ?? string.Empty).Trim();
                    string downloadUrl = (string)info["downloadUrl"] ?? string.Empty;
                    if (string.IsNullOrEmpty(remoteVer) || string.IsNullOrEmpty(downloadUrl))
                    {
                        Log.Warn("[AutoUpdater] Version response missing fields.");
                        return;
                    }
                    if (!IsNewer(remoteVer, currentVersion))
                        return;  // already up to date
                    // ── 2. Show update dialog on UI thread ─────────────────────────────────
                    var form = Application.OpenForms.Count > 0
                        ? Application.OpenForms[0] : null;
                    if (form == null || form.IsDisposed)
                        return;
                    form.BeginInvoke(new Action(() =>
                    {
                        var result = MessageBox.Show(
                            $"A new version of ONE Voice Solution is available!\n\n" +
                            $"Current version:  {currentVersion}\n" +
                            $"New version:       {remoteVer}\n\n" +
                            "Click OK to update now.\n\n" +
                            "The app will close, install the update, and relaunch automatically.\n" +
                            "You do NOT need to touch anything — just wait 30-45 seconds and it will come back on its own.",
                            "Update Available",
                            MessageBoxButtons.OKCancel,
                            MessageBoxIcon.Information);
                        if (result != DialogResult.OK)
                            return;
                        // ── 3. Download on background thread, then install ─────────────────
                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            try
                            {
                                // Use a unique filename to avoid any file-lock from a previous
                                // failed attempt (Windows may hold the old file briefly).
                                string tempDir  = Path.GetTempPath();
                                string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
                                string tempPath = Path.Combine(tempDir, $"ONEVoiceSolution_Update_{uniqueId}.exe");

                                // Clean up any leftover update files from previous attempts
                                try
                                {
                                    foreach (var old in Directory.GetFiles(tempDir, "ONEVoiceSolution_Update_*.exe"))
                                    {
                                        try { File.Delete(old); } catch { /* ignore locked files */ }
                                    }
                                    // Also clean up the old fixed name if present
                                    string oldFixed = Path.Combine(tempDir, "ONEVoiceSolution_Update.exe");
                                    if (File.Exists(oldFixed))
                                        try { File.Delete(oldFixed); } catch { /* ignore */ }
                                }
                                catch { /* cleanup is best-effort */ }

                                // Show balloon tip
                                form.BeginInvoke(new Action(() =>
                                {
                                    try
                                    {
                                        var notify = new NotifyIcon
                                        {
                                            Icon    = System.Drawing.SystemIcons.Information,
                                            Visible = true
                                        };
                                        notify.ShowBalloonTip(5000, "ONE Voice Solution",
                                            $"Downloading update v{remoteVer}... The app will restart when done.",
                                            ToolTipIcon.Info);
                                        Thread.Sleep(1000);
                                        notify.Visible = false;
                                        notify.Dispose();
                                    }
                                    catch { }
                                }));

                                // Download via HttpClient — handles TLS + redirects correctly
                                var dlTask = _http.GetByteArrayAsync(downloadUrl);
                                dlTask.Wait();
                                File.WriteAllBytes(tempPath, dlTask.Result);

                                // Write batch: wait for this process to exit, then run installer.
                                // Inno Setup requests its own UAC elevation — no runas needed here.
                                int pid = Process.GetCurrentProcess().Id;
                                string batPath = Path.Combine(tempDir, "one_voice_update.bat");
                                File.WriteAllText(batPath,
                                    $"@echo off\r\n" +
                                    $":wait\r\n" +
                                    $"tasklist /FI \"PID eq {pid}\" 2>NUL | find /I \"{pid}\" >NUL\r\n" +
                                    $"if not errorlevel 1 (timeout /t 1 /nobreak >NUL & goto wait)\r\n" +
                                    $"timeout /t 2 /nobreak >NUL\r\n" +
                                    $"\"{tempPath}\" /VERYSILENT /NORESTART\r\n");

                                Process.Start(new ProcessStartInfo
                                {
                                    FileName        = "cmd.exe",
                                    Arguments       = $"/C \"{batPath}\"",
                                    UseShellExecute = true,
                                    WindowStyle     = ProcessWindowStyle.Normal
                                    // No Verb = "runas" — installer handles its own elevation
                                });

                                // Exit so the batch file can run the installer cleanly
                                form.BeginInvoke(new Action(() => Application.Exit()));
                            }
                            catch (Exception ex)
                            {
                                Log.Warn("[AutoUpdater] Download failed: " + ex.Message);
                                form.BeginInvoke(new Action(() =>
                                    MessageBox.Show(
                                        "Update download failed. Please download the latest version manually from your member portal.\n\nError: " + ex.Message,
                                        "Update Failed",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Warning)));
                            }
                        });
                    }));
                }
                catch (Exception ex)
                {
                    Log.Warn("[AutoUpdater] Unexpected error: " + ex.Message);
                }
            });
        }
        private static bool IsNewer(string remote, string current)
        {
            Version r, c;
            if (Version.TryParse(remote, out r) && Version.TryParse(current, out c))
                return r > c;
            return string.Compare(remote, current, StringComparison.OrdinalIgnoreCase) > 0;
        }
    }
}
