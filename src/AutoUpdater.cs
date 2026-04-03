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
    /// If found, silently downloads the installer, exits the current process,
    /// then launches the installer — preventing two windows from appearing.
    /// Runs entirely on a background thread — never blocks the UI.
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
        /// All network I/O runs on a ThreadPool thread.
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

                        // Download installer to temp folder
                        string tempPath = Path.Combine(
                            Path.GetTempPath(), "ONEVoiceSolution_Update.exe");
                        wc.DownloadFile(downloadUrl, tempPath);

                        // ── CRITICAL: Exit THIS process BEFORE launching the installer.
                        // If we launch the installer first (with /CLOSEAPPLICATIONS), InnoSetup
                        // closes our process AND then re-launches the new version = two windows.
                        // Instead: exit cleanly first, then the installer runs unattended.
                        string installerPath = tempPath;
                        var form = Application.OpenForms.Count > 0
                            ? Application.OpenForms[0] : null;

                        if (form != null && !form.IsDisposed)
                        {
                            form.BeginInvoke(new Action(() =>
                            {
                                // Show tray notification so user knows what's happening
                                try
                                {
                                    var notify = new NotifyIcon
                                    {
                                        Icon    = System.Drawing.SystemIcons.Information,
                                        Visible = true
                                    };
                                    notify.ShowBalloonTip(4000, "ONE Voice Solution",
                                        "Updating to the latest version. The app will reopen automatically.",
                                        ToolTipIcon.Info);
                                    Thread.Sleep(1500);
                                    notify.Visible = false;
                                    notify.Dispose();
                                }
                                catch { }

                                // Write a batch file that waits for this process to exit,
                                // then launches the installer silently.
                                // This guarantees zero overlap between old and new instances.
                                try
                                {
                                    int pid = Process.GetCurrentProcess().Id;
                                    string batPath = Path.Combine(Path.GetTempPath(), "one_voice_update.bat");
                                    File.WriteAllText(batPath,
                                        $"@echo off\r\n" +
                                        $":wait\r\n" +
                                        $"tasklist /FI \"PID eq {pid}\" 2>NUL | find /I \"{pid}\" >NUL\r\n" +
                                        $"if not errorlevel 1 (timeout /t 1 /nobreak >NUL & goto wait)\r\n" +
                                        $"\"{installerPath}\" /VERYSILENT /NORESTART\r\n");

                                    Process.Start(new ProcessStartInfo
                                    {
                                        FileName        = "cmd.exe",
                                        Arguments       = $"/C \"{batPath}\"",
                                        UseShellExecute = true,
                                        WindowStyle     = ProcessWindowStyle.Hidden,
                                        Verb            = "runas"
                                    });
                                }
                                catch { }

                                // Exit this instance — the batch file will launch the installer
                                // only after this process is fully gone.
                                Application.Exit();
                            }));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("[AutoUpdater] " + ex.Message);
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
