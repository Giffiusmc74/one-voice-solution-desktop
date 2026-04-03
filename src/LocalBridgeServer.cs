/*
 * LocalBridgeServer.cs
 * ONE Voice Solution v7.8
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
 *   - _waveOutCable  → always plays through VB-Audio Cable (for softphone)
 *   - _waveOutSpeaker → plays through the selected headset/speaker device
 *   - Both play the same file simultaneously (two AudioFileReader instances)
 *   - SetOutputDevice(deviceNumber) sets the speaker device number
 *
 * VOLUME:
 *   - VolumeSampleProvider on each WaveOut controls volume independently
 *   - /volume endpoint adjusts in real-time without stopping playback
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

        // Audio playback — two WaveOut instances (cable + speaker)
        private WaveOutEvent         _waveOutCable;
        private WaveOutEvent         _waveOutSpeaker;
        private AudioFileReader      _readerCable;
        private AudioFileReader      _readerSpeaker;
        private VolumeSampleProvider _volCable;
        private VolumeSampleProvider _volSpeaker;
        private readonly object      _playLock = new object();
        private bool                 _isPlaying;
        private string               _currentChannel = "agent";
        private int                  _currentVolume  = 80;
        private int                  _outputDeviceNumber = -1; // -1 = default device

        // Meter task cancellation
        private CancellationTokenSource _meterCts;

        // HTTP client for downloading audio URLs
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>Set the WaveOut device number for the speaker output.</summary>
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

                // ── WaveOut #1: VB-Audio Cable (for softphone routing) ─────────
                // Find cable device number
                int cableDevice = FindWaveOutByName("cable");
                _readerCable = new AudioFileReader(tmpPath);
                _volCable    = new VolumeSampleProvider(_readerCable) { Volume = vol };
                _waveOutCable = new WaveOutEvent { DeviceNumber = cableDevice, DesiredLatency = 100 };
                _waveOutCable.Init(_volCable);
                _waveOutCable.PlaybackStopped += (s, e) =>
                {
                    _isPlaying = false;
                    OnPlaybackStopped?.Invoke();
                    try { File.Delete(tmpPath); } catch { }
                };
                _waveOutCable.Play();

                // ── WaveOut #2: Selected headset/speaker (for agent monitoring) ─
                if (_outputDeviceNumber >= -1)
                {
                    try
                    {
                        _readerSpeaker = new AudioFileReader(tmpPath);
                        _volSpeaker    = new VolumeSampleProvider(_readerSpeaker) { Volume = vol };
                        _waveOutSpeaker = new WaveOutEvent { DeviceNumber = _outputDeviceNumber, DesiredLatency = 100 };
                        _waveOutSpeaker.Init(_volSpeaker);
                        _waveOutSpeaker.Play();
                    }
                    catch (Exception ex)
                    {
                        _log.Warn($"[Bridge] Speaker WaveOut failed: {ex.Message}");
                        try { _waveOutSpeaker?.Dispose(); } catch { }
                        try { _readerSpeaker?.Dispose(); } catch { }
                        _waveOutSpeaker = null;
                        _readerSpeaker  = null;
                    }
                }

                _isPlaying = true;

                // ── METER: background Task reads file chunks, calculates RMS ──
                // This is the same approach as the original MainForm.cs that worked.
                // Reads 4096 samples at a time, calculates RMS, fires OnPlaybackLevel.
                // Completely independent of WaveOut — meters always respond.
                _meterCts = new CancellationTokenSource();
                var meterToken = _meterCts.Token;
                var meterChannel = channel;
                Task.Run(() => RunMeterLoop(tmpPath, meterChannel, meterToken));
            }

            _log.Info($"[Bridge] Playing {channel} audio vol={volume}% cable={FindWaveOutByName("cable")} speaker={_outputDeviceNumber}");
            return "{\"ok\":true,\"channel\":\"" + channel + "\"}";
        }

        // ── Meter loop (original approach from MainForm.cs) ───────────────────
        private void RunMeterLoop(string filePath, string channel, CancellationToken ct)
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
                        float level = (float)Math.Min(1.0, rms * 8.0);

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

        // ── Find WaveOut device by name substring ─────────────────────────────
        private static int FindWaveOutByName(string nameHint)
        {
            if (string.IsNullOrEmpty(nameHint)) return -1;
            string hint = nameHint.ToLower();
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                string prod = WaveOut.GetCapabilities(i).ProductName.ToLower();
                if (prod.Contains(hint) || hint.Contains(prod.Substring(0, Math.Min(prod.Length, 20))))
                    return i;
            }
            return -1;
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
                if (_volCable   != null) _volCable.Volume   = vol;
                if (_volSpeaker != null) _volSpeaker.Volume = vol;
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
            _meterCts?.Cancel();
            _meterCts = null;

            try { _waveOutCable?.Stop();    } catch { }
            try { _waveOutCable?.Dispose(); } catch { }
            try { _readerCable?.Dispose();  } catch { }
            try { _waveOutSpeaker?.Stop();    } catch { }
            try { _waveOutSpeaker?.Dispose(); } catch { }
            try { _readerSpeaker?.Dispose();  } catch { }

            _waveOutCable   = null;
            _readerCable    = null;
            _volCable       = null;
            _waveOutSpeaker = null;
            _readerSpeaker  = null;
            _volSpeaker     = null;
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
