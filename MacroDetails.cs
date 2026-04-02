using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1.src;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace WindowsFormsApp1
{
    public partial class MacroDetails : Form
    {
        private WaveIn waveIn;
        private WaveFileWriter waveFileWriter;
        private string outputFile; // Name of the recorded file
        MacrosInfo macro;
        Stopwatch stopwatch;

        System.Windows.Forms.ToolTip toolTip = new System.Windows.Forms.ToolTip();

        int inputDeviceNumber;
        public MacroDetails(MacrosInfo macroInfo, int device)
        {
            InitializeComponent();
            inputDeviceNumber = device;
            button2.BackgroundImage = Image.FromFile(Path.Combine(Application.StartupPath, "res", "mic_icon_resize.png"));
            button2.BackgroundImageLayout = ImageLayout.Center;

            button3.BackgroundImage = Image.FromFile(Path.Combine(Application.StartupPath, "res", "stop_rec.png"));
            button3.BackgroundImageLayout = ImageLayout.Center;
            button3.Enabled = false;

            pictureBox1.ImageLocation = Path.Combine(Application.StartupPath, "res", "icon-recording.gif");
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.Enabled = false;
            pictureBox1.Visible = false;

            SetToolTip();

            lblStopWatch.Text = "";
            stopwatch = new Stopwatch();
            macro = macroInfo;  
            PopulateComboBox();
            PopulateControls();
        }

        private void SetToolTip()
        {
            toolTip.AutoPopDelay = 3000;  // The time period for which the tooltip is displayed
            toolTip.InitialDelay = 800;  // The delay before the tooltip is shown
            toolTip.ReshowDelay = 1000;    // The delay before showing the tooltip again

            toolTip.SetToolTip(button2, "Start Recording");
            toolTip.SetToolTip(button3, "Stop Recording");
        }

        private void PopulateControls()
        {
            lblMacroName.Text = macro.Name;
            outputFile = macro.Name;
            comboBox1.SelectedItem = Color.FromName(macro.ColorName);
            txtAudioFilename.Text = macro.voiceFilePath;
            txtAudioFilename.ReadOnly = true;
           
        }

        private void PopulateComboBox()
        {
            comboBox1.Items.Clear();

            comboBox1.Items.Add(Color.CornflowerBlue);
            comboBox1.Items.Add(Color.SkyBlue);
            comboBox1.Items.Add(Color.Green);
            comboBox1.Items.Add(Color.Red);
            comboBox1.Items.Add(Color.Purple);
            

            if (macro.ColorName != string.Empty)
                comboBox1.SelectedValue = macro.ColorName;


            //// List available devices
            //for (int n = 0; n < WaveIn.DeviceCount; n++)
            //{
            //    WaveInCapabilities deviceInfo = WaveIn.GetCapabilities(n);
            //    if (deviceInfo.ProductName.ToLower().Contains("cable"))
            //        continue;// excluding virtual cable

            //    cmbAudioInputs.Items.Add($"{n}: {deviceInfo.ProductName}");
            //}

            //// Select the first device by default
            //if (cmbAudioInputs.Items.Count > 0)
            //{
            //    cmbAudioInputs.SelectedIndex = 0;
            //}
        }

        private void btnUploadFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "WAV files (*.wav)|*.wav"; // Filter to only show .wav files
            openFileDialog.Title = "Select a WAV File";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // Check if the file is a .wav file
                if (Path.GetExtension(openFileDialog.FileName).Equals(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    string sourcePath = openFileDialog.FileName;
                    string destPath = Path.Combine(DataUtils.AudioDataPath, Path.GetFileName(sourcePath));

                    try
                    {
                        var audioDirectory = Path.GetDirectoryName(destPath);
                        if (!Directory.Exists(audioDirectory))
                        {
                            Directory.CreateDirectory(audioDirectory);
                        }

                        File.Copy(sourcePath, destPath, true); // true allows overwriting
                        macro.voiceFilePath = destPath;
                        macro.isEmpty = false;
                        MessageBox.Show("File uploaded successfully.");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error: " + ex.Message);
                    }
                }
                else
                {
                    MessageBox.Show("Please select a valid .wav file.");
                }
            }
        }

        private void btnStartRecording_Click(object sender, EventArgs e)
        {
            
            //int selectedDevice = cmbAudioInputs.SelectedIndex;
            //if (selectedDevice < 0)
            //{
            //    MessageBox.Show("Please select a recording device.");
            //    return;
            //}

            //waveIn = new WaveIn();
            //waveIn.WaveFormat = new WaveFormat(44100, 1); // TODO: Need to adjust 
            //waveIn.DeviceNumber = selectedDevice;
            //waveIn.DataAvailable += WaveIn_DataAvailable;
            //waveIn.RecordingStopped += WaveIn_RecordingStopped;

            //string recordedFilePath = Path.Combine(Application.StartupPath, "Audio", outputFile + "_recorded.wav");

            //var audioDirectory = Path.GetDirectoryName(recordedFilePath);
            //if (!Directory.Exists(audioDirectory))
            //{
            //    Directory.CreateDirectory(audioDirectory);
            //}

            //if (!File.Exists(recordedFilePath))
            //    File.Create(recordedFilePath).Close();

            //macro.voiceFilePath = recordedFilePath;
            //waveFileWriter = new WaveFileWriter(Path.Combine(Application.StartupPath, "Audio", outputFile + "_recorded.wav"), waveIn.WaveFormat);
            //stopwatch.Start();
            //lblStopWatch.Text = "00:00";
            //pictureBox1.Enabled = true;
            //pictureBox1.Visible = true;
            //waveIn.StartRecording();
        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (waveFileWriter != null)
            {
                waveFileWriter.Write(e.Buffer, 0, e.BytesRecorded);
                waveFileWriter.Flush();
            }
        }

        private void btnStopRecording_Click(object sender, EventArgs e)
        {
            //stopwatch.Stop();
            //stopwatch.Reset();
            //lblStopWatch.Text = "";
            //pictureBox1.Enabled = false;
            //pictureBox1.Visible = false;
            //if (waveIn != null)
            //{
            //    waveIn.StopRecording();
            //}
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

        private void button1_Click(object sender, EventArgs e)
        {            
            this.Close();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {            
            if (comboBox1.SelectedItem != null)
            {
                Color selectedColor = (Color)comboBox1.SelectedItem;
                macro.ColorName = selectedColor.Name;
                // Now you can use selectedColor as needed
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            button3.Enabled = true;
            button2.Enabled = false;
            int selectedDevice = inputDeviceNumber;//cmbAudioInputs.SelectedIndex;
            if (selectedDevice < 0)
            {
                MessageBox.Show("Please select a recording device.");
                return;
            }

            waveIn = new WaveIn();
            waveIn.WaveFormat = new WaveFormat(44100, 1); // TODO: Need to adjust 
            waveIn.DeviceNumber = selectedDevice;
            waveIn.DataAvailable += WaveIn_DataAvailable;
            waveIn.RecordingStopped += WaveIn_RecordingStopped;

            string recordedFilePath = Path.Combine(Application.StartupPath, "Audio", outputFile + "_recorded.wav");

            var audioDirectory = Path.GetDirectoryName(recordedFilePath);
            if (!Directory.Exists(audioDirectory))
            {
                Directory.CreateDirectory(audioDirectory);
            }

            if (!File.Exists(recordedFilePath))
                File.Create(recordedFilePath).Close();

            stopwatch.Start();
            lblStopWatch.Text = "00:00";
            pictureBox1.Enabled = true;
            pictureBox1.Visible = true;

            macro.voiceFilePath = recordedFilePath;
            waveFileWriter = new WaveFileWriter(Path.Combine(Application.StartupPath, "Audio", outputFile + "_recorded.wav"), waveIn.WaveFormat);

            waveIn.StartRecording();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            button3.Enabled = false;
            button2.Enabled = true;
            stopwatch.Stop();
            stopwatch.Reset();
            lblStopWatch.Text = "";
            pictureBox1.Enabled = false;
            pictureBox1.Visible = false;
            if (waveIn != null)
            {
                waveIn.StopRecording();
            }

            MessageBox.Show("Recording has been saved for this short key.");
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (button3.Enabled && stopwatch.IsRunning)
            {
                lblStopWatch.Text = stopwatch.Elapsed.ToString(@"mm\:ss\.fff");
            }
        }

        private void MacroDetails_FormClosing(object sender, FormClosingEventArgs e)
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

        private void btnUploadFile_MouseEnter(object sender, EventArgs e)
        {
            btnUploadFile.Height += 2;
            btnUploadFile.Width += 2;
        }

        private void btnUploadFile_MouseLeave(object sender, EventArgs e)
        {
            btnUploadFile.Height -= 2;
            btnUploadFile.Width -= 2;
        }
    }
}
