/*
 * MainFormV5.cs  —  ONE Voice Solution v7.37
 *
 * UI REDESIGN v7.37 — Full visual overhaul: laser divider, section labels, meter glow, controls depth.
 *
 *   METER GEOMETRY (SVG canvas 200×200, scaled to panel):
 *     1. Outer soft glow   : r=90, stroke-width=10, opacity=0.15
 *     2. Segment ring      : r=82, stroke-width=12, dasharray=28 16, opacity=0.7
 *     3. Tick ring         : r=68, stroke-width=2,  dasharray=2 6,   gray, opacity=0.5
 *     4. Progress arc      : r=65, stroke-width=14, dashoffset=(1-level)*408, round cap, glow
 *     5. Inner core        : radial gradient black→#070707, r=60
 *     6. Center badge      : dark bg, colored glow, % text, 24px bold
 *
 *   LABELS:
 *     - AGENT in red (#ff3b3b)
 *     - CUSTOMER in green (#2cff88)
 *
 *   ALL audio/backend logic unchanged.
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
        private static readonly Color ONE_RED      = Color.FromArgb(255, 59, 59);
        private static readonly Color BG_DARK      = Color.FromArgb(7, 8, 18);
        private static readonly Color TEXT_WHITE   = Color.White;
        private static readonly Color ONE_BLUE_SEL = Color.FromArgb(0, 102, 204);

        // Meter colours — exact match to ui_mockup.html
        private static readonly Color METER_RED    = Color.FromArgb(255,  59,  59);  // #ff3b3b
        private static readonly Color METER_BLUE   = Color.FromArgb( 59, 163, 255);  // #3ba3ff
        private static readonly Color METER_PURPLE = Color.FromArgb(178, 107, 255);  // #b26bff
        private static readonly Color METER_GREEN  = Color.FromArgb( 44, 255, 136);  // #2cff88

        // ── Version ───────────────────────────────────────────────────────────
        private const string APP_VERSION = "7.37";

        // ── Scale ─────────────────────────────────────────────────────────────
        private float _scale = 1.0f;
        private float SF(float pt) => Math.Max(7f, (float)Math.Round(pt * _scale, 1));

        // DPI helpers
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("gdi32.dll")]  private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
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
        private Panel       _myMicMeterLeft;
        private Panel       _agentScriptMeter;
        private Panel       _customerVoiceMeter;
        private Panel       _customerScriptMeter;
        private Label       _lblFooterCenter;
        private NotifyIcon  _trayIcon;
        private ContextMenuStrip _trayMenu;
        private System.Windows.Forms.Timer _meterTimer;
        private Button _btnClose;
        private Button _btnMinimize;

        private MMDevice _activeMicDevice;
        private MMDevice _activeSpeakerDevice;
        private MMDevice _activeVBCableDevice;

        // Volume gain values
        private float _volCustomerVoice  = 1.0f;
        private float _volCustomerScript = 1.0f;
        private float _volAgentVoice     = 1.0f;
        private float _volAgentScript    = 1.0f;

        // dB display values
        private int _dbCustomerVoice   = -3;
        private int _dbCustomerScript  = -3;
        private int _dbAgentVoice      = -3;
        private int _dbAgentScript     = -3;

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

        private WasapiLoopbackCapture _loopbackCapture;
        private float                 _customerVoiceVolume = 1.0f;

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
            int cx = wa.Left + (wa.Width  - w) / 2;
            int cy = wa.Top  + (wa.Height - h) / 2;
            if (cx < wa.Left) cx = wa.Left;
            if (cy < wa.Top)  cy = wa.Top;
            this.Location = new Point(cx, cy);

            Log.Info($"[UI] Screen={screen.DeviceName} DPI={dpi} WA={wa} FormSize={w}x{h} Scale={_scale:F2}");
        }

        // ══════════════════════════════════════════════════════════════════════
        // FORM PAINT — background + red glow divider
        // ══════════════════════════════════════════════════════════════════════
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int W = this.ClientSize.Width;
            int H = this.ClientSize.Height;

            // ── Background: radial gradient dark blue-black ───────────────────
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(-W / 2, -H / 2, W * 2, H * 2);
                using (var pgb = new PathGradientBrush(path))
                {
                    pgb.CenterPoint    = new PointF(W / 2f, H / 2f);
                    pgb.CenterColor    = Color.FromArgb(7, 8, 18);
                    pgb.SurroundColors = new[] { Color.FromArgb(2, 3, 10) };
                    g.FillRectangle(pgb, 0, 0, W, H);
                }
            }

            // ── Red glow divider line (matches .divider in mockup) ────────────
            int headerH = (int)(90 * _scale);
            int cardPad = (int)(18 * _scale);
            int divY    = cardPad + headerH + (int)(10 * _scale);

            // ── Laser line: thin horizontal glow, strong center flare, fades to edges ──
            // Horizontal glow (edge-based, not vertical thickness)
            int[] laserAlpha = { 6, 14, 30, 60 };
            int[] laserWidth = { (int)(22*_scale), (int)(10*_scale), (int)(4*_scale), (int)(2*_scale) };
            for (int i = 0; i < laserAlpha.Length; i++)
            {
                using (var lp = new Pen(Color.FromArgb(laserAlpha[i], ONE_RED), laserWidth[i]))
                    g.DrawLine(lp, 0, divY, W, divY);
            }
            // 1px core line
            using (var lp = new Pen(Color.FromArgb(230, ONE_RED), 1f))
                g.DrawLine(lp, 0, divY, W, divY);
            // Center flare: radial gradient ellipse at center
            int flareW = (int)(W * 0.35f);
            int flareH = (int)(18 * _scale);
            int flareX = W / 2 - flareW / 2;
            int flareY = divY - flareH / 2;
            using (var flarePath = new System.Drawing.Drawing2D.GraphicsPath())
            {
                flarePath.AddEllipse(flareX, flareY, flareW, flareH);
                using (var flareBrush = new System.Drawing.Drawing2D.PathGradientBrush(flarePath))
                {
                    flareBrush.CenterPoint    = new PointF(W / 2f, divY);
                    flareBrush.CenterColor    = Color.FromArgb(120, ONE_RED);
                    flareBrush.SurroundColors = new[] { Color.FromArgb(0, ONE_RED) };
                    g.FillEllipse(flareBrush, flareX, flareY, flareW, flareH);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // BUILD UI
        // ══════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            int W = this.ClientSize.Width;
            int H = this.ClientSize.Height;
            int cardPad = (int)(18 * _scale);

            BuildWindowButtons(W, cardPad);
            BuildHeader(W, cardPad);
            BuildMeterSection(W, H, cardPad);
            BuildDeviceButtons(W, H, cardPad);
            BuildFooter(W, H, cardPad);
            BuildHiddenTrackBars();
        }

        // ── Window close/minimize buttons ─────────────────────────────────────
        private void BuildWindowButtons(int W, int cardPad)
        {
            int btnSz = (int)(30 * _scale);
            int btnY  = cardPad + (int)(10 * _scale);

            _btnClose = new Button
            {
                Text      = "\u2715",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(55, 55, 65),
                Font      = new Font("Segoe UI", SF(11f), FontStyle.Bold),
                Bounds    = new Rectangle(W - btnSz - cardPad - (int)(10 * _scale), btnY, btnSz, btnSz),
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
                Bounds    = new Rectangle(W - btnSz * 2 - cardPad - (int)(18 * _scale), btnY, btnSz, btnSz),
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

        // ── Header: logo left + plain white title centered ───────────────────
        private void BuildHeader(int W, int cardPad)
        {
            int headerH = (int)(90 * _scale);

            // Logo — drawn as text "ONE VOICE" to match the target screenshot
            int logoH = (int)(64 * _scale);
            int logoW = (int)(200 * _scale);
            int logoX = cardPad + (int)(12 * _scale);
            int logoY = cardPad + (headerH - logoH) / 2;

            // Try to load image logo first; fall back to painted text logo
            _logoBox = new PictureBox
            {
                Bounds    = new Rectangle(logoX, logoY, logoW, logoH),
                SizeMode  = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            LoadLogo();

            // If no image loaded, paint the logo as text
            if (_logoBox.Image == null)
            {
                _logoBox.Paint += (s, e2) =>
                {
                    var g2 = e2.Graphics;
                    g2.SmoothingMode = SmoothingMode.AntiAlias;
                    int pw = _logoBox.Width;
                    int ph = _logoBox.Height;

                    // "ONE" in bold red
                    float oneSz  = Math.Max(10f, 32f * _scale);
                    float voiceSz = Math.Max(7f, 13f * _scale);
                    using (var fOne   = new Font("Segoe UI", oneSz,   FontStyle.Bold,    GraphicsUnit.Pixel))
                    using (var fVoice = new Font("Segoe UI", voiceSz, FontStyle.Regular, GraphicsUnit.Pixel))
                    {
                        SizeF szOne   = g2.MeasureString("ONE",   fOne);
                        SizeF szVoice = g2.MeasureString("VOICE", fVoice);
                        float totalW  = szOne.Width + (int)(8 * _scale) + szVoice.Width;
                        float startX  = (pw - totalW) / 2f;
                        float baseY   = (ph - szOne.Height) / 2f;

                        // Glow behind ONE
                        for (int gi = 0; gi < 3; gi++)
                        {
                            int ga2 = new int[]{ 20, 45, 80 }[gi];
                            float ge = new float[]{ 6f, 3f, 1.5f }[gi] * _scale;
                            using (var gb = new SolidBrush(Color.FromArgb(ga2, ONE_RED)))
                                g2.FillRectangle(gb, startX - ge, baseY - ge, szOne.Width + ge * 2, szOne.Height + ge * 2);
                        }

                        using (var redBrush   = new SolidBrush(ONE_RED))
                        using (var whiteBrush = new SolidBrush(Color.White))
                        {
                            g2.DrawString("ONE",   fOne,   redBrush,   startX, baseY);
                            g2.DrawString("VOICE", fVoice, whiteBrush, startX + szOne.Width + (int)(8 * _scale),
                                          baseY + szOne.Height - szVoice.Height - (int)(2 * _scale));
                        }
                    }
                };
            }
            this.Controls.Add(_logoBox);
            AttachDrag(_logoBox);

            // Title — plain white text, no red background
            string titleText = "The Geniusness Is In The Simplicity";
            var    titleFont = new Font("Segoe UI", SF(18f), FontStyle.Bold);
            var    titleSz   = TextRenderer.MeasureText(titleText, titleFont);
            int    blockW    = titleSz.Width  + (int)(20 * _scale);
            int    blockH    = titleSz.Height + (int)(10 * _scale);
            int    blockX    = (W - blockW) / 2;
            int    blockY    = cardPad + (headerH - blockH) / 2;

            var titlePanel = new Panel
            {
                Bounds    = new Rectangle(blockX, blockY, blockW, blockH),
                BackColor = Color.Transparent
            };
            string tt = titleText;
            Font   tf = titleFont;
            Size   ts = titleSz;
            titlePanel.Paint += (s, e2) =>
            {
                var g2 = e2.Graphics;
                g2.SmoothingMode = SmoothingMode.AntiAlias;
                int pw = titlePanel.Width;
                int ph = titlePanel.Height;
                int tx = (pw - ts.Width)  / 2;
                int ty = (ph - ts.Height) / 2;
                // Subtle red glow passes behind text
                for (int gi = 0; gi < 3; gi++)
                {
                    int   ga2 = new int[]{ 8, 18, 35 }[gi];
                    int   off = new int[]{ 3, 2, 1 }[gi];
                    using (var gbrush = new SolidBrush(Color.FromArgb(ga2, ONE_RED)))
                        g2.FillRectangle(gbrush, tx - off, ty - off, ts.Width + off * 2, ts.Height + off * 2);
                }
                // White text
                TextRenderer.DrawText(g2, tt, tf, new Point(tx, ty), Color.White);
            };
            this.Controls.Add(titlePanel);
            AttachDrag(titlePanel);

            // Agent name label (hidden, kept for heartbeat logic)
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

        // ── Meter section ─────────────────────────────────────────────────────
        // Matches: .section-row (two columns), .meters (4 columns), each .ring 200x200
        private void BuildMeterSection(int W, int H, int cardPad)
        {
            int headerH    = (int)(90 * _scale);
            int divH       = (int)(14 * _scale);
            int sectionTop = cardPad + headerH + divH + (int)(25 * _scale);

            int innerPad  = cardPad + (int)(24 * _scale);
            int usableW   = W - innerPad * 2;
            int slotW     = usableW / 4;

            // Meter diameter: target is 200px at 1x scale
            int meterDiam = (int)(slotW * 0.88f);
            meterDiam = Math.Max(120, Math.Min((int)(200 * _scale), meterDiam));

            int sectionLblH = (int)(28 * _scale);
            int meterTop    = sectionTop + sectionLblH + (int)(4 * _scale);  // tighter: moved up

            // Section labels
            // Left:  "WHAT THE AGENT HEARS"    — AGENT in red
            // Right: "WHAT THE CUSTOMER HEARS" — CUSTOMER in green
            BuildSectionLabel(innerPad, sectionTop, slotW * 2,
                              "WHAT THE ", "AGENT", " HEARS",
                              Color.FromArgb(200, 200, 210), METER_RED);
            BuildSectionLabel(innerPad + slotW * 2, sectionTop, slotW * 2,
                              "WHAT THE ", "CUSTOMER", " HEARS",
                              Color.FromArgb(200, 200, 210), METER_GREEN);

            // Meter order: RED, BLUE, PURPLE, GREEN
            string[] labels = { "CUSTOMER VOICE", "AGENT RECORDINGS", "AGENT VOICE", "AGENT RECORDINGS" };
            Color[]  colors = { METER_RED, METER_BLUE, METER_PURPLE, METER_GREEN };
            string[] keys   = { "customerVoice", "agentScript_left", "myMicLevel", "agentScript" };

            int lblH   = (int)(22 * _scale);
            int btnSz  = (int)(44 * _scale);
            int numW   = (int)(56 * _scale);
            int gap    = (int)(6  * _scale);

            for (int i = 0; i < 4; i++)
            {
                int slotCX = innerPad + i * slotW + slotW / 2;
                int mx     = slotCX - meterDiam / 2;
                int my     = meterTop;

                BuildCircularMeter(mx, my, meterDiam, colors[i], keys[i]);

                // Label below meter
                int labelY = my + meterDiam + (int)(6 * _scale);   // tighter: label closer to meter
                var lbl = new Label
                {
                    Text      = labels[i],
                    ForeColor = Color.FromArgb(200, 200, 210),
                    BackColor = Color.Transparent,
                    Font      = new Font("Segoe UI", SF(10f), FontStyle.Bold),
                    AutoSize  = false,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Bounds    = new Rectangle(slotCX - slotW / 2, labelY, slotW, lblH)
                };
                this.Controls.Add(lbl);

                // Volume controls: [–] [val] [+]
                int ctrlY   = labelY + lblH + (int)(5 * _scale);    // tighter: controls closer to label
                int totalVW = btnSz + gap + numW + gap + btnSz;
                int startVX = slotCX - totalVW / 2;
                BuildVolumeControl(startVX, ctrlY, btnSz, numW, gap, colors[i], i);
            }
        }

        // ── Section label with colored keyword ────────────────────────────────
        private void BuildSectionLabel(int x, int y, int w,
                                       string prefix, string keyword, string suffix,
                                       Color baseColor, Color keyColor)
        {
            var font   = new Font("Segoe UI", SF(11f), FontStyle.Bold);
            int prefW  = TextRenderer.MeasureText(prefix,  font).Width;
            int keyW   = TextRenderer.MeasureText(keyword, font).Width;
            int sufW   = TextRenderer.MeasureText(suffix,  font).Width;
            int totalW = prefW + keyW + sufW;
            int textH  = TextRenderer.MeasureText(keyword, font).Height;

            var panel = new Panel
            {
                Bounds    = new Rectangle(x, y, w, textH + (int)(8 * _scale)),
                BackColor = Color.Transparent
            };
            string pref = prefix, kw = keyword, suf = suffix;
            Color  bc   = baseColor, kc = keyColor;
            int    tw   = totalW, th = textH, pw2 = prefW, kw2 = keyW;
            panel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int ph     = panel.Height;
                int textY  = (ph - th) / 2;
                int startX = (panel.Width - tw) / 2;
                int kwX    = startX + pw2;
                // Glow behind keyword only (2 passes)
                for (int gi = 0; gi < 2; gi++)
                {
                    int   ga  = gi == 0 ? 25 : 55;
                    float off = gi == 0 ? 3f : 1.5f;
                    using (var gb = new SolidBrush(Color.FromArgb(ga, kc)))
                        g.FillRectangle(gb, kwX - off, textY - off, kw2 + off * 2, th + off * 2);
                }
                // Brighter base text (~20% brighter)
                TextRenderer.DrawText(g, pref, font, new Point(startX, textY), Color.FromArgb(240, 240, 248));
                TextRenderer.DrawText(g, kw,   font, new Point(kwX,    textY), kc);
                TextRenderer.DrawText(g, suf,  font, new Point(kwX + kw2, textY), Color.FromArgb(240, 240, 248));
                // Thin accent underline below the full label
                int lineY = textY + th + 2;
                using (var lp = new Pen(Color.FromArgb(160, kc), 1.5f))
                    g.DrawLine(lp, startX, lineY, startX + tw, lineY);
            };
            this.Controls.Add(panel);
        }

        // ── Circular meter panel ──────────────────────────────────────────────
        private Panel BuildCircularMeter(int x, int y, int diam, Color meterColor, string key)
        {
            var panel = new DoubleBufferedPanel
            {
                Bounds    = new Rectangle(x, y, diam, diam),
                BackColor = Color.Transparent,
                Tag       = key
            };
            Color mc = meterColor;
            panel.Paint += (s, e) => { var _p = (Panel)s; DrawDialMeter(e.Graphics, new Rectangle(0, 0, _p.Width, _p.Height), GetVolumePct(key), mc); };
            this.Controls.Add(panel);

            switch (key)
            {
                case "myMicLevel":       _myMicMeterLeft      = panel; break;
                case "customerVoice":    _customerVoiceMeter  = panel; break;
                case "agentScript":      _agentScriptMeter    = panel; break;
                case "agentScript_left": _customerScriptMeter = panel; break;
            }
            return panel;
        }
        // ══════════════════════════════════════════════════════════════════════
        // DRAW DIAL METER v7.37 — neon ring, tight glow, no box, cinematic
        // ══════════════════════════════════════════════════════════════════════
        private void DrawDialMeter(Graphics g, Rectangle bounds, float percent, Color baseColor)
        {
            g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.InterpolationMode  = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            float cx     = bounds.X + bounds.Width  / 2f;
            float cy     = bounds.Y + bounds.Height / 2f;
            float radius = bounds.Width / 2f - 8f;

            // Derived color tones
            Color brightTone = Color.FromArgb(
                Math.Min(255, baseColor.R + 60),
                Math.Min(255, baseColor.G + 60),
                Math.Min(255, baseColor.B + 60));
            Color darkTone = Color.FromArgb(
                (int)(baseColor.R * 0.55f),
                (int)(baseColor.G * 0.55f),
                (int)(baseColor.B * 0.55f));

            // ── ENERGY BURST (stable particles, very low opacity) ─────────────
            var rand = new Random(42);
            for (int i = 0; i < 40; i++)
            {
                double a = rand.NextDouble() * Math.PI * 2;
                double r = radius + 8 + rand.NextDouble() * 30;
                float  x = cx + (float)(Math.Cos(a) * r);
                float  y = cy + (float)(Math.Sin(a) * r);
                using (var b = new SolidBrush(Color.FromArgb(14, darkTone)))
                    g.FillRectangle(b, x, y, 2, 2);
            }

            // ── OUTER GLOW — tight falloff, strongest at ring edge (8–10 passes) ──
            for (int i = 9; i >= 0; i--)
            {
                int   alpha = Math.Max(1, 28 - i * 3);
                float w     = radius + i * 1.8f;
                float pen   = Math.Max(1f, 16f - i * 1.2f);
                using (var p = new Pen(Color.FromArgb(alpha, i < 3 ? brightTone : darkTone), pen))
                    g.DrawEllipse(p, cx - w, cy - w, w * 2f, w * 2f);
            }

            // ── HARD EDGE RING ────────────────────────────────────────────────
            using (var p = new Pen(baseColor, 2.5f))
                g.DrawEllipse(p, cx - radius, cy - radius, radius * 2f, radius * 2f);

            // ── INNER RIM LINE (just inside the ring) ─────────────────────────
            float rimR = radius - 5f;
            using (var p = new Pen(Color.FromArgb(60, brightTone), 1f))
                g.DrawEllipse(p, cx - rimR, cy - rimR, rimR * 2f, rimR * 2f);

            // ── TICK MARKS — 72 ticks, every 6th is major ─────────────────────
            for (int i = 0; i < 72; i++)
            {
                float angle = (float)(i * Math.PI * 2.0 / 72);
                bool  major = (i % 6 == 0);
                float inner = major ? radius - 16f : radius - 10f;
                float x1 = cx + (float)Math.Cos(angle) * inner;
                float y1 = cy + (float)Math.Sin(angle) * inner;
                float x2 = cx + (float)Math.Cos(angle) * radius;
                float y2 = cy + (float)Math.Sin(angle) * radius;
                using (var p = new Pen(Color.FromArgb(major ? 230 : 140, major ? brightTone : baseColor), major ? 3f : 1.2f))
                    g.DrawLine(p, x1, y1, x2, y2);
            }

            // ── PROGRESS ARC — glow layer + sharp layer, rounded caps ─────────
            float sweep = percent / 100f * 360f;
            float arcR  = radius - 14f;
            // Outer glow arc (thicker, semi-transparent)
            using (var glow = new Pen(Color.FromArgb(90, darkTone), 22f))
            {
                glow.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                glow.EndCap   = System.Drawing.Drawing2D.LineCap.Round;
                g.DrawArc(glow, cx - arcR, cy - arcR, arcR * 2f, arcR * 2f, -90f, sweep);
            }
            // Inner sharp arc (solid color)
            using (var p = new Pen(baseColor, 12f))
            {
                p.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                p.EndCap   = System.Drawing.Drawing2D.LineCap.Round;
                g.DrawArc(p, cx - arcR, cy - arcR, arcR * 2f, arcR * 2f, -90f, sweep);
            }
            // Bright highlight on arc
            using (var p = new Pen(Color.FromArgb(180, brightTone), 3f))
            {
                p.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                p.EndCap   = System.Drawing.Drawing2D.LineCap.Round;
                g.DrawArc(p, cx - arcR + 2f, cy - arcR + 2f, (arcR - 2f) * 2f, (arcR - 2f) * 2f, -90f, sweep);
            }

            // ── TOP HIGHLIGHT (~12 degrees, white, thin) ──────────────────────
            using (var p = new Pen(Color.FromArgb(210, Color.White), 3f))
                g.DrawArc(p, cx - radius, cy - radius, radius * 2f, radius * 2f, -96f, 12f);

            // ── INNER CORE — radial gradient black→very dark gray ─────────────
            float coreR = radius - 22f;
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddEllipse(cx - coreR, cy - coreR, coreR * 2f, coreR * 2f);
                using (var brush = new System.Drawing.Drawing2D.PathGradientBrush(path))
                {
                    brush.CenterPoint    = new PointF(cx, cy);
                    brush.CenterColor    = Color.FromArgb(255, 0, 0, 0);
                    brush.SurroundColors = new[] { Color.FromArgb(255, 12, 12, 14) };
                    g.FillPath(brush, path);
                }
            }

            // ── COLOR BLEED — soft radial tint inside ring (low opacity) ──────
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddEllipse(cx - coreR, cy - coreR, coreR * 2f, coreR * 2f);
                using (var brush = new System.Drawing.Drawing2D.PathGradientBrush(path))
                {
                    brush.CenterPoint    = new PointF(cx, cy);
                    brush.CenterColor    = Color.FromArgb(0, baseColor);       // transparent center
                    brush.SurroundColors = new[] { Color.FromArgb(35, baseColor) }; // color at edge
                    g.FillPath(brush, path);
                }
            }

            // ── FAINT INNER RIM HIGHLIGHT ─────────────────────────────────────
            using (var p = new Pen(Color.FromArgb(35, Color.White), 1f))
                g.DrawEllipse(p, cx - coreR, cy - coreR, coreR * 2f, coreR * 2f);

            // ── PERCENT VALUE TEXT — larger, centered, slight color glow ──────
            float fntPx = Math.Max(14f, coreR * 0.52f);
            using (var font  = new Font("Segoe UI", fntPx, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                string text = $"{(int)percent}%";
                SizeF  sz   = g.MeasureString(text, font);
                float  tx   = cx - sz.Width  / 2f;
                float  ty   = cy - sz.Height / 2f;
                // Glow pass (color, 3x3 offsets)
                using (var gb = new SolidBrush(Color.FromArgb(130, baseColor)))
                {
                    float[] offs = { -2f, 0f, 2f };
                    foreach (float ox in offs)
                    foreach (float oy in offs)
                        g.DrawString(text, font, gb, tx + ox, ty + oy);
                }
                // White core text
                using (var wb = new SolidBrush(Color.White))
                    g.DrawString(text, font, wb, tx, ty);
            }
        }
        // ── Volume controls: [–] [val] [+] ───────────────────────────────────
        // Matches: .controls { display:flex; gap:10px; }
        //          .control { width:48px; height:38px; border:2px solid currentColor; }
        private void BuildVolumeControl(int startX, int y, int btnSz, int numW, int gap,
                                        Color accentColor, int channelIndex)
        {
            // [–] button
            var btnMinus = new Button
            {
                Text      = "\u2212",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(8, 8, 12),
                Font      = new Font("Segoe UI", SF(16f), FontStyle.Bold),
                Bounds    = new Rectangle(startX, y, btnSz, btnSz),
                Cursor    = Cursors.Hand,
                TabStop   = false
            };
            btnMinus.FlatAppearance.BorderColor = accentColor;
            btnMinus.FlatAppearance.BorderSize  = Math.Max(1, (int)(2 * _scale));
            btnMinus.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, accentColor);

            // Value label (shows dB value)
            var lblVal = new Label
            {
                Text      = GetDbText(channelIndex),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 0, 0, 100),
                Font      = new Font("Segoe UI", SF(13f), FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(startX + btnSz + gap, y, numW, btnSz)
            };
            Color ac  = accentColor;
            int   bsz = Math.Max(1, (int)(2 * _scale));
            lblVal.Paint += (s, e2) =>
            {
                using (var bp = new Pen(ac, bsz))
                    e2.Graphics.DrawRectangle(bp, new Rectangle(0, 0, lblVal.Width - 1, lblVal.Height - 1));
            };

            // [+] button
            var btnPlus = new Button
            {
                Text      = "+",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(8, 8, 12),
                Font      = new Font("Segoe UI", SF(16f), FontStyle.Bold),
                Bounds    = new Rectangle(startX + btnSz + gap + numW + gap, y, btnSz, btnSz),
                Cursor    = Cursors.Hand,
                TabStop   = false
            };
            btnPlus.FlatAppearance.BorderColor = accentColor;
            btnPlus.FlatAppearance.BorderSize  = Math.Max(1, (int)(2 * _scale));
            btnPlus.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, accentColor);

            // Store dB label reference
            switch (channelIndex)
            {
                case 0: _lblDbCustomerVoice   = lblVal; break;
                case 1: _lblDbCustomerScript  = lblVal; break;
                case 2: _lblDbAgentVoice      = lblVal; break;
                case 3: _lblDbAgentScript     = lblVal; break;
            }

            int   ch = channelIndex;
            Label lv = lblVal;
            btnMinus.Click += (s, e) => { AdjustDb(ch, -1, lv); lv.Text = GetDbText(ch); };
            btnPlus.Click  += (s, e) => { AdjustDb(ch, +1, lv); lv.Text = GetDbText(ch); };

            AttachHoverGlow(btnMinus, accentColor);
            AttachHoverGlow(btnPlus,  accentColor);

            this.Controls.Add(btnMinus);
            this.Controls.Add(lblVal);
            this.Controls.Add(btnPlus);
        }

        private string GetDbText(int channelIndex)
        {
            switch (channelIndex)
            {
                case 0: return _dbCustomerVoice  + " dB";
                case 1: return _dbCustomerScript + " dB";
                case 2: return _dbAgentVoice     + " dB";
                case 3: return _dbAgentScript    + " dB";
                default: return "-3 dB";
            }
        }

        // Returns volume level (0-100) for the given meter key — shown as % in center
        private int GetVolumePct(string key)
        {
            switch (key)
            {
                case "customerVoice":    return DbToPercent(_dbCustomerVoice);
                case "agentScript_left": return DbToPercent(_dbCustomerScript);
                case "myMicLevel":       return DbToPercent(_dbAgentVoice);
                case "agentScript":      return DbToPercent(_dbAgentScript);
                default: return 0;
            }
        }

        private void AdjustDb(int channelIndex, int delta, Label lblVal)
        {
            switch (channelIndex)
            {
                case 0:
                    _dbCustomerVoice = Math.Max(-20, Math.Min(6, _dbCustomerVoice + delta));
                    _customerVoiceVolume = DbToLinear(_dbCustomerVoice);
                    AppSettings.Instance.SpeakerSystemVolume = DbToPercent(_dbCustomerVoice);
                    break;
                case 1:
                    _dbCustomerScript = Math.Max(-20, Math.Min(6, _dbCustomerScript + delta));
                    AppSettings.Instance.SetVolume("agentScript", DbToLinear(_dbCustomerScript));
                    LocalBridgeServer.Instance.SetVolume("agent", DbToPercent(_dbCustomerScript));
                    break;
                case 2:
                    _dbAgentVoice = Math.Max(-20, Math.Min(6, _dbAgentVoice + delta));
                    if (_activeMicDevice != null)
                        try { _activeMicDevice.AudioEndpointVolume.MasterVolumeLevelScalar = DbToLinear(_dbAgentVoice); } catch { }
                    AppSettings.Instance.MicSystemVolume = DbToPercent(_dbAgentVoice);
                    break;
                case 3:
                    _dbAgentScript = Math.Max(-20, Math.Min(6, _dbAgentScript + delta));
                    AppSettings.Instance.SetVolume("customerScript", DbToLinear(_dbAgentScript));
                    LocalBridgeServer.Instance.SetVolume("customer", DbToPercent(_dbAgentScript));
                    break;
            }
            AppSettings.Instance.Save();
        }

        private static float DbToLinear(int db)  => (float)Math.Pow(10.0, db / 20.0);
        private static int   DbToPercent(int db) => (int)(DbToLinear(db) * 100f);

        private void AttachHoverGlow(Button btn, Color accent)
        {
            // Persistent glow border paint
            btn.Paint += (s, e2) =>
            {
                var g2 = e2.Graphics;
                g2.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                // Outer glow (2 passes)
                for (int gi = 0; gi < 2; gi++)
                {
                    int   ga = gi == 0 ? 18 : 45;
                    float gw = gi == 0 ? 4f : 2f;
                    using (var gp = new Pen(Color.FromArgb(ga, accent), gw))
                        g2.DrawRectangle(gp, new Rectangle(0, 0, btn.Width - 1, btn.Height - 1));
                }
            };
            btn.MouseEnter += (s, e) =>
            {
                btn.FlatAppearance.BorderColor = Color.FromArgb(255, accent);
                btn.BackColor = Color.FromArgb(45, accent);
                btn.Invalidate();
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.FlatAppearance.BorderColor = accent;
                btn.BackColor = Color.FromArgb(8, 8, 12);
                btn.Invalidate();
            };
        }

        // ── Device buttons ────────────────────────────────────────────────────
        // Matches: .btn { padding:14px 40px; border:2px solid currentColor; filter:drop-shadow(0 0 6px currentColor); }
        private void BuildDeviceButtons(int W, int H, int cardPad)
        {
            int footerH = (int)(44 * _scale);
            int btnH    = (int)(54 * _scale);
            int btnW    = (int)(Math.Min(W * 0.30f, 380 * _scale));
            int gap     = (int)(40 * _scale);
            int totalBW = btnW * 2 + gap;
            int btnX0   = (W - totalBW) / 2;
            int btnY    = H - footerH - (int)(28 * _scale) - btnH;

            // SELECT MICROPHONE — red border + glow
            var btnMic = new Button
            {
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(6, 6, 10),
                Font      = new Font("Segoe UI", SF(13f), FontStyle.Bold),
                Bounds    = new Rectangle(btnX0, btnY, btnW, btnH),
                Cursor    = Cursors.Hand,
                TabStop   = false,
                Text      = "\uD83C\uDF99  SELECT MICROPHONE"
            };
            btnMic.FlatAppearance.BorderColor = METER_RED;
            btnMic.FlatAppearance.BorderSize  = Math.Max(1, (int)(2 * _scale));
            btnMic.FlatAppearance.MouseOverBackColor = Color.FromArgb(35, METER_RED);
            AttachDeviceButtonHover(btnMic, METER_RED);
            btnMic.Click += (s, e) => ShowMicDropdown(btnMic);
            this.Controls.Add(btnMic);

            // SELECT SPEAKER — blue border + glow
            var btnSpk = new Button
            {
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(6, 6, 10),
                Font      = new Font("Segoe UI", SF(13f), FontStyle.Bold),
                Bounds    = new Rectangle(btnX0 + btnW + gap, btnY, btnW, btnH),
                Cursor    = Cursors.Hand,
                TabStop   = false,
                Text      = "\uD83D\uDD0A  SELECT SPEAKER"
            };
            btnSpk.FlatAppearance.BorderColor = METER_BLUE;
            btnSpk.FlatAppearance.BorderSize  = Math.Max(1, (int)(2 * _scale));
            btnSpk.FlatAppearance.MouseOverBackColor = Color.FromArgb(35, METER_BLUE);
            AttachDeviceButtonHover(btnSpk, METER_BLUE);
            btnSpk.Click += (s, e) => ShowSpeakerDropdown(btnSpk);
            this.Controls.Add(btnSpk);

            // Hidden combos for device selection logic
            _cboMic     = new ComboBox { Visible = false, Location = new Point(-500, -500), Width = 1 };
            _cboHeadset = new ComboBox { Visible = false, Location = new Point(-500, -500), Width = 1 };
            this.Controls.Add(_cboMic);
            this.Controls.Add(_cboHeadset);

            _cboMic.SelectedIndexChanged += (s, e) =>
            {
                if (_cboMic.SelectedIndex >= 0)
                    btnMic.Text = "\uD83C\uDF99  " + TruncateDevice(_cboMic.Text, 24);
            };
            _cboHeadset.SelectedIndexChanged += (s, e) =>
            {
                if (_cboHeadset.SelectedIndex >= 0)
                    btnSpk.Text = "\uD83D\uDD0A  " + TruncateDevice(_cboHeadset.Text, 24);
            };
        }

        private string TruncateDevice(string name, int maxLen)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return name.Length > maxLen ? name.Substring(0, maxLen) + "\u2026" : name;
        }

        private void AttachDeviceButtonHover(Button btn, Color accent)
        {
            // Persistent outer glow on device buttons
            btn.Paint += (s, e2) =>
            {
                var g2 = e2.Graphics;
                g2.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                for (int gi = 0; gi < 3; gi++)
                {
                    int   ga = new int[]{ 10, 22, 45 }[gi];
                    float gw = new float[]{ 6f, 3f, 1.5f }[gi];
                    using (var gp = new Pen(Color.FromArgb(ga, accent), gw))
                        g2.DrawRectangle(gp, new Rectangle(0, 0, btn.Width - 1, btn.Height - 1));
                }
            };
            btn.MouseEnter += (s, e) =>
            {
                btn.FlatAppearance.BorderColor = Color.FromArgb(255, accent);
                btn.BackColor = Color.FromArgb(30, accent);
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.FlatAppearance.BorderColor = accent;
                btn.BackColor = Color.FromArgb(6, 6, 10);
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
                var mi = new ToolStripMenuItem(it) { ForeColor = Color.White, BackColor = Color.FromArgb(22, 22, 38) };
                mi.Click += (s, e) => { int idx = _cboMic.Items.IndexOf(it); if (idx >= 0) _cboMic.SelectedIndex = idx; };
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
                var mi = new ToolStripMenuItem(it) { ForeColor = Color.White, BackColor = Color.FromArgb(22, 22, 38) };
                mi.Click += (s, e) => { int idx = _cboHeadset.Items.IndexOf(it); if (idx >= 0) _cboHeadset.SelectedIndex = idx; };
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
                Text      = "One United Global LLC 2026  V 7.37",
                ForeColor = Color.FromArgb(100, 100, 110),
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
                case "myMicLevel":       return _micLevel;
                case "customerVoice":    return _customerVoiceLevel;
                case "agentScript":      return _agentScriptLevel;
                case "agentScript_left": return _customerScriptLevel;
                default: return 0f;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // BACKEND — all unchanged from original
        // ══════════════════════════════════════════════════════════════════════

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
                string name = AppSettings.Instance.AgentName;
                if (string.IsNullOrEmpty(name))
                    name = HeartbeatService.Instance.AgentName;
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
            int micPct   = Math.Max(1, Math.Min(200, s.MicSystemVolume));
            int spkPct   = Math.Max(1, Math.Min(200, s.SpeakerSystemVolume));
            int agentPct = Math.Max(1, (int)(s.GetVolume("agentScript",    0.48f) * 100));
            int custPct  = Math.Max(1, (int)(s.GetVolume("customerScript", 0.55f) * 100));
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

    /// <summary>
    /// Double-buffered panel — eliminates flicker on transparent/custom-painted controls.
    /// </summary>
    internal sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint  |
                ControlStyles.UserPaint,
                true);
            UpdateStyles();
        }
    }
}
