using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using NLog;

namespace WindowsFormsApp1
{
    /// <summary>
    /// Checks GitHub Releases for a newer version of ONE Voice Solution.
    /// Shows a dark-themed UpdateProgressForm during download + install.
    /// No visible command prompt windows. UAC prompt appears on top naturally.
    /// </summary>
    internal static class AutoUpdater
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private const string GITHUB_OWNER = "Giffiusmc74";
        private const string GITHUB_REPO  = "one-voice-solution-desktop";
        private static readonly string API_URL =
            "https://api.github.com/repos/" + GITHUB_OWNER + "/" + GITHUB_REPO + "/releases/latest";

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
                    using (var wc = new WebClient())
                    {
                        wc.Headers[HttpRequestHeader.UserAgent] = "ONEVoiceSolution-AutoUpdater/1.0";

                        string json = wc.DownloadString(API_URL);
                        var release = JObject.Parse(json);

                        string tagName   = (string)release["tag_name"] ?? string.Empty;
                        string remoteVer = tagName.TrimStart('v');

                        if (!IsNewer(remoteVer, currentVersion))
                            return;  // already up to date

                        // Find the .exe installer asset
                        string downloadUrl = null;
                        var assets = release["assets"] as JArray;
                        if (assets != null)
                        {
                            foreach (JObject asset in assets)
                            {
                                string name = (string)asset["name"] ?? string.Empty;
                                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    downloadUrl = (string)asset["browser_download_url"];
                                    break;
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(downloadUrl))
                            return;

                        // Show update dialog on the UI thread
                        var form = Application.OpenForms.Count > 0
                            ? Application.OpenForms[0] : null;

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
                                "You do NOT need to touch anything — just wait and it will come back on its own.",
                                "Update Available",
                                MessageBoxButtons.OKCancel,
                                MessageBoxIcon.Information);

                            if (result != DialogResult.OK)
                                return;

                            // Show the progress splash immediately on the UI thread
                            var splash = new UpdateProgressForm(capturedVer);
                            splash.Show(form);
                            splash.StartMarquee();
                            splash.SetStatus("Downloading update...");
                            splash.SetDetail($"Downloading ONE Voice Solution v{capturedVer}");
                            Application.DoEvents();

                            // Download + install on background thread
                            ThreadPool.QueueUserWorkItem(delegate
                            {
                                try
                                {
                                    string tempPath = Path.Combine(
                                        Path.GetTempPath(), "ONEVoiceSolution_Update.exe");

                                    // Download with progress
                                    using (var dlClient = new WebClient())
                                    {
                                        dlClient.Headers[HttpRequestHeader.UserAgent] = "ONEVoiceSolution-AutoUpdater/1.0";

                                        dlClient.DownloadProgressChanged += (s, e) =>
                                        {
                                            try
                                            {
                                                splash.SetProgress(e.ProgressPercentage);
                                                splash.SetDetail($"Downloading... {e.ProgressPercentage}%  ({e.BytesReceived / 1024:N0} KB)");
                                            }
                                            catch { }
                                        };

                                        dlClient.DownloadFileCompleted += (s, e) =>
                                        {
                                            try
                                            {
                                                splash.SetProgress(100);
                                                splash.SetStatus("Installing update...");
                                                splash.SetDetail("Please approve the Windows security prompt if it appears.");
                                                splash.StartCountdown();
                                            }
                                            catch { }
                                        };

                                        // Synchronous download so we can sequence the install after
                                        dlClient.DownloadFile(capturedUrl, tempPath);
                                    }

                                    Log.Info($"[AutoUpdater] Downloaded v{capturedVer} to {tempPath}");

                                    // Write batch: wait for this process to exit, then run installer silently.
                                    // /SILENT shows the Inno Setup progress window (handles UAC on top).
                                    // /NORESTART prevents automatic reboot.
                                    int pid = Process.GetCurrentProcess().Id;
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

                                    // Exit so the batch file can run the installer cleanly
                                    form.BeginInvoke(new Action(() => Application.Exit()));
                                }
                                catch (Exception ex)
                                {
                                    Log.Warn("[AutoUpdater] Download/install failed: " + ex.Message);
                                    form.BeginInvoke(new Action(() =>
                                    {
                                        try { splash.Close(); splash.Dispose(); } catch { }
                                        MessageBox.Show(
                                            "Update failed. Please download the latest version manually from your member portal.\n\nError: " + ex.Message,
                                            "Update Failed",
                                            MessageBoxButtons.OK,
                                            MessageBoxIcon.Warning);
                                    }));
                                }
                            });
                        }));
                    }
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
