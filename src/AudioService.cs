using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1.src
{
    public class AudioService
    {
        private static AudioService instance;
        public static AudioService Instance => instance ?? (instance = new AudioService());

        public WaveInEvent waveIn;
        public WaveOutEvent waveOut;
        
        public WaveOutEvent waveOutSecond;
        private WasapiLoopbackCapture loopbackCapture;
        public MMDevice mMDevice;

        private CancellationTokenSource cancellationTokenSource;

        BufferedWaveProvider bufferedWaveProvider;
        bool isAudioPlaying = false;

        private AudioFileReader audioFileReader;
        private MacrosInfo audioMacroInfo;

        public WaveOutEvent waveO;
        private AudioFileReader audioFileReader2;

        public int virtualCableInputDeviceNumber;
        public int outputDeviceNumber;

        public event EventHandler<AudioIntensityEventArgs> AudioIntensityChanged;

        public event EventHandler<AudioIntensityEventArgs> AudioMicIntensityChanged;

        public event EventHandler<AudioIntensityEventArgs> AudioRecIntensityChanged;

        // Microphone volume control
        private float microphoneVolume = 1.0f;
        private bool isMicrophoneMuted = false;
        
        // Incoming audio volume control
        private float incomingAudioVolume = 1.0f;

        // Real-time level matching: rolling RMS of the agent's live mic (updated by WaveIn_DataAvailable1)
        // Used to auto-adjust recording playback volume so recordings match the agent's voice level.
        private float _liveMicRms = 0.126f; // Default to target level (-18 dBFS) until we have real data
        private const float TargetRmsLinear = 0.126f; // -18 dBFS — same target as AudioNormalizer
        private const float MinPlaybackGain = 0.5f;   // Never go below 50% of nominal volume
        private const float MaxPlaybackGain = 2.0f;   // Never go above 200% to avoid clipping

        // Public methods to trigger meter updates from script playback
        public void TriggerRecordingMeterUpdate(int intensity)
        {
            AudioRecIntensityChanged?.Invoke(this, new AudioIntensityEventArgs(intensity));
        }

        /// <summary>
        /// Set microphone input volume (0.0 to 1.0)
        /// </summary>
        public void SetMicrophoneVolume(float volume)
        {
            microphoneVolume = Math.Max(0.0f, Math.Min(1.0f, volume));
            
            // Apply volume to the microphone device if available
            try
            {
                if (waveIn != null)
                {
                    // For WaveIn, we'll apply volume scaling in the data processing
                    // The actual volume control will be applied during audio processing
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting microphone volume: {ex.Message}");
            }
        }

        /// <summary>
        /// Mute or unmute the microphone
        /// </summary>
        public void MuteMicrophone(bool mute)
        {
            isMicrophoneMuted = mute;
        }

        /// <summary>
        /// Get current microphone volume level
        /// </summary>
        public float GetMicrophoneVolume()
        {
            return isMicrophoneMuted ? 0.0f : microphoneVolume;
        }

        /// <summary>
        /// Set incoming audio volume (0.0 to 1.0)
        /// </summary>
        public void SetIncomingAudioVolume(float volume)
        {
            incomingAudioVolume = Math.Max(0.0f, Math.Min(1.0f, volume));
        }

        /// <summary>
        /// Get current incoming audio volume level
        /// </summary>
        public float GetIncomingAudioVolume()
        {
            return incomingAudioVolume;
        }

        public void TriggerSpeakerMeterUpdate(int intensity)
        {
            AudioIntensityChanged?.Invoke(this, new AudioIntensityEventArgs(intensity));
        }

        public void TriggerMicMeterUpdate(int intensity)
        {
            AudioMicIntensityChanged?.Invoke(this, new AudioIntensityEventArgs(intensity));
        }

        private AudioService()
        {
            // Initialize audio devices here or in a separate init method
        }


        public void StartLoopbackCapture(string speaker1, string speaker2)
        {
            if (waveOutSecond != null)
            {
                waveOutSecond.Stop();
                waveOutSecond.Dispose();
                waveOutSecond = null;
            }

            mMDevice = GetSpecificPlaybackDevice(speaker1);
            // Initialize loopback capture from Speaker 1
            if (mMDevice != null)
                loopbackCapture = new WasapiLoopbackCapture(mMDevice); // Default playback device, adjust as needed
            else
                loopbackCapture = new WasapiLoopbackCapture();

            loopbackCapture.DataAvailable += OnLoopbackDataAvailable;
            loopbackCapture.RecordingStopped += OnLoopbackRecordingStopped;
            //loopbackCapture.StartRecording();

            // Initialize waveOut for Speaker 2
            string deviceSpeaker2 = speaker2.Contains(":") ? speaker2.Substring(0, speaker2.Length - 3) : speaker2;

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
            // Apply incoming audio volume control
            float currentIncomingVolume = GetIncomingAudioVolume();
            
            // Apply volume scaling to the incoming audio buffer
            byte[] scaledBuffer = new byte[e.BytesRecorded];
            for (int i = 0; i < e.BytesRecorded; i += 4) // 32-bit float audio
            {
                if (i + 3 < e.BytesRecorded)
                {
                    // Convert bytes to float
                    float sample = BitConverter.ToSingle(e.Buffer, i);
                    
                    // Apply volume scaling
                    sample *= currentIncomingVolume;
                    
                    // Convert back to bytes
                    byte[] sampleBytes = BitConverter.GetBytes(sample);
                    Array.Copy(sampleBytes, 0, scaledBuffer, i, 4);
                }
            }
            
            // Add scaled data to the buffer for playback
            bufferedWaveProvider.AddSamples(scaledBuffer, 0, e.BytesRecorded);

            // Calculate RMS with volume scaling applied for intensity meter
            float rmsValue = CalculateRMS(scaledBuffer);
            int intensityPercentage = (int)(rmsValue * 100);

            // Assuming you have two separate controls: audioIntensityMeterSpeaker1 and audioIntensityMeterSpeaker2
            // Update UI controls safely from the background thread
            try
            {
                AudioIntensityChanged?.Invoke(this, new AudioIntensityEventArgs(intensityPercentage));
                //if (this.IsHandleCreated && !this.IsDisposed)
                //{
                //    this.Invoke(new Action(() =>
                //    {
                //        float volumeAdjustmentSpeaker1 = volumeA1Speaker.Value / 100f;
                //        float volumeAdjustmentSpeaker2 = volumeA2Speaker.Value / 100f;

                //        int speaker1Adjusted = (int)(intensityPercentage * volumeAdjustmentSpeaker1);
                //        int speaker2Adjusted = (int)(intensityPercentage * volumeAdjustmentSpeaker2);
                //        if (audioIntensityMeterSpeaker11 != null)
                //            audioIntensityMeterSpeaker11.UpdateIntensity(speaker1Adjusted);
                //        if (audioIntensityMeterSpeaker22 != null && waveOutSecond != null)
                //            audioIntensityMeterSpeaker22.UpdateIntensity(speaker2Adjusted);
                //    }));
                //}
            }
            catch (ObjectDisposedException)
            {
                // Handle or ignore the exception
                // This can happen if the form is disposed before the invoke is executed
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

        private MMDevice GetSpecificPlaybackDevice(string deviceNameHint)
        {
            if (deviceNameHint == null || string.IsNullOrEmpty(deviceNameHint))
                return null;

            if (deviceNameHint.Contains(":"))
            {
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

        public void StartMicrophoneCaptureNew(string comboBoxMicroPhone)
        {
            // Initialize WaveIn for agent's machine main micrphone

            string micrphoneName = comboBoxMicroPhone;
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

                waveIn.StartRecording();

                // Start playback to the virtual audio cable
                waveOut.Play();
            }
        }

        private void WaveIn_DataAvailable1(object sender, WaveInEventArgs e)
        {
            // Apply microphone volume control and muting
            float currentMicVolume = GetMicrophoneVolume();
            
            // If microphone is muted, zero out the buffer and show no intensity
            if (isMicrophoneMuted || currentMicVolume == 0.0f)
            {
                // Zero out the audio buffer (mute the microphone)
                for (int i = 0; i < e.BytesRecorded; i++)
                {
                    e.Buffer[i] = 0;
                }
                
                // Show zero intensity when muted
                try
                {
                    AudioMicIntensityChanged?.Invoke(this, new AudioIntensityEventArgs(0));
                }
                catch (ObjectDisposedException) { }
                return;
            }

            // Apply volume scaling to the audio buffer
            double sumOfSquares = 0;
            for (int i = 0; i < e.BytesRecorded; i += 2) // 16-bit audio, hence step by 2 bytes
            {
                short sample = (short)((e.Buffer[i + 1] << 8) | e.Buffer[i]);
                
                // Apply volume scaling
                sample = (short)(sample * currentMicVolume);
                
                // Write back the scaled sample
                e.Buffer[i] = (byte)(sample & 0xFF);
                e.Buffer[i + 1] = (byte)((sample >> 8) & 0xFF);
                
                sumOfSquares += sample * sample;
            }
             double rms = Math.Sqrt(sumOfSquares / (e.BytesRecorded / 2));

            // Update the rolling live mic RMS for real-time level matching during playback
            // Convert from 16-bit scale (0-32768) to linear float (0.0-1.0)
            float liveMicRmsLinear = (float)(rms / 32768.0);
            if (liveMicRmsLinear > 0.001f) // Only update when there is actual speech (ignore silence)
                _liveMicRms = liveMicRmsLinear * 0.1f + _liveMicRms * 0.9f; // Smooth with 90% EMA

            // Normalize RMS to a 0-100 scale and apply volume scaling to the display
            int normalizedRMS = (int)(rms / 32768 * 100 * currentMicVolume);
            normalizedRMS = Math.Min(normalizedRMS, 100); // Cap at 100
            
            try
            {
                AudioMicIntensityChanged?.Invoke(this, new AudioIntensityEventArgs(normalizedRMS));
                //if (this.IsHandleCreated && !this.IsDisposed)
                //{
                //    // Adjust intensity based on the TrackBar's volume setting
                //    this.Invoke((MethodInvoker)(() =>
                //    {
                //        int volumeSetting = volumeA1Mic.Value; // Assume trackBarVolume is your TrackBar control
                //        int adjustedIntensity = normalizedRMS * volumeSetting / 100; // Scale intensity by volume percentage
                //        adjustedIntensity = Math.Min(adjustedIntensity, 100); // Ensure it doesn't exceed 100%
                //        if (audioIntensityMeterMic1 != null)
                //            audioIntensityMeterMic1.UpdateIntensity(adjustedIntensity);
                //    }));
                //}

            }
            catch (ObjectDisposedException)
            {
                // Handle or ignore the exception
                // This can happen if the form is disposed before the invoke is executed
            }
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



        public void PlayAudio(MacrosInfo info)
        {
            try
            {
                if (!isAudioPlaying)
                {
                    
                    lastReadPosition = 0;

                    // Set up the WaveOut device to play through the virtual cable output for agent's
                    isAudioPlaying = true;

                    cancellationTokenSource?.Cancel();
                    cancellationTokenSource = new CancellationTokenSource();

                    waveOut = new WaveOutEvent
                    {
                        DeviceNumber = FindWaveOutDeviceNumber("cable")
                    };
                        // ── Real-time level matching ───────────────────────────────────────────────
                    // Recordings are normalized to -18 dBFS at save time (AudioNormalizer).
                    // Here we compute a fine-tuning gain so the recording matches the agent's
                    // current live voice level, compensating for call-to-call mic variations.
                    float playbackGain = 1.0f;
                    if (_liveMicRms > 0.001f && TargetRmsLinear > 0.0001f)
                    {
                        playbackGain = _liveMicRms / TargetRmsLinear;
                        playbackGain = Math.Max(MinPlaybackGain, Math.Min(MaxPlaybackGain, playbackGain));
                    }
                    System.Diagnostics.Debug.WriteLine(
                        $"[AudioService] PlayAudio level-match gain: {playbackGain:F2}x (liveMicRms={_liveMicRms:F4})");

                    // Load the recorded audio file
                    audioFileReader = new AudioFileReader(info.voiceFilePath);
                    audioFileReader.Volume = playbackGain; // Apply level-match gain to agent-ear playback
                    // Initialize WaveOut with the audio file and start playback
                    waveOut.Init(audioFileReader);
                    waveOut.Play();
                    // Mute or stop the WaveIn capturing (agent's microphone)
                    waveIn.StopRecording();
                    // Handle Playback Stopped event
                    waveOut.PlaybackStopped += OnPlaybackStopped;
                   // timer1.Start(); // timer to update remaining time
                    // new Wavout for agent                    
                    //string deviceSpeaker = cmbBoxSpeaker.Text.Contains(":") ? cmbBoxSpeaker.Text.Substring(0, cmbBoxSpeaker.Text.Length - 3) : cmbBoxSpeaker.Text;
                    //outputDeviceNumber = FindWaveOutDeviceNumber(deviceSpeaker);
                    waveO = new WaveOutEvent
                    {
                        DeviceNumber = outputDeviceNumber, // Implement this to find the correct device index
                        DesiredLatency = 100 // Experiment with this value, starting as low as 100ms
                    };
                    audioFileReader2 = new AudioFileReader(info.voiceFilePath);
                    audioFileReader2.Volume = playbackGain; // Apply same gain to customer-facing playback
                    waveO.Init(audioFileReader2);
                    waveO.Play();;
                    waveO.PlaybackStopped += OnPlaybackStopped2;

                    // Start processing audio for intensity visualization in a separate task
                    Task.Run(() => ProcessAudioAndVisualizeIntensity_(info.voiceFilePath, cancellationTokenSource.Token));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:" + ex.Message);
            }
        }        

        private long lastReadPosition = 0;
        public void ProcessAudioAndVisualizeIntensity_(string filePath, CancellationToken cancellationToken)
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

                    AudioRecIntensityChanged?.Invoke(this,new AudioIntensityEventArgs(intensity));

                    //// Update your AudioIntensityMeter control on the UI thread
                    //if (audioIntensityMeterRecIn.InvokeRequired && audioIntensityMeterRecOut.InvokeRequired)
                    //{
                    //    audioIntensityMeterRecIn.Invoke(new Action(() => {
                    //        float volumeAdjustmentRecIn = volumeA1RecIn.Value / 100f;
                    //        int recInAdjusted = (int)(intensity * volumeAdjustmentRecIn);
                    //        audioIntensityMeterRecIn.UpdateIntensity(recInAdjusted);

                    //    }));
                    //    audioIntensityMeterRecOut.Invoke(new Action(() => {
                    //        float volumeAdjustmentRecOut = volumeA1RecOut.Value / 100f;
                    //        int recOutAdjusted = (int)(intensity * volumeAdjustmentRecOut);
                    //        audioIntensityMeterRecOut.UpdateIntensity(recOutAdjusted);

                    //    }));
                    //}
                    //else
                    //{
                    //    float volumeAdjustmentRecIn = volumeA1RecIn.Value / 100f;
                    //    int recInAdjusted = (int)(intensity * volumeAdjustmentRecIn);
                    //    audioIntensityMeterRecIn.UpdateIntensity(recInAdjusted);

                    //    float volumeAdjustmentRecOut = volumeA1RecOut.Value / 10f;
                    //    int recOutAdjusted = (int)(intensity * volumeAdjustmentRecOut);
                    //    audioIntensityMeterRecOut.UpdateIntensity(recOutAdjusted);
                    //}

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

        private void RefreshIntensityMeter()
        {
            AudioRecIntensityChanged?.Invoke(this, new AudioIntensityEventArgs(0));            
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
                
                // Once playback is finished, start capturing from the agent's microphone again
                waveIn.StartRecording();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:" + ex.Message);
            }
        }

        public void StopAudioRecordings()
        {
            if (isAudioPlaying)
            {
                
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
               // timer1.Stop();

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
                //waveIn.StartRecording();

               // lblTimeRemaining.Text = "Rec. Time: 00:00";
            }
            
        }

        public void Dispose()
        {
            if (waveIn != null)
            {
                waveIn.StopRecording();
                waveIn.Dispose();
            }

            if (waveOut != null)
            {
                waveOut.Stop();
                waveOut.Dispose();
            }

            if (audioFileReader != null)
            {
                audioFileReader.Close();
                audioFileReader = null;
            }

            if (audioFileReader2 != null)
            {
                audioFileReader2.Dispose();
                audioFileReader2 = null;
            }            

            if (loopbackCapture != null)
            {
                loopbackCapture.StopRecording();
                // loopbackCapture.Dispose();
            }

            if (waveOutSecond != null)
            {
                waveOutSecond.Stop();
                waveOutSecond.Dispose();
            }
        }
    }

    public class AudioIntensityEventArgs : EventArgs
    {
        public int Intensity { get; set; }       

        public AudioIntensityEventArgs(int intensity)
        {
            Intensity = intensity;            
        }
    }
}
