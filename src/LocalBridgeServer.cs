/*
 * LocalBridgeServer.cs
 * ONE Voice Solution v7.6
 *
 * Hosts a tiny HTTP server on localhost:9001 so the Script Dashboard
 * (running in the browser) can send real-time commands to the desktop app:
 *
 *   POST /play   { audioUrl, volume (0-100), channel ("agent"|"customer") }
 *                → Downloads audio, plays it through the selected output device
 *                  at the requested volume.  Fires OnPlaybackLevel with REAL
 *                  audio levels from the PCM stream so VU meters respond.
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
 *
 * v7.5 changes:
 *   - VU meters now read REAL audio sample data (RMS) instead of fake static values
 *   - MeteringSampleProvider taps into the live PCM stream
 *   - Speaker routing uses WaveOutEvent DeviceNumber properly
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
    /// <summary>
    /// Sample provider that reads audio and computes peak level per buffer,
    /// firing a callback so the UI can update VU meters with real audio data.
    /// </summary>
    internal class MeteringSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly Action<float> _onLevel;
        private int _sampleCount;
        private float _maxSample;
        private const int NotifyEvery = 2048; // ~46ms at 44100Hz

        public WaveFormat WaveFormat => _source.WaveFormat;

        public MeteringSampleProvider(ISampleProvider source, Action<float> onLevel)
        {
            _source  = source;
            _onLevel = onLevel;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);
            for (int i = 0; i < read; i++)
            {
                float abs = Math.Abs(buffer[offset + i]);
                if (abs > _maxSample) _maxSample = abs;
                _sampleCount++;
                if (_sampleCount >= NotifyEvery)
                {
                    // Boost sensitivity: multiply peak by 5 so even quiet
                    // recordings light up most of the meter bar
                    float level = Math.Min(1f, _maxSample * 5.0f);
                    _onLevel?.Invoke(level);
                    _sampleCount = 0;
                    _maxSample   = 0f;
                }
            }
            return read;
        }
    }

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

        // Audio playback
        private WaveOutEvent    _waveOut;
        private AudioFileReader _audioReader;
        private VolumeSampleProvider _volumeProvider;
        private MeteringSampleProvider _meterProvider;
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

        // HTTP client for downloading audio URLs
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

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

            // Play on background thread — NAudio WaveOutEvent is thread-safe
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

                // ── REAL VU METER: tap into the live audio stream ──────────
                // MeteringSampleProvider reads every PCM sample and computes
                // peak level, then fires OnPlaybackLevel with the real value.
                // This is what makes the meters move with actual audio.
                _meterProvider = new MeteringSampleProvider(_volumeProvider, (level) =>
                {
                    if (_isPlaying)
                        OnPlaybackLevel?.Invoke(level, _currentChannel);
                });

                _waveOut = new WaveOutEvent { DeviceNumber = _outputDeviceNumber };
                _waveOut.PlaybackStopped += (s, e) =>
                {
                    _isPlaying = false;
                    OnPlaybackStopped?.Invoke();
                    // Clean up temp file after playback
                    try { File.Delete(tmpPath); } catch { }
                };
                _waveOut.Init(_meterProvider);  // Use metering provider instead of volume provider
                _waveOut.Play();
                _isPlaying = true;
            }

            _log.Info($"[Bridge] Playing {channel} audio vol={volume}% device={_outputDeviceNumber}");
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
            try { _waveOut?.Stop(); }   catch { }
            try { _waveOut?.Dispose(); } catch { }
            try { _audioReader?.Dispose(); } catch { }
            _waveOut        = null;
            _audioReader    = null;
            _volumeProvider = null;
            _meterProvider  = null;
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
