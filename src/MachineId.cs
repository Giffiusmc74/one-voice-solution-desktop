/**
 * MachineId.cs
 * ONE Voice Solution v5.0
 *
 * Generates a stable, anonymous machine fingerprint for license binding.
 * Uses the Windows machine GUID from the registry (set once at OS install).
 * Falls back to a hash of the machine name + processor ID if registry is unavailable.
 * The ID is stored locally so it never changes even if the registry key is deleted.
 */
using Microsoft.Win32;
using NLog;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WindowsFormsApp1.src
{
    public static class MachineId
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static string _cached;

        private static readonly string IdFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OneVoiceSolution", "machine.id");

        public static string Get()
        {
            if (!string.IsNullOrEmpty(_cached)) return _cached;

            // 1. Try stored file first
            try
            {
                if (File.Exists(IdFile))
                {
                    _cached = File.ReadAllText(IdFile).Trim();
                    if (!string.IsNullOrEmpty(_cached)) return _cached;
                }
            }
            catch { }

            // 2. Try Windows MachineGuid registry key
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Cryptography", false))
                {
                    var guid = key?.GetValue("MachineGuid")?.ToString();
                    if (!string.IsNullOrEmpty(guid))
                    {
                        _cached = Hash(guid);
                        Persist(_cached);
                        return _cached;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[MachineId] Registry read failed: {ex.Message}");
            }

            // 3. Fallback: hash of MachineName + ProcessorId
            try
            {
                string raw = Environment.MachineName + GetProcessorId();
                _cached = Hash(raw);
                Persist(_cached);
                return _cached;
            }
            catch (Exception ex)
            {
                Log.Warn($"[MachineId] Fallback failed: {ex.Message}");
                _cached = Guid.NewGuid().ToString("N");
                Persist(_cached);
                return _cached;
            }
        }

        private static string GetProcessorId()
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                        return obj["ProcessorId"]?.ToString() ?? "";
                }
            }
            catch { }
            return "";
        }

        private static string Hash(string input)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant().Substring(0, 32);
            }
        }

        private static void Persist(string id)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(IdFile));
                File.WriteAllText(IdFile, id);
            }
            catch { }
        }
    }
}
