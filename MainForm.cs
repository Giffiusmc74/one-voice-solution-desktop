using NAudio.CoreAudioApi;
using NAudio.Wave;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1.src;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolBar;

namespace WindowsFormsApp1
{
    public partial class MainForm : Form
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private WaveInEvent waveIn;
        private WaveOutEvent waveOut;

        //private WaveInEvent waveInSecond;
        private WaveOutEvent waveOutSecond;
        private WasapiLoopbackCapture loopbackCapture;
        MMDevice mMDevice;

        private CancellationTokenSource cancellationTokenSource;

        BufferedWaveProvider bufferedWaveProvider;
        bool isAudioPlaying = false;

        private float currentVolume = 1.0f; // Volume level as a float between 0.0 (silent) and 1.0 (full volume)

        public static bool IsFormOpen { get; private set; }

        System.Windows.Forms.ToolTip toolTip1 = new System.Windows.Forms.ToolTip();

        private AudioFileReader audioFileReader;
        private MacrosInfo audioMacroInfo;

        ScriptsForm form;
        MacrosForm macrForm;

        private WaveOutEvent waveO;
        private AudioFileReader audioFileReader2;
        Dictionary<string, string> volumeValue = new Dictionary<string, string>();

        WaveformControl waveformControl = new WaveformControl();

        private int virtualCableInputDeviceNumber; // Device number for virtual cable input
        private int virtualCableOutputDeviceNumber; // Device number for virtual cable output

        private int speaker2DeviceNumber = 0;
        private int mic2DeviceNumber = 0;

        //private AudioIntensityMeter audioIntensityMeterSpeaker1;
        //private AudioIntensityMeter audioIntensityMeterSpeaker2;        

        MacroManager manager;
        ScriptManager scriptManager;
        AudioService audioService;
        LicenseForm licenseForm;
        public MainForm(LicenseForm license)
        {
            try
            {
                logger.Info("Main form started. Intializing Objects.");
                InitializeComponent();
                SetupResponsiveLayout();
                
                // Initialize Virtual Audio Cable automatically
                _ = Task.Run(async () => await InitializeVirtualAudioAsync());
                
                DataUtils.MigrateData(); // Centralize and migrate data first

                PopulateComboBox();
                InitializeTrackBar();
                //StartMicrophoneCaptureNew();
                AudioService.Instance.StartLoopbackCapture(cmbBoxSpeaker.Text, cmbBoxSpeaker2.Text);//StartLoopbackCapture();

                manager = DataUtils.LoadMacros(DataUtils.MacrosPath);
                scriptManager = DataUtils.LoadScripts(DataUtils.ScriptsPath);
                this.licenseForm = license;
                //LoadMacrosControls();
                IsFormOpen = true;

                btnStop.Enabled = false;

                //cmbBoxSpeaker2.MouseMove += new MouseEventHandler(cmbBoxSpeaker2_MouseMove);
                cmbBoxSpeaker2.SelectedIndexChanged += new EventHandler(cmbBoxSpeaker2_SelectedIndexChanged);
                comboBoxMicroPhone.SelectedIndexChanged += new EventHandler(comboBoxMicroPhone_SelectedIndexChanged);
                cmbBoxSpeaker.SelectedIndexChanged += new EventHandler(cmbBoxSpeaker_SelectedIndexChanged);

                // Add right-click context menu for Virtual Audio setup
                this.MouseClick += MainForm_MouseClick;

                AudioService.Instance.AudioIntensityChanged += Instance_AudioIntensityChanged;
                AudioService.Instance.AudioMicIntensityChanged += Instance_AudioMicIntensityChanged;
                AudioService.Instance.AudioRecIntensityChanged += Instance_AudioRecIntensityChanged;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
            }
        }

        private async Task InitializeVirtualAudioAsync()
        {
            try
            {
                logger.Info("Initializing Virtual Audio Cable...");

                // Use the new automatic installer
                bool isInstalled = await VirtualAudioInstaller.EnsureVirtualAudioCableInstalled();
                
                // Update UI on main thread
                this.Invoke(new Action(() =>
                {
                    if (isInstalled)
                    {
                        logger.Info("Virtual Audio Cable is ready");
                        
                        // Refresh combo boxes to include virtual audio devices
                        PopulateComboBox();
                        
                        // Show setup guide if this is first time
                        if (IsFirstTimeSetup())
                        {
                            ShowVirtualAudioSetupGuide();
                            MarkSetupComplete();
                        }
                    }
                    else
                    {
                        logger.Warn("Virtual Audio Cable not available");
                        // The installer handles user communication, so no need for additional warnings
                    }
                }));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error initializing Virtual Audio: " + ex.Message);
            }
        }

        private bool IsFirstTimeSetup()
        {
            try
            {
                string settingsFile = DataUtils.SettingsPath;
                if (File.Exists(settingsFile))
                {
                    string content = File.ReadAllText(settingsFile);
                    return !content.Contains("VirtualAudioSetupComplete=true");
                }
                return true;
            }
            catch
            {
                return true;
            }
        }

        private void MarkSetupComplete()
        {
            try
            {
                string dataPath = DataUtils.AppDataPath;
                if (!Directory.Exists(dataPath))
                {
                    Directory.CreateDirectory(dataPath);
                }

                string settingsFile = DataUtils.SettingsPath;
                string settings = "VirtualAudioSetupComplete=true\n";
                
                if (File.Exists(settingsFile))
                {
                    string existing = File.ReadAllText(settingsFile);
                    if (!existing.Contains("VirtualAudioSetupComplete"))
                    {
                        settings = existing + "\n" + settings;
                    }
                    else
                    {
                        return; // Already marked
                    }
                }
                
                File.WriteAllText(settingsFile, settings);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error marking setup complete: " + ex.Message);
            }
        }

