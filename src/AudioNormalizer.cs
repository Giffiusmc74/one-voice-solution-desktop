using NAudio.Wave;
using System;
using System.IO;

namespace WindowsFormsApp1.src
{
    /// <summary>
    /// Normalizes WAV audio files to a consistent loudness level (-18 dBFS RMS target).
    /// Call NormalizeFile() after any recording is saved so that all script recordings
    /// play back at a consistent volume relative to the agent's live voice.
    /// </summary>
    public static class AudioNormalizer
    {
        // Target RMS level in linear scale.  -18 dBFS ≈ 0.126 linear.
        // This matches typical telephone/VoIP voice levels so recordings blend
        // seamlessly with the agent's live microphone.
        private const float TargetRmsLinear = 0.126f;

        // Safety ceiling — never amplify beyond this gain multiplier to avoid clipping
        private const float MaxGain = 10.0f;

        /// <summary>
        /// Reads a WAV file, calculates its RMS level, applies a gain correction to
        /// reach TargetRmsLinear, and writes the result back to the same file path.
        /// Supports 16-bit PCM and IEEE float WAV files via NAudio.
        /// </summary>
        /// <param name="filePath">Absolute path to the WAV file to normalize.</param>
        public static void NormalizeFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            try
            {
                float[] samples;
                WaveFormat format;

                // ── Step 1: Read all samples ──────────────────────────────────────
                using (var reader = new AudioFileReader(filePath))
                {
                    format = reader.WaveFormat;
                    int totalSamples = (int)(reader.Length / (reader.WaveFormat.BitsPerSample / 8));
                    samples = new float[totalSamples];
                    int read = 0;
                    int offset = 0;
                    var buffer = new float[4096];
                    while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        Array.Copy(buffer, 0, samples, offset, read);
                        offset += read;
                    }
                    // Trim to actual samples read
                    if (offset < samples.Length)
                        Array.Resize(ref samples, offset);
                }

                if (samples.Length == 0) return;

                // ── Step 2: Calculate RMS ─────────────────────────────────────────
                double sumSquares = 0;
                for (int i = 0; i < samples.Length; i++)
                    sumSquares += samples[i] * samples[i];
                float rms = (float)Math.Sqrt(sumSquares / samples.Length);

                if (rms < 0.0001f) return; // Silent file — nothing to normalize

                // ── Step 3: Calculate gain ────────────────────────────────────────
                float gain = TargetRmsLinear / rms;
                gain = Math.Min(gain, MaxGain); // Cap to avoid over-amplification

                // ── Step 4: Apply gain and clamp to [-1, 1] ───────────────────────
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = Math.Max(-1.0f, Math.Min(1.0f, samples[i] * gain));
                }

                // ── Step 5: Write normalized samples back to file ─────────────────
                string tempPath = filePath + ".tmp";
                using (var writer = new WaveFileWriter(tempPath, new WaveFormat(format.SampleRate, 16, format.Channels)))
                {
                    // Convert float samples back to 16-bit PCM
                    var pcmBuffer = new byte[samples.Length * 2];
                    for (int i = 0; i < samples.Length; i++)
                    {
                        short pcmSample = (short)(samples[i] * 32767);
                        pcmBuffer[i * 2] = (byte)(pcmSample & 0xFF);
                        pcmBuffer[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
                    }
                    writer.Write(pcmBuffer, 0, pcmBuffer.Length);
                }

                // Replace original with normalized version
                File.Delete(filePath);
                File.Move(tempPath, filePath);

                System.Diagnostics.Debug.WriteLine(
                    $"[AudioNormalizer] Normalized '{Path.GetFileName(filePath)}' " +
                    $"RMS {rms:F4} → {TargetRmsLinear:F4} (gain ×{gain:F2})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioNormalizer] Error normalizing '{filePath}': {ex.Message}");
                // Non-fatal — the original file remains untouched if normalization fails
            }
        }

        /// <summary>
        /// Returns the RMS level of a WAV file as a linear float (0.0 – 1.0).
        /// Useful for real-time level matching: compare this against the live mic RMS
        /// to compute a playback volume adjustment factor.
        /// </summary>
        public static float MeasureRms(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return 0f;

            try
            {
                double sumSquares = 0;
                long sampleCount = 0;
                var buffer = new float[4096];
                int read;

                using (var reader = new AudioFileReader(filePath))
                {
                    while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        for (int i = 0; i < read; i++)
                            sumSquares += buffer[i] * buffer[i];
                        sampleCount += read;
                    }
                }

                return sampleCount > 0 ? (float)Math.Sqrt(sumSquares / sampleCount) : 0f;
            }
            catch
            {
                return 0f;
            }
        }
    }
}
