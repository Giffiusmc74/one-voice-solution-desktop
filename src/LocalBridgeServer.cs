/*
 * LocalBridgeServer.cs
 * ONE Voice Solution v7.9
 *
 * Hosts a tiny HTTP server on localhost:9001 so the Script Dashboard
 * (running in the browser) can send real-time commands to the desktop app.
 *
 * METER APPROACH (matches original MainForm.cs that worked):
 *   - Background Task.Run loop reads the audio file in 4096-sample chunks
 *   - Calculates RMS per chunk, fires OnPlaybackLevel every 75ms
 *   - Completely independent of WaveOut — meters always respond
 *
 * DEVICE ROUTING:
 *   - Single WaveOutEvent plays through the selected headset/speaker device
 *   - SetOutputDevice(deviceNumber) sets the device number
 *   - VB-Audio Cable routing is handled at the Windows/softphone level — NOT here
 *
 * VOLUME:
 *   - VolumeSampleProvider controls volume in real-time
 *   - /volume endpoint adjusts without stopping playback
 *   - Volume also applied at meter level so meters scale with volume
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
        /// <summary>Fired with real audio level (0..1) from the playback stream.</summary>
        public event Action<float, string> OnPlaybackLevel;   // (level, channel)
        /// <summary>Fired when playback stops.</summary>
        public event Action OnPlaybackStopped;

        // ── State ─────────────────────────────────────────────────────────────
        private readonly Logger _log = LogManager.GetCurrentClassLogger();
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private bool _disposed;

        // Audio playback — single WaveOut to selected device
        private WaveOutEvent         _waveOut;
        private AudioFileReader      _reader;
        private VolumeSampleProvider _volProvider;
        private readonly object      _playLock = new object();
        private bool                 _isPlaying;
        private string               _currentChannel = "agent";
        private int                  _currentVolume  = 80;
        private int                  _outputDeviceNumber = -1; // -1 = default device

        // Meter task cancellation
        private CancellationTokenSource _meterCts;

        // HTTP client for downloading audio URLs
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>Set the WaveOut device number for playback output.</summary>
        public void SetOutputDevice(int deviceNumber)
        {
            _outputDeviceNumber = deviceNumber;
            _log.Info($"[Bridge] Output device set to #{deviceNumber}");
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
                _log.Warn($"[Bridge] Could not start HTTP listener: {ex.Message}");
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

            // CORS — allow browser on any localhost origin
            resp.Headers.Add("Access-Control-Allow-Origin", "*");
            resp.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (req.HttpMethod == "OPTIONS")
            {
                resp.StatusCode = 204;
                resp.Close();
                return;
            }

            string path = req.Url.AbsolutePath.ToLowerInvariant().TrimEnd('/');
            string body = "";
            if (req.HasEntityBody)
            {
                using (var sr = new StreamReader(req.InputStream, req.ContentEncoding))
                    body = await sr.ReadToEndAsync();
            }

            try
            {
                string json;
                switch (path)
                {
                    case "/play":
                        json = await HandlePlay(body);
                        break;
                    case "/stop":
                        StopAudio();
                        json = "{\"ok\":true}";
                        break;
                    case "/volume":
                        json = HandleVolume(body);
                        break;
                    case "/status":
                        json = JsonConvert.SerializeObject(new
                        {
                            playing = _isPlaying,
                            channel = _currentChannel,
                            volume  = _currentVolume
                        });
                        break;
                    default:
                        resp.StatusCode = 404;
                        json = "{\"error\":\"Not found\"}";
                        break;
                }
                byte[] buf = Encoding.UTF8.GetBytes(json);
                resp.ContentType     = "application/json";
                resp.ContentLength64 = buf.Length;
                resp.StatusCode      = 200;
                await resp.OutputStream.WriteAsync(buf, 0, buf.Length);
            }
            catch (Exception ex)
            {
                _log.Warn($"[Bridge] Handler error ({path}): {ex.Message}");
                byte[] err = Encoding.UTF8.GetBytes("{\"error\":\"" + ex.Message.Replace("\"", "'") + "\"}");
                resp.StatusCode      = 500;
                resp.ContentLength64 = err.Length;
                await resp.OutputStream.WriteAsync(err, 0, err.Length);
            }
            finally { resp.Close(); }
        }

        // ── /play ─────────────────────────────────────────────────────────────
        private async Task<string> HandlePlay(string body)
        {
            dynamic data    = JsonConvert.DeserializeObject(body);
            string audioUrl = (string)data?.audioUrl;
            int    volume   = (int?)data?.volume ?? _currentVolume;
            string channel  = (string)data?.channel ?? "agent";

            if (string.IsNullOrWhiteSpace(audioUrl))
                return "{\"error\":\"audioUrl is required\"}";

            // Download audio to a temp file
            string tmpPath = Path.Combine(Path.GetTempPath(), "ov_script_" + Guid.NewGuid().ToString("N") + ".mp3");
            byte[] bytes   = await _http.GetByteArrayAsync(audioUrl);
            File.WriteAllBytes(tmpPath, bytes);

            lock (_playLock)
            {
                StopAudioInternal();
                _currentChannel = channel;
                _currentVolume  = Math.Max(0, Math.Min(100, volume));
                float vol = _currentVolume / 100f;

                // ── Single WaveOut: plays through the selected headset/speaker ──
                // Device number is set by the dropdown in MainFormV5 via SetOutputDevice()
                // -1 = Windows default device
                try
                {
                    _reader      = new AudioFileReader(tmpPath);
                    _volProvider = new VolumeSampleProvider(_reader) { Volume = vol };
                    _waveOut     = new WaveOutEvent { DeviceNumber = _outputDeviceNumber, DesiredLatency = 100 };
                    _waveOut.Init(_volProvider);
                    _waveOut.PlaybackStopped += (s, e) =>
                    {
                        _isPlaying = false;
                        OnPlaybackStopped?.Invoke();
                        try { File.Delete(tmpPath); } catch { }
                    };
                    _waveOut.Play();
                    _isPlaying = true;
                }
                catch (Exception ex)
                {
                    _log.Warn($"[Bridge] WaveOut failed (device #{_outputDeviceNumber}): {ex.Message}");
                    // Fall back to default device
                    try
                    {
                        _reader?.Dispose();
                        _reader      = new AudioFileReader(tmpPath);
                        _volProvider = new VolumeSampleProvider(_reader) { Volume = vol };
                        _waveOut     = new WaveOutEvent { DeviceNumber = -1, DesiredLatency = 100 };
                        _waveOut.Init(_volProvider);
                        _waveOut.PlaybackStopped += (s, e) =>
                        {
                            _isPlaying = false;
                            OnPlaybackStopped?.Invoke();
                            try { File.Delete(tmpPath); } catch { }
                        };
                        _waveOut.Play();
                        _isPlaying = true;
                    }
                    catch (Exception ex2)
                    {
                        _log.Error($"[Bridge] Fallback WaveOut also failed: {ex2.Message}");
                        return "{\"error\":\"Playback failed: " + ex2.Message.Replace("\"", "'") + "\"}";
                    }
                }

                // ── METER: background Task reads file chunks, calculates RMS ──
                // Reads 4096 samples at a time, calculates RMS, fires OnPlaybackLevel every 75ms.
                // Completely independent of WaveOut — meters always respond.
                _meterCts = new CancellationTokenSource();
                var meterToken   = _meterCts.Token;
                var meterChannel = channel;
                var meterVol     = vol;
                Task.Run(() => RunMeterLoop(tmpPath, meterChannel, meterVol, meterToken));
            }

            _log.Info($"[Bridge] Playing {channel} audio vol={volume}% device={_outputDeviceNumber}");
            return "{\"ok\":true,\"channel\":\"" + channel + "\"}";
        }

        // ── Meter loop (original proven approach from MainForm.cs) ─────────────
        private void RunMeterLoop(string filePath, string channel, float initialVol, CancellationToken ct)
        {
            try
            {
                const int bufferSize = 4096;
                using (var reader = new AudioFileReader(filePath))
                {
                    var buffer = new float[bufferSize];
                    int samplesRead;
                    while ((samplesRead = reader.Read(buffer, 0, buffer.Length)) > 0
                           && !ct.IsCancellationRequested
                           && _isPlaying)
                    {
                        // Calculate RMS of this chunk
                        double sumSq = 0.0;
                        for (int i = 0; i < samplesRead; i++)
                            sumSq += buffer[i] * buffer[i];
                        double rms = Math.Sqrt(sumSq / samplesRead);

                        // Scale to 0..1, boost so quiet recordings still show on meter
                        // Also apply current volume so meter reflects actual output level
                        float vol   = _currentVolume / 100f;
                        float level = (float)Math.Min(1.0, rms * 8.0 * vol);

                        OnPlaybackLevel?.Invoke(level, channel);

                        // 75ms delay matches original code — keeps meter smooth
                        Thread.Sleep(75);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"[Bridge] Meter loop error: {ex.Message}");
            }
        }

        // ── /volume ───────────────────────────────────────────────────────────
        private string HandleVolume(string body)
        {
            dynamic data   = JsonConvert.DeserializeObject(body);
            int     volume = (int?)data?.volume ?? _currentVolume;
            _currentVolume = Math.Max(0, Math.Min(100, volume));
            float vol = _currentVolume / 100f;
            lock (_playLock)
            {
                if (_volProvider != null) _volProvider.Volume = vol;
            }
            _log.Info($"[Bridge] Volume set to {_currentVolume}%");
            return "{\"ok\":true,\"volume\":" + _currentVolume + "}";
        }

        // ── Audio stop ────────────────────────────────────────────────────────
        public void StopAudio()
        {
            lock (_playLock) { StopAudioInternal(); }
            OnPlaybackStopped?.Invoke();
        }

        private void StopAudioInternal()
        {
            _isPlaying = false;
            _meterCts?.Cancel();
            _meterCts = null;

            try { _waveOut?.Stop();    } catch { }
            try { _waveOut?.Dispose(); } catch { }
            try { _reader?.Dispose();  } catch { }

            _waveOut     = null;
            _reader      = null;
            _volProvider = null;
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
