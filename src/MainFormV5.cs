/*
 * MainFormV5.cs  —  ONE Voice Solution v7.6
 *
 * v7.6 changes:
 *   - Removed Auto Level-Match button (non-functional, cluttered layout)
 *   - Moved AGENT AUDIO / CUSTOMER OUTPUT headings up closer to dropdowns
 *   - Evenly spaced meters between heading and "Tap to switch" hint
 *   - Minimize/Close buttons moved inward (not sitting on border)
 *   - VU meter decay slowed so meters stay visible during playback
 *   - MeteringSampleProvider sensitivity boosted for better response
 *   - "Tap to switch" text replaced with simpler hint
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
        private static readonly Color ONE_BLUE_SEL = Color.FromArgb(0, 102, 204);

        // ── Version ───────────────────────────────────────────────────────────
        private const string APP_VERSION = "7.6";

        // Meter segment colours
        private static readonly Color SEG_OFF  = Color.FromArgb(0, 102, 204);
        private static readonly Color SEG_ON   = Color.FromArgb(254, 1, 1);
        private static readonly Color SEG_PEAK = Color.FromArgb(255, 80, 80);

        // ── Scale ─────────────────────────────────────────────────────────────
        private float _scale = 1.0f;
        private int HEADER_H     => (int)(130 * _scale);
        private int DEVICE_ROW_H => (int)(90  * _scale);
        private int FOOTER_H     => (int)(52  * _scale);
        private int SIDE_PAD     => (int)(30  * _scale);
        private int VIDEO_GAP    => (int)(12  * _scale);
        private int METER_H      => (int)(26  * _scale);
        private const int METER_SEGS = 24;
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
        private MMDeviceEnumerator _deviceEnum = new MMDeviceEnumerator();
        private float _micLevel            = 0f;
        private float _customerVoiceLevel  = 0f;
        private float _agentScriptLevel    = 0f;
        private float _customerScriptLevel = 0f;

        // ── UI Controls ───────────────────────────────────────────────────────
        private PictureBox  _logoBox;
        private Label       _lblVoiceSolution;
        private Label       _lblAudioDashboard;
        private Label       _lblAgentName;
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
        private Label       _lblTagline;
        private Label       _lblFooterLeft;
        private Label       _lblFooterCenter;
        private NotifyIcon  _trayIcon;
        private ContextMenuStrip _trayMenu;
        private System.Windows.Forms.Timer _meterTimer;
        private Button _btnClose;
        private Button _btnMinimize;
        private string _videoFilePath;
        private int    _wmpExtraH = 80;
        private MMDevice _activeMicDevice;
        private MMDevice _activeSpeakerDevice;
        private MMDevice _activeVBCableDevice;
        private TrackBar _trkMicVol;
        private TrackBar _trkSpeakerVol;
        private TrackBar _trkAgentScriptVol;
        private TrackBar _trkCustomerScriptVol;
        private Label    _lblMicVol;
        private Label    _lblSpeakerVol;
        private Label    _lblAgentScriptVol;
        private Label    _lblCustomerScriptVol;

        // Loopback capture for Customer Voice meter
        private WasapiLoopbackCapture _loopbackCapture;

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
                StartVideoPlayback();
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

            int w = Math.Max((int)(wa.Width  * 0.88), 860);
            int h = Math.Max((int)(wa.Height * 0.88), 560);

            float sizeScale = Math.Min((float)w / 1280f, (float)h / 900f);
            _scale = Math.Max(0.48f, Math.Min(sizeScale, 1.20f));

            this.ClientSize = new Size(w, h);
            int cx = wa.Left + (wa.Width - w) / 2;
            int cy = wa.Top + (wa.Height - h) / 2;
            if (cx < wa.Left) cx = wa.Left;
            if (cy < wa.Top) cy = wa.Top;
            this.Location = new Point(cx, cy);

            Log.Info($"[UI] Screen={screen.DeviceName} DPI={dpi} WA={wa} FormSize={w}x{h} Scale={_scale:F2}");
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            DrawRedBorder(e.Graphics);
        }

        // ── Build UI ──────────────────────────────────────────────────────────
        private void BuildUI()
        {
            int W = this.ClientSize.Width;
            int H = this.ClientSize.Height;
            BuildHeader(W);
            BuildDeviceRow(W);
            int contentTop = HEADER_H + DEVICE_ROW_H + (int)(4 * _scale);
            BuildContentArea(W, H, contentTop);
            BuildFooter(W, H);
        }

        // ── Header ────────────────────────────────────────────────────────────
        private void BuildHeader(int W)
        {
            int logoSz = (int)(120 * _scale);
            int logoY  = (HEADER_H - logoSz) / 2;
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

            _lblVoiceSolution = new Label
            {
                Text      = "Voice Solution",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(18f), FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(logoX + logoSz + (int)(8 * _scale), logoY + (logoSz / 2) - (int)(12 * _scale))
            };
            this.Controls.Add(_lblVoiceSolution);
            AttachDrag(_lblVoiceSolution);

            _lblAudioDashboard = new Label
            {
                Text      = "Audio Dashboard",
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(28f), FontStyle.Bold),
                AutoSize  = true
            };
            this.Controls.Add(_lblAudioDashboard);
            int dashW = TextRenderer.MeasureText(_lblAudioDashboard.Text, _lblAudioDashboard.Font).Width;
            int dashH = TextRenderer.MeasureText(_lblAudioDashboard.Text, _lblAudioDashboard.Font).Height;
            int dashX = (W - dashW) / 2;
            int dashY = (HEADER_H / 2) - (int)(24 * _scale);
            _lblAudioDashboard.Location = new Point(dashX, dashY);
            AttachDrag(_lblAudioDashboard);

            _lblAgentName = new Label
            {
                Text      = "Agent: " + AppSettings.Instance.AgentName,
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(14f), FontStyle.Bold),
                AutoSize  = true
            };
            this.Controls.Add(_lblAgentName);
            int agentW = TextRenderer.MeasureText(_lblAgentName.Text, _lblAgentName.Font).Width;
            int agentY = dashY + dashH + (int)(6 * _scale);
            _lblAgentName.Location = new Point((W - agentW) / 2, agentY);
            AttachDrag(_lblAgentName);

            // ── Window buttons — moved INWARD from border ─────────────────────
            int btnSz = (int)(34 * _scale);
            int btnMargin = (int)(12 * _scale);  // inset from edge
            int btnY  = btnMargin;

            _btnClose = new Button
            {
                Text      = "\u2715",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(55, 55, 55),
                Font      = new Font("Segoe UI", SF(12f), FontStyle.Bold),
                Bounds    = new Rectangle(W - btnSz - btnMargin, btnY, btnSz, btnSz),
                Cursor    = Cursors.Hand,
                TabStop   = false
            };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(180, 0, 0);
            _btnClose.Click += (s, e) => Application.Exit();
            this.Controls.Add(_btnClose);

            _btnMinimize = new Button
            {
                Text      = "\u2212",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(55, 55, 55),
                Font      = new Font("Segoe UI", SF(12f), FontStyle.Bold),
                Bounds    = new Rectangle(W - btnSz * 2 - btnMargin - (int)(6 * _scale), btnY, btnSz, btnSz),
                Cursor    = Cursors.Hand,
                TabStop   = false
            };
            _btnMinimize.FlatAppearance.BorderSize = 0;
            _btnMinimize.FlatAppearance.MouseOverBackColor = Color.FromArgb(90, 90, 90);
            _btnMinimize.Click += (s, e) => { this.WindowState = FormWindowState.Minimized; };
            this.Controls.Add(_btnMinimize);

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

        // ── Device row ────────────────────────────────────────────────────────
        private void BuildDeviceRow(int W)
        {
            int top   = HEADER_H + (int)(8 * _scale);
            int dropW = (int)(W * 0.42f);
            int labelH = (int)(28 * _scale);
            int cboH   = (int)(40 * _scale);
            int cboGap = (int)(6 * _scale);

            _lblMicLabel = new Label
            {
                Text      = "Microphone",
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(18f), FontStyle.Bold),
                AutoSize  = true
            };
            this.Controls.Add(_lblMicLabel);
            int micLblW = TextRenderer.MeasureText(_lblMicLabel.Text, _lblMicLabel.Font).Width;
            _lblMicLabel.Location = new Point(SIDE_PAD + (dropW - micLblW) / 2, top);

            _cboMic = new ComboBox
            {
                Bounds        = new Rectangle(SIDE_PAD, top + labelH + cboGap, dropW, cboH),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = ONE_BLUE_SEL,
                ForeColor     = Color.White,
                FlatStyle     = FlatStyle.Flat,
                Font          = new Font("Segoe UI", SF(16f), FontStyle.Bold)
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
            _lblHeadsetLabel = new Label
            {
                Text      = "Headset / Speaker",
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(18f), FontStyle.Bold),
                AutoSize  = true
            };
            this.Controls.Add(_lblHeadsetLabel);
            int hsLblW = TextRenderer.MeasureText(_lblHeadsetLabel.Text, _lblHeadsetLabel.Font).Width;
            _lblHeadsetLabel.Location = new Point(rightX + (dropW - hsLblW) / 2, top);

            _cboHeadset = new ComboBox
            {
                Bounds        = new Rectangle(rightX, top + labelH + cboGap, dropW, cboH),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = ONE_BLUE_SEL,
                ForeColor     = Color.White,
                FlatStyle     = FlatStyle.Flat,
                Font          = new Font("Segoe UI", SF(16f), FontStyle.Bold)
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
            int footerTop = H - FOOTER_H - (int)(10 * _scale);
            int taglineH  = (int)(30 * _scale);
            int taglineGap = (int)(10 * _scale);
            int availH    = footerTop - top - taglineH - taglineGap;

            int videoW    = (int)(W * 0.38f);
            int sideW     = (W - SIDE_PAD * 2 - VIDEO_GAP * 2 - videoW) / 2;
            if (sideW < 160) sideW = 160;
            int videoLeft = SIDE_PAD + sideW + VIDEO_GAP;

            int videoH   = (int)(availH * 0.85f);
            if (videoH < 160) videoH = 160;
            int videoTop = top + (int)(6 * _scale);

            int panelTop = top + (int)(6 * _scale);
            int panelH   = videoH;

            _videoPanel = new Panel
            {
                Bounds    = new Rectangle(videoLeft, videoTop, videoW, videoH),
                BackColor = Color.Black
            };
            this.Controls.Add(_videoPanel);

            try
            {
                int wmpExtraH = 80;
                _videoPlayer = new AxWMPLib.AxWindowsMediaPlayer
                {
                    Bounds = new Rectangle(0, 0, videoW, videoH + wmpExtraH)
                };
                _videoPanel.Controls.Add(_videoPlayer);
                _videoPanel.AutoScroll = false;
                _videoPanel.AutoSize   = false;

                string videoPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Resources", "1ONEDigitalVideo.mp4");
                if (File.Exists(videoPath))
                {
                    _videoFilePath = videoPath;
                    _wmpExtraH     = wmpExtraH;
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
                else
                {
                    Log.Warn($"[Video] File not found: {videoPath}");
                }
            }
            catch (Exception ex) { Log.Warn($"[Video] WMP failed: {ex.Message}"); }

            // Side panels
            BuildSidePanel(SIDE_PAD, panelTop, sideW, panelH, isLeft: true);
            int rightX = videoLeft + videoW + VIDEO_GAP;
            BuildSidePanel(rightX, panelTop, sideW, panelH, isLeft: false);

            // Tagline below video
            int tagY = videoTop + videoH + taglineGap;
            _lblTagline = new Label
            {
                Text      = "\u201c The Geniusness Is In The Simplicity \u201d",
                ForeColor = ONE_RED,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(16f), FontStyle.Bold),
                AutoSize  = true
            };
            this.Controls.Add(_lblTagline);
            int tagW = TextRenderer.MeasureText(_lblTagline.Text, _lblTagline.Font).Width;
            _lblTagline.Location = new Point(videoLeft + (videoW - tagW) / 2, tagY);
        }

        // ── Side panel ────────────────────────────────────────────────────────
        // Layout: Title → Subtitle → [evenly spaced: meter1 block, meter2 block] → hint
        // No Auto Level-Match button — removed (non-functional)
        private void BuildSidePanel(int x, int top, int w, int panelH, bool isLeft)
        {
            string title      = isLeft ? "AGENT AUDIO"          : "CUSTOMER OUTPUT";
            string sub        = isLeft ? "(What You Hear)"       : "(What They Hear)";
            string m1Label    = isLeft ? "Customer Voice"        : "My Voice Level";
            string m2Label    = "Script Playback";
            string volLblText = isLeft ? "Customer Voice Volume" : "My Voice Volume";

            // ── Font sizes for measurement ────────────────────────────────────
            var titleFont = new Font("Segoe UI", SF(19f), FontStyle.Bold);
            var subFont   = new Font("Segoe UI", SF(15f), FontStyle.Bold);
            var lblFont   = new Font("Segoe UI", SF(15f), FontStyle.Bold);
            var volFont   = new Font("Segoe UI", SF(14f), FontStyle.Bold);
            var hintFont  = new Font("Segoe UI", SF(12f), FontStyle.Regular);

            int titleH = TextRenderer.MeasureText(title, titleFont).Height;
            int subH   = TextRenderer.MeasureText(sub, subFont).Height;
            int lblH   = TextRenderer.MeasureText(m1Label, lblFont).Height;
            int volH   = TextRenderer.MeasureText("Volume", volFont).Height;
            int hintH  = TextRenderer.MeasureText("hint", hintFont).Height;
            int barH   = METER_H;
            int trkH   = (int)(24 * _scale);
            int smallGap = (int)(4 * _scale);

            // Fixed elements at top: title + subtitle
            // Fixed element at bottom: hint text
            // The two meter blocks go in between, evenly spaced

            // One meter block height = lblH + smallGap + barH + smallGap + volH + trkH
            int meterBlockH = lblH + smallGap + barH + smallGap + volH + trkH;

            // Title + sub at top, hint at bottom
            int headerZoneH = titleH + subH + (int)(4 * _scale);
            int footerZoneH = hintH;

            // Available space for the two meter blocks
            int availForMeters = panelH - headerZoneH - footerZoneH;
            // We want the two blocks evenly distributed in this space
            // gap = (availForMeters - 2 * meterBlockH) / 3  (top gap, middle gap, bottom gap)
            int meterGap = Math.Max((int)(8 * _scale), (availForMeters - 2 * meterBlockH) / 3);

            // ── Start laying out ──────────────────────────────────────────────
            // Title positioned right below the dropdown (top of panel)
            int cy = top;

            var lblTitle = new Label
            {
                Text      = title,
                ForeColor = ONE_RED,
                BackColor = Color.Transparent,
                Font      = titleFont,
                AutoSize  = true,
                Location  = new Point(x, cy)
            };
            this.Controls.Add(lblTitle);
            cy += titleH;

            var lblSub = new Label
            {
                Text      = sub,
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = subFont,
                AutoSize  = true,
                Location  = new Point(x, cy)
            };
            this.Controls.Add(lblSub);
            cy += subH + (int)(4 * _scale);

            // ── Meter 1 ──────────────────────────────────────────────────────
            cy += meterGap;

            var lm1 = new Label
            {
                Text      = m1Label,
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = lblFont,
                AutoSize  = true,
                Location  = new Point(x, cy)
            };
            this.Controls.Add(lm1);
            cy += lblH + smallGap;

            MakeMeterBar(x, cy, w, isLeft ? "customerVoice" : "myMicLevel");
            cy += barH + smallGap;

            // Volume label + % + slider
            var lv1 = new Label
            {
                Text      = volLblText,
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = volFont,
                AutoSize  = true,
                Location  = new Point(x, cy)
            };
            this.Controls.Add(lv1);

            int pctDefault1 = isLeft ? AppSettings.Instance.MicSystemVolume : 75;
            var lp1 = new Label
            {
                Text      = $"{pctDefault1}%",
                ForeColor = ONE_RED,
                BackColor = Color.Transparent,
                Font      = volFont,
                AutoSize  = true
            };
            this.Controls.Add(lp1);
            int pctW1 = TextRenderer.MeasureText(lp1.Text, lp1.Font).Width;
            lp1.Location = new Point(x + w - pctW1, cy);
            cy += volH;

            var trk1 = new TrackBar
            {
                Minimum   = 0,
                Maximum   = 100,
                Value     = Math.Max(0, Math.Min(100, pctDefault1)),
                TickStyle = TickStyle.None,
                Bounds    = new Rectangle(x, cy, w, trkH),
                BackColor = BG_DARK
            };
            trk1.ValueChanged += (s, e) =>
            {
                lp1.Text = $"{trk1.Value}%";
                int pw = TextRenderer.MeasureText(lp1.Text, lp1.Font).Width;
                lp1.Location = new Point(x + w - pw, lp1.Location.Y);
                try {
                    if (isLeft && _activeMicDevice != null)
                        _activeMicDevice.AudioEndpointVolume.MasterVolumeLevelScalar = trk1.Value / 100f;
                    if (!isLeft && _activeSpeakerDevice != null)
                        _activeSpeakerDevice.AudioEndpointVolume.MasterVolumeLevelScalar = trk1.Value / 100f;
                } catch { }
                if (isLeft) { AppSettings.Instance.MicSystemVolume = trk1.Value; }
                else { AppSettings.Instance.SpeakerSystemVolume = trk1.Value; }
                AppSettings.Instance.Save();
            };
            this.Controls.Add(trk1);
            if (isLeft) { _trkMicVol = trk1; _lblMicVol = lp1; }
            else { _trkSpeakerVol = trk1; _lblSpeakerVol = lp1; }
            cy += trkH;

            // ── Meter 2 ──────────────────────────────────────────────────────
            cy += meterGap;

            var lm2 = new Label
            {
                Text      = m2Label,
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = lblFont,
                AutoSize  = true,
                Location  = new Point(x, cy)
            };
            this.Controls.Add(lm2);
            cy += lblH + smallGap;

            MakeMeterBar(x, cy, w, isLeft ? "agentScript" : "customerScript");
            cy += barH + smallGap;

            var lv2 = new Label
            {
                Text      = "Script Playback Volume",
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = volFont,
                AutoSize  = true,
                Location  = new Point(x, cy)
            };
            this.Controls.Add(lv2);

            int trk2Default = isLeft
                ? (int)(AppSettings.Instance.GetVolume("agentScript", 0.48f) * 100)
                : (int)(AppSettings.Instance.GetVolume("customerScript", 0.55f) * 100);
            trk2Default = Math.Max(0, Math.Min(100, trk2Default));

            var lp2 = new Label
            {
                Text      = $"{trk2Default}%",
                ForeColor = ONE_RED,
                BackColor = Color.Transparent,
                Font      = volFont,
                AutoSize  = true
            };
            this.Controls.Add(lp2);
            int pctW2 = TextRenderer.MeasureText(lp2.Text, lp2.Font).Width;
            lp2.Location = new Point(x + w - pctW2, cy);
            cy += volH;

            var trk2 = new TrackBar
            {
                Minimum   = 0,
                Maximum   = 100,
                Value     = trk2Default,
                TickStyle = TickStyle.None,
                Bounds    = new Rectangle(x, cy, w, trkH),
                BackColor = BG_DARK
            };
            trk2.ValueChanged += (s, e) =>
            {
                lp2.Text = $"{trk2.Value}%";
                int pw = TextRenderer.MeasureText(lp2.Text, lp2.Font).Width;
                lp2.Location = new Point(x + w - pw, lp2.Location.Y);
                float vol = trk2.Value / 100f;
                if (isLeft)
                    AppSettings.Instance.SetVolume("agentScript", vol);
                else
                    AppSettings.Instance.SetVolume("customerScript", vol);
                AppSettings.Instance.Save();
                try
                {
                    using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) })
                    {
                        string ch = isLeft ? "agent" : "customer";
                        var content = new System.Net.Http.StringContent(
                            $"{{\"volume\":{trk2.Value},\"channel\":\"{ch}\"}}",
                            System.Text.Encoding.UTF8, "application/json");
                        http.PostAsync("http://localhost:9001/volume", content).ConfigureAwait(false);
                    }
                }
                catch { }
            };
            this.Controls.Add(trk2);
            if (isLeft)  { _trkAgentScriptVol    = trk2; _lblAgentScriptVol    = lp2; }
            else         { _trkCustomerScriptVol = trk2; _lblCustomerScriptVol = lp2; }
            cy += trkH;

            // ── Hint at bottom of panel ───────────────────────────────────────
            int hintY = top + panelH - hintH - (int)(4 * _scale);
            var lblHint = new Label
            {
                Text      = "Adjust sliders to control volume levels",
                ForeColor = Color.FromArgb(160, 160, 160),
                BackColor = Color.Transparent,
                Font      = hintFont,
                AutoSize  = true
            };
            this.Controls.Add(lblHint);
            int hintW = TextRenderer.MeasureText(lblHint.Text, lblHint.Font).Width;
            lblHint.Location = new Point(x + (w - hintW) / 2, hintY);
        }

        private Panel MakeMeterBar(int x, int y, int w, string key)
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
                case "myMicLevel":    _myMicMeterLeft      = panel; break;
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
                case "myMicLevel":    return _micLevel;
                case "customerVoice": return _customerVoiceLevel;
                case "agentScript":   return _agentScriptLevel;
                case "customerScript":return _customerScriptLevel;
                default: return 0f;
            }
        }

        // ── Footer ────────────────────────────────────────────────────────────
        private void BuildFooter(int W, int H)
        {
            int fy = H - FOOTER_H - (int)(8 * _scale);
            _lblFooterLeft = new Label
            {
                Text      = $"ONE United Global  \u2022  2026  \u2022  v{APP_VERSION}",
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(13f), FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(SIDE_PAD, fy + (FOOTER_H / 2) - (int)(10 * _scale))
            };
            this.Controls.Add(_lblFooterLeft);

            _lblFooterCenter = new Label
            {
                Text      = "This App Can Be Minimized  \u2022  Settings Are Auto-Saved",
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(13f), FontStyle.Bold),
                AutoSize  = true
            };
            this.Controls.Add(_lblFooterCenter);
            int fcW = TextRenderer.MeasureText(_lblFooterCenter.Text, _lblFooterCenter.Font).Width;
            _lblFooterCenter.Location = new Point((W - fcW) / 2, fy + (FOOTER_H / 2) - (int)(10 * _scale));
        }

        // ── Red border — 5px ──────────────────────────────────────────────────
        private void DrawRedBorder(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int W = this.ClientSize.Width, H = this.ClientSize.Height;
            float penW = 5f;
            float half = penW / 2f;
            using (var pen = new Pen(ONE_RED, penW))
            {
                g.DrawRectangle(pen, half, half, W - penW, H - penW);
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

        // ── Video playback ────────────────────────────────────────────────────
        private void StartVideoPlayback()
        {
            if (_videoPlayer == null || string.IsNullOrEmpty(_videoFilePath)) return;
            var t = new System.Windows.Forms.Timer { Interval = 500 };
            t.Tick += (ts, te) =>
            {
                t.Stop();
                try
                {
                    _videoPlayer.settings.volume    = 0;
                    _videoPlayer.settings.autoStart = true;
                    _videoPlayer.settings.setMode("loop", true);
                    _videoPlayer.stretchToFit       = true;
                    _videoPlayer.uiMode             = "none";
                    _videoPlayer.URL                = _videoFilePath;
                    var t2 = new System.Windows.Forms.Timer { Interval = 700 };
                    t2.Tick += (ts2, te2) =>
                    {
                        t2.Stop();
                        try
                        {
                            _videoPlayer.uiMode = "none";
                            if (_videoPlayer.playState != WMPLib.WMPPlayState.wmppsPlaying)
                                _videoPlayer.Ctlcontrols.play();
                        }
                        catch (Exception ex2) { Log.Warn("[Video] play() failed: " + ex2.Message); }
                    };
                    t2.Start();
                }
                catch (Exception ex) { Log.Warn("[Video] StartVideoPlayback failed: " + ex.Message); }
            };
            t.Start();
        }

        // ── Meter timer ───────────────────────────────────────────────────────
        // Decay is SLOW so meters stay visible during playback.
        // The MeteringSampleProvider fires every ~46ms with real peak levels;
        // this timer decays at 0.02/tick (50ms) so the bar drops smoothly
        // between updates instead of snapping to zero.
        private void SetupMeterTimer()
        {
            _meterTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _meterTimer.Tick += (s, e) =>
            {
                // Mic level — decay slowly
                if (_micLevel > 0)
                {
                    _micLevel = Math.Max(0f, _micLevel - 0.02f);
                    _myMicMeterLeft?.Invalidate();
                }

                // Customer voice level — decay slowly
                if (_customerVoiceLevel > 0)
                {
                    _customerVoiceLevel = Math.Max(0f, _customerVoiceLevel - 0.02f);
                    _customerVoiceMeter?.Invalidate();
                }

                // Script levels — decay slowly
                if (_agentScriptLevel > 0)
                {
                    _agentScriptLevel = Math.Max(0f, _agentScriptLevel - 0.02f);
                    _agentScriptMeter?.Invalidate();
                }
                if (_customerScriptLevel > 0)
                {
                    _customerScriptLevel = Math.Max(0f, _customerScriptLevel - 0.02f);
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
            }
            catch (Exception ex) { Log.Warn($"[Audio] Capture failed: {ex.Message}"); }
        }

        // ── Loopback capture for Customer Voice meter ─────────────────────────
        private void StartLoopbackCapture()
        {
            try
            {
                _loopbackCapture = new WasapiLoopbackCapture();
                _loopbackCapture.DataAvailable += (s, e) =>
                {
                    if (e.BytesRecorded < 4) return;
                    float max = 0f;
                    int stride = 4;
                    for (int i = 0; i + stride <= e.BytesRecorded; i += stride)
                    {
                        float sample = Math.Abs(BitConverter.ToSingle(e.Buffer, i));
                        if (sample > max) max = sample;
                    }
                    float level = Math.Min(1f, max * 3.5f);
                    if (level > _customerVoiceLevel)
                        _customerVoiceLevel = level;
                };
                _loopbackCapture.StartRecording();
                Log.Info("[Audio] Loopback capture started for Customer Voice meter.");
            }
            catch (Exception ex)
            {
                Log.Warn($"[Audio] Loopback capture failed: {ex.Message}");
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
                        LocalBridgeServer.Instance.SetOutputDevice(selIdx);
                        try
                        {
                            int savedPct = AppSettings.Instance.SpeakerSystemVolume;
                            _activeSpeakerDevice.AudioEndpointVolume.MasterVolumeLevelScalar = savedPct / 100f;
                            if (_trkSpeakerVol != null) { _trkSpeakerVol.Value = savedPct; if (_lblSpeakerVol != null) _lblSpeakerVol.Text = $"{savedPct}%"; }
                        }
                        catch { }
                    }
                }
                _activeVBCableDevice = rends.FirstOrDefault(d =>
                    d.FriendlyName.Contains("CABLE") || d.FriendlyName.Contains("VB-Audio"));
                if (_activeVBCableDevice != null)
                    Log.Info($"[Audio] VB-Cable output: {_activeVBCableDevice.FriendlyName}");

                _cboHeadset.SelectedIndexChanged += (s2, e2) =>
                {
                    int si = _cboHeadset.SelectedIndex;
                    if (si >= 0 && si < rends.Count)
                    {
                        _activeSpeakerDevice = rends[si];
                        LocalBridgeServer.Instance.SetOutputDevice(si);
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
                        // Only update if new level is higher (peak hold between timer ticks)
                        if (level > _agentScriptLevel)
                            _agentScriptLevel = level;
                        _agentScriptMeter?.Invalidate();
                    }
                    else
                    {
                        if (level > _customerScriptLevel)
                            _customerScriptLevel = level;
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
                };
                if (this.InvokeRequired) this.BeginInvoke(reset); else reset();
            };
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
                Log.Info("[License] Owner machine \u2014 bypassed.");
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
                    if (_lblAgentName != null)
                    {
                        _lblAgentName.Text = "Agent: " + name;
                        int agentW = TextRenderer.MeasureText(_lblAgentName.Text, _lblAgentName.Font).Width;
                        _lblAgentName.Location = new Point((this.ClientSize.Width - agentW) / 2, _lblAgentName.Location.Y);
                    }
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
            try { _loopbackCapture?.StopRecording(); } catch { }
            try { _loopbackCapture?.Dispose(); } catch { }
            _trayIcon?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
