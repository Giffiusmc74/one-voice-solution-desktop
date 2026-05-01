/*
 * LocalBridgeServer.cs  —  v7.71
 * ONE Voice Solution
 *
 * Hosts a tiny HTTP server on localhost:9001 so the Script Dashboard
 * can send real-time play commands to the desktop app.
 *
 * AUDIO ARCHITECTURE:
 *   waveOut      → VB-Audio Cable Input  (customer hears the recording)
 *                  Uses WaveOutEvent with device index — VB Cable is always found by name.
 *   _agentOut    → Agent headset (WasapiOut + MMDevice)  (agent hears the recording)
 *                  Uses the exact MMDevice selected in the headset dropdown.
 *                  No index lookup, no truncation, no VB Cable collision possible.
 *
 *   Red meter source = WasapiLoopbackCapture on Jabra render device.
 *   Captures what the softphone plays out (customer voice).
 *   Card audio also bleeds into loopback via _agentOut — suppressed by
 *   IsCardPlaying flag in MainFormV5 DataAvailable handler.
 *
 * VOLUME — uses AudioFileReader.Volume (software-level float 0.0-1.0).
 *   This is reliable across ALL drivers including Jabra.
 *
 *   /volume { channel:"customer", volume:0-100 } → audioFileReader.Volume
 *   /volume { channel:"agent",    volume:0-100 } → audioFileReader2.Volume
 */
