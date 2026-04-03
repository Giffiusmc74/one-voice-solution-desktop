using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using WindowsFormsApp1.src;

namespace WindowsFormsApp1
{
    internal static class Program
    {
        // ── Pipe name — unique to this app ───────────────────────────────────
        private const string PipeName = "ONEVoiceSolution_SingleInstance_Pipe";

        // ── CRITICAL: Mutex must be a static field held for the entire process lifetime.
        // Putting it in a 'using' block disposes it early, allowing a second instance to launch.
        private static Mutex _appMutex;

        // ── Win32: bring existing window to foreground ───────────────────────
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        private const int SW_RESTORE = 9;

        [STAThread]
        static void Main(string[] args)
        {
            // ── Single-instance guard ─────────────────────────────────────────
            bool isFirstInstance;
            _appMutex = new Mutex(true, "ONEVoiceSolution_Mutex_v3", out isFirstInstance);

            if (!isFirstInstance)
            {
                // Another instance is running — signal it to come to the front and exit.
                SignalFirstInstance();
                _appMutex.Close();
                return;
            }

            // We are the first instance — register protocol, extract key, start pipe listener.
            RegisterUriScheme();
            ExtractAndSaveLicenseKey(args);

            Thread pipeThread = new Thread(ListenForActivation)
            {
                IsBackground = true,
                Name = "PipeListener"
            };
            pipeThread.Start();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LicenseForm());

            // Release mutex when the app exits normally.
            try { _appMutex.ReleaseMutex(); } catch { }
            _appMutex.Close();
        }

        /// <summary>
        /// Called by the SECOND instance: connects to the first instance's
        /// pipe and sends an "activate" signal, then exits.
        /// </summary>
        private static void SignalFirstInstance()
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    client.Connect(2000); // 2-second timeout
                    using (var sw = new StreamWriter(client))
                        sw.WriteLine("activate");
                }
            }
            catch { /* Best-effort — if pipe not ready, just exit silently */ }
        }

        /// <summary>
        /// Runs on a background thread in the FIRST instance.
        /// Waits for activation signals and brings the main window to front.
        /// </summary>
        private static void ListenForActivation()
        {
            while (true)
            {
                try
                {
                    using (var server = new NamedPipeServerStream(PipeName, PipeDirection.In,
                        1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                    {
                        server.WaitForConnection();
                        using (var sr = new StreamReader(server))
                        {
                            string msg = sr.ReadLine();
                            if (msg == "activate")
                                BringMainWindowToFront();
                        }
                    }
                }
                catch { Thread.Sleep(500); }
            }
        }

        /// <summary>
        /// Finds the main application window and brings it to the foreground.
        /// </summary>
        private static void BringMainWindowToFront()
        {
            try
            {
                // Find the main form — it may be LicenseForm or MainFormV5.
                Form mainForm = null;
                // Marshal to UI thread to safely access Application.OpenForms
                if (Application.OpenForms.Count > 0)
                {
                    Form f = Application.OpenForms[Application.OpenForms.Count - 1];
                    f.Invoke((Action)(() =>
                    {
                        f.WindowState = FormWindowState.Normal;
                        f.Activate();
                        f.BringToFront();
                        IntPtr hWnd = f.Handle;
                        if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);
                        SetForegroundWindow(hWnd);
                    }));
                }
            }
            catch { }
        }

        /// <summary>
        /// Extracts the license key from the one-voice:// protocol URL.
        /// </summary>
        private static void ExtractAndSaveLicenseKey(string[] args)
        {
            try
            {
                if (args == null || args.Length == 0) return;
                string url = args[0];
                if (string.IsNullOrWhiteSpace(url)) return;
                if (!url.StartsWith("one-voice://", StringComparison.OrdinalIgnoreCase)) return;

                string licenseKey = null;
                int queryStart = url.IndexOf('?');
                if (queryStart >= 0 && queryStart < url.Length - 1)
                {
                    string queryString = url.Substring(queryStart + 1);
                    foreach (string pair in queryString.Split('&'))
                    {
                        string[] kv = pair.Split(new[] { '=' }, 2);
                        if (kv.Length == 2 && kv[0].Equals("key", StringComparison.OrdinalIgnoreCase))
                        {
                            licenseKey = Uri.UnescapeDataString(kv[1]).Trim();
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(licenseKey)) return;

                RegistryUtils.SetRegistryValue(
                    @"Software\OneApp3", "License",
                    DataEncryption.EncryptString_Aes(licenseKey),
                    Microsoft.Win32.RegistryValueKind.String);

                AppSettings.Instance.LicenseKey = licenseKey;
                AppSettings.Instance.Save();
            }
            catch { }
        }

        /// <summary>
        /// Registers the one-voice:// custom URI scheme.
        /// </summary>
        private static void RegisterUriScheme()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                string commandValue = $"\"{exePath}\" \"%1\"";

                using (RegistryKey key = Registry.ClassesRoot.CreateSubKey("one-voice"))
                {
                    key?.SetValue("", "URL:ONE Voice Solution");
                    key?.SetValue("URL Protocol", "");
                }
                using (RegistryKey iconKey = Registry.ClassesRoot.CreateSubKey(@"one-voice\DefaultIcon"))
                    iconKey?.SetValue("", $"{exePath},0");

                using (RegistryKey cmdKey = Registry.ClassesRoot.CreateSubKey(@"one-voice\shell\open\command"))
                    cmdKey?.SetValue("", commandValue);
            }
            catch { }
        }
    }
}
