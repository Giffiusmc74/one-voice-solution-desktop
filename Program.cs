using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WindowsFormsApp1
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Register the one-voice:// URI scheme so the member portal can launch this app.
            // This runs silently every startup so existing installs self-heal without a reinstall.
            RegisterUriScheme();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LicenseForm());
            //Application.Run(new MainForm(new LicenseForm()));
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
