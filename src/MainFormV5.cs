/*
 * MainFormV5.cs  —  ONE Voice Solution v5.1
 *
 * v5.1 Fixes (Apr 2 2026):
 *   - Header: extra top cushion (~half inch), clear space between "Audio Dashboard" and "Agent:" label
 *   - LIVE pill: repositioned left — no overlap with minimize/close buttons
 *   - Device labels: bold, 3pts bigger, wider containers
 *   - Dropdown selected state: bright ONE sky blue background, persists after selection
 *   - Panel titles: 2pts bigger, subtitle on SAME LINE as title (right-aligned)
 *   - More vertical gap between meter label and bar
 *   - Meter bars: sky blue (ONE logo blue) active segments; dark navy inactive; ALL start at 0
 *   - Only "My Mic Level" meter moves; all others stay at 0
 *   - Auto Level-Match badge: font 2pts bigger, button taller
 *   - Helper text: 1pt bigger, color white
 *   - Side panels fill all the way down to footer — no empty black space
 *   - Video: reduced to ~2/3 of previous size, WMP controls hidden, fills panel edge-to-edge
 *   - Footer: half-inch bottom cushion, text vertically centered within taller footer
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
        private static readonly Color ONE_RED      = Color.FromArgb(254, 1, 1);
        private static readonly Color BG_DARK      = Color.FromArgb(18, 18, 18);
        private static readonly Color BG_PANEL     = Color.FromArgb(28, 28, 28);
        private static readonly Color TEXT_WHITE   = Color.White;
        private static readonly Color TEXT_GREY    = Color.FromArgb(155, 155, 155);
        // Sky blue from ONE logo
        private static readonly Color ONE_BLUE_SEL = Color.FromArgb(0, 102, 204);
        // Meter segment colours — sky blue active, dark navy inactive
        private static readonly Color SEG_OFF      = Color.FromArgb(0, 40, 90);
        private static readonly Color SEG_ON       = Color.FromArgb(30, 144, 255);
        private static readonly Color SEG_PEAK     = Color.FromArgb(0, 200, 255);

        // ── Scale ─────────────────────────────────────────────────────────────
        private float _scale = 1.0f;
        private int HEADER_H     => (int)(148 * _scale);  // taller: ~half-inch extra cushion
        private int REDLINE_H    => 3;
        private int DEVICE_ROW_H => (int)(58  * _scale);
        private int FOOTER_H     => (int)(48  * _scale);  // taller: half-inch cushion
        private int SIDE_PAD     => (int)(26  * _scale);
        private int VIDEO_GAP    => (int)(12  * _scale);
        private int METER_H      => (int)(28  * _scale);
        private int BADGE_H      => (int)(36  * _scale);  // 2pts taller badge
        private const int METER_SEGS = 24;
        private float SF(float pt) => Math.Max(7f, (float)Math.Round(pt * _scale, 1));

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        // ── Audio ─────────────────────────────────────────────────────────────
        private WasapiCapture      _micCapture;
        private MMDeviceEnumerator _deviceEnum = new MMDeviceEnumerator();
        private float _micLevel            = 0f;
        private float _agentScriptLevel    = 0f;
        private float _customerScriptLevel = 0f;
        private float _agentVoiceSlider     = 0.62f;
        private float _agentScriptSlider    = 0.48f;
        private float _customerVoiceSlider  = 0.55f;
        private float _customerScriptSlider = 0.55f;
        private bool _agentAutoLevel    = true;
        private bool _customerAutoLevel = true;

        // ── UI Controls ───────────────────────────────────────────────────────
        private PictureBox  _logoBox;
        private Label       _lblVoiceSolution;
        private Label       _lblAudioDashboard;
        private Label       _lblAgentName;
        private Panel       _livePill;
        private Panel       _redLine;
        private ComboBox    _cboMic;
        private ComboBox    _cboHeadset;
        private Label       _lblMicLabel;
        private Label       _lblHeadsetLabel;
        private Panel       _videoPanel;
        private AxWMPLib.AxWindowsMediaPlayer _videoPlayer;
        private Panel       _myMicMeterLeft;
        private Panel       _agentScriptMeter;
        private Panel       _customerVoiceMeter;
        private Panel       _customerScriptMeter;
        private Button      _btnAgentAutoLevel;
        private Button      _btnCustomerAutoLevel;
        private Label       _lblTagline;
        private Label       _lblFooterLeft;
        private Label       _lblFooterCenter;
        private NotifyIcon  _trayIcon;
        private ContextMenuStrip _trayMenu;
        private System.Windows.Forms.Timer _meterTimer;
        private System.Windows.Forms.Timer _livePulseTimer;
        private bool _livePulseState = true;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION        = 0x2;
        private Button _btnClose;
        private Button _btnMinimize;

        // ── Constructor ───────────────────────────────────────────────────────
        public MainFormV5() : this(null) { }
        public MainFormV5(string agentNameOverride)
        {
            InitializeComponent();
            BuildUI();
            SetupTrayIcon();
            SetupMeterTimer();
            SetupLivePulse();
            LoadSettings();
            PopulateDevices();
            StartAudioCapture();
            StartHeartbeat();
            if (!string.IsNullOrEmpty(agentNameOverride) && _lblAgentName != null)
                _lblAgentName.Text = "Agent: " + agentNameOverride;
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
            int w = Math.Max((int)(wa.Width  * 0.96), 900);
            int h = Math.Max((int)(wa.Height * 0.96), 600);
            _scale = Math.Max(0.60f, Math.Min(Math.Min((float)w / 1280f, (float)h / 900f), 1.20f));
            this.ClientSize = new Size(w, h);
            this.Location   = new Point(wa.Left + (wa.Width - w) / 2, wa.Top + (wa.Height - h) / 2);
        }

        // ── Build UI ──────────────────────────────────────────────────────────
        private void BuildUI()
        {
            int W = this.ClientSize.Width;
            int H = this.ClientSize.Height;
            this.Paint += (s, e) => DrawRedBorder(e.Graphics);
            BuildHeader(W);
            _redLine = new Panel { BackColor = ONE_RED, Bounds = new Rectangle(0, HEADER_H, W, REDLINE_H) };
            this.Controls.Add(_redLine);
            BuildDeviceRow(W);
            int contentTop = HEADER_H + REDLINE_H + DEVICE_ROW_H + (int)(14 * _scale);
            BuildContentArea(W, H, contentTop);
            BuildFooter(W, H);
        }

        // ── Header ────────────────────────────────────────────────────────────
        private void BuildHeader(int W)
        {
            int logoSz = (int)(104 * _scale);
            int logoY  = (int)(22 * _scale) + (HEADER_H - (int)(22 * _scale) - logoSz) / 2;
            int logoX  = SIDE_PAD;

            _logoBox = new PictureBox
            {
                Bounds    = new Rectangle(logoX, logoY, logoSz, logoSz),
                SizeMode  = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            LoadLogo();
            this.Controls.Add(_logoBox);
            AttachDrag(_logoBox);

            // "Voice Solution" — beside logo, baseline aligned
            int vsX = logoX + logoSz + (int)(10 * _scale);
            _lblVoiceSolution = new Label
            {
                Text      = "Voice Solution",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(14f), FontStyle.Bold),
                AutoSize  = false,
                TextAlign = ContentAlignment.BottomLeft,
                Bounds    = new Rectangle(vsX, logoY, (int)(160 * _scale), logoSz)
            };
            this.Controls.Add(_lblVoiceSolution);
            AttachDrag(_lblVoiceSolution);

            // "Audio Dashboard" — centred, with top cushion
            int dashY = (int)(24 * _scale);
            _lblAudioDashboard = new Label
            {
                Text      = "Audio Dashboard",
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(30f), FontStyle.Bold),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(0, dashY, W, (int)(48 * _scale))
            };
            this.Controls.Add(_lblAudioDashboard);
            AttachDrag(_lblAudioDashboard);

            // "Agent:" — clear separation below "Audio Dashboard"
            int agentY = dashY + (int)(54 * _scale);
            _lblAgentName = new Label
            {
                Text      = "Agent: " + AppSettings.Instance.AgentName,
                ForeColor = TEXT_GREY,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(14f), FontStyle.Regular),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(0, agentY, W, (int)(26 * _scale))
            };
            this.Controls.Add(_lblAgentName);
            AttachDrag(_lblAgentName);

            // Window buttons — absolute top-right
            int btnSz = (int)(32 * _scale);
            int btnY  = (int)(8  * _scale);
            _btnClose = new Button
            {
                Text      = "X",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = ONE_RED,
                Font      = new Font("Segoe UI", SF(11f), FontStyle.Bold),
                Bounds    = new Rectangle(W - btnSz - 6, btnY, btnSz, btnSz),
                Cursor    = Cursors.Hand,
                TabStop   = false
            };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 0, 0);
            _btnClose.Click += (s, e) => Application.Exit();
            this.Controls.Add(_btnClose);

            _btnMinimize = new Button
            {
                Text      = "_",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(55, 55, 55),
                Font      = new Font("Segoe UI", SF(11f), FontStyle.Bold),
                Bounds    = new Rectangle(W - btnSz * 2 - 12, btnY, btnSz, btnSz),
                Cursor    = Cursors.Hand,
                TabStop   = false
            };
            _btnMinimize.FlatAppearance.BorderSize = 0;
            _btnMinimize.FlatAppearance.MouseOverBackColor = Color.FromArgb(90, 90, 90);
            _btnMinimize.Click += (s, e) => { this.WindowState = FormWindowState.Minimized; };
            this.Controls.Add(_btnMinimize);

            // LIVE pill — well left of window buttons, no overlap
            int pillW = (int)(168 * _scale);
            int pillH = (int)(32  * _scale);
            int pillX = W - btnSz * 2 - 12 - pillW - (int)(20 * _scale);
            int pillY = btnY + (btnSz - pillH) / 2;
            _livePill = new Panel { Bounds = new Rectangle(pillX, pillY, pillW, pillH), BackColor = ONE_RED };
            _livePill.Paint += DrawLivePill;
            this.Controls.Add(_livePill);

            this.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && e.Y < HEADER_H)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                }
            };
        }

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

        private void DrawLivePill(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var p = (Panel)sender;
            using (var path = RoundedRect(new Rectangle(0, 0, p.Width - 1, p.Height - 1), 15))
            using (var brush = new SolidBrush(ONE_RED))
                g.FillPath(brush, path);
            using (var dot = new SolidBrush(_livePulseState ? Color.White : Color.FromArgb(150, 255, 255, 255)))
                g.FillEllipse(dot, 10, (p.Height - 10) / 2, 10, 10);
            using (var font  = new Font("Segoe UI", SF(12f), FontStyle.Bold))
            using (var brush2 = new SolidBrush(Color.White))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                g.DrawString("  LIVE  \u2022  Connected", font, brush2,
                    new RectangleF(20, 0, p.Width - 20, p.Height), sf);
            }
        }

        // ── Device row ────────────────────────────────────────────────────────
        private void BuildDeviceRow(int W)
        {
            int top   = HEADER_H + REDLINE_H + (int)(8 * _scale);
            int dropW = (W - SIDE_PAD * 2 - (int)(50 * _scale)) / 2;

            // Microphone label — bold, 3pts bigger (14 vs 11)
            _lblMicLabel = MakeLabel("Microphone", SIDE_PAD, top, 14, bold: true, color: TEXT_WHITE);
            _cboMic = new ComboBox
            {
                Bounds        = new Rectangle(SIDE_PAD, top + (int)(22 * _scale), dropW, (int)(30 * _scale)),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = BG_PANEL,
                ForeColor     = TEXT_WHITE,
                FlatStyle     = FlatStyle.Flat,
                Font          = new Font("Segoe UI", SF(12f))
            };
            _cboMic.SelectedIndexChanged += (s, e) =>
            {
                _cboMic.BackColor = ONE_BLUE_SEL;
                _cboMic.ForeColor = Color.White;
                AppSettings.Instance.MicDevice = _cboMic.Text;
                AppSettings.Instance.Save();
                StartAudioCapture(_cboMic.Text);
            };
            this.Controls.Add(_cboMic);

            int rightX = W - SIDE_PAD - dropW;
            // Headset/Speaker label — bold, 3pts bigger
            _lblHeadsetLabel = MakeLabel("Headset / Speaker", rightX, top, 14, bold: true, color: TEXT_WHITE);
            _cboHeadset = new ComboBox
            {
                Bounds        = new Rectangle(rightX, top + (int)(22 * _scale), dropW, (int)(30 * _scale)),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = BG_PANEL,
                ForeColor     = TEXT_WHITE,
                FlatStyle     = FlatStyle.Flat,
                Font          = new Font("Segoe UI", SF(12f))
            };
            _cboHeadset.SelectedIndexChanged += (s, e) =>
            {
                _cboHeadset.BackColor = ONE_BLUE_SEL;
                _cboHeadset.ForeColor = Color.White;
                AppSettings.Instance.HeadsetDevice = _cboHeadset.Text;
                AppSettings.Instance.Save();
            };
            this.Controls.Add(_cboHeadset);
        }

        // ── Content area ──────────────────────────────────────────────────────
        private void BuildContentArea(int W, int H, int top)
        {
            int taglineH   = (int)(28 * _scale);
            int taglineGap = (int)(8  * _scale);
            int availH     = H - top - taglineH - taglineGap - FOOTER_H;

            // Side panels narrower so video is ~2/3 of full width
            int sideW     = (int)(240 * _scale);
            int videoLeft = SIDE_PAD + sideW + VIDEO_GAP;
            int videoW    = W - videoLeft - VIDEO_GAP - sideW - SIDE_PAD;
            // Video height = 2/3 of available height, vertically centered
            int videoH    = (int)(availH * 0.67f);
            if (videoH < 180) videoH = 180;
            int videoTop  = top + (availH - videoH) / 2;

            // Side panels fill FULL available height
            int panelTop = top;
            int panelH   = availH;

            // Video panel
            _videoPanel = new Panel
            {
                Bounds    = new Rectangle(videoLeft, videoTop, videoW, videoH),
                BackColor = Color.Black
            };
            this.Controls.Add(_videoPanel);

            // WMP — looping, muted, no chrome
            try
            {
                _videoPlayer = new AxWMPLib.AxWindowsMediaPlayer
                {
                    Bounds = new Rectangle(0, 0, videoW, videoH)
                };
                _videoPanel.Controls.Add(_videoPlayer);
                _videoPlayer.CreateControl();
                _videoPlayer.uiMode       = "none";
                _videoPlayer.stretchToFit = true;

                string videoPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Resources", "1ONEDigitalVideo.mp4");
                if (File.Exists(videoPath))
                {
                    _videoPlayer.settings.setMode("loop", true);
                    _videoPlayer.settings.volume = 0;
                    _videoPlayer.URL    = new Uri(videoPath).AbsoluteUri;
                    _videoPlayer.uiMode = "none";
                    _videoPlayer.Ctlcontrols.play();

                    // Enforce uiMode="none" for 5 seconds (25 x 200ms)
                    var uiFixTimer = new System.Windows.Forms.Timer { Interval = 200 };
                    int uiFixCount = 0;
                    uiFixTimer.Tick += (ts, te) =>
                    {
                        try
                        {
                            if (_videoPlayer.uiMode != "none") _videoPlayer.uiMode = "none";
                            _videoPlayer.Bounds = new Rectangle(0, 0, _videoPanel.Width, _videoPanel.Height);
                        }
                        catch { }
                        if (++uiFixCount >= 25) uiFixTimer.Stop();
                    };
                    uiFixTimer.Start();

                    _videoPlayer.PlayStateChange += (s2, e2) =>
                    {
                        Action fix = () =>
                        {
                            try { if (_videoPlayer.uiMode != "none") _videoPlayer.uiMode = "none"; } catch { }
                            if (e2.newState == 1) { try { _videoPlayer.Ctlcontrols.play(); } catch { } }
                        };
                        if (this.InvokeRequired) this.BeginInvoke(fix); else fix();
                    };
                }
            }
            catch (Exception ex) { Log.Warn($"[Video] WMP failed: {ex.Message}"); }

            // Side panels
            BuildMeterPanel(SIDE_PAD, panelTop, sideW, panelH, isLeft: true);
            int rightX = videoLeft + videoW + VIDEO_GAP;
            BuildMeterPanel(rightX, panelTop, sideW, panelH, isLeft: false);

            // Tagline below content area
            int tagY = top + availH + taglineGap;
            _lblTagline = new Label
            {
                Text      = "\u201c The Geniusness Is In The Simplicity \u201d",
                ForeColor = ONE_RED,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(18f), FontStyle.Bold | FontStyle.Italic),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(videoLeft, tagY, videoW, taglineH)
            };
            this.Controls.Add(_lblTagline);
        }

        // ── Meter panel ───────────────────────────────────────────────────────
        private void BuildMeterPanel(int x, int top, int w, int panelH, bool isLeft)
        {
            string title   = isLeft ? "AGENT AUDIO"     : "CUSTOMER OUTPUT";
            string sub     = isLeft ? "(What You Hear)"  : "(What They Hear)";
            // Left: "My Mic Level" moves; Right: "Customer Voice" stays at 0
            string m1Label = isLeft ? "My Mic Level"    : "Customer Voice";
            string m2Label = "Script Playback";

            // Title — 2pts bigger (15 vs 13), left portion of row
            var lblTitle = new Label
            {
                Text      = title,
                ForeColor = ONE_RED,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(15f), FontStyle.Bold),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds    = new Rectangle(x, top, (int)(w * 0.58f), (int)(22 * _scale))
            };
            this.Controls.Add(lblTitle);

            // Subtitle — same line, right portion, 2pts bigger (12 vs 10)
            var lblSub = new Label
            {
                Text      = sub,
                ForeColor = TEXT_GREY,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(12f)),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleRight,
                Bounds    = new Rectangle(x + (int)(w * 0.58f), top, (int)(w * 0.42f), (int)(22 * _scale))
            };
            this.Controls.Add(lblSub);

            // Meter 1 label + bar with extra separation
            int m1LabelY = top + (int)(28 * _scale);
            MakeLabel(m1Label, x, m1LabelY, 11, color: TEXT_WHITE);
            int m1BarY = m1LabelY + (int)(20 * _scale);
            MakeMeterPanel(x, m1BarY, w, isLeft ? "myMicLevel" : "customerVoice");

            // Meter 2 label + bar with extra separation
            int m2LabelY = m1BarY + METER_H + (int)(14 * _scale);
            MakeLabel(m2Label, x, m2LabelY, 11, color: TEXT_WHITE);
            int m2BarY = m2LabelY + (int)(20 * _scale);
            MakeMeterPanel(x, m2BarY, w, isLeft ? "agentScript" : "customerScript");

            // Auto Level-Match badge — 2pts bigger font (13 vs 11), taller button
            int badgeTop = m2BarY + METER_H + (int)(16 * _scale);
            var btnBadge = new Button
            {
                Text      = "\u25cf Auto Level-Match: ON",
                ForeColor = Color.White,
                BackColor = ONE_RED,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", SF(13f), FontStyle.Bold),
                Bounds    = new Rectangle(x, badgeTop, w, BADGE_H),
                Cursor    = Cursors.Hand
            };
            btnBadge.FlatAppearance.BorderSize = 0;
            btnBadge.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 0, 0);
            btnBadge.Click += (s, e) => ToggleAutoLevel(isLeft, btnBadge);
            this.Controls.Add(btnBadge);
            if (isLeft) _btnAgentAutoLevel = btnBadge; else _btnCustomerAutoLevel = btnBadge;

            // Helper hint — 1pt bigger (10 vs 9), color white
            int hintY = badgeTop + BADGE_H + (int)(4 * _scale);
            var lblHint = new Label
            {
                Text      = "Tap to switch between manual & automatic level control",
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(10f), FontStyle.Italic),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds    = new Rectangle(x, hintY, w, (int)(18 * _scale))
            };
            this.Controls.Add(lblHint);

            // Fill remaining space to footer — no dead black space
            int filledTop = hintY + (int)(18 * _scale) + (int)(4 * _scale);
            int filledH   = (top + panelH) - filledTop;
            if (filledH > 0)
            {
                var filler = new Panel
                {
                    Bounds    = new Rectangle(x, filledTop, w, filledH),
                    BackColor = BG_DARK
                };
                this.Controls.Add(filler);
            }
        }

        private Panel MakeMeterPanel(int x, int y, int w, string key)
        {
            var panel = new Panel
            {
                Bounds    = new Rectangle(x, y, w, METER_H),
                BackColor = Color.Transparent,
                Tag       = key
            };
            panel.Paint += (s, e) => DrawVUMeter(e.Graphics, (Panel)s, key);
            this.Controls.Add(panel);
            switch (key)
            {
                case "myMicLevel":    _myMicMeterLeft     = panel; break;
                case "customerVoice": _customerVoiceMeter  = panel; break;
                case "agentScript":   _agentScriptMeter    = panel; break;
                case "customerScript":_customerScriptMeter = panel; break;
            }
            return panel;
        }

        // ── Segmented VU bar ──────────────────────────────────────────────────
        private void DrawVUMeter(Graphics g, Panel panel, string key)
        {
            int w    = panel.Width;
            int h    = panel.Height;
            int segs = METER_SEGS;
            int gap  = 2;
            int segW = Math.Max(2, (w - gap * (segs - 1)) / segs);
            float level  = GetLevel(key);
            int   litCnt = (int)(level * segs);
            for (int i = 0; i < segs; i++)
            {
                int   sx   = i * (segW + gap);
                bool  lit  = i < litCnt;
                bool  peak = lit && i >= (int)(segs * 0.85f);
                Color fill = lit ? (peak ? SEG_PEAK : SEG_ON) : SEG_OFF;
                using (var b = new SolidBrush(fill))
                    g.FillRectangle(b, sx, 0, segW, h);
            }
            using (var pen = new Pen(Color.FromArgb(20, 20, 40), 1f))
                g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
        }

        private float GetLevel(string key)
        {
            switch (key)
            {
                case "myMicLevel":    return _micLevel;            // only this moves
                case "customerVoice": return 0f;
                case "agentScript":   return _agentScriptLevel;
                case "customerScript":return _customerScriptLevel;
                default: return 0f;
            }
        }

        // ── Footer ────────────────────────────────────────────────────────────
        private void BuildFooter(int W, int H)
        {
            int fy = H - FOOTER_H;
            _lblFooterLeft = new Label
            {
                Text      = "ONE United Global  \u2022  2026  \u2022  v5.1",
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(12f)),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds    = new Rectangle(SIDE_PAD, fy, 320, FOOTER_H)
            };
            this.Controls.Add(_lblFooterLeft);

            _lblFooterCenter = new Label
            {
                Text      = "This App Can Be Minimized  \u2022  Settings Are Auto-Saved",
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(12f)),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(0, fy, W, FOOTER_H)
            };
            this.Controls.Add(_lblFooterCenter);
        }

        // ── Borders ───────────────────────────────────────────────────────────
        private void DrawRedBorder(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int W = this.ClientSize.Width, H = this.ClientSize.Height;
            using (var pen = new Pen(ONE_RED, 3f))
                g.DrawRectangle(pen, 1, 1, W - 3, H - 3);
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
                        "Minimized to tray \u2014 double-click to restore.", ToolTipIcon.Info);
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
                _micLevel = Math.Max(0f, _micLevel - 0.05f);
                _myMicMeterLeft?.Invalidate();
                if (_agentScriptLevel > 0)
                {
                    _agentScriptLevel = Math.Max(0f, _agentScriptLevel - 0.05f);
                    _agentScriptMeter?.Invalidate();
                }
                if (_customerScriptLevel > 0)
                {
                    _customerScriptLevel = Math.Max(0f, _customerScriptLevel - 0.05f);
                    _customerScriptMeter?.Invalidate();
                }
            };
            _meterTimer.Start();
        }

        private void SetupLivePulse()
        {
            _livePulseTimer = new System.Windows.Forms.Timer { Interval = 800 };
            _livePulseTimer.Tick += (s, e) =>
            {
                _livePulseState = !_livePulseState;
                _livePill?.Invalidate();
            };
            _livePulseTimer.Start();
        }

        // ── Audio capture ─────────────────────────────────────────────────────
        private void StartAudioCapture(string deviceName = null)
        {
            try { _micCapture?.StopRecording(); } catch { }
            try { _micCapture?.Dispose(); }       catch { }
            _micCapture = null;
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
                    _micLevel = Math.Min(1f, max * 3.0f);
                };
                _micCapture.StartRecording();
            }
            catch (Exception ex) { Log.Warn($"[Audio] Capture failed: {ex.Message}"); }
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
                    // Blue bg immediately on load
                    _cboMic.BackColor = ONE_BLUE_SEL;
                    _cboMic.ForeColor = Color.White;
                }
                var rends = _deviceEnum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (var d in rends) _cboHeadset.Items.Add(d.FriendlyName);
                if (_cboHeadset.Items.Count > 0)
                {
                    int idx = _cboHeadset.Items.IndexOf(AppSettings.Instance.HeadsetDevice);
                    _cboHeadset.SelectedIndex = idx >= 0 ? idx : 0;
                    // Blue bg immediately on load
                    _cboHeadset.BackColor = ONE_BLUE_SEL;
                    _cboHeadset.ForeColor = Color.White;
                }
            }
            catch (Exception ex) { Log.Warn($"[Audio] Device enum: {ex.Message}"); }
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
            _agentVoiceSlider     = s.GetVolume("agentVoice",     0.62f);
            _agentScriptSlider    = s.GetVolume("agentScript",    0.48f);
            _customerVoiceSlider  = s.GetVolume("customerVoice",  0.55f);
            _customerScriptSlider = s.GetVolume("customerScript", 0.55f);
            _agentAutoLevel       = s.AgentAutoLevel;
            _customerAutoLevel    = s.CustomerAutoLevel;
        }

        // ── Logo ──────────────────────────────────────────────────────────────
        private void LoadLogo()
        {
            string agencyLogo = AppSettings.Instance.AgencyLogoPath;
            string oneLogo    = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "one_logo.png");
            string logoPath   = (!string.IsNullOrEmpty(agencyLogo) && File.Exists(agencyLogo))
                ? agencyLogo : oneLogo;
            if (File.Exists(logoPath))
                _logoBox.Image = Image.FromFile(logoPath);
        }

        // ── Auto Level-Match toggle ───────────────────────────────────────────
        private void ToggleAutoLevel(bool isLeft, Button btn)
        {
            if (isLeft)
            {
                _agentAutoLevel = !_agentAutoLevel;
                btn.Text = _agentAutoLevel ? "\u25cf Auto Level-Match: ON" : "\u25cb Auto Level-Match: OFF";
                AppSettings.Instance.AgentAutoLevel = _agentAutoLevel;
            }
            else
            {
                _customerAutoLevel = !_customerAutoLevel;
                btn.Text = _customerAutoLevel ? "\u25cf Auto Level-Match: ON" : "\u25cb Auto Level-Match: OFF";
                AppSettings.Instance.CustomerAutoLevel = _customerAutoLevel;
            }
            AppSettings.Instance.Save();
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private Label MakeLabel(string text, int x, int y, float size, bool bold = false, Color? color = null)
        {
            var lbl = new Label
            {
                Text      = text,
                ForeColor = color ?? TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(size), bold ? FontStyle.Bold : FontStyle.Regular),
                AutoSize  = true,
                Location  = new Point(x, y)
            };
            this.Controls.Add(lbl);
            return lbl;
        }

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
            _meterTimer?.Stop();
            _livePulseTimer?.Stop();
            _micCapture?.StopRecording();
            _micCapture?.Dispose();
            _trayIcon?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
