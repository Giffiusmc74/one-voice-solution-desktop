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
    /// If a newer version is found, shows a visible update dialog so the user
    /// can approve the update. The installer handles its own UAC elevation.
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

                        form.BeginInvoke(new Action(() =>
                        {
                            var result = MessageBox.Show(
                                $"A new version of ONE Voice Solution is available!\n\n" +
                                $"Current version:  {currentVersion}\n" +
                                $"New version:       {remoteVer}\n\n" +
                                "Click OK to download and install the update now.\n" +
                                "The app will restart automatically.",
                                "Update Available",
                                MessageBoxButtons.OKCancel,
                                MessageBoxIcon.Information);

                            if (result != DialogResult.OK)
                                return;

                            // Download on background thread, then install
                            ThreadPool.QueueUserWorkItem(delegate
                            {
                                try
                                {
                                    string tempPath = Path.Combine(
                                        Path.GetTempPath(), "ONEVoiceSolution_Update.exe");

                                    // Show progress on UI thread
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

                                    using (var dlClient = new WebClient())
                                    {
                                        dlClient.Headers[HttpRequestHeader.UserAgent] = "ONEVoiceSolution-AutoUpdater/1.0";
                                        dlClient.DownloadFile(downloadUrl, tempPath);
                                    }

                                    // Write batch: wait for this process to exit, then run installer.
                                    // Inno Setup requests its own UAC elevation — no runas needed here.
                                    int pid = Process.GetCurrentProcess().Id;
                                    string batPath = Path.Combine(Path.GetTempPath(), "one_voice_update.bat");
                                    File.WriteAllText(batPath,
                                        $"@echo off\r\n" +
                                        $":wait\r\n" +
                                        $"tasklist /FI \"PID eq {pid}\" 2>NUL | find /I \"{pid}\" >NUL\r\n" +
                                        $"if not errorlevel 1 (timeout /t 1 /nobreak >NUL & goto wait)\r\n" +
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
