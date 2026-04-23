/*
 * MainFormV5.cs  —  ONE Voice Solution v7.38
 *
 * UI REDESIGN v7.38:
 *   - Complete visual overhaul to match design mock exactly.
 *   - Dark space background with 800 static and glowing stars.
 *   - ONE logo (top-left) + "The Geniusness Is In The Simplicity" tagline centered in header.
 *   - 4 circular neon glow meters with live frequency tracking capped at static volume %.
 *   - Unified [– VOLUME +] pill-shaped controls below meters.
 *   - Custom GDI+ drawn device icons for MICROPHONE (red) and SPEAKER (blue).
 *   - Footer: "One United Global LLC 2026. V 7.38"
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
        private const string APP_VERSION = "7.49";

        // ── Scale ─────────────────────────────────────────────────────────────
        private float _scale = 1.0f;
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
        private MMDeviceEnumerator _deviceEnum = new MMDeviceEnumerator();
        private float _micLevel            = 0f;
        private float _customerVoiceLevel  = 0f;
        private float _agentScriptLevel    = 0f;
        private float _customerScriptLevel = 0f;

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

        // Loopback capture for Customer Voice meter
        private WasapiLoopbackCapture  _loopbackCapture;
        private float                  _customerVoiceVolume = 1.0f;

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
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int W = this.ClientSize.Width;
            int H = this.ClientSize.Height;

            // 1. Dark deep space background
            g.FillRectangle(new SolidBrush(Color.FromArgb(5, 5, 12)), 0, 0, W, H);

            // 2. Draw nebulas (simulated background image)
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

            // Original ambient corner/top glows (dialed down slightly to not overwhelm)
            DrawNebula(W / 2 - (int)(W * 0.4f), - (int)(H * 0.2f), (int)(W * 0.8f), (int)(H * 0.4f), Color.FromArgb(40, 255, 0, 0));
            DrawNebula(- (int)(W * 0.2f), H - (int)(H * 0.4f), (int)(W * 0.6f), (int)(H * 0.6f), Color.FromArgb(25, 255, 0, 0));
            DrawNebula(W - (int)(W * 0.4f), H - (int)(H * 0.4f), (int)(W * 0.6f), (int)(H * 0.6f), Color.FromArgb(25, 0, 120, 255));

            // 3. Static stars for depth
            var rnd = new Random(W * H); // seeded by size
            for (int i = 0; i < 800; i++) {
                int sx = rnd.Next(W);
                int sy = rnd.Next(H);
                int alpha = rnd.Next(10, 80); // softer stars so they don't look like noise
                float sz = rnd.Next(1, 3);
                bool isGlow = rnd.Next(100) > 85; // 15% of stars glow

                if (isGlow) {
                    float glowRadius = sz * rnd.Next(2, 4);
                    using (var glowPath = new GraphicsPath()) {
                        glowPath.AddEllipse(sx - glowRadius, sy - glowRadius, glowRadius * 2, glowRadius * 2);
                        using (var pgb = new PathGradientBrush(glowPath)) {
                            // Use a soft, deep blue/cyan for the glow to blend smoothly into the dark space background
                            pgb.CenterColor = Color.FromArgb(alpha + 40, 140, 180, 255);
                            // Avoid grayish GDI+ fringing by blending to a transparent version of the same color
                            pgb.SurroundColors = new[] { Color.FromArgb(0, 140, 180, 255) };
                            g.FillPath(pgb, glowPath);
                        }
                    }
                    alpha = rnd.Next(120, 255); // make the core of glowing stars brighter
                    sz += 1f;
                }

                using (var b = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
                    g.FillEllipse(b, sx - sz / 2f, sy - sz / 2f, sz, sz);
            }

            // 4. Horizontal red light flare
            int cardPad  = (int)(18 * _scale);
            int headerH  = (int)(90 * _scale);
            int flareY   = cardPad + headerH; 
            int flareX1  = (int)(30 * _scale);
            int flareX2  = W - (int)(30 * _scale);

            // Horizontal gradient flare: brand red (#FE0101) on both edges → pure white at center
            // Outer soft glow pass (wide, low alpha)
            int glowH2 = (int)(14f * _scale);
            using (var lgb2 = new System.Drawing.Drawing2D.LinearGradientBrush(
                new Rectangle(flareX1, flareY - glowH2 / 2, flareX2 - flareX1, glowH2),
                Color.FromArgb(60, 254, 1, 1), Color.FromArgb(60, 254, 1, 1), 0f))
            {
                lgb2.InterpolationColors = new System.Drawing.Drawing2D.ColorBlend
                {
                    Colors = new[] { Color.FromArgb(60, 254, 1, 1), Color.FromArgb(20, 255, 255, 255), Color.FromArgb(60, 254, 1, 1) },
                    Positions = new[] { 0f, 0.5f, 1f }
                };
                using (var pen2 = new Pen(lgb2, glowH2)) g.DrawLine(pen2, flareX1, flareY, flareX2, flareY);
            }
            // Mid glow pass
            int glowH = (int)(5f * _scale);
            using (var lgb = new System.Drawing.Drawing2D.LinearGradientBrush(
                new Rectangle(flareX1, flareY - glowH / 2, flareX2 - flareX1, glowH),
                Color.FromArgb(180, 254, 1, 1), Color.FromArgb(180, 254, 1, 1), 0f))
            {
                lgb.InterpolationColors = new System.Drawing.Drawing2D.ColorBlend
                {
                    Colors = new[] { Color.FromArgb(180, 254, 1, 1), Color.FromArgb(220, 255, 255, 255), Color.FromArgb(180, 254, 1, 1) },
                    Positions = new[] { 0f, 0.5f, 1f }
                };
                using (var pen = new Pen(lgb, glowH)) g.DrawLine(pen, flareX1, flareY, flareX2, flareY);
            }
            // Bright core line: full brand red on edges → pure white center
            int coreH = (int)(2f * _scale);
            using (var lcore = new System.Drawing.Drawing2D.LinearGradientBrush(
                new Rectangle(flareX1, flareY - 1, flareX2 - flareX1, Math.Max(2, coreH)),
                Color.FromArgb(255, 254, 1, 1), Color.FromArgb(255, 254, 1, 1), 0f))
            {
                lcore.InterpolationColors = new System.Drawing.Drawing2D.ColorBlend
                {
                    Colors = new[] { Color.FromArgb(255, 254, 1, 1), Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 254, 1, 1) },
                    Positions = new[] { 0f, 0.5f, 1f }
                };
                using (var penCore = new Pen(lcore, Math.Max(1.5f, coreH))) g.DrawLine(penCore, flareX1, flareY, flareX2, flareY);
            }

            // 5. Window Border — red neon glow (multi-pass)
            using (var path = RoundedRect(new Rectangle(1, 1, W - 3, H - 3), (int)(15 * _scale)))
            {
                using (var gp1 = new Pen(Color.FromArgb(18, 255, 0, 0), 18f * _scale)) g.DrawPath(gp1, path);
                using (var gp2 = new Pen(Color.FromArgb(35, 255, 0, 0), 12f * _scale)) g.DrawPath(gp2, path);
                using (var gp3 = new Pen(Color.FromArgb(60, 255, 0, 0), 7f * _scale))  g.DrawPath(gp3, path);
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
            string[] labels = { "CUSTOMER VOICE", "AGENT RECORDING", "AGENT VOICE", "AGENT RECORDING" };
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
            if (volLevel > 0.01f)
            {
                float litSweep = sweepAngle * volLevel;
                
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

            // 7. Value Text (Percentage)
            string valStr  = percent.ToString() + "%";
            float  valSize = SF(38f);
            using (var valFont = new Font("Segoe UI", valSize, FontStyle.Bold))
            {
                var sz = TextRenderer.MeasureText(valStr, valFont);
                TextRenderer.DrawText(g, valStr, valFont,
                    new Point(cx - sz.Width / 2, cy - sz.Height / 2 - (int)(4 * _scale)), meterColor);
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

            int agentDefault    = (int)(AppSettings.Instance.GetVolume("agentScript",    0.48f) * 100);
            int customerDefault = (int)(AppSettings.Instance.GetVolume("customerScript", 0.55f) * 100);
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
                case "myMicLevel":      return _micLevel;
                case "customerVoice":   return _customerVoiceLevel;
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
                case "agentScript_left":return DbToPercent(_dbCustomerScript);
                case "myMicLevel":      return DbToPercent(_dbAgentVoice);
                case "agentScript":     return DbToPercent(_dbAgentScript);
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
                    _customerVoiceLevel = Math.Max(0f, _customerVoiceLevel - 0.02f);
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
            };
            _meterTimer.Start();
        }

        // ── Audio capture (microphone) ────────────────────────────────────────
        private void StartAudioCapture(string deviceName = null)
        {
            try { _micCapture?.StopRecording(); } catch { }
            try { _micCapture?.Dispose(); }       catch { }
            _micCapture = null;
            try { _micPassWaveIn?.StopRecording(); } catch { }
            try { _micPassWaveIn?.Dispose(); }       catch { }
            _micPassWaveIn = null;
            try { _micPassWaveOut?.Stop(); }    catch { }
            try { _micPassWaveOut?.Dispose(); } catch { }
            _micPassWaveOut = null;

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
                    for (int i = 0; i + stride <= e.BytesRecorded; i += stride)
                    {
                        float sample = stride == 2
                            ? Math.Abs(BitConverter.ToInt16(e.Buffer, i) / 32768f)
                            : Math.Abs(BitConverter.ToSingle(e.Buffer, i));
                        if (sample > max) max = sample;
                    }
                    _micLevel = Math.Min(1f, max * 2.5f);
                };
                _micCapture.StartRecording();
                StartMicPassThrough(device.FriendlyName);
            }
            catch (Exception ex) { Log.Warn($"[Audio] Capture failed: {ex.Message}"); }
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
                    BufferMilliseconds = 50
                };
                var provider = new WaveInProvider(_micPassWaveIn);

                _micPassWaveOut = new WaveOutEvent
                {
                    DeviceNumber   = cableNum,
                    DesiredLatency = 100
                };
                _micPassWaveOut.Init(provider);
                _micPassWaveIn.StartRecording();
                _micPassWaveOut.Play();
                _micPassWaveOut.Volume = 1.0f;

                Log.Info($"[PassThrough] ACTIVE: WaveIn #{waveInNum} ('{deviceFriendlyName}') → WaveOut #{cableNum} (CABLE)");
            }
            catch (Exception ex)
            {
                Log.Warn($"[PassThrough] Failed: {ex.Message}");
                try { _micPassWaveIn?.StopRecording(); } catch { }
                try { _micPassWaveIn?.Dispose(); }       catch { }
                _micPassWaveIn = null;
                try { _micPassWaveOut?.Stop(); }    catch { }
                try { _micPassWaveOut?.Dispose(); } catch { }
                _micPassWaveOut = null;
            }
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
        private void StartLoopbackCapture()
        {
            try
            {
                try { _loopbackCapture?.StopRecording(); _loopbackCapture?.Dispose(); } catch { }
                _loopbackCapture = null;
                _loopbackCapture = new WasapiLoopbackCapture();
                _loopbackCapture.DataAvailable += (s, e) =>
                {
                    if (e.BytesRecorded < 4) return;
                    if (LocalBridgeServer.Instance.IsPlaying) { _customerVoiceLevel = 0f; return; }
                    float max = 0f;
                    for (int i = 0; i + 4 <= e.BytesRecorded; i += 4)
                    {
                        float sample = Math.Abs(BitConverter.ToSingle(e.Buffer, i)) * _customerVoiceVolume;
                        if (sample > max) max = sample;
                    }
                    float level = Math.Min(1f, max * 2.2f);
                    if (level > _customerVoiceLevel) _customerVoiceLevel = level;
                };
                _loopbackCapture.StartRecording();
                Log.Info("[Audio] Loopback capture started.");
            }
            catch (Exception ex) { Log.Warn($"[Audio] Loopback failed: {ex.Message}"); }
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

                _activeVBCableDevice = rends.FirstOrDefault(d =>
                    d.FriendlyName.IndexOf("CABLE",    StringComparison.OrdinalIgnoreCase) >= 0 ||
                    d.FriendlyName.IndexOf("VB-Audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    d.FriendlyName.IndexOf("Virtual",  StringComparison.OrdinalIgnoreCase) >= 0 ||
                    d.FriendlyName.IndexOf("Line ",    StringComparison.OrdinalIgnoreCase) >= 0);

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

        // ── Local Bridge Server ───────────────────────────────────────────────
        private void StartBridgeServer()
        {
            var bridge = LocalBridgeServer.Instance;
            bridge.OnPlaybackLevel += (level, channel) =>
            {
                if (this.IsDisposed) return;
                Action update = () =>
                {
                    if (channel == "agent")
                    {
                        if (level * 0.7f > _agentScriptLevel) _agentScriptLevel = level * 0.7f;
                        _agentScriptMeter?.Invalidate();
                    }
                    else
                    {
                        if (level * 0.7f > _customerScriptLevel) _customerScriptLevel = level * 0.7f;
                        _customerScriptMeter?.Invalidate();
                    }
                };
                if (this.InvokeRequired) this.BeginInvoke(update); else update();
            };
            bridge.OnPlaybackStopped += () =>
            {
                if (this.IsDisposed) return;
                Action reset = () =>
                {
                    _agentScriptLevel    = 0f;
                    _customerScriptLevel = 0f;
                    _agentScriptMeter?.Invalidate();
                    _customerScriptMeter?.Invalidate();
                    if (_activeMicDevice != null)
                    {
                        try { _micPassWaveIn?.StopRecording(); }  catch { }
                        try { _micPassWaveIn?.Dispose(); }        catch { }
                        _micPassWaveIn = null;
                        try { _micPassWaveOut?.Stop(); }    catch { }
                        try { _micPassWaveOut?.Dispose(); } catch { }
                        _micPassWaveOut = null;
                        StartMicPassThrough(_activeMicDevice.FriendlyName);
                        Log.Info("[PassThrough] Restarted after recording stopped.");
                    }
                };
                if (this.InvokeRequired) this.BeginInvoke(reset); else reset();
            };

            int savedAgent    = (int)(AppSettings.Instance.GetVolume("agentScript",    1.0f) * 100);
            int savedCustomer = (int)(AppSettings.Instance.GetVolume("customerScript", 0.55f) * 100);
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
            int agentPct   = Math.Max(1, (int)(s.GetVolume("agentScript",    0.48f) * 100));
            int custPct    = Math.Max(1, (int)(s.GetVolume("customerScript", 0.55f) * 100));
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
            _trayIcon?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
