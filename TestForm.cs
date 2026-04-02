using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1
{

    public partial class TestForm : Form
    {
        private WaveInEvent waveIn;
        private WaveOut waveOut;
        private WasapiLoopbackCapture loopbackCapture;
        public TestForm()
        {
            InitializeComponent();
            // Initialize WaveInEvent for microphone
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            loopbackCapture = new WasapiLoopbackCapture();
            waveIn = new WaveInEvent
            {
                DeviceNumber = int.Parse(txtWaveIn.Text), // Set the microphone device number
                WaveFormat = new WaveFormat(44100, 16, 1), // Adjust as needed
                BufferMilliseconds = 70
            };

            // Initialize WaveOut for virtual audio cable
            waveOut = new WaveOut
            {
                DeviceNumber = int.Parse(txtWaveOut.Text), // Set the virtual audio cable device number
                DesiredLatency = 125 // Adjust latency as needed
            };

            loopbackCapture.StartRecording();
            // Start recording from the microphone
            waveIn.StartRecording();

            
            // Initialize WaveInProvider for playback
            waveOut.Init(new WaveInProvider(waveIn));

            // Start playback to the virtual audio cable
            waveOut.Play();

            btnStart.Enabled = false;
            btnStop.Enabled = true;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (waveOut != null && waveIn != null)
            {
                waveIn.StopRecording();
                waveOut.Stop();

                // Dispose of resources
                waveIn.Dispose();
                waveOut.Dispose();

                btnStart.Enabled = true;
                btnStop.Enabled = false;
            }
        }

        private void TestForm_Load(object sender, EventArgs e)
        {
            txtDevices.Text = "";
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var capabilities = WaveOut.GetCapabilities(i);
                if (capabilities.ProductName.ToLower().Contains("cable input"))
                {
                    txtDevices.Text = capabilities.ProductName + ":" + i.ToString();
                }
            }
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var capabilities = WaveIn.GetCapabilities(i);
                if (capabilities.ProductName.ToLower().Contains("microphone"))
                {
                    txtDevices.Text += "\r\n---------------------------------------";
                    txtDevices.Text += "\r\n" + capabilities.ProductName + ":" + i.ToString();
                }
            }
        }

        private void btnPlayAudio_Click(object sender, EventArgs e)
        {
            if (waveOut != null && waveIn != null)
            {
                waveIn.StopRecording();
                waveOut.Stop();
                PlayAudio();
            }

            loopbackCapture = new WasapiLoopbackCapture();
            waveOut.Init(new WaveInProvider(waveIn));
            waveOut.Play();
            loopbackCapture.StartRecording();
            waveIn.StartRecording();
        }

        private void PlayAudio()
        {
            string targetDeviceName = "CABLE Input"; // Replace with the name of your virtual cable
            int targetDeviceNumber = FindDeviceNumber2(targetDeviceName);

            if (targetDeviceNumber != -1)
            {
                var virtualMicOut = new WaveOutEvent();
                virtualMicOut.DeviceNumber = targetDeviceNumber;
                virtualMicOut.Init(new WaveFileReader("1.wav"));
                virtualMicOut.Volume = 1.0f;

                var virtualSpeaker = new WaveOutEvent();
                virtualSpeaker.DeviceNumber = FindDeviceNumber2("Speakers");//FindDeviceNumber2("Speakers (Logitech");
                virtualSpeaker.Init(new WaveFileReader("1.wav"));
                virtualSpeaker.Volume = 1.0f;

                virtualSpeaker.Play();
                virtualMicOut.Play();

                Thread.Sleep(10000); // Muneeb CHUTIYEEE

                virtualMicOut.Stop();
                virtualSpeaker.Stop();
            }
            else
            {
                Console.WriteLine("Device not found.");
            }
        }

        int FindDeviceNumber2(string targetDeviceName)
        {
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var capabilities = WaveOut.GetCapabilities(i);
                if (capabilities.ProductName.ToLower().Contains(targetDeviceName.ToLower()))
                {
                    return i;
                }
            }
            return -1; // Device not found
        }
    }

    
}
