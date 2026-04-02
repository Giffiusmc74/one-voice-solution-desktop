using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1.src
{
    internal class DataUtils
    {
        // Centralized brand name
        private const string BrandName = "ONE Voice Solution";

        // Centralized path management
        public static string AppDataPath
        {
            get
            {
                // Core data files go into the "Data" subfolder
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), BrandName, "Data");
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                return path;
            }
        }

        public static string AudioDataPath
        {
            get
            {
                // Audio files go into a "Audio" folder that is a SIBLING of "Data"
                string parentPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), BrandName);
                string path = Path.Combine(parentPath, "Audio");
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                return path;
            }
        }

        public static string VolumeDataPath => Path.Combine(AppDataPath, "Volume.txt");
        public static string TabStatePath => Path.Combine(AppDataPath, "tab_state.json");
        public static string MacrosPath => Path.Combine(AppDataPath, "MacrosInfo.txt");
        public static string ScriptsPath => Path.Combine(AppDataPath, "Scripts.txt");
        public static string SettingsPath => Path.Combine(AppDataPath, "Settings.txt");
        public static string MigrationFlagPath => Path.Combine(AppDataPath, ".migration_v5_done"); // Bumped version to force re-migration
        
        public static void MigrateData()
        {
            if (File.Exists(MigrationFlagPath)) return;

            try
            {
                string newPath = AppDataPath;
                string oldRoamingPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OneVoiceSolution");
                string clickOncePath = FindClickOnceDataPath();
                
                string sourcePath = null;
                
                // Priority 1: ClickOnce
                if (!string.IsNullOrEmpty(clickOncePath) && Directory.Exists(clickOncePath))
                {
                    sourcePath = clickOncePath;
                }
                // Priority 2: Legacy Roaming
                else if (Directory.Exists(oldRoamingPath))
                {
                    sourcePath = oldRoamingPath;
                }

                if (sourcePath != null)
                {
                    // 1. Copy core data files (Unconditional replace for migration)
                    string[] coreFiles = { "Scripts.txt", "MacrosInfo.txt", "tab_state.json", "Volume.txt", "Settings.txt" };
                    foreach (string fileName in coreFiles)
                    {
                        string src = Path.Combine(sourcePath, fileName);
                        if (!File.Exists(src)) src = Path.Combine(sourcePath, "Data", fileName); // Check Data subfolder
                        
                        if (File.Exists(src))
                        {
                            CopyFileSafely(src, Path.Combine(newPath, fileName));
                        }
                    }

                    // 2. Smart Audio Migration (Only referenced files)
                    MigrateReferencedAudio(sourcePath, newPath);
                }

                // Mark migration as successful (even if nothing was found, so we don't scan every time)
                File.WriteAllText(MigrationFlagPath, DateTime.Now.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Migration error: " + ex.Message);
            }
        }

        private static void MigrateReferencedAudio(string sourcePath, string destPath)
        {
            try
            {
                HashSet<string> referencedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Check Scripts.txt
                string scriptsPath = Path.Combine(destPath, "Scripts.txt");
                if (File.Exists(scriptsPath))
                {
                    try
                    {
                        var scripts = JsonConvert.DeserializeObject<List<Scripts>>(File.ReadAllText(scriptsPath));
                        if (scripts != null)
                        {
                            foreach (var s in scripts) if (!string.IsNullOrEmpty(s.AudioFilePath)) referencedFiles.Add(Path.GetFileName(s.AudioFilePath));
                        }
                    }
                    catch { }
                }

                // Check tab_state.json
                string tabStatePath = Path.Combine(destPath, "tab_state.json");
                if (File.Exists(tabStatePath))
                {
                    try
                    {
                        // Use dynamic or a simple regex if the class structure differs too much, 
                        // but here we can try the actual model
                        var tabState = JsonConvert.DeserializeObject<ScriptsForm.TabStateData>(File.ReadAllText(tabStatePath));
                        if (tabState?.Tabs != null)
                        {
                            foreach (var tab in tabState.Tabs)
                            {
                                if (tab.Scripts != null)
                                {
                                    foreach (var s in tab.Scripts) if (!string.IsNullOrEmpty(s.AudioFilePath)) referencedFiles.Add(Path.GetFileName(s.AudioFilePath));
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (referencedFiles.Count > 0)
                {
                    // Define possible source audio folders
                    string[] audioFolders = { "Audio", "Recordings", "Data\\Audio", "Data\\Recordings" };
                    string targetAudioDir = AudioDataPath;

                    foreach (string subFolder in audioFolders)
                    {
                        string fullSourceDir = Path.Combine(sourcePath, subFolder);
                        if (Directory.Exists(fullSourceDir))
                        {
                            foreach (string refFile in referencedFiles)
                            {
                                string srcFile = Path.Combine(fullSourceDir, refFile);
                                if (File.Exists(srcFile))
                                {
                                    CopyFileSafely(srcFile, Path.Combine(targetAudioDir, refFile));
                                }
                            }
                        }
                    }

                    // 3. REWRITE PATHS IN MIGRATED FILES
                    // We rewrite Scripts.txt and tab_state.json to use the NEW absolute paths in the sibling Audio folder
                    
                    // Rewrite Scripts.txt
                    if (File.Exists(scriptsPath))
                    {
                        try
                        {
                            var scripts = JsonConvert.DeserializeObject<List<Scripts>>(File.ReadAllText(scriptsPath));
                            if (scripts != null)
                            {
                                bool modified = false;
                                foreach (var s in scripts)
                                {
                                    // Ensure ID exists
                                    if (string.IsNullOrEmpty(s.Id))
                                    {
                                        s.Id = Guid.NewGuid().ToString();
                                        modified = true;
                                    }

                                    if (!string.IsNullOrEmpty(s.AudioFilePath))
                                    {
                                        s.AudioFilePath = Path.Combine(targetAudioDir, Path.GetFileName(s.AudioFilePath));
                                        modified = true;
                                    }
                                }
                                if (modified) File.WriteAllText(scriptsPath, JsonConvert.SerializeObject(scripts, Formatting.Indented));
                            }
                        }
                        catch { }
                    }

                    // Rewrite tab_state.json
                    if (File.Exists(tabStatePath))
                    {
                        try
                        {
                            var tabState = JsonConvert.DeserializeObject<ScriptsForm.TabStateData>(File.ReadAllText(tabStatePath));
                            if (tabState?.Tabs != null)
                            {
                                bool modified = false;
                                foreach (var tab in tabState.Tabs)
                                {
                                    if (tab.Scripts != null)
                                    {
                                        foreach (var s in tab.Scripts)
                                        {
                                            // Ensure ID exists
                                            if (string.IsNullOrEmpty(s.Id))
                                            {
                                                s.Id = Guid.NewGuid().ToString();
                                                modified = true;
                                            }

                                            if (!string.IsNullOrEmpty(s.AudioFilePath))
                                            {
                                                s.AudioFilePath = Path.Combine(targetAudioDir, Path.GetFileName(s.AudioFilePath));
                                                modified = true;
                                            }
                                        }
                                    }
                                }
                                if (modified) File.WriteAllText(tabStatePath, JsonConvert.SerializeObject(tabState, Formatting.Indented));
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static string FindClickOnceDataPath()
        {
            try
            {
                string localAppsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Apps", "2.0");
                if (Directory.Exists(localAppsRoot))
                {
                    // The user provided a specific pattern: ...\onea..tion_...
                    // We search for "onea*" which matches the truncated ClickOnce folder name
                    var found = Directory.GetDirectories(localAppsRoot, "onea*", SearchOption.AllDirectories);
                    foreach (var dir in found)
                    {
                        // Look for any folder that contains our signature files
                        if (File.Exists(Path.Combine(dir, "Scripts.txt")) || 
                            File.Exists(Path.Combine(dir, "tab_state.json")) ||
                            File.Exists(Path.Combine(dir, "Data", "Scripts.txt")))
                        {
                            return dir;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static void MigrateDirectory(string source, string dest)
        {
            if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);
            foreach (string file in Directory.GetFiles(source))
            {
                string destFile = Path.Combine(dest, Path.GetFileName(file));
                CopyFileSafely(file, destFile);
            }
        }

        private static void CopyFileSafely(string source, string dest)
        {
            try
            {
                // Ensure destination directory exists
                string destDir = Path.GetDirectoryName(dest);
                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

                // Unconditionally overwrite existing files during migration to ensure legacy data takes priority
                File.Copy(source, dest, true);
            }
            catch { /* Ignore copy errors to prevent migration from crashing */ }
        }

        public static void ResetData()
        {
            try
            {
                string rootFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), BrandName);
                if (Directory.Exists(rootFolder))
                {
                    Directory.Delete(rootFolder, true);
                }
            }
            catch { /* Ignore cleanup errors */ }
        }

        public static MacroManager LoadMacros(string filePath)
        {
              
            MacroManager macroManager = new MacroManager();
            try
            {
                macroManager.macroList = new List<MacrosInfo>();

                if (!File.Exists(filePath))
                {
                    CreateMacrosFile(filePath);
                }

                string json = File.ReadAllText(filePath);
                macroManager.macroList = JsonConvert.DeserializeObject<List<MacrosInfo>>(json);
            }
            catch(Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
            return macroManager;
        }

        private static void CreateMacrosFile(string filePath)
        {
            // Check if the Data folder exists, if not, create it
            var dataDirectory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            // Check if the file exists, if not, create an empty file
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "[\r\n  {\r\n    \"Name\": \"Shift + F1\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + F2\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + F3\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n {\r\n    \"Name\": \"Shift + F4\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + F5\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + F6\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n {\r\n    \"Name\": \"Shift + F7\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + F8\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + F9\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n {\r\n    \"Name\": \"Shift + F10\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + F11\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + F12\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + A\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + B\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + C\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + D\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + E\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + F\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + G\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + H\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + I\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + J\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + K\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + L\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + M\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + N\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + O\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + P\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + Q\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + R\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + S\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + T\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + U\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + V\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + W\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + X\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + Y\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + Z\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + 1\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + 2\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + 3\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + 4\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + 5\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + 6\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + 7\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + 8\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + 9\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + 0\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + Num1\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + Num2\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + Num3\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + Num4\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + Num5\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + Num6\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + Num7\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + Num8\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + Num9\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + Num0\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + ,\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + .\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + ;\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + '\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + [\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + ]\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + -\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + Space\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Shift + Enter\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + F1\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + F2\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + F3\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + F4\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + F5\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + F6\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + F7\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + F8\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + F9\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + F10\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + F11\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + F12\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + A\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + B\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + C\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + D\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + E\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + F\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + G\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + H\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + I\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + J\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + K\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + L\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + M\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + N\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + O\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + P\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + Q\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + R\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + S\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + T\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + U\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + V\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + W\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + X\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + Y\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + Z\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + 1\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + 2\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + 3\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + 4\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + 5\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + 6\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + 7\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + 8\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + 9\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + 0\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + Num1\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + Num2\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + Num3\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + Num4\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + Num5\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + Num6\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + Num7\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + Num8\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + Num9\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + Num0\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + ,\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + .\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + ;\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + '\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + [\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + ]\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + -\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + Space\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Alt + Enter\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + F1\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + F2\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + F3\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + F4\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + F5\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + F6\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + F7\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + F8\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + F9\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + F10\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + F11\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + F12\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + A\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + B\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + C\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + D\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + E\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + F\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + G\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + H\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + I\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + J\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + K\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + L\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + M\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + N\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + O\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + P\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + Q\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + R\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + S\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + T\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + U\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + V\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + W\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + X\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + Y\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + Z\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + 1\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + 2\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + 3\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + 4\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + 5\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + 6\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + 7\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + 8\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + 9\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + 0\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + Num1\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + Num2\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + Num3\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + Num4\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + Num5\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + Num6\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + Num7\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + Num8\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + Num9\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + Num0\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + ,\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + .\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + ;\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + '\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + [\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + ]\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + -\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + Space\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  },\r\n   {\r\n    \"Name\": \"Ctrl + Enter\",\r\n    \"ColorName\": \"SkyBlue\",\r\n    \"VoiceFilePath\": \"\",\r\n    \"IsEmpty\": true\r\n  }\r\n]"); // Creates an empty JSON array
            }
        }

        public static void SaveMacros(MacroManager data, string filePath)
        {
            try
            {
                if (data == null)
                {
                    MessageBox.Show("No data to save");
                }
                string json = JsonConvert.SerializeObject(data.macroList, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        public static ScriptManager LoadScripts(string filePath)
        {

            ScriptManager scriptManager = new ScriptManager();
            try
            {
                scriptManager.scriptList = new List<Scripts>();

                if (!File.Exists(filePath))
                {
                    CreateScriptsFile(filePath);
                }

                string json = File.ReadAllText(filePath);
                scriptManager.scriptList = JsonConvert.DeserializeObject<List<Scripts>>(json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
            return scriptManager;
        }

        private static void CreateScriptsFile(string filePath)
        {
            // Check if the Data folder exists, if not, create it
            var dataDirectory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            // Check if the file exists, if not, create an empty file
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "[]"); // Creates an empty JSON array
            }
        }

        public static void SaveScripts(ScriptManager data, string filePath)
        {
            try
            {
                if (data == null)
                {
                    MessageBox.Show("No data to save");
                }
                string json = JsonConvert.SerializeObject(data.scriptList, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        public static void SaveVolumeValue(Dictionary<string, string> volumeValue, string filePath)
        {
            try
            {
                if (volumeValue == null || volumeValue.Count == 0)
                {
                    MessageBox.Show("No data to save");
                }

                string json = JsonConvert.SerializeObject(volumeValue, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private static void CreateVolumeFile(string filePath)
        {
            // Check if the Data folder exists, if not, create it
            var dataDirectory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            // Check if the file exists, if not, create an empty file
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "{\r\n  \"volumeA1Mic\": \"100\",\r\n   \"volumeA1RecIn\": \"100\",\r\n  \"volumeA1RecOut\": \"100\",\r\n    \"volumeA1Speaker\": \"100\",\r\n \"volumeA2Speaker\": \"100\"\r\n    }"); // Creates an empty JSON array
            }
        }

        public static Dictionary<string, string> LoadVolumeValue(string filePath)
        {
            Dictionary<string,string> keyValuePairs = new Dictionary<string,string>();
            try
            {               
                if (!File.Exists(filePath))
                {
                    CreateVolumeFile(filePath);
                }

                string json = File.ReadAllText(filePath);
                keyValuePairs = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
            return keyValuePairs;
        }
    }
}
