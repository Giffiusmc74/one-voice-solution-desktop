/*
 * MainFormV5.cs  —  ONE Voice Solution v7.92
 *
 * UI REDESIGN v7.31+ (footer / branding version in APP_VERSION below):
 *   - Complete visual overhaul to match design mock exactly.
 *   - Dark space background with ~140 static, glowing stars (clipped to the background only).
 *   - ONE logo (top-left) + "The Geniusness Is In The Simplicity" tagline centered in header.
 *   - 4 circular neon glow meters with live frequency tracking capped at static volume %.
 *   - Unified [– VOLUME +] pill-shaped controls below meters.
 *   - Custom GDI+ drawn device icons for MICROPHONE (red) and SPEAKER (blue).
 *   - Footer: "One United Global LLC 2026. V {APP_VERSION}"
 *   - ALL audio logic, routing, and meter data connections mapped accurately.
 *
 * v7.30 changes (audio):
 *   - Mic pass-through restarts automatically after each recording finishes.
 *   - WaveFormat upgraded to 48000/16/stereo.
 *
 * v7.29 changes (audio):
 *   - Mic pass-through WaveOut volume explicitly set to 1.0f after Play().
 *
 * v7.27 changes (audio):
 *   - Mic pass-through: agent's live voice routed from Jabra mic through VB-Audio Cable Input.
 *
 * v7.21 changes (audio):
 *   - Volume sliders take effect from the FIRST playback.
 */
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NLog;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1.src;

