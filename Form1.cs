using NAudio.CoreAudioApi;
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
    public partial class Form1 : Form
    {
        bool cancelTransmission = false;
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            cancelTransmission = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            NewMethod();
            Main();
        }

        private void NewMethod()
        {
            button1.Enabled = false;
            string targetDeviceName = "CABLE Input"; // Replace with the name of your virtual cable

            // Find the device number based on the target device name
            int targetDeviceNumber = FindDeviceNumber2(targetDeviceName);

            if (targetDeviceNumber != -1)
            {
                var virtualMicOut = new WaveOutEvent();
                virtualMicOut.DeviceNumber = targetDeviceNumber;
                virtualMicOut.Init(new WaveFileReader("C:\\Users\\talha.baig\\Documents\\Sound recordings\\1.wav"));
                virtualMicOut.Volume = 1.0f;
                // Start playing the recorded audio through the virtual microphone
                virtualMicOut.Play();

                // Keep the application running
                Thread.Sleep(10000);

                // Stop playing when done
                virtualMicOut.Stop();
            }
            else
            {
                Console.WriteLine("Device not found.");
            }
            button1.Enabled = true;
        }

        int FindDeviceNumber2(string targetDeviceName)
        {
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var capabilities = WaveOut.GetCapabilities(i);
                if (capabilities.ProductName.Contains(targetDeviceName))
                {
                    return i;
                }
            }
            return -1; // Device not found
        }

        void Main()
        {
            // Set up the WaveInEvent for capturing audio from the microphone
            var microphone = new WaveInEvent();
            microphone.WaveFormat = new WaveFormat(44100, 1); // Adjust format as needed

            // Set up the WaveOutEvent for playing audio to the speakers
            var speaker = new WaveOutEvent();

            // Set up the WaveFileReader for reading an audio file
            var audioFile = new AudioFileReader(@"C:\Users\talha.baig\Documents\Sound recordings\2.wav");
            audioFile.Volume = 1.0f;
            // Ensure that the audio file has the same sample rate and channel count as the microphone
            //audioFile = ResampleIfNeeded(audioFile, microphone.WaveFormat);

            // Set up the MixingSampleProvider to mix audio streams
            var sampleProvider = new WaveInProvider(microphone);
            var mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 1));

            var test = mixer.WaveFormat.ToString();
            var test2 = sampleProvider.WaveFormat.ToString();
            var test3 = audioFile.WaveFormat.ToString();
            
            mixer.AddMixerInput(sampleProvider.ToSampleProvider());
            mixer.AddMixerInput(audioFile.ToSampleProvider());

            // Connect the mixer to the speaker
            speaker.Init(mixer);

            // Start capturing audio from the microphone
            microphone.StartRecording();

            // Start playing the audio file and capturing the microphone input
            speaker.Play();

            // Keep the application running
            Thread.Sleep(10000);

            // Stop capturing and playing when done
            microphone.StopRecording();
            speaker.Stop();
        }

        void ConvertToFormatT()
        {
            string inputFile = "C:\\Users\\talha.baig\\Documents\\Sound recordings\\1.wav";
            string outputFile = "C:\\Users\\talha.baig\\Documents\\Sound recordings\\2.wav";

            // Set the desired output WaveFormat (16-bit PCM, 44100Hz, 1 channel)
            WaveFormat outputWaveFormat = new WaveFormat(44100, 16, 1);

            using (var waveReader = new WaveFileReader(inputFile))
            {
                // Ensure that the input audio format matches the desired output format
                var conversionStream = new WaveFormatConversionStream(outputWaveFormat, waveReader);

                // Create a new WaveFileWriter with the desired output format
                using (var waveWriter = new WaveFileWriter(outputFile, outputWaveFormat))
                {
                    // Copy data from the conversion stream to the output file
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = conversionStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        waveWriter.Write(buffer, 0, bytesRead);
                    }
                }
            }

            Console.WriteLine("Conversion completed.");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            cancelTransmission = true;
        }
    }
}

