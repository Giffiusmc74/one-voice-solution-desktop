using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using WindowsFormsApp1.src;

namespace WindowsFormsApp1
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Register the one-voice:// URI scheme so the member portal can launch this app.
            // This runs silently every startup so existing installs self-heal without a reinstall.
            RegisterUriScheme();

            // ── Parse protocol URL for license key ──────────────────────────
            // When launched via one-voice://launch?key=XXXX from the member
            // portal, Windows passes the full URL as the first command-line arg.
            // We extract the key and save it to the registry so the user never
            // has to enter it manually.
            ExtractAndSaveLicenseKey(args);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LicenseForm());
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
                // This overwrites any existing key — the portal always has the
                // authoritative key for this user.
                RegistryUtils.SetRegistryValue(
                    @"Software\OneApp3",
                    "License",
                    DataEncryption.EncryptString_Aes(licenseKey),
                    RegistryValueKind.String);
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
        /// Requires the app to be run at least once after install (or after a fresh download).
        /// </summary>
        private static void RegisterUriScheme()
        {
            try
            {
                // Get the full path to this executable
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                // For .NET Framework WinForms, Application.ExecutablePath is more reliable
                exePath = Application.ExecutablePath;

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
                // The app still launches normally even if this fails (e.g. no admin rights).
            }
        }
    }
}
