/*
 * MainFormV5.cs  —  ONE Voice Solution v7.31
 *
 * UI REDESIGN v7.31:
 *   - Complete visual overhaul to match design mock exactly.
 *   - Dark space/nebula background with rounded dark card.
 *   - ONE logo (top-left) + "The Geniusness Is In The Simplicity" tagline centered in header.
 *   - 4 circular VU dial meters: Customer Voice (RED), Customer Recordings (BLUE),
 *     Agent Voice (PURPLE), Agent Recordings (GREEN).
 *   - Section dividers: "WHAT THE AGENT HEARS" / "WHAT THE CUSTOMER HEARS".
 *   - [–] [dB value] [+] volume controls below each meter label.
 *   - Two large device buttons: SELECT MICROPHONE (red glow) / SELECT SPEAKER (blue glow).
 *   - Footer: "One United Global LLC 2026. V 7.31"
 *   - ALL audio logic, routing, and meter data connections unchanged.
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
using System.Windows.Forms;
using WindowsFormsApp1.src;

namespace WindowsFormsApp1
{
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
        private const string APP_VERSION = "7.31";

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

            // ── Outer form: pure deep space black ─────────────────────────────
            g.FillRectangle(new SolidBrush(Color.FromArgb(4, 4, 10)), 0, 0, W, H);

            // ── Dark card with rounded corners ────────────────────────────────
            int cardPad  = (int)(18 * _scale);
            var cardRect = new Rectangle(cardPad, cardPad, W - cardPad * 2, H - cardPad * 2);
            int radius   = (int)(22 * _scale);

            // Card fill: very dark navy
            using (var cardPath = RoundedRect(cardRect, radius))
            using (var cardBrush = new SolidBrush(Color.FromArgb(235, 10, 10, 24)))
                g.FillPath(cardBrush, cardPath);

            // ── Top-center red radial glow (nebula behind header) ─────────────
            int topGlowW = (int)(W * 0.55f);
            int topGlowH = (int)(H * 0.28f);
            using (var gp = new GraphicsPath())
            {
                gp.AddEllipse(W / 2 - topGlowW / 2, cardPad - topGlowH / 3,
                              topGlowW, topGlowH);
                using (var pgb = new PathGradientBrush(gp))
                {
                    pgb.CenterColor    = Color.FromArgb(75, 200, 10, 10);
                    pgb.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(pgb, gp);
                }
            }

            // ── Bottom-right subtle blue/teal ambient glow ────────────────────
            int brGlowW = (int)(W * 0.45f);
            int brGlowH = (int)(H * 0.35f);
            using (var gp2 = new GraphicsPath())
            {
                gp2.AddEllipse(W - brGlowW + (int)(20 * _scale),
                               H - brGlowH + (int)(20 * _scale),
                               brGlowW, brGlowH);
                using (var pgb2 = new PathGradientBrush(gp2))
                {
                    pgb2.CenterColor    = Color.FromArgb(35, 0, 80, 140);
                    pgb2.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(pgb2, gp2);
                }
            }

            // ── Horizontal center glow line (light flare) ─────────────────────
            // Positioned at ~28% from top of card — just below header
            int headerH  = (int)(90 * _scale);
            int flareY   = cardPad + headerH + (int)(10 * _scale);
            int flareX1  = cardPad + (int)(30 * _scale);
            int flareX2  = cardPad + (W - cardPad * 2) - (int)(30 * _scale);

            // Wide soft outer halo (very transparent)
            for (int pass = 0; pass < 3; pass++)
            {
                int alpha  = new int[] { 12, 22, 35 }[pass];
                float wid  = new float[] { 28f, 14f, 5f }[pass];
                using (var haloPen = new Pen(Color.FromArgb(alpha, 220, 80, 40), wid))
                {
                    haloPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    haloPen.EndCap   = System.Drawing.Drawing2D.LineCap.Round;
                    g.DrawLine(haloPen, flareX1, flareY, flareX2, flareY);
                }
            }
            // Bright core line
            using (var corePen = new Pen(Color.FromArgb(200, 255, 120, 80), 1.5f))
            {
                corePen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                corePen.EndCap   = System.Drawing.Drawing2D.LineCap.Round;
                g.DrawLine(corePen, flareX1, flareY, flareX2, flareY);
            }
            // Bright center hotspot
            int hotW = (int)(W * 0.18f);
            using (var gph = new GraphicsPath())
            {
                gph.AddEllipse(W / 2 - hotW, flareY - (int)(8 * _scale), hotW * 2, (int)(16 * _scale));
                using (var pgbh = new PathGradientBrush(gph))
                {
                    pgbh.CenterColor    = Color.FromArgb(90, 255, 140, 80);
                    pgbh.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(pgbh, gph);
                }
            }

            // ── Card border: subtle rounded rect ──────────────────────────────
            using (var cardPath = RoundedRect(cardRect, radius))
            using (var borderPen = new Pen(Color.FromArgb(70, 80, 95, 120), 1.5f))
                g.DrawPath(borderPen, cardPath);
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
            int logoH   = (int)(64 * _scale);
            int logoW   = (int)(160 * _scale);
            int logoX   = cardPad + (int)(28 * _scale);
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

            // Tagline: positioned to the RIGHT of the logo, vertically centered in header
            // (matches mock: logo left, tagline fills remaining space to the right)
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
            // Center tagline in the space between logo-right-edge and window-right (minus close buttons)
            int logoRight  = logoX + logoW + (int)(16 * _scale);
            int rightEdge  = W - (int)(90 * _scale); // leave room for close/min buttons
            int availSpace = rightEdge - logoRight;
            int tagX       = logoRight + (availSpace - tagW) / 2;
            if (tagX < logoRight) tagX = logoRight;
            _lblTagline.Location = new Point(tagX, cardPad + (headerH - tagH) / 2);

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
            // sectionTop: just below the horizontal glow flare
            int sectionTop = cardPad + headerH + (int)(28 * _scale);

            // Sizing constants
            int footerH     = (int)(44 * _scale);
            int btnAreaH    = (int)(90 * _scale);
            int dbCtrlH     = (int)(34 * _scale);
            int lblH        = (int)(26 * _scale);
            int sectionLblH = (int)(28 * _scale);

            // Meter diameter: fill available width evenly across 4 slots
            int innerPad    = cardPad + (int)(24 * _scale);
            int usableW     = W - innerPad * 2;
            int meterSpacing = usableW / 4;
            int meterDiam   = (int)(meterSpacing * 0.88f);
            meterDiam = Math.Max(120, Math.Min((int)(200 * _scale), meterDiam));

            // Meter top: below section label row
            int meterTop = sectionTop + sectionLblH + (int)(14 * _scale);

            // ── Section labels ──────────────────────────────────────────────────
            // Left label spans over meters 0+1, right label spans over meters 2+3
            BuildSectionLabel(innerPad, sectionTop, meterSpacing * 2,
                              "WHAT THE ", "AGENT", " HEARS",
                              Color.FromArgb(195, 195, 205), ONE_RED);

            BuildSectionLabel(innerPad + meterSpacing * 2, sectionTop, meterSpacing * 2,
                              "WHAT THE ", "CUSTOMER", " HEARS",
                              Color.FromArgb(195, 195, 205), METER_GREEN);

            // NO vertical divider line — sections separated by natural spacing only

            // ── 4 Meters ─────────────────────────────────────────────────────────────
            // 0: Customer Voice (RED)       — left side (AGENT HEARS)
            // 1: Customer Recordings (BLUE) — left side
            // 2: Agent Voice (PURPLE)       — right side (CUSTOMER HEARS)
            // 3: Agent Recordings (GREEN)   — right side
            string[] labels = { "CUSTOMER VOICE", "CUSTOMER RECORDINGS", "AGENT VOICE", "AGENT RECORDINGS" };
            Color[]  colors = { METER_RED, METER_BLUE, METER_PURPLE, METER_GREEN };
            string[] keys   = { "customerVoice", "agentScript_left", "myMicLevel", "agentScript" };

            for (int i = 0; i < 4; i++)
            {
                // Center each meter within its equal slot
                int mx = innerPad + i * meterSpacing + (meterSpacing - meterDiam) / 2;
                int my = meterTop;

                // Circular meter panel
                BuildCircularMeter(mx, my, meterDiam, colors[i], keys[i]);

                // Label below meter
                int labelY = my + meterDiam + (int)(10 * _scale);
                var lbl = new Label
                {
                    Text      = labels[i],
                    ForeColor = Color.White,
                    BackColor = Color.Transparent,
                    Font      = new Font("Segoe UI", SF(11f), FontStyle.Bold),
                    AutoSize  = false,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Bounds    = new Rectangle(mx - (int)(8 * _scale), labelY,
                                              meterDiam + (int)(16 * _scale), lblH)
                };
                this.Controls.Add(lbl);

                // [–] [dB] [+] controls
                int ctrlY = labelY + lblH + (int)(3 * _scale);
                BuildDbControl(mx, ctrlY, meterDiam, colors[i], i);
            }
        }

        // ── Section label with colored keyword ────────────────────────────────
        private void BuildSectionLabel(int x, int y, int w,
                                       string prefix, string keyword, string suffix,
                                       Color baseColor, Color keyColor)
        {
            // We render this as a single owner-draw panel for exact color control
            var font     = new Font("Segoe UI", SF(11f), FontStyle.Bold);
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

                // Left line
                int lineY = ph / 2;
                using (var pen = new Pen(Color.FromArgb(80, 150, 150, 170), 1f))
                {
                    g.DrawLine(pen, 0, lineY, startX - (int)(8 * _scale), lineY);
                    g.DrawLine(pen, startX + totalTW + (int)(8 * _scale), lineY, pw, lineY);
                }

                // Text segments
                TextRenderer.DrawText(g, pref, font, new Point(startX, textY), bc);
                TextRenderer.DrawText(g, kw,   font, new Point(startX + prefW, textY), kc);
                TextRenderer.DrawText(g, suf,  font, new Point(startX + prefW + keyW, textY), bc);
            };
            this.Controls.Add(panel);
        }

        // ── Circular meter panel ──────────────────────────────────────────────
        private Panel BuildCircularMeter(int x, int y, int diam, Color meterColor, string key)
        {
            var panel = new Panel
            {
                Bounds    = new Rectangle(x, y, diam, diam),
                BackColor = Color.Transparent,
                Tag       = key
            };

            Color mc = meterColor;
            panel.Paint += (s, e) => DrawDialMeter(e.Graphics, (Panel)s, mc, key);
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
        private void DrawDialMeter(Graphics g, Panel panel, Color meterColor, string key)
        {
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            int W    = panel.Width;
            int H    = panel.Height;
            int cx   = W / 2;
            int cy   = H / 2;
            int r    = Math.Min(W, H) / 2 - 4;

            float level = GetLevel(key);

            // ── Outer ring: dark metallic ──────────────────────────────────────
            using (var outerBrush = new SolidBrush(Color.FromArgb(30, 30, 40)))
                g.FillEllipse(outerBrush, cx - r, cy - r, r * 2, r * 2);

            // Outer ring gradient (metallic rim)
            using (var rimPath = new GraphicsPath())
            {
                rimPath.AddEllipse(cx - r, cy - r, r * 2, r * 2);
                using (var pgb = new PathGradientBrush(rimPath))
                {
                    pgb.CenterPoint  = new PointF(cx, cy);
                    pgb.CenterColor  = Color.Transparent;
                    pgb.SurroundColors = new[] { Color.FromArgb(120, 60, 60, 70) };
                    g.FillPath(pgb, rimPath);
                }
            }

            // ── Tick marks (arc from ~210° to ~330° = 300° sweep) ─────────────
            float startAngle = 135f;   // bottom-left
            float sweepAngle = 270f;   // 270° sweep
            int   numTicks   = 36;
            int   rTick      = r - 2;
            int   rTickIn    = r - (int)(r * 0.14f);

            for (int t = 0; t <= numTicks; t++)
            {
                float angle = startAngle + (sweepAngle / numTicks) * t;
                float rad   = (float)(angle * Math.PI / 180.0);
                float fx1   = cx + rTick * (float)Math.Cos(rad);
                float fy1   = cy + rTick * (float)Math.Sin(rad);
                float fx2   = cx + rTickIn * (float)Math.Cos(rad);
                float fy2   = cy + rTickIn * (float)Math.Sin(rad);

                // Lit ticks glow in meter color, unlit are dark
                float tickFrac = (float)t / numTicks;
                bool  lit      = tickFrac <= level;
                Color tickCol  = lit
                    ? Color.FromArgb(220, meterColor)
                    : Color.FromArgb(50, 60, 65, 75);

                bool isMajor = (t % 6 == 0);
                float tickW  = isMajor ? 2.5f : 1.2f;
                if (lit && isMajor) tickW = 3f;

                using (var pen = new Pen(tickCol, tickW))
                    g.DrawLine(pen, fx1, fy1, fx2, fy2);
            }

            // ── Glow arc (lit portion) ─────────────────────────────────────────
            if (level > 0.01f)
            {
                int arcR = r - (int)(r * 0.08f);
                var arcRect = new RectangleF(cx - arcR, cy - arcR, arcR * 2, arcR * 2);
                float litSweep = sweepAngle * level;
                using (var glowPen = new Pen(Color.FromArgb(180, meterColor), 3.5f))
                {
                    glowPen.LineJoin = LineJoin.Round;
                    g.DrawArc(glowPen, arcRect, startAngle, litSweep);
                }
                // Outer soft glow
                using (var glowPen2 = new Pen(Color.FromArgb(60, meterColor), 7f))
                    g.DrawArc(glowPen2, arcRect, startAngle, litSweep);
            }

            // ── Inner dark circle ──────────────────────────────────────────────
            int innerR = (int)(r * 0.72f);
            using (var innerBrush = new LinearGradientBrush(
                new Rectangle(cx - innerR, cy - innerR, innerR * 2, innerR * 2),
                Color.FromArgb(22, 22, 35),
                Color.FromArgb(10, 10, 20),
                LinearGradientMode.ForwardDiagonal))
            {
                g.FillEllipse(innerBrush, cx - innerR, cy - innerR, innerR * 2, innerR * 2);
            }

            // Inner ring subtle glow
            using (var innerRimPen = new Pen(Color.FromArgb(60, meterColor), 1.5f))
                g.DrawEllipse(innerRimPen, cx - innerR, cy - innerR, innerR * 2, innerR * 2);

            // ── Percentage value text (centered) ──────────────────────────────
            // Convert level (0–1) to percentage: 0% = silent, 100% = full
            int pctVal   = (int)Math.Round(level * 100f);
            string pctStr = pctVal.ToString() + "%";

            float numSize = SF(28f) * (innerR / 60f);
            numSize = Math.Max(SF(14f), Math.Min(SF(32f), numSize));
            using (var numFont = new Font("Segoe UI", numSize, FontStyle.Bold))
            {
                var numSz = TextRenderer.MeasureText(pctStr, numFont);
                int numX  = cx - numSz.Width / 2;
                int numY  = cy - numSz.Height / 2;
                Color numCol = level > 0.01f ? meterColor : Color.FromArgb(120, 120, 140);
                TextRenderer.DrawText(g, pctStr, numFont, new Point(numX, numY), numCol);
            }

            // ── Clockwise arc fill ring (replaces needle) ─────────────────────
            if (level > 0.005f)
            {
                int   arcR    = (int)(innerR * 1.22f);
                float arcSweep = 360f * level;
                // Start from top (-90 degrees)
                float arcStart = -90f;
                var   arcRect  = new RectangleF(cx - arcR, cy - arcR, arcR * 2, arcR * 2);

                // Outer soft glow layer
                using (var glowPen = new Pen(Color.FromArgb(50, meterColor), (int)(12 * _scale)))
                {
                    glowPen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                    g.DrawArc(glowPen, arcRect, arcStart, arcSweep);
                }
                // Mid glow layer
                using (var midPen = new Pen(Color.FromArgb(120, meterColor), (int)(7 * _scale)))
                    g.DrawArc(midPen, arcRect, arcStart, arcSweep);
                // Bright core arc
                using (var corePen = new Pen(Color.FromArgb(240, meterColor), (int)(3 * _scale)))
                    g.DrawArc(corePen, arcRect, arcStart, arcSweep);

                // Tip dot at arc endpoint
                float tipAngleRad = (float)((arcStart + arcSweep) * Math.PI / 180.0);
                float tipX = cx + arcR * (float)Math.Cos(tipAngleRad);
                float tipY = cy + arcR * (float)Math.Sin(tipAngleRad);
                int   tipR = (int)(5 * _scale);
                using (var tipBrush = new SolidBrush(Color.FromArgb(255, meterColor)))
                    g.FillEllipse(tipBrush, tipX - tipR, tipY - tipR, tipR * 2, tipR * 2);
                using (var tipGlow = new Pen(Color.FromArgb(80, meterColor), (int)(8 * _scale)))
                    g.DrawEllipse(tipGlow, tipX - tipR - 3, tipY - tipR - 3,
                                  (tipR + 3) * 2, (tipR + 3) * 2);
            }
        }

        // ── [–] [dB] [+] control row ──────────────────────────────────────────
        private void BuildDbControl(int meterX, int y, int meterW, Color accentColor, int channelIndex)
        {
            // Sizing — slightly smaller than original to be subtle
            int btnW  = (int)(28 * _scale);   // ~10% smaller than 32
            int btnH  = (int)(28 * _scale);
            int valW  = (int)(54 * _scale);
            int totalW = btnW + valW + btnW + (int)(6 * _scale);
            int startX = meterX + (meterW - totalW) / 2;
            int gap    = (int)(3 * _scale);

            // [–] button
            var btnMinus = new Button
            {
                Text      = "–",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(28, 28, 42),
                Font      = new Font("Segoe UI", SF(13f), FontStyle.Bold),
                Bounds    = new Rectangle(startX, y, btnW, btnH),
                Cursor    = Cursors.Hand,
                TabStop   = false
            };
            btnMinus.FlatAppearance.BorderColor = Color.FromArgb(80, accentColor);
            btnMinus.FlatAppearance.BorderSize  = 1;
            // Idle: low glow; Hover: strong glow
            btnMinus.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, accentColor);
            ApplyButtonGlow(btnMinus, accentColor, idle: true);

            // Value label
            var lblVal = new Label
            {
                Text      = GetDbText(channelIndex),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(20, 20, 35),
                Font      = new Font("Segoe UI", SF(12f), FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(startX + btnW + gap, y, valW, btnH)
            };

            // [+] button
            var btnPlus = new Button
            {
                Text      = "+",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(28, 28, 42),
                Font      = new Font("Segoe UI", SF(13f), FontStyle.Bold),
                Bounds    = new Rectangle(startX + btnW + gap + valW + gap, y, btnW, btnH),
                Cursor    = Cursors.Hand,
                TabStop   = false
            };
            btnPlus.FlatAppearance.BorderColor = Color.FromArgb(80, accentColor);
            btnPlus.FlatAppearance.BorderSize  = 1;
            btnPlus.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, accentColor);
            ApplyButtonGlow(btnPlus, accentColor, idle: true);

            // Store dB label reference
            switch (channelIndex)
            {
                case 0: _lblDbCustomerVoice   = lblVal; break;
                case 1: _lblDbCustomerScript  = lblVal; break;
                case 2: _lblDbAgentVoice      = lblVal; break;
                case 3: _lblDbAgentScript     = lblVal; break;
            }

            // Wire up click handlers
            int ch = channelIndex;
            Label lv = lblVal;
            btnMinus.Click += (s, e) => AdjustDb(ch, -1, lv);
            btnPlus.Click  += (s, e) => AdjustDb(ch, +1, lv);

            // Hover glow effect
            AttachHoverGlow(btnMinus, accentColor);
            AttachHoverGlow(btnPlus,  accentColor);

            this.Controls.Add(btnMinus);
            this.Controls.Add(lblVal);
            this.Controls.Add(btnPlus);
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
                    lblVal.Text = _dbCustomerVoice.ToString();
                    _customerVoiceVolume = DbToLinear(_dbCustomerVoice);
                    AppSettings.Instance.SpeakerSystemVolume = DbToPercent(_dbCustomerVoice);
                    break;
                case 1:
                    _dbCustomerScript = Math.Max(-20, Math.Min(6, _dbCustomerScript + delta));
                    lblVal.Text = _dbCustomerScript.ToString();
                    AppSettings.Instance.SetVolume("agentScript", DbToLinear(_dbCustomerScript));
                    LocalBridgeServer.Instance.SetVolume("agent", DbToPercent(_dbCustomerScript));
                    break;
                case 2:
                    _dbAgentVoice = Math.Max(-20, Math.Min(6, _dbAgentVoice + delta));
                    lblVal.Text = _dbAgentVoice.ToString();
                    if (_activeMicDevice != null)
                        try { _activeMicDevice.AudioEndpointVolume.MasterVolumeLevelScalar = DbToLinear(_dbAgentVoice); } catch { }
                    AppSettings.Instance.MicSystemVolume = DbToPercent(_dbAgentVoice);
                    break;
                case 3:
                    _dbAgentScript = Math.Max(-20, Math.Min(6, _dbAgentScript + delta));
                    lblVal.Text = _dbAgentScript.ToString();
                    AppSettings.Instance.SetVolume("customerScript", DbToLinear(_dbAgentScript));
                    LocalBridgeServer.Instance.SetVolume("customer", DbToPercent(_dbAgentScript));
                    break;
            }
            AppSettings.Instance.Save();
        }

        private static float DbToLinear(int db) => (float)Math.Pow(10.0, db / 20.0);
        private static int   DbToPercent(int db) => (int)(DbToLinear(db) * 100f);

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
            int btnH     = (int)(58 * _scale);
            int btnW     = (int)(Math.Min(W * 0.30f, 340 * _scale));
            int btnY     = H - footerH - btnAreaH + (btnAreaH - btnH) / 2;
            int gap      = (int)(30 * _scale);
            int totalBtnW = btnW * 2 + gap;
            int btnX0    = (W - totalBtnW) / 2;

            // SELECT MICROPHONE — red glow
            var btnMic = new Button
            {
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(18, 18, 30),
                Font      = new Font("Segoe UI", SF(14f), FontStyle.Bold),
                Bounds    = new Rectangle(btnX0, btnY, btnW, btnH),
                Cursor    = Cursors.Hand,
                TabStop   = false,
                Text      = "  \uD83C\uDF99  SELECT MICROPHONE   \u2304"
            };
            btnMic.FlatAppearance.BorderColor = METER_RED;
            btnMic.FlatAppearance.BorderSize  = 2;
            btnMic.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 180, 0, 0);
            AttachDeviceButtonHover(btnMic, METER_RED);
            btnMic.Click += (s, e) => ShowMicDropdown(btnMic);
            this.Controls.Add(btnMic);

            // SELECT SPEAKER — blue glow
            var btnSpk = new Button
            {
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(18, 18, 30),
                Font      = new Font("Segoe UI", SF(14f), FontStyle.Bold),
                Bounds    = new Rectangle(btnX0 + btnW + gap, btnY, btnW, btnH),
                Cursor    = Cursors.Hand,
                TabStop   = false,
                Text      = "  \uD83D\uDD0A  SELECT SPEAKER   \u2304"
            };
            btnSpk.FlatAppearance.BorderColor = METER_BLUE;
            btnSpk.FlatAppearance.BorderSize  = 2;
            btnSpk.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 0, 120, 200);
            AttachDeviceButtonHover(btnSpk, METER_BLUE);
            btnSpk.Click += (s, e) => ShowSpeakerDropdown(btnSpk);
            this.Controls.Add(btnSpk);

            // Store references for PopulateDevices
            _cboMic     = new ComboBox { Visible = false, Location = new Point(-500, -500), Width = 1 };
            _cboHeadset = new ComboBox { Visible = false, Location = new Point(-500, -500), Width = 1 };
            this.Controls.Add(_cboMic);
            this.Controls.Add(_cboHeadset);

            // Keep button text updated after device selection
            _cboMic.SelectedIndexChanged += (s, e) =>
            {
                if (_cboMic.SelectedIndex >= 0)
                    btnMic.Text = "  \uD83C\uDF99  " + TruncateDevice(_cboMic.Text, 22) + "   \u2304";
            };
            _cboHeadset.SelectedIndexChanged += (s, e) =>
            {
                if (_cboHeadset.SelectedIndex >= 0)
                    btnSpk.Text = "  \uD83D\uDD0A  " + TruncateDevice(_cboHeadset.Text, 22) + "   \u2304";
            };
        }

        private string TruncateDevice(string name, int maxLen)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return name.Length > maxLen ? name.Substring(0, maxLen) + "…" : name;
        }

        private void AttachDeviceButtonHover(Button btn, Color accent)
        {
            btn.MouseEnter += (s, e) =>
            {
                btn.FlatAppearance.BorderColor = Color.FromArgb(255, accent);
                btn.BackColor = Color.FromArgb(30, accent);
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.FlatAppearance.BorderColor = accent;
                btn.BackColor = Color.FromArgb(18, 18, 30);
            };
        }

        private void ShowMicDropdown(Button anchor)
        {
            var ctx = new ContextMenuStrip();
            ctx.BackColor = Color.FromArgb(22, 22, 38);
            ctx.ForeColor = Color.White;
            ctx.Font      = new Font("Segoe UI", SF(11f), FontStyle.Regular);
            foreach (string item in _cboMic.Items)
            {
                string it = item;
                var mi = new ToolStripMenuItem(it)
                {
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(22, 22, 38)
                };
                mi.Click += (s, e) =>
                {
                    int idx = _cboMic.Items.IndexOf(it);
                    if (idx >= 0) _cboMic.SelectedIndex = idx;
                };
                ctx.Items.Add(mi);
            }
            ctx.Show(anchor, new Point(0, anchor.Height));
        }

        private void ShowSpeakerDropdown(Button anchor)
        {
            var ctx = new ContextMenuStrip();
            ctx.BackColor = Color.FromArgb(22, 22, 38);
            ctx.ForeColor = Color.White;
            ctx.Font      = new Font("Segoe UI", SF(11f), FontStyle.Regular);
            foreach (string item in _cboHeadset.Items)
            {
                string it = item;
                var mi = new ToolStripMenuItem(it)
                {
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(22, 22, 38)
                };
                mi.Click += (s, e) =>
                {
                    int idx = _cboHeadset.Items.IndexOf(it);
                    if (idx >= 0) _cboHeadset.SelectedIndex = idx;
                };
                ctx.Items.Add(mi);
            }
            ctx.Show(anchor, new Point(0, anchor.Height));
        }

        // ── Footer ────────────────────────────────────────────────────────────
        private void BuildFooter(int W, int H, int cardPad)
        {
            int footerH = (int)(44 * _scale);
            int fy      = H - cardPad - footerH;

            _lblFooterCenter = new Label
            {
                Text      = "One United Global LLC 2026.  V 7.31",
                ForeColor = Color.FromArgb(160, 165, 175),
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(11f), FontStyle.Regular),
                AutoSize  = true
            };
            this.Controls.Add(_lblFooterCenter);
            int fcW = TextRenderer.MeasureText(_lblFooterCenter.Text, _lblFooterCenter.Font).Width;
            int fcH = TextRenderer.MeasureText(_lblFooterCenter.Text, _lblFooterCenter.Font).Height;
            _lblFooterCenter.Location = new Point((W - fcW) / 2, fy + (footerH - fcH) / 2);
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
            this.Resize += (s, e) =>
            {
                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.ShowInTaskbar = false;
                    _trayIcon.ShowBalloonTip(2000, "ONE Voice Solution",
                        "Minimized to tray — double-click to restore.", ToolTipIcon.Info);
                }
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
                    _micLevel = Math.Min(1f, max * 4.0f);
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
                    float level = Math.Min(1f, max * 3.5f);
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
                        if (level > _agentScriptLevel) _agentScriptLevel = level;
                        _agentScriptMeter?.Invalidate();
                    }
                    else
                    {
                        if (level > _customerScriptLevel) _customerScriptLevel = level;
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

            int savedAgent    = (int)(AppSettings.Instance.GetVolume("agentScript",    0.48f) * 100);
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
            string oneLogo    = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "one_logo.png");
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
