using NAudio.Wave;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WindowsFormsApp1.src;

namespace WindowsFormsApp1
{
    public partial class ScriptRecordingForm : Form
    {
        private WaveIn waveIn;
        private WaveFileWriter waveFileWriter;
        private string outputFile;
        private Stopwatch stopwatch;
        private int inputDeviceNumber;
        private Scripts currentScript;
        private bool isEditMode = false;

        public Scripts Script { get; private set; }
        public bool IsAddMode { get; private set; }

        private string SanitizeFileName(string fileName)
        {
            // Remove invalid file name characters
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return sanitized.Trim();
        }

        public ScriptRecordingForm(int deviceNumber, Scripts script = null)
        {
            InitializeComponent();
            inputDeviceNumber = deviceNumber;
            // Initialize with the provided script, or a new one to ensure we have an ID immediately
            currentScript = script ?? new Scripts(); 
            isEditMode = script != null;
            
            stopwatch = new Stopwatch();
            
            SetupUI();
            PopulateControls();
        }

        private void SetupUI()
        {
            // Set button images if they exist
            try
            {
                string appPath = Application.StartupPath;
                string micIconPath = Path.Combine(appPath, "res", "mic_icon_resize.png");
                string stopIconPath = Path.Combine(appPath, "res", "stop_rec.png");
                string recordingGifPath = Path.Combine(appPath, "res", "icon-recording.gif");

                if (File.Exists(micIconPath))
                {
                    btnRecord.BackgroundImage = Image.FromFile(micIconPath);
                    btnRecord.BackgroundImageLayout = ImageLayout.Center;
                }

                if (File.Exists(stopIconPath))
                {
                    btnStopRecord.BackgroundImage = Image.FromFile(stopIconPath);
                    btnStopRecord.BackgroundImageLayout = ImageLayout.Center;
                }

                if (File.Exists(recordingGifPath))
                {
                    picRecording.ImageLocation = recordingGifPath;
                    picRecording.SizeMode = PictureBoxSizeMode.Zoom;
                }
            }
            catch
            {
                // If images don't exist, use text buttons
            }

            btnStopRecord.Enabled = false;
            picRecording.Visible = false;
            lblTimer.Text = "";
        }

        private void PopulateControls()
        {
            if (isEditMode)
            {
                txtScriptName.Text = currentScript.Name;
                txtAudioPath.Text = currentScript.AudioFilePath;
                btnAdd.Text = "Update";
            }
            else
            {
                txtScriptName.Text = "";
                txtAudioPath.Text = "";
                btnAdd.Text = "Add";
            }

            txtAudioPath.ReadOnly = true;
        }

        private void btnRecord_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtScriptName.Text))
            {
                MessageBox.Show("Please enter a script name before recording.", "Script Name Required", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            StartRecording();
        }

        private void StartRecording()
        {
            btnRecord.Enabled = false;
            btnStopRecord.Enabled = true;
            btnUpload.Enabled = false;

            try
            {
                waveIn = new WaveIn();
                waveIn.WaveFormat = new WaveFormat(44100, 1);
                waveIn.DeviceNumber = inputDeviceNumber;
                waveIn.DataAvailable += WaveIn_DataAvailable;
                waveIn.RecordingStopped += WaveIn_RecordingStopped;

                string audioDir = DataUtils.AudioDataPath;
                if (!Directory.Exists(audioDir))
                {
                    Directory.CreateDirectory(audioDir);
                }

                // Use Script ID instead of script name for the filename to allow duplicate names
                string fileName = $"{currentScript.Id}_recorded.wav";
                
                outputFile = Path.Combine(audioDir, fileName);
                
                if (File.Exists(outputFile))
                    File.Delete(outputFile);

                waveFileWriter = new WaveFileWriter(outputFile, waveIn.WaveFormat);

                stopwatch.Start();
                picRecording.Visible = true;
                timer1.Start();
                
                waveIn.StartRecording();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting recording: {ex.Message}", "Recording Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                ResetRecordingState();
            }
        }

        private void btnStopRecord_Click(object sender, EventArgs e)
        {
            StopRecording();
        }

        private void StopRecording()
        {
            try
            {
                if (waveIn != null)
                {
                    waveIn.StopRecording();
                }

                stopwatch.Stop();
                stopwatch.Reset();
                timer1.Stop();
                
                ResetRecordingState();
                
                if (File.Exists(outputFile))
                {
                    // Normalize the recording to -18 dBFS so it matches the agent's live voice level
                    AudioNormalizer.NormalizeFile(outputFile);
                    txtAudioPath.Text = outputFile;
                    MessageBox.Show("Recording completed successfully!", "Recording Complete", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping recording: {ex.Message}", "Recording Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetRecordingState()
        {
            btnRecord.Enabled = true;
            btnStopRecord.Enabled = false;
            btnUpload.Enabled = true;
            picRecording.Visible = false;
            lblTimer.Text = "";
        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (waveFileWriter != null)
            {
                waveFileWriter.Write(e.Buffer, 0, e.BytesRecorded);
                waveFileWriter.Flush();
            }
        }

        private void WaveIn_RecordingStopped(object sender, StoppedEventArgs e)
        {
            if (waveIn != null)
            {
                waveIn.Dispose();
                waveIn = null;
            }

            if (waveFileWriter != null)
            {
                waveFileWriter.Dispose();
                waveFileWriter = null;
            }
        }

        private void btnUpload_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtScriptName.Text))
            {
                MessageBox.Show("Please enter a script name before uploading audio.", "Script Name Required", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "WAV files (*.wav)|*.wav|MP3 files (*.mp3)|*.mp3|All Audio Files|*.wav;*.mp3";
            openFileDialog.Title = "Select an Audio File";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string sourcePath = openFileDialog.FileName;
                    string audioDir = DataUtils.AudioDataPath;
                    
                    if (!Directory.Exists(audioDir))
                    {
                        Directory.CreateDirectory(audioDir);
                    }

                    // Use Script ID instead of script name for the filename
                    string extension = Path.GetExtension(sourcePath);
                    string destFileName = $"{currentScript.Id}{extension}";
                    string destPath = Path.Combine(audioDir, destFileName);
                    
                    File.Copy(sourcePath, destPath, true);
                    // Normalize uploaded file to -18 dBFS to match live voice level
                    AudioNormalizer.NormalizeFile(destPath);
                    txtAudioPath.Text = destPath;
                    
                    MessageBox.Show("Audio file uploaded successfully!", "Upload Complete", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error uploading file: {ex.Message}", "Upload Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtScriptName.Text))
            {
                MessageBox.Show("Please enter a script name.", "Script Name Required", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Update script properties from UI
            currentScript.Name = txtScriptName.Text.Trim();
            if (!string.IsNullOrEmpty(txtAudioPath.Text))
            {
                currentScript.AudioFilePath = txtAudioPath.Text;
                currentScript.HasAudio = true;
            }
            Script = currentScript;

            IsAddMode = !isEditMode;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (stopwatch.IsRunning)
            {
                lblTimer.Text = stopwatch.Elapsed.ToString(@"mm\:ss");
            }
        }

        private void ScriptRecordingForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                if (waveIn != null)
                {
                    waveIn.StopRecording();
                    waveIn.Dispose();
                    waveIn = null;
                }

                if (waveFileWriter != null)
                {
                    waveFileWriter.Dispose();
                    waveFileWriter = null;
                }

                timer1.Stop();
                stopwatch.Stop();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
