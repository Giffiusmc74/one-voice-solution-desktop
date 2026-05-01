/*
 * LocalBridgeServer.cs  —  v7.64
 * ONE Voice Solution
 *
 * Hosts a tiny HTTP server on localhost:9001 so the Script Dashboard
 * can send real-time play commands to the desktop app.
 *
 * AUDIO ARCHITECTURE:
 *   waveOut  → VB-Audio Cable Input  (customer hears the recording)
 *   Agent hears the recording via the app monitor path (portal CallBoard).
 *   Card audio NEVER goes to Jabra — this keeps the Jabra loopback (red meter)
 *   clean so it only captures customer voice from the softphone.
 *
 * VOLUME — uses AudioFileReader.Volume (software-level float 0.0-1.0).
 *   This is reliable across ALL drivers including Jabra.
 *   WaveOutEvent.Volume is ignored by many drivers — not used here.
 *
 *   /volume { channel:"customer", volume:0-100 } → audioFileReader.Volume
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

        private readonly object _playLock = new object();
        private bool   isAudioPlaying;

        /// <summary>True while a recording is actively playing.</summary>
        public bool IsPlaying => isAudioPlaying;
        private long   lastReadPosition;
        private CancellationTokenSource cancellationTokenSource;
        private string _currentTmpPath;   // temp file for current playback — deleted on stop

        // ── Device numbers ────────────────────────────────────────────────────
        private int _cableDeviceNumber  = -1;  // VB-Cable — found by name on startup

        // ── Per-channel volumes (0-100) ───────────────────────────────────────
        // Applied via AudioFileReader.Volume — software-level, works on ALL drivers.
        private int _customerVol = 100;

        // ── Device setters ────────────────────────────────────────────────────
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
                // "agent" channel volume is handled by the app monitor path — no waveO here.
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
                // Card audio goes ONLY to VB Cable — NEVER to Jabra.
                // This keeps the Jabra loopback (red meter) clean.
                // Only create if VB-Cable was found. Never fall back to device -1
                // (default device = system speakers / Jabra).
                if (_cableDeviceNumber >= 0)
                {
                    try
                    {
                        audioFileReader = new AudioFileReader(tmpPath);
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
                        isAudioPlaying = false;
                        OnPlaybackStopped?.Invoke();
                        return "{\"error\":\"Cable WaveOut failed: " + ex.Message.Replace("\"", "'") + "\"}";
                    }
                }
                else
                {
                    _log.Warn("[Bridge] No VB-Cable found — cannot play card. Aborting to prevent Jabra leak.");
                    isAudioPlaying = false;
                    DeleteCurrentTmpFile();
                    return "{\"error\":\"VB-Cable not found — card not played\"}";
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
                    float  agentLvl = (float)Math.Min(1.0, rms * 8.0 * (_customerVol / 100.0));
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
            isAudioPlaying = false;
            try { audioFileReader?.Dispose(); audioFileReader = null; waveOut?.Dispose(); waveOut = null; } catch { }
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
        private string HandleVolume(string body)
        {
            dynamic data    = JsonConvert.DeserializeObject(body);
            int     volume  = Math.Max(0, Math.Min(100, (int?)data?.volume ?? 80));
            string  channel = (string)data?.channel ?? "customer";

            lock (_playLock)
            {
                if (channel == "customer")
                {
                    _customerVol = volume;
                    if (audioFileReader != null) audioFileReader.Volume = volume / 100f;
                }
                // "agent" channel: no waveO — handled by app monitor path
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
