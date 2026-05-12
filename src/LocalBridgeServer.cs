/*
 * LocalBridgeServer.cs  —  v7.90 (/prefetch disk cache + non-blocking meter prep)
 * ONE Voice Solution
 *
 * Hosts a tiny HTTP server on localhost:9001 so the Script Dashboard
 * can send real-time play commands to the desktop app.
 *
 * AUDIO ARCHITECTURE:
 *   waveOut      → VB-Audio Cable Input  (customer hears the recording)
 *   agent player → WasapiOut(MMDevice headset) when set, else WaveOut index fallback
 *   POST /prefetch body: audioUrl — warms on-disk cache so a later POST /play skips network download when URL matches.
 *     channel in JSON is ignored — both outputs play when devices exist.
 *   Temp file extension follows URL or Content-Type (e.g. .webm) so Media Foundation decodes correctly.
 *   Agent path: WasapiOut(MMDevice); on COM/init failure → WaveOutEvent.DeviceNumber fallback.
 *   Single meter loop fires OnPlaybackLevel for both meters.
 *
 * VOLUME — AudioFileReader.Volume (software) on both decode chains.
 *   /volume { channel:"agent",    volume:0-100 } → headset reader (steeper pow curve)
 *   /volume { channel:"customer", volume:0-100 } → VB-Cable reader (gentler curve + small calibration vs headset)
 *   Live mic RMS from MainForm updates script level-match gain (same target as AudioService.PlayAudio)
 */
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
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
        public event Action OnPlaybackStarted;
        public event Action OnPlaybackStopped;

        // ── Logging ───────────────────────────────────────────────────────────
        private readonly Logger _log = LogManager.GetCurrentClassLogger();

        // ── HTTP server ───────────────────────────────────────────────────────
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private bool _disposed;
        // User-Agent must look like a real browser — CloudFront WAF managed rules (AWSManagedRulesCommonRuleSet)
        // reject requests with no User-Agent or .NET default agents (403 Forbidden).
        // Using a standard Chrome UA satisfies AWS bot detection without disabling WAF on the distribution.
        private static readonly HttpClient _http = BuildHttpClient();
        private static HttpClient BuildHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            return client;
        }

        // ── Audio state ───────────────────────────────────────────────────────
        private WaveOutEvent    waveOut;           // → VB-Cable (customer)
        private AudioFileReader audioFileReader;

        private IWavePlayer     waveAgentOut;      // WasapiOut or WaveOut → headset (agent)
        private AudioFileReader audioFileReader2;

        /// <summary>Headset/speaker MMDevice from MainFormV5 — avoids WaveOut index mismatch with WASAPI.</summary>
        private MMDevice _agentRenderDevice;

        private readonly object _playLock = new object();
        private readonly object _cacheOpsLock = new object();
        private readonly string _audioCacheDir = Path.Combine(Path.GetTempPath(), "OneVoiceBridgeAudioCache");
        private const long MaxAudioCacheBytes = 350L * 1024 * 1024;
        private const int MaxAudioCacheFiles = 48;
        /// <summary>Seen by playback callbacks + meter Task — must be volatile for correct visibility across threads.</summary>
        private volatile bool isAudioPlaying;

        /// <summary>True while a recording is actively playing. Used by MainFormV5 to suppress loopback meter during playback.</summary>
        public bool IsPlaying => isAudioPlaying;
        private CancellationTokenSource cancellationTokenSource;
        private string _currentTmpPath;   // temp file for current playback — deleted on stop
        /// <summary>True if current /play session started agent headset output. False = cable-only (agent init failed).</summary>
        private bool _playbackHadAgentOutput;

        // ── Device numbers ────────────────────────────────────────────────────
        private int outputDeviceNumber = -1;   // headset (agent) — set by dropdown
        private int _cableDeviceNumber  = -1;  // VB-Cable — found by name on startup

        // ── Per-channel volumes (0-100) ───────────────────────────────────────
        // Applied via WaveOutEvent.Volume — hardware-level, no sample processing.
        private int _agentVol    = 100;
        private int _customerVol = 100;

        // Same idea as AudioService.PlayAudio: scale script decode gain toward the agent's live speech level (-18 dBFS target).
        private volatile float _scriptLevelMatchGain = 1.0f;
        private const float TargetSpeechRmsLinear  = 0.126f;
        private const float MinScriptLevelMatchGain = 0.5f;
        private const float MaxScriptLevelMatchGain = 2.0f;

        /// <summary>Optional lift for VB-Cable path only — keep modest to avoid customer-side clipping/robotic breakup.</summary>
        private const float CustomerCableCalibrationGain = 1.16f;
        /// <summary>
        /// WasapiOut to physical headset is often much quieter than VB-Cable WaveOut at the same
        /// AudioFileReader.Volume — lift agent-ear path so it matches customer path loudness at a given slider %.
        /// </summary>
        private const float AgentHeadsetCalibrationGain = 1.52f;
        /// <summary>Global script playback loudness lift for agent/headset and customer/VB paths.</summary>
        private const float AgentPlaybackBoost = 1.35f;
        private const float CustomerPlaybackBoost = 1.10f;
        /// <summary>
        /// Keep separate caps: agent ear can be hotter; customer path should stay conservative
        /// to avoid codec/AGC artifacts heard as intermittent "broken/garbled" playback.
        /// </summary>
        private const float MaxAgentPlaybackLinear = 1.6f;
        private const float MaxCustomerPlaybackLinear = 1.08f;
        /// <summary>Do not let live-mic level matching over-drive the customer path.</summary>
        private const float MaxCustomerLevelMatchGain = 1.15f;

        // ── Device setters ────────────────────────────────────────────────────
        /// <summary>Called by MainFormV5 when the headset dropdown changes.</summary>
        public void SetOutputDevice(int deviceNumber)
        {
            outputDeviceNumber = deviceNumber;
            _log.Info($"[Bridge] Agent WaveOut index (fallback) → #{deviceNumber}");
        }

        /// <summary>Binds script playback for the agent ear to this render device (headset). Preferred over WaveOut index.</summary>
        public void SetAgentRenderDevice(MMDevice device)
        {
            lock (_playLock)
            {
                _agentRenderDevice = device;
                _log.Info($"[Bridge] Agent MMDevice → {(device?.FriendlyName ?? "(null)")}");
            }
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

        /// <summary>
        /// Shared base curve for script playback sliders.
        /// Agent/customer remain separately controllable via per-path multipliers.
        /// </summary>
        private const float SharedPlaybackCurveExponent = 1.45f;

        /// <summary>Headset path base curve.</summary>
        private static float SliderToPlaybackGainAgent(int pct)
        {
            pct = Math.Max(0, Math.Min(100, pct));
            if (pct <= 0) return 0f;
            return (float)Math.Pow(pct / 100.0, SharedPlaybackCurveExponent);
        }

        /// <summary>VB-Cable / caller path base curve (matches agent curve).</summary>
        private static float SliderToPlaybackGainCustomer(int pct)
        {
            pct = Math.Max(0, Math.Min(100, pct));
            if (pct <= 0) return 0f;
            return (float)Math.Pow(pct / 100.0, SharedPlaybackCurveExponent);
        }

        /// <summary>Updated from live mic RMS (MainForm WasapiCapture) — mirrors AudioService level matching.</summary>
        public void SetScriptLevelMatchGain(float gain)
        {
            if (float.IsNaN(gain) || float.IsInfinity(gain)) return;
            gain = Math.Max(MinScriptLevelMatchGain, Math.Min(MaxScriptLevelMatchGain, gain));
            _scriptLevelMatchGain = gain;
            lock (_playLock)
            {
                try
                {
                    if (audioFileReader != null) audioFileReader.Volume = EffectiveCustomerPlaybackLinear(_customerVol);
                    if (audioFileReader2 != null) audioFileReader2.Volume = EffectiveAgentPlaybackLinear(_agentVol);
                }
                catch { /* reader may be in flux */ }
            }
        }

        private float EffectiveAgentPlaybackLinear(int pctVol)
            => Math.Min(MaxAgentPlaybackLinear, SliderToPlaybackGainAgent(pctVol) * _scriptLevelMatchGain * AgentPlaybackBoost * AgentHeadsetCalibrationGain);

        private float EffectiveCustomerPlaybackLinear(int pctVol)
        {
            float customerMatch = Math.Min(_scriptLevelMatchGain, MaxCustomerLevelMatchGain);
            return Math.Min(MaxCustomerPlaybackLinear, SliderToPlaybackGainCustomer(pctVol) * customerMatch * CustomerCableCalibrationGain * CustomerPlaybackBoost);
        }

        /// <summary>Allows MainForm/AEC to derive match gain from smoothed RMS the same way as AudioService.</summary>
        public static float LevelMatchGainFromMicRms(float smoothedMicRmsLinear)
        {
            if (smoothedMicRmsLinear <= 0.001f || TargetSpeechRmsLinear <= 0.0001f) return 1f;
            float g = smoothedMicRmsLinear / TargetSpeechRmsLinear;
            return Math.Max(MinScriptLevelMatchGain, Math.Min(MaxScriptLevelMatchGain, g));
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
                    if (audioFileReader != null) audioFileReader.Volume = EffectiveCustomerPlaybackLinear(volume);
                }
                else
                {
                    _agentVol = volume;
                    if (audioFileReader2 != null) audioFileReader2.Volume = EffectiveAgentPlaybackLinear(volume);
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
                try { Directory.CreateDirectory(_audioCacheDir); } catch { }
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
            // CORS + Private Network Access (Chrome): https pages on onevoicesolution.com
            // may call http://localhost:9001 only if Allow-Private-Network is returned on preflight.
            AddBridgeCorsHeaders(resp);

            if (req.HttpMethod == "OPTIONS") { resp.StatusCode = 204; resp.Close(); return; }

            resp.ContentType = "application/json";

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
                    case "/play":     _log.Info($"[Bridge] HTTP POST /play from {req.RemoteEndPoint} bodyChars={body?.Length ?? 0}"); json = await HandlePlay(body); break;
                    case "/prefetch": _log.Info($"[Bridge] HTTP POST /prefetch from {req.RemoteEndPoint}"); json = HandlePrefetch(body); break;
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

        /// <summary>
        /// Allows the member portal (HTTPS) to POST to this localhost bridge (Chrome Private Network Access).
        /// Without Access-Control-Allow-Private-Network, /status and /play never run — browser falls back to
        /// HTML Audio on VB-Cable only (RED meter moves, BLUE/GREEN stay dead).
        /// </summary>
        private static void AddBridgeCorsHeaders(HttpListenerResponse resp)
        {
            resp.Headers.Set("Access-Control-Allow-Origin", "*");
            resp.Headers.Set("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            resp.Headers.Set("Access-Control-Allow-Headers", "Content-Type, Access-Control-Request-Private-Network");
            resp.Headers.Set("Access-Control-Allow-Private-Network", "true");
        }

        private static string Clip(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        /// <summary>Extensions MF/AudioFileReader handles reliably; avoids naming WebM bytes ".mp3".</summary>
        private static readonly HashSet<string> _allowedBridgeExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".webm", ".ogg", ".oga", ".m4a", ".aac", ".wma", ".flac", ".opus", ".mp4", ".mkv",
        };

        private static string ResolveBridgeTempExtension(string audioUrl, string contentType)
        {
            try
            {
                var uri = new Uri(audioUrl);
                string path = uri.AbsolutePath;
                int dot = path.LastIndexOf('.');
                if (dot >= 0)
                {
                    string ext = path.Substring(dot);
                    if (ext.Length >= 2 && ext.Length <= 8 && _allowedBridgeExtensions.Contains(ext))
                        return ext;
                }
            }
            catch { /* ignore bad URLs */ }

            if (!string.IsNullOrEmpty(contentType))
            {
                string ct = contentType.ToLowerInvariant();
                if (ct.Contains("webm")) return ".webm";
                if (ct.Contains("ogg")) return ".ogg";
                if (ct.Contains("wav")) return ".wav";
                if (ct.Contains("mp4") || ct.Contains("m4a") || ct.Contains("mp4a")) return ".m4a";
                if (ct.Contains("mpeg") || ct.Contains("mp3")) return ".mp3";
                if (ct.Contains("flac")) return ".flac";
                if (ct.Contains("aac")) return ".aac";
            }

            return ".mp3";
        }

        private static string UrlHashHex(string audioUrl)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes((audioUrl ?? "").Trim()));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    sb.AppendFormat("{0:x2}", b);
                return sb.ToString();
            }
        }

        private void EnsureCacheDir()
        {
            try { Directory.CreateDirectory(_audioCacheDir); } catch { }
        }

        private bool TryGetCachedFilePath(string audioUrl, out string fullPath)
        {
            fullPath = null;
            if (string.IsNullOrWhiteSpace(audioUrl)) return false;
            EnsureCacheDir();
            string hash = UrlHashHex(audioUrl);
            foreach (string ext in _allowedBridgeExtensions)
            {
                string p = Path.Combine(_audioCacheDir, hash + ext);
                try
                {
                    if (File.Exists(p) && new FileInfo(p).Length > 0)
                    {
                        fullPath = p;
                        return true;
                    }
                }
                catch { /* ignore */ }
            }
            return false;
        }

        private static void TouchCacheFile(string path)
        {
            try { File.SetLastWriteTimeUtc(path, DateTime.UtcNow); } catch { }
        }

        private void EnforceCacheLimits()
        {
            try
            {
                if (!Directory.Exists(_audioCacheDir)) return;
                var files = new DirectoryInfo(_audioCacheDir).GetFiles()
                    .OrderBy(f => f.LastWriteTimeUtc)
                    .ToList();
                long total = 0;
                foreach (var f in files)
                    total += f.Length;
                while (files.Count > 0 && (total > MaxAudioCacheBytes || files.Count > MaxAudioCacheFiles))
                {
                    FileInfo victim = files[0];
                    files.RemoveAt(0);
                    total -= victim.Length;
                    try { victim.Delete(); } catch { }
                }
            }
            catch { /* best-effort */ }
        }

        /// <summary>Writes bytes to cache path hash+ext; caller should hold _cacheOpsLock when needed.</summary>
        private void WriteCacheEntryUnlocked(string audioUrl, byte[] bytes, string ext)
        {
            if (bytes == null || bytes.Length == 0) return;
            EnsureCacheDir();
            string hash = UrlHashHex(audioUrl);
            if (!_allowedBridgeExtensions.Contains(ext))
                ext = ".mp3";
            foreach (string e in _allowedBridgeExtensions)
            {
                if (e == ext) continue;
                string old = Path.Combine(_audioCacheDir, hash + e);
                try { if (File.Exists(old)) File.Delete(old); } catch { }
            }
            string dest = Path.Combine(_audioCacheDir, hash + ext);
            File.WriteAllBytes(dest, bytes);
            _log.Info($"[Bridge] Cache stored {Clip(dest, 120)} bytes={bytes.Length}");
            EnforceCacheLimits();
        }

        private async Task EnsureUrlCachedAsync(string audioUrl)
        {
            if (string.IsNullOrWhiteSpace(audioUrl)) return;
            try
            {
                if (TryGetCachedFilePath(audioUrl, out string hit))
                {
                    TouchCacheFile(hit);
                    return;
                }
                lock (_cacheOpsLock)
                {
                    if (TryGetCachedFilePath(audioUrl, out hit))
                    {
                        TouchCacheFile(hit);
                        return;
                    }
                }

                string responseContentType = null;
                byte[] bytes;
                using (var resp = await _http.GetAsync(audioUrl))
                {
                    if (resp.Content.Headers.ContentType != null)
                        responseContentType = resp.Content.Headers.ContentType.MediaType;
                    resp.EnsureSuccessStatusCode();
                    bytes = await resp.Content.ReadAsByteArrayAsync();
                }
                string ext = ResolveBridgeTempExtension(audioUrl, responseContentType);
                lock (_cacheOpsLock)
                {
                    if (TryGetCachedFilePath(audioUrl, out hit))
                    {
                        TouchCacheFile(hit);
                        return;
                    }
                    WriteCacheEntryUnlocked(audioUrl, bytes, ext);
                }
            }
            catch (Exception ex)
            {
                _log.Warn(ex, $"[Bridge] Prefetch / cache fill failed url={Clip(audioUrl, 160)}");
            }
        }

        /// <summary>Fire-and-forget warm cache for portal — same bytes /play will use.</summary>
        private string HandlePrefetch(string body)
        {
            dynamic data    = JsonConvert.DeserializeObject(body);
            string audioUrl = (string)data?.audioUrl;
            if (string.IsNullOrWhiteSpace(audioUrl))
                return "{\"error\":\"audioUrl is required\"}";
            _ = Task.Run(async () => await EnsureUrlCachedAsync(audioUrl));
            return "{\"ok\":true}";
        }

        // ── /play ─────────────────────────────────────────────────────────────
        private async Task<string> HandlePlay(string body)
        {
            _log.Info($"[Bridge] /play ENTER jsonChars={body?.Length ?? 0}");
            dynamic data    = JsonConvert.DeserializeObject(body);
            string audioUrl = (string)data?.audioUrl;
            if (string.IsNullOrWhiteSpace(audioUrl))
                return "{\"error\":\"audioUrl is required\"}";

            _log.Info($"[Bridge] /play audioUrl={Clip(audioUrl, 160)}");

            // Portal may send volume (0–100) and channel — channel is reserved (playback always
            // renders agent headset + VB-Cable when cable exists); volume applies to this clip only.
            int playAgentVol = _agentVol;
            int playCustVol  = _customerVol;
            try
            {
                int? pv = (int?)data?.volume;
                if (pv.HasValue)
                {
                    int v = Math.Max(0, Math.Min(100, pv.Value));
                    playAgentVol = playCustVol = v;
                    _log.Info($"[Bridge] /play requested volume → {v}% (clip-only gain curve; persisted sliders unchanged).");
                }
            }
            catch { /* ignore malformed volume */ }

            byte[] bytes = null;
            string responseContentType = null;
            string ext = ResolveBridgeTempExtension(audioUrl, null);

            if (TryGetCachedFilePath(audioUrl, out string cachedPath))
            {
                try
                {
                    TouchCacheFile(cachedPath);
                    bytes = File.ReadAllBytes(cachedPath);
                    ext = Path.GetExtension(cachedPath);
                    if (string.IsNullOrEmpty(ext) || !_allowedBridgeExtensions.Contains(ext))
                        ext = ResolveBridgeTempExtension(audioUrl, null);
                    _log.Info($"[Bridge] /play CACHE HIT bytes={bytes.Length} ext={ext}");
                }
                catch (Exception ex)
                {
                    _log.Warn(ex, "[Bridge] /play cache read failed — will download");
                    bytes = null;
                }
            }

            if (bytes == null || bytes.Length == 0)
            {
                try
                {
                    using (var resp = await _http.GetAsync(audioUrl))
                    {
                        long? cl = resp.Content.Headers.ContentLength;
                        if (resp.Content.Headers.ContentType != null)
                            responseContentType = resp.Content.Headers.ContentType.MediaType;
                        _log.Info($"[Bridge] /play GET → {(int)resp.StatusCode} {resp.ReasonPhrase} contentLength={cl?.ToString() ?? "?"} contentType={responseContentType ?? "?"}");
                        resp.EnsureSuccessStatusCode();
                        bytes = await resp.Content.ReadAsByteArrayAsync();
                    }
                    _log.Info($"[Bridge] /play downloaded {bytes.Length} bytes");
                }
                catch (Exception ex)
                {
                    _log.Error(ex, $"[Bridge] /play download FAILED url={Clip(audioUrl, 200)}");
                    return "{\"error\":\"download_failed\",\"message\":\"" + ex.Message.Replace("\\", "\\\\").Replace("\"", "'") + "\"}";
                }

                ext = ResolveBridgeTempExtension(audioUrl, responseContentType);
                try
                {
                    lock (_cacheOpsLock)
                    {
                        if (!TryGetCachedFilePath(audioUrl, out _))
                            WriteCacheEntryUnlocked(audioUrl, bytes, ext);
                    }
                }
                catch (Exception cex)
                {
                    _log.Warn(cex, "[Bridge] /play cache write skipped");
                }
            }

            string tmpPath = Path.Combine(Path.GetTempPath(), "ov_script_" + Guid.NewGuid().ToString("N") + ext);
            File.WriteAllBytes(tmpPath, bytes);
            _log.Info($"[Bridge] /play saved temp ext={ext} path={Clip(tmpPath, 120)}");

            bool cableStarted = false;
            bool agentStarted = false;
            lock (_playLock)
            {
                _log.Info($"[Bridge] /play device snapshot: cableWaveOut#={_cableDeviceNumber} agentWaveOutFallback#={outputDeviceNumber} agentMMDevice={(_agentRenderDevice?.FriendlyName ?? "(null)")} persistedVol agent={_agentVol}% customer={_customerVol}% clipVol agent={playAgentVol}% customer={playCustVol}%");

                if (isAudioPlaying) StopAudioInternal();

                _currentTmpPath  = tmpPath;
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
                        audioFileReader.Volume = EffectiveCustomerPlaybackLinear(playCustVol);
                        waveOut = new WaveOutEvent { DeviceNumber = _cableDeviceNumber, DesiredLatency = 100 };
                        waveOut.Init(audioFileReader);
                        waveOut.Play();
                        waveOut.PlaybackStopped += OnPlaybackStopped_Cable;
                        cableStarted = true;
                        _log.Info($"[Bridge] Cable WaveOut STARTED device #{_cableDeviceNumber} vol={playCustVol}% playbackState={waveOut.PlaybackState}");
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
                MMDevice agentDev = _agentRenderDevice;

                try
                {
                    audioFileReader2 = new AudioFileReader(tmpPath);
                    audioFileReader2.Volume = EffectiveAgentPlaybackLinear(playAgentVol);

                    if (agentDev != null)
                    {
                        WasapiOut wasapiTry = null;
                        try
                        {
                            wasapiTry = new WasapiOut(agentDev, AudioClientShareMode.Shared, false, 100);
                            wasapiTry.Init(audioFileReader2);
                            wasapiTry.PlaybackStopped += OnPlaybackStopped_Agent;
                            wasapiTry.Play();
                            waveAgentOut = wasapiTry;
                            agentStarted = true;
                            wasapiTry = null;
                            _log.Info($"[Bridge] Agent WasapiOut STARTED '{agentDev.FriendlyName}' vol={playAgentVol}% playbackState={waveAgentOut.PlaybackState}");
                        }
                        catch (Exception wasapiEx)
                        {
                            try { wasapiTry?.Dispose(); } catch { }
                            _log.Warn(wasapiEx, $"[Bridge] Agent WasapiOut failed ({wasapiEx.Message}) — WaveOut fallback");

                            try { audioFileReader2?.Dispose(); } catch { }
                            audioFileReader2 = null;
                            audioFileReader2 = new AudioFileReader(tmpPath);
                            audioFileReader2.Volume = EffectiveAgentPlaybackLinear(playAgentVol);

                            int devNum = outputDeviceNumber >= 0 ? outputDeviceNumber : 0;
                            var wo = new WaveOutEvent { DeviceNumber = devNum, DesiredLatency = 100 };
                            wo.Init(audioFileReader2);
                            wo.PlaybackStopped += OnPlaybackStopped_Agent;
                            wo.Play();
                            waveAgentOut = wo;
                            agentStarted = true;
                            _log.Info($"[Bridge] Agent WaveOut AFTER Wasapi failure STARTED #{devNum} vol={playAgentVol}% playbackState={wo.PlaybackState}");
                        }
                    }
                    else
                    {
                        int devNum = outputDeviceNumber >= 0 ? outputDeviceNumber : 0;
                        var wo = new WaveOutEvent { DeviceNumber = devNum, DesiredLatency = 100 };
                        wo.Init(audioFileReader2);
                        wo.PlaybackStopped += OnPlaybackStopped_Agent;
                        wo.Play();
                        waveAgentOut = wo;
                        agentStarted = true;
                        _log.Warn($"[Bridge] Agent WaveOut fallback STARTED #{devNum} (no MMDevice — verify headset routing) vol={playAgentVol}% playbackState={wo.PlaybackState}");
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, $"[Bridge] Agent playback FAILED — headset/script audio silent for this clip. Detail: {ex}");
                    try { audioFileReader2?.Dispose(); } catch { }
                    audioFileReader2 = null;
                    try { waveAgentOut?.Dispose(); } catch { }
                    waveAgentOut = null;
                    // BUG FIX: Only abort isAudioPlaying if BOTH outputs failed.
                    // Previously this always set isAudioPlaying=false on agent failure,
                    // which skipped the meter loop even when cable output was still running.
                    if (!cableStarted)
                    {
                        isAudioPlaying = false;
                        OnPlaybackStopped?.Invoke();
                    }
                    else
                    {
                        _log.Warn("[Bridge] Agent output failed but cable is running — meter loop will proceed (cable-only mode).");
                    }
                }

                _log.Info($"[Bridge] /play setup summary: cableStarted={cableStarted} agentStarted={agentStarted} isAudioPlaying={isAudioPlaying} OnPlaybackLevel subscribers={(OnPlaybackLevel?.GetInvocationList()?.Length ?? 0)}");

                if (!cableStarted && !agentStarted)
                    _log.Error("[Bridge] /play ABORTED: neither cable nor agent output started — no audible script route.");
                else if (!agentStarted)
                    _log.Warn("[Bridge] /play agent output MISSING — headset/script silence expected for this clip (RED loopback unrelated).");
                else if (!cableStarted)
                    _log.Warn("[Bridge] /play cable output MISSING — VB-Cable not routed / not found (customer path silent).");

                _playbackHadAgentOutput = agentStarted;

                if (cableStarted || agentStarted)
                    OnPlaybackStarted?.Invoke();

                // ── Meter loop (drives BLUE/GREEN bridge rings in UI) ──────────
                // WAV prep + third decode must NOT block POST /play — portal waits on HTTP ack.
                if (isAudioPlaying && cancellationTokenSource != null)
                {
                    var token            = cancellationTokenSource.Token;
                    string meterSrcPath  = tmpPath;
                    int    meterAgentVol = playAgentVol;
                    int    meterCustVol  = playCustVol;
                    Task.Run(() =>
                    {
                        string mdPath   = meterSrcPath;
                        bool   delCopy = false;
                        try
                        {
                            string meterCopy = Path.Combine(Path.GetTempPath(), "ov_meter_" + Guid.NewGuid().ToString("N") + ".wav");
                            try
                            {
                                using (var srcReader = new AudioFileReader(meterSrcPath))
                                using (var wavWriter = new WaveFileWriter(meterCopy, srcReader.WaveFormat))
                                {
                                    srcReader.CopyTo(wavWriter);
                                }
                                _log.Info($"[Bridge] Meter decode WAV copy \u2192 {Clip(meterCopy, 100)}");
                                mdPath   = meterCopy;
                                delCopy = true;
                            }
                            catch (Exception convEx)
                            {
                                _log.Warn(convEx, "[Bridge] WAV conversion failed \u2014 falling back to raw copy");
                                if (File.Exists(meterCopy)) { try { File.Delete(meterCopy); } catch { } }
                                File.Copy(meterSrcPath, meterCopy, overwrite: true);
                                _log.Info($"[Bridge] Meter decode raw copy (fallback) \u2192 {Clip(meterCopy, 100)}");
                                mdPath   = meterCopy;
                                delCopy = true;
                            }
                        }
                        catch (Exception cex)
                        {
                            _log.Warn(cex, "[Bridge] Meter copy failed \u2014 viz reads primary temp (may be flat WebM)");
                            mdPath   = meterSrcPath;
                            delCopy = false;
                        }

                        try
                        {
                            Thread.Sleep(80); // Let playback IMF graphs finish opening before third decode.
                            ProcessAudioAndVisualizeIntensity_(mdPath, token, meterAgentVol, meterCustVol);
                        }
                        finally
                        {
                            if (delCopy)
                            {
                                try { if (File.Exists(mdPath)) File.Delete(mdPath); } catch { }
                            }
                        }
                    });
                    _log.Info("[Bridge] Meter decode loop QUEUED (non-blocking HTTP)");
                }
                else
                    _log.Warn("[Bridge] Meter decode loop SKIPPED — isAudioPlaying=false (agent init failed?). BLUE/GREEN stay flat; cable-only audio may still run.");
            }

            return "{\"ok\":true}";
        }

        // ── Meter loop ────────────────────────────────────────────────────────
        /// <summary>
        /// Third decoder reads the same file sequentially — never assign Position from the live
        /// playback AudioFileReaders: MF IMFSourceReader.SetCurrentPosition throws COMException
        /// 0xC00D36B2 (&quot;invalid state&quot;) when seeking tracks opened by parallel readers.
        /// Pace reads by decoded duration so this loop finishes with playback instead of racing to EOF.
        /// </summary>
        private void ProcessAudioAndVisualizeIntensity_(string filePath, CancellationToken ct, int agentVolPct, int customerVolPct)
        {
            var sw = Stopwatch.StartNew();
            int iter = 0;
            int zeroReads = 0;
            double maxRms = 0;
            float maxAgentLvl = 0, maxCustLvl = 0;
            long nextDiagMs = 0;

            int bufferSize = 4096;
            try
            {
                using (var reader = new AudioFileReader(filePath))
                {
                    var wf        = reader.WaveFormat;
                    int channels  = Math.Max(1, wf.Channels);
                    double rateHz = Math.Max(1.0, wf.SampleRate);
                    _log.Info($"[Bridge] MeterLoop START file={Clip(filePath, 80)} WF={wf.SampleRate}Hz ch={wf.Channels} bits={wf.BitsPerSample} playing={isAudioPlaying}");

                    var buffer = new float[bufferSize];
                    while (!ct.IsCancellationRequested && isAudioPlaying)
                    {
                        int samplesRead = reader.Read(buffer, 0, buffer.Length);
                        if (samplesRead <= 0)
                        {
                            zeroReads++;
                            Thread.Sleep(50);
                            continue;
                        }

                        iter++;
                        double rms      = CalculateRMS(buffer, samplesRead);
                        if (rms > maxRms) maxRms = rms;
                        // Slightly hotter gain than live mic paths — bridge viz should match audible script energy.
                        float agentGain = EffectiveAgentPlaybackLinear(agentVolPct);
                        float custGain  = EffectiveCustomerPlaybackLinear(customerVolPct);
                        float agentLvl = (float)Math.Min(1.0, rms * 14.0 * agentGain);
                        float custLvl  = (float)Math.Min(1.0, rms * 14.0 * custGain);
                        if (agentLvl > maxAgentLvl) maxAgentLvl = agentLvl;
                        if (custLvl > maxCustLvl) maxCustLvl = custLvl;

                        OnPlaybackLevel?.Invoke(agentLvl, "agent");
                        OnPlaybackLevel?.Invoke(custLvl,  "customer");

                        long elapsed = sw.ElapsedMilliseconds;
                        if (elapsed >= nextDiagMs)
                        {
                            _log.Info($"[Bridge] MeterLoop heartbeat iter={iter} zeroReads={zeroReads} rms={rms:F5} peakRms={maxRms:F5} lvlAgent={agentLvl:F3} lvlCust={custLvl:F3} playing={isAudioPlaying} ctCancel={ct.IsCancellationRequested}");
                            nextDiagMs = elapsed + 500;
                        }

                        int frames = samplesRead / channels;
                        int sleepMs = (int)Math.Round(frames / rateHz * 1000.0);
                        sleepMs = Math.Max(1, Math.Min(sleepMs, 250));
                        Thread.Sleep(sleepMs);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"[Bridge] MeterLoop EXCEPTION after iter={iter}");
            }
            finally
            {
                _log.Info($"[Bridge] MeterLoop END elapsedMs={sw.ElapsedMilliseconds} iter={iter} zeroReads={zeroReads} maxRms={maxRms:F5} maxLvlAgent={maxAgentLvl:F3} maxLvlCust={maxCustLvl:F3} playing={isAudioPlaying} ctCancel={ct.IsCancellationRequested}");
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
            if (e.Exception != null)
                _log.Warn(e.Exception, "[Bridge] Cable PlaybackStopped with exception");
            else
                _log.Info("[Bridge] Cable PlaybackStopped (normal)");
            try { audioFileReader?.Dispose(); audioFileReader = null; waveOut?.Dispose(); waveOut = null; } catch { }
            // When agent headset output never started (cable-only mode), OnPlaybackStopped_Agent never runs — finalize here
            // so desktop UI / mic pass-through restarts (same as agent handler).
            if (!_playbackHadAgentOutput && isAudioPlaying)
            {
                _log.Info("[Bridge] Cable-only playback ended — finalizing session (agent output was not active).");
                isAudioPlaying = false;
                DeleteCurrentTmpFile();
                OnPlaybackStopped?.Invoke();
            }
        }

        private void OnPlaybackStopped_Agent(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
                _log.Warn(e.Exception, "[Bridge] Agent PlaybackStopped with exception");
            else
                _log.Info("[Bridge] Agent PlaybackStopped (normal)");
            isAudioPlaying = false;
            try { audioFileReader2?.Dispose(); audioFileReader2 = null; waveAgentOut?.Dispose(); waveAgentOut = null; } catch { }
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
                    if (audioFileReader  != null) audioFileReader.Volume  = EffectiveCustomerPlaybackLinear(volume);
                }
                else
                {
                    _agentVol = volume;
                    if (audioFileReader2 != null) audioFileReader2.Volume = EffectiveAgentPlaybackLinear(volume);
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
            _playbackHadAgentOutput = false;
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = null;

            try { waveOut?.Stop();  } catch { }
            try { waveOut?.Dispose(); } catch { }
            try { audioFileReader?.Dispose(); } catch { }
            waveOut = null; audioFileReader = null;

            try { waveAgentOut?.Stop();    } catch { }
            try { waveAgentOut?.Dispose(); } catch { }
            try { audioFileReader2?.Dispose(); } catch { }
            waveAgentOut = null; audioFileReader2 = null;

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
