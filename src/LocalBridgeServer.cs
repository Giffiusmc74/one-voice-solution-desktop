/*
 * LocalBridgeServer.cs  —  v7.27
 * ONE Voice Solution
 *
 * Hosts a tiny HTTP server on localhost:9001 so the Script Dashboard
 * can send real-time play commands to the desktop app.
 *
 * AUDIO ARCHITECTURE:
 *   waveOut  → VB-Audio Cable Input  (customer hears the recording)
 *   waveO    → outputDeviceNumber    (agent hears the recording)
 *   Single meter loop fires OnPlaybackLevel for both meters.
 *
 * VOLUME — uses WaveOutEvent.Volume (hardware-level float 0.0-1.0).
 *   This sets volume at the device driver level with ZERO sample processing,
 *   so it cannot cause clipping or garbling regardless of slider position.
 *   VolumeSampleProvider is NOT used anywhere in this class.
 *
 *   /volume { channel:"agent",    volume:0-100 } → waveO.Volume
 *   /volume { channel:"customer", volume:0-100 } → waveOut.Volume
 */
using NAudio.Wave;
using Newtonsoft.Json;
using NLog;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsFormsApp1.src
{
    public class LocalBridgeServer : IDisposable
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        private static LocalBridgeServer _instance;
        public static LocalBridgeServer Instance => _instance ?? (_instance = new LocalBridgeServer());

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<float, string> OnPlaybackLevel;  // (level, channel)
        public event Action OnPlaybackStopped;

        // ── Logging ───────────────────────────────────────────────────────────
        private readonly Logger _log = LogManager.GetCurrentClassLogger();

        // ── HTTP server ───────────────────────────────────────────────────────
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private bool _disposed;
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        // ── Audio state ───────────────────────────────────────────────────────
        private WaveOutEvent    waveOut;           // → VB-Cable (customer)
        private AudioFileReader audioFileReader;

        private WaveOutEvent    waveO;             // → headset (agent)
        private AudioFileReader audioFileReader2;

        private readonly object _playLock = new object();
        private bool   isAudioPlaying;

        /// <summary>True while a recording is actively playing. Used by MainFormV5 to suppress loopback meter during playback.</summary>
        public bool IsPlaying => isAudioPlaying;
        private long   lastReadPosition;
        private CancellationTokenSource cancellationTokenSource;
        private string _currentTmpPath;   // temp file for current playback — deleted on stop

        // ── Device numbers ────────────────────────────────────────────────────
        private int outputDeviceNumber = -1;   // headset (agent) — set by dropdown
        private int _cableDeviceNumber  = -1;  // VB-Cable — found by name on startup

        // ── Per-channel volumes (0-100) ───────────────────────────────────────
        // Applied via WaveOutEvent.Volume — hardware-level, no sample processing.
        private int _agentVol    = 80;
        private int _customerVol = 100;

        // ── Device setters ────────────────────────────────────────────────────
        /// <summary>Called by MainFormV5 when the headset dropdown changes.</summary>
        public void SetOutputDevice(int deviceNumber)
        {
            outputDeviceNumber = deviceNumber;
            _log.Info($"[Bridge] Agent device → #{deviceNumber}");
        }

        /// <summary>Current headset WaveOut device number (-1 = default).</summary>
        public int OutputDeviceNumber => outputDeviceNumber;

        /// <summary>VB-Cable WaveOut device number (-1 = not found).</summary>
        public int CableDeviceNumber => _cableDeviceNumber;

        /// <summary>Called by MainFormV5 on startup after VB-Cable is found.</summary>
        public void SetCableDevice(int deviceNumber)
        {
            _cableDeviceNumber = deviceNumber;
            _log.Info($"[Bridge] Cable device → #{deviceNumber}");
        }

        public void SetInitialVolume(string channel, int volume)
        {
            SetVolume(channel, volume);
            _log.Info($"[Bridge] Initial volume {channel} → {volume}%");
        }

        /// <summary>
        /// Updates the volume for a channel in real-time. 
        /// Called by MainFormV5 sliders for zero-latency updates.
        /// </summary>
        public void SetVolume(string channel, int volume)
        {
            volume = Math.Max(0, Math.Min(100, volume));
            lock (_playLock)
            {
                if (channel == "customer")
                {
                    _customerVol = volume;
                    if (audioFileReader != null) audioFileReader.Volume = volume / 100f;
                }
                else
                {
                    _agentVol = volume;
                    if (audioFileReader2 != null) audioFileReader2.Volume = volume / 100f;
                }
            }
        }

        // ── Start / Stop ──────────────────────────────────────────────────────
        public void Start()
        {
            if (_listener != null) return;
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://localhost:9001/");
                _listener.Start();
                _cts = new CancellationTokenSource();
                Task.Run(() => ListenLoop(_cts.Token));
                _log.Info("[Bridge] Listening on http://localhost:9001/");
            }
            catch (Exception ex)
            {
                _log.Warn($"[Bridge] Could not start: {ex.Message}");
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _listener?.Stop(); } catch { }
            _listener = null;
            StopAudio();
        }

        // ── Listen loop ───────────────────────────────────────────────────────
        private async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(ctx));
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex) { _log.Warn($"[Bridge] Listen error: {ex.Message}"); }
            }
        }

        // ── Request handler ───────────────────────────────────────────────────
        private async Task HandleRequest(HttpListenerContext ctx)
        {
            var req  = ctx.Request;
            var resp = ctx.Response;
            resp.Headers.Add("Access-Control-Allow-Origin", "*");
            resp.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            resp.ContentType = "application/json";

            if (req.HttpMethod == "OPTIONS") { resp.StatusCode = 204; resp.Close(); return; }

            string path = req.Url.AbsolutePath.ToLowerInvariant().TrimEnd('/');
            string body = "";
            if (req.HasEntityBody)
                using (var sr = new StreamReader(req.InputStream, req.ContentEncoding))
                    body = await sr.ReadToEndAsync();

            try
            {
                string json;
                switch (path)
                {
                    case "/play":   json = await HandlePlay(body); break;
                    case "/stop":   StopAudio(); json = "{\"ok\":true}"; break;
                    case "/volume": json = HandleVolume(body); break;
                    case "/status": json = "{\"ok\":true,\"playing\":" + (isAudioPlaying ? "true" : "false") + "}"; break;
                    default:        resp.StatusCode = 404; json = "{\"error\":\"Not found\"}"; break;
                }
                byte[] buf = Encoding.UTF8.GetBytes(json);
                resp.ContentLength64 = buf.Length;
                await resp.OutputStream.WriteAsync(buf, 0, buf.Length);
            }
            catch (Exception ex)
            {
                byte[] err = Encoding.UTF8.GetBytes("{\"error\":\"" + ex.Message.Replace("\"", "'") + "\"}");
                resp.StatusCode = 500; resp.ContentLength64 = err.Length;
                await resp.OutputStream.WriteAsync(err, 0, err.Length);
            }
            finally { resp.Close(); }
        }

        // ── /play ─────────────────────────────────────────────────────────────
        private async Task<string> HandlePlay(string body)
        {
            dynamic data    = JsonConvert.DeserializeObject(body);
            string audioUrl = (string)data?.audioUrl;
            if (string.IsNullOrWhiteSpace(audioUrl))
                return "{\"error\":\"audioUrl is required\"}";

            // Download to temp file
            string tmpPath = Path.Combine(Path.GetTempPath(), "ov_script_" + Guid.NewGuid().ToString("N") + ".mp3");
            byte[] bytes   = await _http.GetByteArrayAsync(audioUrl);
            File.WriteAllBytes(tmpPath, bytes);

            lock (_playLock)
            {
                if (isAudioPlaying) StopAudioInternal();

                _currentTmpPath  = tmpPath;
                lastReadPosition = 0;
                isAudioPlaying   = true;
                cancellationTokenSource?.Cancel();
                cancellationTokenSource = new CancellationTokenSource();

                // ── VB-Cable output (customer hears recording) ────────────────
                // Only create if VB-Cable was found. Never fall back to device -1
                // (default device = computer speakers).
                if (_cableDeviceNumber >= 0)
                {
                    try
                    {
                        audioFileReader = new AudioFileReader(tmpPath);
                        // Use AudioFileReader.Volume (software gain) — reliable on ALL drivers.
                        // WaveOutEvent.Volume is ignored by many drivers (e.g. Jabra).
                        audioFileReader.Volume = Math.Max(0f, Math.Min(1f, _customerVol / 100f));
                        waveOut = new WaveOutEvent { DeviceNumber = _cableDeviceNumber, DesiredLatency = 100 };
                        waveOut.Init(audioFileReader);
                        waveOut.Play();
                        waveOut.PlaybackStopped += OnPlaybackStopped_Cable;
                        _log.Info($"[Bridge] Cable WaveOut → device #{_cableDeviceNumber} vol={_customerVol}%");
                    }
                    catch (Exception ex)
                    {
                        _log.Warn($"[Bridge] Cable WaveOut failed: {ex.Message}");
                        try { audioFileReader?.Dispose(); } catch { }
                        audioFileReader = null; waveOut = null;
                    }
                }
                else
                {
                    _log.Info("[Bridge] No VB-Cable — skipping cable output.");
                }

                // ── Headset output (agent hears recording) ────────────────────
                try
                {
                    audioFileReader2 = new AudioFileReader(tmpPath);
                    // Use AudioFileReader.Volume (software gain) — reliable on ALL drivers.
                    // WaveOutEvent.Volume is ignored by many drivers (e.g. Jabra).
                    audioFileReader2.Volume = Math.Max(0f, Math.Min(1f, _agentVol / 100f));
                    waveO = new WaveOutEvent { DeviceNumber = outputDeviceNumber, DesiredLatency = 100 };
                    waveO.Init(audioFileReader2);
                    waveO.Play();
                    waveO.PlaybackStopped += OnPlaybackStopped_Agent;
                    _log.Info($"[Bridge] Agent WaveOut → device #{outputDeviceNumber} vol={_agentVol}%");
                }
                catch (Exception ex)
                {
                    _log.Error($"[Bridge] Agent WaveOut failed (device #{outputDeviceNumber}): {ex.Message}. No fallback.");
                    try { audioFileReader2?.Dispose(); } catch { }
                    audioFileReader2 = null; waveO = null;
                    isAudioPlaying = false;
                    OnPlaybackStopped?.Invoke();
                }

                // ── Meter loop ────────────────────────────────────────────────
                var token = cancellationTokenSource.Token;
                Task.Run(() => ProcessAudioAndVisualizeIntensity_(tmpPath, token));
            }

            return "{\"ok\":true}";
        }

        // ── Meter loop ────────────────────────────────────────────────────────
        private void ProcessAudioAndVisualizeIntensity_(string filePath, CancellationToken ct)
        {
            int bufferSize = 4096;
            using (var reader = new AudioFileReader(filePath))
            {
                if (lastReadPosition > 0 && lastReadPosition < reader.Length)
                    reader.Position = lastReadPosition;

                var buffer = new float[bufferSize];
                int samplesRead;
                while ((samplesRead = reader.Read(buffer, 0, buffer.Length)) > 0
                       && !ct.IsCancellationRequested
                       && isAudioPlaying)
                {
                    double rms      = CalculateRMS(buffer, samplesRead);
                    float  agentLvl = (float)Math.Min(1.0, rms * 8.0 * (_agentVol    / 100.0));
                    float  custLvl  = (float)Math.Min(1.0, rms * 8.0 * (_customerVol / 100.0));

                    OnPlaybackLevel?.Invoke(agentLvl, "agent");
                    OnPlaybackLevel?.Invoke(custLvl,  "customer");

                    lastReadPosition = reader.Position;
                    Thread.Sleep(75);
                }
                if (reader.Position >= reader.Length)
                    lastReadPosition = 0;
            }
        }

        private double CalculateRMS(float[] buffer, int samplesRead)
        {
            double sum = 0;
            for (int i = 0; i < samplesRead; i++) sum += buffer[i] * buffer[i];
            return Math.Sqrt(sum / samplesRead);
        }

        private void OnPlaybackStopped_Cable(object sender, StoppedEventArgs e)
        {
            try { audioFileReader?.Dispose(); audioFileReader = null; waveOut?.Dispose(); waveOut = null; } catch { }
            // Temp file cleanup is handled by OnPlaybackStopped_Agent (fires last)
        }

        private void OnPlaybackStopped_Agent(object sender, StoppedEventArgs e)
        {
            isAudioPlaying = false;
            try { audioFileReader2?.Dispose(); audioFileReader2 = null; waveO?.Dispose(); waveO = null; } catch { }
            DeleteCurrentTmpFile();
            OnPlaybackStopped?.Invoke();
        }

        private void DeleteCurrentTmpFile()
        {
            string path = _currentTmpPath;
            _currentTmpPath = null;
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (File.Exists(path)) File.Delete(path);
                _log.Info($"[Bridge] Deleted temp file: {path}");
            }
            catch (Exception ex)
            {
                _log.Warn($"[Bridge] Could not delete temp file '{path}': {ex.Message}");
            }
        }

        // ── /volume ───────────────────────────────────────────────────────────
        // Uses AudioFileReader.Volume (software gain, works on ALL drivers including Jabra).
        // WaveOutEvent.Volume is ignored by many drivers — AudioFileReader.Volume is reliable.
        // AudioFileReader.Volume range: 0.0-1.0 (same as slider / 100).
        private string HandleVolume(string body)
        {
            dynamic data    = JsonConvert.DeserializeObject(body);
            int     volume  = Math.Max(0, Math.Min(100, (int?)data?.volume ?? 80));
            string  channel = (string)data?.channel ?? "agent";

            lock (_playLock)
            {
                if (channel == "customer")
                {
                    _customerVol = volume;
                    if (audioFileReader  != null) audioFileReader.Volume  = volume / 100f;
                }
                else
                {
                    _agentVol = volume;
                    if (audioFileReader2 != null) audioFileReader2.Volume = volume / 100f;
                }
            }
            _log.Info($"[Bridge] Volume {channel} → {volume}%");
            return "{\"ok\":true,\"channel\":\"" + channel + "\",\"volume\":" + volume + "}";
        }

        // ── Stop ──────────────────────────────────────────────────────────────
        public void StopAudio()
        {
            lock (_playLock) { StopAudioInternal(); }
            OnPlaybackStopped?.Invoke();
        }

        private void StopAudioInternal()
        {
            isAudioPlaying = false;
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = null;

            try { waveOut?.Stop();  } catch { }
            try { waveOut?.Dispose(); } catch { }
            try { audioFileReader?.Dispose(); } catch { }
            waveOut = null; audioFileReader = null;

            try { waveO?.Stop();    } catch { }
            try { waveO?.Dispose(); } catch { }
            try { audioFileReader2?.Dispose(); } catch { }
            waveO = null; audioFileReader2 = null;

            DeleteCurrentTmpFile();
        }

        // ── Dispose ───────────────────────────────────────────────────────────
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _http.Dispose();
        }
    }
}
