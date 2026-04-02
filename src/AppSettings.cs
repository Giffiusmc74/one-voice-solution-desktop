/**
 * AppSettings.cs
 * ONE Voice Solution v5.0
 *
 * Persists all user preferences to a JSON file in AppData\Local\OneVoiceSolution\.
 * Settings are loaded on startup and saved automatically whenever any value changes.
 *
 * Stored values:
 *   - Selected microphone device name
 *   - Selected headset/speaker device name
 *   - Customer Voice volume (0–100)
 *   - Script Playback volume (0–100)
 *   - My Mic Level volume (0–100)
 *   - Customer Output Script Playback volume (0–100)
 *   - Auto Level-Match enabled (bool)
 *   - Last window position (X, Y)
 *   - Last window screen index
 *   - Agency logo path (if uploaded)
 */
using Newtonsoft.Json;
using NLog;
using System;
using System.IO;

namespace WindowsFormsApp1.src
{
    public class AppSettings
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        private static AppSettings _instance;
        public static AppSettings Instance => _instance ?? (_instance = Load());

        // ── Settings properties ───────────────────────────────────────────────
        public string MicrophoneDevice           { get; set; } = "";
        public string HeadsetDevice              { get; set; } = "";
        public int    CustomerVoiceVolume         { get; set; } = 60;
        public int    AgentScriptPlaybackVolume   { get; set; } = 50;
        public int    MyMicLevelVolume            { get; set; } = 55;
        public int    CustomerScriptPlaybackVolume{ get; set; } = 55;
        public bool   AutoLevelMatchEnabled       { get; set; } = true;
        public int    WindowX                     { get; set; } = -1; // -1 = use default centering
        public int    WindowY                     { get; set; } = -1;
        public int    WindowScreenIndex           { get; set; } = 0;
        public string AgencyLogoPath              { get; set; } = "";
        public string AgentName                   { get; set; } = "";
        public string LicenseKey                  { get; set; } = "";

        // Aliases for v5 naming consistency
        public string MicDevice     { get => MicrophoneDevice; set => MicrophoneDevice = value; }

        // Per-side Auto Level-Match (v5)
        public bool AgentAutoLevel    { get; set; } = true;
        public bool CustomerAutoLevel { get; set; } = true;

        // Volume helpers (float 0–1, maps to int 0–100 internally)
        public float GetVolume(string key, float defaultVal = 0.5f)
        {
            switch (key)
            {
                case "agentVoice":    return CustomerVoiceVolume / 100f;
                case "agentScript":   return AgentScriptPlaybackVolume / 100f;
                case "customerVoice": return MyMicLevelVolume / 100f;
                case "customerScript":return CustomerScriptPlaybackVolume / 100f;
                default: return defaultVal;
            }
        }

        public void SetVolume(string key, float value)
        {
            int v = (int)(Math.Max(0f, Math.Min(1f, value)) * 100);
            switch (key)
            {
                case "agentVoice":    CustomerVoiceVolume          = v; break;
                case "agentScript":   AgentScriptPlaybackVolume    = v; break;
                case "customerVoice": MyMicLevelVolume             = v; break;
                case "customerScript":CustomerScriptPlaybackVolume = v; break;
            }
        }

        // ── File path ─────────────────────────────────────────────────────────
        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OneVoiceSolution");
        private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        // ── Load ──────────────────────────────────────────────────────────────
        private static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (settings != null)
                    {
                        Log.Info("[AppSettings] Loaded from disk.");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[AppSettings] Could not load settings: {ex.Message}");
            }
            Log.Info("[AppSettings] Using defaults.");
            return new AppSettings();
        }

        // ── Save ──────────────────────────────────────────────────────────────
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                Log.Warn($"[AppSettings] Could not save settings: {ex.Message}");
            }
        }
    }
}
