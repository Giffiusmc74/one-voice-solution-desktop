/**
 * HeartbeatService.cs
 * ONE Voice Solution v5.0
 *
 * Sends a periodic heartbeat to the ONE portal every N minutes while the app is running.
 *
 * Flow:
 *   1. On startup, after license validation passes, call DesktopLoginAsync() to exchange
 *      the license key for a Bearer session token and agent name.
 *   2. Every HeartbeatIntervalMinutes, POST to /api/heartbeat with:
 *         Authorization: Bearer <sessionToken>
 *         Body: { licenseKey, sessionId, appVersion }
 *   3. If the server responds with seatLimitExceeded=true, show a warning.
 *   4. If the server responds 401/403, the license has been revoked — lock the app.
 *   5. Offline pings are queued locally and flushed when connectivity restores.
 */
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsFormsApp1.src
{
    public class HeartbeatService : IDisposable
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        private static HeartbeatService _instance;
        public static HeartbeatService Instance => _instance ?? (_instance = new HeartbeatService());

        // ── Events ────────────────────────────────────────────────────────────
        public event EventHandler<LicenseInvalidEventArgs> LicenseInvalid;
        public event EventHandler<SeatLimitEventArgs>      SeatLimitExceeded;

        // ── State ─────────────────────────────────────────────────────────────
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private readonly Logger     _log  = LogManager.GetCurrentClassLogger();
        private System.Threading.Timer _timer;
        private DateTime _sessionStart;
        private string   _licenseKey;
        private string   _sessionToken;  // Bearer token from /api/license/desktop-login
        private string   _sessionId;     // Unique UUID per app launch
        private readonly string _appVersion;
        private readonly string _baseUrl;
        private readonly string _heartbeatUrl;
        private readonly string _desktopLoginUrl;
        private readonly int    _intervalMs;
        private bool _disposed;

        // ── Agent info (returned from desktop-login) ──────────────────────────
        public string AgentName { get; private set; } = "";

        // ── Offline queue ─────────────────────────────────────────────────────
        private readonly Queue<HeartbeatPayload> _offlineQueue = new Queue<HeartbeatPayload>();
        private const int MaxOfflineQueue = 50;
        private readonly string _queueFile;

        // ── Constructor ───────────────────────────────────────────────────────
        private HeartbeatService()
        {
            _baseUrl         = "https://onevoiceapp-wpzvhh8c.manus.space";
            _heartbeatUrl    = _baseUrl + "/api/heartbeat";
            _desktopLoginUrl = _baseUrl + "/api/license/desktop-login";
            _appVersion      = ConfigurationManager.AppSettings["AppVersion"] ?? "5.0";

            int minutes = 10;
            int.TryParse(ConfigurationManager.AppSettings["HeartbeatIntervalMinutes"], out minutes);
            _intervalMs = minutes * 60 * 1000;

            _sessionId = Guid.NewGuid().ToString("N"); // unique per launch

            _queueFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OneVoiceSolution", "heartbeat_queue.json");

            LoadOfflineQueue();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Step 1: Exchange license key for a session token.
        /// Call this once after license validation passes on startup.
        /// Returns true if login succeeded.
        /// </summary>
        public async Task<bool> DesktopLoginAsync(string licenseKey, string machineId)
        {
            _licenseKey = licenseKey;

            try
            {
                var payload = new { key = licenseKey, machineId };
                string json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(_desktopLoginUrl, content);
                string body = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<DesktopLoginResponse>(body);

                if (result != null && result.ok)
                {
                    _sessionToken = result.sessionToken ?? "";
                    AgentName     = result.agentName ?? "";
                    _log.Info($"[Heartbeat] Desktop login OK — agent={AgentName}");
                    return true;
                }
                else
                {
                    _log.Warn($"[Heartbeat] Desktop login failed: {result?.reason}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"[Heartbeat] Desktop login network error: {ex.Message}");
                // Non-fatal — app can still run offline; heartbeat will retry
                return true;
            }
        }

        /// <summary>
        /// Step 2: Start the periodic heartbeat timer.
        /// Call this after DesktopLoginAsync succeeds.
        /// </summary>
        public void Start()
        {
            _sessionStart = DateTime.UtcNow;
            _timer = new System.Threading.Timer(
                async _ => await SendHeartbeatAsync(),
                null,
                dueTime: 30_000,     // first ping after 30 seconds
                period: _intervalMs);

            _log.Info($"[Heartbeat] Started — interval={_intervalMs / 60000}min");
        }

        /// <summary>Stop the timer (call on form close).</summary>
        public void Stop()
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _timer?.Dispose();
            _timer = null;
            _log.Info("[Heartbeat] Stopped.");
        }

        // ── Core send logic ───────────────────────────────────────────────────

        private async Task SendHeartbeatAsync()
        {
            if (_disposed || string.IsNullOrEmpty(_licenseKey)) return;

            // Flush queued offline pings first
            await FlushOfflineQueueAsync();

            var payload = new HeartbeatPayload
            {
                licenseKey = _licenseKey,
                sessionId  = _sessionId,
                appVersion = _appVersion
            };

            bool success = await PostHeartbeatAsync(payload);
            if (!success)
                EnqueueOffline(payload);
        }

        private async Task<bool> PostHeartbeatAsync(HeartbeatPayload payload)
        {
            try
            {
                string json = JsonConvert.SerializeObject(payload);
                var request = new HttpRequestMessage(HttpMethod.Post, _heartbeatUrl)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                if (!string.IsNullOrEmpty(_sessionToken))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sessionToken);

                var response = await _http.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _log.Warn("[Heartbeat] License revoked — locking app.");
                    FireLicenseInvalid("Your license has been revoked or payment has lapsed.");
                    Stop();
                    return true; // don't queue — it's a hard stop
                }

                if (response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<HeartbeatResponse>(body);

                    if (result != null && result.seatLimitExceeded)
                    {
                        _log.Warn($"[Heartbeat] Seat limit exceeded: active={result.activeSeats} purchased={result.purchasedSeats}");
                        FireSeatLimitExceeded(result.activeSeats, result.purchasedSeats);
                    }
                    return true;
                }

                _log.Warn($"[Heartbeat] Server returned {(int)response.StatusCode} — queuing.");
                return false;
            }
            catch (Exception ex)
            {
                _log.Warn($"[Heartbeat] Network error — queuing. {ex.Message}");
                return false;
            }
        }

        // ── Offline queue ─────────────────────────────────────────────────────

        private void EnqueueOffline(HeartbeatPayload payload)
        {
            if (_offlineQueue.Count >= MaxOfflineQueue)
                _offlineQueue.Dequeue();
            _offlineQueue.Enqueue(payload);
            SaveOfflineQueue();
        }

        private async Task FlushOfflineQueueAsync()
        {
            while (_offlineQueue.Count > 0)
            {
                var queued = _offlineQueue.Peek();
                bool ok = await PostHeartbeatAsync(queued);
                if (ok) _offlineQueue.Dequeue();
                else break;
            }
            if (_offlineQueue.Count == 0)
                SaveOfflineQueue();
        }

        private void SaveOfflineQueue()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_queueFile));
                File.WriteAllText(_queueFile, JsonConvert.SerializeObject(_offlineQueue));
            }
            catch { }
        }

        private void LoadOfflineQueue()
        {
            try
            {
                if (!File.Exists(_queueFile)) return;
                var items = JsonConvert.DeserializeObject<List<HeartbeatPayload>>(File.ReadAllText(_queueFile));
                if (items == null) return;
                foreach (var item in items)
                    _offlineQueue.Enqueue(item);
            }
            catch { }
        }

        // ── Event helpers ─────────────────────────────────────────────────────

        private void FireLicenseInvalid(string reason) =>
            LicenseInvalid?.Invoke(this, new LicenseInvalidEventArgs { Reason = reason });

        private void FireSeatLimitExceeded(int active, int purchased) =>
            SeatLimitExceeded?.Invoke(this, new SeatLimitEventArgs { ActiveSeats = active, PurchasedSeats = purchased });

        // ── IDisposable ───────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _http?.Dispose();
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    public class HeartbeatPayload
    {
        public string licenseKey { get; set; }
        public string sessionId  { get; set; }
        public string appVersion { get; set; }
    }

    public class HeartbeatResponse
    {
        public bool ok               { get; set; }
        public bool seatLimitExceeded { get; set; }
        public int  activeSeats      { get; set; }
        public int  purchasedSeats   { get; set; }
    }

    public class DesktopLoginResponse
    {
        public bool   ok           { get; set; }
        public string sessionToken { get; set; }
        public string agentName    { get; set; }
        public string reason       { get; set; }
    }

    public class LicenseInvalidEventArgs : EventArgs
    {
        public string Reason { get; set; }
    }

    public class SeatLimitEventArgs : EventArgs
    {
        public int ActiveSeats    { get; set; }
        public int PurchasedSeats { get; set; }
    }
}
