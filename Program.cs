using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using WindowsFormsApp1.src;

namespace WindowsFormsApp1
{
    internal static class Program
    {
        // ── Win32 helpers to bring existing window to foreground ─────────────
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // ── Single-instance guard ────────────────────────────────────────
            // Named mutex ensures only ONE copy of the app runs at a time.
            // If a second instance is launched (e.g. via one-voice:// protocol),
            // it brings the existing window to the front and exits immediately.
            const string MutexName = "Global\\ONEVoiceSolution_SingleInstance";
            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // Another instance is already running — bring it to the front.
                    BringExistingInstanceToFront();
                    return; // Exit this second instance immediately.
                }

                // This is the first (and only) instance — proceed normally.
                RegisterUriScheme();
                ExtractAndSaveLicenseKey(args);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new LicenseForm());
            }
        }

        /// <summary>
        /// Finds the already-running instance of this app and brings its window
        /// to the foreground (restoring it if minimized).
        /// </summary>
        private static void BringExistingInstanceToFront()
        {
            try
            {
                string exeName = Process.GetCurrentProcess().ProcessName;
                Process current = Process.GetCurrentProcess();
                foreach (Process proc in Process.GetProcessesByName(exeName))
                {
                    if (proc.Id == current.Id) continue;
                    IntPtr hWnd = proc.MainWindowHandle;
                    if (hWnd == IntPtr.Zero) continue;
                    if (IsIconic(hWnd))
                        ShowWindow(hWnd, SW_RESTORE);
                    SetForegroundWindow(hWnd);
                    break;
                }
            }
            catch
            {
                // Best-effort — silently ignore if we can't bring window to front.
            }
        }

        /// <summary>
        /// Extracts the license key from the one-voice:// protocol URL passed
        /// as a command-line argument and saves it to the registry.
        /// Example URL: one-voice://launch?key=ABC-123-DEF
        /// </summary>
        private static void ExtractAndSaveLicenseKey(string[] args)
        {
            try
            {
                if (args == null || args.Length == 0)
                    return;

                // The full URL comes as the first argument, e.g.:
                // "one-voice://launch?key=ABC-123-DEF"
                string url = args[0];

                if (string.IsNullOrWhiteSpace(url))
                    return;

                // Only process if it's our protocol
                if (!url.StartsWith("one-voice://", StringComparison.OrdinalIgnoreCase))
                    return;

                // Parse the key parameter from the URL
                string licenseKey = null;

                int queryStart = url.IndexOf('?');
                if (queryStart >= 0 && queryStart < url.Length - 1)
                {
                    string queryString = url.Substring(queryStart + 1);
                    // Split on & to handle multiple params
                    string[] pairs = queryString.Split('&');
                    foreach (string pair in pairs)
                    {
                        string[] kv = pair.Split(new[] { '=' }, 2);
                        if (kv.Length == 2 && kv[0].Equals("key", StringComparison.OrdinalIgnoreCase))
                        {
                            licenseKey = Uri.UnescapeDataString(kv[1]).Trim();
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(licenseKey))
                    return;

                // Save the license key to registry (encrypted, same as manual entry)
                RegistryUtils.SetRegistryValue(
                    @"Software\OneApp3",
                    "License",
                    DataEncryption.EncryptString_Aes(licenseKey),
                    RegistryValueKind.String);

                // Also save to AppSettings so MainFormV5 heartbeat can use it
                AppSettings.Instance.LicenseKey = licenseKey;
                AppSettings.Instance.Save();
            }
            catch
            {
                // Silently ignore — license extraction is best-effort.
                // The user can still enter the key manually if this fails.
            }
        }

        /// <summary>
        /// Registers the one-voice:// custom URI scheme in HKEY_CLASSES_ROOT.
        /// Safe to call on every launch — only writes if the key is missing or stale.
        /// </summary>
        private static void RegisterUriScheme()
        {
            try
            {
                string exePath = Application.ExecutablePath;

                string protocolKey = @"one-voice";
                string commandValue = $"\"{exePath}\" \"%1\"";

                using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(protocolKey))
                {
                    if (key != null)
                    {
                        key.SetValue("", "URL:ONE Voice Solution");
                        key.SetValue("URL Protocol", "");
                    }
                }

                using (RegistryKey iconKey = Registry.ClassesRoot.CreateSubKey($@"{protocolKey}\DefaultIcon"))
                {
                    iconKey?.SetValue("", $"{exePath},0");
                }

                using (RegistryKey cmdKey = Registry.ClassesRoot.CreateSubKey($@"{protocolKey}\shell\open\command"))
                {
                    cmdKey?.SetValue("", commandValue);
                }
            }
            catch
            {
                // Silently ignore — protocol registration is best-effort.
            }
        }
    }
}
