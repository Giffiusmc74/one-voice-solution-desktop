/*
 * LocalBridgeServer.cs
 * ONE Voice Solution
 *
 * Hosts a tiny HTTP server on localhost:9001 so the Script Dashboard
 * can send real-time play commands to the desktop app.
 *
 * AUDIO ARCHITECTURE (matches original AudioService.cs exactly):
 *   waveOut  → VB-Audio Cable Input  (customer hears the recording)
 *   waveO    → outputDeviceNumber    (agent hears the recording, low volume)
 *   Single meter loop fires OnPlaybackLevel for both meters.
 *
 * VOLUME:
 *   /volume { channel:"agent",    volume:0-100 } → VolumeSampleProvider on waveO
 *   /volume { channel:"customer", volume:0-100 } → VolumeSampleProvider on waveOut
 */
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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

        // ── Audio state (matches original AudioService field names) ────────────
        private WaveOutEvent         waveOut;          // → VB-Cable (customer)
        private AudioFileReader      audioFileReader;
        private VolumeSampleProvider _volCable;        // volume for cable output

        private WaveOutEvent         waveO;            // → headset (agent)
        private AudioFileReader      audioFileReader2;
        private VolumeSampleProvider _volAgent;        // volume for agent output

        private readonly object _playLock = new object();
        private bool   isAudioPlaying;
        private long   lastReadPosition;
        private CancellationTokenSource cancellationTokenSource;

        // Device numbers
        private int outputDeviceNumber = -1;   // headset (agent) — set by dropdown
        private int _cableDeviceNumber  = -1;  // VB-Cable — found by name on startup

        // Per-channel volumes (0-100)
        private int _agentVol    = 80;
        private int _customerVol = 100;

        // ── Device setters ────────────────────────────────────────────────────
        /// <summary>Called by MainFormV5 when the headset dropdown changes.</summary>
        public void SetOutputDevice(int deviceNumber)
        {
            outputDeviceNumber = deviceNumber;
            _log.Info($"[Bridge] Agent device → #{deviceNumber}");
        }

        /// <summary>Called by MainFormV5 on startup after VB-Cable is found.</summary>
        public void SetCableDevice(int deviceNumber)
        {
            _cableDeviceNumber = deviceNumber;
            _log.Info($"[Bridge] Cable device → #{deviceNumber}");
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

        // ── /play — mirrors original AudioService.PlayAudio() ─────────────────
        private async Task<string> HandlePlay(string body)
        {
            dynamic data    = JsonConvert.DeserializeObject(body);
            string audioUrl = (string)data?.audioUrl;
            if (string.IsNullOrWhiteSpace(audioUrl))
                return "{\"error\":\"audioUrl is required\"}";

            // Download to temp file (same as original — file path passed to both WaveOuts)
            string tmpPath = Path.Combine(Path.GetTempPath(), "ov_script_" + Guid.NewGuid().ToString("N") + ".mp3");
            byte[] bytes   = await _http.GetByteArrayAsync(audioUrl);
            File.WriteAllBytes(tmpPath, bytes);

            lock (_playLock)
            {
                if (isAudioPlaying) StopAudioInternal();

                lastReadPosition = 0;
                isAudioPlaying   = true;
                cancellationTokenSource?.Cancel();
                cancellationTokenSource = new CancellationTokenSource();

                float cVol = _customerVol / 100f;
                float aVol = _agentVol    / 100f;

                // ── waveOut → VB-Cable (customer hears recording) ─────────────
                try
                {
                    audioFileReader = new AudioFileReader(tmpPath);
                    _volCable       = new VolumeSampleProvider(audioFileReader) { Volume = cVol };
                    waveOut         = new WaveOutEvent { DeviceNumber = _cableDeviceNumber, DesiredLatency = 100 };
                    waveOut.Init(_volCable);
                    waveOut.Play();
                    waveOut.PlaybackStopped += OnPlaybackStopped_Cable;
                    _log.Info($"[Bridge] Cable WaveOut → device #{_cableDeviceNumber} vol={_customerVol}%");
                }
                catch (Exception ex)
                {
                    _log.Warn($"[Bridge] Cable WaveOut failed: {ex.Message}");
                    try { audioFileReader?.Dispose(); } catch { }
                    audioFileReader = null; _volCable = null; waveOut = null;
                }

                // ── waveO → headset (agent hears recording) ───────────────────
                try
                {
                    audioFileReader2 = new AudioFileReader(tmpPath);
                    _volAgent        = new VolumeSampleProvider(audioFileReader2) { Volume = aVol };
                    waveO            = new WaveOutEvent { DeviceNumber = outputDeviceNumber, DesiredLatency = 100 };
                    waveO.Init(_volAgent);
                    waveO.Play();
                    waveO.PlaybackStopped += OnPlaybackStopped_Agent;
                    _log.Info($"[Bridge] Agent WaveOut → device #{outputDeviceNumber} vol={_agentVol}%");
                }
                catch (Exception ex)
                {
                    _log.Warn($"[Bridge] Agent WaveOut failed (device #{outputDeviceNumber}): {ex.Message}. Falling back to default.");
                    try { audioFileReader2?.Dispose(); } catch { }
                    try
                    {
                        audioFileReader2 = new AudioFileReader(tmpPath);
                        _volAgent        = new VolumeSampleProvider(audioFileReader2) { Volume = aVol };
                        waveO            = new WaveOutEvent { DeviceNumber = -1, DesiredLatency = 100 };
                        waveO.Init(_volAgent);
                        waveO.Play();
                        waveO.PlaybackStopped += OnPlaybackStopped_Agent;
                    }
                    catch (Exception ex2)
                    {
                        _log.Error($"[Bridge] Agent fallback also failed: {ex2.Message}");
                    }
                }

                // ── Meter loop (matches original ProcessAudioAndVisualizeIntensity_) ──
                var token = cancellationTokenSource.Token;
                Task.Run(() => ProcessAudioAndVisualizeIntensity_(tmpPath, token));
            }

            return "{\"ok\":true}";
        }

        // ── Meter loop — fires both "agent" and "customer" events ─────────────
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
                    double rms       = CalculateRMS(buffer, samplesRead);
                    float  agentLvl  = (float)Math.Min(1.0, rms * 8.0 * (_agentVol    / 100f));
                    float  custLvl   = (float)Math.Min(1.0, rms * 8.0 * (_customerVol / 100f));

                    OnPlaybackLevel?.Invoke(agentLvl,  "agent");
                    OnPlaybackLevel?.Invoke(custLvl,   "customer");

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
            try { audioFileReader?.Dispose(); audioFileReader = null; waveOut?.Dispose(); } catch { }
        }

        private void OnPlaybackStopped_Agent(object sender, StoppedEventArgs e)
        {
            isAudioPlaying = false;
            try { audioFileReader2?.Dispose(); audioFileReader2 = null; waveO?.Dispose(); } catch { }
            OnPlaybackStopped?.Invoke();
        }

        // ── /volume ───────────────────────────────────────────────────────────
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
                    if (_volCable  != null) _volCable.Volume  = volume / 100f;
                }
                else
                {
                    _agentVol = volume;
                    if (_volAgent != null) _volAgent.Volume = volume / 100f;
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

            try { waveOut?.Stop();        } catch { }
            try { waveOut?.Dispose();     } catch { }
            try { audioFileReader?.Dispose(); } catch { }
            waveOut = null; audioFileReader = null; _volCable = null;

            try { waveO?.Stop();          } catch { }
            try { waveO?.Dispose();       } catch { }
            try { audioFileReader2?.Dispose(); } catch { }
            waveO = null; audioFileReader2 = null; _volAgent = null;
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
