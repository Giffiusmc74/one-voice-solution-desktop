/*
 * MainFormV5.cs  —  ONE Voice Solution v5.9
 *
 * v5.4 Fixes (Apr 2 2026 — fourth pass):
 *   - Video narrowed to ~50% of form width; side panels wider accordingly
 *   - All fonts below device row increased 2pts
 *   - More vertical separation between meter labels and meter bars
 *   - Meter + Auto Level-Match content vertically centered within each side panel
 *   - LIVE pill removed entirely
 *   - Close and minimize buttons moved down to vertical center of header, made bigger
 *   - ONE logo made bigger
 *
 * v5.3 Fix (Apr 2 2026 — third pass):
 *   - Agent name moved to same line as "Voice Solution" (beside logo, bottom-left)
 *
 * v5.2 Fixes (Apr 2 2026 — second pass):
 *   - LIVE pill moved to top-CENTER of header (was overlapping window buttons)
 *   - Subtitle text ("What You Hear" / "What They Hear") on its own row — no longer cut off
 *   - Side panels now fill all the way to footer (panelH includes tagline+gap area)
 *   - WMP control bar clipped below visible area (player bounds = panel height + 52px extra)
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
        // Sky blue from ONE logo — used for dropdown selected background
        private static readonly Color ONE_BLUE_SEL = Color.FromArgb(0, 102, 204);

        // ── Version — update this single constant for every release ──────────
        private const string APP_VERSION = "6.6";
        // Meter segment colours — inactive = same blue as dropdown, active = ONE red
        private static readonly Color SEG_OFF      = Color.FromArgb(0, 102, 204);   // same as dropdown blue
        private static readonly Color SEG_ON       = Color.FromArgb(254, 1, 1);     // ONE red
        private static readonly Color SEG_PEAK     = Color.FromArgb(255, 80, 80);   // bright red peak

        // ── Scale ─────────────────────────────────────────────────────────────
        private float _scale = 1.0f;
        private int HEADER_H     => (int)(148 * _scale);  // taller: ~half-inch extra cushion
        private int REDLINE_H    => 3;
        private int DEVICE_ROW_H => (int)(96  * _scale);
        private int FOOTER_H     => (int)(48  * _scale);  // taller: half-inch cushion
        private int SIDE_PAD     => (int)(40  * _scale);  // more cushion from edges
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
        private string _videoFilePath;
        private int    _wmpExtraH = 80;
        // Volume control — hold references to the selected MMDevices for real-time volume
        private MMDevice _activeMicDevice;
        private MMDevice _activeSpeakerDevice;
        private MMDevice _activeVBCableDevice;   // VB-Audio Cable output (customer channel)
        private TrackBar _trkMicVol;
        private TrackBar _trkSpeakerVol;
        private TrackBar _trkAgentScriptVol;     // Left panel Script Playback Volume
        private TrackBar _trkCustomerScriptVol;  // Right panel Script Playback Volume
        private Label    _lblMicVol;
        private Label    _lblSpeakerVol;
        private Label    _lblAgentScriptVol;
        private Label    _lblCustomerScriptVol;

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
            StartBridgeServer();
            if (!string.IsNullOrEmpty(agentNameOverride) && _lblAgentName != null)
                _lblAgentName.Text = "Agent: " + agentNameOverride;
            // Delay video play until form is fully shown — WMP needs handle created
            this.Shown += (s, e) =>
            {
                StartVideoPlayback();
                // Check for updates silently in the background
                AutoUpdater.CheckAndUpdate(APP_VERSION);
                // Auto-open ScriptBuilder in default browser so agent is ready to call
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://onevoiceapp-wpzvhh8c.manus.space/member/script-builder",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex) { Log.Warn($"[Startup] Could not open ScriptBuilder: {ex.Message}"); }
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
            int w = Math.Max((int)(wa.Width  * 0.88), 860);
            int h = Math.Max((int)(wa.Height * 0.88), 560);
            _scale = Math.Max(0.60f, Math.Min(Math.Min((float)w / 1280f, (float)h / 900f), 1.20f));
            this.ClientSize = new Size(w, h);
            int cx = wa.Left + (wa.Width - w) / 2;
            int cy2 = wa.Top + (wa.Height - h) / 2;
            // Ensure form is never off-screen
            if (cx < wa.Left) cx = wa.Left;
            if (cy2 < wa.Top) cy2 = wa.Top;
            this.Location   = new Point(cx, cy2);
        }

        // ── Build UI ──────────────────────────────────────────────────────────
        private void BuildUI()
        {
            int W = this.ClientSize.Width;
            int H = this.ClientSize.Height;
            this.Paint += (s, e) => DrawRedBorder(e.Graphics);
            BuildHeader(W);
            // Separator red line below header
            _redLine = new Panel { BackColor = ONE_RED, Bounds = new Rectangle(0, HEADER_H, W, REDLINE_H) };
            this.Controls.Add(_redLine);
            BuildDeviceRow(W);
            int contentTop = HEADER_H + REDLINE_H + DEVICE_ROW_H + (int)(4 * _scale);
            BuildContentArea(W, H, contentTop);
            // Top red border — added LAST so it's never covered by any other control
            var topLine = new Panel { BackColor = ONE_RED, Bounds = new Rectangle(0, 0, W, REDLINE_H) };
            this.Controls.Add(topLine);
            topLine.BringToFront();
            BuildFooter(W, H);
        }

        // ── Header ────────────────────────────────────────────────────────────
        private void BuildHeader(int W)
        {
            // Logo — large, vertically centered in header
            int logoSz = (int)(160 * _scale);  // bigger logo
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

            // "Voice Solution" — sits BESIDE the logo (to the right), vertically centered
            int vsGap = (int)(10 * _scale);   // gap between logo right edge and text
            int vsX   = logoX + logoSz + vsGap;
            int vsW   = (int)(220 * _scale);
            int vsH   = (int)(32 * _scale);
            int vsY   = logoY + (logoSz - vsH) / 2;  // vertically centered with logo
            _lblVoiceSolution = new Label
            {
                Text      = "Voice Solution",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(20f), FontStyle.Bold),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds    = new Rectangle(vsX, vsY, vsW, vsH)
            };
            this.Controls.Add(_lblVoiceSolution);
            AttachDrag(_lblVoiceSolution);

            // "Audio Dashboard" — centred horizontally, vertically centered in header
            int dashH = (int)(52 * _scale);
            int dashY = (HEADER_H - dashH) / 2 - (int)(10 * _scale);
            _lblAudioDashboard = new Label
            {
                Text      = "Audio Dashboard",
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(30f), FontStyle.Bold),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(0, dashY, W, dashH)
            };
            this.Controls.Add(_lblAudioDashboard);
            AttachDrag(_lblAudioDashboard);

            // "Agent: Name" — centered below "Audio Dashboard", with clear gap
            int agentH = (int)(26 * _scale);
            int agentY = dashY + dashH + (int)(24 * _scale);
            _lblAgentName = new Label
            {
                Text      = "Agent: " + AppSettings.Instance.AgentName,
                ForeColor = TEXT_GREY,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(15f), FontStyle.Bold),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(0, agentY, W, agentH)
            };
            this.Controls.Add(_lblAgentName);
            AttachDrag(_lblAgentName);

            // Window buttons — top-right corner, fully visible (no cutoff)
            int btnSz = (int)(38 * _scale);
            int btnY  = (int)(6 * _scale);
            _btnClose = new Button
            {
                Text      = "✕",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(55, 55, 55),
                Font      = new Font("Segoe UI", SF(13f), FontStyle.Bold),
                Bounds    = new Rectangle(W - btnSz - 6, btnY, btnSz, btnSz),
                Cursor    = Cursors.Hand,
                TabStop   = false
            };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(180, 0, 0);
            _btnClose.Click += (s, e) => Application.Exit();
            this.Controls.Add(_btnClose);

            _btnMinimize = new Button
            {
                Text      = "−",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(55, 55, 55),
                Font      = new Font("Segoe UI", SF(13f), FontStyle.Bold),
                Bounds    = new Rectangle(W - btnSz * 2 - 14, btnY, btnSz, btnSz),
                Cursor    = Cursors.Hand,
                TabStop   = false
            };
            _btnMinimize.FlatAppearance.BorderSize = 0;
            _btnMinimize.FlatAppearance.MouseOverBackColor = Color.FromArgb(90, 90, 90);
            _btnMinimize.Click += (s, e) => { this.WindowState = FormWindowState.Minimized; };
            this.Controls.Add(_btnMinimize);
            // LIVE pill removed (v5.4)

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
            int top   = HEADER_H + REDLINE_H + (int)(18 * _scale);
            // Widen dropdowns — each takes ~45% of form width so they feel substantial
            int dropW = (int)(W * 0.44f);
            int labelH = (int)(32 * _scale);
            int cboH   = (int)(44 * _scale);
            int cboGap = (int)(12 * _scale);  // gap between label and dropdown

            // Microphone label — bold, 20pt (3pts bigger than before)
            _lblMicLabel = MakeLabel("Microphone", SIDE_PAD, top, 20, bold: true, color: TEXT_WHITE);
            _cboMic = new ComboBox
            {
                Bounds        = new Rectangle(SIDE_PAD, top + labelH + cboGap, dropW, cboH),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = ONE_BLUE_SEL,   // starts blue (no device selected yet)
                ForeColor     = Color.White,
                FlatStyle     = FlatStyle.Flat,
                Font          = new Font("Segoe UI", SF(18f), FontStyle.Bold)
            };
            _cboMic.SelectedIndexChanged += (s, e) =>
            {
                _cboMic.BackColor = ONE_BLUE_SEL;  // stays blue permanently
                _cboMic.ForeColor = Color.White;
                AppSettings.Instance.MicDevice = _cboMic.Text;
                AppSettings.Instance.Save();
                StartAudioCapture(_cboMic.Text);
            };
            this.Controls.Add(_cboMic);

            int rightX = W - SIDE_PAD - dropW;
            // Headset/Speaker label — bold, 20pt
            _lblHeadsetLabel = MakeLabel("Headset / Speaker", rightX, top, 20, bold: true, color: TEXT_WHITE);
            _cboHeadset = new ComboBox
            {
                Bounds        = new Rectangle(rightX, top + labelH + cboGap, dropW, cboH),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = ONE_BLUE_SEL,   // starts blue
                ForeColor     = Color.White,
                FlatStyle     = FlatStyle.Flat,
                Font          = new Font("Segoe UI", SF(18f), FontStyle.Bold)
            };
            _cboHeadset.SelectedIndexChanged += (s, e) =>
            {
                _cboHeadset.BackColor = ONE_BLUE_SEL;  // stays blue permanently
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
            int taglineGap = (int)(20 * _scale);
            int availH     = H - top - taglineH - taglineGap - FOOTER_H;

            // Video ~38% of form width; side panels get the rest
            int videoW    = (int)(W * 0.38f);
            int sideW     = (W - SIDE_PAD * 2 - VIDEO_GAP * 2 - videoW) / 2;
            if (sideW < 200) sideW = 200;
            int videoLeft = SIDE_PAD + sideW + VIDEO_GAP;

            // Video is ~2/3 of available height, vertically centered
            int videoH   = (int)(availH * 0.80f);
            if (videoH < 200) videoH = 200;
            int videoTop = top + (int)(8 * _scale);  // pin near top, no dead space

            // Side panels match video height exactly to prevent badge overlap
            int panelTop = top + (int)(8 * _scale);
            int panelH   = videoH;  // match video height so badges align

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
                // WMP chrome bar is ~80px tall — make the control taller than the panel
                // so the chrome bar is clipped below the visible area
                int wmpExtraH = 80;
                _videoPlayer = new AxWMPLib.AxWindowsMediaPlayer
                {
                    Bounds = new Rectangle(0, 0, videoW, videoH + wmpExtraH)
                };
                _videoPanel.Controls.Add(_videoPlayer);
                _videoPanel.AutoScroll = false;
                _videoPanel.AutoSize   = false;
                // NOTE: do NOT call CreateControl() manually — WinForms handles it
                // Setting uiMode here causes a COM exception before the handle exists;
                // defer all WMP config to StartVideoPlayback() called from Shown event.

                string videoPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Resources", "1ONEDigitalVideo.mp4");
                if (File.Exists(videoPath))
                {
                    _videoFilePath = videoPath;
                    _wmpExtraH     = wmpExtraH;
                    // Loop: when playback stops (state 1), restart
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

            // Side panels — aligned exactly with video top/bottom
            BuildMeterPanel(SIDE_PAD, panelTop, sideW, panelH, isLeft: true);
            int rightX = videoLeft + videoW + VIDEO_GAP;
            BuildMeterPanel(rightX, panelTop, sideW, panelH, isLeft: false);

            // Tagline directly below the video panel
            int tagY = videoTop + videoH + taglineGap;
            _lblTagline = new Label
            {
                Text      = "\u201c The Geniusness Is In The Simplicity \u201d",
                ForeColor = ONE_RED,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(18f), FontStyle.Bold),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(videoLeft, tagY, videoW, taglineH)
            };
            this.Controls.Add(_lblTagline);
        }

        // ── Meter panel ───────────────────────────────────────────────────────
        private void BuildMeterPanel(int x, int top, int w, int panelH, bool isLeft)
        {
            // LEFT=AGENT AUDIO: what agent hears (Customer Voice + Script Playback)
            // RIGHT=CUSTOMER OUTPUT: what customer hears (My Voice Level + Script Playback)
            string title      = isLeft ? "AGENT AUDIO"          : "CUSTOMER OUTPUT";
            string sub        = isLeft ? "(What You Hear)"       : "(What They Hear)";
            string m1Label    = isLeft ? "Customer Voice"        : "My Voice Level";
            string m2Label    = "Script Playback";
            string volLblText = isLeft ? "Customer Voice Volume" : "Recordings";

            // ── Sizes ──────────────────────────────────────────────────────────
            int titleH   = (int)(36 * _scale);
            int subH     = (int)(22 * _scale);
            int gap1     = (int)(22 * _scale);  // title block -> meter 1
            int lbl1H    = (int)(30 * _scale);
            int lblBarGap= (int)(10 * _scale);  // label -> bar
            int barH     = METER_H;
            int volLblH  = (int)(28 * _scale);
            int trkH     = (int)(32 * _scale);
            int volGap   = (int)(12 * _scale);  // bar -> vol label
            int betweenH = (int)(36 * _scale);  // slider -> meter 2 label
            int badgeGap = (int)(16 * _scale);  // bar2 slider -> badge
            int hintH    = (int)(24 * _scale);
            int hintGap  = (int)(8  * _scale);

            // Total content height
            int contentH = titleH + subH + gap1
                         + lbl1H + lblBarGap + barH + volGap + volLblH + trkH   // meter 1 + vol
                         + betweenH
                         + lbl1H + lblBarGap + barH + volGap + volLblH + trkH   // meter 2 + vol
                         + badgeGap + BADGE_H + hintGap + hintH;

            // Vertically center the content block within the panel
            int cy = top + Math.Max((int)(30 * _scale), (panelH - contentH) / 2);

            // ── Title ─────────────────────────────────────────────────────────
            var lblTitle = new Label
            {
                Text      = title,
                ForeColor = ONE_RED,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(21f), FontStyle.Bold),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds    = new Rectangle(x, cy, w, titleH)
            };
            this.Controls.Add(lblTitle);

            // ── Subtitle ──────────────────────────────────────────────────────
            int subY = cy + titleH;
            var lblSub = new Label
            {
                Text      = sub,
                ForeColor = TEXT_GREY,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(17f), FontStyle.Bold),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds    = new Rectangle(x, subY, w, subH)
            };
            this.Controls.Add(lblSub);

            // ── Meter 1: label + bar + volume slider directly under ────────────
            int m1LabelY = subY + subH + gap1;
            MakeLabel(m1Label, x, m1LabelY, 17, color: TEXT_WHITE);
            int m1BarY = m1LabelY + lbl1H + lblBarGap;
            MakeMeterPanel(x, m1BarY, w, isLeft ? "customerVoice" : "myMicLevel");

            // Vol label + % + slider directly under meter 1
            int v1LblY = m1BarY + barH + volGap;
            var lv1 = new Label { Text = volLblText, ForeColor = TEXT_WHITE, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", SF(16f), FontStyle.Bold), AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(x, v1LblY, w - (int)(60 * _scale), volLblH) };
            this.Controls.Add(lv1);
            var lp1 = new Label { Text = "75%", ForeColor = ONE_RED, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", SF(16f), FontStyle.Bold), AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Bounds = new Rectangle(x + w - (int)(65 * _scale), v1LblY, (int)(65 * _scale), volLblH) };
            this.Controls.Add(lp1);
            int t1Y = v1LblY + volLblH;
            var trk1 = new TrackBar { Minimum = 0, Maximum = 100, Value = 75,
                TickStyle = TickStyle.None, Bounds = new Rectangle(x, t1Y, w, trkH), BackColor = BG_DARK };
            trk1.ValueChanged += (s, e) =>
            {
                lp1.Text = $"{trk1.Value}%";
                try {
                    if (isLeft && _activeMicDevice != null)
                        _activeMicDevice.AudioEndpointVolume.MasterVolumeLevelScalar = trk1.Value / 100f;
                } catch { }
                if (isLeft) { AppSettings.Instance.MicSystemVolume = trk1.Value; AppSettings.Instance.Save(); }
            };
            this.Controls.Add(trk1);
            if (isLeft) { _trkMicVol = trk1; _lblMicVol = lp1; }

            // ── Meter 2: label + bar + volume slider directly under ────────────
            int m2LabelY = t1Y + trkH + betweenH;
            MakeLabel(m2Label, x, m2LabelY, 17, color: TEXT_WHITE);
            int m2BarY = m2LabelY + lbl1H + lblBarGap;
            MakeMeterPanel(x, m2BarY, w, isLeft ? "agentScript" : "customerScript");

            // Vol label + % + slider directly under meter 2
            int v2LblY = m2BarY + barH + volGap;
            var lv2 = new Label { Text = isLeft ? "Script Playback Volume" : "Script Playback Volume", ForeColor = TEXT_WHITE, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", SF(16f), FontStyle.Bold), AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(x, v2LblY, w - (int)(60 * _scale), volLblH) };
            this.Controls.Add(lv2);
            var lp2 = new Label { Text = "100%", ForeColor = ONE_RED, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", SF(16f), FontStyle.Bold), AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Bounds = new Rectangle(x + w - (int)(65 * _scale), v2LblY, (int)(65 * _scale), volLblH) };
            this.Controls.Add(lp2);
            int t2Y = v2LblY + volLblH;
            int trk2Default = isLeft
                ? (int)(AppSettings.Instance.GetVolume("agentScript",    0.48f) * 100)
                : (int)(AppSettings.Instance.GetVolume("customerScript", 0.55f) * 100);
            var trk2 = new TrackBar { Minimum = 0, Maximum = 100, Value = Math.Max(0, Math.Min(100, trk2Default)),
                TickStyle = TickStyle.None, Bounds = new Rectangle(x, t2Y, w, trkH), BackColor = BG_DARK };
            lp2.Text = $"{trk2.Value}%";
            trk2.ValueChanged += (s, e) =>
            {
                lp2.Text = $"{trk2.Value}%";
                float vol = trk2.Value / 100f;
                if (isLeft)
                {
                    // Left panel = AGENT AUDIO: Script Playback heard by agent
                    // Route to the agent's headset/speaker device
                    try { if (_activeSpeakerDevice != null) _activeSpeakerDevice.AudioEndpointVolume.MasterVolumeLevelScalar = vol; } catch { }
                    AppSettings.Instance.SetVolume("agentScript", vol);
                    AppSettings.Instance.Save();
                }
                else
                {
                    // Right panel = CUSTOMER OUTPUT: Script Playback heard by customer
                    // Route to the VB-Audio virtual cable (customer channel)
                    try { if (_activeVBCableDevice != null) _activeVBCableDevice.AudioEndpointVolume.MasterVolumeLevelScalar = vol; } catch { }
                    AppSettings.Instance.SetVolume("customerScript", vol);
                    AppSettings.Instance.Save();
                }
            };
            this.Controls.Add(trk2);
            if (isLeft)  { _trkAgentScriptVol    = trk2; _lblAgentScriptVol    = lp2; }
            if (!isLeft) { _trkCustomerScriptVol = trk2; _lblCustomerScriptVol = lp2; }

            // ── Auto Level-Match badge ─────────────────────────────────────────
            int badgeTop = t2Y + trkH + badgeGap;
            var btnBadge = new Button
            {
                Text      = "\u25cf Auto Level-Match: ON",
                ForeColor = Color.White,
                BackColor = ONE_RED,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", SF(17f), FontStyle.Bold),
                Bounds    = new Rectangle(x, badgeTop, w, BADGE_H),
                Cursor    = Cursors.Hand
            };
            btnBadge.FlatAppearance.BorderSize = 0;
            btnBadge.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 0, 0);
            btnBadge.Click += (s, e) => ToggleAutoLevel(isLeft, btnBadge);
            this.Controls.Add(btnBadge);
            if (isLeft) _btnAgentAutoLevel = btnBadge; else _btnCustomerAutoLevel = btnBadge;

            // ── Hint ──────────────────────────────────────────────────────────
            int hintY = badgeTop + BADGE_H + hintGap;
            var lblHint = new Label
            {
                Text      = "Tap to switch between manual / automatic",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(13f), FontStyle.Regular),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(x, hintY, w, hintH)
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
                        Text      = $"ONE United Global  \u2022  2026  \u2022  v{APP_VERSION}",
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(14f), FontStyle.Bold),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds    = new Rectangle(SIDE_PAD, fy, 320, FOOTER_H)
            };
            this.Controls.Add(_lblFooterLeft);

            _lblFooterCenter = new Label
            {
                   Text      = "This App Can Be Minimized  •  Settings Are Auto-Saved",
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(14f), FontStyle.Bold),
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
            {
                // Left border
                g.DrawLine(pen, 1, 0, 1, H - 1);
                // Right border
                g.DrawLine(pen, W - 2, 0, W - 2, H - 1);
                // Bottom border
                g.DrawLine(pen, 0, H - 2, W, H - 2);
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

          // ── Video playback (deferred until form Shown) ────────────────────
        private void StartVideoPlayback()
        {
            if (_videoPlayer == null || string.IsNullOrEmpty(_videoFilePath)) return;

            // Use a timer so the WMP ActiveX control handle is fully created
            // before we touch any COM properties (avoids black screen / COM errors)
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

                    // After another short delay, ensure uiMode stayed "none" and press play
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
                _activeMicDevice = device;  // store for volume slider
                // Restore saved volume, or fall back to current system volume
                if (_trkMicVol != null)
                {
                    try
                    {
                        int savedPct = AppSettings.Instance.MicSystemVolume;
                        // Apply saved volume to device
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
                var rends = _deviceEnum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
                foreach (var d in rends) _cboHeadset.Items.Add(d.FriendlyName);
                if (_cboHeadset.Items.Count > 0)
                {
                    int idx = _cboHeadset.Items.IndexOf(AppSettings.Instance.HeadsetDevice);
                    _cboHeadset.SelectedIndex = idx >= 0 ? idx : 0;
                    // Blue bg immediately on load
                    _cboHeadset.BackColor = ONE_BLUE_SEL;
                    _cboHeadset.ForeColor = Color.White;
                    // Store active speaker device and restore saved volume
                    int selIdx = _cboHeadset.SelectedIndex;
                    if (selIdx >= 0 && selIdx < rends.Count)
                    {
                        _activeSpeakerDevice = rends[selIdx];
                        try
                        {
                            int savedPct = AppSettings.Instance.SpeakerSystemVolume;
                            _activeSpeakerDevice.AudioEndpointVolume.MasterVolumeLevelScalar = savedPct / 100f;
                            if (_trkSpeakerVol != null) { _trkSpeakerVol.Value = savedPct; if (_lblSpeakerVol != null) _lblSpeakerVol.Text = $"{savedPct}%"; }
                        }
                        catch { }
                    }
                }
                // Detect VB-Audio Cable output device for customer script channel
                _activeVBCableDevice = rends.FirstOrDefault(d =>
                    d.FriendlyName.Contains("CABLE") || d.FriendlyName.Contains("VB-Audio"));
                if (_activeVBCableDevice != null)
                    Log.Info($"[Audio] VB-Cable output: {_activeVBCableDevice.FriendlyName}");

                // Update speaker device when user changes selection
                _cboHeadset.SelectedIndexChanged += (s2, e2) =>
                {
                    int si = _cboHeadset.SelectedIndex;
                    if (si >= 0 && si < rends.Count)
                    {
                        _activeSpeakerDevice = rends[si];
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
            // Wire playback level → script meter bars
            bridge.OnPlaybackLevel += (level, channel) =>
            {
                if (this.IsDisposed) return;
                Action update = () =>
                {
                    if (channel == "agent")
                    {
                        _agentScriptLevel = level;
                        _agentScriptMeter?.Invalidate();
                    }
                    else
                    {
                        _customerScriptLevel = level;
                        _customerScriptMeter?.Invalidate();
                    }
                };
                if (this.InvokeRequired) this.BeginInvoke(update); else update();
            };
            // Wire stop → zero out meters
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
            // Always Bold, never Italic — consistent with all other labels
            var lbl = new Label
            {
                Text      = text,
                ForeColor = color ?? TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", SF(size), FontStyle.Bold),
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
            LocalBridgeServer.Instance.Stop();
            _meterTimer?.Stop();
            _livePulseTimer?.Stop();
            _micCapture?.StopRecording();
            _micCapture?.Dispose();
            _trayIcon?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