namespace WindowsFormsApp1
{
    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();
        }
    }

    public partial class MainFormV5 : Form
    {
        // ── Brand colours ─────────────────────────────────────────────────────
        private static readonly Color ONE_RED       = Color.FromArgb(254, 1, 1);
        private static readonly Color BG_DARK       = Color.FromArgb(5, 5, 18);        // Deep space black-blue
        private static readonly Color BG_CARD       = Color.FromArgb(14, 14, 30);      // Dark card background
        private static readonly Color BG_PANEL      = Color.FromArgb(28, 28, 28);
        private static readonly Color TEXT_WHITE     = Color.White;
        private static readonly Color ONE_BLUE_SEL   = Color.FromArgb(0, 102, 204);

        // Meter colours per channel
        private static readonly Color METER_RED     = Color.FromArgb(254, 1, 1);
        private static readonly Color METER_BLUE    = Color.FromArgb(0, 160, 255);
        private static readonly Color METER_PURPLE  = Color.FromArgb(160, 32, 240);
        private static readonly Color METER_GREEN   = Color.FromArgb(0, 220, 80);

        // ── Version ───────────────────────────────────────────────────────────
        private const string APP_VERSION = "8.1";

        // ── Scale ─────────────────────────────────────────────────────────────
        private float _scale = 1.0f;

        // ── Background cache (prevents OutOfMemory from rapid GDI+ repaints) ─────
        private Bitmap _bgCache = null;
        private Size   _bgCacheSize = Size.Empty;
        private float SF(float pt) => Math.Max(7f, (float)Math.Round(pt * _scale, 1));

        // DPI helper
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
        private const int LOGPIXELSX = 88;

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        // ── Audio ─────────────────────────────────────────────────────────────
        private WasapiCapture      _micCapture;
        private WaveInEvent        _micPassWaveIn;
        private WaveOutEvent       _micPassWaveOut;
        private BufferedWaveProvider _micPassBuffer;
        /// <summary>
        /// Reusable processing buffer for mic pass-through. Pre-allocated to avoid per-callback
        /// heap churn (~20 allocs/sec × 9,600 bytes = 192KB/sec) that causes GC pauses and audio
        /// dropouts during long calls. Grown on demand, never shrunk.
        /// </summary>
        private byte[]             _micPassProcBuf = Array.Empty<byte>();
        /// <summary>
        /// True while a bridge script is actively playing. Guards against StartLoopbackCapture()
        /// tearing down active audio sources during window restore or bridge reconnection events.
        /// Set to true on OnPlaybackStarted, false on OnPlaybackStopped.
        /// </summary>
        private volatile bool      _isCallActive   = false;
        private MMDeviceEnumerator _deviceEnum = new MMDeviceEnumerator();
        /// <summary>Keep mic pass-through conservative to prevent downstream VB-cable/call-app breakup.</summary>
        private const float MicPassThroughGain = 0.82f;
        private const float MicPassThroughHardLimit = 0.90f;
        private volatile float _micLevel            = 0f;
        /// <summary>Smoothed mic RMS (−1..1 samples) — drives bridge script gain like AudioService.PlayAudio.</summary>
        private float _smoothedMicRmsLinear = 0.126f;
        private int _bridgeLevelMatchThrottle;
        private volatile float _customerVoiceLevel  = 0f;
        private float _agentScriptLevel    = 0f;
        private float _customerScriptLevel = 0f;
        /// <summary>
        /// Smoothed script playback envelope used to suppress bridge playback bleed from RED loopback meter.
        /// </summary>
        private volatile float _bridgePlaybackEnvelope = 0f;
        /// <summary>
        /// Max raw meter sample seen this clip (never decays during silence inside the recording).
        /// Prevents bleed subtraction from collapsing between phrases → RED blips when speech resumes.
        /// </summary>
        private volatile float _bridgePlaybackSessionPeak = 0f;
        /// <summary>Throttles [Bridge→UI] diagnostic lines (~500ms).</summary>
        private readonly Stopwatch _bridgeUiDiagSw = Stopwatch.StartNew();

        // ── UI Controls ───────────────────────────────────────────────────────
        private PictureBox  _logoBox;
        private Label       _lblTagline;
        private Label       _lblAgentName;
        private ComboBox    _cboMic;
        private ComboBox    _cboHeadset;
        // Meter panels (used for real-time invalidation)
        private Panel       _myMicMeterLeft;       // Agent Voice (purple) — right side
        private Panel       _agentScriptMeter;     // Agent Recordings (green) — right side
        private Panel       _customerVoiceMeter;   // Customer Voice (red) — left side
        private Panel       _customerScriptMeter;  // Customer Recordings (blue) — left side
        private Label       _lblFooterCenter;
        private NotifyIcon  _trayIcon;
        private ContextMenuStrip _trayMenu;
        private System.Windows.Forms.Timer _meterTimer;
        private Button _btnClose;
        private Button _btnMinimize;
        private MMDevice _activeMicDevice;
        private MMDevice _activeSpeakerDevice;
        private MMDevice _activeVBCableDevice;

        // Volume gain values (0.0–2.0, default 1.0 = 0 dB)
        // These are the per-channel gain scalars used by the meter display and bridge.
        private float _volCustomerVoice      = 1.0f;
        private float _volCustomerScript     = 1.0f;
        private float _volAgentVoice         = 1.0f;
        private float _volAgentScript        = 1.0f;

        // dB display values (integer, shown in the [–][val][+] controls)
        private int _dbCustomerVoice     = -3;
        private int _dbCustomerScript    = -3;
        private int _dbAgentVoice        = -3;
        private int _dbAgentScript       = -3;

        // dB display labels
        private Label _lblDbCustomerVoice;
        private Label _lblDbCustomerScript;
        private Label _lblDbAgentVoice;
        private Label _lblDbAgentScript;

        // TrackBar fields kept for audio logic compatibility (hidden)
        private TrackBar _trkMicVol;
        private TrackBar _trkSpeakerVol;
        private TrackBar _trkAgentScriptVol;
        private TrackBar _trkCustomerScriptVol;
        private Label    _lblMicVol;
        private Label    _lblSpeakerVol;
        private Label    _lblAgentScriptVol;
        private Label    _lblCustomerScriptVol;

        // Loopback capture(s) for Customer Voice (RED): VB-Cable + default Multimedia render + optional Communications render.
        // Browser VoIP (Dialpad, WebRTC) often uses Role.Communications (e.g. Bluetooth Hands-Free) while we already tap Multimedia (Stereo).
        private WasapiLoopbackCapture  _loopbackCapture;
        private WasapiLoopbackCapture  _loopbackDefaultCapture;
        private WasapiLoopbackCapture  _loopbackCommunicationsCapture;
        private float                  _customerVoiceVolume = 1.0f;
        /// <summary>
        /// Timestamp when loopback capture last started. Loopback data is suppressed for the first
        /// 2 seconds after startup to prevent Windows startup sounds or app-launch audio from
        /// freezing the RED meter at a non-zero value before any call begins.
        /// </summary>
        private DateTime _loopbackStartTime = DateTime.MinValue;
        private const int LoopbackWarmupMs = 2000;
        /// <summary>
        /// Tracks when mic pass-through last started. Auto-restarts every 4 minutes to flush
        /// USB clock drift between the mic device and VB-Cable before it accumulates enough
        /// to cause a garble (observed ~7 min on Scarlett/Plugable USB setups).
        /// </summary>
        private DateTime _micPassLastRestart = DateTime.MinValue;
        private const int MicPassDriftRestartMs = 4 * 60 * 1000; // 4 minutes
        /// <summary>
        /// Mic gate constants for RED (customer voice) meter suppression.
        /// When the agent mic is active above MicGateThreshold, the VB-Cable loopback
        /// is dominated by mic passthrough audio — so we zero RED completely instead of
        /// trying to subtract (scalar subtraction is unreliable due to thread timing).
        /// MicGateHoldTickCount keeps RED zeroed for 300ms after mic drops below threshold
        /// to prevent flickering between syllables (each tick = 50ms meter timer interval).
        /// Tune MicGateThreshold: lower = more sensitive gate (may suppress during quiet speech);
        ///                        higher = less sensitive (may let brief mic spikes through).
        /// </summary>
        private const float MicGateThreshold    = 0.12f; // 12 % = only gates on clear speech, not quiet ambient
        private const int   MicGateHoldTickCount = 2;    // 2 × 50 ms = 100 ms release (was 300 ms — RED came back too slow)
        private int         _micGateHoldTicks    = 0;    // runtime hold-down counter
        /// <summary>
        /// During bridge playback, headphone loopback still carries tiny residual signal (DSP noise / bleed).
        /// Do not advance RED unless clearly above this — avoids ~4% random arc twitch when customer is silent.
        /// </summary>
        private const float RedMeterPlaybackNoiseGate = 0.058f;
        /// <summary>
        /// When not playing: loopback still has idle noise / quantization; kills ~4% blips when customer is muted/silent.
        /// </summary>
        private const float RedMeterIdleNoiseGate = 0.052f;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION        = 0x2;

        // ── Constructor ───────────────────────────────────────────────────────
        public MainFormV5() : this(null) { }
        public MainFormV5(string agentNameOverride)
        {
            InitializeComponent();
            BuildUI();
            SetupTrayIcon();
            SetupMeterTimer();
            LoadSettings();
            PopulateDevices();
            StartAudioCapture();
            StartHeartbeat();
            StartBridgeServer();
            AudioService.Instance.ScriptPlaybackToCustomerLevelChanged += OnScriptPlaybackToCustomerLevel;
            AudioService.Instance.ScriptPlaybackAgentHeadsetLevelChanged += OnScriptPlaybackAgentHeadsetLevel;
            StartLoopbackCapture();
            if (!string.IsNullOrEmpty(agentNameOverride) && _lblAgentName != null)
                _lblAgentName.Text = "Agent: " + agentNameOverride;
            this.Shown += (s, e) =>
            {
                AutoUpdater.CheckAndUpdate(APP_VERSION);
            };
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Text            = "ONE Voice Solution";
            this.BackColor       = BG_DARK;
            this.ForeColor       = TEXT_WHITE;
            this.FormBorderStyle = FormBorderStyle.None;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.StartPosition   = FormStartPosition.Manual;
            this.ShowInTaskbar   = true;
            this.DoubleBuffered  = true;
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "one_logo.ico");
            if (File.Exists(iconPath)) this.Icon = new Icon(iconPath);
            CenterWithMargin();
            this.ResumeLayout(false);
        }

        private void CenterWithMargin()
        {
            Screen screen = Screen.FromPoint(Cursor.Position);
            Rectangle wa  = screen.WorkingArea;

            float dpi = 96f;
            try
            {
                IntPtr hdc = GetDC(IntPtr.Zero);
                dpi = GetDeviceCaps(hdc, LOGPIXELSX);
                ReleaseDC(IntPtr.Zero, hdc);
            }
            catch { }

            int w = Math.Max((int)(wa.Width  * 0.92), 960);
            int h = Math.Max((int)(wa.Height * 0.88), 600);

            float sizeScale = Math.Min((float)w / 1400f, (float)h / 820f);
            _scale = Math.Max(0.55f, Math.Min(sizeScale, 1.20f));

            this.ClientSize = new Size(w, h);
            int cx = wa.Left + (wa.Width - w) / 2;
            int cy = wa.Top + (wa.Height - h) / 2;
            if (cx < wa.Left) cx = wa.Left;
            if (cy < wa.Top) cy = wa.Top;
            this.Location = new Point(cx, cy);

            Log.Info($"[UI] Screen={screen.DeviceName} DPI={dpi} WA={wa} FormSize={w}x{h} Scale={_scale:F2}");
        }

         // ── Paint: space background + dark card + horizontal glow flare ─────────
        // Background is cached to a Bitmap to prevent OutOfMemoryException from
        // rapid GDI+ object allocation during minimize/restore repaint cascades.
        // NOTE: base.OnPaint is intentionally NOT called — calling it triggers
        // recursive child-control repaints during restore which exhaust GDI+ memory.
        protected override void OnPaintBackground(PaintEventArgs e) { /* suppress erase flash */ }
        protected override void OnPaint(PaintEventArgs e)
        {
            int W = this.ClientSize.Width;
            int H = this.ClientSize.Height;
            if (W <= 0 || H <= 0) return;

            // Rebuild cache only when size changes or cache is empty
            if (_bgCache == null || _bgCacheSize != this.ClientSize)
            {
                try
                {
                    _bgCache?.Dispose();
                    _bgCache = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    _bgCacheSize = this.ClientSize;
                    using (var bg = Graphics.FromImage(_bgCache))
                    {
                        bg.SmoothingMode = SmoothingMode.AntiAlias;
                        PaintBackground(bg, W, H);
                    }
                }
                catch
                {
                    // If cache build fails, fill with solid background — never crash
                    try { e.Graphics.Clear(Color.FromArgb(5, 5, 12)); } catch { }
                    return;
                }
            }
            // Fast blit — no new GDI objects created on every repaint
            try { e.Graphics.DrawImage(_bgCache, 0, 0); } catch { }
        }

        private void PaintBackground(Graphics g, int W, int H)
        {
            // 1. Dark deep space background
            using (var bgBrush = new SolidBrush(Color.FromArgb(5, 5, 12)))
                g.FillRectangle(bgBrush, 0, 0, W, H);

            // 2. Draw nebulas
            void DrawNebula(int nx, int ny, int nw, int nh, Color c) {
                using (var gp = new GraphicsPath()) {
                    gp.AddEllipse(nx, ny, nw, nh);
                    using (var pgb = new PathGradientBrush(gp)) {
                        pgb.CenterColor = c;
                        pgb.SurroundColors = new[] { Color.FromArgb(0, c.R, c.G, c.B) };
                        g.FillPath(pgb, gp);
                    }
                }
            }
            DrawNebula(W / 2 - (int)(W * 0.4f), -(int)(H * 0.2f), (int)(W * 0.8f), (int)(H * 0.4f), Color.FromArgb(40, 255, 0, 0));
            DrawNebula(-(int)(W * 0.2f), H - (int)(H * 0.4f), (int)(W * 0.6f), (int)(H * 0.6f), Color.FromArgb(25, 255, 0, 0));
            DrawNebula(W - (int)(W * 0.4f), H - (int)(H * 0.4f), (int)(W * 0.6f), (int)(H * 0.6f), Color.FromArgb(25, 0, 120, 255));

            // 3. Static stars — realistic temperature colors, glow halos, diffraction spikes.
            //    Density reduced ~80% (700 -> 140) and kept in the BACKGROUND ONLY: any
            //    star landing on a control/text (labels, VOLUME +/- , device dropdowns,
            //    meters, footer/title) is skipped so nothing renders over the UI.
            int starExclMargin = (int)(16 * _scale); // covers a bright star's halo/spike bleed
            bool StarBlocked(int px, int py) {
                foreach (Control ctl in this.Controls) {
                    if (!ctl.Visible || ctl.Width <= 1 || ctl.Height <= 1) continue;
                    var rr = ctl.Bounds; rr.Inflate(starExclMargin, starExclMargin);
                    if (rr.Contains(px, py)) return true;
                }
                return false;
            }
            var rnd = new Random(W * H);
            for (int i = 0; i < 126; i++) {
                int sx = rnd.Next(W);
                int sy = rnd.Next(H);
                if (StarBlocked(sx, sy)) continue;
                // Size skewed toward small — most stars are tiny, few are large (realistic distribution)
                double r1 = rnd.NextDouble(), r2 = rnd.NextDouble();
                float sz = (float)(Math.Min(r1, r2) * 3.2f + 0.4f);

                // Star temperature color: blue-white (hot) → white → yellow-white → orange-warm
                int colorRoll = rnd.Next(100);
                int sr, sg, sb;
                if      (colorRoll < 15) { sr = 180; sg = 200; sb = 255; }   // blue-white (O/B type)
                else if (colorRoll < 55) { sr = 255; sg = 255; sb = 255; }   // pure white (A type)
                else if (colorRoll < 82) { sr = 255; sg = 248; sb = 210; }   // yellow-white (F/G type)
                else                     { sr = 255; sg = 215; sb = 170; }   // orange-warm (K type)

                int alpha = rnd.Next(25, 160);
                bool isBright = sz > 2.0f;

                if (isBright) {
                    // Outer soft halo
                    float haloR = sz * 3.5f;
                    using (var haloPath = new GraphicsPath()) {
                        haloPath.AddEllipse(sx - haloR, sy - haloR, haloR * 2, haloR * 2);
                        using (var pgb = new PathGradientBrush(haloPath)) {
                            pgb.CenterColor    = Color.FromArgb(Math.Min(255, alpha + 50), sr, sg, sb);
                            pgb.SurroundColors = new[] { Color.FromArgb(0, sr, sg, sb) };
                            g.FillPath(pgb, haloPath);
                        }
                    }
                    // 4-spike diffraction cross (like telescope optics)
                    float spikeLen = sz * 5f;
                    int spikeAlpha = alpha / 3;
                    using (var sp = new Pen(Color.FromArgb(spikeAlpha, sr, sg, sb), 0.6f)) {
                        g.DrawLine(sp, sx - spikeLen, sy, sx + spikeLen, sy);
                        g.DrawLine(sp, sx, sy - spikeLen, sx, sy + spikeLen);
                    }
                    alpha = Math.Min(255, alpha + 80);
                }

                using (var b = new SolidBrush(Color.FromArgb(alpha, sr, sg, sb)))
                    g.FillEllipse(b, sx - sz / 2f, sy - sz / 2f, sz, sz);
            }

            // 4. Horizontal red light flare
            int cardPad = (int)(18 * _scale);
            int headerH = (int)(90 * _scale);
            int flareY  = cardPad + headerH;
            int flareX1 = (int)(30 * _scale);
            int flareX2 = W - (int)(30 * _scale);
            int glowH2  = (int)(14f * _scale);
            using (var lgb2 = new LinearGradientBrush(
                new Rectangle(flareX1, flareY - glowH2 / 2, Math.Max(1, flareX2 - flareX1), glowH2),
                Color.FromArgb(60, 254, 1, 1), Color.FromArgb(60, 254, 1, 1), 0f))
            {
                lgb2.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(60, 254, 1, 1), Color.FromArgb(20, 255, 255, 255), Color.FromArgb(60, 254, 1, 1) }, Positions = new[] { 0f, 0.5f, 1f } };
                using (var pen2 = new Pen(lgb2, glowH2)) g.DrawLine(pen2, flareX1, flareY, flareX2, flareY);
            }
            int glowH = (int)(5f * _scale);
            using (var lgb = new LinearGradientBrush(
                new Rectangle(flareX1, flareY - glowH / 2, Math.Max(1, flareX2 - flareX1), glowH),
                Color.FromArgb(180, 254, 1, 1), Color.FromArgb(180, 254, 1, 1), 0f))
            {
                lgb.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(180, 254, 1, 1), Color.FromArgb(220, 255, 255, 255), Color.FromArgb(180, 254, 1, 1) }, Positions = new[] { 0f, 0.5f, 1f } };
                using (var pen = new Pen(lgb, glowH)) g.DrawLine(pen, flareX1, flareY, flareX2, flareY);
            }
            int coreH = (int)(2f * _scale);
            using (var lcore = new LinearGradientBrush(
                new Rectangle(flareX1, flareY - 1, Math.Max(1, flareX2 - flareX1), Math.Max(2, coreH)),
                Color.FromArgb(255, 254, 1, 1), Color.FromArgb(255, 254, 1, 1), 0f))
            {
                lcore.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(255, 254, 1, 1), Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 254, 1, 1) }, Positions = new[] { 0f, 0.5f, 1f } };
                using (var penCore = new Pen(lcore, Math.Max(1.5f, coreH))) g.DrawLine(penCore, flareX1, flareY, flareX2, flareY);
            }

            // 5. Window Border — red neon glow (multi-pass)
            using (var path = RoundedRect(new Rectangle(1, 1, W - 3, H - 3), (int)(15 * _scale)))
            {
                using (var gp1 = new Pen(Color.FromArgb(18, 255, 0, 0), 18f * _scale))  g.DrawPath(gp1, path);
                using (var gp2 = new Pen(Color.FromArgb(35, 255, 0, 0), 12f * _scale))  g.DrawPath(gp2, path);
                using (var gp3 = new Pen(Color.FromArgb(60, 255, 0, 0), 7f * _scale))   g.DrawPath(gp3, path);
                using (var gp4 = new Pen(Color.FromArgb(100, 255, 0, 0), 3.5f * _scale)) g.DrawPath(gp4, path);
                using (var gp5 = new Pen(Color.FromArgb(200, 255, 40, 40), 1.2f * _scale)) g.DrawPath(gp5, path);
            }
        }

        // ── Build UI ──────────────────────────────────────────────────────────
        private void BuildUI()
        {
            int W = this.ClientSize.Width;
            int H = this.ClientSize.Height;
            int cardPad = (int)(18 * _scale);

            BuildHeader(W, cardPad);
            BuildMeterSection(W, H, cardPad);
            BuildDeviceButtons(W, H, cardPad);
            BuildFooter(W, H, cardPad);
            BuildWindowButtons(W, cardPad);
            BuildHiddenTrackBars(); // keep audio logic wired
        }

        // ── Header ────────────────────────────────────────────────────────────
        private void BuildHeader(int W, int cardPad)
        {
            int headerH = (int)(90 * _scale);
            int logoH   = (int)(84 * _scale);
            int logoW   = (int)(230 * _scale);
            int logoX   = cardPad + (int)(24 * _scale);
            int logoY   = cardPad + (headerH - logoH) / 2;

            _logoBox = new PictureBox
            {
                Bounds    = new Rectangle(logoX, logoY, logoW, logoH),
                SizeMode  = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            LoadLogo();
            this.Controls.Add(_logoBox);
            AttachDrag(_logoBox);

            // Tagline: centered across the entire window
            _lblTagline = new Label
            {
                Text      = "The Geniusness Is In The Simplicity",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(21f), FontStyle.Bold),
                AutoSize  = true
            };
            this.Controls.Add(_lblTagline);
            AttachDrag(_lblTagline);
            int tagW  = TextRenderer.MeasureText(_lblTagline.Text, _lblTagline.Font).Width;
            int tagH  = TextRenderer.MeasureText(_lblTagline.Text, _lblTagline.Font).Height;
            _lblTagline.Location = new Point((W - tagW) / 2, cardPad + (headerH - tagH) / 2);

            // Agent name (hidden, kept for heartbeat logic)
            _lblAgentName = new Label
            {
                Text      = "Agent: " + AppSettings.Instance.AgentName,
                ForeColor = Color.Transparent,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(11f), FontStyle.Regular),
                AutoSize  = true,
                Location  = new Point(-500, -500)
            };
            this.Controls.Add(_lblAgentName);
        }

        // ── Window buttons ────────────────────────────────────────────────────
        private void BuildWindowButtons(int W, int cardPad)
        {
            int btnSz     = (int)(30 * _scale);
            int btnMargin = cardPad + (int)(10 * _scale);
            int btnY      = cardPad + (int)(10 * _scale);

            _btnClose = new Button
            {
                Text      = "\u2715",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(55, 55, 65),
                Font      = new Font("Segoe UI", SF(11f), FontStyle.Bold),
                Bounds    = new Rectangle(W - btnSz - btnMargin, btnY, btnSz, btnSz),
                Cursor    = Cursors.Hand,
                TabStop   = false
            };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 0, 0);
            _btnClose.Click += (s, e) => Application.Exit();
            this.Controls.Add(_btnClose);

            _btnMinimize = new Button
            {
                Text      = "\u2212",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(55, 55, 65),
                Font      = new Font("Segoe UI", SF(11f), FontStyle.Bold),
                Bounds    = new Rectangle(W - btnSz * 2 - btnMargin - (int)(8 * _scale), btnY, btnSz, btnSz),
                Cursor    = Cursors.Hand,
                TabStop   = false
            };
            _btnMinimize.FlatAppearance.BorderSize = 0;
            _btnMinimize.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 90);
            _btnMinimize.Click += (s, e) => { this.WindowState = FormWindowState.Minimized; };
            this.Controls.Add(_btnMinimize);

            this.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && e.Y < (int)(90 * _scale) + (int)(18 * _scale))
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                }
            };
        }

        // ── Meter section ─────────────────────────────────────────────────────
        private void BuildMeterSection(int W, int H, int cardPad)
        {
            int headerH    = (int)(90 * _scale);
            // sectionTop: moved down to give breathing room from the red flare line
            int sectionTop = cardPad + headerH + (int)(45 * _scale);
            
            // Sizing constants
            int footerH     = (int)(44 * _scale);
            int btnAreaH    = (int)(90 * _scale);
            int dbCtrlH     = (int)(50 * _scale);  // Increased to match device button height
            int lblH        = (int)(34 * _scale); // Increased height to prevent font cropping at the top
            int sectionLblH = (int)(28 * _scale);

            // Meter diameter: fill available width evenly across 4 slots
            int innerPad    = cardPad + (int)(24 * _scale);
            int usableW     = W - innerPad * 2;
            int meterSpacing = usableW / 4;
            int meterDiam   = (int)(meterSpacing * 0.95f);
            meterDiam = Math.Max(140, Math.Min((int)(340 * _scale), meterDiam));

            // Vertically center only the meters block
            int topBoundary = sectionTop + sectionLblH;
            int bottomBoundary = H - btnAreaH - footerH - (int)(10 * _scale);
            int availableH = bottomBoundary - topBoundary;
            
            // Meter panel height: full diameter to show full inner circle
            int meterPanelH = meterDiam;
            
            int spacingToCtrl = (int)(10 * _scale); // gap between meter panel and volume controls
            int blockH = meterPanelH + spacingToCtrl + dbCtrlH;
            
            int meterTop = topBoundary + (availableH - blockH) / 2;

            // ── Section labels ──────────────────────────────────────────────────
            // Left label spans over meters 0+1, right label spans over meters 2+3
            BuildSectionLabel(innerPad, sectionTop, meterSpacing * 2,
                              "WHAT THE ", "AGENT", " HEARS",
                              Color.FromArgb(195, 195, 205), METER_BLUE);

            BuildSectionLabel(innerPad + meterSpacing * 2, sectionTop, meterSpacing * 2,
                              "WHAT THE ", "CUSTOMER", " HEARS",
                              Color.FromArgb(195, 195, 205), METER_PURPLE);

            // NO vertical divider line — sections separated by natural spacing only

            // ── 4 Meters ─────────────────────────────────────────────────────────────
            // 0: Customer Voice (RED)       — left side (AGENT HEARS)
            // 1: Agent Recordings (BLUE)    — left side (AGENT HEARS)
            // 2: Agent Voice (PURPLE)       — right side (CUSTOMER HEARS)
            // 3: Agent Recordings (GREEN)   — right side
            string[] labels = { "CUSTOMER VOICE", "AGENT RECORDINGS", "AGENT VOICE", "AGENT RECORDINGS" };
            Color[]  colors = { METER_RED, METER_BLUE, METER_PURPLE, METER_GREEN };
            string[] keys   = { "customerVoice", "agentScript_left", "myMicLevel", "agentScript" };

            for (int i = 0; i < 4; i++)
            {
                // Center each meter within its equal slot
                int mx = innerPad + i * meterSpacing + (meterSpacing - meterDiam) / 2;
                int my = meterTop;

                // Circular meter panel (label text drawn inside the panel to avoid clipping issues)
                BuildCircularMeter(mx, my, meterDiam, colors[i], keys[i], labels[i]);

                // [–] [dB] [+] controls
                int ctrlY = my + meterPanelH + spacingToCtrl;
                BuildDbControl(mx, ctrlY, meterDiam, colors[i], i);
            }
        }

        // ── Section label with colored keyword ────────────────────────────────
        private void BuildSectionLabel(int x, int y, int w,
                                       string prefix, string keyword, string suffix,
                                       Color baseColor, Color keyColor)
        {
            // We render this as a single owner-draw panel for exact color control
            var font     = new Font("Segoe UI", SF(15f), FontStyle.Bold);
            int prefW    = TextRenderer.MeasureText(prefix,  font).Width;
            int keyW     = TextRenderer.MeasureText(keyword, font).Width;
            int sufW     = TextRenderer.MeasureText(suffix,  font).Width;
            int totalTW  = prefW + keyW + sufW;
            int textH    = TextRenderer.MeasureText(keyword, font).Height;

            // Left divider line + text + right divider line
            var panel = new Panel
            {
                Bounds    = new Rectangle(x, y, w, textH + (int)(8 * _scale)),
                BackColor = Color.Transparent
            };
            string pref = prefix, kw = keyword, suf = suffix;
            Color bc = baseColor, kc = keyColor;
            panel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int pw = panel.Width;
                int ph = panel.Height;
                int textY = (ph - textH) / 2;

                // Center text block
                int startX = (pw - totalTW) / 2;
                int lineY  = ph / 2 + (int)(1 * _scale);
                int textGap = (int)(15 * _scale); // gap between text and line
                int edgeGap = (int)(20 * _scale); // gap at the outer edges of the section

                // Draw thin, subtle glowing lines on both sides
                Action<int, int> drawSubtleLine = (x1, x2) => {
                    if (x2 <= x1) return;
                    using (var gp1 = new Pen(Color.FromArgb(100, kc), 2f * _scale)) g.DrawLine(gp1, x1, lineY, x2, lineY);
                    using (var gp2 = new Pen(Color.FromArgb(220, kc), 1f * _scale)) g.DrawLine(gp2, x1, lineY, x2, lineY);
                };

                // Left line (indented from left edge to leave a gap in the center of the screen)
                drawSubtleLine(edgeGap, startX - textGap);

                // Right line (indented from right edge)
                drawSubtleLine(startX + totalTW + textGap, pw - edgeGap);

                // Text segments
                TextRenderer.DrawText(g, pref, font, new Point(startX, textY), bc);
                TextRenderer.DrawText(g, kw,   font, new Point(startX + prefW, textY), kc);
                TextRenderer.DrawText(g, suf,  font, new Point(startX + prefW + keyW, textY), bc);
            };
            this.Controls.Add(panel);
        }

        // ── Circular meter panel ──────────────────────────────────────────────
        private Panel BuildCircularMeter(int x, int y, int diam, Color meterColor, string key, string labelText)
        {
            // Panel height: full diameter
            int panelHeight = diam;
            var panel = new DoubleBufferedPanel
            {
                Bounds    = new Rectangle(x, y, diam, panelHeight),
                BackColor = Color.Transparent,
                Tag       = key
            };

            Color mc = meterColor;
            panel.Paint += (s, e) => DrawDialMeter(e.Graphics, (Panel)s, mc, key, labelText);
            this.Controls.Add(panel);

            switch (key)
            {
                case "myMicLevel":      _myMicMeterLeft      = panel; break;
                case "customerVoice":   _customerVoiceMeter  = panel; break;
                case "agentScript":     _agentScriptMeter    = panel; break;
                case "agentScript_left":_customerScriptMeter = panel; break;
            }
            return panel;
        }

        // ── Draw circular dial VU meter ───────────────────────────────────────
        private void DrawDialMeter(Graphics g, Panel panel, Color meterColor, string key, string labelText)
        {
            g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            int diam = panel.Width;
            int cx   = diam / 2;
            int cy   = diam / 2;

            // Block background stars from showing through the transparent panel.
            // Fill the circular meter face with the same deep-space background color.
            using (var bgPath = new GraphicsPath()) {
                bgPath.AddEllipse(1, 1, diam - 2, diam - 2);
                using (var bgBrush = new SolidBrush(Color.FromArgb(5, 5, 12)))
                    g.FillPath(bgBrush, bgPath);
            }

            // All arcs share the same span
            const float startAngle = 135f;
            const float sweepAngle = 270f;

            // Generous margin so outer glow never clips at panel edge
            int margin    = (int)(26 * _scale);
            int outerR    = diam / 2 - margin;
            int arcR      = outerR - (int)(28 * _scale);
            int innerR    = outerR - (int)(36 * _scale);

            int tickOuter = outerR;
            int tickMajor = outerR - (int)(18 * _scale);
            int tickMinor = outerR - (int)(9 * _scale);

            int   percent  = GetVolumePercent(key);
            float volLevel = Math.Min(GetLevel(key), percent / 100f);

            Func<float, bool> inArc = (theta) =>
                (((theta - startAngle) % 360f + 360f) % 360f) <= sweepAngle;

            // Helper for soft diffused neon glow
            Action<float, float, float, float, Color, bool, float, int> DrawGlowLine = (radius, startAng, sweep, coreThick, col, isFull, glowWidthMult, passes) => {
                // Fixed maximum width, with many passes for a smooth, diffused blend
                float maxGlowWidth = 24f * _scale * glowWidthMult;
                for (int p = passes; p > 0; p--) {
                    float thick = coreThick + (maxGlowWidth * ((float)p / passes));
                    int alpha = 180 / (p * p + 1); // smooth exponential falloff
                    if (alpha < 1) alpha = 1;
                    using (var glow = new Pen(Color.FromArgb(alpha, col), thick)) {
                        glow.StartCap = System.Drawing.Drawing2D.LineCap.Round; glow.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                        if (isFull) g.DrawEllipse(glow, cx - radius, cy - radius, radius * 2, radius * 2);
                        else g.DrawArc(glow, cx - radius, cy - radius, radius * 2, radius * 2, startAng, sweep);
                    }
                }
                // Solid core
                using (var core = new Pen(Color.FromArgb(220, col), coreThick)) {
                    core.StartCap = System.Drawing.Drawing2D.LineCap.Round; core.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    if (isFull) g.DrawEllipse(core, cx - radius, cy - radius, radius * 2, radius * 2);
                    else g.DrawArc(core, cx - radius, cy - radius, radius * 2, radius * 2, startAng, sweep);
                }
            };

            // 2. Outer Ring
            float outerStart = startAngle - 6f;
            float outerSweep = sweepAngle + 12f;
            DrawGlowLine(outerR, outerStart, outerSweep, 3.0f * _scale, meterColor, false, 1.8f, 24);

            // 3. Ticks pointing inward (with slight blur)
            int tickCount = 40;
            for (int t = 0; t <= tickCount; t++)
            {
                float tickGdi = startAngle + (t / (float)tickCount) * sweepAngle;
                double ma = tickGdi * Math.PI / 180.0;
                bool major = (t % 5 == 0);
                int tEnd = major ? tickMajor : tickMinor;
                float x1 = cx + (float)(tickOuter * Math.Cos(ma));
                float y1 = cy + (float)(tickOuter * Math.Sin(ma));
                float x2 = cx + (float)(tEnd * Math.Cos(ma));
                float y2 = cy + (float)(tEnd * Math.Sin(ma));

                // Tick glow
                using (var glowPen = new Pen(Color.FromArgb(major ? 40 : 15, meterColor), major ? 4f * _scale : 3f * _scale))
                    g.DrawLine(glowPen, x1, y1, x2, y2);
                
                // Tick core
                using (var corePen = new Pen(Color.FromArgb(major ? 220 : 120, meterColor), major ? 1.5f * _scale : 1f * _scale))
                    g.DrawLine(corePen, x1, y1, x2, y2);
            }

            // 4. Inner Ring
            DrawGlowLine(innerR, 0, 360, 0.5f * _scale, meterColor, true, 0.4f, 16);

            // Transparent depth color inside the meter
            using (var gp = new GraphicsPath()) {
                gp.AddEllipse(cx - innerR, cy - innerR, innerR * 2, innerR * 2);
                using (var pgb = new PathGradientBrush(gp)) {
                    pgb.CenterColor = Color.FromArgb(90, meterColor);
                    pgb.SurroundColors = new[] { Color.FromArgb(25, meterColor) };
                    g.FillPath(pgb, gp);
                }
            }

            // 5. Background track for progress
            float arcThick = 6f * _scale;
            // The user requested the unfilled progress bar to be transparent instead of solid color, so we don't draw it.
            // (Removed the trackPen drawing to make it completely empty/transparent)

            // 6. Glowing Progress Arc
            if (volLevel > 0.005f)
            {
                // Visual floor: ensure even tiny sounds are visible as at least a 3.5% arc
                float litSweep = Math.Max(sweepAngle * 0.035f, sweepAngle * volLevel);
                
                // Diffused progress glow
                int glowPasses = 16;
                float maxArcGlow = 32f * _scale;
                for (int p = glowPasses; p > 0; p--) {
                    float thick = arcThick + (maxArcGlow * ((float)p / glowPasses));
                    int alpha = 160 / (p * p + 1); // smooth falloff
                    if (alpha < 1) alpha = 1;
                    using (var glow = new Pen(Color.FromArgb(alpha, meterColor), thick)) {
                        glow.StartCap = System.Drawing.Drawing2D.LineCap.Round; glow.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                        g.DrawArc(glow, cx - arcR, cy - arcR, arcR * 2, arcR * 2, startAngle, litSweep);
                    }
                }
                
                // Bright color core
                using (var gp2 = new Pen(Color.FromArgb(220, meterColor), arcThick)) {
                    gp2.StartCap = System.Drawing.Drawing2D.LineCap.Round; gp2.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    g.DrawArc(gp2, cx - arcR, cy - arcR, arcR * 2, arcR * 2, startAngle, litSweep);
                }
                
                // Bright meter-color inner core
                var coreColor = Color.FromArgb(255,
                    Math.Min(255, meterColor.R + 80),
                    Math.Min(255, meterColor.G + 80),
                    Math.Min(255, meterColor.B + 80));
                using (var gp3 = new Pen(coreColor, arcThick - 3f * _scale)) {
                    gp3.StartCap = System.Drawing.Drawing2D.LineCap.Round; gp3.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    g.DrawArc(gp3, cx - arcR, cy - arcR, arcR * 2, arcR * 2, startAngle, litSweep);
                }
            }

            // 7. Center percentage display = this channel's VOLUME SETTING (0–100%),
            //    the value driven by the – / + buttons (GetVolumePercent). This is a
            //    DIFFERENT value from the bouncing ring above (which is the live audio
            //    level, volLevel) — do not merge them. The number tracks the +/- buttons;
            //    the ring keeps reacting to live audio.
            int pctDisp = Math.Max(0, Math.Min(100, percent));
            string numStr = pctDisp.ToString();
            string supStr = "%";
            float numSize = SF(34f);
            float supSize = SF(15f);
            using (var numFont = new Font("Segoe UI", numSize, FontStyle.Bold))
            using (var supFont = new Font("Segoe UI", supSize, FontStyle.Bold))
            {
                var numSz = TextRenderer.MeasureText(numStr, numFont, Size.Empty, TextFormatFlags.NoPadding);
                var supSz = TextRenderer.MeasureText(supStr, supFont, Size.Empty, TextFormatFlags.NoPadding);
                int blockW = numSz.Width + supSz.Width + (int)(2 * _scale);
                int blockX = cx - blockW / 2;
                int numY   = cy - numSz.Height / 2;
                int supY   = numY + (int)(6 * _scale);
                TextRenderer.DrawText(g, numStr, numFont,
                    new Point(blockX, numY), meterColor, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, supStr, supFont,
                    new Point(blockX + numSz.Width + (int)(1 * _scale), supY),
                    Color.FromArgb(210, meterColor), TextFormatFlags.NoPadding);
            }

            // 8. Section Label (drawn directly on panel to prevent WinForms clipping artifacts)
            using (var lblFont = new Font("Segoe UI", SF(11f), FontStyle.Bold))
            {
                var szL = TextRenderer.MeasureText(labelText, lblFont);
                TextRenderer.DrawText(g, labelText, lblFont,
                    new Point(cx - szL.Width / 2, diam - szL.Height), Color.White);
            }
        }

        // ── [–] [dB] [+] control row ──────────────────────────────────────────
        private void BuildDbControl(int meterX, int y, int meterW, Color accentColor, int channelIndex)
        {
            int btnW   = (int)(48 * _scale);
            int btnH   = (int)(46 * _scale);
            int valW   = (int)(159 * _scale); // combined previous width + gaps
            int totalW = btnW * 2 + valW;
            int startX = meterX + (meterW - totalW) / 2;

            int ch = channelIndex;

            // Render a single unified box that contains the label and the +/- buttons inside of it
            var containerPnl = new DoubleBufferedPanel {
                Bounds = new Rectangle(startX, y, totalW, btnH),
                BackColor = Color.Transparent
            };

            Color idleBorder = accentColor;
            Color currentBorder = idleBorder;
            
            containerPnl.Paint += (s, e) => {
                var gg = e.Graphics;
                gg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var r = new Rectangle(1, 1, containerPnl.Width - 3, containerPnl.Height - 3);
                using (var path = RoundedRect(r, (int)(8 * _scale))) {
                    using (var gp1 = new Pen(Color.FromArgb(60, accentColor), 4f))
                        gg.DrawPath(gp1, path);
                    using (var pen = new Pen(currentBorder, 1.5f))
                        gg.DrawPath(pen, path);
                }

                // Volume Text in center
                var font = new Font("Segoe UI", SF(12f), FontStyle.Bold);
                var textRect = new Rectangle(btnW, 0, valW, btnH);
                TextRenderer.DrawText(gg, "VOLUME", font, textRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                font.Dispose();
            };
            this.Controls.Add(containerPnl);

            // Create invisible transparent overlay buttons for the + and - click zones
            Func<int, int, int, string, DoubleBufferedPanel> makeBtn = (bx, bw, bh, txt) =>
            {
                var pnl = new DoubleBufferedPanel {
                    Bounds = new Rectangle(bx, 0, bw, bh),
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };

                Color hoverFill = Color.FromArgb(40, accentColor);
                Color downFill = Color.FromArgb(70, accentColor);
                Color currentFill = Color.Transparent;

                void repaint() { pnl.Invalidate(); containerPnl.Invalidate(); }

                pnl.MouseEnter += (s, e) => { currentFill = hoverFill; repaint(); };
                pnl.MouseLeave += (s, e) => { currentFill = Color.Transparent; repaint(); };
                pnl.MouseDown  += (s, e) => { currentFill = downFill; repaint(); };
                pnl.MouseUp    += (s, e) => { currentFill = hoverFill; repaint(); };

                pnl.Paint += (s, e) => {
                    var gg = e.Graphics;
                    gg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    
                    var rBtn = new Rectangle(1, 1, pnl.Width - 3, pnl.Height - 3);
                    using (var path = RoundedRect(rBtn, (int)(8 * _scale))) {
                        if (currentFill != Color.Transparent) {
                            using (var brush = new SolidBrush(currentFill)) gg.FillPath(brush, path);
                        }
                        
                        // Draw glowing border for the button itself
                        using (var glow = new Pen(Color.FromArgb(60, accentColor), 4f))
                            gg.DrawPath(glow, path);
                        using (var pen = new Pen(idleBorder, 1.5f))
                            gg.DrawPath(pen, path);
                    }
                    
                    var font = new Font("Segoe UI", SF(20f), FontStyle.Bold);
                    var bounds = pnl.ClientRectangle;
                    bounds.Y -= (int)(2 * _scale);
                    
                    // Draw a subtle text glow
                    var textGlowColor = Color.FromArgb(100, accentColor);
                    TextRenderer.DrawText(gg, txt, font, new Rectangle(bounds.X - 1, bounds.Y, bounds.Width, bounds.Height), textGlowColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(gg, txt, font, new Rectangle(bounds.X + 1, bounds.Y, bounds.Width, bounds.Height), textGlowColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(gg, txt, font, new Rectangle(bounds.X, bounds.Y - 1, bounds.Width, bounds.Height), textGlowColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(gg, txt, font, new Rectangle(bounds.X, bounds.Y + 1, bounds.Width, bounds.Height), textGlowColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    
                    // Draw the core text
                    TextRenderer.DrawText(gg, txt, font, bounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    font.Dispose();
                };

                containerPnl.Controls.Add(pnl);
                return pnl;
            };

            var minusPanel = makeBtn(0, btnW, btnH, "–");
            var plusPanel  = makeBtn(containerPnl.Width - btnW, btnW, btnH, "+");

            var lblVal = new Label { Visible = false, Bounds = new Rectangle(-500, -500, 10, 10) };
            switch (channelIndex)
            {
                case 0: _lblDbCustomerVoice   = lblVal; break;
                case 1: _lblDbCustomerScript  = lblVal; break;
                case 2: _lblDbAgentVoice      = lblVal; break;
                case 3: _lblDbAgentScript     = lblVal; break;
            }
            this.Controls.Add(lblVal);

            Action<DoubleBufferedPanel, int> wireBtn = (btn, delta) => {
                System.Windows.Forms.Timer repeatTimer = null;
                btn.MouseDown += (s, e) => {
                    if (e.Button != MouseButtons.Left) return;
                    AdjustDb(ch, delta, lblVal);
                    containerPnl.Invalidate();
                    
                    if (repeatTimer != null) { repeatTimer.Stop(); repeatTimer.Dispose(); }
                    repeatTimer = new System.Windows.Forms.Timer { Interval = 450 }; // Initial delay
                    repeatTimer.Tick += (ts, te) => {
                        repeatTimer.Interval = 40; // Faster repeat
                        AdjustDb(ch, delta, lblVal);
                        containerPnl.Invalidate();
                    };
                    repeatTimer.Start();
                };
                EventHandler stopAction = (s, e) => {
                    if (repeatTimer != null) { repeatTimer.Stop(); repeatTimer.Dispose(); repeatTimer = null; }
                };
                btn.MouseUp += (s, e) => stopAction(s, e);
                btn.MouseLeave += (s, e) => stopAction(s, e);
            };

            wireBtn(minusPanel, -1);
            wireBtn(plusPanel,  1);
        }

        private string GetDbText(int channelIndex)
        {
            int db;
            switch (channelIndex)
            {
                case 0:  db = _dbCustomerVoice;  break;
                case 1:  db = _dbCustomerScript; break;
                case 2:  db = _dbAgentVoice;     break;
                case 3:  db = _dbAgentScript;    break;
                default: db = -3;                break;
            }
            return db.ToString();
        }

        private void AdjustDb(int channelIndex, int delta, Label lblVal)
        {
            switch (channelIndex)
            {
                case 0:
                    _dbCustomerVoice = Math.Max(-20, Math.Min(6, _dbCustomerVoice + delta));
                    _customerVoiceVolume = DbToLinear(_dbCustomerVoice);
                    AppSettings.Instance.SpeakerSystemVolume = DbToPercent(_dbCustomerVoice);
                    ApplyCustomerVoiceListenEndpointVolume();
                    _customerVoiceMeter?.Invalidate();
                    break;
                case 1:
                    _dbCustomerScript = Math.Max(-20, Math.Min(6, _dbCustomerScript + delta));
                    AppSettings.Instance.SetVolume("agentScript", DbToLinear(_dbCustomerScript));
                    LocalBridgeServer.Instance.SetVolume("agent", DbToPercent(_dbCustomerScript));
                    _customerScriptMeter?.Invalidate();
                    break;
                case 2:
                    _dbAgentVoice = Math.Max(-20, Math.Min(6, _dbAgentVoice + delta));
                    if (_activeMicDevice != null)
                        try { _activeMicDevice.AudioEndpointVolume.MasterVolumeLevelScalar = DbToLinear(_dbAgentVoice); } catch { }
                    AppSettings.Instance.MicSystemVolume = DbToPercent(_dbAgentVoice);
                    _myMicMeterLeft?.Invalidate();
                    break;
                case 3:
                    _dbAgentScript = Math.Max(-20, Math.Min(6, _dbAgentScript + delta));
                    AppSettings.Instance.SetVolume("customerScript", DbToLinear(_dbAgentScript));
                    LocalBridgeServer.Instance.SetVolume("customer", DbToPercent(_dbAgentScript));
                    _agentScriptMeter?.Invalidate();
                    break;
            }
            // Save settings in background to prevent UI stutter during rapid clicks
            Task.Run(() => { try { AppSettings.Instance.Save(); } catch { } });
        }

        private static float DbToLinear(int db) => (float)Math.Pow(10.0, db / 20.0);
        
        // Map the allowed dB range (-20 to +6) to 0% - 100%
        private static int DbToPercent(int db)
        {
            float p = ((db + 20f) / 26f) * 100f;
            return (int)Math.Max(0, Math.Min(100, Math.Round(p)));
        }

        private void ApplyButtonGlow(Button btn, Color accent, bool idle)
        {
            // Idle: very subtle border glow only
            int alpha = idle ? 55 : 160;
            btn.FlatAppearance.BorderColor = Color.FromArgb(alpha, accent);
        }

        private void AttachHoverGlow(Button btn, Color accent)
        {
            btn.MouseEnter += (s, e) =>
            {
                btn.FlatAppearance.BorderColor = Color.FromArgb(200, accent);
                btn.BackColor = Color.FromArgb(45, accent);
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.FlatAppearance.BorderColor = Color.FromArgb(55, accent);
                btn.BackColor = Color.FromArgb(28, 28, 42);
            };
        }

        // ── Device buttons ────────────────────────────────────────────────────
        private void BuildDeviceButtons(int W, int H, int cardPad)
        {
            int footerH  = (int)(44 * _scale);
            int btnAreaH = (int)(90 * _scale);
            int btnH     = (int)(70 * _scale);
            int btnW     = (int)(Math.Min(W * 0.36f, 400 * _scale));
            int btnY     = H - footerH - btnAreaH + (btnAreaH - btnH) / 2 - (int)(25 * _scale);
            int gap      = (int)(30 * _scale);
            int totalBtnW = btnW * 2 + gap;
            int btnX0    = (W - totalBtnW) / 2;

            DoubleBufferedPanel BuildDeviceButton(int bx, int by, int bw, int bh, string text, Color accent, Action<Control> onClick, string iconType)
            {
                var pnl = new DoubleBufferedPanel {
                    Bounds = new Rectangle(bx, by, bw, bh),
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                    Tag = text
                };
                Color idleBorder = accent;
                Color hoverBorder = Color.White;
                Color currentBorder = idleBorder;
                Color currentFill = Color.Transparent;
                
                pnl.MouseEnter += (s, e) => { currentBorder = hoverBorder; currentFill = Color.FromArgb(40, accent); pnl.Invalidate(); };
                pnl.MouseLeave += (s, e) => { currentBorder = idleBorder; currentFill = Color.Transparent; pnl.Invalidate(); };
                pnl.MouseDown += (s, e) => { currentFill = Color.FromArgb(70, accent); pnl.Invalidate(); };
                pnl.MouseUp += (s, e) => { currentFill = Color.FromArgb(40, accent); pnl.Invalidate(); };
                
                pnl.Click += (s, e) => onClick(pnl);
                
                pnl.Paint += (s, e) => {
                    var g = e.Graphics;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    var r = new Rectangle(2, 2, pnl.Width - 5, pnl.Height - 5);
                    using (var path = RoundedRect(r, (int)(8 * _scale))) {
                        if (currentFill != Color.Transparent) {
                            using (var b = new SolidBrush(currentFill)) g.FillPath(b, path);
                        }
                        // Outer soft glow passes
                        using (var g1 = new Pen(Color.FromArgb(30, accent), 28f)) g.DrawPath(g1, path);
                        using (var g2 = new Pen(Color.FromArgb(60, accent), 14f)) g.DrawPath(g2, path);
                        using (var glow = new Pen(Color.FromArgb(100, accent), 5f)) g.DrawPath(glow, path);
                        using (var pen = new Pen(currentBorder, 2f)) g.DrawPath(pen, path);
                    }
                    var font = new Font("Segoe UI", SF(16f), FontStyle.Bold);
                    
                    int iconSz = (int)(26 * _scale);
                    int ix = (int)(22 * _scale);
                    
                    // Shift text to the right so it never overlaps the icon
                    int textLeft = ix + iconSz + (int)(10 * _scale);
                    var textRect = new Rectangle(textLeft, 0, pnl.Width - textLeft - (int)(10 * _scale), pnl.Height);
                    TextRenderer.DrawText(g, pnl.Tag as string, font, textRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    font.Dispose();

                    // Draw custom vector icon on the left
                    if (!string.IsNullOrEmpty(iconType)) {
                        int iy = (pnl.Height - iconSz) / 2;
                        using (var pen = new Pen(Color.White, 2f * _scale) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
                        using (var brush = new SolidBrush(Color.White)) {
                            if (iconType == "mic") {
                                float cx = ix + iconSz / 2f;
                                float capW = 8f * _scale;
                                float capH = 12f * _scale;
                                float capR = capW / 2f;
                                float cy = iy + 2f * _scale;
                                
                                // Perfect pill shape using floating-point GraphicsPath to avoid cut-off flat tops
                                using (var gp = new GraphicsPath()) {
                                    gp.AddArc(cx - capR, cy, capW, capW, 180, 180);
                                    gp.AddArc(cx - capR, cy + capH - capW, capW, capW, 0, 180);
                                    gp.CloseFigure();
                                    g.DrawPath(pen, gp);
                                }
                                
                                g.DrawArc(pen, cx - capR - 4f*_scale, cy + capH/2f - 2f*_scale, capW + 8f*_scale, capH, 0, 180);
                                g.DrawLine(pen, cx, cy + capH + 4f*_scale, cx, iy + iconSz - 2f*_scale);
                                g.DrawLine(pen, cx - 5f*_scale, iy + iconSz - 2f*_scale, cx + 5f*_scale, iy + iconSz - 2f*_scale);
                            }
                            else if (iconType == "speaker") {
                                float left = ix + 2f * _scale;
                                float cy = iy + iconSz / 2f;
                                g.FillRectangle(brush, left, cy - 3f*_scale, 5f*_scale, 6f*_scale);
                                PointF[] cone = {
                                    new PointF(left + 5f*_scale, cy - 3f*_scale),
                                    new PointF(left + 12f*_scale, cy - 8f*_scale),
                                    new PointF(left + 12f*_scale, cy + 8f*_scale),
                                    new PointF(left + 5f*_scale, cy + 3f*_scale)
                                };
                                g.FillPolygon(brush, cone);
                                g.DrawArc(pen, left + 6f*_scale, cy - 5f*_scale, 10f*_scale, 10f*_scale, -60, 120);
                                g.DrawArc(pen, left + 6f*_scale, cy - 9f*_scale, 18f*_scale, 18f*_scale, -60, 120);
                            }
                        }
                    }
                };
                this.Controls.Add(pnl);
                return pnl;
            }

            var btnMic = BuildDeviceButton(btnX0, btnY, btnW, btnH, "SELECT MICROPHONE   \u25BC", METER_RED, ShowMicDropdown, "mic");
            var btnSpk = BuildDeviceButton(btnX0 + btnW + gap, btnY, btnW, btnH, "SELECT SPEAKER   \u25BC", METER_BLUE, ShowSpeakerDropdown, "speaker");

            _cboMic     = new ComboBox { Visible = false, Location = new Point(-500, -500), Width = 1 };
            _cboHeadset = new ComboBox { Visible = false, Location = new Point(-500, -500), Width = 1 };
            this.Controls.Add(_cboMic);
            this.Controls.Add(_cboHeadset);

            _cboMic.SelectedIndexChanged += (s, e) => {
                if (_cboMic.SelectedIndex >= 0) {
                    btnMic.Tag = TruncateDevice(_cboMic.Text, 22) + "   \u25BC";
                    btnMic.Invalidate();
                }
            };
            _cboHeadset.SelectedIndexChanged += (s, e) => {
                if (_cboHeadset.SelectedIndex >= 0) {
                    btnSpk.Tag = TruncateDevice(_cboHeadset.Text, 22) + "   \u25BC";
                    btnSpk.Invalidate();
                }
            };
        }

        private string TruncateDevice(string name, int maxLen)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return name.Length > maxLen ? name.Substring(0, maxLen) + "…" : name;
        }

        private void ShowMicDropdown(Control anchor)
        {
            var ctx = new ContextMenuStrip();
            ctx.Renderer  = new ModernMenuRenderer(METER_RED);
            ctx.BackColor = Color.FromArgb(18, 18, 28);
            ctx.ForeColor = Color.White;
            ctx.ShowImageMargin = false;
            ctx.Font      = new Font("Segoe UI", SF(11f), FontStyle.Regular);

            foreach (string item in _cboMic.Items)
            {
                string it = item;
                var mi = new ToolStripMenuItem(it) { 
                    ForeColor = Color.White,
                    Height = (int)(32 * _scale),
                    Padding = new Padding(10, 5, 10, 5)
                };
                mi.Click += (s, e) =>
                {
                    int idx = _cboMic.Items.IndexOf(it);
                    if (idx >= 0) _cboMic.SelectedIndex = idx;
                };
                ctx.Items.Add(mi);
            }
            ctx.Show(anchor, new Point(0, anchor.Height + 2));
        }

        private void ShowSpeakerDropdown(Control anchor)
        {
            var ctx = new ContextMenuStrip();
            ctx.Renderer  = new ModernMenuRenderer(METER_BLUE);
            ctx.BackColor = Color.FromArgb(18, 18, 28);
            ctx.ForeColor = Color.White;
            ctx.ShowImageMargin = false;
            ctx.Font      = new Font("Segoe UI", SF(11f), FontStyle.Regular);

            foreach (string item in _cboHeadset.Items)
            {
                string it = item;
                var mi = new ToolStripMenuItem(it) { 
                    ForeColor = Color.White,
                    Height = (int)(32 * _scale),
                    Padding = new Padding(10, 5, 10, 5)
                };
                mi.Click += (s, e) =>
                {
                    int idx = _cboHeadset.Items.IndexOf(it);
                    if (idx >= 0) _cboHeadset.SelectedIndex = idx;
                };
                ctx.Items.Add(mi);
            }
            ctx.Show(anchor, new Point(0, anchor.Height + 2));
        }

        // ── Custom Dropdown Renderer ──────────────────────────────────────────
        public class ModernMenuRenderer : ToolStripProfessionalRenderer
        {
            private Color _accent;
            public ModernMenuRenderer(Color accent) : base(new ModernMenuColorTable()) { _accent = accent; }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                
                // Glowing border layers
                using (var p1 = new Pen(Color.FromArgb(60, _accent), 3f)) g.DrawRectangle(p1, r);
                using (var p2 = new Pen(Color.FromArgb(180, _accent), 1f)) g.DrawRectangle(p2, r);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (e.Item.Selected)
                {
                    var g = e.Graphics;
                    var r = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
                    using (var brush = new SolidBrush(Color.FromArgb(60, _accent)))
                        g.FillRectangle(brush, r);
                    using (var pen = new Pen(Color.FromArgb(150, _accent), 1f))
                        g.DrawRectangle(pen, r);
                }
            }
        }

        public class ModernMenuColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Color.FromArgb(18, 18, 28);
            public override Color MenuBorder => Color.Transparent; // Handled by renderer
            public override Color MenuItemSelected => Color.Transparent; // Handled by renderer
            public override Color MenuItemSelectedGradientBegin => Color.Transparent;
            public override Color MenuItemSelectedGradientEnd => Color.Transparent;
        }

        // ── Footer ────────────────────────────────────────────────────────────
        private void BuildFooter(int W, int H, int cardPad)
        {
            int footerH = (int)(44 * _scale);
            int fy      = H - cardPad - footerH;

            _lblFooterCenter = new Label
            {
                Text      = $"One United Global LLC 2026.  V {APP_VERSION}",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(13f), FontStyle.Regular),
                AutoSize  = true
            };
            this.Controls.Add(_lblFooterCenter);
            int fcW = TextRenderer.MeasureText(_lblFooterCenter.Text, _lblFooterCenter.Font).Width;
            int fcH = TextRenderer.MeasureText(_lblFooterCenter.Text, _lblFooterCenter.Font).Height;
            _lblFooterCenter.Location = new Point((W - fcW) / 2, fy + (footerH - fcH) / 2);

            // W.O.T. 31 ! — centered below the main footer line
            var lblWot = new Label
            {
                Text      = "W.O.T. 31 !",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(12f), FontStyle.Regular),
                AutoSize  = true
            };
            this.Controls.Add(lblWot);
            int wotW = TextRenderer.MeasureText(lblWot.Text, lblWot.Font).Width;
            lblWot.Location = new Point((W - wotW) / 2, fy + (footerH - fcH) / 2 + fcH + (int)(2 * _scale));
        }

        // ── Hidden TrackBars (audio logic compatibility) ───────────────────────
        private void BuildHiddenTrackBars()
        {
            // These are required by the audio logic (StartAudioCapture, PopulateDevices).
            // They are hidden off-screen and never shown.
            _trkMicVol = new TrackBar
            {
                Minimum = 0, Maximum = 100,
                Value   = Math.Max(0, Math.Min(100, AppSettings.Instance.MicSystemVolume)),
                Bounds  = new Rectangle(-2000, -2000, 100, 20),
                Visible = false
            };
            _lblMicVol = new Label { Bounds = new Rectangle(-2000, -2000, 60, 20), Visible = false };
            _trkSpeakerVol = new TrackBar
            {
                Minimum = 0, Maximum = 100,
                Value   = Math.Max(0, Math.Min(100, AppSettings.Instance.SpeakerSystemVolume)),
                Bounds  = new Rectangle(-2000, -2000, 100, 20),
                Visible = false
            };
            _lblSpeakerVol = new Label { Bounds = new Rectangle(-2000, -2000, 60, 20), Visible = false };

            int agentDefault    = (int)(AppSettings.Instance.GetVolume("agentScript",    0.90f) * 100);
            int customerDefault = (int)(AppSettings.Instance.GetVolume("customerScript", 0.85f) * 100);
            agentDefault    = Math.Max(0, Math.Min(100, agentDefault));
            customerDefault = Math.Max(0, Math.Min(100, customerDefault));

            _trkAgentScriptVol = new TrackBar
            {
                Minimum = 0, Maximum = 100, Value = agentDefault,
                Bounds  = new Rectangle(-2000, -2000, 100, 20), Visible = false
            };
            _lblAgentScriptVol = new Label { Bounds = new Rectangle(-2000, -2000, 60, 20), Visible = false };
            _trkCustomerScriptVol = new TrackBar
            {
                Minimum = 0, Maximum = 100, Value = customerDefault,
                Bounds  = new Rectangle(-2000, -2000, 100, 20), Visible = false
            };
            _lblCustomerScriptVol = new Label { Bounds = new Rectangle(-2000, -2000, 60, 20), Visible = false };

            this.Controls.AddRange(new Control[] {
                _trkMicVol, _lblMicVol, _trkSpeakerVol, _lblSpeakerVol,
                _trkAgentScriptVol, _lblAgentScriptVol,
                _trkCustomerScriptVol, _lblCustomerScriptVol
            });
        }

        // ── Level getter ──────────────────────────────────────────────────────
        private float GetLevel(string key)
        {
            switch (key)
            {
                // Violet = agent live mic toward customer. During script playback, mic pass-through is
                // muted so the customer does not hear the agent — hide meter motion so UI matches reality.
                case "myMicLevel":      return LocalBridgeServer.Instance.IsPlaying ? 0f : _micLevel;
                case "customerVoice":
                    {
                        float v = _customerVoiceLevel;
                        if (LocalBridgeServer.Instance.IsPlaying)
                        {
                            if (v < RedMeterPlaybackNoiseGate) return 0f;
                        }
                        else
                        {
                            if (v < RedMeterIdleNoiseGate) return 0f;
                        }
                        return v;
                    }
                case "agentScript":     return _agentScriptLevel;
                case "agentScript_left":return _customerScriptLevel;
                default: return 0f;
            }
        }

        private int GetVolumePercent(string key)
        {
            switch (key)
            {
                case "customerVoice":   return DbToPercent(_dbCustomerVoice);
                // Bridge script rings: DbToPercent(-20) is 0 — would clamp arc off even when levels fire.
                case "agentScript_left":return Math.Max(1, DbToPercent(_dbCustomerScript));
                case "myMicLevel":      return DbToPercent(_dbAgentVoice);
                case "agentScript":     return Math.Max(1, DbToPercent(_dbAgentScript));
                default: return 50;
            }
        }

        // ── Tray icon ─────────────────────────────────────────────────────────
        private void SetupTrayIcon()
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Open ONE Voice", null, (s, e) => RestoreFromTray());
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("Exit", null, (s, e) => Application.Exit());
            _trayIcon = new NotifyIcon
            {
                Text             = "ONE Voice Solution",
                ContextMenuStrip = _trayMenu,
                Visible          = true
            };
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "one_logo.ico");
            _trayIcon.Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
            _trayIcon.DoubleClick += (s, e) => RestoreFromTray();
            // Minimize stays in taskbar — no tray hide
            this.Resize += (s, e) =>
            {
                // Window stays in taskbar when minimized
            };
        }

        private void RestoreFromTray()
        {
            this.ShowInTaskbar = true;
            this.WindowState   = FormWindowState.Normal;
            this.BringToFront();
            this.Activate();
        }

        // Rebuild all controls on restore from minimize — prevents GDI corruption (broken red boxes)
        private bool _wasMinimized = false;
        protected override void WndProc(ref Message m)
        {
            const int WM_SIZE        = 0x0005;
            const int SIZE_MINIMIZED = 1;
            const int SIZE_RESTORED  = 0;
            const int SIZE_MAXIMIZED = 2;
            base.WndProc(ref m);
            if (m.Msg == WM_SIZE)
            {
                int sizeType = m.WParam.ToInt32();
                if (sizeType == SIZE_MINIMIZED)
                {
                    _wasMinimized = true;
                }
                else if (_wasMinimized && (sizeType == SIZE_RESTORED || sizeType == SIZE_MAXIMIZED))
                {
                    _wasMinimized = false;
                    this.BeginInvoke(new Action(() =>
                    {
                        // Clear background cache so it rebuilds fresh after restore
                        _bgCache?.Dispose();
                        _bgCache = null;
                        _bgCacheSize = Size.Empty;

                        // Stop meter timer FIRST so it cannot fire Invalidate on disposed controls
                        _meterTimer?.Stop();

                        // Null out all meter + UI field references before disposing controls
                        // so the timer tick cannot call .Invalidate() on a disposed handle
                        _myMicMeterLeft     = null;
                        _customerVoiceMeter = null;
                        _agentScriptMeter   = null;
                        _customerScriptMeter = null;
                        _logoBox            = null;
                        _lblTagline         = null;
                        _lblAgentName       = null;
                        _lblFooterCenter    = null;
                        _btnClose           = null;
                        _btnMinimize        = null;

                        this.SuspendLayout();

                        // Dispose and remove all existing controls
                        var controls = new System.Windows.Forms.Control[this.Controls.Count];
                        this.Controls.CopyTo(controls, 0);
                        this.Controls.Clear();
                        foreach (var c in controls)
                        {
                            try { if (c is PictureBox pb) pb.Image = null; } catch { }
                            try { if (c != null && !c.IsDisposed) c.Dispose(); } catch { }
                        }

                        // Rebuild fresh UI
                        BuildUI();
                        // Re-populate audio devices — BuildUI creates new _cboMic/_cboHeadset
                        // combo boxes that are empty until PopulateDevices() fills them.
                        // Without this, the mic/speaker dropdown buttons show nothing after restore.
                        PopulateDevices();
                        // Bug 3 fix: do NOT restart loopback captures while a script is actively
                        // playing. StartLoopbackCapture() disposes and recreates all three
                        // WasapiLoopbackCapture instances, which interrupts the RED meter sources
                        // mid-call. Defer until playback is complete (_isCallActive cleared by
                        // OnPlaybackStopped). Device numbers were already updated by PopulateDevices().
                        if (!_isCallActive)
                            StartLoopbackCapture();
                        this.ResumeLayout(true);
                        this.Invalidate(true);
                        this.Refresh();

                        // Restart meter timer now that new controls are in place
                        _meterTimer?.Start();
                    }));
                }
            }
        }

        // ── Meter timer ───────────────────────────────────────────────────────
        private void SetupMeterTimer()
        {
            _meterTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _meterTimer.Tick += (s, e) =>
            {
                if (_micLevel > 0)
                {
                    _micLevel = Math.Max(0f, _micLevel - 0.02f);
                    _myMicMeterLeft?.Invalidate();
                }
                if (_customerVoiceLevel > 0)
                {
                    _customerVoiceLevel = Math.Max(0f, _customerVoiceLevel - 0.10f);
                    _customerVoiceMeter?.Invalidate();
                }
                if (_agentScriptLevel > 0)
                {
                    _agentScriptLevel = Math.Max(0f, _agentScriptLevel - 0.008f);
                    _agentScriptMeter?.Invalidate();
                }
                if (_customerScriptLevel > 0)
                {
                    _customerScriptLevel = Math.Max(0f, _customerScriptLevel - 0.008f);
                    _customerScriptMeter?.Invalidate();
                }

                // Periodic mic pass-through restart — prevents USB clock drift from accumulating
                // into audible garble. Drift between mic device clock and VB-Cable clock builds up
                // ~1ms/min; at ~7 min the 250ms buffer overflows → garble. Restart every 4 min
                // resets the buffer to a clean state. Only restarts when no script is playing.
                if (_micPassWaveIn != null &&
                    !LocalBridgeServer.Instance.IsPlaying &&
                    _micPassLastRestart != DateTime.MinValue &&
                    (DateTime.UtcNow - _micPassLastRestart).TotalMilliseconds > MicPassDriftRestartMs)
                {
                    string micName = _activeMicDevice?.FriendlyName;
                    Log.Info("[PassThrough] Drift restart — flushing clock drift after 4 min.");
                    StopMicPassThrough();
                    if (!string.IsNullOrEmpty(micName))
                        StartMicPassThrough(micName);
                }
            };
            _meterTimer.Start();
        }

        // ── Audio capture (microphone) ────────────────────────────────────────
        private void StartAudioCapture(string deviceName = null)
        {
            try { _micCapture?.StopRecording(); } catch { }
            try { _micCapture?.Dispose(); }       catch { }
            _micCapture = null;
            StopMicPassThrough();

            try
            {
                var devices = _deviceEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                if (!devices.Any()) { Log.Warn("[Audio] No capture devices."); return; }
                string targetName = deviceName ?? (_cboMic?.Text) ?? AppSettings.Instance.MicDevice;
                MMDevice device   = null;
                if (!string.IsNullOrWhiteSpace(targetName))
                    device = devices.FirstOrDefault(d => d.FriendlyName == targetName);
                if (device == null)
                    device = devices.FirstOrDefault(d =>
                        !d.FriendlyName.Contains("CABLE") &&
                        !d.FriendlyName.Contains("VB-Audio") &&
                        !d.FriendlyName.Contains("Virtual")) ?? devices.First();
                Log.Info($"[Audio] Capturing: {device.FriendlyName}");
                _activeMicDevice = device;
                if (_trkMicVol != null)
                {
                    try
                    {
                        int savedPct = AppSettings.Instance.MicSystemVolume;
                        device.AudioEndpointVolume.MasterVolumeLevelScalar = savedPct / 100f;
                        if (_trkMicVol.InvokeRequired)
                            _trkMicVol.BeginInvoke(new Action(() => { _trkMicVol.Value = savedPct; if (_lblMicVol != null) _lblMicVol.Text = $"{savedPct}%"; }));
                        else { _trkMicVol.Value = savedPct; if (_lblMicVol != null) _lblMicVol.Text = $"{savedPct}%"; }
                    }
                    catch { }
                }

                _micCapture = new WasapiCapture(device, false);
                _micCapture.DataAvailable += (s, e) =>
                {
                    if (e.BytesRecorded < 4) return;
                    float max    = 0f;
                    int   stride = (_micCapture.WaveFormat.BitsPerSample == 16) ? 2 : 4;
                    double sumSq = 0;
                    int    n      = 0;
                    for (int i = 0; i + stride <= e.BytesRecorded; i += stride)
                    {
                        float sample = stride == 2
                            ? BitConverter.ToInt16(e.Buffer, i) / 32768f
                            : BitConverter.ToSingle(e.Buffer, i);
                        float a = Math.Abs(sample);
                        if (a > max) max = a;
                        sumSq += sample * sample;
                        n++;
                    }
                    _micLevel = Math.Min(1f, max * 3.25f);
                    if (n > 0)
                    {
                        float frameRms = (float)Math.Sqrt(sumSq / n);
                        if (frameRms > 0.001f)
                            _smoothedMicRmsLinear = frameRms * 0.1f + _smoothedMicRmsLinear * 0.9f;
                    }
                    if (++_bridgeLevelMatchThrottle >= 3)
                    {
                        _bridgeLevelMatchThrottle = 0;
                        float g = LocalBridgeServer.LevelMatchGainFromMicRms(_smoothedMicRmsLinear);
                        LocalBridgeServer.Instance.SetScriptLevelMatchGain(g);
                    }
                };
                _micCapture.StartRecording();
                StartMicPassThrough(device.FriendlyName);
            }
            catch (Exception ex) { Log.Warn($"[Audio] Capture failed: {ex.Message}"); }
        }

        private void StopMicPassThrough()
        {
            try { if (_micPassWaveIn != null) _micPassWaveIn.DataAvailable -= MicPassWaveIn_DataAvailable; } catch { }
            try { _micPassWaveIn?.StopRecording(); } catch { }
            try { _micPassWaveIn?.Dispose(); }       catch { }
            _micPassWaveIn = null;
            try { _micPassWaveOut?.Stop(); }    catch { }
            try { _micPassWaveOut?.Dispose(); } catch { }
            _micPassWaveOut = null;
            _micPassBuffer = null;
        }

        private void StartMicPassThrough(string deviceFriendlyName)
        {
            int cableNum = LocalBridgeServer.Instance.CableDeviceNumber;
            if (cableNum < 0)
            {
                Log.Warn("[PassThrough] VB-Cable not found — mic pass-through disabled.");
                return;
            }

            int waveInNum = FindWaveInDeviceNumber(deviceFriendlyName);
            if (waveInNum < 0)
            {
                Log.Warn($"[PassThrough] Could not find WaveIn device for '{deviceFriendlyName}'.");
                return;
            }

            try
            {
                _micPassWaveIn = new WaveInEvent
                {
                    DeviceNumber       = waveInNum,
                    WaveFormat         = new WaveFormat(48000, 16, 2),
                    BufferMilliseconds = 100   // larger WaveIn chunks = less sensitive to brief USB hiccups
                };
                _micPassBuffer = new BufferedWaveProvider(_micPassWaveIn.WaveFormat)
                {
                    DiscardOnBufferOverflow = true,
                    BufferDuration = TimeSpan.FromMilliseconds(500)  // 500ms headroom vs 250ms — absorbs USB stalls
                };
                _micPassWaveIn.DataAvailable += MicPassWaveIn_DataAvailable;

                _micPassWaveOut = new WaveOutEvent
                {
                    DeviceNumber   = cableNum,
                    DesiredLatency = 200  // 200ms vs 100ms — sustains playback during brief WaveIn stalls
                };
                _micPassWaveOut.Init(_micPassBuffer);
                _micPassWaveIn.StartRecording();
                _micPassWaveOut.Play();
                _micPassWaveOut.Volume = 1.0f;
                _micPassLastRestart = DateTime.UtcNow;

                Log.Info($"[PassThrough] ACTIVE: WaveIn #{waveInNum} ('{deviceFriendlyName}') → WaveOut #{cableNum} (CABLE)");
            }
            catch (Exception ex)
            {
                Log.Warn($"[PassThrough] Failed: {ex.Message}");
                StopMicPassThrough();
            }
        }

        private void MicPassWaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (_micPassBuffer == null || e.BytesRecorded < 2) return;

            // Reuse pre-allocated buffer — grow only when needed, never shrink.
            // Prevents ~192KB/sec of heap churn (20 callbacks/sec × ~9,600 bytes each) that
            // would accumulate GC pressure and cause audio dropouts on calls lasting 4+ hours.
            if (_micPassProcBuf.Length < e.BytesRecorded)
                _micPassProcBuf = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, _micPassProcBuf, 0, e.BytesRecorded);

            for (int i = 0; i + 1 < e.BytesRecorded; i += 2)
            {
                short sample = BitConverter.ToInt16(_micPassProcBuf, i);
                float x = sample / 32768f;

                // Conservative make-up gain + hard ceiling to avoid call-path overload artifacts.
                x *= MicPassThroughGain;
                if (x > MicPassThroughHardLimit) x = MicPassThroughHardLimit;
                else if (x < -MicPassThroughHardLimit) x = -MicPassThroughHardLimit;

                short y = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, (int)(x * 32767f)));
                // Direct bit-write: zero allocation vs BitConverter.GetBytes(y) which allocates
                // a byte[2] on every sample — at 48kHz stereo that was ~96,000 heap allocations/sec
                // (~3 MB/sec Gen0 garbage) causing GC pauses and progressive audio degradation on calls
                // lasting more than a few minutes. Little-endian layout matches BitConverter output.
                _micPassProcBuf[i]     = (byte)(y & 0xFF);
                _micPassProcBuf[i + 1] = (byte)((y >> 8) & 0xFF);
            }

            _micPassBuffer.AddSamples(_micPassProcBuf, 0, e.BytesRecorded);
        }

        private int FindWaveInDeviceNumber(string targetDeviceName)
        {
            if (string.IsNullOrEmpty(targetDeviceName)) return -1;
            string target = targetDeviceName.ToLower().Trim();
            Log.Info($"[PassThrough] Finding WaveIn match for '{targetDeviceName}'");
            int count   = WaveIn.DeviceCount;
            int bestIdx = -1;
            int bestLen = 0;
            for (int i = 0; i < count; i++)
            {
                string prod = WaveIn.GetCapabilities(i).ProductName.ToLower().Trim();
                if (target.Contains(prod) || prod.Contains(target))
                {
                    Log.Info($"[PassThrough] WaveIn #{i}: MATCH");
                    return i;
                }
                int common = 0;
                int minLen = Math.Min(target.Length, prod.Length);
                for (int c = 0; c < minLen; c++) { if (target[c] == prod[c]) common++; else break; }
                if (common > bestLen) { bestLen = common; bestIdx = i; }
            }
            if (bestIdx >= 0 && bestLen >= 8) return bestIdx;
            Log.Warn($"[PassThrough] No WaveIn match for '{targetDeviceName}'");
            return -1;
        }

        // ── Loopback capture ──────────────────────────────────────────────────
        private float DecodeLoopbackPeak(byte[] buffer, int bytesRecorded)
        {
            if (bytesRecorded < 4) return 0f;
            float max = 0f;
            for (int i = 0; i + 4 <= bytesRecorded; i += 4)
            {
                float sample = Math.Abs(BitConverter.ToSingle(buffer, i)) * _customerVoiceVolume;
                if (sample > max) max = sample;
            }
            return Math.Min(1f, max * 2.85f);
        }

        private void PushCustomerVoicePeak(float level)
        {
            if (LocalBridgeServer.Instance.IsPlaying)
            {
                if (level < RedMeterPlaybackNoiseGate)
                    level = 0f;
            }
            else
            {
                if (level < RedMeterIdleNoiseGate)
                    level = 0f;
            }
            if (level > _customerVoiceLevel) _customerVoiceLevel = level;
        }

        /// <param name="softerBleedRemoval">
        /// Headphone loopback: use softer subtraction so quiet customer speech still moves RED during script playback.
        /// </param>
        /// <param name="softerBleedRemoval">
        /// Multimedia headphone path: slight relax so quiet customer speech survives subtraction during playback.
        /// Communications/HF path: pass false — agent script often shares this endpoint with VoIP; weak subtraction lets RED track playback.
        /// </param>
        private float SuppressBridgePlaybackFromRed(float loopbackLevel, bool softerBleedRemoval = false)
        {
            if (!LocalBridgeServer.Instance.IsPlaying) return loopbackLevel;
            float envRef = Math.Max(_bridgePlaybackEnvelope, _bridgePlaybackSessionPeak);
            float suppress = Math.Min(loopbackLevel, envRef * 1.22f);
            // Previously 0.5× gutted bleed rejection; keep most of envelope match so RED does not follow agent recordings.
            if (softerBleedRemoval) suppress *= 0.88f;
            float cleaned = Math.Max(0f, loopbackLevel - suppress);
            float noiseFloor = softerBleedRemoval ? 0.024f : 0.018f;
            return cleaned < noiseFloor ? 0f : cleaned;
        }

        private void StartLoopbackCapture()
        {
            try
            {
                try { _loopbackCapture?.StopRecording(); _loopbackCapture?.Dispose(); } catch { }
                try { _loopbackDefaultCapture?.StopRecording(); _loopbackDefaultCapture?.Dispose(); } catch { }
                try { _loopbackCommunicationsCapture?.StopRecording(); _loopbackCommunicationsCapture?.Dispose(); } catch { }
                _loopbackCapture = null;
                _loopbackDefaultCapture = null;
                _loopbackCommunicationsCapture = null;

                MMDevice defaultRender = null;
                try { defaultRender = _deviceEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia); }
                catch { /* no default */ }

                MMDevice communicationsRender = null;
                try { communicationsRender = _deviceEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications); }
                catch { /* no communications default */ }

                bool CommSameAsMultimedia(MMDevice mm, MMDevice comm)
                {
                    if (mm == null || comm == null) return false;
                    return string.Equals(mm.ID, comm.ID, StringComparison.OrdinalIgnoreCase);
                }

                void TryStartCommunicationsLoopback(MMDevice cableDev)
                {
                    if (communicationsRender == null) return;
                    if (CommSameAsMultimedia(defaultRender, communicationsRender))
                        return;
                    if (cableDev != null &&
                        string.Equals(communicationsRender.ID, cableDev.ID, StringComparison.OrdinalIgnoreCase))
                        return;
                    try
                    {
                        Log.Info($"[Audio] Loopback capture → Communications render '{communicationsRender.FriendlyName}' (browser VoIP / HF path)");
                        _loopbackCommunicationsCapture = new WasapiLoopbackCapture(communicationsRender);
                        _loopbackCommunicationsCapture.DataAvailable += LoopbackCommunications_DataAvailable_PushRed;
                        _loopbackCommunicationsCapture.StartRecording();
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"[Audio] Communications loopback failed: {ex.Message}");
                        try { _loopbackCommunicationsCapture?.Dispose(); } catch { }
                        _loopbackCommunicationsCapture = null;
                    }
                }

                if (_activeVBCableDevice == null)
                {
                    Log.Warn("[Audio] No VB-Cable device — RED loopback uses default playback device only.");
                    _loopbackCapture = new WasapiLoopbackCapture();
                    _loopbackCapture.DataAvailable += LoopbackDefault_DataAvailable_PushRed;
                    _loopbackCapture.StartRecording();
                    TryStartCommunicationsLoopback(null);
                    _loopbackStartTime = DateTime.UtcNow;
                    Log.Info("[Audio] Loopback capture started (default + optional Communications).");
                    return;
                }

                // Cable: Dialpad / apps routed to VB-Cable (customer toward agent path).
                Log.Info($"[Audio] Loopback capture → VB-Cable render '{_activeVBCableDevice.FriendlyName}'");
                _loopbackCapture = new WasapiLoopbackCapture(_activeVBCableDevice);
                _loopbackCapture.DataAvailable += LoopbackCable_DataAvailable_PushRed;
                _loopbackCapture.StartRecording();

                // Second tap: default speakers/headphones (YouTube, browser sim customer) — usually NOT the cable endpoint.
                bool defaultSameAsCable = defaultRender != null &&
                    string.Equals(defaultRender.ID, _activeVBCableDevice.ID, StringComparison.OrdinalIgnoreCase);
                if (!defaultSameAsCable && defaultRender != null)
                {
                    Log.Info($"[Audio] Loopback capture → default Multimedia render '{defaultRender.FriendlyName}' (browser / system playback)");
                    _loopbackDefaultCapture = new WasapiLoopbackCapture(defaultRender);
                    _loopbackDefaultCapture.DataAvailable += LoopbackDefault_DataAvailable_PushRed;
                    _loopbackDefaultCapture.StartRecording();
                }

                TryStartCommunicationsLoopback(_activeVBCableDevice);

                _loopbackStartTime = DateTime.UtcNow;
                Log.Info("[Audio] Loopback capture started.");
            }
            catch (Exception ex) { Log.Warn($"[Audio] Loopback failed: {ex.Message}"); }
        }

        private void LoopbackCable_DataAvailable_PushRed(object sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded < 4) return;
            if ((DateTime.UtcNow - _loopbackStartTime).TotalMilliseconds < LoopbackWarmupMs) return;
            // During bridge playback, VB-Cable loopback mostly contains script audio.
            // Ignore this source so RED does not react to agent recordings.
            // Customer voice should still come from default/headphone loopback.
            if (LocalBridgeServer.Instance.IsPlaying) return;

            // ── Mic gate (replaces scalar bleed subtraction) ────────────────────
            // Scalar subtraction (micLevel × coupling) is unreliable because the mic
            // capture callback and loopback callback run on different threads with
            // different buffer timing. A gate is simpler and 100% effective:
            // if the agent mic is active, the VB-Cable loopback is mostly mic audio → zero RED.
            if (_micLevel > MicGateThreshold)
            {
                _micGateHoldTicks = MicGateHoldTickCount; // reset hold
                _customerVoiceLevel = 0f;
                return;
            }
            if (_micGateHoldTicks > 0)
            {
                _micGateHoldTicks--;
                _customerVoiceLevel = 0f;
                return;
            }

            float level = DecodeLoopbackPeak(e.Buffer, e.BytesRecorded);
            PushCustomerVoicePeak(SuppressBridgePlaybackFromRed(level));
        }

        private void LoopbackDefault_DataAvailable_PushRed(object sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded < 4) return;
            if ((DateTime.UtcNow - _loopbackStartTime).TotalMilliseconds < LoopbackWarmupMs) return;

            // During bridge playback the headset loopback carries script audio from waveAgentOut.
            // Passing that through SuppressBridgePlaybackFromRed is imprecise — envelope lag causes
            // residual bleed on the RED (customer voice) meter while scripts play.
            // Suppress entirely: the agent is listening to a script; customer-voice RED is not
            // meaningful during playback. Mirrors the same guard on LoopbackCable.
            if (LocalBridgeServer.Instance.IsPlaying) return;

            // No mic gate here: the headset speaker loopback carries customer voice, not mic audio.
            // Softphone echo cancellation prevents agent mic from bleeding into the speaker path.
            // Hard-zeroing RED on every agent utterance caused choppy, non-realtime animation.
            float level = DecodeLoopbackPeak(e.Buffer, e.BytesRecorded);
            PushCustomerVoicePeak(SuppressBridgePlaybackFromRed(level));
        }

        /// <summary>
        /// Windows Communications default (browser VoIP / Hands-Free). Same RED rules as Multimedia loopback.
        /// Suppressed entirely during bridge playback — this endpoint often shares hardware with the agent
        /// headset path, making envelope subtraction unreliable and causing RED bleed during scripts.
        /// </summary>
        private void LoopbackCommunications_DataAvailable_PushRed(object sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded < 4) return;
            if ((DateTime.UtcNow - _loopbackStartTime).TotalMilliseconds < LoopbackWarmupMs) return;

            // Guard matches LoopbackCable and LoopbackDefault: zero RED during playback.
            // Channel bleed fix: script audio on the Communications endpoint would otherwise
            // push _customerVoiceLevel via imprecise envelope subtraction.
            if (LocalBridgeServer.Instance.IsPlaying) return;

            // No mic gate here: same reason as LoopbackDefault — softphone echo cancellation
            // keeps agent mic out of the speaker loopback. Gate was causing hard drops that made
            // RED choppy. Track audio directly for smooth, realtime customer voice animation.
            float level = DecodeLoopbackPeak(e.Buffer, e.BytesRecorded);
            PushCustomerVoicePeak(SuppressBridgePlaybackFromRed(level));
        }

        /// <summary>CUSTOMER VOICE volume affects Windows master volume on the agent headset render device.</summary>
        private void ApplyCustomerVoiceListenEndpointVolume()
        {
            if (_activeSpeakerDevice == null) return;
            try
            {
                _activeSpeakerDevice.AudioEndpointVolume.MasterVolumeLevelScalar = DbToLinear(_dbCustomerVoice);
            }
            catch (Exception ex)
            {
                Log.Warn($"[Audio] Could not set customer-voice listen level on headset: {ex.Message}");
            }
        }

        // ── Device population ─────────────────────────────────────────────────
        private void PopulateDevices()
        {
            try
            {
                var caps = _deviceEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                foreach (var d in caps) _cboMic.Items.Add(d.FriendlyName);
                if (_cboMic.Items.Count > 0)
                {
                    int idx = _cboMic.Items.IndexOf(AppSettings.Instance.MicDevice);
                    _cboMic.SelectedIndex = idx >= 0 ? idx : 0;
                    _cboMic.BackColor = ONE_BLUE_SEL;
                    _cboMic.ForeColor = Color.White;
                }
                var rends = _deviceEnum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
                foreach (var d in rends) _cboHeadset.Items.Add(d.FriendlyName);
                if (_cboHeadset.Items.Count > 0)
                {
                    int idx = _cboHeadset.Items.IndexOf(AppSettings.Instance.HeadsetDevice);
                    _cboHeadset.SelectedIndex = idx >= 0 ? idx : 0;
                    _cboHeadset.BackColor = ONE_BLUE_SEL;
                    _cboHeadset.ForeColor = Color.White;

                    int selIdx = _cboHeadset.SelectedIndex;
                    if (selIdx >= 0 && selIdx < rends.Count)
                    {
                        _activeSpeakerDevice = rends[selIdx];
                        int waveOutNum = FindWaveOutDeviceNumber(rends[selIdx].FriendlyName);
                        LocalBridgeServer.Instance.SetOutputDevice(waveOutNum);
                        LocalBridgeServer.Instance.SetAgentRenderDevice(_activeSpeakerDevice);
                        ApplyCustomerVoiceListenEndpointVolume();
                        Log.Info($"[Audio] Initial speaker: {rends[selIdx].FriendlyName} → WaveOut #{waveOutNum}");
                        try
                        {
                            int savedPct = AppSettings.Instance.SpeakerSystemVolume;
                            _customerVoiceVolume = savedPct / 100f;
                            if (_trkSpeakerVol != null) { _trkSpeakerVol.Value = savedPct; if (_lblSpeakerVol != null) _lblSpeakerVol.Text = $"{savedPct}%"; }
                        }
                        catch { }
                    }
                }
                foreach (var d in rends)
                    Log.Info($"[Audio] Render Device: '{d.FriendlyName}' | State: {d.State} | ID: {d.ID}");

                // BUG FIX: Tightened VB-Cable detection.
                // Previously matched "Virtual" and "Line " which could pick up unrelated devices
                // (e.g. "Virtual Surround Sound", "Line In") and route customer audio to the wrong output.
                // Now only matches the actual VB-Audio Virtual Cable product name patterns.
                _activeVBCableDevice = rends.FirstOrDefault(d =>
                    d.FriendlyName.IndexOf("CABLE Input",  StringComparison.OrdinalIgnoreCase) >= 0 ||
                    d.FriendlyName.IndexOf("CABLE Output", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    d.FriendlyName.IndexOf("VB-Audio",     StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (d.FriendlyName.IndexOf("CABLE", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     d.FriendlyName.IndexOf("VB",    StringComparison.OrdinalIgnoreCase) >= 0));
                // Fallback: if strict match fails, try broader CABLE match (catches older VB-Cable installs)
                if (_activeVBCableDevice == null)
                {
                    _activeVBCableDevice = rends.FirstOrDefault(d =>
                        d.FriendlyName.IndexOf("CABLE", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (_activeVBCableDevice != null)
                        Log.Warn($"[Audio] VB-Cable found via broad CABLE match: '{_activeVBCableDevice.FriendlyName}' — verify this is VB-Audio Virtual Cable");
                }

                if (_activeVBCableDevice != null)
                {
                    Log.Info($"[Audio] VB-Cable: '{_activeVBCableDevice.FriendlyName}'");
                    int cableNum = FindWaveOutDeviceNumber(_activeVBCableDevice.FriendlyName);
                    LocalBridgeServer.Instance.SetCableDevice(cableNum);
                }
                else
                {
                    Log.Info("[Audio] No VB-Cable found.");
                }

                _cboHeadset.SelectedIndexChanged += (s2, e2) =>
                {
                    _cboHeadset.BackColor = ONE_BLUE_SEL;
                    _cboHeadset.ForeColor = Color.White;
                    int si = _cboHeadset.SelectedIndex;
                    if (si >= 0 && si < rends.Count)
                    {
                        _activeSpeakerDevice = rends[si];
                        AppSettings.Instance.HeadsetDevice = _cboHeadset.Text;
                        AppSettings.Instance.Save();
                        int waveOutNum = FindWaveOutDeviceNumber(rends[si].FriendlyName);
                        LocalBridgeServer.Instance.SetOutputDevice(waveOutNum);
                        LocalBridgeServer.Instance.SetAgentRenderDevice(_activeSpeakerDevice);
                        ApplyCustomerVoiceListenEndpointVolume();
                        Log.Info($"[Audio] Speaker changed: {rends[si].FriendlyName} → WaveOut #{waveOutNum}");
                        try
                        {
                            float cur = _activeSpeakerDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
                            int pct = (int)(cur * 100);
                            if (_trkSpeakerVol != null) { _trkSpeakerVol.Value = pct; if (_lblSpeakerVol != null) _lblSpeakerVol.Text = $"{pct}%"; }
                        }
                        catch { }
                    }
                };
            }
            catch (Exception ex) { Log.Warn($"[Audio] Device enum: {ex.Message}"); }
        }

        private int FindWaveOutDeviceNumber(string targetDeviceName)
        {
            if (string.IsNullOrEmpty(targetDeviceName)) return -1;
            string target = targetDeviceName.ToLower().Trim();
            Log.Info($"[Audio] Finding WaveOut match for '{targetDeviceName}'");
            int count = WaveOut.DeviceCount;
            int bestIdx = -1;
            int bestScore = 0;
            for (int i = 0; i < count; i++)
            {
                var capabilities = WaveOut.GetCapabilities(i);
                string prod = capabilities.ProductName.ToLower().Trim();
                if (target.Contains(prod) || prod.Contains(target))
                {
                    Log.Info($"[Audio] WaveOut #{i}: MATCH");
                    return i;
                }
                string targetAlpha = new string(target.Where(char.IsLetterOrDigit).ToArray());
                string prodAlpha   = new string(prod.Where(char.IsLetterOrDigit).ToArray());
                bool alphaMatch    = targetAlpha.StartsWith(prodAlpha) || prodAlpha.StartsWith(targetAlpha);
                int alphaScore     = alphaMatch ? Math.Min(targetAlpha.Length, prodAlpha.Length) : 0;
                char[] sep         = new char[] { ' ', '(', ')', '-', '/', '[', ']', ',', '.' };
                string[] prodWords   = prod.Split(sep,   StringSplitOptions.RemoveEmptyEntries);
                string[] targetWords = target.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                int wordMatches = 0;
                foreach (var pw in prodWords)
                {
                    if (pw.Length < 3) { if (targetWords.Contains(pw)) wordMatches++; continue; }
                    if (targetWords.Any(tw => tw.StartsWith(pw) || pw.StartsWith(tw))) wordMatches++;
                }
                int wordScore    = wordMatches * 5;
                int currentScore = Math.Max(alphaScore, wordScore);
                if (currentScore > bestScore) { bestScore = currentScore; bestIdx = i; }
            }
            if (bestIdx >= 0 && bestScore >= 5) return bestIdx;
            Log.Warn($"[Audio] No WaveOut match for '{targetDeviceName}'");
            return -1;
        }

        // ── Script playback meters (ScriptsForm) ──────────────────────────────
        // Dedicated AudioService channels avoid feeding live VoIP AudioIntensityChanged into these rings.
        private void OnScriptPlaybackToCustomerLevel(object sender, AudioIntensityEventArgs e)
        {
            if (IsDisposed) return;
            float peaked = Math.Min(1f, e.Intensity / 100f * 0.95f) * 0.85f;
            void tick()
            {
                if (IsDisposed) return;
                if (peaked > _agentScriptLevel) _agentScriptLevel = peaked;
                _agentScriptMeter?.Invalidate();
            }
            if (InvokeRequired) BeginInvoke((Action)tick); else tick();
        }

        private void OnScriptPlaybackAgentHeadsetLevel(object sender, AudioIntensityEventArgs e)
        {
            if (IsDisposed) return;
            float peaked = Math.Min(1f, e.Intensity / 100f * 0.95f) * 0.85f;
            void tick()
            {
                if (IsDisposed) return;
                if (peaked > _customerScriptLevel) _customerScriptLevel = peaked;
                _customerScriptMeter?.Invalidate();
            }
            if (InvokeRequired) BeginInvoke((Action)tick); else tick();
        }

        // ── Local Bridge Server ───────────────────────────────────────────────
        private void StartBridgeServer()
        {
            var bridge = LocalBridgeServer.Instance;
            Log.Info("[Bridge→UI] Script meters: bridge channel 'agent'(headset)→BLUE left ring; 'customer'(VB-Cable)→GREEN right. WhatsApp/discord/etc. must use 'CABLE Output' / VB-Audio as mic for callers to hear script audio. Dial arcs cap at slider %.");
            bridge.OnPlaybackLevel += (level, channel) =>
            {
                if (this.IsDisposed) return;
                const float smoothK = 0.65f;  // Increased for faster real-time tracking (was 0.24)
                                               // 0.44 was too sluggish, causing the BLUE agent ring to appear flat
                                               // during playback. 0.72 gives visible real-time tracking.
                Action update = () =>
                {
                    // Bridge: "agent" = headset playback; "customer" = VB-Cable toward caller.
                    // Left (BLUE) = _customerScriptLevel; right (GREEN) = _agentScriptLevel.
                    if (channel == "agent")
                    {
                        float t = level * 1.85f; // Heavily boosted for visibility at low volume (Scott request)
                        _customerScriptLevel = _customerScriptLevel * (1f - smoothK) + t * smoothK;
                        _customerScriptMeter?.Invalidate();
                    }
                    else
                    {
                        // Match BLUE boost so GREEN (customer/VB-Cable) meter tracks perceived headset energy (~same as agent ear).
                        float t = level * 1.85f;
                        _agentScriptLevel = _agentScriptLevel * (1f - smoothK) + t * smoothK;
                        _agentScriptMeter?.Invalidate();
                    }
                    _bridgePlaybackEnvelope = Math.Max(_customerScriptLevel, _agentScriptLevel);
                    float boosted = level * 1.85f;
                    if (boosted > _bridgePlaybackSessionPeak)
                        _bridgePlaybackSessionPeak = boosted;

                    if (_bridgeUiDiagSw.ElapsedMilliseconds >= 500)
                    {
                        _bridgeUiDiagSw.Restart();
                        int capBlue  = GetVolumePercent("agentScript_left");
                        int capGreen = GetVolumePercent("agentScript");
                        Log.Info($"[Bridge→UI] rx ch={channel} rawLvl={level:F3} → BLUE(left)_lvl={_customerScriptLevel:F3} cap={capBlue}% GREEN(right)_lvl={_agentScriptLevel:F3} cap={capGreen}% RED(loopback)={_customerVoiceLevel:F3} panelNull BLUE={_customerScriptMeter == null} GREEN={_agentScriptMeter == null} bridgePlaying={LocalBridgeServer.Instance.IsPlaying}");
                    }
                };
                if (this.InvokeRequired) this.BeginInvoke(update); else update();
            };
            bridge.OnPlaybackStarted += () =>
            {
                if (this.IsDisposed) return;
                _isCallActive = true; // guard window-restore from restarting loopback mid-playback
                Action mutePassThrough = () =>
                {
                    StopMicPassThrough();
                    _customerVoiceLevel = 0f;
                    _customerVoiceMeter?.Invalidate();
                    _myMicMeterLeft?.Invalidate(); // violet shows 0 while customer path is muted
                    _bridgePlaybackSessionPeak = 0.052f; // seed bleed reference so first syllable does not under-subtract RED
                    Log.Info("[PassThrough] Hard-muted while recording is playing.");
                };
                if (this.InvokeRequired) this.BeginInvoke(mutePassThrough); else mutePassThrough();
            };
            bridge.OnPlaybackStopped += () =>
            {
                if (this.IsDisposed) return;
                _isCallActive = false; // release guard so window-restore can restart loopback normally
                Action reset = () =>
                {
                    _agentScriptLevel    = 0f;
                    _customerScriptLevel = 0f;
                    _bridgePlaybackEnvelope = 0f;
                    _bridgePlaybackSessionPeak = 0f;
                    _agentScriptMeter?.Invalidate();
                    _customerScriptMeter?.Invalidate();
                    if (_activeMicDevice != null)
                    {
                        StopMicPassThrough();
                        StartMicPassThrough(_activeMicDevice.FriendlyName);
                        _myMicMeterLeft?.Invalidate();
                        Log.Info("[PassThrough] Restarted after recording stopped.");
                    }
                };
                if (this.InvokeRequired) this.BeginInvoke(reset); else reset();
            };

            int savedAgent    = (int)(AppSettings.Instance.GetVolume("agentScript",    0.92f) * 100);
            int savedCustomer = (int)(AppSettings.Instance.GetVolume("customerScript", 0.85f) * 100);
            bridge.SetInitialVolume("agent",    savedAgent);
            bridge.SetInitialVolume("customer", savedCustomer);
            Log.Info($"[Audio] Initial volumes: agent={savedAgent}% customer={savedCustomer}%");
            bridge.Start();
        }

        // ── Heartbeat / License ───────────────────────────────────────────────
        private const string OWNER_UUID = "4C4C4544-0058-3510-8043-B5C04F595733";
        private async void StartHeartbeat()
        {
            string machineId = MachineId.Get();
            if (string.Equals(machineId, OWNER_UUID, StringComparison.OrdinalIgnoreCase))
            {
                if (_lblAgentName != null) _lblAgentName.Text = "Agent: Owner";
                Log.Info("[License] Owner machine — bypassed.");
                return;
            }
            string key = AppSettings.Instance.LicenseKey;
            if (string.IsNullOrEmpty(key)) return;
            bool ok = await HeartbeatService.Instance.DesktopLoginAsync(key, machineId);
            if (ok)
            {
                string name = HeartbeatService.Instance.AgentName;
                if (!string.IsNullOrEmpty(name))
                {
                    AppSettings.Instance.AgentName = name;
                    AppSettings.Instance.Save();
                    if (_lblAgentName != null) _lblAgentName.Text = "Agent: " + name;
                }
                HeartbeatService.Instance.Start();
            }
            HeartbeatService.Instance.LicenseInvalid += (s, e) =>
            {
                this.Invoke((Action)(() =>
                {
                    MessageBox.Show(e.Reason + "\n\nPlease visit onevoicesolution.com to renew.",
                        "License Invalid", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Application.Exit();
                }));
            };
            HeartbeatService.Instance.SeatLimitExceeded += (s, e) =>
            {
                this.Invoke((Action)(() =>
                {
                    MessageBox.Show(
                        $"Your license allows {e.PurchasedSeats} concurrent seat(s), " +
                        $"but {e.ActiveSeats} are currently active.\n\n" +
                        "Please close ONE Voice on another machine or upgrade your plan.",
                        "Seat Limit Exceeded", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }));
            };
        }

        // ── Settings ──────────────────────────────────────────────────────────
        private void LoadSettings()
        {
            var s = AppSettings.Instance;
            // Restore dB values from saved volume percentages
            int micPct     = Math.Max(1, Math.Min(200, s.MicSystemVolume));
            int spkPct     = Math.Max(1, Math.Min(200, s.SpeakerSystemVolume));
            int agentPct   = Math.Max(1, (int)(s.GetVolume("agentScript",    0.90f) * 100));
            int custPct    = Math.Max(1, (int)(s.GetVolume("customerScript", 0.85f) * 100));
            _dbAgentVoice      = LinearToDb(micPct   / 100f);
            _dbCustomerVoice   = LinearToDb(spkPct   / 100f);
            _dbCustomerScript  = LinearToDb(agentPct / 100f);
            _dbAgentScript     = LinearToDb(custPct  / 100f);
        }

        private static int LinearToDb(float linear)
        {
            if (linear <= 0.001f) return -20;
            int db = (int)Math.Round(20.0 * Math.Log10(linear));
            return Math.Max(-20, Math.Min(6, db));
        }

        // ── Logo ──────────────────────────────────────────────────────────────
        private void LoadLogo()
        {
            string agencyLogo = AppSettings.Instance.AgencyLogoPath;
            string oneLogo    = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "logo.png");
            
            // Fallback for debug mode if not copied to output directory
            if (!File.Exists(oneLogo)) {
                oneLogo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "res", "logo.png");
            }

            string logoPath   = (!string.IsNullOrEmpty(agencyLogo) && File.Exists(agencyLogo))
                ? agencyLogo : oneLogo;
                
            if (File.Exists(logoPath) && _logoBox != null)
                _logoBox.Image = Image.FromFile(logoPath);
        }

        // ── Drag support ──────────────────────────────────────────────────────
        private void AttachDrag(Control c)
        {
            c.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                }
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(r.Right - radius * 2, r.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(r.X, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ── Form close ────────────────────────────────────────────────────────
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            AudioService.Instance.ScriptPlaybackToCustomerLevelChanged -= OnScriptPlaybackToCustomerLevel;
            AudioService.Instance.ScriptPlaybackAgentHeadsetLevelChanged -= OnScriptPlaybackAgentHeadsetLevel;
            HeartbeatService.Instance.Stop();
            LocalBridgeServer.Instance.Stop();
            _meterTimer?.Stop();
            _micCapture?.StopRecording();
            _micCapture?.Dispose();
            try { _micPassWaveIn?.StopRecording(); } catch { }
            try { _micPassWaveIn?.Dispose(); }       catch { }
            try { _micPassWaveOut?.Stop(); }    catch { }
            try { _micPassWaveOut?.Dispose(); } catch { }
            try { _loopbackCapture?.StopRecording(); } catch { }
            try { _loopbackCapture?.Dispose(); } catch { }
            try { _loopbackDefaultCapture?.StopRecording(); } catch { }
            try { _loopbackDefaultCapture?.Dispose(); } catch { }
            try { _loopbackCommunicationsCapture?.StopRecording(); } catch { }
            try { _loopbackCommunicationsCapture?.Dispose(); } catch { }
            _trayIcon?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
