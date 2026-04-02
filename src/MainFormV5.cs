/*
 * MainFormV5.cs  —  ONE Voice Solution v5.0
 *
 * Layout:
 *   [ONE Logo]  Voice Solution   |  Audio Dashboard   Agent: {name}  | [─][×]
 *   ──────────────────────────────────────────────────── RED LINE
 *   Microphone [▾]                          Headset / Speaker [▾]
 *
 *   AGENT AUDIO    ┌──────── VIDEO (large) ──────┐   CUSTOMER OUTPUT
 *   Customer Voice │                             │   My Mic Level
 *   [▓▓▓▓░░░░░░]  │                             │   [▓▓▓▓░░░░░░]
 *   Script Playback│                             │   Script Playback
 *   [░░░░░░░░░░]  └─────────────────────────────┘   [░░░░░░░░░░]
 *   [● Auto Level-Match: ON]         [● Auto Level-Match: ON]
 *   Tap to toggle manual / automatic  Tap to toggle manual / automatic
 *
 *        " The Geniusness Is In The Simplicity "
 *   ──────────────────────────────────────────────────── RED LINE
 *   ONE United Global 2026 v5.0        This App Can Be Minimized
 *
 * Changes in this version:
 *   - ONE logo bigger; "Voice Solution" beside it (same row, not under)
 *   - Video panel much larger (~72% of available height)
 *   - Segmented VU bars — no slider thumb, no gradient, no jitter
 *   - Only mic meter moves; script/customer meters stay flat until audio plays
 *   - WMP uiMode forced via BeginInvoke delay + PlayStateChange handler
 *   - Minimize/Close buttons at absolute top-right corner, always visible
 *   - Window drag attached to all header child controls
 *   - No dead space — tagline sits tight below video
 *   - Helper hint text under each Auto Level-Match badge
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
        private static readonly Color ONE_RED   = Color.FromArgb(254, 1, 1);
        private static readonly Color BG_DARK   = Color.FromArgb(18, 18, 18);
        private static readonly Color BG_PANEL  = Color.FromArgb(28, 28, 28);
        private static readonly Color TEXT_WHITE = Color.White;
        private static readonly Color TEXT_GREY  = Color.FromArgb(155, 155, 155);
        private static readonly Color SEG_OFF    = Color.FromArgb(55, 10, 10);
        private static readonly Color SEG_ON     = Color.FromArgb(254, 1, 1);
        private static readonly Color SEG_PEAK   = Color.FromArgb(255, 145, 0);

        // ── Scale — set from screen dimensions at startup ─────────────────────
        private float _scale = 1.0f;
        private int HEADER_H     => (int)(100 * _scale);
        private int REDLINE_H    => 3;
        private int DEVICE_ROW_H => (int)(52  * _scale);
        private int FOOTER_H     => (int)(40  * _scale);
        private int SIDE_PAD     => (int)(26  * _scale);
        private int VIDEO_GAP    => (int)(12  * _scale);
        private int METER_H      => (int)(20  * _scale);
        private int BADGE_H      => (int)(30  * _scale);
        private const int METER_SEGS = 24;
        private float SF(float pt) => Math.Max(7f, (float)Math.Round(pt * _scale, 1));

        // ── Logger ────────────────────────────────────────────────────────────
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        // ── Audio ─────────────────────────────────────────────────────────────
        private WasapiCapture      _micCapture;
        private MMDeviceEnumerator _deviceEnum = new MMDeviceEnumerator();

        // Only _micLevel is driven by live capture.
        // Script meters stay at 0 unless audio is actually routed to them.
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
        private Panel       _micMeterLeft;        // left panel meter 1 (shows mic)
        private Panel       _micMeterRight;       // right panel meter 1 (shows mic)
        private Panel       _agentScriptMeter;
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

        // ── P/Invoke — borderless drag ────────────────────────────────────────
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

        // ── Form init ─────────────────────────────────────────────────────────
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
            int contentTop = HEADER_H + REDLINE_H + DEVICE_ROW_H + 8;
            BuildContentArea(W, H, contentTop);
            BuildFooter(W, H);
        }

        // ── Header ────────────────────────────────────────────────────────────
        private void BuildHeader(int W)
        {
            int logoSz = (int)(86 * _scale);
            int logoY  = (HEADER_H - logoSz) / 2;
            int logoX  = SIDE_PAD;

            // ONE logo — large square on the left
            _logoBox = new PictureBox
            {
                Bounds    = new Rectangle(logoX, logoY, logoSz, logoSz),
                SizeMode  = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            LoadLogo();
            this.Controls.Add(_logoBox);
            AttachDrag(_logoBox);

            // "Voice Solution" — beside the logo, vertically centred to it
            int vsX = logoX + logoSz + (int)(10 * _scale);
            int vsY = logoY + (logoSz / 2) - (int)(SF(14f) * 0.8f);
            _lblVoiceSolution = new Label
            {
                Text      = "Voice Solution",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(14f), FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(vsX, vsY)
            };
            this.Controls.Add(_lblVoiceSolution);
            AttachDrag(_lblVoiceSolution);

            // "Audio Dashboard" — centred across full width
            _lblAudioDashboard = new Label
            {
                Text      = "Audio Dashboard",
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(30f), FontStyle.Bold),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(0, (int)(8 * _scale), W, (int)(48 * _scale))
            };
            this.Controls.Add(_lblAudioDashboard);
            AttachDrag(_lblAudioDashboard);

            // Agent name — centred below Audio Dashboard
            _lblAgentName = new Label
            {
                Text      = "Agent: " + AppSettings.Instance.AgentName,
                ForeColor = TEXT_GREY,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(14f), FontStyle.Regular),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(0, (int)(56 * _scale), W, (int)(26 * _scale))
            };
            this.Controls.Add(_lblAgentName);
            AttachDrag(_lblAgentName);

            // LIVE pill — right side, clear of window buttons
            int pillW = (int)(168 * _scale);
            int pillH = (int)(32  * _scale);
            int pillX = W - pillW - (int)(88 * _scale);
            int pillY = (HEADER_H - pillH) / 2;
            _livePill = new Panel { Bounds = new Rectangle(pillX, pillY, pillW, pillH), BackColor = ONE_RED };
            _livePill.Paint += DrawLivePill;
            this.Controls.Add(_livePill);

            // Window buttons — absolute top-right, always visible
            int btnSz = (int)(32 * _scale);
            int btnY  = (int)(7  * _scale);

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

            // Form-level drag on the header background
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
            using (var brush = new SolidBrush(Color.White))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                g.DrawString("  LIVE  \u2022  Connected", font, brush,
                    new RectangleF(20, 0, p.Width - 20, p.Height), sf);
            }
        }

        // ── Device row ────────────────────────────────────────────────────────
        private void BuildDeviceRow(int W)
        {
            int top   = HEADER_H + REDLINE_H + 6;
            int dropW = (W - SIDE_PAD * 2 - 40) / 2;

            _lblMicLabel = MakeLabel("Microphone", SIDE_PAD, top, 11, color: TEXT_GREY);
            _cboMic = new ComboBox
            {
                Bounds        = new Rectangle(SIDE_PAD, top + 15, dropW, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = BG_PANEL,
                ForeColor     = TEXT_WHITE,
                FlatStyle     = FlatStyle.Flat,
                Font          = new Font("Segoe UI", SF(12f))
            };
            _cboMic.SelectedIndexChanged += (s, e) =>
            {
                AppSettings.Instance.MicDevice = _cboMic.Text;
                AppSettings.Instance.Save();
                StartAudioCapture(_cboMic.Text);
            };
            this.Controls.Add(_cboMic);

            int rightX = W - SIDE_PAD - dropW;
            _lblHeadsetLabel = MakeLabel("Headset / Speaker", rightX, top, 11, color: TEXT_GREY);
            _cboHeadset = new ComboBox
            {
                Bounds        = new Rectangle(rightX, top + 15, dropW, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = BG_PANEL,
                ForeColor     = TEXT_WHITE,
                FlatStyle     = FlatStyle.Flat,
                Font          = new Font("Segoe UI", SF(12f))
            };
            _cboHeadset.SelectedIndexChanged += (s, e) =>
            {
                AppSettings.Instance.HeadsetDevice = _cboHeadset.Text;
                AppSettings.Instance.Save();
            };
            this.Controls.Add(_cboHeadset);
        }

        // ── Content area ──────────────────────────────────────────────────────
        private void BuildContentArea(int W, int H, int top)
        {
            int availH = H - top - FOOTER_H - 8;

            // Video: 72% of available height, ~4:3 aspect
            int videoH = (int)(availH * 0.72f);
            int videoW = (int)(videoH * 1.45f);
            int maxVW  = W - SIDE_PAD * 2 - (int)(220 * _scale) * 2;
            if (videoW > maxVW) { videoW = maxVW; videoH = (int)(videoW / 1.45f); }

            int videoLeft = (W - videoW) / 2;
            int videoTop  = top + (int)(10 * _scale);

            // Video panel
            _videoPanel = new Panel
            {
                Bounds    = new Rectangle(videoLeft, videoTop, videoW, videoH),
                BackColor = Color.Black
            };
            _videoPanel.Paint += DrawVideoRedBorder;
            this.Controls.Add(_videoPanel);

            // WMP — looping, muted, no UI chrome
            try
            {
                _videoPlayer = new AxWMPLib.AxWindowsMediaPlayer
                {
                    Bounds = new Rectangle(2, 2, videoW - 4, videoH - 4)
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
                    _videoPlayer.URL = videoPath;
                    _videoPlayer.uiMode = "none";
                    _videoPlayer.Ctlcontrols.play();

                    // Force uiMode hidden after WMP finishes its internal load
                    this.BeginInvoke((Action)(() =>
                    {
                        try { _videoPlayer.uiMode = "none"; } catch { }
                    }));

                    _videoPlayer.PlayStateChange += (s2, e2) =>
                    {
                        try
                        {
                            if (_videoPlayer.uiMode != "none") _videoPlayer.uiMode = "none";
                            if (e2.newState == 1) _videoPlayer.Ctlcontrols.play();
                        }
                        catch { }
                    };
                }
            }
            catch (Exception ex) { Log.Warn($"[Video] WMP failed: {ex.Message}"); }

            // Side panels flush with video
            int sideW = videoLeft - SIDE_PAD - VIDEO_GAP;
            BuildMeterPanel(SIDE_PAD, videoTop, sideW, videoH, isLeft: true);

            int rightX = videoLeft + videoW + VIDEO_GAP;
            int rightW = W - rightX - SIDE_PAD;
            BuildMeterPanel(rightX, videoTop, rightW, videoH, isLeft: false);

            // Tagline — snug below video
            int tagY = videoTop + videoH + (int)(8 * _scale);
            _lblTagline = new Label
            {
                Text      = "\u201c The Geniusness Is In The Simplicity \u201d",
                ForeColor = ONE_RED,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(18f), FontStyle.Bold | FontStyle.Italic),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(0, tagY, W, (int)(30 * _scale))
            };
            this.Controls.Add(_lblTagline);
        }

        // ── Meter panel ───────────────────────────────────────────────────────
        private void BuildMeterPanel(int x, int top, int w, int panelH, bool isLeft)
        {
            string title = isLeft ? "AGENT AUDIO"     : "CUSTOMER OUTPUT";
            string sub   = isLeft ? "(What You Hear)" : "(What They Hear)";

            var lblTitle = new Label
            {
                Text      = title,
                ForeColor = ONE_RED,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(13f), FontStyle.Bold),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds    = new Rectangle(x, top, w, (int)(20 * _scale))
            };
            this.Controls.Add(lblTitle);

            var lblSub = new Label
            {
                Text      = sub,
                ForeColor = TEXT_GREY,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(10f)),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds    = new Rectangle(x, top + (int)(20 * _scale), w, (int)(16 * _scale))
            };
            this.Controls.Add(lblSub);

            // Meter 1
            string m1Label = isLeft ? "Customer Voice" : "My Mic Level";
            int    m1Top   = top + (int)(44 * _scale);
            MakeLabel(m1Label, x, m1Top, 11, color: TEXT_WHITE);
            var meter1 = MakeMeterPanel(x, m1Top + (int)(16 * _scale), w, isLeft ? "agentVoice" : "customerVoice");

            // Meter 2
            int m2Top = m1Top + (int)(16 * _scale) + METER_H + (int)(12 * _scale);
            MakeLabel("Script Playback", x, m2Top, 11, color: TEXT_WHITE);
            var meter2 = MakeMeterPanel(x, m2Top + (int)(16 * _scale), w, isLeft ? "agentScript" : "customerScript");

            // Auto Level-Match badge
            int badgeTop = m2Top + (int)(16 * _scale) + METER_H + (int)(12 * _scale);
            var btnBadge = new Button
            {
                Text      = "\u25cf Auto Level-Match: ON",
                ForeColor = Color.White,
                BackColor = ONE_RED,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", SF(11f), FontStyle.Bold),
                Bounds    = new Rectangle(x, badgeTop, w, BADGE_H),
                Cursor    = Cursors.Hand
            };
            btnBadge.FlatAppearance.BorderSize = 0;
            btnBadge.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 0, 0);
            btnBadge.Click += (s, e) => ToggleAutoLevel(isLeft, btnBadge);
            this.Controls.Add(btnBadge);
            if (isLeft) _btnAgentAutoLevel = btnBadge; else _btnCustomerAutoLevel = btnBadge;

            // Helper hint under badge
            var lblHint = new Label
            {
                Text      = "Tap to switch between manual & automatic level control",
                ForeColor = Color.FromArgb(100, 100, 100),
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(9f), FontStyle.Italic),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds    = new Rectangle(x, badgeTop + BADGE_H + 2, w, (int)(16 * _scale))
            };
            this.Controls.Add(lblHint);
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
                case "agentVoice":    _micMeterLeft         = panel; break;
                case "customerVoice": _micMeterRight        = panel; break;
                case "agentScript":   _agentScriptMeter     = panel; break;
                case "customerScript":_customerScriptMeter  = panel; break;
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
            using (var pen = new Pen(Color.FromArgb(50, 50, 50), 1f))
                g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
        }

        private float GetLevel(string key)
        {
            switch (key)
            {
                case "agentVoice":    return _micLevel;
                case "customerVoice": return _micLevel;
                case "agentScript":   return _agentScriptLevel;
                case "customerScript":return _customerScriptLevel;
                default: return 0f;
            }
        }

        // ── Footer ────────────────────────────────────────────────────────────
        private void BuildFooter(int W, int H)
        {
            int fy = H - FOOTER_H;
            this.Controls.Add(new Panel { BackColor = ONE_RED, Bounds = new Rectangle(0, fy, W, 2) });

            _lblFooterLeft = new Label
            {
                Text      = "ONE United Global  2026  v5.0",
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(12f)),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds    = new Rectangle(SIDE_PAD, fy + 2, 300, FOOTER_H - 2)
            };
            this.Controls.Add(_lblFooterLeft);

            _lblFooterCenter = new Label
            {
                Text      = "This App Can Be Minimized  \u2022  Settings Are Auto-Saved",
                ForeColor = TEXT_GREY,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(11f)),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(0, fy + 2, W, FOOTER_H - 2)
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

        private void DrawVideoRedBorder(object sender, PaintEventArgs e)
        {
            var p = (Panel)sender;
            using (var pen = new Pen(ONE_RED, 2f))
                e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
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

        // ── Meter timer — only mic level decays ───────────────────────────────
        private void SetupMeterTimer()
        {
            _meterTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _meterTimer.Tick += (s, e) =>
            {
                _micLevel = Math.Max(0f, _micLevel - 0.05f);
                _micMeterLeft?.Invalidate();
                _micMeterRight?.Invalidate();

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
                }
                var rends = _deviceEnum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (var d in rends) _cboHeadset.Items.Add(d.FriendlyName);
                if (_cboHeadset.Items.Count > 0)
                {
                    int idx = _cboHeadset.Items.IndexOf(AppSettings.Instance.HeadsetDevice);
                    _cboHeadset.SelectedIndex = idx >= 0 ? idx : 0;
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
