/*
 * MainFormV5.cs  —  ONE Voice Solution v7.31
 *
 * UI REDESIGN v7.31 — COMPLETE VISUAL REWRITE to match render_new_design.py exactly.
 *   - Dark space background (BG = 4,5,14)
 *   - ONE logo top-left, "The Geniusness Is In The Simplicity" centered with red glow/outline
 *   - Full-width red glow line with white center flare
 *   - Section labels: "WHAT THE AGENT HEARS" / "WHAT THE CUSTOMER HEARS"
 *     (AGENT in red, CUSTOMER in purple)
 *   - 4 circular VU meters drawn entirely via owner-draw:
 *       Layer 1: Wide color bloom outward
 *       Layer 2: Dark base disc + thick colored outer ring with bloom glow
 *       Layer 3: Chrome highlight arcs (blurred) on left/right
 *       Layer 4: LED dot ring (36 dots, lit by level)
 *       Layer 5: Inner bezel ring (0.82r) with color glow, dark fill, colored outline
 *       Layer 6: Tick marks on inner bezel (60 ticks, white)
 *       Layer 7: Deep recessed center (concentric dark circles + color tint)
 *       Layer 8: % value centered (white core + color glow passes)
 *   - Labels below each meter
 *   - [–] [val] [+] volume controls (large square buttons, accent-colored borders)
 *   - SELECT MICROPHONE / SELECT SPEAKER buttons (large, glowing)
 *   - Footer: "One United Global LLC 2026  V 7.31"
 *   - ALL audio/backend logic unchanged.
 *
 * v7.30 changes (audio):
 *   - Mic pass-through restarts automatically after each recording finishes.
 *   - WaveFormat upgraded to 48000/16/stereo.
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
        private static readonly Color ONE_RED       = Color.FromArgb(255, 59, 59);
        private static readonly Color BG_DARK       = Color.FromArgb(4, 5, 14);
        private static readonly Color TEXT_WHITE     = Color.White;
        private static readonly Color ONE_BLUE_SEL   = Color.FromArgb(0, 102, 204);

        // Meter colours — exact match to Python render
        private static readonly Color METER_RED     = Color.FromArgb(255,  59,  59);
        private static readonly Color METER_BLUE    = Color.FromArgb( 59, 163, 255);
        private static readonly Color METER_PURPLE  = Color.FromArgb(168,  85, 247);
        private static readonly Color METER_GREEN   = Color.FromArgb( 34, 197,  94);

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
        private float _volCustomerVoice      = 1.0f;
        private float _volCustomerScript     = 1.0f;
        private float _volAgentVoice         = 1.0f;
        private float _volAgentScript        = 1.0f;

        // dB display values
        private int _dbCustomerVoice     = -3;
        private int _dbCustomerScript    = -3;
        private int _dbAgentVoice        = -3;
        private int _dbAgentScript       = -3;

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

        // ══════════════════════════════════════════════════════════════════════
        // FORM PAINT — background, glow line, vignette
        // ══════════════════════════════════════════════════════════════════════
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int W = this.ClientSize.Width;
            int H = this.ClientSize.Height;

            // ── Background: deep space black-blue ─────────────────────────────
            using (var bgBrush = new SolidBrush(Color.FromArgb(4, 5, 14)))
                g.FillRectangle(bgBrush, 0, 0, W, H);

            // ── Subtle vignette (dark edges, lighter center) ──────────────────
            int[] vigR = { W, (int)(W * 0.85f), (int)(W * 0.65f), (int)(W * 0.45f) };
            int[] vigA = { 0, 20, 50, 90 };
            for (int v = vigR.Length - 1; v >= 0; v--)
            {
                using (var vp = new GraphicsPath())
                {
                    vp.AddEllipse(W / 2 - vigR[v], H / 2 - vigR[v], vigR[v] * 2, vigR[v] * 2);
                    using (var vpb = new PathGradientBrush(vp))
                    {
                        vpb.CenterColor    = Color.Transparent;
                        vpb.SurroundColors = new[] { Color.FromArgb(vigA[v], 0, 0, 0) };
                        g.FillPath(vpb, vp);
                    }
                }
            }

            // ── Glow line: full-width red line with white center flare ─────────
            int headerH = (int)(90 * _scale);
            int cardPad = (int)(18 * _scale);
            int glareY  = cardPad + headerH + (int)(16 * _scale);

            // Outer red ambient glow passes
            int[] glowWidths = { (int)(120 * _scale), (int)(70 * _scale), (int)(40 * _scale), (int)(20 * _scale) };
            int[] glowAlphas = { 6, 14, 28, 55 };
            for (int gp2 = 0; gp2 < glowWidths.Length; gp2++)
            {
                using (var gpen = new Pen(Color.FromArgb(glowAlphas[gp2], ONE_RED), glowWidths[gp2]))
                    g.DrawLine(gpen, 0, glareY, W, glareY);
            }

            // Solid red line full width
            using (var redLine = new Pen(Color.FromArgb(230, ONE_RED), Math.Max(1, (int)(2 * _scale))))
                g.DrawLine(redLine, 0, glareY, W, glareY);

            // White center flare — horizontal gradient hotspot
            int flareHalf = (int)(W * 0.15f);
            int flareCX   = W / 2;
            int flareH2   = Math.Max(4, (int)(4 * _scale));
            for (int px = flareCX - flareHalf; px <= flareCX + flareHalf; px++)
            {
                float t  = 1.0f - (float)Math.Abs(px - flareCX) / flareHalf;
                float t2 = (float)Math.Pow(t, 1.5);
                int   r2 = 255;
                int   g2 = Math.Min(255, (int)(59 + t2 * (255 - 59)));
                int   b2 = Math.Min(255, (int)(59 + t2 * (255 - 59)));
                int   a2 = (int)(80 + t2 * 175);
                using (var fp = new Pen(Color.FromArgb(a2, r2, g2, b2), flareH2))
                    g.DrawLine(fp, px, glareY - flareH2 / 2, px, glareY + flareH2 / 2);
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

        // ── Header: logo left + tagline centered ──────────────────────────────
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

            // Tagline — drawn as owner-draw panel so we can do red glow + outline + white fill
            string tagText = "The Geniusness Is In The Simplicity";
            var tagFont    = new Font("Segoe UI", SF(21f), FontStyle.Bold);
            var tagSz      = TextRenderer.MeasureText(tagText, tagFont);
            int tagX       = (W - tagSz.Width) / 2;
            int tagY       = cardPad + (headerH - tagSz.Height) / 2;

            var tagPanel = new Panel
            {
                Bounds    = new Rectangle(tagX - (int)(20 * _scale), tagY - (int)(8 * _scale),
                                          tagSz.Width + (int)(40 * _scale), tagSz.Height + (int)(16 * _scale)),
                BackColor = Color.Transparent
            };
            string tt = tagText;
            Font   tf = tagFont;
            tagPanel.Paint += (s, e2) =>
            {
                var g = e2.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int pw = tagPanel.Width;
                int ph = tagPanel.Height;
                int tx = (pw - tagSz.Width) / 2;
                int ty = (ph - tagSz.Height) / 2;

                // Red glow passes
                int[] glowOff = { (int)(30 * _scale), (int)(16 * _scale), (int)(7 * _scale) };
                int[] glowAlp = { 90, 140, 180 };
                for (int gp = 0; gp < 3; gp++)
                {
                    for (int ox = -glowOff[gp]; ox <= glowOff[gp]; ox += Math.Max(1, glowOff[gp] / 3))
                    for (int oy = -glowOff[gp]; oy <= glowOff[gp]; oy += Math.Max(1, glowOff[gp] / 3))
                    {
                        TextRenderer.DrawText(g, tt, tf,
                            new Point(tx + ox, ty + oy),
                            Color.FromArgb(glowAlp[gp] / 9, ONE_RED));
                    }
                }
                // Red outline (8 directions, 2px offset)
                int[] outOff = { -2, 0, 2 };
                foreach (int ox in outOff)
                foreach (int oy in outOff)
                {
                    if (ox == 0 && oy == 0) continue;
                    TextRenderer.DrawText(g, tt, tf, new Point(tx + ox, ty + oy), ONE_RED);
                }
                // White fill
                TextRenderer.DrawText(g, tt, tf, new Point(tx, ty), Color.White);
            };
            this.Controls.Add(tagPanel);
            AttachDrag(tagPanel);

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
        private void BuildMeterSection(int W, int H, int cardPad)
        {
            int headerH    = (int)(90 * _scale);
            int glareH     = (int)(16 * _scale);
            int sectionTop = cardPad + headerH + glareH + (int)(26 * _scale);

            // Layout constants matching render
            int innerPad     = cardPad + (int)(24 * _scale);
            int usableW      = W - innerPad * 2;
            int slotW        = usableW / 4;
            int meterDiam    = (int)(slotW * 0.88f);
            meterDiam = Math.Max(120, Math.Min((int)(200 * _scale), meterDiam));

            int sectionLblH  = (int)(28 * _scale);
            int meterTop     = sectionTop + sectionLblH + (int)(14 * _scale);

            // Section labels
            // Left: "WHAT THE AGENT HEARS" — AGENT in red
            BuildSectionLabel(innerPad, sectionTop, slotW * 2,
                              "WHAT THE ", "AGENT", " HEARS",
                              Color.FromArgb(180, 180, 205), METER_RED);
            // Right: "WHAT THE CUSTOMER HEARS" — CUSTOMER in purple
            BuildSectionLabel(innerPad + slotW * 2, sectionTop, slotW * 2,
                              "WHAT THE ", "CUSTOMER", " HEARS",
                              Color.FromArgb(180, 180, 205), METER_PURPLE);

            // Meter definitions matching render order:
            // 0: RED   = CUSTOMER VOICE
            // 1: BLUE  = AGENT RECORDINGS
            // 2: PURPLE= AGENT VOICE
            // 3: GREEN = AGENT RECORDINGS
            string[] labels = { "CUSTOMER VOICE", "AGENT RECORDINGS", "AGENT VOICE", "AGENT RECORDINGS" };
            Color[]  colors = { METER_RED, METER_BLUE, METER_PURPLE, METER_GREEN };
            string[] keys   = { "customerVoice", "agentScript_left", "myMicLevel", "agentScript" };

            int lblH    = (int)(24 * _scale);
            int btnSzV  = (int)(50 * _scale);   // large square buttons matching render
            int numWV   = (int)(60 * _scale);
            int gapV    = (int)(6 * _scale);

            for (int i = 0; i < 4; i++)
            {
                int slotCX = innerPad + i * slotW + slotW / 2;
                int mx     = slotCX - meterDiam / 2;
                int my     = meterTop;

                // Meter panel
                BuildCircularMeter(mx, my, meterDiam, colors[i], keys[i]);

                // Label below meter
                int labelY = my + meterDiam + (int)(18 * _scale);
                var lbl = new Label
                {
                    Text      = labels[i],
                    ForeColor = Color.FromArgb(200, 200, 210),
                    BackColor = Color.Transparent,
                    Font      = new Font("Segoe UI", SF(11f), FontStyle.Bold),
                    AutoSize  = false,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Bounds    = new Rectangle(slotCX - slotW / 2, labelY, slotW, lblH)
                };
                this.Controls.Add(lbl);

                // [–] [val] [+] volume controls — large square buttons
                int ctrlY   = labelY + lblH + (int)(12 * _scale);
                int totalVW = btnSzV + gapV + numWV + gapV + btnSzV;
                int startVX = slotCX - totalVW / 2;

                BuildVolumeControl(startVX, ctrlY, btnSzV, numWV, gapV, colors[i], i);
            }
        }

        // ── Section label with colored keyword ────────────────────────────────
        private void BuildSectionLabel(int x, int y, int w,
                                       string prefix, string keyword, string suffix,
                                       Color baseColor, Color keyColor)
        {
            var font    = new Font("Segoe UI", SF(11f), FontStyle.Bold);
            int prefW   = TextRenderer.MeasureText(prefix,  font).Width;
            int keyW    = TextRenderer.MeasureText(keyword, font).Width;
            int sufW    = TextRenderer.MeasureText(suffix,  font).Width;
            int totalTW = prefW + keyW + sufW;
            int textH   = TextRenderer.MeasureText(keyword, font).Height;

            var panel = new Panel
            {
                Bounds    = new Rectangle(x, y, w, textH + (int)(8 * _scale)),
                BackColor = Color.Transparent
            };
            string pref = prefix, kw = keyword, suf = suffix;
            Color  bc   = baseColor, kc = keyColor;
            panel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int pw = panel.Width;
                int ph = panel.Height;
                int textY  = (ph - textH) / 2;
                int startX = (pw - totalTW) / 2;
                int lineY  = ph / 2;

                // Decorative lines
                using (var pen = new Pen(Color.FromArgb(80, 150, 150, 170), 1f))
                {
                    g.DrawLine(pen, 0, lineY, startX - (int)(8 * _scale), lineY);
                    g.DrawLine(pen, startX + totalTW + (int)(8 * _scale), lineY, pw, lineY);
                }
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
                case "myMicLevel":       _myMicMeterLeft      = panel; break;
                case "customerVoice":    _customerVoiceMeter  = panel; break;
                case "agentScript":      _agentScriptMeter    = panel; break;
                case "agentScript_left": _customerScriptMeter = panel; break;
            }
            return panel;
        }

        // ══════════════════════════════════════════════════════════════════════
        // DRAW DIAL METER — exact translation of render_new_design.py draw_meter()
        // ══════════════════════════════════════════════════════════════════════
        private void DrawDialMeter(Graphics g, Panel panel, Color col, string key)
        {
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            int W  = panel.Width;
            int H  = panel.Height;
            int cx = W / 2;
            int cy = H / 2;
            int R  = Math.Min(W, H) / 2 - 2;   // METER_R

            float level = GetLevel(key);
            int   pct   = (int)Math.Round(level * 100f);

            // ── LAYER 1: Wide color bloom bleeding outward ────────────────────
            // Simulated with multiple PathGradientBrush ellipses at increasing radii
            int[] bloomR = { (int)(R * 1.7f), (int)(R * 1.4f), (int)(R * 1.15f), (int)(R * 0.9f), (int)(R * 0.65f), (int)(R * 0.45f) };
            int[] bloomA = { 5, 10, 18, 35, 60, 85 };
            for (int b = 0; b < bloomR.Length; b++)
            {
                int br = bloomR[b];
                if (cx - br < -br || cy - br < -br) continue; // skip if outside panel
                using (var bp = new GraphicsPath())
                {
                    bp.AddEllipse(cx - br, cy - br, br * 2, br * 2);
                    using (var pgb = new PathGradientBrush(bp))
                    {
                        pgb.CenterPoint    = new PointF(cx, cy);
                        pgb.CenterColor    = Color.FromArgb(bloomA[b], col);
                        pgb.SurroundColors = new[] { Color.FromArgb(0, col) };
                        g.FillPath(pgb, bp);
                    }
                }
            }

            // ── LAYER 2: Outer dark base disc ─────────────────────────────────
            using (var baseBrush = new SolidBrush(Color.FromArgb(18, 18, 22)))
                g.FillEllipse(baseBrush, cx - R, cy - R, R * 2, R * 2);

            // ── LAYER 2b: Thick colored outer ring with bloom glow ─────────────
            int outerRingW = Math.Max((int)(18 * _scale), 10);
            // Glow layers behind the ring
            int[] ringGlowW = { outerRingW + (int)(20 * _scale), outerRingW + (int)(10 * _scale), outerRingW };
            int[] ringGlowA = { 40, 80, 160 };
            for (int rg = 0; rg < 3; rg++)
            {
                using (var rpen = new Pen(Color.FromArgb(ringGlowA[rg], col), ringGlowW[rg]))
                    g.DrawEllipse(rpen, cx - R, cy - R, R * 2, R * 2);
            }
            // Solid colored ring on top
            using (var solidRing = new Pen(Color.FromArgb(255, col), outerRingW))
                g.DrawEllipse(solidRing, cx - R, cy - R, R * 2, R * 2);

            // ── LAYER 3: Chrome highlight arcs (left + right) ─────────────────
            int chromeW = outerRingW + (int)(2 * _scale);
            // Left arc: 110° to 250° (sweep 140°)
            using (var cp1 = new Pen(Color.FromArgb(180, 240, 240, 245), chromeW))
                g.DrawArc(cp1, cx - R, cy - R, R * 2, R * 2, 110f, 140f);
            // Right arc: 290° to 70° (sweep 140°)
            using (var cp2 = new Pen(Color.FromArgb(180, 240, 240, 245), chromeW))
                g.DrawArc(cp2, cx - R, cy - R, R * 2, R * 2, 290f, 140f);
            // Thin bright chrome line on top
            using (var ct = new Pen(Color.FromArgb(220, 255, 255, 255), Math.Max(2, (int)(3 * _scale))))
            {
                g.DrawArc(ct, cx - R, cy - R, R * 2, R * 2, 115f, 130f);
                g.DrawArc(ct, cx - R, cy - R, R * 2, R * 2, 295f, 130f);
            }

            // ── LAYER 4: LED dot ring ──────────────────────────────────────────
            int   ledR    = R - (int)(9 * _scale);
            int   dotSize = Math.Max(2, (int)(3 * _scale));
            int   numDots = 36;
            float litFrac = level;  // fraction of dots lit = level
            for (int d = 0; d < numDots; d++)
            {
                float dotAngle = (float)(d * (360.0 / numDots) - 90.0);
                float dotRad   = (float)(dotAngle * Math.PI / 180.0);
                float dx       = cx + ledR * (float)Math.Cos(dotRad);
                float dy       = cy + ledR * (float)Math.Sin(dotRad);
                float dotFrac  = (float)d / numDots;
                bool  isLit    = dotFrac <= litFrac;
                int   dotAlpha = isLit ? 255 : 60;
                Color dotCol   = isLit ? Color.FromArgb(dotAlpha, col) : Color.FromArgb(dotAlpha, 80, 80, 85);

                if (isLit)
                {
                    // Dot glow
                    using (var dgp = new GraphicsPath())
                    {
                        dgp.AddEllipse(dx - dotSize * 3, dy - dotSize * 3, dotSize * 6, dotSize * 6);
                        using (var dgb = new PathGradientBrush(dgp))
                        {
                            dgb.CenterPoint    = new PointF(dx, dy);
                            dgb.CenterColor    = Color.FromArgb(120, col);
                            dgb.SurroundColors = new[] { Color.FromArgb(0, col) };
                            g.FillPath(dgb, dgp);
                        }
                    }
                }
                using (var dotBrush = new SolidBrush(dotCol))
                    g.FillEllipse(dotBrush, dx - dotSize, dy - dotSize, dotSize * 2, dotSize * 2);
            }

            // ── LAYER 5: Inner bezel ring with color glow ─────────────────────
            int innerBezelR = (int)(R * 0.82f);
            // Glow passes
            int[] ibGlowW = { (int)(16 * _scale), (int)(8 * _scale), (int)(3 * _scale) };
            int[] ibGlowA = { 60, 120, 200 };
            for (int ib = 0; ib < 3; ib++)
            {
                using (var ibp = new Pen(Color.FromArgb(ibGlowA[ib], col), ibGlowW[ib]))
                    g.DrawEllipse(ibp, cx - innerBezelR, cy - innerBezelR, innerBezelR * 2, innerBezelR * 2);
            }
            // Dark fill inside inner bezel
            using (var ibFill = new SolidBrush(Color.FromArgb(12, 12, 16)))
                g.FillEllipse(ibFill, cx - innerBezelR, cy - innerBezelR, innerBezelR * 2, innerBezelR * 2);
            // Colored ring outline
            using (var ibRing = new Pen(Color.FromArgb(180, col), Math.Max(2, (int)(3 * _scale))))
                g.DrawEllipse(ibRing, cx - innerBezelR, cy - innerBezelR, innerBezelR * 2, innerBezelR * 2);

            // ── LAYER 6: Tick marks on inner bezel ────────────────────────────
            int tickOuterR = innerBezelR - (int)(2 * _scale);
            int tickInnerR = (int)(innerBezelR * 0.88f);
            for (int t = 0; t < 60; t++)
            {
                float angle   = (float)(t * 6.0 - 90.0);
                float rad     = (float)(angle * Math.PI / 180.0);
                bool  isMajor = (t % 5 == 0);
                float tickW   = isMajor ? Math.Max(2, (int)(2 * _scale)) : Math.Max(1, (int)(1 * _scale));
                int   tickA   = isMajor ? 200 : 100;
                float x1 = cx + tickOuterR * (float)Math.Cos(rad);
                float y1 = cy + tickOuterR * (float)Math.Sin(rad);
                float x2 = cx + tickInnerR * (float)Math.Cos(rad);
                float y2 = cy + tickInnerR * (float)Math.Sin(rad);
                using (var tp = new Pen(Color.FromArgb(tickA, 255, 255, 255), tickW))
                    g.DrawLine(tp, x1, y1, x2, y2);
            }

            // ── LAYER 7: Deep recessed center disc ────────────────────────────
            int innerR = (int)(R * 0.68f);
            // Concentric dark circles — deep recession effect
            int[] discR = { innerR, (int)(innerR * 0.85f), (int)(innerR * 0.65f), (int)(innerR * 0.40f), (int)(innerR * 0.20f) };
            Color[] discC = {
                Color.FromArgb(6, 6, 10),
                Color.FromArgb(4, 4, 8),
                Color.FromArgb(2, 2, 5),
                Color.FromArgb(1, 1, 3),
                Color.FromArgb(0, 0, 0)
            };
            for (int dc = 0; dc < discR.Length; dc++)
            {
                using (var db = new SolidBrush(discC[dc]))
                    g.FillEllipse(db, cx - discR[dc], cy - discR[dc], discR[dc] * 2, discR[dc] * 2);
            }

            // Color tint — multiple concentric passes with blur simulation (PathGradientBrush)
            int[] tintR = { (int)(innerR * 0.95f), (int)(innerR * 0.80f), (int)(innerR * 0.60f), (int)(innerR * 0.40f), (int)(innerR * 0.20f) };
            int[] tintA = { 30, 60, 90, 110, 120 };
            for (int ti = 0; ti < tintR.Length; ti++)
            {
                using (var tp2 = new GraphicsPath())
                {
                    tp2.AddEllipse(cx - tintR[ti], cy - tintR[ti], tintR[ti] * 2, tintR[ti] * 2);
                    using (var pgb = new PathGradientBrush(tp2))
                    {
                        pgb.CenterPoint    = new PointF(cx, cy);
                        pgb.CenterColor    = Color.FromArgb(tintA[ti], col);
                        pgb.SurroundColors = new[] { Color.FromArgb(0, col) };
                        g.FillPath(pgb, tp2);
                    }
                }
            }

            // Inset shadow edge
            int[] insetW = { (int)(6 * _scale), (int)(14 * _scale), (int)(28 * _scale) };
            int[] insetA = { 200, 130, 70 };
            for (int ii = 0; ii < 3; ii++)
            {
                using (var isp = new Pen(Color.FromArgb(insetA[ii], 0, 0, 0), insetW[ii]))
                    g.DrawEllipse(isp, cx - innerR, cy - innerR, innerR * 2, innerR * 2);
            }

            // ── LAYER 8: % value — white core with color glow ─────────────────
            string valStr  = pct.ToString() + "%";
            float  valSize = SF(28f) * ((float)innerR / 60f);
            valSize = Math.Max(SF(14f), Math.Min(SF(36f), valSize));
            using (var valFont = new Font("Segoe UI", valSize, FontStyle.Bold))
            {
                var valSz = TextRenderer.MeasureText(valStr, valFont);
                int vx    = cx - valSz.Width / 2;
                int vy    = cy - valSz.Height / 2;

                // Outer color glow passes (wide, soft)
                int[] vgBlur = { (int)(22 * _scale), (int)(14 * _scale), (int)(7 * _scale) };
                int[] vgAlph = { 160, 200, 230 };
                for (int vg = 0; vg < 3; vg++)
                {
                    int off = vgBlur[vg] / 4 + 1;
                    for (int ox = -off; ox <= off; ox += Math.Max(1, off / 2))
                    for (int oy = -off; oy <= off; oy += Math.Max(1, off / 2))
                    {
                        TextRenderer.DrawText(g, valStr, valFont,
                            new Point(vx + ox, vy + oy),
                            Color.FromArgb(vgAlph[vg] / (off * 2 + 1), col));
                    }
                }
                // White core
                TextRenderer.DrawText(g, valStr, valFont, new Point(vx, vy), Color.White);
            }
        }

        // ── Volume control row: [–] [val] [+] ────────────────────────────────
        private void BuildVolumeControl(int startX, int y, int btnSz, int numW, int gap,
                                        Color accentColor, int channelIndex)
        {
            // [–] button
            var btnMinus = new Button
            {
                Text      = "–",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 0, 0, 102),
                Font      = new Font("Segoe UI", SF(18f), FontStyle.Bold),
                Bounds    = new Rectangle(startX, y, btnSz, btnSz),
                Cursor    = Cursors.Hand,
                TabStop   = false
            };
            btnMinus.FlatAppearance.BorderColor = accentColor;
            btnMinus.FlatAppearance.BorderSize  = Math.Max(1, (int)(2 * _scale));
            btnMinus.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, accentColor);
            StyleVolumeButton(btnMinus, accentColor);

            // Value label
            var lblVal = new Label
            {
                Text      = GetDbText(channelIndex),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 0, 0, 102),
                Font      = new Font("Segoe UI", SF(14f), FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(startX + btnSz + gap, y, numW, btnSz)
            };
            // Draw border on label via Paint
            Color ac = accentColor;
            int   bsz = Math.Max(1, (int)(2 * _scale));
            lblVal.Paint += (s, e2) =>
            {
                using (var bp = new Pen(ac, bsz))
                {
                    var r = new Rectangle(0, 0, lblVal.Width - 1, lblVal.Height - 1);
                    e2.Graphics.DrawRectangle(bp, r);
                }
            };

            // [+] button
            var btnPlus = new Button
            {
                Text      = "+",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 0, 0, 102),
                Font      = new Font("Segoe UI", SF(18f), FontStyle.Bold),
                Bounds    = new Rectangle(startX + btnSz + gap + numW + gap, y, btnSz, btnSz),
                Cursor    = Cursors.Hand,
                TabStop   = false
            };
            btnPlus.FlatAppearance.BorderColor = accentColor;
            btnPlus.FlatAppearance.BorderSize  = Math.Max(1, (int)(2 * _scale));
            btnPlus.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, accentColor);
            StyleVolumeButton(btnPlus, accentColor);

            // Store dB label reference
            switch (channelIndex)
            {
                case 0: _lblDbCustomerVoice   = lblVal; break;
                case 1: _lblDbCustomerScript  = lblVal; break;
                case 2: _lblDbAgentVoice      = lblVal; break;
                case 3: _lblDbAgentScript     = lblVal; break;
            }

            int ch = channelIndex;
            Label lv = lblVal;
            btnMinus.Click += (s, e) => AdjustDb(ch, -1, lv);
            btnPlus.Click  += (s, e) => AdjustDb(ch, +1, lv);

            AttachHoverGlow(btnMinus, accentColor);
            AttachHoverGlow(btnPlus,  accentColor);

            this.Controls.Add(btnMinus);
            this.Controls.Add(lblVal);
            this.Controls.Add(btnPlus);
        }

        private void StyleVolumeButton(Button btn, Color accent)
        {
            // Rounded corners via Paint
            btn.Paint += (s, e2) =>
            {
                var g = e2.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int radius = (int)(4 * _scale);
                var rect   = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using (var path = RoundedRect(rect, radius))
                {
                    using (var fill = new SolidBrush(btn.BackColor))
                        g.FillPath(fill, path);
                    using (var border = new Pen(btn.FlatAppearance.BorderColor, btn.FlatAppearance.BorderSize))
                        g.DrawPath(border, path);
                }
                // Draw text centered
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                using (var tf = new SolidBrush(btn.ForeColor))
                    g.DrawString(btn.Text, btn.Font, tf, new RectangleF(0, 0, btn.Width, btn.Height), sf);
            };
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

        private void AttachHoverGlow(Button btn, Color accent)
        {
            btn.MouseEnter += (s, e) =>
            {
                btn.FlatAppearance.BorderColor = Color.FromArgb(255, accent);
                btn.BackColor = Color.FromArgb(45, accent);
                btn.Invalidate();
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.FlatAppearance.BorderColor = accent;
                btn.BackColor = Color.FromArgb(0, 0, 0, 102);
                btn.Invalidate();
            };
        }

        // ── Device buttons ────────────────────────────────────────────────────
        private void BuildDeviceButtons(int W, int H, int cardPad)
        {
            int footerH  = (int)(44 * _scale);
            int btnH     = (int)(58 * _scale);
            int btnW     = (int)(Math.Min(W * 0.30f, 390 * _scale));
            int gap      = (int)(36 * _scale);
            int totalBW  = btnW * 2 + gap;
            int btnX0    = (W - totalBW) / 2;
            int btnY     = H - footerH - (int)(30 * _scale) - btnH;

            // SELECT MICROPHONE — red glow
            var btnMic = new Button
            {
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 0, 0, 114),
                Font      = new Font("Segoe UI", SF(14f), FontStyle.Bold),
                Bounds    = new Rectangle(btnX0, btnY, btnW, btnH),
                Cursor    = Cursors.Hand,
                TabStop   = false,
                Text      = "  \uD83C\uDF99  SELECT MICROPHONE"
            };
            btnMic.FlatAppearance.BorderColor = METER_RED;
            btnMic.FlatAppearance.BorderSize  = Math.Max(1, (int)(2 * _scale));
            btnMic.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, METER_RED);
            AttachDeviceButtonHover(btnMic, METER_RED);
            btnMic.Click += (s, e) => ShowMicDropdown(btnMic);
            this.Controls.Add(btnMic);

            // SELECT SPEAKER — blue glow
            var btnSpk = new Button
            {
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 0, 0, 114),
                Font      = new Font("Segoe UI", SF(14f), FontStyle.Bold),
                Bounds    = new Rectangle(btnX0 + btnW + gap, btnY, btnW, btnH),
                Cursor    = Cursors.Hand,
                TabStop   = false,
                Text      = "  \uD83D\uDD0A  SELECT SPEAKER"
            };
            btnSpk.FlatAppearance.BorderColor = METER_BLUE;
            btnSpk.FlatAppearance.BorderSize  = Math.Max(1, (int)(2 * _scale));
            btnSpk.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, METER_BLUE);
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
                    btnMic.Text = "  \uD83C\uDF99  " + TruncateDevice(_cboMic.Text, 22);
            };
            _cboHeadset.SelectedIndexChanged += (s, e) =>
            {
                if (_cboHeadset.SelectedIndex >= 0)
                    btnSpk.Text = "  \uD83D\uDD0A  " + TruncateDevice(_cboHeadset.Text, 22);
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
                btn.BackColor = Color.FromArgb(0, 0, 0, 114);
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
                Text      = "One United Global LLC 2026  V 7.31",
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
                    name = await HeartbeatService.Instance.GetAgentNameAsync(key);
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
}
