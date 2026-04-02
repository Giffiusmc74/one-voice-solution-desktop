using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OneApplication
{
    /// <summary>
    /// Checks GitHub Releases for a newer version of ONE Voice Solution.
    /// If found, silently downloads the installer and runs it with /VERYSILENT,
    /// then exits the current process so the new version takes over.
    /// </summary>
    internal static class AutoUpdater
    {
        // ── Configuration ─────────────────────────────────────────────────────
        private const string GITHUB_OWNER    = "Giffiusmc74";
        private const string GITHUB_REPO     = "one-voice-solution-desktop";
        private const string INSTALLER_ASSET = "ONEVoiceSolution_Setup.exe";

        // GitHub API endpoint for the latest release
        private static readonly string API_URL =
            $"https://api.github.com/repos/{GITHUB_OWNER}/{GITHUB_REPO}/releases/latest";

        // ── Public entry point ────────────────────────────────────────────────
        /// <summary>
        /// Call this on startup (fire-and-forget).  Shows a brief status label
        /// if an update is being downloaded, otherwise runs silently.
        /// </summary>
        public static async Task CheckAndUpdateAsync(string currentVersion, Label statusLabel = null)
        {
            try
            {
                SetStatus(statusLabel, "Checking for updates…");

                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(10);
                http.DefaultRequestHeaders.Add("User-Agent", "ONEVoiceSolution-AutoUpdater");

                var json = await http.GetStringAsync(API_URL);
                using var doc  = JsonDocument.Parse(json);
                var root        = doc.RootElement;

                // Tag name is expected to be "v6.5" or "6.5" — strip leading 'v'
                string tagName     = root.GetProperty("tag_name").GetString() ?? "";
                string remoteVer   = tagName.TrimStart('v');

                if (!IsNewer(remoteVer, currentVersion))
                {
                    SetStatus(statusLabel, null);   // up to date — hide label
                    return;
                }

                // Find the installer asset in the release
                string downloadUrl = null;
                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string name = asset.GetProperty("name").GetString() ?? "";
                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    SetStatus(statusLabel, null);
                    return;
                }

                SetStatus(statusLabel, $"Downloading update v{remoteVer}…");

                // Download to temp folder
                string tempPath = Path.Combine(Path.GetTempPath(), INSTALLER_ASSET);
                var bytes = await http.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(tempPath, bytes);

                SetStatus(statusLabel, $"Installing v{remoteVer}…");

                // Run installer silently — InnoSetup flags:
                //   /VERYSILENT  = no UI at all
                //   /NORESTART   = don't force reboot
                //   /CLOSEAPPLICATIONS = close running instances
                var psi = new ProcessStartInfo
                {
                    FileName        = tempPath,
                    Arguments       = "/VERYSILENT /NORESTART /CLOSEAPPLICATIONS",
                    UseShellExecute = true,
                    Verb            = "runas"   // request elevation if needed
                };
                Process.Start(psi);

                // Exit current instance — installer will relaunch the new version
                Application.Exit();
            }
            catch (Exception ex)
            {
                // Never crash the app over an update failure — just log and continue
                Log.Warn($"[AutoUpdater] {ex.Message}");
                SetStatus(statusLabel, null);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static bool IsNewer(string remote, string current)
        {
            if (Version.TryParse(remote,  out var r) &&
                Version.TryParse(current, out var c))
                return r > c;

            // Fallback: simple string compare
            return string.Compare(remote, current, StringComparison.OrdinalIgnoreCase) > 0;
        }

        private static void SetStatus(Label lbl, string text)
        {
            if (lbl == null) return;
            if (lbl.InvokeRequired)
                lbl.BeginInvoke(new Action(() => SetStatus(lbl, text)));
            else
            {
                lbl.Text    = text ?? "";
                lbl.Visible = !string.IsNullOrEmpty(text);
            }
        }
    }
}
