using NAudio.Wave;
using System;

namespace WindowsFormsApp1
{
    // Helper class to monitor audio samples during playback
    public class SampleMonitoringProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        public event EventHandler<SampleReadEventArgs> SampleRead;

        public SampleMonitoringProvider(ISampleProvider source)
        {
            this.source = source;
            WaveFormat = source.WaveFormat;
        }

        public WaveFormat WaveFormat { get; private set; }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);
            
            if (samplesRead > 0 && SampleRead != null)
            {
                // Calculate RMS using the same method as AudioService
                float sumOfSquares = 0f;
                for (int i = offset; i < offset + samplesRead; i++)
                {
                    sumOfSquares += buffer[i] * buffer[i];
                }
                float rms = (float)Math.Sqrt(sumOfSquares / samplesRead);
                int intensity = (int)(rms * 100); // Same as AudioService loopback
                
                SampleRead?.Invoke(this, new SampleReadEventArgs(intensity));
            }
            
            return samplesRead;
        }
    }

    public class SampleReadEventArgs : EventArgs
    {
        public int Intensity { get; private set; }
        
        public SampleReadEventArgs(int intensity)
        {
            Intensity = intensity;
        }
    }
}