        private void ShowVirtualAudioSetupGuide()
        {
            string guide = "🎯 Virtual Audio Cable Setup Complete!\n\n" +
                          "To use this application with video calls:\n\n" +
                          "1. In your video call application (Zoom, Google Meet, etc.):\n" +
                          "   • Go to Audio Settings\n" +
                          "   • Set Microphone to: 'CABLE Output'\n" +
                          "   • Set Speaker to: Your normal speakers/headphones\n\n" +
                          "2. In this application:\n" +
                          "   • Select 'CABLE Input' as your speaker\n" +
                          "   • Play scripts - they will be heard in your video call\n\n" +
                          "3. For monitoring (to hear yourself):\n" +
                          "   • Use the volume controls in this application\n" +
                          "   • Or enable 'Listen to this device' in Windows Sound settings\n\n" +
                          "Right-click anywhere for more Virtual Audio options!";

            MessageBox.Show(guide, "Virtual Audio Setup Guide", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        //private void Instance_AudioIntensityChanged(object sender, AudioIntensityEventArgs e)
        //{
        //    try
        //    {
        //        if (this != null && this.IsHandleCreated && !this.IsDisposed)
        //        {
        //            this.Invoke(new Action(() =>
        //            {
        //                // Update your UI controls based on e.Speaker1Intensity

        //                float volumeAdjustmentSpeaker1 = volumeA1Speaker.Value / 100f;
        //                float volumeAdjustmentSpeaker2 = volumeA2Speaker.Value / 100f;

        //                int speaker1Adjusted = (int)(e.Intensity * volumeAdjustmentSpeaker1);
        //                int speaker2Adjusted = (int)(e.Intensity * volumeAdjustmentSpeaker2);
        //                if (audioIntensityMeterSpeaker11 != null)
        //                    audioIntensityMeterSpeaker11.UpdateIntensity(speaker1Adjusted);
        //                if (audioIntensityMeterSpeaker22 != null && AudioService.Instance.waveOutSecond != null)
        //                    audioIntensityMeterSpeaker22.UpdateIntensity(speaker2Adjusted);

        //            }));
        //        }
        //        else
        //        {
        //            if (this == null)
        //                logger.Error("Form is null");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error(ex, "An error occurred while performing an operation.");
        //        logger.Error("ExceptionMessage: " + ex.Message);
        //        logger.Error("Exception: " + ex.ToString());
        //    }
        //}

        //private void Instance_AudioRecIntensityChanged(object sender, AudioIntensityEventArgs e)
        //{
        //    try
        //    {

        //        if (this != null && this.IsHandleCreated && !this.IsDisposed)
        //        {
        //            this.Invoke(new Action(() =>
        //            {
        //                // Update your AudioIntensityMeter control on the UI thread
        //                if (audioIntensityMeterRecIn.InvokeRequired && audioIntensityMeterRecOut.InvokeRequired)
        //                {
        //                    audioIntensityMeterRecIn.Invoke(new Action(() =>
        //                    {
        //                        float volumeAdjustmentRecIn = volumeA1RecIn.Value / 100f;
        //                        int recInAdjusted = (int)(e.Intensity * volumeAdjustmentRecIn);
        //                        audioIntensityMeterRecIn.UpdateIntensity(recInAdjusted);

        //                    }));
        //                    audioIntensityMeterRecOut.Invoke(new Action(() =>
        //                    {
        //                        float volumeAdjustmentRecOut = volumeA1RecOut.Value / 100f;
        //                        int recOutAdjusted = (int)(e.Intensity * volumeAdjustmentRecOut);
        //                        audioIntensityMeterRecOut.UpdateIntensity(recOutAdjusted);

        //                    }));
        //                }
        //                else
        //                {
        //                    float volumeAdjustmentRecIn = volumeA1RecIn.Value / 100f;
        //                    int recInAdjusted = (int)(e.Intensity * volumeAdjustmentRecIn);
        //                    audioIntensityMeterRecIn.UpdateIntensity(recInAdjusted);

        //                    float volumeAdjustmentRecOut = volumeA1RecOut.Value / 10f;
        //                    int recOutAdjusted = (int)(e.Intensity * volumeAdjustmentRecOut);
        //                    audioIntensityMeterRecOut.UpdateIntensity(recOutAdjusted);
        //                }
        //            }));
        //        }
        //        else
        //        {
        //            if (this == null)
        //                logger.Error("Form is null");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error(ex, "An error occurred while performing an operation.");
        //        logger.Error("ExceptionMessage: " + ex.Message);
        //        logger.Error("Exception: " + ex.ToString());
        //    }
        //}

        //private void Instance_AudioMicIntensityChanged(object sender, AudioIntensityEventArgs e)
        //{
        //    try
        //    {
        //        if (this != null && this.IsHandleCreated && !this.IsDisposed)
        //        {
        //            this.Invoke(new Action(() =>
        //            {
        //                if (this.IsHandleCreated && !this.IsDisposed)
        //                {
        //                    // Adjust intensity based on the TrackBar's volume setting
        //                    this.Invoke((MethodInvoker)(() =>
        //                    {
        //                        int volumeSetting = volumeA1Mic.Value; // Assume trackBarVolume is your TrackBar control
        //                        int adjustedIntensity = e.Intensity * volumeSetting / 100; // Scale intensity by volume percentage
        //                        adjustedIntensity = Math.Min(adjustedIntensity, 100); // Ensure it doesn't exceed 100%
        //                        if (audioIntensityMeterMic1 != null)
        //                            audioIntensityMeterMic1.UpdateIntensity(adjustedIntensity);
        //                    }));
        //                }

        //            }));
        //        }
        //        else
        //        {
        //            if (this == null)
        //                logger.Error("Form is null");
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error(ex, "An error occurred while performing an operation.");
        //        logger.Error("ExceptionMessage: " + ex.Message);
        //        logger.Error("Exception: " + ex.ToString());
        //    }
        //}

        private void Instance_AudioIntensityChanged(object sender, AudioIntensityEventArgs e)
        {
            try
            {
                UpdateUI(() =>
                {
                    float volumeAdjustmentSpeaker1 = volumeA1Speaker.Value / 100f;
                    float volumeAdjustmentSpeaker2 = volumeA2Speaker.Value / 100f;
                    audioIntensityMeterSpeaker11?.UpdateIntensity((int)(e.Intensity * volumeAdjustmentSpeaker1));
                    if (AudioService.Instance.waveOutSecond != null)
                        audioIntensityMeterSpeaker22?.UpdateIntensity((int)(e.Intensity * volumeAdjustmentSpeaker2));
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void Instance_AudioRecIntensityChanged(object sender, AudioIntensityEventArgs e)
        {
            try
            {
                UpdateUI(() =>
                {
                    audioIntensityMeterRecIn?.UpdateIntensity((int)(e.Intensity * volumeA1RecIn.Value / 100f));
                    audioIntensityMeterRecOut?.UpdateIntensity((int)(e.Intensity * volumeA1RecOut.Value / 100f));
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void Instance_AudioMicIntensityChanged(object sender, AudioIntensityEventArgs e)
        {
            try
            {
                UpdateUI(() =>
                {
                    int adjustedIntensity = Math.Min((int)(e.Intensity * volumeA1Mic.Value / 100), 100);
                    audioIntensityMeterMic1?.UpdateIntensity(adjustedIntensity);
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void UpdateUI(Action updateAction)
        {
            try
            {
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    this.Invoke(updateAction);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void cmbBoxSpeaker_SelectedIndexChanged(object sender, EventArgs e)
        {
            System.Windows.Forms.ToolTip toolTip1 = new System.Windows.Forms.ToolTip();
            toolTip1.AutoPopDelay = 0;
            toolTip1.InitialDelay = 0;
            toolTip1.ReshowDelay = 0;
            toolTip1.ShowAlways = true;
            toolTip1.SetToolTip(this.cmbBoxSpeaker, cmbBoxSpeaker.Items[cmbBoxSpeaker.SelectedIndex].ToString());
        }

        private void comboBoxMicroPhone_SelectedIndexChanged(object sender, EventArgs e)
        {
            System.Windows.Forms.ToolTip toolTip1 = new System.Windows.Forms.ToolTip();
            toolTip1.AutoPopDelay = 0;
            toolTip1.InitialDelay = 0;
            toolTip1.ReshowDelay = 0;
            toolTip1.ShowAlways = true;
            toolTip1.SetToolTip(this.comboBoxMicroPhone, comboBoxMicroPhone.Items[comboBoxMicroPhone.SelectedIndex].ToString());
        }

        private void cmbBoxSpeaker2_SelectedIndexChanged(object sender, EventArgs e)
        {
            System.Windows.Forms.ToolTip toolTip1 = new System.Windows.Forms.ToolTip();
            toolTip1.AutoPopDelay = 0;
            toolTip1.InitialDelay = 0;
            toolTip1.ReshowDelay = 0;
            toolTip1.ShowAlways = true;
            toolTip1.SetToolTip(this.cmbBoxSpeaker2, cmbBoxSpeaker2.Items[cmbBoxSpeaker2.SelectedIndex].ToString());
        }

        private void cmbBoxSpeaker2_MouseMove(object sender, MouseEventArgs e)
        {
            //System.Windows.Forms.ComboBox comboBox = sender as System.Windows.Forms.ComboBox;
            //int index = comboBox.IndexFromPoint(e.Location);
            //if (index >= 0 && index < comboBox.Items.Count)
            //{
            //    // Assuming each item in the ComboBox is a string
            //    string itemText = comboBox.Items[index].ToString();

            //    // Here you determine the tooltip text based on the item.
            //    // This example directly uses the item's text, but you could customize it.
            //    string toolTipText = itemText;

            //    // Set the tooltip text
            //    toolTip1.SetToolTip(comboBox, toolTipText);
            //}
        }

        private void InitializeTrackBar()
        {
            try
            {
                volumeValue.Clear();
                volumeValue = DataUtils.LoadVolumeValue(DataUtils.VolumeDataPath);

                if (volumeValue != null && volumeValue.Count > 0)
                {
                    volumeA1Mic.Value = volumeValue.ContainsKey("volumeA1Mic") && int.TryParse(volumeValue["volumeA1Mic"], out int A1Mic) ? A1Mic : 100;
                    volumeA1RecIn.Value = volumeValue.ContainsKey("volumeA1RecIn") && int.TryParse(volumeValue["volumeA1RecIn"], out int A1Rec) ? A1Rec : 100;
                    volumeA1RecOut.Value = volumeValue.ContainsKey("volumeA1RecOut") && int.TryParse(volumeValue["volumeA1RecOut"], out int A1RecOut) ? A1RecOut : 100;
                    volumeA1Speaker.Value = volumeValue.ContainsKey("volumeA1Speaker") && int.TryParse(volumeValue["volumeA1Speaker"], out int A1Speaker) ? A1Speaker : 100;
                    volumeA2Speaker.Value = volumeValue.ContainsKey("volumeA2Speaker") && int.TryParse(volumeValue["volumeA2Speaker"], out int A2Speaker) ? A2Speaker : 100;

                    string cmbItem = volumeValue.ContainsKey("cmbMicValue") ? volumeValue["cmbMicValue"] : "";
                    SelectItemInComboBox(comboBoxMicroPhone, cmbItem);

                    cmbItem = volumeValue.ContainsKey("cmbSpeaker1Value") ? volumeValue["cmbSpeaker1Value"] : "";
                    SelectItemInComboBox(cmbBoxSpeaker, cmbItem);

                    cmbItem = volumeValue.ContainsKey("cmbSpeaker2Value") ? volumeValue["cmbSpeaker2Value"] : "";
                    SelectItemInComboBox(cmbBoxSpeaker2, cmbItem);
                }
                else
                {
                    volumeA1Mic.Value = 100;
                    volumeA1RecIn.Value = 100;
                    volumeA1RecOut.Value = 100;
                    volumeA1Speaker.Value = 100;
                    volumeA2Speaker.Value = 100;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void StartLoopbackCapture()
        {
            if (waveOutSecond != null)
            {
                waveOutSecond.Stop();
                waveOutSecond.Dispose();

                waveOutSecond = null;
            }

            mMDevice = GetSpecificPlaybackDevice(cmbBoxSpeaker.Text);
            // Initialize loopback capture from Speaker 1
            if (mMDevice != null)
                loopbackCapture = new WasapiLoopbackCapture(mMDevice); // Default playback device, adjust as needed
            else
                loopbackCapture = new WasapiLoopbackCapture();

            loopbackCapture.DataAvailable += OnLoopbackDataAvailable;
            loopbackCapture.RecordingStopped += OnLoopbackRecordingStopped;
            //loopbackCapture.StartRecording();

            // Initialize waveOut for Speaker 2
            string deviceSpeaker2 = cmbBoxSpeaker2.Text.Contains(":") ? cmbBoxSpeaker2.Text.Substring(0, cmbBoxSpeaker2.Text.Length - 3) : cmbBoxSpeaker2.Text;

            if (!string.IsNullOrEmpty(deviceSpeaker2) && deviceSpeaker2 != "NONE")
            {
                waveOutSecond = new WaveOutEvent
                {
                    DeviceNumber = FindWaveOutDeviceNumber(deviceSpeaker2), // Implement this to find the correct device index
                    DesiredLatency = 120 // Experiment with this value, starting as low as 100ms
                };
            }

            if (waveOutSecond == null || waveOutSecond.DeviceNumber == -1)
            {
                bufferedWaveProvider = new BufferedWaveProvider(loopbackCapture.WaveFormat)
                {
                    DiscardOnBufferOverflow = true,
                    BufferDuration = TimeSpan.FromMilliseconds(100)
                };

                loopbackCapture.StartRecording();
                // Message box error because then speaker 1 audio will be relay to speaker 1 creating loop
            }
            else
            {
                // Since WasapiLoopbackCapture captures in IEEE float format, make sure your WaveOut supports this
                // Initialize BufferedWaveProvider to buffer audio data
                bufferedWaveProvider = new BufferedWaveProvider(loopbackCapture.WaveFormat)
                {
                    DiscardOnBufferOverflow = true,
                    BufferDuration = TimeSpan.FromMilliseconds(100)
                };
                waveOutSecond.Init(bufferedWaveProvider);

                waveOutSecond.Play();

                loopbackCapture.StartRecording();
            }
        }

        private void OnLoopbackDataAvailable(object sender, WaveInEventArgs e)
        {
            // Add captured data to the buffer for playback
            bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);

            float rmsValue = CalculateRMS(e.Buffer);
            int intensityPercentage = (int)(rmsValue * 100);

            // Assuming you have two separate controls: audioIntensityMeterSpeaker1 and audioIntensityMeterSpeaker2
            // Update UI controls safely from the background thread
            try
            {
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    this.Invoke(new Action(() =>
                    {
                        float volumeAdjustmentSpeaker1 = volumeA1Speaker.Value / 100f;
                        float volumeAdjustmentSpeaker2 = volumeA2Speaker.Value / 100f;

                        int speaker1Adjusted = (int)(intensityPercentage * volumeAdjustmentSpeaker1);
                        int speaker2Adjusted = (int)(intensityPercentage * volumeAdjustmentSpeaker2);
                        if (audioIntensityMeterSpeaker11 != null)
                            audioIntensityMeterSpeaker11.UpdateIntensity(speaker1Adjusted);
                        if (audioIntensityMeterSpeaker22 != null && waveOutSecond != null)
                            audioIntensityMeterSpeaker22.UpdateIntensity(speaker2Adjusted);
                    }));
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private float CalculateRMS(byte[] buffer)
        {
            int bytesPerSample = (loopbackCapture.WaveFormat.BitsPerSample / 8) * loopbackCapture.WaveFormat.Channels;
            int sampleCount = buffer.Length / bytesPerSample;

            float sumOfSquares = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float sampleVal = BitConverter.ToSingle(buffer, i * bytesPerSample);
                sumOfSquares += sampleVal * sampleVal;
            }
            return (float)Math.Sqrt(sumOfSquares / sampleCount);
        }

        private void OnLoopbackRecordingStopped(object sender, StoppedEventArgs e)
        {
            try
            {
                if (loopbackCapture != null)
                {
                    loopbackCapture.Dispose();
                }

                if (waveOutSecond != null)
                {
                    waveOutSecond.Stop();
                    waveOutSecond.Dispose();
                }

                if (e.Exception != null)
                {
                    MessageBox.Show($"An error occurred: {e.Exception.Message}");

                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private MMDevice GetSpecificPlaybackDevice(string deviceNameHint)
        {
            if (deviceNameHint == null || string.IsNullOrEmpty(deviceNameHint))
                return null;

            if (deviceNameHint.Contains(":")) {
                // removing : index
                deviceNameHint = deviceNameHint.Substring(0, deviceNameHint.Length - 3);
            }
            var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                if (device.FriendlyName.Contains(deviceNameHint))
                {
                    return device;
                }
            }
            return null; // Or return a default device if preferred
        }

        private void InitializeWaveform()
        {
            // waveformControl.Location = new Point(10, 10),
            waveformControl.Size = new Size(200, 70);
            waveformControl.BackColor = Color.White;
            waveformControl.Dock = DockStyle.Fill;
            //flowLayoutPanelWaveForm.Controls.Add(waveformControl);
        }

        private void SelectItemInComboBox(System.Windows.Forms.ComboBox comboBox, string searchString)
        {
            try
            {
                if (string.IsNullOrEmpty(searchString) || searchString == "NONE")
                    return;

                if (searchString.Contains(":"))
                {
                    searchString = searchString.Substring(0, searchString.Length - 3);
                }
                // Loop through all items in the ComboBox
                for (int i = 0; i < comboBox.Items.Count; i++)
                {
                    if (comboBox.Items[i].ToString().Contains(searchString))
                    {
                        comboBox.SelectedIndex = i;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void PopulateComboBox()
        {
            try {
                comboBoxMicroPhone.Items.Clear();
                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var capabilities = WaveIn.GetCapabilities(i);
                    if (capabilities.ProductName.ToLower().Contains("cable"))
                        continue;// excluding virtual cable
                    comboBoxMicroPhone.Items.Add(capabilities.ProductName + ":" + i.ToString());
                    //cmbBoxMicroPhone2.Items.Add(capabilities.ProductName + ":" + i.ToString());
                }

                if (comboBoxMicroPhone.Items.Count > 0)
                    comboBoxMicroPhone.SelectedIndex = 0;

                //if (cmbBoxMicroPhone2.Items.Count > 0)
                //    cmbBoxMicroPhone2.SelectedIndex = 0;

                cmbBoxSpeaker.Items.Clear();
                cmbBoxSpeaker2.Items.Clear();

                cmbBoxSpeaker2.Items.Add("NONE");
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    var capabilities = WaveOut.GetCapabilities(i);
                    if (capabilities.ProductName.ToLower().Contains("cable"))
                        continue; // excluding virtual cable
                    cmbBoxSpeaker.Items.Add(capabilities.ProductName + ":" + i.ToString());
                    cmbBoxSpeaker2.Items.Add(capabilities.ProductName + ":" + i.ToString());
                }

                if (cmbBoxSpeaker.Items.Count > 0)
                    cmbBoxSpeaker.SelectedIndex = 0;

                if (cmbBoxSpeaker2.Items.Count > 0)
                    cmbBoxSpeaker2.SelectedIndex = 0;

                //if (cmbBoxSpeaker2.Items.Count > 0)
                //{
                //    cmbBoxSpeaker2.SelectedIndex = 1;
                //}
                //else
                //{
                //    cmbBoxSpeaker2.SelectedIndex = -1;
                //}
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }


        private int FindWaveInDeviceNumber(string targetDeviceName)
        {
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var capabilities = WaveIn.GetCapabilities(i);
                if (capabilities.ProductName.ToLower().Contains(targetDeviceName.ToLower()))
                {
                    return i;
                }
            }
            return -1; // Device not found
        }

        private int FindWaveOutDeviceNumber(string targetDeviceName)
        {
            try
            {
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    var capabilities = WaveOut.GetCapabilities(i);
                    if (capabilities.ProductName.ToLower().Contains(targetDeviceName.ToLower()))
                    {
                        return i;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
            return -1; // Device not found
        }

        private void StartMicrophoneCapture(int microPhoneIndex = 0)
        {
            //// Initialize WaveIn for agent's machine main micrphone
            //waveIn = new WaveIn
            //{
            //    DeviceNumber = virtualCableInputDeviceNumber, // must be selected by a drop down
            //    WaveFormat = new WaveFormat(44100, 16, 1),
            //    BufferMilliseconds = 70
            //};

            //// Initialize WaveOut for virtual audio cable
            //waveOut = new WaveOut
            //{
            //    DeviceNumber = FindWaveInDeviceNumber("cable"), // Set the virtual audio cable device number
            //    DesiredLatency = 125 // Adjust latency as needed
            //};

            //// Start capturing
            //waveIn.DataAvailable += WaveIn_DataAvailable;
            //if (waveIn != null)
    waveIn.StartRecording();

            //// Initialize WaveInProvider for playback
            //waveOut.Init(new WaveInProvider(waveIn));
            //// Start playback to the virtual audio cable
            //waveOut.Play();
        }

        private void StartMicrophoneCaptureNew()
        {
            // Initialize WaveIn for agent's machine main micrphone

            string micrphoneName = comboBoxMicroPhone.SelectedItem.ToString();
            if (!string.IsNullOrEmpty(micrphoneName))
            {
                string[] mic = micrphoneName.Split(':');
                if (mic[0].Length > 0)
                {
                    virtualCableInputDeviceNumber = FindWaveInDeviceNumber(mic[0]);
                }

                waveIn = new WaveInEvent
                {
                    DeviceNumber = virtualCableInputDeviceNumber, // must be selected by a drop down
                    WaveFormat = new WaveFormat(44100, 16, 1),
                    BufferMilliseconds = 70
                };

                // Initialize WaveOut for virtual audio cable
                waveOut = new WaveOutEvent
                {
                    DeviceNumber = FindWaveOutDeviceNumber("cable"), // Set the virtual audio cable device number
                    DesiredLatency = 125 // Adjust latency as needed
                };


                //// Buffer for storing audio data for playback
                //bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat);

                //// Handle DataAvailable event to capture audio and feed to buffer
                //waveIn.DataAvailable += (sender, e) =>
                //{
                //    bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
                //};
                waveIn.DataAvailable += WaveIn_DataAvailable1;

                // Initialize WaveInProvider for playback
                var waveInProvider = new WaveInProvider(waveIn);
                waveOut.Init(waveInProvider);

                if (waveIn != null)
    waveIn.StartRecording();

                // Start playback to the virtual audio cable
                waveOut.Play();
            }
        }

        private void WaveIn_DataAvailable1(object sender, WaveInEventArgs e)
        {
            double sumOfSquares = 0;
            for (int i = 0; i < e.BytesRecorded; i += 2) // 16-bit audio, hence step by 2 bytes
            {
                short sample = (short)((e.Buffer[i + 1] << 8) | e.Buffer[i]);
                sumOfSquares += sample * sample;
            }
            double rms = Math.Sqrt(sumOfSquares / (e.BytesRecorded / 2));

            // Normalize RMS to a 0-100 scale
            int normalizedRMS = (int)(rms / 32768 * 100);
            try
            {
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    // Adjust intensity based on the TrackBar's volume setting
                    this.Invoke((MethodInvoker)(() =>
                    {
                        int volumeSetting = volumeA1Mic.Value; // Assume trackBarVolume is your TrackBar control
                        int adjustedIntensity = normalizedRMS * volumeSetting / 100; // Scale intensity by volume percentage
                        adjustedIntensity = Math.Min(adjustedIntensity, 100); // Ensure it doesn't exceed 100%
                        if (audioIntensityMeterMic1 != null)
                            audioIntensityMeterMic1.UpdateIntensity(adjustedIntensity);
                    }));
                }

            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            float[] buffer = Convert16BitByteArrayToFloatArray(e.Buffer, e.BytesRecorded);
            waveformControl.AddAudioData(buffer, buffer.Length);
        }

        private float[] Convert16BitByteArrayToFloatArray(byte[] input, int length)
        {
            float[] output = new float[length / 2];
            for (int i = 0; i < output.Length; i++)
            {
                short sample = (short)((input[2 * i + 1] << 8) | input[2 * i]);
                output[i] = sample / 32768f;
            }
            return output;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // Check if the form instance exists and whether it has been disposed
            if (macrForm == null || macrForm.IsDisposed)
            {
                macrForm = new MacrosForm(manager);
                macrForm.Show();
            }
            else
            {
                // If the form is minimized, restore it
                if (macrForm.WindowState == FormWindowState.Minimized)
                {
                    macrForm.WindowState = FormWindowState.Normal;
                }

                // Bring the form to the front
                macrForm.BringToFront();
                // Alternatively, you can use Activate() to make it the active window
                macrForm.Activate();
            }
        }


        #region ShortKeyOperations


        // Capture keyboard data to start respective audio
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Check for Shift + F1
            if (keyData == (Keys.Shift | Keys.F1))
            {
                PerformShiftF1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F2
            if (keyData == (Keys.Shift | Keys.F2))
            {
                PerformShiftF2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F3
            if (keyData == (Keys.Shift | Keys.F3))
            {
                PerformShiftF3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F4
            if (keyData == (Keys.Shift | Keys.F4))
            {
                PerformShiftF4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F5
            if (keyData == (Keys.Shift | Keys.F5))
            {
                // Perform the operation for Shift + F1
                PerformShiftF5Operation();
                return true;
            }

            // Check for Shift + F6
            if (keyData == (Keys.Shift | Keys.F6))
            {
                PerformShiftF6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F7
            if (keyData == (Keys.Shift | Keys.F7))
            {
                PerformShiftF7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F8
            if (keyData == (Keys.Shift | Keys.F8))
            {
                PerformShiftF8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F9
            if (keyData == (Keys.Shift | Keys.F9))
            {
                PerformShiftF9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F10
            if (keyData == (Keys.Shift | Keys.F10))
            {
                PerformShiftF10Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F11
            if (keyData == (Keys.Shift | Keys.F11))
            {
                PerformShiftF11Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F12
            if (keyData == (Keys.Shift | Keys.F12))
            {
                PerformShiftF12Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + A
            if (keyData == (Keys.Shift | Keys.A))
            {
                PerformShiftAOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + B
            if (keyData == (Keys.Shift | Keys.B))
            {
                PerformShiftBOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + C
            if (keyData == (Keys.Shift | Keys.C))
            {
                PerformShiftCOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + D
            if (keyData == (Keys.Shift | Keys.D))
            {
                PerformShiftDOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + E
            if (keyData == (Keys.Shift | Keys.E))
            {
                PerformShiftEOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F
            if (keyData == (Keys.Shift | Keys.F))
            {
                PerformShiftFOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + G
            if (keyData == (Keys.Shift | Keys.G))
            {
                PerformShiftGOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + H
            if (keyData == (Keys.Shift | Keys.H))
            {
                PerformShiftHOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + I
            if (keyData == (Keys.Shift | Keys.I))
            {
                PerformShiftIOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + J
            if (keyData == (Keys.Shift | Keys.J))
            {
                PerformShiftJOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + K
            if (keyData == (Keys.Shift | Keys.K))
            {
                PerformShiftKOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + L
            if (keyData == (Keys.Shift | Keys.L))
            {
                PerformShiftLOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + M
            if (keyData == (Keys.Shift | Keys.M))
            {
                PerformShiftMOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + N
            if (keyData == (Keys.Shift | Keys.N))
            {
                PerformShiftNOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + O
            if (keyData == (Keys.Shift | Keys.O))
            {
                PerformShiftOOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + P
            if (keyData == (Keys.Shift | Keys.P))
            {
                PerformShiftPOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Q
            if (keyData == (Keys.Shift | Keys.Q))
            {
                PerformShiftQOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + R
            if (keyData == (Keys.Shift | Keys.R))
            {
                PerformShiftROperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + S
            if (keyData == (Keys.Shift | Keys.S))
            {
                PerformShiftSOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + T
            if (keyData == (Keys.Shift | Keys.T))
            {
                PerformShiftTOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + U
            if (keyData == (Keys.Shift | Keys.U))
            {
                PerformShiftUOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + V
            if (keyData == (Keys.Shift | Keys.V))
            {
                PerformShiftVOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + W
            if (keyData == (Keys.Shift | Keys.W))
            {
                PerformShiftWOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + X
            if (keyData == (Keys.Shift | Keys.X))
            {
                PerformShiftXOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Y
            if (keyData == (Keys.Shift | Keys.Y))
            {
                PerformShiftYOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Z
            if (keyData == (Keys.Shift | Keys.Z))
            {
                PerformShiftZOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 1
            if (keyData == (Keys.Shift | Keys.D1))
            {
                PerformShift1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 2
            if (keyData == (Keys.Shift | Keys.D2))
            {
                PerformShift2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 3
            if (keyData == (Keys.Shift | Keys.D3))
            {
                PerformShift3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 4
            if (keyData == (Keys.Shift | Keys.D4))
            {
                PerformShift4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 5
            if (keyData == (Keys.Shift | Keys.D5))
            {
                PerformShift5Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 6
            if (keyData == (Keys.Shift | Keys.D6))
            {
                PerformShift6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 7
            if (keyData == (Keys.Shift | Keys.D7))
            {
                PerformShift7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 8
            if (keyData == (Keys.Shift | Keys.D8))
            {
                PerformShift8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 9
            if (keyData == (Keys.Shift | Keys.D9))
            {
                PerformShift9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 0
            if (keyData == (Keys.Shift | Keys.D0))
            {
                PerformShift0Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num1
            if (keyData == (Keys.Shift | Keys.NumPad1))
            {
                PerformShiftNum1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num2
            if (keyData == (Keys.Shift | Keys.NumPad2))
            {
                PerformShiftNum2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num3
            if (keyData == (Keys.Shift | Keys.NumPad3))
            {
                PerformShiftNum3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num4
            if (keyData == (Keys.Shift | Keys.NumPad4))
            {
                PerformShiftNum4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num5
            if (keyData == (Keys.Shift | Keys.NumPad5))
            {
                PerformShiftNum5Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num6
            if (keyData == (Keys.Shift | Keys.NumPad6))
            {
                PerformShiftNum6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num7
            if (keyData == (Keys.Shift | Keys.NumPad7))
            {
                PerformShiftNum7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num8
            if (keyData == (Keys.Shift | Keys.NumPad8))
            {
                PerformShiftNum8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num9
            if (keyData == (Keys.Shift | Keys.NumPad9))
            {
                PerformShiftNum9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num0
            if (keyData == (Keys.Shift | Keys.NumPad0))
            {
                PerformShiftNum0Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + ,
            if (keyData == (Keys.Shift | Keys.Oemcomma))
            {
                PerformShiftCommaOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + .
            if (keyData == (Keys.Shift | Keys.OemPeriod))
            {
                PerformShiftPeriodOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + ;
            if (keyData == (Keys.Shift | Keys.OemSemicolon))
            {
                PerformShiftSemiColonOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + '
            if (keyData == (Keys.Shift | Keys.OemQuotes))
            {
                PerformShiftQuotesOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + [
            if (keyData == (Keys.Shift | Keys.OemOpenBrackets))
            {
                PerformShiftOpenBracketOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + [
            if (keyData == (Keys.Shift | Keys.OemCloseBrackets))
            {
                PerformShiftCloseBracketOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + -
            if (keyData == (Keys.Shift | Keys.OemMinus))
            {
                PerformShiftMinusOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Space
            if (keyData == (Keys.Shift | Keys.Space))
            {
                PerformShiftSpaceOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Enter
            if (keyData == (Keys.Shift | Keys.Enter))
            {
                PerformShiftEnterOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F1
            if (keyData == (Keys.Alt | Keys.F1))
            {
                PerformAltF1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F2
            if (keyData == (Keys.Alt | Keys.F2))
            {
                PerformAltF2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F3
            if (keyData == (Keys.Alt | Keys.F3))
            {
                PerformAltF3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F4
            if (keyData == (Keys.Alt | Keys.F4))
            {
                PerformAltF4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F5
            if (keyData == (Keys.Alt | Keys.F5))
            {

                PerformAltF5Operation();
                return true;
            }

            // Check for Alt + F6
            if (keyData == (Keys.Alt | Keys.F6))
            {
                PerformAltF6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F7
            if (keyData == (Keys.Alt | Keys.F7))
            {
                PerformAltF7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F8
            if (keyData == (Keys.Alt | Keys.F8))
            {
                PerformAltF8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F9
            if (keyData == (Keys.Alt | Keys.F9))
            {
                PerformAltF9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F10
            if (keyData == (Keys.Alt | Keys.F10))
            {
                PerformAltF10Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F11
            if (keyData == (Keys.Alt | Keys.F11))
            {
                PerformAltF11Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F12
            if (keyData == (Keys.Alt | Keys.F12))
            {
                PerformAltF12Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + A
            if (keyData == (Keys.Alt | Keys.A))
            {
                PerformAltAOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + B
            if (keyData == (Keys.Alt | Keys.B))
            {
                PerformAltBOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + C
            if (keyData == (Keys.Alt | Keys.C))
            {
                PerformAltCOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + D
            if (keyData == (Keys.Alt | Keys.D))
            {
                PerformAltDOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + E
            if (keyData == (Keys.Alt | Keys.E))
            {
                PerformAltEOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F
            if (keyData == (Keys.Alt | Keys.F))
            {
                PerformAltFOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + G
            if (keyData == (Keys.Alt | Keys.G))
            {
                PerformAltGOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + H
            if (keyData == (Keys.Alt | Keys.H))
            {
                PerformAltHOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + I
            if (keyData == (Keys.Alt | Keys.I))
            {
                PerformAltIOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + J
            if (keyData == (Keys.Alt | Keys.J))
            {
                PerformAltJOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + K
            if (keyData == (Keys.Alt | Keys.K))
            {
                PerformAltKOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + L
            if (keyData == (Keys.Alt | Keys.L))
            {
                PerformAltLOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + M
            if (keyData == (Keys.Alt | Keys.M))
            {
                PerformAltMOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + N
            if (keyData == (Keys.Alt | Keys.N))
            {
                PerformAltNOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + O
            if (keyData == (Keys.Alt | Keys.O))
            {
                PerformAltOOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + P
            if (keyData == (Keys.Alt | Keys.P))
            {
                PerformAltPOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Q
            if (keyData == (Keys.Alt | Keys.Q))
            {
                PerformAltQOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + R
            if (keyData == (Keys.Alt | Keys.R))
            {
                PerformAltROperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + S
            if (keyData == (Keys.Alt | Keys.S))
            {
                PerformAltSOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + T
            if (keyData == (Keys.Alt | Keys.T))
            {
                PerformAltTOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + U
            if (keyData == (Keys.Alt | Keys.U))
            {
                PerformAltUOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + V
            if (keyData == (Keys.Alt | Keys.V))
            {
                PerformAltVOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + W
            if (keyData == (Keys.Alt | Keys.W))
            {
                PerformAltWOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + X
            if (keyData == (Keys.Alt | Keys.X))
            {
                PerformAltXOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Y
            if (keyData == (Keys.Alt | Keys.Y))
            {
                PerformAltYOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Z
            if (keyData == (Keys.Alt | Keys.Z))
            {
                PerformAltZOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 1
            if (keyData == (Keys.Alt | Keys.D1))
            {
                PerformAlt1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 2
            if (keyData == (Keys.Alt | Keys.D2))
            {
                PerformAlt2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 3
            if (keyData == (Keys.Alt | Keys.D3))
            {
                PerformAlt3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 4
            if (keyData == (Keys.Alt | Keys.D4))
            {
                PerformAlt4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 5
            if (keyData == (Keys.Alt | Keys.D5))
            {
                PerformAlt5Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 6
            if (keyData == (Keys.Alt | Keys.D6))
            {
                PerformAlt6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 7
            if (keyData == (Keys.Alt | Keys.D7))
            {
                PerformAlt7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 8
            if (keyData == (Keys.Alt | Keys.D8))
            {
                PerformAlt8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 9
            if (keyData == (Keys.Alt | Keys.D9))
            {
                PerformAlt9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 0
            if (keyData == (Keys.Alt | Keys.D0))
            {
                PerformAlt0Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num1
            if (keyData == (Keys.Alt | Keys.NumPad1))
            {
                PerformAltNum1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num2
            if (keyData == (Keys.Alt | Keys.NumPad2))
            {
                PerformAltNum2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num3
            if (keyData == (Keys.Alt | Keys.NumPad3))
            {
                PerformAltNum3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num4
            if (keyData == (Keys.Alt | Keys.NumPad4))
            {
                PerformAltNum4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num5
            if (keyData == (Keys.Alt | Keys.NumPad5))
            {
                PerformAltNum5Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num6
            if (keyData == (Keys.Alt | Keys.NumPad6))
            {
                PerformAltNum6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num7
            if (keyData == (Keys.Alt | Keys.NumPad7))
            {
                PerformAltNum7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num8
            if (keyData == (Keys.Alt | Keys.NumPad8))
            {
                PerformAltNum8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num9
            if (keyData == (Keys.Alt | Keys.NumPad9))
            {
                PerformAltNum9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num0
            if (keyData == (Keys.Alt | Keys.NumPad0))
            {
                PerformAltNum0Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + ,
            if (keyData == (Keys.Alt | Keys.Oemcomma))
            {
                PerformAltCommaOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + .
            if (keyData == (Keys.Alt | Keys.OemPeriod))
            {
                PerformAltPeriodOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + ;
            if (keyData == (Keys.Alt | Keys.OemSemicolon))
            {
                PerformAltSemiColonOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + '
            if (keyData == (Keys.Alt | Keys.OemQuotes))
            {
                PerformAltQuotesOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + [
            if (keyData == (Keys.Alt | Keys.OemOpenBrackets))
            {
                PerformAltOpenBracketOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + [
            if (keyData == (Keys.Alt | Keys.OemCloseBrackets))
            {
                PerformAltCloseBracketOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + -
            if (keyData == (Keys.Alt | Keys.OemMinus))
            {
                PerformAltMinusOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Space
            if (keyData == (Keys.Alt | Keys.Space))
            {
                PerformAltSpaceOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Enter
            if (keyData == (Keys.Alt | Keys.Enter))
            {
                PerformAltEnterOperation();
                return true; // Return true to indicate that the key has been handled
            }


            // Check for Control + F1
            if (keyData == (Keys.Control | Keys.F1))
            {
                PerformControlF1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F2
            if (keyData == (Keys.Control | Keys.F2))
            {
                PerformControlF2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F3
            if (keyData == (Keys.Control | Keys.F3))
            {
                PerformControlF3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F4
            if (keyData == (Keys.Control | Keys.F4))
            {
                PerformControlF4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F5
            if (keyData == (Keys.Control | Keys.F5))
            {
                PerformControlF5Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F6
            if (keyData == (Keys.Control | Keys.F6))
            {
                PerformControlF6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F7
            if (keyData == (Keys.Control | Keys.F7))
            {
                PerformControlF7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F8
            if (keyData == (Keys.Control | Keys.F8))
            {
                PerformControlF8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F9
            if (keyData == (Keys.Control | Keys.F9))
            {
                PerformControlF9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F10
            if (keyData == (Keys.Control | Keys.F10))
            {
                PerformControlF10Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F11
            if (keyData == (Keys.Control | Keys.F11))
            {
                PerformControlF11Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F12
            if (keyData == (Keys.Control | Keys.F12))
            {
                PerformControlF12Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + A
            if (keyData == (Keys.Control | Keys.A))
            {
                PerformCtrlAOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + B
            if (keyData == (Keys.Control | Keys.B))
            {
                PerformCtrlBOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + C
            if (keyData == (Keys.Control | Keys.C))
            {
                PerformCtrlCOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + D
            if (keyData == (Keys.Control | Keys.D))
            {
                PerformCtrlDOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + E
            if (keyData == (Keys.Control | Keys.E))
            {
                PerformCtrlEOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + F
            if (keyData == (Keys.Control | Keys.F))
            {
                PerformCtrlFOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + G
            if (keyData == (Keys.Control | Keys.G))
            {
                PerformCtrlGOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + H
            if (keyData == (Keys.Control | Keys.H))
            {
                PerformCtrlHOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + I
            if (keyData == (Keys.Control | Keys.I))
            {
                PerformCtrlIOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + J
            if (keyData == (Keys.Control | Keys.J))
            {
                PerformCtrlJOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + K
            if (keyData == (Keys.Control | Keys.K))
            {
                PerformCtrlKOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + L
            if (keyData == (Keys.Control | Keys.L))
            {
                PerformCtrlLOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + M
            if (keyData == (Keys.Control | Keys.M))
            {
                PerformCtrlMOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + N
            if (keyData == (Keys.Control | Keys.N))
            {
                PerformCtrlNOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + O
            if (keyData == (Keys.Control | Keys.O))
            {
                PerformCtrlOOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + P
            if (keyData == (Keys.Control | Keys.P))
            {
                PerformCtrlPOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Q
            if (keyData == (Keys.Control | Keys.Q))
            {
                PerformCtrlQOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + R
            if (keyData == (Keys.Control | Keys.R))
            {
                PerformCtrlROperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + S
            if (keyData == (Keys.Control | Keys.S))
            {
                PerformCtrlSOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + T
            if (keyData == (Keys.Control | Keys.T))
            {
                PerformCtrlTOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + U
            if (keyData == (Keys.Control | Keys.U))
            {
                PerformCtrlUOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + V
            if (keyData == (Keys.Control | Keys.V))
            {
                PerformCtrlVOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + W
            if (keyData == (Keys.Control | Keys.W))
            {
                PerformCtrlWOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + X
            if (keyData == (Keys.Control | Keys.X))
            {
                PerformCtrlXOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Y
            if (keyData == (Keys.Control | Keys.Y))
            {
                PerformCtrlYOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Z
            if (keyData == (Keys.Control | Keys.Z))
            {
                PerformCtrlZOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 1
            if (keyData == (Keys.Control | Keys.D1))
            {
                PerformCtrl1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 2
            if (keyData == (Keys.Control | Keys.D2))
            {
                PerformCtrl2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 3
            if (keyData == (Keys.Control | Keys.D3))
            {
                PerformCtrl3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 4
            if (keyData == (Keys.Control | Keys.D4))
            {
                PerformCtrl4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 5
            if (keyData == (Keys.Control | Keys.D5))
            {
                PerformCtrl5Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 6
            if (keyData == (Keys.Control | Keys.D6))
            {
                PerformCtrl6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 7
            if (keyData == (Keys.Control | Keys.D7))
            {
                PerformCtrl7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 8
            if (keyData == (Keys.Control | Keys.D8))
            {
                PerformCtrl8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 9
            if (keyData == (Keys.Control | Keys.D9))
            {
                PerformCtrl9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 0
            if (keyData == (Keys.Control | Keys.D0))
            {
                PerformCtrl0Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num1
            if (keyData == (Keys.Control | Keys.NumPad1))
            {
                PerformCtrlNum1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num2
            if (keyData == (Keys.Control | Keys.NumPad2))
            {
                PerformCtrlNum2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num3
            if (keyData == (Keys.Control | Keys.NumPad3))
            {
                PerformCtrlNum3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num4
            if (keyData == (Keys.Control | Keys.NumPad4))
            {
                PerformCtrlNum4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num5
            if (keyData == (Keys.Control | Keys.NumPad5))
            {
                PerformCtrlNum5Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num6
            if (keyData == (Keys.Control | Keys.NumPad6))
            {
                PerformCtrlNum6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num7
            if (keyData == (Keys.Control | Keys.NumPad7))
            {
                PerformCtrlNum7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num8
            if (keyData == (Keys.Control | Keys.NumPad8))
            {
                PerformCtrlNum8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num9
            if (keyData == (Keys.Control | Keys.NumPad9))
            {
                PerformCtrlNum9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num0
            if (keyData == (Keys.Control | Keys.NumPad0))
            {
                PerformCtrlNum0Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + ,
            if (keyData == (Keys.Control | Keys.Oemcomma))
            {
                PerformCtrlCommaOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + .
            if (keyData == (Keys.Control | Keys.OemPeriod))
            {
                PerformCtrlPeriodOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + ;
            if (keyData == (Keys.Control | Keys.OemSemicolon))
            {
                PerformCtrlSemiColonOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + '
            if (keyData == (Keys.Control | Keys.OemQuotes))
            {
                PerformCtrlQuotesOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + [
            if (keyData == (Keys.Control | Keys.OemOpenBrackets))
            {
                PerformCtrlOpenBracketOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + [
            if (keyData == (Keys.Control | Keys.OemCloseBrackets))
            {
                PerformCtrlCloseBracketOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + -
            if (keyData == (Keys.Control | Keys.OemMinus))
            {
                PerformCtrlMinusOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Space
            if (keyData == (Keys.Control | Keys.Space))
            {
                PerformCtrlSpaceOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Enter
            if (keyData == (Keys.Control | Keys.Enter))
            {
                PerformCtrlEnterOperation();
                return true; // Return true to indicate that the key has been handled
            }

            if (keyData == Keys.Escape)
            {
                AudioService.Instance.StopAudioRecordings();//StopAudioRecordings();               
                return true;
            }

            // For other keys, call the base class implementation
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void PerformShiftF1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF10Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F10").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF11Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F11").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF12Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F12").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftAOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + A").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftBOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + B").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftCOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + C").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftDOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + D").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftEOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + E").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftFOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftGOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + G").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftHOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + H").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftIOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + I").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftJOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + J").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftKOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + K").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftLOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + L").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftMOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + M").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + N").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftOOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + O").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftPOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + P").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftQOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Q").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftROperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + R").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftSOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + S").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftTOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + T").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftUOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + U").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftVOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + V").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftWOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + W").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftXOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + X").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftYOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Y").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftZOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Z").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift0Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 0").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum0Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num0").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftCommaOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + ,").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftPeriodOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + .").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftSemiColonOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + ;").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftQuotesOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + '").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftCloseBracketOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + ]").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftOpenBracketOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + [").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftMinusOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + -").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftSpaceOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Space").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftEnterOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Enter").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF10Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F10").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF11Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F11").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF12Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F12").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltAOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + A").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltBOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + B").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltCOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + C").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltDOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + D").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltEOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + E").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltFOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltGOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + G").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltHOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + H").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltIOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + I").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltJOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + J").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltKOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + K").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltLOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + L").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltMOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + M").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + N").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltOOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + O").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltPOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + P").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltQOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Q").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltROperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + R").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltSOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + S").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltTOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + T").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltUOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + U").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltVOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + V").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltWOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + W").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltXOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + X").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltYOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Y").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltZOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Z").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt0Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 0").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum0Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num0").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltCommaOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + ,").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltPeriodOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + .").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltSemiColonOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + ;").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltQuotesOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + '").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltCloseBracketOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + ]").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltOpenBracketOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + [").FirstOrDefault();
            PerformShortKeyOperation(info);
        }


        private void PerformAltMinusOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + -").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltSpaceOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Space").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltEnterOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Enter").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF10Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F10").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF11Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F11").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF12Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F12").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlAOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + A").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlBOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + B").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlCOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + C").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlDOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + D").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlEOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + E").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlFOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlGOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + G").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlHOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + H").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlIOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + I").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlJOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + J").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlKOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + K").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlLOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + L").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlMOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + M").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + N").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlOOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + O").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlPOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + P").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlQOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Q").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlROperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + R").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlSOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + S").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlTOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + T").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlUOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + U").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlVOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + V").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlWOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + W").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlXOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + X").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlYOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Y").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlZOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Z").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl0Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 0").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum0Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num0").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlCommaOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + ,").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlPeriodOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + .").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlSemiColonOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + ;").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlQuotesOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + '").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlCloseBracketOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + ]").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlOpenBracketOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + [").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlMinusOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + -").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlSpaceOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Space").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlEnterOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Enter").FirstOrDefault();
            PerformShortKeyOperation(info);
        }


        #endregion

        private void PerformShortKeyOperation(MacrosInfo info)
        {
            try
            {
                if (info != null)
                {
                    audioMacroInfo = info;
                    if (info.voiceFilePath != string.Empty && File.Exists(info.voiceFilePath))
                    {
                        AudioService.Instance.PlayAudio(info);//PlayAudio(info);
                    }
                    else
                    {
                        MessageBox.Show("No Voice file is attached with this Key. Please record or upload first.");
                    }
                }
                else
                {
                    MessageBox.Show("No Handling of this key to play recorded voice.");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void PlayAudio(MacrosInfo info)
        {
            try
            {
                if (!isAudioPlaying)
                {
                    btnStop.Enabled = true;

                    lastReadPosition = 0;

                    // Set up the WaveOut device to play through the virtual cable output for agent's
                    isAudioPlaying = true;

                    cancellationTokenSource?.Cancel();
                    cancellationTokenSource = new CancellationTokenSource();

                    waveOut = new WaveOutEvent
                    {
                        DeviceNumber = FindWaveOutDeviceNumber("cable")
                    };
                    // Load the recorded audio file
                    audioFileReader = new AudioFileReader(info.voiceFilePath);
                    // Initialize WaveOut with the audio file and start playback
                    waveOut.Init(audioFileReader);
                    waveOut.Play();
                    // Mute or stop the WaveIn capturing (agent's microphone)
                    waveIn.StopRecording();
                    // Handle Playback Stopped event
                    waveOut.PlaybackStopped += OnPlaybackStopped;


                    timer1.Start(); // timer to update remaining time
                    // new Wavout for agent                    

                    string deviceSpeaker = cmbBoxSpeaker.Text.Contains(":") ? cmbBoxSpeaker.Text.Substring(0, cmbBoxSpeaker.Text.Length - 3) : cmbBoxSpeaker.Text;
                    waveO = new WaveOutEvent
                    {
                        DeviceNumber = FindWaveOutDeviceNumber(deviceSpeaker), // Implement this to find the correct device index
                        DesiredLatency = 100 // Experiment with this value, starting as low as 100ms
                    };

                    audioFileReader2 = new AudioFileReader(info.voiceFilePath);
                    waveO.Init(audioFileReader2);
                    waveO.Play();
                    waveO.PlaybackStopped += OnPlaybackStopped2;

                    // Start processing audio for intensity visualization in a separate task
                    Task.Run(() => ProcessAudioAndVisualizeIntensity_(info.voiceFilePath, cancellationTokenSource.Token));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:" + ex.Message);

                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());

            }
        }

        private void ProcessAudioAndVisualizeIntensity(string filePath)
        {
            // Assuming 44100 Hz, 16 bits per sample, 1 channel (Mono)
            int sampleRate = 44100;
            int bytesPerSample = 2;
            int bufferSize = 4096; // Buffer size for reading chunks of the audio file

            using (var reader = new AudioFileReader(filePath))
            {
                var buffer = new float[bufferSize];
                int samplesRead;
                while ((samplesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    // Calculate RMS of the current buffer
                    double rms = CalculateRMS(buffer, samplesRead);

                    // Convert RMS to a suitable scale for your AudioIntensityMeter (e.g., 0-100)
                    int intensity = (int)(rms * 100); // Adjust scale as needed

                    // Update your AudioIntensityMeter control on the UI thread
                    if (audioIntensityMeterRecIn.InvokeRequired && audioIntensityMeterRecOut.InvokeRequired)
                    {
                        audioIntensityMeterRecIn.Invoke(new Action(() => {
                            float volumeAdjustmentRecIn = volumeA1RecIn.Value / 100f;
                            int recInAdjusted = (int)(intensity * volumeAdjustmentRecIn);
                            audioIntensityMeterRecIn.UpdateIntensity(recInAdjusted);

                        }));
                        audioIntensityMeterRecOut.Invoke(new Action(() => {
                            float volumeAdjustmentRecOut = volumeA1RecOut.Value / 100f;
                            int recOutAdjusted = (int)(intensity * volumeAdjustmentRecOut);
                            audioIntensityMeterRecOut.UpdateIntensity(recOutAdjusted);

                        }));
                    }
                    else
                    {
                        float volumeAdjustmentRecIn = volumeA1RecIn.Value / 100f;
                        int recInAdjusted = (int)(intensity * volumeAdjustmentRecIn);
                        audioIntensityMeterRecIn.UpdateIntensity(recInAdjusted);

                        float volumeAdjustmentRecOut = volumeA1RecOut.Value / 10f;
                        int recOutAdjusted = (int)(intensity * volumeAdjustmentRecOut);
                        audioIntensityMeterRecOut.UpdateIntensity(recOutAdjusted);
                    }

                    // Simulate real-time processing delay
                    int test = (int)(bufferSize / (double)sampleRate * 1000 / 2);
                    Thread.Sleep(75);
                }
            }
        }

        private long lastReadPosition = 0;
        public void ProcessAudioAndVisualizeIntensity_(string filePath, CancellationToken cancellationToken)
        {
            try
            {
                // Assuming 44100 Hz, 16 bits per sample, 1 channel (Mono)
                int sampleRate = 44100;
                int bytesPerSample = 2;
                int bufferSize = 4096; // Buffer size for reading chunks of the audio file

                using (var reader = new AudioFileReader(filePath))
                {
                    // Set the reader's position if we are resuming
                    if (lastReadPosition > 0 && lastReadPosition < reader.Length)
                    {
                        reader.Position = lastReadPosition;
                    }

                    var buffer = new float[bufferSize];
                    int samplesRead;
                    while ((samplesRead = reader.Read(buffer, 0, buffer.Length)) > 0 && !cancellationToken.IsCancellationRequested)
                    {
                        // Calculate RMS of the current buffer
                        double rms = CalculateRMS(buffer, samplesRead);

                        // Convert RMS to a suitable scale for your AudioIntensityMeter (e.g., 0-100)
                        int intensity = (int)(rms * 100); // Adjust scale as needed

                        // Update your AudioIntensityMeter control on the UI thread
                        if (audioIntensityMeterRecIn.InvokeRequired && audioIntensityMeterRecOut.InvokeRequired)
                        {
                            audioIntensityMeterRecIn.Invoke(new Action(() =>
                            {
                                float volumeAdjustmentRecIn = volumeA1RecIn.Value / 100f;
                                int recInAdjusted = (int)(intensity * volumeAdjustmentRecIn);
                                audioIntensityMeterRecIn.UpdateIntensity(recInAdjusted);

                            }));
                            audioIntensityMeterRecOut.Invoke(new Action(() =>
                            {
                                float volumeAdjustmentRecOut = volumeA1RecOut.Value / 100f;
                                int recOutAdjusted = (int)(intensity * volumeAdjustmentRecOut);
                                audioIntensityMeterRecOut.UpdateIntensity(recOutAdjusted);

                            }));
                        }
                        else
                        {
                            float volumeAdjustmentRecIn = volumeA1RecIn.Value / 100f;
                            int recInAdjusted = (int)(intensity * volumeAdjustmentRecIn);
                            audioIntensityMeterRecIn.UpdateIntensity(recInAdjusted);

                            float volumeAdjustmentRecOut = volumeA1RecOut.Value / 10f;
                            int recOutAdjusted = (int)(intensity * volumeAdjustmentRecOut);
                            audioIntensityMeterRecOut.UpdateIntensity(recOutAdjusted);
                        }

                        lastReadPosition = reader.Position;

                        // Simulate real-time processing delay
                        int test = (int)(bufferSize / (double)sampleRate * 1000 / 2);
                        Thread.Sleep(75);
                    }

                    if (reader.Position >= reader.Length)
                    {
                        // Reset position if we've reached the end of the file
                        lastReadPosition = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void RefreshIntensityMeter()
        {
            try { 
            if (audioIntensityMeterRecIn != null && audioIntensityMeterRecOut != null)
            {
                if (audioIntensityMeterRecIn.InvokeRequired && audioIntensityMeterRecOut.InvokeRequired)
                {
                    audioIntensityMeterRecIn.Invoke(new Action(() =>
                    {
                        audioIntensityMeterRecIn.UpdateIntensity(0);

                    }));
                    audioIntensityMeterRecOut.Invoke(new Action(() =>
                    {
                        audioIntensityMeterRecOut.UpdateIntensity(0);
                    }));
                }
                else
                {
                    audioIntensityMeterRecIn.UpdateIntensity(0);
                    audioIntensityMeterRecOut.UpdateIntensity(0);
                }
            }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private double CalculateRMS(float[] buffer, int samplesRead)
        {
            double sum = 0;
            for (int i = 0; i < samplesRead; i++)
            {
                sum += buffer[i] * buffer[i];
            }
            return Math.Sqrt(sum / samplesRead);
        }

        private void OnPlaybackStopped2(object sender, StoppedEventArgs e)
        {
            // Dispose resources
            try
            {
                if (audioFileReader2 != null)
                {
                    audioFileReader2.Dispose();
                }

                audioFileReader2 = null;

                if (waveO != null)
                    waveO.Dispose();

                RefreshIntensityMeter();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:" + ex.Message);

                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());

            }
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            // Dispose resources
            try
            {
                isAudioPlaying = false;
                if (audioFileReader != null)
                    audioFileReader.Dispose();
                audioFileReader = null;

                if (audioMacroInfo != null)
                    audioMacroInfo = null;

                lastReadPosition = 0;

                if (waveOut != null)
                    waveOut.Dispose();
                btnStop.Enabled = false;
                // Once playback is finished, start capturing from the agent's microphone again
                if (waveIn != null)
    waveIn.StartRecording();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:" + ex.Message);
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }

        }

        //private void btnExit_Click(object sender, EventArgs e)
        //{
        //    DataUtils.SaveMacros(manager, Path.Combine(Application.StartupPath, "Data", "MacrosInfo.txt"));
        //    this.Close();
        //}

        private void Form_Closing(object sender, FormClosingEventArgs e)
        {
            try
            {
                volumeValue["volumeA1Mic"] = volumeA1Mic.Value.ToString();
                volumeValue["volumeA1RecIn"] = volumeA1RecIn.Value.ToString();
                volumeValue["volumeA1RecOut"] = volumeA1RecOut.Value.ToString();
                volumeValue["volumeA1Speaker"] = volumeA1Speaker.Value.ToString();
                volumeValue["volumeA2Speaker"] = volumeA2Speaker.Value.ToString();
                volumeValue["cmbMicValue"] = comboBoxMicroPhone.SelectedItem.ToString();
                volumeValue["cmbSpeaker1Value"] = cmbBoxSpeaker.SelectedItem.ToString();
                volumeValue["cmbSpeaker2Value"] = cmbBoxSpeaker2.SelectedItem.ToString();

                DataUtils.SaveMacros(manager, Path.Combine(Application.StartupPath, "Data", "MacrosInfo.txt"));
                DataUtils.SaveScripts(scriptManager, Path.Combine(Application.StartupPath, "Data", "Scripts.txt"));
                DataUtils.SaveVolumeValue(volumeValue, Path.Combine(Application.StartupPath, "Data", "Volume.txt"));


                AudioService.Instance.AudioIntensityChanged -= Instance_AudioIntensityChanged;
                AudioService.Instance.AudioMicIntensityChanged -= Instance_AudioMicIntensityChanged;
                AudioService.Instance.AudioRecIntensityChanged -= Instance_AudioRecIntensityChanged;

                AudioService.Instance.Dispose(); // dispose of audio objects

                //if (waveIn != null)
                //{
                //    waveIn.StopRecording();
                //    waveIn.Dispose();
                //}

                //if (waveOut != null)

        if (waveOut != null)
            waveOut.Dispose();
        btnStop.Enabled = false;
        // Once playback is finished, start capturing from the agent's microphone again
        if (waveIn != null)
    waveIn.StartRecording();
    }
    catch (Exception ex)
    {
        MessageBox.Show("Error:" + ex.Message);
        logger.Error(ex, "An error occurred while performing an operation.");
        logger.Error("ExceptionMessage: " + ex.Message);
        logger.Error("Exception: " + ex.ToString());
    }

}

//private void btnExit_Click(object sender, EventArgs e)
//{
//    DataUtils.SaveMacros(manager, Path.Combine(Application.StartupPath, "Data", "MacrosInfo.txt"));
//    this.Close();
//}

        private void comboBoxMicroPhone_SelectedValueChanged(object sender, EventArgs e)
        {
            //virtualCableInputDeviceNumber = comboBoxMicroPhone.SelectedIndex;
            try
            {
                int selectedMic = 0;
                if (comboBoxMicroPhone.SelectedItem != null)
                {
                    string micrphoneName = comboBoxMicroPhone.SelectedItem.ToString();
                    if (!string.IsNullOrEmpty(micrphoneName))
                    {
                        string[] mic = micrphoneName.Split(':');
                        if (mic.Length > 1)
                        {
                            if (int.TryParse(mic[1], out selectedMic))
                            {
                                if (AudioService.Instance.waveIn != null)
                                {
                                    AudioService.Instance.waveIn.StopRecording();
                                    AudioService.Instance.waveIn.Dispose();
                                }

                                if (AudioService.Instance.waveOut != null)
                                {
                                    AudioService.Instance.waveOut.Stop();
                                    AudioService.Instance.waveOut.Dispose();
                                }

                                AudioService.Instance.virtualCableInputDeviceNumber = selectedMic;
                                AudioService.Instance.StartMicrophoneCaptureNew(micrphoneName); //StartMicrophoneCaptureNew();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error");

                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());

            }
        }

        private void btnScripts_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if the form instance exists and whether it has been disposed
                if (form == null || form.IsDisposed)
                {
                    form = new ScriptsForm(scriptManager, manager, this);
                    form.Show();
                }
                else
                {
                    // If the form is minimized, restore it
                    if (form.WindowState == FormWindowState.Minimized)
                    {
                        form.WindowState = FormWindowState.Normal;
                    }

                    // Bring the form to the front
                    form.BringToFront();
                    // Alternatively, you can use Activate() to make it the active window
                    form.Activate();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error");

                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());

            }
        }

        // Add context menu for right-click on form to access Virtual Audio setup
        private void MainForm_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ContextMenuStrip contextMenu = new ContextMenuStrip();
                
                ToolStripMenuItem setupVirtualAudio = new ToolStripMenuItem("Install Virtual Audio Cable");
                setupVirtualAudio.Click += async (s, args) => await SetupVirtualAudioManually();
                
                ToolStripMenuItem showSetupGuide = new ToolStripMenuItem("Show Virtual Audio Setup Guide");
                showSetupGuide.Click += (s, args) => ShowVirtualAudioSetupGuide();
                
                ToolStripMenuItem checkVirtualAudio = new ToolStripMenuItem("Check Virtual Audio Status");
                checkVirtualAudio.Click += (s, args) => CheckVirtualAudioStatus();

                ToolStripMenuItem downloadVBAudio = new ToolStripMenuItem("Download VB-Audio Cable");
                downloadVBAudio.Click += (s, args) => OpenVBAudioWebsite();

                contextMenu.Items.Add(setupVirtualAudio);
                contextMenu.Items.Add(showSetupGuide);
                contextMenu.Items.Add(checkVirtualAudio);
                contextMenu.Items.Add(new ToolStripSeparator());
                contextMenu.Items.Add(downloadVBAudio);
                
                contextMenu.Show(this, e.Location);
            }
        }

        private async Task SetupVirtualAudioManually()
        {
            try
            {
                logger.Info("Manual Virtual Audio setup requested");
                await InitializeVirtualAudioAsync();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in manual Virtual Audio setup: " + ex.Message);
                MessageBox.Show($"Error setting up Virtual Audio: {ex.Message}", "Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CheckVirtualAudioStatus()
        {
            try
            {
                bool isAvailable = VirtualAudioInstaller.IsVBAudioCableInstalled();
                string deviceName = VirtualAudioInstaller.GetVirtualAudioDeviceName();
                
                string message;
                MessageBoxIcon icon;
                
                if (isAvailable)
                {
                    message = $"✅ Virtual Audio Cable is installed and available!\n\n" +
                             $"Device: {deviceName}\n\n" +
                             "You can now use this application with video calls.";
                    icon = MessageBoxIcon.Information;
                }
                else
                {
                    message = "❌ Virtual Audio Cable is not available.\n\n" +
                             "Right-click and select 'Install Virtual Audio Cable' to install it automatically.";
                    icon = MessageBoxIcon.Warning;
                }
                
                MessageBox.Show(message, "Virtual Audio Status", MessageBoxButtons.OK, icon);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error checking Virtual Audio status: " + ex.Message);
                MessageBox.Show($"Error checking status: {ex.Message}", "Status Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenVBAudioWebsite()
        {
            try
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    FileName = "https://vb-audio.com/Cable/",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error opening VB-Audio website: " + ex.Message);
                MessageBox.Show("Please visit https://vb-audio.com/Cable/ to download VB-Audio Cable", 
                               "Download VB-Audio Cable", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        //#region Macros     

        //private void LoadMacrosControls()
        //{
        //    panel1.Controls.Clear();

        //    FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
        //    flowLayoutPanel.Dock = DockStyle.Fill;
        //    flowLayoutPanel.FlowDirection = FlowDirection.TopDown;
        //    flowLayoutPanel.AutoScroll = true;
        //    flowLayoutPanel.WrapContents = false;
        //    panel1.Controls.Add(flowLayoutPanel);

        //    flowLayoutPanel.Controls.Clear();

        //    // Loop according to number of Macros 
        //    FlowLayoutPanel currentRowPanel = null;
        //    for (int i = 0; i < manager.macroList.Count; i++)
        //    {
        //        if (i % 3 == 0)
        //        {
        //            currentRowPanel = CreateRowPanel(flowLayoutPanel);
        //            flowLayoutPanel.Controls.Add(currentRowPanel);
        //        }
        //        CreateMacroPanel(currentRowPanel, i);
        //    }
        //}

        //private void CreateMacroPanel(FlowLayoutPanel flowLayoutPanel, int macroIndex)
        //{
        //    var MacroPanel = new Panel();
        //    int panelWidth = flowLayoutPanel.Width / 3 - 15;
        //    MacroPanel.Size = new Size(panelWidth, 50); // Adjust the width here
        //    MacroPanel.Name = manager.macroList[macroIndex].Name;
        //    MacroPanel.Click += (sender, e) => MacroPanel_Click(manager.macroList[macroIndex]);
        //    MacroPanel.BackColor = Color.FromName(manager.macroList[macroIndex].ColorName);
        //    MacroPanel.MouseHover += (sender, e) => { MacroPanel.BackColor = ControlPaint.Light(Color.FromName(manager.macroList[macroIndex].ColorName)); };
        //    MacroPanel.MouseLeave += (sender, e) => { MacroPanel.BackColor = Color.FromName(manager.macroList[macroIndex].ColorName); };
        //    MacroPanel.MouseEnter += (sender, e) => { MacroPanel.BackColor = ControlPaint.Light(Color.FromName(manager.macroList[macroIndex].ColorName)); };


        //    var macroLabel = new Label();
        //    // macroLabel.Location = new Point(panelWidth, 15);
        //    macroLabel.Text = manager.macroList[macroIndex].Name;
        //    //macroLabel.TextAlign = ContentAlignment.MiddleCenter;

        //    macroLabel.Font = new Font("Montserrat", 10, FontStyle.Bold);
        //    macroLabel.Click += (sender, e) => MacroPanel_Click(manager.macroList[macroIndex]);
        //    macroLabel.MouseHover += (sender, e) => { MacroPanel.BackColor = ControlPaint.Light(Color.FromName(manager.macroList[macroIndex].ColorName)); };
        //    macroLabel.MouseLeave += (sender, e) => { MacroPanel.BackColor = Color.FromName(manager.macroList[macroIndex].ColorName); };
        //    macroLabel.MouseEnter += (sender, e) => { MacroPanel.BackColor = ControlPaint.Light(Color.FromName(manager.macroList[macroIndex].ColorName)); };
        //    macroLabel.Cursor = Cursors.Hand;
        //    macroLabel.Location = new Point(MacroPanel.Size.Width / 4, MacroPanel.Size.Height / 3);
        //    macroLabel.BackColor = System.Drawing.Color.Transparent;
        //    macroLabel.ForeColor = System.Drawing.Color.Black;
        //    int labelWidth = flowLayoutPanel.Width / 4;
        //    macroLabel.Size = new Size(panelWidth - 10, 30);

        //    MacroPanel.Controls.Add(macroLabel);

        //    flowLayoutPanel.Controls.Add(MacroPanel);
        //}

        //private FlowLayoutPanel CreateRowPanel(FlowLayoutPanel mainLayoutPanel)
        //{
        //    var rowPanel = new FlowLayoutPanel();
        //    rowPanel.Size = new Size(mainLayoutPanel.Width - 20, 60); // Adjust height as needed
        //    rowPanel.FlowDirection = FlowDirection.LeftToRight;
        //    //rowPanel.Dock = DockStyle.Right;
        //    rowPanel.WrapContents = true;
        //    rowPanel.AutoScroll = false;
        //    return rowPanel;
        //}

        //private void MacroPanel_Click(MacrosInfo info)
        //{
        //    string colorName = info.ColorName;
        //    macrForm = new MacroDetails(info, virtualCableInputDeviceNumber);
        //    macrForm.ShowDialog();

        //    if (info.ColorName != colorName)
        //        UpdatePanelColor(info);

        //   // LoadMacrosControls(); // Refresh form again
        //}

        //private void UpdatePanelColor(MacrosInfo info)
        //{
        //    Control[] foundControls = this.Controls.Find(info.Name, true);
        //    if (foundControls.Length > 0)
        //    {
        //        Control myControl = foundControls[0];

        //        // Cast the control to the Panel
        //        if (myControl is Panel)
        //        {
        //            Panel panel = myControl as Panel;
        //            panel.BackColor = Color.FromName(info.ColorName);
        //        }                
        //    }
            
        //}

        //#endregion

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (isAudioPlaying && audioFileReader != null)
            {
                TimeSpan timeRemaining = audioFileReader.TotalTime - audioFileReader.CurrentTime;
                lblTimeRemaining.Text = "Rec. Time: " + timeRemaining.ToString(@"mm\:ss");
            }
            else
            {
                timer1.Stop();
            }
        }

        private void btnScripts_MouseEnter(object sender, EventArgs e)
        {
            btnScripts.Height += 3;
            btnScripts.Width += 3;
        }

        private void btnScripts_MouseLeave(object sender, EventArgs e)
        {
            btnScripts.Height -= 3;
            btnScripts.Width -= 3;
        }

        //private void trackBar1_ValueChanged(object sender, EventArgs e)
        //{
        //    // Update currentVolume based on the TrackBar's value
        //    // currentVolume = trackBar1.Value / 100f; // Normalize to 0.0 - 1.0
        //    if (waveOut != null)
        //        waveOut.Volume = trackBarA1Mic.Value / 100f; // Normalize to 0.0 - 1.0
        //}

        //private void trackBarA2Speaker_ValueChanged(object sender, EventArgs e)
        //{
        //    if (waveOutSecond != null)
        //    {
        //        // Convert TrackBar value to a float volume level (0.0 to 1.0)
        //        float volume = trackBarA2Speaker.Value / 100f;
        //        waveOutSecond.Volume = volume;
        //    }
        //}

        //private void trackBarA1Speaker_ValueChanged(object sender, EventArgs e)
        //{
        //    if (mMDevice != null)
        //    {
        //        float volumeLevel = trackBarA1Speaker.Value / 100f; // Convert to a 0.0 to 1.0 range
        //        SetSpeakerVolume(mMDevice, volumeLevel);
        //    }
        //}

        private void SetSpeakerVolume(MMDevice device, float volumeLevel)
        {
            if (device == null) return;

            // Ensure volumeLevel is between 0.0 (0%) and 1.0 (100%)
            volumeLevel = Math.Max(0.0f, Math.Min(1.0f, volumeLevel));

            // Access the AudioEndpointVolume instance of the device
            var volumeControl = device.AudioEndpointVolume;

            // Set the master volume level
            volumeControl.MasterVolumeLevelScalar = volumeLevel;

            // Optionally, you can mute/unmute the device as needed
            // volumeControl.Mute = false; // or true to mute
        }

        //private void trackBarA1Rec_ValueChanged(object sender, EventArgs e)
        //{
        //    if (waveO != null)
        //    {
        //        // Convert TrackBar value to a float volume level (0.0 to 1.0)
        //        float volume = trackBarA1Rec.Value / 100f;
        //        waveO.Volume = volume;
        //    }
        //}

        //private void trackBarCustRec_ValueChanged(object sender, EventArgs e)
        //{
        //    if (waveOut != null)
        //    {
        //        // Convert TrackBar value to a float volume level (0.0 to 1.0)
        //        float volume = trackBarCustRec.Value / 100f;
        //        waveOut.Volume = volume;
        //    }
        //}

        private void cmbBoxSpeaker_SelectedValueChanged(object sender, EventArgs e)
        {
            try { 
            string deviceSpeaker = cmbBoxSpeaker.Text.Contains(":") ? cmbBoxSpeaker.Text.Substring(0, cmbBoxSpeaker.Text.Length - 3) : cmbBoxSpeaker.Text;
            AudioService.Instance.outputDeviceNumber = FindWaveOutDeviceNumber(deviceSpeaker);
            AudioService.Instance.StartLoopbackCapture(cmbBoxSpeaker.Text, cmbBoxSpeaker2.Text);//StartLoopbackCapture();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void cmbBoxSpeaker2_SelectedValueChanged(object sender, EventArgs e)
        {
            try
            {
                AudioService.Instance.StartLoopbackCapture(cmbBoxSpeaker.Text, cmbBoxSpeaker2.Text);//StartLoopbackCapture();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void volumeA1Mic_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                // C1 Main Level (Your Voice) - Control microphone input volume
                float micVolume = volumeA1Mic.Value / 100f; // Normalize to 0.0 - 1.0
                
                // Apply microphone volume to WaveIn (your voice input)
                if (AudioService.Instance.waveIn != null)
                {
                    // Set the microphone input volume
                    AudioService.Instance.SetMicrophoneVolume(micVolume);
                }
                
                // If volume is 0, mute the microphone completely
                if (volumeA1Mic.Value == 0)
                {
                    AudioService.Instance.MuteMicrophone(true);
                }
                else
                {
                    AudioService.Instance.MuteMicrophone(false);
                }
                
                logger.Debug($"Set microphone volume to: {micVolume:F2}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void volumeA1RecIn_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                // C1 Recording Level - Controls script volume on customer/receiving side
                float volume = volumeA1RecIn.Value / 100f;
                logger.Debug($"A1 RecIn (Customer Side) volume changed to: {volume:F2} (trackbar: {volumeA1RecIn.Value})");
                
                // This controls the customer side volume for script playback (waveOut in ScriptsForm)
                // waveO is for agent side, waveOut is for customer side
                if (AudioService.Instance.waveO != null)
                {
                    AudioService.Instance.waveO.Volume = volume;
                }
                
                // Update ScriptsForm volume for customer side (waveOut)
                UpdateScriptsFormVolume();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void volumeA2Speaker_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (AudioService.Instance.waveOutSecond != null)
                {
                    // Convert TrackBar value to a float volume level (0.0 to 1.0)
                    float volume = volumeA2Speaker.Value / 100f;
                    AudioService.Instance.waveOutSecond.Volume = volume;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void volumeA1Speaker_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                // A1 Volume (Incoming) - Controls volume of incoming audio from other person
                float volumeLevel = volumeA1Speaker.Value / 100f; // Convert to a 0.0 to 1.0 range
                
                // Set the system speaker volume for incoming audio
                if (AudioService.Instance.mMDevice != null)
                {
                    SetSpeakerVolume(AudioService.Instance.mMDevice, volumeLevel);
                }
                
                // Also control the loopback capture volume (incoming audio processing)
                AudioService.Instance.SetIncomingAudioVolume(volumeLevel);
                
                logger.Debug($"Set incoming audio volume to: {volumeLevel:F2}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void volumeA1RecOut_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                // A1 Volume (Recordings level in Agent1 Headset) - Controls script volume on agent side only
                float volume = volumeA1RecOut.Value / 100f;
                logger.Debug($"A1 RecOut (Agent Headset) volume changed to: {volume:F2} (trackbar: {volumeA1RecOut.Value})");
                
                // This controls the agent headset volume for script playback (waveO in ScriptsForm)
                // The regular waveOut is for live microphone, not script playback
                if (AudioService.Instance.waveOut != null)
                {
                    AudioService.Instance.waveOut.Volume = volume;
                }
                
                // Update ScriptsForm volume for agent headset (waveO)
                UpdateScriptsFormVolume();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        /// <summary>
        /// Update ScriptsForm volume when MainForm volume controls change
        /// </summary>
        private void UpdateScriptsFormVolume()
        {
            try
            {
                logger.Debug("UpdateScriptsFormVolume called");
                if (form != null && !form.IsDisposed)
                {
                    logger.Debug("Calling form.UpdateVolumesFromMainForm()");
                    form.UpdateVolumesFromMainForm();
                }
                else
                {
                    logger.Debug("ScriptsForm is null or disposed");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error updating ScriptsForm volume: " + ex.Message);
            }
        }

        private void btnPause_Click(object sender, EventArgs e)
        {
            StopAudioRecordings();
        }

        private void StopAudioRecordings()
        {
            try
            {
                if (isAudioPlaying)
                {
                    btnStop.Enabled = false;

                    if (waveOut != null && waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        waveOut.Stop(); // Pause playback to resume later

                        if (waveO != null && waveO.PlaybackState == PlaybackState.Playing)
                        {
                            waveO.Stop();
                        }
                    }

                    // Update UI accordingly
                    isAudioPlaying = false;
                    cancellationTokenSource?.Cancel();
                    timer1.Stop();

                    //if (audioFileReader != null)
                    //    audioFileReader.Dispose();
                    //audioFileReader = null;

                    //if (waveOut != null)
                    //    waveOut.Dispose();
                    //btnStop.Enabled = false;

                    //if (audioFileReader2 != null)
                    //{
                    //    audioFileReader2.Dispose();
                    //}

                    //audioFileReader2 = null;

                    //if (waveO != null)
                    //    waveO.Dispose();


                    //// Once playback is stopped, start capturing from the agent's microphone again
                    //if (waveIn != null)
    waveIn.StartRecording();

                    lblTimeRemaining.Text = "Rec. Time: 00:00";
                }
                //else
                //{
                //    btnStop.Enabled = true;
                //    btnStop.Text = "Pause";

                //    if (waveOut != null && waveOut.PlaybackState == PlaybackState.Paused)
                //    {
                //        waveOut.Play();
                //        // Similarly, handle waveO if needed
                //        if (waveO != null && waveO.PlaybackState == PlaybackState.Paused)
                //        {
                //            waveO.Play();
                //        }

                //        timer1.Start();

                //        cancellationTokenSource?.Cancel();
                //        cancellationTokenSource = new CancellationTokenSource();

                //        if (audioMacroInfo != null)
                //        {
                //            // Start processing audio for intensity visualization in a separate task
                //            Task.Run(() => ProcessAudioAndVisualizeIntensity_(audioMacroInfo.voiceFilePath, cancellationTokenSource.Token));
                //        }
                //        // Update UI accordingly
                //        isAudioPlaying = true;
                //    }
                //}
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void button1_MouseEnter(object sender, EventArgs e)
        {
            button1.Height += 3;
            button1.Width += 3;
        }

        private void button1_MouseLeave(object sender, EventArgs e)
        {
            button1.Height -= 3;
            button1.Width -= 3;
        }

        private Panel topPanel;
        private Panel bottomPanel;
        private Panel centerPanel;
        private Panel rightTextPanel;
        private Panel bottomCenterPanel; // New panel for "One United Global" text bottomPanel;
        private Panel leftColumn;
        private Panel middleColumn;
        private Panel rightColumn;
        private Panel leftVolumePanel;
        private Panel rightVolumePanel;

        private void SetupResponsiveLayout()
        {
            try
            {
                // Set form properties for responsive behavior
                this.MinimumSize = new Size(900, 700); // Adjusted to client requirements
                this.FormBorderStyle = FormBorderStyle.Sizable;
                
                // Subscribe to resize event for dynamic layout adjustments
                this.Resize += MainForm_Resize;
                
                // Subscribe to location changed to detect when form moves to a different monitor
                this.LocationChanged += MainForm_LocationChanged;
                
                // Create and setup panel structure
                CreatePanelStructure();
                
                // Organize controls into panels
                OrganizeControlsIntoPanels();
                
                // Setup anchoring for panels
                SetupPanelAnchoring();
                
                // Initial layout adjustment
                AdjustLayoutForCurrentSize();
                
                logger.Info("Panel-based responsive layout setup completed");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting up responsive layout: " + ex.Message);
            }
        }

        private void CreatePanelStructure()
        {
            // Create main horizontal panels
            topPanel = new Panel
            {
                Name = "topPanel",
                BackColor = Color.Transparent,
                Dock = DockStyle.Top,
                Height = 400 // Will be adjusted dynamically
            };

            bottomPanel = new Panel
            {
                Name = "bottomPanel",
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill
            };

            // Create vertical columns for top panel
            leftColumn = new Panel
            {
                Name = "leftColumn",
                BackColor = Color.Transparent,
                Dock = DockStyle.Left,
                Width = 280
            };

            rightColumn = new Panel
            {
                Name = "rightColumn",
                BackColor = Color.Transparent,
                Dock = DockStyle.Right,
                Width = 250
            };

            middleColumn = new Panel
            {
                Name = "middleColumn",
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill
            };

            // Create vertical columns for bottom panel
            leftVolumePanel = new Panel
            {
                Name = "leftVolumePanel",
                BackColor = Color.Transparent,
                Dock = DockStyle.Left,
                Width = 450
            };

            rightVolumePanel = new Panel
            {
                Name = "rightVolumePanel",
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill,
                //Dock = DockStyle.Right,
                Width = 450
            };

            // Create bottom center panel for "One United Global" text
            bottomCenterPanel = new Panel
            {
                Name = "bottomCenterPanel",
                BackColor = Color.Transparent,
                Dock = DockStyle.Bottom,
                Height = 60 // Height for the centered text
            };

            // Add panels to form
            this.Controls.Add(bottomCenterPanel); // Add bottom center panel first (bottom-most)
            this.Controls.Add(bottomPanel);
            this.Controls.Add(topPanel);

            // Add columns to top panel
            topPanel.Controls.Add(middleColumn);
            topPanel.Controls.Add(rightColumn);
            topPanel.Controls.Add(leftColumn);

            // Add volume panels to bottom panel
            bottomPanel.Controls.Add(rightVolumePanel);
            bottomPanel.Controls.Add(leftVolumePanel);
        }

        private void OrganizeControlsIntoPanels()
        {
            // Move left side controls to left column
            leftColumn.Controls.Add(comboBoxMicroPhone);
            leftColumn.Controls.Add(cmbBoxSpeaker);
            leftColumn.Controls.Add(cmbBoxSpeaker2);
            leftColumn.Controls.Add(label1);
            leftColumn.Controls.Add(label3);
            leftColumn.Controls.Add(label4);

            // Move center content to middle column
            middleColumn.Controls.Add(pictureBox2); // Logo
            middleColumn.Controls.Add(pictureBox1); // Video

            // Move right side controls to right column
            rightColumn.Controls.Add(button1);
            rightColumn.Controls.Add(btnScripts);
            rightColumn.Controls.Add(label5);
            rightColumn.Controls.Add(label10);
            rightColumn.Controls.Add(label11);
            rightColumn.Controls.Add(label12);

            // Move left volume controls to left volume panel
            leftVolumePanel.Controls.Add(volumeA1Speaker);
            leftVolumePanel.Controls.Add(volumeA1RecIn);
            leftVolumePanel.Controls.Add(volumeA2Speaker);
            leftVolumePanel.Controls.Add(audioIntensityMeterSpeaker11);
            leftVolumePanel.Controls.Add(audioIntensityMeterRecIn);
            leftVolumePanel.Controls.Add(audioIntensityMeterSpeaker22);
            leftVolumePanel.Controls.Add(label2);
            leftVolumePanel.Controls.Add(label6);
            leftVolumePanel.Controls.Add(label7);

            // Move right volume controls to right volume panel
            rightVolumePanel.Controls.Add(volumeA1Mic);
            rightVolumePanel.Controls.Add(volumeA1RecOut);
            rightVolumePanel.Controls.Add(audioIntensityMeterMic1);
            rightVolumePanel.Controls.Add(audioIntensityMeterRecOut);
            rightVolumePanel.Controls.Add(label8);
            rightVolumePanel.Controls.Add(label9);

            // Move "One United Global" to bottom center panel
            bottomCenterPanel.Controls.Add(label15);
            
            // Keep other labels in right volume panel
            rightVolumePanel.Controls.Add(label13);
            rightVolumePanel.Controls.Add(label14);
            rightVolumePanel.Controls.Add(lblTimeRemaining);
            rightVolumePanel.Controls.Add(btnStop);
        }

        private void SetupPanelAnchoring()
        {
            // Left column controls
            comboBoxMicroPhone.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cmbBoxSpeaker.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cmbBoxSpeaker2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label1.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            label3.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            label4.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            // Middle column controls
            pictureBox2.Anchor = AnchorStyles.Top;
            pictureBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            // Right column controls
            button1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnScripts.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label5.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            label10.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            label11.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            label12.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            // Volume controls
            volumeA1Speaker.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            volumeA1RecIn.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            volumeA2Speaker.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            volumeA1Mic.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            volumeA1RecOut.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            audioIntensityMeterSpeaker11.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            audioIntensityMeterRecIn.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            audioIntensityMeterSpeaker22.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            audioIntensityMeterMic1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            audioIntensityMeterRecOut.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            // Bottom center panel label
            label15.Anchor = AnchorStyles.None; // Center in bottom panel

            // Right panel labels
            //label13.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            label13.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            //label14.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            label14.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblTimeRemaining.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnStop.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            try
            {
                AdjustLayoutForCurrentSize();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in MainForm_Resize: " + ex.Message);
            }
        }

        private Screen _lastScreen = null;

        private void MainForm_LocationChanged(object sender, EventArgs e)
        {
            try
            {
                Screen currentScreen = Screen.FromControl(this);
                if (_lastScreen == null || !currentScreen.DeviceName.Equals(_lastScreen.DeviceName))
                {
                    _lastScreen = currentScreen;
                    FitFormToScreen(currentScreen);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in MainForm_LocationChanged: " + ex.Message);
            }
        }

        private void FitFormToScreen(Screen screen)
        {
            try
            {
                Rectangle workArea = screen.WorkingArea;

                // If the form is larger than the screen's working area, shrink it to fit
                int newWidth = Math.Min(this.Width, workArea.Width - 20);
                int newHeight = Math.Min(this.Height, workArea.Height - 20);

                // Respect minimum size
                newWidth = Math.Max(newWidth, this.MinimumSize.Width);
                newHeight = Math.Max(newHeight, this.MinimumSize.Height);

                // Only resize if the form actually needs to shrink
                if (newWidth < this.Width || newHeight < this.Height)
                {
                    this.Size = new Size(newWidth, newHeight);
                }

                // Make sure the form is fully visible on the screen
                int newX = Math.Max(workArea.Left, Math.Min(this.Left, workArea.Right - this.Width));
                int newY = Math.Max(workArea.Top, Math.Min(this.Top, workArea.Bottom - this.Height));
                this.Location = new Point(newX, newY);

                logger.Info($"Form fitted to screen: {screen.DeviceName} ({workArea.Width}x{workArea.Height}), Form size: {this.Width}x{this.Height}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error fitting form to screen: " + ex.Message);
            }
        }

        private void AdjustLayoutForCurrentSize()
        {
            try
            {
                int formWidth = this.ClientSize.Width;
                int formHeight = this.ClientSize.Height;
                
                // Adjust panel sizes based on form size
                AdjustPanelSizes(formWidth, formHeight);
                
                // Adjust control positions within panels
                AdjustControlPositions();
                
                // Adjust font sizes based on form size for better readability
                AdjustFontSizes(formWidth, formHeight);
                
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error adjusting layout: " + ex.Message);
            }
        }

        private void AdjustPanelSizes(int formWidth, int formHeight)
        {
            // Adjust top panel height (60% of form height, minimum 350px)
            int topPanelHeight = Math.Max(350, (int)(formHeight * 0.6));
            topPanel.Height = topPanelHeight;

            // Adjust column widths based on form width
            int leftColumnWidth = Math.Max(250, (int)(formWidth * 0.25));
            int rightColumnWidth = Math.Max(220, (int)(formWidth * 0.22));
            
            leftColumn.Width = leftColumnWidth;
            rightColumn.Width = rightColumnWidth;

            // Adjust volume panel widths
            int leftVolumePanelWidth = Math.Max(350, (int)(formWidth * 0.5));
            leftVolumePanel.Width = leftVolumePanelWidth;
        }

        private void AdjustControlPositions()
        {
            // Middle column - center logo and video first to get video position
            int videoStartY = 0;
            if (middleColumn.Width > 0)
            {
                pictureBox2.Location = new Point((middleColumn.Width - pictureBox2.Width) / 2, 10);
                
                int videoWidth = Math.Max(300, middleColumn.Width - 40);
                int videoHeight = Math.Max(200, middleColumn.Height - pictureBox2.Height - 40);
                pictureBox1.Size = new Size(videoWidth, videoHeight);
                videoStartY = pictureBox2.Bottom + 10;
                pictureBox1.Location = new Point((middleColumn.Width - videoWidth) / 2, videoStartY);
            }

            // Left column control positions - align with video box
            int leftMargin = 10;
            int verticalSpacing = 60; // Increased spacing

            // Align first control with video box top
            label1.Location = new Point(leftMargin, videoStartY);
            comboBoxMicroPhone.Location = new Point(leftMargin, videoStartY + 25);
            
            label3.Location = new Point(leftMargin, videoStartY + verticalSpacing);
            cmbBoxSpeaker.Location = new Point(leftMargin, videoStartY + verticalSpacing + 25);
            
            label4.Location = new Point(leftMargin, videoStartY + (verticalSpacing * 2));
            cmbBoxSpeaker2.Location = new Point(leftMargin, videoStartY + (verticalSpacing * 2) + 25);

            // Right column control positions - align Scripts button with video box
            int rightMargin = 10;
            btnScripts.Location = new Point(rightMargin, videoStartY); // Align with video top
            button1.Location = new Point(rightMargin, videoStartY - 60); // Keep Macros button above if needed
            
            // Key info labels positioned below Scripts button
            label12.Location = new Point(rightMargin, videoStartY + 70);
            label5.Location = new Point(rightMargin, videoStartY + 100);
            label10.Location = new Point(rightMargin, videoStartY + 125);
            label11.Location = new Point(rightMargin, videoStartY + 150);

            // Volume controls positioning
            AdjustVolumeControlPositions();
        }

        private void AdjustVolumeControlPositions()
        {
            int margin = 15; // Increased margin
            int verticalSpacing = 80; // Increased vertical spacing between controls
            int currentY = 20; // More top margin
            
            // Calculate equal control widths for both panels
            int leftPanelControlWidth = Math.Max(300, leftVolumePanel.Width - (margin * 2));
            int rightPanelControlWidth = Math.Max(300, rightVolumePanel.Width - (margin * 2));

            // Left volume panel
            label2.Location = new Point(margin, currentY);
            audioIntensityMeterSpeaker11.Location = new Point(margin, currentY + 25);
            audioIntensityMeterSpeaker11.Size = new Size(leftPanelControlWidth, 8);
            volumeA1Speaker.Location = new Point(margin, currentY + 35);
            volumeA1Speaker.Size = new Size(leftPanelControlWidth, 19);
            
            currentY += verticalSpacing;
            label6.Location = new Point(margin, currentY);
            audioIntensityMeterRecIn.Location = new Point(margin, currentY + 25);
            audioIntensityMeterRecIn.Size = new Size(leftPanelControlWidth, 8);
            volumeA1RecIn.Location = new Point(margin, currentY + 35);
            volumeA1RecIn.Size = new Size(leftPanelControlWidth, 19);
            
            if (volumeA2Speaker.Visible)
            {
                currentY += verticalSpacing;
                label7.Location = new Point(margin, currentY);
                audioIntensityMeterSpeaker22.Location = new Point(margin, currentY + 25);
                audioIntensityMeterSpeaker22.Size = new Size(leftPanelControlWidth, 8);
                volumeA2Speaker.Location = new Point(margin, currentY + 35);
                volumeA2Speaker.Size = new Size(leftPanelControlWidth, 19);
            }

            // Right volume panel - reset Y position
            currentY = 20;
            label8.Location = new Point(margin, currentY);
            audioIntensityMeterMic1.Location = new Point(margin, currentY + 25);
            audioIntensityMeterMic1.Size = new Size(rightPanelControlWidth, 8);
            volumeA1Mic.Location = new Point(margin, currentY + 35);
            volumeA1Mic.Size = new Size(rightPanelControlWidth, 19);
            
            currentY += verticalSpacing;
            label9.Location = new Point(margin, currentY);
            audioIntensityMeterRecOut.Location = new Point(margin, currentY + 25);
            audioIntensityMeterRecOut.Size = new Size(rightPanelControlWidth, 8);
            volumeA1RecOut.Location = new Point(margin, currentY + 35);
            volumeA1RecOut.Size = new Size(rightPanelControlWidth, 19);

            // Position "One United Global" in bottom center panel
            if (bottomCenterPanel != null && bottomCenterPanel.Height > 0)
            {
                // Set font and center the label in the bottom center panel
                label15.Font = new Font(label15.Font.FontFamily, 16F, FontStyle.Bold);
                label15.AutoSize = true;
                
                // Center the label in the bottom center panel
                int centerX = (bottomCenterPanel.Width - label15.Width) / 2;
                int centerY = (bottomCenterPanel.Height - label15.Height) / 2;
                label15.Location = new Point(centerX, centerY);
            }
            
            // Position right-side labels in right volume panel
            if (rightVolumePanel.Width > 0 && rightVolumePanel.Height > 0)
            {
                // Increase font sizes for right-side labels
                label13.Font = new Font(label13.Font.FontFamily, 14F, FontStyle.Bold);
                label14.Font = new Font(label14.Font.FontFamily, 12F, FontStyle.Bold);
                
                // Position right-side labels below the volume controls
                int rightMargin = 20;
                int belowVolumeY = 160; // Position below the volume controls (after A1 Mic and C1 Recording Level)
                
                label13.Location = new Point(rightVolumePanel.Width - label13.Width - rightMargin, belowVolumeY);
                label14.Location = new Point(rightVolumePanel.Width - label14.Width - rightMargin, belowVolumeY + 25);
                
                // Keep these at bottom as they have Bottom anchoring
                int bottomY = rightVolumePanel.Height - 40;
                lblTimeRemaining.Location = new Point(rightVolumePanel.Width - lblTimeRemaining.Width - rightMargin, bottomY + 25);
                btnStop.Location = new Point(rightVolumePanel.Width - btnStop.Width - rightMargin, bottomY + 50);
            }
        }

        private void AdjustFontSizes(int formWidth, int formHeight)
        {
            try
            {
                // Calculate scale factor based on form size (adjusted for 900x700 minimum)
                float scaleFactor = Math.Min(formWidth / 900f, formHeight / 700f);
                scaleFactor = Math.Max(0.8f, Math.Min(1.3f, scaleFactor)); // Clamp between 0.8 and 1.3
                
                // Adjust label font sizes
                float baseFontSize = 10f * scaleFactor;
                float titleFontSize = 14f * scaleFactor;
                float smallFontSize = 8f * scaleFactor;
                
                Font titleFont = new Font("Microsoft Sans Serif", titleFontSize, FontStyle.Bold);
                Font normalFont = new Font("Microsoft Sans Serif", baseFontSize, FontStyle.Bold);
                Font smallFont = new Font("Microsoft Sans Serif", smallFontSize, FontStyle.Bold);
                
                // Apply fonts to prevent text from being too large on small screens
                label2.Font = normalFont;
                label6.Font = normalFont;
                label7.Font = normalFont;
                label8.Font = normalFont;
                label9.Font = normalFont;
                
                label1.Font = normalFont;
                label3.Font = normalFont;
                label4.Font = normalFont;
                
                label5.Font = smallFont;
                label10.Font = smallFont;
                label11.Font = smallFont;
                label12.Font = titleFont;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error adjusting font sizes: " + ex.Message);
            }
        }
    }
}
