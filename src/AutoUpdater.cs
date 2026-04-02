using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace OneApplication
{
    /// <summary>
    /// Checks GitHub Releases for a newer version of ONE Voice Solution.
    /// If found, silently downloads the installer and runs it with /VERYSILENT,
    /// then exits the current process so the new version takes over.
    /// Runs entirely on a background thread — never blocks the UI.
    /// </summary>
    internal static class AutoUpdater
    {
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

                        // Launch installer silently (InnoSetup /VERYSILENT flag)
                        var psi = new ProcessStartInfo
                        {
                            FileName        = tempPath,
                            Arguments       = "/VERYSILENT /NORESTART /CLOSEAPPLICATIONS",
                            UseShellExecute = true,
                            Verb            = "runas"   // request elevation if needed
                        };
                        Process.Start(psi);

                        // Exit current instance from the UI thread
                        var form = Application.OpenForms.Count > 0
                            ? Application.OpenForms[0] : null;
                        if (form != null && !form.IsDisposed)
                            form.BeginInvoke(new Action(() => Application.Exit()));
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
