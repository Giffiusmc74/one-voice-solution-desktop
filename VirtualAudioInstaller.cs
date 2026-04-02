using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using NLog;
using Microsoft.Win32;
using System.IO.Compression;
using System.Security.Principal;
using NAudio.Wave;
using System.Linq;
using System.Drawing;

namespace WindowsFormsApp1
{
    public class VirtualAudioInstaller
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        // VB-Audio Cable download information
        private const string VB_CABLE_DOWNLOAD_URL = "https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip";
        private const string VB_CABLE_INSTALLER_NAME = "VBCABLE_Driver_Pack45.exe";
        private const string VB_CABLE_REGISTRY_KEY = @"SOFTWARE\VB-Audio\CABLE";
        
        public static async Task<bool> EnsureVirtualAudioCableInstalled()
        {
            try
            {
                logger.Info("Checking Virtual Audio Cable installation status...");

                // 1. Check if already installed
                if (IsVBAudioCableInstalled())
                {
                    logger.Info("VB-Audio Cable is already installed");
                    return true;
                }

                // 2. If NOT installed, do NOT try to auto-install (requires Admin/Restarts which is bad UX)
                // Just warn the user that the driver is missing.
                logger.Warn("VB-Audio Cable not found.");
                
                MessageBox.Show(
                    "VB-Audio Virtual Cable driver is missing!\n\n" +
                    "This driver is required for the application to function correctly.\n" +
                    "Please reinstall the application to fix this issue.",
                    "Driver Missing",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error checking Virtual Audio Cable: " + ex.Message);
                return false;
            }
        }

        // Helper method to safely update UI
        private static void SafeUpdateUI(Form form, Action action)
        {
            try
            {
                if (form != null && !form.IsDisposed)
                {
                    if (form.InvokeRequired)
                    {
                        form.Invoke(action);
                    }
                    else
                    {
                        action();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warn($"UI update failed: {ex.Message}");
            }
        }

        private static async Task<bool> TrySimpleInstallation()
        {
            Form progressForm = null;
            Label statusLabel = null;
            ProgressBar progressBar = null;
            string extractPath = null;
            
            try
            {
                // Create a simple progress form
                progressForm = new Form();
                statusLabel = new Label();
                progressBar = new ProgressBar();
                
                // Setup progress form
                progressForm.Text = "Installing VB-Audio Cable";
                progressForm.Size = new System.Drawing.Size(450, 150);
                progressForm.StartPosition = FormStartPosition.CenterScreen;
                progressForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                progressForm.MaximizeBox = false;
                progressForm.MinimizeBox = false;
                progressForm.ControlBox = false; // Prevent closing during operation
                
                statusLabel.Text = "Preparing download...";
                statusLabel.Location = new System.Drawing.Point(10, 20);
                statusLabel.Size = new System.Drawing.Size(420, 40);
                statusLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
                
                progressBar.Location = new System.Drawing.Point(10, 70);
                progressBar.Size = new System.Drawing.Size(420, 30);
                progressBar.Style = ProgressBarStyle.Marquee;
                progressBar.MarqueeAnimationSpeed = 30;
                
                progressForm.Controls.Add(statusLabel);
                progressForm.Controls.Add(progressBar);
                
                progressForm.Show();
                Application.DoEvents();
                
                // Give the form time to fully initialize
                await Task.Delay(500);

                // Use temp folder for better reliability
                string tempPath = Path.GetTempPath();
                string workingDir = Path.Combine(tempPath, "VBAudioSetup_" + DateTime.Now.Ticks);
                Directory.CreateDirectory(workingDir);
                
                string zipPath = Path.Combine(workingDir, "VBCABLE_Driver_Pack45.zip");
                extractPath = Path.Combine(workingDir, "extracted");
                Directory.CreateDirectory(extractPath);

                logger.Info($"Working directory: {workingDir}");
                logger.Info($"Zip path: {zipPath}");
                logger.Info($"Extract path: {extractPath}");

                // Download
                SafeUpdateUI(progressForm, () =>
                {
                    statusLabel.Text = "Downloading VB-Audio Cable from official website...\nThis may take a minute...";
                    Application.DoEvents();
                });

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(10);
                    logger.Info($"Downloading from: {VB_CABLE_DOWNLOAD_URL}");
                    
                    var response = await client.GetAsync(VB_CABLE_DOWNLOAD_URL);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        logger.Error($"Download failed with status: {response.StatusCode}");
                        SafeUpdateUI(progressForm, () =>
                        {
                            statusLabel.Text = $"Download failed: {response.StatusCode}";
                        });
                        await Task.Delay(3000);
                        return false;
                    }

                    var content = await response.Content.ReadAsByteArrayAsync();
                    File.WriteAllBytes(zipPath, content);
                    
                    logger.Info($"Downloaded {content.Length} bytes successfully");
                }

                // Verify download
                if (!File.Exists(zipPath))
                {
                    logger.Error("Downloaded file does not exist");
                    return false;
                }

                var zipInfo = new FileInfo(zipPath);
                logger.Info($"Zip file size: {zipInfo.Length} bytes");

                if (zipInfo.Length < 1000)
                {
                    logger.Error("Downloaded file is too small, likely corrupted");
                    return false;
                }

                // Extract
                SafeUpdateUI(progressForm, () =>
                {
                    statusLabel.Text = "Extracting installer files...";
                    Application.DoEvents();
                });

                try
                {
                    using (var archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
                    {
                        logger.Info($"Zip archive contains {archive.Entries.Count} entries");
                        
                        foreach (var entry in archive.Entries)
                        {
                            if (!string.IsNullOrEmpty(entry.Name))
                            {
                                string destinationPath = Path.Combine(extractPath, entry.FullName);
                                
                                // Create directory if needed
                                string directory = Path.GetDirectoryName(destinationPath);
                                if (!Directory.Exists(directory))
                                {
                                    Directory.CreateDirectory(directory);
                                }
                                
                                // Extract file
                                entry.ExtractToFile(destinationPath, true);
                                logger.Info($"Extracted: {entry.Name} ({entry.Length} bytes)");
                            }
                        }
                    }
                    
                    logger.Info("Extraction completed successfully");
                    
                    // Give file system time to finalize all writes
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Extraction error: {ex.Message}");
                    SafeUpdateUI(progressForm, () =>
                    {
                        statusLabel.Text = "Extraction failed!";
                    });
                    await Task.Delay(3000);
                    return false;
                }

                // Find installer
                SafeUpdateUI(progressForm, () =>
                {
                    statusLabel.Text = "Locating installer...";
                    Application.DoEvents();
                });

                var allFiles = Directory.GetFiles(extractPath, "*.*", SearchOption.AllDirectories);
                logger.Info($"Total files in extract folder: {allFiles.Length}");
                
                string installerPath = null;
                
                // Look for the SPECIFIC setup executable - VBCABLE_Setup_x64.exe
                foreach (var file in allFiles)
                {
                    string fileName = Path.GetFileName(file);
                    logger.Info($"Found file: {fileName}");
                    
                    // First priority: Look for the x64 setup file
                    if (fileName.Equals("VBCABLE_Setup_x64.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        installerPath = file;
                        logger.Info($"Found x64 installer: {installerPath}");
                        break;
                    }
                }
                
                // Fallback: If x64 not found, look for x86 version
                if (string.IsNullOrEmpty(installerPath))
                {
                    foreach (var file in allFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        
                        if (fileName.Equals("VBCABLE_Setup.exe", StringComparison.OrdinalIgnoreCase) ||
                            fileName.Equals("VBCABLE_Setup_x86.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            installerPath = file;
                            logger.Info($"Found fallback installer: {installerPath}");
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(installerPath) || !File.Exists(installerPath))
                {
                    logger.Error("VBCABLE_Setup_x64.exe not found in extracted files");
                    logger.Error("Available files:");
                    foreach (var file in allFiles)
                    {
                        logger.Error($"  - {Path.GetFileName(file)}");
                    }
                    
                    SafeUpdateUI(progressForm, () =>
                    {
                        statusLabel.Text = "Installer not found!";
                    });
                    await Task.Delay(3000);
                    return false;
                }

                var installerInfo = new FileInfo(installerPath);
                logger.Info($"Installer file: {installerInfo.Name}, Size: {installerInfo.Length} bytes");

                // Close progress form on UI thread
                SafeUpdateUI(progressForm, () =>
                {
                    progressForm.Close();
                });
                progressForm = null;

                // DISABLED: Show instructions - now auto-launches installer
                // var result = MessageBox.Show(
                //     $"VB-Audio Cable installer is ready!\n\n" +
                //     $"Installer: {Path.GetFileName(installerPath)}\n\n" +
                //     "IMPORTANT STEPS:\n" +
                //     "1. Click OK to launch the installer\n" +
                //     "2. Click 'Yes' when Windows asks for administrator permission\n" +
                //     "3. In the installer window, click 'Install Driver'\n" +
                //     "4. Wait for 'Installation Complete' message\n" +
                //     "5. Click 'OK' to finish\n\n" +
                //     "The application will verify installation afterwards.",
                //     "Ready to Install VB-Audio Cable",
                //     MessageBoxButtons.OKCancel,
                //     MessageBoxIcon.Information);
                //
                // if (result != DialogResult.OK)
                // {
                //     logger.Info("User cancelled installation");
                //     return false;
                // }

                // Auto-launch installer without prompting
                logger.Info($"Auto-launching installer: {Path.GetFileName(installerPath)}");

                // Run installer
                logger.Info($"Launching installer: {installerPath}");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    WorkingDirectory = Path.GetDirectoryName(installerPath),
                    UseShellExecute = true,
                    Verb = "runas" // Request admin privileges
                };

                Process installerProcess = null;
                try
                {
                    installerProcess = Process.Start(startInfo);
                    
                    if (installerProcess == null)
                    {
                        logger.Error("Failed to start installer process");
                        MessageBox.Show("Failed to start installer. Please try manual installation.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }

                    logger.Info($"Installer process started with ID: {installerProcess.Id}");
                    
                    // Wait for installer to complete
                    logger.Info("Waiting for installer to complete...");
                    installerProcess.WaitForExit();
                    
                    int exitCode = installerProcess.ExitCode;
                    logger.Info($"Installer exited with code: {exitCode}");
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    logger.Error(ex, "Win32 error starting installer: " + ex.Message);
                    
                    if (ex.NativeErrorCode == 1223) // User cancelled UAC
                    {
                        MessageBox.Show(
                            "Administrator privileges are required to install VB-Audio Cable.\n\n" +
                            "Please click 'Yes' when Windows asks for permission.",
                            "Administrator Access Required",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error running installer: " + ex.Message);
                    return false;
                }

                // Wait for system to register the driver
                logger.Info("Waiting for system to register driver...");
                await Task.Delay(5000);

                // Verify installation
                logger.Info("Verifying installation...");
                bool installed = IsVBAudioCableInstalled();
                logger.Info($"Installation verification result: {installed}");

                if (installed)
                {
                    // Cleanup
                    try
                    {
                        if (Directory.Exists(workingDir))
                        {
                            Directory.Delete(workingDir, true);
                            logger.Info("Cleaned up temporary files");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Warn($"Failed to cleanup temp files: {ex.Message}");
                    }

                    MessageBox.Show(
                        "✅ VB-Audio Cable installed successfully!\n\n" +
                        "The application will now restart to detect the new audio devices.",
                        "Installation Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    
                    Application.Restart();
                    Environment.Exit(0);
                    return true;
                }
                else
                {
                    MessageBox.Show(
                        "⚠️ Installation completed, but VB-Audio Cable devices were not detected yet.\n\n" +
                        "This is normal. Please:\n" +
                        "1. Restart your computer\n" +
                        "2. Launch the application again\n\n" +
                        "If the problem persists, try manual installation from:\nhttps://vb-audio.com/Cable/",
                        "Restart Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Installation failed: {ex.Message}");
                logger.Error($"Stack trace: {ex.StackTrace}");
                
                MessageBox.Show(
                    $"Installation failed with error:\n{ex.Message}\n\n" +
                    "Please try manual installation from:\nhttps://vb-audio.com/Cable/",
                    "Installation Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                
                return false;
            }
            finally
            {
                // Always close progress form safely
                SafeUpdateUI(progressForm, () =>
                {
                    if (progressForm != null && !progressForm.IsDisposed)
                    {
                        progressForm.Close();
                        progressForm.Dispose();
                    }
                });
            }
        }

        private static async Task<bool> DownloadAndInstallVBAudioCable()
        {
            string tempDir = null;
            try
            {
                // Create temporary directory
                tempDir = Path.Combine(Path.GetTempPath(), "VBAudioInstaller_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(tempDir);

                logger.Info($"Created temporary directory: {tempDir}");

                // Show progress dialog
                InstallationProgressDialog progressDialog = null;
                try
                {
                    progressDialog = new InstallationProgressDialog();
                    progressDialog.Show();
                    progressDialog.UpdateStatus("Downloading VB-Audio Cable...");

                    // Download the installer
                    string zipPath = Path.Combine(tempDir, "VBCABLE_Driver_Pack45.zip");
                    string installerPath = await DownloadVBAudioInstaller(zipPath, progressDialog);

                    if (string.IsNullOrEmpty(installerPath))
                    {
                        progressDialog.UpdateStatus("Download failed!");
                        await Task.Delay(2000);
                        return false;
                    }

                    progressDialog.UpdateStatus("Installing VB-Audio Cable...");
                    progressDialog.UpdateProgress(75);

                    // Install the driver
                    bool installResult = await InstallVBAudioCable(installerPath, progressDialog);

                    progressDialog.UpdateStatus(installResult ? "Installation completed!" : "Installation failed");
                    progressDialog.UpdateProgress(100);

                    await Task.Delay(1500); // Show completion message briefly

                    if (installResult)
                    {
                        // Close progress dialog before showing success message
                        progressDialog?.Close();
                        progressDialog = null;

                        // Verify installation
                        await Task.Delay(2000); // Wait for system to recognize new devices
                        
                        if (IsVBAudioCableInstalled())
                        {
                            ShowInstallationSuccess();
                            return true;
                        }
                        else
                        {
                            logger.Warn("Installation completed but device not detected");
                            ShowInstallationWarning();
                            return false;
                        }
                    }

                    return false;
                }
                finally
                {
                    // Ensure progress dialog is always closed
                    try
                    {
                        progressDialog?.Close();
                        progressDialog?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error closing progress dialog: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error during download and installation: " + ex.Message);
                ShowInstallationError(ex.Message);
                return false;
            }
            finally
            {
                // Cleanup temporary files
                if (tempDir != null && Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                        logger.Info("Cleaned up temporary directory");
                    }
                    catch (Exception ex)
                    {
                        logger.Warn("Failed to cleanup temporary directory: " + ex.Message);
                    }
                }
            }
        }

        private static async Task<string> DownloadVBAudioInstaller(string zipPath, InstallationProgressDialog progressDialog)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(10); // 10 minute timeout

                    logger.Info("Starting download from: " + VB_CABLE_DOWNLOAD_URL);
                    progressDialog.UpdateStatus("Connecting to VB-Audio server...");

                    using (var response = await httpClient.GetAsync(VB_CABLE_DOWNLOAD_URL))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            logger.Error($"Download failed with status: {response.StatusCode} - {response.ReasonPhrase}");
                            progressDialog.UpdateStatus($"Download failed: {response.StatusCode}");
                            await Task.Delay(2000);
                            return null;
                        }

                        var totalBytes = response.Content.Headers.ContentLength ?? 0;
                        logger.Info($"Download size: {totalBytes} bytes");

                        progressDialog.UpdateStatus("Downloading VB-Audio Cable...");

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(zipPath, FileMode.Create))
                        {
                            var buffer = new byte[8192];
                            var totalRead = 0L;
                            int bytesRead;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                totalRead += bytesRead;

                                if (totalBytes > 0)
                                {
                                    var progress = (int)((totalRead * 40) / totalBytes); // 0-40% for download
                                    progressDialog.UpdateProgress(progress);
                                }
                            }
                        }

                        logger.Info($"Download completed. File size: {new FileInfo(zipPath).Length} bytes");
                        progressDialog.UpdateStatus("Download completed. Extracting installer...");
                        progressDialog.UpdateProgress(50);

                        // Extract the installer
                        string installerPath = ExtractInstaller(zipPath);
                        
                        if (!string.IsNullOrEmpty(installerPath))
                        {
                            logger.Info($"Successfully extracted installer to: {installerPath}");
                            return installerPath;
                        }
                        else
                        {
                            logger.Error("Failed to extract installer from zip file");
                            progressDialog.UpdateStatus("Error: Failed to extract installer");
                            await Task.Delay(2000);
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error downloading VB-Audio installer: " + ex.Message);
                progressDialog.UpdateStatus($"Download error: {ex.Message}");
                await Task.Delay(2000);
                return null;
            }
        }

        private static string ExtractInstaller(string zipPath)
        {
            try
            {
                string extractDir = Path.GetDirectoryName(zipPath);
                logger.Info($"Extracting zip file: {zipPath}");
                logger.Info($"Extract directory: {extractDir}");

                if (!File.Exists(zipPath))
                {
                    logger.Error($"Zip file does not exist: {zipPath}");
                    return null;
                }

                var fileInfo = new FileInfo(zipPath);
                logger.Info($"Zip file size: {fileInfo.Length} bytes");

                if (fileInfo.Length == 0)
                {
                    logger.Error("Downloaded zip file is empty");
                    return null;
                }
                
                using (var archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
                {
                    logger.Info($"Zip archive opened successfully. Entries count: {archive.Entries.Count}");
                    
                    foreach (var entry in archive.Entries)
                    {
                        logger.Info($"Found entry: {entry.Name} (Size: {entry.Length} bytes)");
                        
                        if (entry.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            string installerPath = Path.Combine(extractDir, entry.Name);
                            logger.Info($"Extracting executable to: {installerPath}");
                            
                            // Delete existing file if it exists
                            if (File.Exists(installerPath))
                            {
                                File.Delete(installerPath);
                            }
                            
                            entry.ExtractToFile(installerPath, true);
                            
                            if (File.Exists(installerPath))
                            {
                                var extractedInfo = new FileInfo(installerPath);
                                logger.Info($"Successfully extracted installer. Size: {extractedInfo.Length} bytes");
                                return installerPath;
                            }
                            else
                            {
                                logger.Error("Extraction completed but file was not created");
                                return null;
                            }
                        }
                    }
                }

                logger.Error("No executable (.exe) file found in the downloaded zip archive");
                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error extracting installer: " + ex.Message);
                return null;
            }
        }

        private static async Task<bool> InstallVBAudioCable(string installerPath, InstallationProgressDialog progressDialog)
        {
            try
            {
                // Check if running as administrator
                if (!IsRunningAsAdministrator())
                {
                    logger.Info("Requesting administrator privileges for installation");
                    return await RunInstallerAsAdmin(installerPath);
                }

                logger.Info("Starting VB-Audio Cable installation...");

                var startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/S", // Silent installation flag
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        logger.Error("Failed to start installer process");
                        return false;
                    }

                    // Wait for installation to complete with timeout
                    bool completed = await Task.Run(() => process.WaitForExit(60000)); // 60 second timeout

                    if (!completed)
                    {
                        logger.Error("Installation timed out");
                        try { process.Kill(); } catch { }
                        return false;
                    }

                    logger.Info($"Installation process completed with exit code: {process.ExitCode}");
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error installing VB-Audio Cable: " + ex.Message);
                return false;
            }
        }

        private static async Task<bool> RunInstallerAsAdmin(string installerPath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/S", // Silent installation
                    UseShellExecute = true,
                    Verb = "runas", // Request admin privileges
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        logger.Error("Failed to start installer with admin privileges");
                        return false;
                    }

                    bool completed = await Task.Run(() => process.WaitForExit(120000)); // 2 minute timeout

                    if (!completed)
                    {
                        logger.Error("Admin installation timed out");
                        return false;
                    }

                    logger.Info($"Admin installation completed with exit code: {process.ExitCode}");
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error running installer as admin: " + ex.Message);
                
                if (ex.Message.Contains("operation was canceled"))
                {
                    MessageBox.Show(
                        "Administrator privileges are required to install Virtual Audio Cable.\n\n" +
                        "Please click 'Yes' when prompted for administrator access.",
                        "Administrator Access Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                
                return false;
            }
        }

        private static bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsVBAudioCableInstalled()
        {
            try
            {
                // Method 1: Check registry (Both 32-bit and 64-bit views)
                if (CheckRegistryKey(RegistryView.Registry64) || CheckRegistryKey(RegistryView.Registry32))
                {
                    logger.Info("VB-Audio Cable found in registry");
                    return true;
                }

                // Method 2: Check for audio devices
                return CheckForVBAudioDevices();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error checking VB-Audio Cable installation: " + ex.Message);
                return CheckForVBAudioDevices(); // Fallback to device check
            }
        }

        private static bool CheckRegistryKey(RegistryView view)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var key = baseKey.OpenSubKey(VB_CABLE_REGISTRY_KEY))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckForVBAudioDevices()
        {
            try
            {
                // Check output devices
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    var capabilities = WaveOut.GetCapabilities(i);
                    if (capabilities.ProductName.IndexOf("CABLE", StringComparison.OrdinalIgnoreCase) >= 0 || 
                        capabilities.ProductName.IndexOf("VB-Audio", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        logger.Info($"Found VB-Audio output device: {capabilities.ProductName}");
                        return true;
                    }
                }

                // Check input devices
                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var capabilities = WaveIn.GetCapabilities(i);
                    if (capabilities.ProductName.IndexOf("CABLE", StringComparison.OrdinalIgnoreCase) >= 0 || 
                        capabilities.ProductName.IndexOf("VB-Audio", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        logger.Info($"Found VB-Audio input device: {capabilities.ProductName}");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error checking for VB-Audio devices: " + ex.Message);
                return false;
            }
        }

        public static string GetVirtualAudioDeviceName()
        {
            try
            {
                // Try to find CABLE Input first (most common)
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    var capabilities = WaveOut.GetCapabilities(i);
                    if (capabilities.ProductName.Contains("CABLE Input"))
                    {
                        return capabilities.ProductName;
                    }
                }

                // Fallback to any VB-Audio device
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    var capabilities = WaveOut.GetCapabilities(i);
                    if (capabilities.ProductName.Contains("CABLE") || 
                        capabilities.ProductName.Contains("VB-Audio"))
                    {
                        return capabilities.ProductName;
                    }
                }

                return "Not found";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error getting virtual audio device name: " + ex.Message);
                return "Error checking device";
            }
        }

        private static void ShowInstallationSuccess()
        {
            MessageBox.Show(
                "✅ Virtual Audio Cable installed successfully!\n\n" +
                "The application will now restart to detect the new audio devices.\n\n" +
                "After restart, you can use this application with video calls like Zoom and Google Meet.",
                "Installation Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            // Restart application
            Application.Restart();
        }

        private static void ShowInstallationWarning()
        {
            MessageBox.Show(
                "⚠️ Installation completed, but Virtual Audio Cable devices were not detected.\n\n" +
                "This might be normal - please restart your computer and try again.\n\n" +
                "If the problem persists, you may need to install VB-Audio Cable manually.",
                "Installation Warning",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private static void ShowInstallationError(string errorMessage)
        {
            DialogResult result = MessageBox.Show(
                $"❌ Failed to install Virtual Audio Cable automatically.\n\n" +
                $"Error: {errorMessage}\n\n" +
                "Would you like to download it manually from the official website?",
                "Installation Error",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Error);

            if (result == DialogResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://vb-audio.com/Cable/",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error opening VB-Audio website: " + ex.Message);
                    MessageBox.Show("Please visit: https://vb-audio.com/Cable/", "Download VB-Audio Cable");
                }
            }
        }

        private static void ShowManualInstallationInstructions()
        {
            DialogResult result = MessageBox.Show(
                "To use this application with video calls, you'll need to install VB-Audio Cable manually.\n\n" +
                "Would you like to open the official download page?",
                "Manual Installation Required",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://vb-audio.com/Cable/",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error opening VB-Audio website: " + ex.Message);
                    MessageBox.Show("Please visit: https://vb-audio.com/Cable/", "Download VB-Audio Cable");
                }
            }
        }
    }
}
