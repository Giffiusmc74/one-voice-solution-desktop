using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsFormsApp1.src
{
    public static class RegistryUtils
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        // Create or update a registry key/value
        public static void SetRegistryValue(string keyPath, string valueName, object value, RegistryValueKind valueKind)
        {
            try 
            {
                using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    if (key != null)
                    {
                        key.SetValue(valueName, value, valueKind);
                        logger.Info($"Registry Saved: {keyPath}\\{valueName}");
                    }
                    else
                    {
                         logger.Error($"Failed to create/open registry key: {keyPath}");
                         throw new Exception($"Failed to create/open registry key: {keyPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error writing to registry {keyPath}\\{valueName}");
                throw; // Re-throw to let the caller handle UI feedback
            }
        }

        // Get a value from a registry key
        public static object GetRegistryValue(string keyPath, string valueName)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                {
                    if (key != null)
                    {
                        return key.GetValue(valueName);
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error reading from registry {keyPath}\\{valueName}");
                return null;
            }
        }

        // Delete a value from a registry key
        public static void DeleteRegistryValue(string keyPath, string valueName)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(keyPath, true))
                {
                    if (key != null)
                    {
                        key.DeleteValue(valueName);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error deleting registry value {keyPath}\\{valueName}");
            }
        }

        // Delete a registry key and its all values
        public static void DeleteRegistryKey(string keyPath)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(keyPath);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error deleting registry key {keyPath}");
            }
        }
    }
}
