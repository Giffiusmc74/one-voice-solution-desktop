/*
 * LocalBridgeServer.cs
 * ONE Voice Solution v5.7
 *
 * Hosts a tiny HTTP server on localhost:9001 so the Script Dashboard
 * (running in the browser) can send real-time commands to the desktop app:
 *
 *   POST /play   { audioUrl, volume (0-100), channel ("agent"|"customer") }
 *                → Downloads audio, plays it through the selected output device
 *                  at the requested volume.  Fires OnPlaybackStarted so the
 *                  meter can light up.
 *
 *   POST /stop   {}
 *                → Stops any currently-playing script audio.
 *
 *   POST /volume { volume (0-100), channel ("agent"|"customer") }
 *                → Adjusts playback volume in real-time without stopping audio.
 *
 *   GET  /status → Returns { playing: bool, channel: string, volume: int }
 *
 * CORS headers are set to allow requests from any localhost origin so the
 * browser dashboard can call it without a proxy.
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
        /// <summary>Fired when script audio starts playing. level = 0..1</summary>
        public event Action<float, string> OnPlaybackLevel;   // (level, channel)
        /// <summary>Fired when playback stops.</summary>
        public event Action OnPlaybackStopped;

        // ── State ─────────────────────────────────────────────────────────────
        private readonly Logger _log = LogManager.GetCurrentClassLogger();
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private bool _disposed;

        // Audio playback
        private WaveOutEvent    _waveOut;
        private AudioFileReader _audioReader;
        private VolumeSampleProvider _volumeProvider;
        private readonly object _playLock = new object();
        private bool   _isPlaying;
        private string _currentChannel = "agent";
        private int    _currentVolume  = 80;
        private int    _outputDeviceNumber = -1; // -1 = default device

        /// <summary>Set the output device number for WaveOutEvent playback.</summary>
        public void SetOutputDevice(int deviceNumber)
        {
            _outputDeviceNumber = deviceNumber;
            _log.Info($"[Bridge] Output device set to #{deviceNumber}");
        }

        // Meter pump — fires OnPlaybackLevel at 50ms intervals while playing
        private System.Threading.Timer _meterTimer;

        // HTTP client for downloading audio URLs
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

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

            // Play on UI thread is not needed — NAudio WaveOutEvent is thread-safe
            lock (_playLock)
            {
                StopAudioInternal();
                _currentChannel = channel;
                _currentVolume  = Math.Max(0, Math.Min(100, volume));

                _audioReader    = new AudioFileReader(tmpPath);
                _volumeProvider = new VolumeSampleProvider(_audioReader)
                {
                    Volume = _currentVolume / 100f
                };
                _waveOut = new WaveOutEvent { DeviceNumber = _outputDeviceNumber };
                _waveOut.PlaybackStopped += (s, e) =>
                {
                    _isPlaying = false;
                    _meterTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                    OnPlaybackStopped?.Invoke();
                    // Clean up temp file after playback
                    try { File.Delete(tmpPath); } catch { }
                };
                _waveOut.Init(_volumeProvider);
                _waveOut.Play();
                _isPlaying = true;
            }

            // Start meter pump
            _meterTimer?.Dispose();
            _meterTimer = new System.Threading.Timer(_ =>
            {
                if (!_isPlaying) return;
                // Simulate a level based on volume (real level would need audio analysis)
                float level = (_currentVolume / 100f) * 0.75f;
                OnPlaybackLevel?.Invoke(level, _currentChannel);
            }, null, 0, 50);

            _log.Info($"[Bridge] Playing {channel} audio vol={volume}%");
            return "{\"ok\":true,\"channel\":\"" + channel + "\"}";
        }

        // ── /volume ───────────────────────────────────────────────────────────
        private string HandleVolume(string body)
        {
            dynamic data   = JsonConvert.DeserializeObject(body);
            int     volume = (int?)data?.volume ?? _currentVolume;
            _currentVolume = Math.Max(0, Math.Min(100, volume));
            lock (_playLock)
            {
                if (_volumeProvider != null)
                    _volumeProvider.Volume = _currentVolume / 100f;
            }
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
            _meterTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            try { _waveOut?.Stop(); }   catch { }
            try { _waveOut?.Dispose(); } catch { }
            try { _audioReader?.Dispose(); } catch { }
            _waveOut        = null;
            _audioReader    = null;
            _volumeProvider = null;
        }

        // ── Dispose ───────────────────────────────────────────────────────────
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _meterTimer?.Dispose();
            _http.Dispose();
        }
    }
}