using NAudio.CoreAudioApi;
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

        // ── Audio state ───────────────────────────────────────────────────────
        private WaveOutEvent    waveOut;           // → VB-Cable (customer)
        private AudioFileReader audioFileReader;

        private WasapiOut       _agentOut;         // → Agent headset (WasapiOut, MMDevice-based)
        private AudioFileReader audioFileReader2;

        private readonly object _playLock = new object();
        private bool   isAudioPlaying;

        /// <summary>True while a recording is actively playing.</summary>
        public bool IsPlaying => isAudioPlaying;

        /// <summary>
        /// True while a card recording is actively playing.
        /// Used by MainFormV5 to suppress the red meter during playback.
        /// </summary>
        public bool IsCardPlaying => isAudioPlaying;
        private string _currentTmpPath;

        // ── Device state ──────────────────────────────────────────────────────
        private int       _cableDeviceNumber = -1;   // WaveOut index for VB-Cable
        private MMDevice  _agentMMDevice     = null; // MMDevice for agent headset (set by MainFormV5)

        // ── Per-channel volumes (0-100) ───────────────────────────────────────
        private int _customerVol = 100;
        private int _agentVol    = 100;

        // ── Device setters ────────────────────────────────────────────────────
        public int CableDeviceNumber => _cableDeviceNumber;

        /// <summary>Called by MainFormV5 on startup after VB-Cable is found.</summary>
        public void SetCableDevice(int deviceNumber)
        {
            _cableDeviceNumber = deviceNumber;
            _log.Info($"[Bridge] Cable device → #{deviceNumber}");
        }

        /// <summary>
        /// Called by MainFormV5 on startup/change after agent headset is selected.
        /// Passes the exact MMDevice — no index lookup needed.
        /// </summary>
        public void SetAgentMMDevice(MMDevice device)
        {
            _agentMMDevice = device;
            _log.Info($"[Bridge] Agent MMDevice → '{(device != null ? device.FriendlyName : "null")}'");
        }

        public void SetInitialVolume(string channel, int volume)
        {
            SetVolume(channel, volume);
            _log.Info($"[Bridge] Initial volume {channel} → {volume}%");
        }

        /// <summary>Updates the volume for a channel in real-time.</summary>
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
                else if (channel == "agent")
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

                _currentTmpPath = tmpPath;
                isAudioPlaying  = true;

                _log.Info($"[Bridge] cable=#{_cableDeviceNumber} agentMMDevice='{(_agentMMDevice != null ? _agentMMDevice.FriendlyName : "null")}'");

                // ── VB-Cable output (customer hears recording) ────────────────
                if (_cableDeviceNumber >= 0)
                {
                    try
                    {
                        audioFileReader = new AudioFileReader(tmpPath);
                        audioFileReader.Volume = Math.Max(0f, Math.Min(1f, _customerVol / 100f));

                        // MeteringSampleProvider drives green meter (agent) and blue meter (customer).
                        var metering = new MeteringSampleProvider(audioFileReader);
                        metering.StreamVolume += (s2, e2) =>
                        {
                            float max = 0f;
                            foreach (var ch in e2.MaxSampleValues)
                            {
                                float abs = Math.Abs(ch);
                                if (abs > max) max = abs;
                            }
                            float level = Math.Min(1f, max * 2.0f);
                            OnPlaybackLevel?.Invoke(level, "agent");    // green meter
                            OnPlaybackLevel?.Invoke(level, "customer"); // blue meter
                        };

                        waveOut = new WaveOutEvent { DeviceNumber = _cableDeviceNumber, DesiredLatency = 100 };
                        waveOut.Init(metering);
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
                    _log.Warn("[Bridge] No VB-Cable found — cannot play card. Aborting.");
                    isAudioPlaying = false;
                    DeleteCurrentTmpFile();
                    return "{\"error\":\"VB-Cable not found — card not played\"}";
                }

                // ── Agent headset output (WasapiOut + MMDevice) ───────────────
                // Uses the exact MMDevice the user selected — no index, no truncation,
                // no VB Cable collision possible.
                if (_agentMMDevice != null)
                {
                    try
                    {
                        audioFileReader2 = new AudioFileReader(tmpPath);
                        audioFileReader2.Volume = Math.Max(0f, Math.Min(1f, _agentVol / 100f));

                        _agentOut = new WasapiOut(_agentMMDevice, AudioClientShareMode.Shared, true, 50);
                        _agentOut.Init(audioFileReader2);
                        _agentOut.Play();
                        _agentOut.PlaybackStopped += OnPlaybackStopped_Agent;
                        _log.Info($"[Bridge] Agent WasapiOut → '{_agentMMDevice.FriendlyName}' vol={_agentVol}%");
                    }
                    catch (Exception ex)
                    {
                        // Non-fatal — VB-Cable path is still playing.
                        _log.Warn($"[Bridge] Agent WasapiOut failed (non-fatal): {ex.Message}");
                        try { audioFileReader2?.Dispose(); } catch { }
                        audioFileReader2 = null; _agentOut = null;
                    }
                }
                else
                {
                    _log.Warn("[Bridge] No agent MMDevice set — agent monitor path skipped.");
                }
            }

            return "{\"ok\":true}";
        }

        private void OnPlaybackStopped_Cable(object sender, StoppedEventArgs e)
        {
            lock (_playLock)
            {
                isAudioPlaying = false;

                try { waveOut?.Dispose();        } catch { }
                try { audioFileReader?.Dispose(); } catch { }
                waveOut = null; audioFileReader = null;

                try { _agentOut?.Stop();          } catch { }
                try { _agentOut?.Dispose();       } catch { }
                try { audioFileReader2?.Dispose(); } catch { }
                _agentOut = null; audioFileReader2 = null;
            }
            DeleteCurrentTmpFile();
            OnPlaybackStopped?.Invoke();
        }

        private void OnPlaybackStopped_Agent(object sender, StoppedEventArgs e)
        {
            lock (_playLock)
            {
                try { _agentOut?.Dispose();       } catch { }
                try { audioFileReader2?.Dispose(); } catch { }
                _agentOut = null; audioFileReader2 = null;
            }
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
                else if (channel == "agent")
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

            try { waveOut?.Stop();   } catch { }
            try { waveOut?.Dispose(); } catch { }
            try { audioFileReader?.Dispose(); } catch { }
            waveOut = null; audioFileReader = null;

            try { _agentOut?.Stop();  } catch { }
            try { _agentOut?.Dispose(); } catch { }
            try { audioFileReader2?.Dispose(); } catch { }
            _agentOut = null; audioFileReader2 = null;

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
