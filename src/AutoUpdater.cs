using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using NLog;

namespace WindowsFormsApp1
{
    /// <summary>
    /// Checks the ONE Voice portal for updates and downloads the installer directly.
    /// Uses the portal API (not GitHub) so the download works reliably from any version.
    /// HttpClient with TLS 1.2 + redirect support — no WebClient failures.
    /// Shows a dark progress splash during download — no black cmd windows.
    /// </summary>
    internal static class AutoUpdater
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        // Portal version check endpoint — public, no auth required
        private const string VERSION_URL = "https://onevoiceapp.manus.space/api/desktop/version";

        // Shared HttpClient — handles redirects and TLS 1.2 automatically on .NET 4.8
        private static readonly HttpClient _http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect        = true,
                MaxAutomaticRedirections = 10
            };
            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent", "ONEVoiceSolution-AutoUpdater/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
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
                    // ── 1. Fetch latest version from portal ───────────────────────────
                    string json = _http.GetStringAsync(VERSION_URL).Result;
                    var info = JObject.Parse(json);

                    string remoteVer   = ((string)info["version"]     ?? string.Empty).Trim();
                    string downloadUrl = ((string)info["downloadUrl"] ?? string.Empty).Trim();

                    if (string.IsNullOrEmpty(remoteVer) || string.IsNullOrEmpty(downloadUrl))
                    {
                        Log.Warn("[AutoUpdater] Portal returned empty version or downloadUrl.");
                        return;
                    }

                    if (!IsNewer(remoteVer, currentVersion))
                        return; // already up to date

                    Log.Info($"[AutoUpdater] Update available: {currentVersion} → {remoteVer}");

                    // ── 2. Prompt user on the UI thread ────────────────────────────────
                    var form = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
                    if (form == null || form.IsDisposed)
                        return;

                    string capturedUrl = downloadUrl;
                    string capturedVer = remoteVer;

                    form.BeginInvoke(new Action(() =>
                    {
                        var result = MessageBox.Show(
                            $"A new version of ONE Voice Solution is available!\n\n" +
                            $"Current version:  {currentVersion}\n" +
                            $"New version:       {capturedVer}\n\n" +
                            "Click OK to update now.\n\n" +
                            "The app will close, install the update, and relaunch automatically.\n" +
                            "You do NOT need to touch anything — just wait.",
                            "Update Available",
                            MessageBoxButtons.OKCancel,
                            MessageBoxIcon.Information);

                        if (result != DialogResult.OK)
                            return;

                        // Show progress splash immediately
                        var splash = new UpdateProgressForm(capturedVer);
                        splash.Show(form);
                        splash.StartMarquee();
                        splash.SetStatus("Downloading update...");
                        splash.SetDetail($"Downloading ONE Voice Solution v{capturedVer}");
                        Application.DoEvents();

                        // ── 3. Download + install on background thread ─────────────────
                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            try
                            {
                                string tempPath = Path.Combine(
                                    Path.GetTempPath(), "ONEVoiceSolution_Update.exe");

                                // Stream download with progress reporting
                                var response = _http.GetAsync(
                                    capturedUrl,
                                    HttpCompletionOption.ResponseHeadersRead).Result;
                                response.EnsureSuccessStatusCode();

                                long totalBytes = response.Content.Headers.ContentLength ?? -1;

                                using (var netStream  = response.Content.ReadAsStreamAsync().Result)
                                using (var fileStream = File.Create(tempPath))
                                {
                                    byte[] buffer     = new byte[81920];
                                    long   downloaded = 0;
                                    int    read;

                                    while ((read = netStream.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        fileStream.Write(buffer, 0, read);
                                        downloaded += read;

                                        if (totalBytes > 0)
                                        {
                                            int pct = (int)(downloaded * 100 / totalBytes);
                                            try
                                            {
                                                splash.SetProgress(pct);
                                                splash.SetDetail(
                                                    $"Downloading... {pct}%  ({downloaded / 1024:N0} KB)");
                                            }
                                            catch { /* splash may be closing */ }
                                        }
                                    }
                                }

                                // Download complete — switch to install phase
                                try
                                {
                                    splash.SetProgress(100);
                                    splash.SetStatus("Installing update...");
                                    splash.SetDetail(
                                        "Please approve the Windows security prompt if it appears.");
                                    splash.StartCountdown();
                                }
                                catch { }

                                Log.Info($"[AutoUpdater] Downloaded v{capturedVer} to {tempPath}");

                                // ── 4. Write hidden batch: wait for this PID to exit, then run installer ──
                                int    pid     = Process.GetCurrentProcess().Id;
                                string batPath = Path.Combine(Path.GetTempPath(), "one_voice_update.bat");
                                File.WriteAllText(batPath,
                                    $"@echo off\r\n" +
                                    $":wait\r\n" +
                                    $"tasklist /FI \"PID eq {pid}\" 2>NUL | find /I \"{pid}\" >NUL\r\n" +
                                    $"if not errorlevel 1 (timeout /t 1 /nobreak >NUL & goto wait)\r\n" +
                                    $"\"{tempPath}\" /SILENT /NORESTART\r\n");

                                // Launch batch hidden — no black window
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName        = "cmd.exe",
                                    Arguments       = $"/C \"{batPath}\"",
                                    UseShellExecute = false,
                                    CreateNoWindow  = true
                                });

                                Log.Info("[AutoUpdater] Installer batch launched. Exiting app.");

                                // Exit so the batch can run the installer cleanly
                                form.BeginInvoke(new Action(() => Application.Exit()));
                            }
                            catch (Exception ex)
                            {
                                Log.Warn("[AutoUpdater] Download/install failed: " + ex.Message);
                                form.BeginInvoke(new Action(() =>
                                {
                                    try { splash.Close(); splash.Dispose(); } catch { }
                                    MessageBox.Show(
                                        "Update failed. Please download the latest version manually " +
                                        "from your member portal.\n\nError: " + ex.Message,
                                        "Update Failed",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Warning);
                                }));
                            }
                        });
                    }));
                }
                catch (Exception ex)
                {
                    Log.Warn("[AutoUpdater] Version check failed: " + ex.Message);
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
