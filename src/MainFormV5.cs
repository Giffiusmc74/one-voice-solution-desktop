/**
 * MainFormV5.cs
 * ONE Voice Solution v5.0 — Main Application Window
 *
 * Layout (matches approved mockup v5):
 *   ┌─────────────────────────────────────────────────────────────────────┐
 *   │  [ONE Logo / Agency Logo]  Voice Solution  [LIVE • Connected pill]  │  ← Header
 *   │                            Audio Control Panel                      │
 *   │                            Agent: {name}                            │
 *   ├─────────────────────────────────────────────────────────────────────┤  ← Red line
 *   │  [Microphone ▾]                                [Headset/Speaker ▾]  │  ← Device row
 *   │                                                                     │
 *   │  AGENT AUDIO      [── Video ──]      CUSTOMER OUTPUT               │
 *   │  Customer Voice   [           ]      My Mic Level                  │
 *   │  [meter ◄──────►] [           ] [meter ◄──────►]                  │
 *   │  Script Playback  [           ]      Script Playback               │
 *   │  [meter ◄──────►] [           ] [meter ◄──────►]                  │
 *   │  [Auto Level-Match: ON]       [Auto Level-Match: ON]               │
 *   │  [Audio Settings]             [Test Microphone]                    │
 *   │                                                                     │
 *   │           "The Geniusness Is In The Simplicity"                    │  ← Red tagline
 *   ├─────────────────────────────────────────────────────────────────────┤
 *   │  ONE United Global 2026 v5.0    This Desktop App Can Be Minimized  │  ← Footer
 *   └─────────────────────────────────────────────────────────────────────┘
 *
 * Features:
 *   - Centered on launch, moveable, multi-monitor aware
 *   - 1.5-inch margin from screen edges
 *   - Minimizes to system tray (ONE logo icon)
 *   - Auto-saves all settings on change
 *   - Agency logo swap from portal
 *   - Looping ONE Digital Video in center panel
 *   - Blue→Red gradient meters with drag-thumb sliders
 *   - Heartbeat service integration
 */
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NLog;
using System;
using System.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WindowsFormsApp1.src;

namespace WindowsFormsApp1
{
    public partial class MainFormV5 : Form
    {
        // ── Constants ─────────────────────────────────────────────────────────
        private static readonly Color ONE_RED   = Color.FromArgb(254, 1, 1);
        private static readonly Color BG_DARK   = Color.FromArgb(18, 18, 18);
        private static readonly Color BG_PANEL  = Color.FromArgb(28, 28, 28);
        private static readonly Color TEXT_WHITE = Color.White;
        private static readonly Color TEXT_GREY  = Color.FromArgb(180, 180, 180);

        private const int HEADER_H     = 90;
        private const int REDLINE_H    = 3;
        private const int DEVICE_ROW_H = 50;
        private const int FOOTER_H     = 44;
        private const int SIDE_PAD     = 32;
        private const int PANEL_W      = 280;
        private const int VIDEO_W      = 380;
        private const int VIDEO_H      = 280;
        private const int METER_H      = 28;
        private const int METER_GAP    = 18;
        private const int BTN_H        = 40;
        private const int BADGE_H      = 30;

        // ── Logger ────────────────────────────────────────────────────────────
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        // ── Audio ─────────────────────────────────────────────────────────────
        private WasapiCapture    _micCapture;
        private WaveInEvent      _waveIn;
        private MMDeviceEnumerator _deviceEnum = new MMDeviceEnumerator();

        // Meter levels (0.0 – 1.0)
        private float _agentVoiceLevel    = 0f;
        private float _agentScriptLevel   = 0f;
        private float _customerVoiceLevel = 0f;
        private float _customerScriptLevel = 0f;

        // Slider positions (0.0 – 1.0)
        private float _agentVoiceSlider    = 0.62f;
        private float _agentScriptSlider   = 0.48f;
        private float _customerVoiceSlider = 0.55f;
        private float _customerScriptSlider = 0.55f;

        // Auto Level-Match state
        private bool _agentAutoLevel    = true;
        private bool _customerAutoLevel = true;

        // ── UI Controls ───────────────────────────────────────────────────────
        private PictureBox       _logoBox;
        private Label            _lblVoiceSolution;
        private Label            _lblAudioControlPanel;
        private Label            _lblAgentName;
        private Panel            _livePill;
        private Label            _lblLive;
        private Panel            _redLine;
        private ComboBox         _cboMic;
        private ComboBox         _cboHeadset;
        private Label            _lblMicLabel;
        private Label            _lblHeadsetLabel;
        private Panel            _videoPanel;
        private AxWMPLib.AxWindowsMediaPlayer _videoPlayer;
        private Panel            _leftPanel;
        private Panel            _rightPanel;
        private Label            _lblAgentAudio;
        private Label            _lblCustomerOutput;
        private Panel            _agentVoiceMeterPanel;
        private Panel            _agentScriptMeterPanel;
        private Panel            _customerVoiceMeterPanel;
        private Panel            _customerScriptMeterPanel;
        private Button           _btnAgentAutoLevel;
        private Button           _btnCustomerAutoLevel;
        private Button           _btnAudioSettings;
        private Button           _btnTestMic;
        private Label            _lblTagline;
        private Label            _lblFooterLeft;
        private Label            _lblFooterCenter;
        private NotifyIcon       _trayIcon;
        private ContextMenuStrip _trayMenu;
        private System.Windows.Forms.Timer _meterTimer;
        private System.Windows.Forms.Timer _livePulseTimer;
        private bool _livePulseState = true;

        // ── Dragging slider state ─────────────────────────────────────────────
        private Panel  _draggingMeter;
        private string _draggingKey;

        // ── Constructor ───────────────────────────────────────────────────────
        public MainFormV5()
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
        }

        // ── Form initialization ───────────────────────────────────────────────
        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Text            = "ONE Voice Solution";
            this.BackColor       = BG_DARK;
            this.ForeColor       = TEXT_WHITE;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox     = false;
            this.MinimizeBox     = true;
            this.StartPosition   = FormStartPosition.Manual;
            this.ShowInTaskbar   = true;
            this.DoubleBuffered  = true;

            // Set ONE logo as window icon
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "one_logo.ico");
            if (File.Exists(iconPath))
                this.Icon = new Icon(iconPath);

            // Size and center with 1.5-inch margin
            CenterWithMargin();

            this.ResumeLayout(false);
        }

        private void CenterWithMargin()
        {
            Screen screen = Screen.FromPoint(Cursor.Position);
            Rectangle wa = screen.WorkingArea;
            int dpi = GetDpi();
            int margin = (int)(1.5 * dpi); // 1.5 inches in pixels

            int w = wa.Width  - (margin * 2);
            int h = wa.Height - (margin * 2);

            // Clamp to a reasonable minimum
            w = Math.Max(w, 900);
            h = Math.Max(h, 620);

            this.ClientSize = new Size(w, h);
            this.Location   = new Point(
                wa.Left + (wa.Width  - w) / 2,
                wa.Top  + (wa.Height - h) / 2);
        }

        private int GetDpi()
        {
            using (Graphics g = this.CreateGraphics())
                return (int)g.DpiX;
        }

        // ── Build UI ──────────────────────────────────────────────────────────
        private void BuildUI()
        {
            int W = this.ClientSize.Width;
            int H = this.ClientSize.Height;

            // ── Red outer border ──────────────────────────────────────────────
            this.Paint += (s, e) => DrawRedBorder(e.Graphics);

            // ── Header ────────────────────────────────────────────────────────
            BuildHeader(W);

            // ── Red separator line ────────────────────────────────────────────
            _redLine = new Panel
            {
                BackColor = ONE_RED,
                Bounds    = new Rectangle(0, HEADER_H, W, REDLINE_H)
            };
            this.Controls.Add(_redLine);

            // ── Device selectors ──────────────────────────────────────────────
            BuildDeviceRow(W);

            // ── Main content area ─────────────────────────────────────────────
            int contentTop = HEADER_H + REDLINE_H + DEVICE_ROW_H + 10;
            BuildContentArea(W, H, contentTop);

            // ── Footer ────────────────────────────────────────────────────────
            BuildFooter(W, H);
        }

        private void BuildHeader(int W)
        {
            // Logo
            _logoBox = new PictureBox
            {
                Bounds   = new Rectangle(SIDE_PAD, 12, 140, 66),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            LoadLogo();
            this.Controls.Add(_logoBox);

            // "Voice Solution" sits beneath the ONE logo
            _lblVoiceSolution = new Label
            {
                Text      = "Voice Solution",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 13f, FontStyle.Regular),
                AutoSize  = true,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(SIDE_PAD, 78, 140, 18)
            };
            this.Controls.Add(_lblVoiceSolution);

            // "Audio Dashboard" — centered across the full header width, large
            _lblAudioControlPanel = new Label
            {
                Text      = "Audio Dashboard",
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 30f, FontStyle.Bold),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(0, 10, W, 44)
            };
            this.Controls.Add(_lblAudioControlPanel);

            // Agent name — centered below Audio Dashboard
            _lblAgentName = new Label
            {
                Text      = "Agent: " + AppSettings.Instance.AgentName,
                ForeColor = TEXT_GREY,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 16f, FontStyle.Regular),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(0, 52, W, 28)
            };
            this.Controls.Add(_lblAgentName);

            // LIVE pill (right side)
            _livePill = new Panel
            {
                Bounds    = new Rectangle(W - 200, (HEADER_H - 36) / 2, 168, 36),
                BackColor = ONE_RED
            };
            _livePill.Paint += DrawLivePill;
            this.Controls.Add(_livePill);
        }

        private void DrawLivePill(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var p = (Panel)sender;
            var r = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
            using (var path = RoundedRect(r, 18))
            using (var brush = new SolidBrush(ONE_RED))
            {
                g.FillPath(brush, path);
            }
            // Dot
            bool pulse = _livePulseState;
            using (var dot = new SolidBrush(pulse ? Color.White : Color.FromArgb(180, 255, 255, 255)))
                g.FillEllipse(dot, 12, (p.Height - 10) / 2, 10, 10);
            // Text
            using (var font = new Font("Segoe UI", 14f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                g.DrawString("  LIVE  •  Connected", font, brush, new RectangleF(22, 0, p.Width - 22, p.Height), sf);
            }
        }

        private void BuildDeviceRow(int W)
        {
            int top = HEADER_H + REDLINE_H + 8;
            int dropW = (W - SIDE_PAD * 2 - 40) / 2;

            _lblMicLabel = MakeLabel("Microphone", SIDE_PAD, top, 14, color: TEXT_GREY);
            _cboMic = new ComboBox
            {
                Bounds        = new Rectangle(SIDE_PAD, top + 16, dropW, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = BG_PANEL,
                ForeColor     = TEXT_WHITE,
                FlatStyle     = FlatStyle.Flat,
                Font          = new Font("Segoe UI", 13f)
            };
            _cboMic.SelectedIndexChanged += (s, e) => { AppSettings.Instance.MicDevice = _cboMic.Text; AppSettings.Instance.Save(); };
            this.Controls.Add(_cboMic);

            int rightX = W - SIDE_PAD - dropW;
            _lblHeadsetLabel = MakeLabel("Headset / Speaker", rightX, top, 14, color: TEXT_GREY);
            _cboHeadset = new ComboBox
            {
                Bounds        = new Rectangle(rightX, top + 16, dropW, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = BG_PANEL,
                ForeColor     = TEXT_WHITE,
                FlatStyle     = FlatStyle.Flat,
                Font          = new Font("Segoe UI", 13f)
            };
            _cboHeadset.SelectedIndexChanged += (s, e) => { AppSettings.Instance.HeadsetDevice = _cboHeadset.Text; AppSettings.Instance.Save(); };
            this.Controls.Add(_cboHeadset);
        }

        private void BuildContentArea(int W, int H, int top)
        {
            int videoLeft = (W - VIDEO_W) / 2;
            int videoTop  = top + 30;

            // ── Video panel ───────────────────────────────────────────────────
            _videoPanel = new Panel
            {
                Bounds    = new Rectangle(videoLeft, videoTop, VIDEO_W, VIDEO_H),
                BackColor = Color.Black
            };
            _videoPanel.Paint += DrawVideoRedBorder;
            this.Controls.Add(_videoPanel);

            // Windows Media Player for looping video
            try
            {
                _videoPlayer = new AxWMPLib.AxWindowsMediaPlayer
                {
                    Bounds = new Rectangle(2, 2, VIDEO_W - 4, VIDEO_H - 4)
                };
                _videoPanel.Controls.Add(_videoPlayer);
                _videoPlayer.CreateControl();
                string videoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "1ONEDigitalVideo.mp4");
                if (File.Exists(videoPath))
                {
                    _videoPlayer.settings.setMode("loop", true);
                    _videoPlayer.settings.volume = 0; // muted — visual only
                    _videoPlayer.URL = videoPath;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[Video] Could not load WMP: {ex.Message}");
            }

            // ── Left panel (AGENT AUDIO) ───────────────────────────────────────
            int lx = SIDE_PAD;
            int lw = videoLeft - SIDE_PAD - 16;
            BuildMeterPanel(lx, videoTop, lw, H, isLeft: true);

            // ── Right panel (CUSTOMER OUTPUT) ─────────────────────────────────
            int rx = videoLeft + VIDEO_W + 16;
            int rw = W - rx - SIDE_PAD;
            BuildMeterPanel(rx, videoTop, rw, H, isLeft: false);

            // ── Tagline ───────────────────────────────────────────────────────
            int taglineY = videoTop + VIDEO_H + 16;
            _lblTagline = new Label
            {
                Text      = "The Geniusness Is In The Simplicity",
                ForeColor = ONE_RED,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 28f, FontStyle.Bold),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(0, taglineY, W, 40)
            };
            this.Controls.Add(_lblTagline);
        }

        private void BuildMeterPanel(int x, int top, int w, int formH, bool isLeft)
        {
            // Section label
            string sectionLabel = isLeft ? "AGENT AUDIO" : "CUSTOMER OUTPUT";
            string subLabel     = isLeft ? "(What You Hear)" : "(What They Hear)";
            var lbl = new Label
            {
                Text      = sectionLabel,
                ForeColor = ONE_RED,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 16f, FontStyle.Bold),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds    = new Rectangle(x, top, w, 24)
            };
            this.Controls.Add(lbl);
            var lblSub = new Label
            {
                Text      = subLabel,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 14f, FontStyle.Regular),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds    = new Rectangle(x, top + 26, w, 20)
            };
            this.Controls.Add(lblSub);

            // Meter 1
             string m1Label = isLeft ? "Customer Voice" : "My Mic Level";
            int m1Top = top + 52;
            var lbl1 = MakeLabel(m1Label, x, m1Top, 14, color: TEXT_WHITE);
            var meter1 = BuildMeterControl(x, m1Top + 20, w, isLeft ? "agentVoice" : "customerVoice");
            // Meter 2
            string m2Label = "Script Playback";
            int m2Top = m1Top + 20 + METER_H + METER_GAP;
            var lbl2 = MakeLabel(m2Label, x, m2Top, 14, color: TEXT_WHITE);
            var meter2 = BuildMeterControl(x, m2Top + 18, w, isLeft ? "agentScript" : "customerScript");

            // Auto Level-Match badge
            int badgeTop = m2Top + 18 + METER_H + 14;
            var btnBadge = new Button
            {
                Text      = "● Auto Level-Match: ON",
                ForeColor = Color.White,
                BackColor = ONE_RED,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
                Bounds    = new Rectangle(x, badgeTop, w, BADGE_H),
                Cursor    = Cursors.Hand
            };
            btnBadge.FlatAppearance.BorderSize = 0;
            btnBadge.Click += (s, e) => ToggleAutoLevel(isLeft, btnBadge);
            this.Controls.Add(btnBadge);
            if (isLeft) _btnAgentAutoLevel = btnBadge;
            else        _btnCustomerAutoLevel = btnBadge;

            // Action button
            int btnTop = badgeTop + BADGE_H + 10;
            string btnText = isLeft ? "⚙  Audio Settings" : "🎙  Test Microphone";
            var btn = new Button
            {
                Text      = btnText,
                ForeColor = TEXT_WHITE,
                BackColor = BG_PANEL,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 14f, FontStyle.Bold),
                Bounds    = new Rectangle(x, btnTop, w, BTN_H),
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = ONE_RED;
            btn.FlatAppearance.BorderSize  = 1;
            btn.Click += isLeft ? (EventHandler)OnAudioSettingsClick : OnTestMicClick;
            this.Controls.Add(btn);
            if (isLeft) _btnAudioSettings = btn;
            else        _btnTestMic = btn;
        }

        private Panel BuildMeterControl(int x, int y, int w, string key)
        {
            var panel = new Panel
            {
                Bounds    = new Rectangle(x, y, w, METER_H),
                BackColor = Color.Transparent,
                Cursor    = Cursors.SizeWE,
                Tag       = key
            };

            panel.Paint += (s, e) => DrawMeter(e.Graphics, (Panel)s, key);
            panel.MouseDown += MeterMouseDown;
            panel.MouseMove += MeterMouseMove;
            panel.MouseUp   += MeterMouseUp;

            this.Controls.Add(panel);

            switch (key)
            {
                case "agentVoice":    _agentVoiceMeterPanel    = panel; break;
                case "agentScript":   _agentScriptMeterPanel   = panel; break;
                case "customerVoice": _customerVoiceMeterPanel = panel; break;
                case "customerScript":_customerScriptMeterPanel= panel; break;
            }

            return panel;
        }

        // ── Meter drawing ─────────────────────────────────────────────────────
        private void DrawMeter(Graphics g, Panel panel, string key)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = panel.Width;
            int h = panel.Height;

            // Background track
            using (var bg = new SolidBrush(Color.FromArgb(40, 40, 40)))
            using (var path = RoundedRect(new Rectangle(0, 0, w - 1, h - 1), 4))
                g.FillPath(bg, path);

            // Gradient fill (blue → red)
            float level = GetLevel(key);
            int fillW = (int)(w * level);
            if (fillW > 2)
            {
                using (var grad = new LinearGradientBrush(
                    new Point(0, 0), new Point(w, 0),
                    Color.FromArgb(30, 80, 220),   // deep blue
                    ONE_RED))                       // ONE red
                using (var path = RoundedRect(new Rectangle(0, 0, fillW, h - 1), 4))
                    g.FillPath(grad, path);
            }

            // Red border
            using (var pen = new Pen(ONE_RED, 1f))
            using (var path = RoundedRect(new Rectangle(0, 0, w - 1, h - 1), 4))
                g.DrawPath(pen, path);

            // Drag thumb
            float sliderPos = GetSlider(key);
            int thumbX = (int)(w * sliderPos) - 4;
            thumbX = Math.Max(0, Math.Min(thumbX, w - 8));
            using (var thumbBrush = new SolidBrush(ONE_RED))
                g.FillRectangle(thumbBrush, thumbX, 0, 8, h);
            using (var thumbPen = new Pen(Color.White, 1f))
                g.DrawRectangle(thumbPen, thumbX, 0, 7, h - 1);
        }

        private float GetLevel(string key)
        {
            switch (key)
            {
                case "agentVoice":    return _agentVoiceLevel;
                case "agentScript":   return _agentScriptLevel;
                case "customerVoice": return _customerVoiceLevel;
                case "customerScript":return _customerScriptLevel;
                default: return 0f;
            }
        }

        private float GetSlider(string key)
        {
            switch (key)
            {
                case "agentVoice":    return _agentVoiceSlider;
                case "agentScript":   return _agentScriptSlider;
                case "customerVoice": return _customerVoiceSlider;
                case "customerScript":return _customerScriptSlider;
                default: return 0.5f;
            }
        }

        private void SetSlider(string key, float val)
        {
            val = Math.Max(0f, Math.Min(1f, val));
            switch (key)
            {
                case "agentVoice":    _agentVoiceSlider    = val; break;
                case "agentScript":   _agentScriptSlider   = val; break;
                case "customerVoice": _customerVoiceSlider = val; break;
                case "customerScript":_customerScriptSlider= val; break;
            }
            AppSettings.Instance.SetVolume(key, val);
            AppSettings.Instance.Save();
        }

        // ── Meter mouse drag ──────────────────────────────────────────────────
        private void MeterMouseDown(object sender, MouseEventArgs e)
        {
            _draggingMeter = (Panel)sender;
            _draggingKey   = (string)_draggingMeter.Tag;
        }

        private void MeterMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingMeter == null || e.Button != MouseButtons.Left) return;
            float val = (float)e.X / _draggingMeter.Width;
            SetSlider(_draggingKey, val);
            _draggingMeter.Invalidate();
        }

        private void MeterMouseUp(object sender, MouseEventArgs e)
        {
            _draggingMeter = null;
            _draggingKey   = null;
        }

        // ── Footer ────────────────────────────────────────────────────────────
        private void BuildFooter(int W, int H)
        {
            int footerY = H - FOOTER_H;

            // Red footer line
            var footerLine = new Panel
            {
                BackColor = ONE_RED,
                Bounds    = new Rectangle(0, footerY, W, 2)
            };
            this.Controls.Add(footerLine);

            _lblFooterLeft = new Label
            {
                Text      = "ONE United Global  2026  v5.0",
                ForeColor = TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 15f),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds    = new Rectangle(SIDE_PAD, footerY + 2, 300, FOOTER_H - 2)
            };
            this.Controls.Add(_lblFooterLeft);

            _lblFooterCenter = new Label
            {
                Text      = "This Desktop App Can Be Minimized  •  Settings Are Auto Saved",
                ForeColor = TEXT_GREY,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 14f),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds    = new Rectangle(0, footerY + 2, W, FOOTER_H - 2)
            };
            this.Controls.Add(_lblFooterCenter);
        }

        // ── Red border ────────────────────────────────────────────────────────
        private void DrawRedBorder(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int W = this.ClientSize.Width;
            int H = this.ClientSize.Height;
            using (var pen = new Pen(ONE_RED, 3f))
                g.DrawRectangle(pen, 1, 1, W - 3, H - 3);
        }

        private void DrawVideoRedBorder(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var p = (Panel)sender;
            using (var pen = new Pen(ONE_RED, 2f))
                g.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
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
                Text            = "ONE Voice Solution",
                ContextMenuStrip = _trayMenu,
                Visible         = true
            };

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "one_logo.ico");
            if (File.Exists(iconPath))
                _trayIcon.Icon = new Icon(iconPath);
            else
                _trayIcon.Icon = SystemIcons.Application;

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
            this.ShowInTaskbar  = true;
            this.WindowState    = FormWindowState.Normal;
            this.BringToFront();
            this.Activate();
        }

        // ── Timers ────────────────────────────────────────────────────────────
        private void SetupMeterTimer()
        {
            _meterTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _meterTimer.Tick += (s, e) =>
            {
                // Decay levels
                _agentVoiceLevel     = Math.Max(0f, _agentVoiceLevel     - 0.04f);
                _agentScriptLevel    = Math.Max(0f, _agentScriptLevel    - 0.04f);
                _customerVoiceLevel  = Math.Max(0f, _customerVoiceLevel  - 0.04f);
                _customerScriptLevel = Math.Max(0f, _customerScriptLevel - 0.04f);

                _agentVoiceMeterPanel?.Invalidate();
                _agentScriptMeterPanel?.Invalidate();
                _customerVoiceMeterPanel?.Invalidate();
                _customerScriptMeterPanel?.Invalidate();
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
        private void StartAudioCapture()
        {
            try
            {
                var devices = _deviceEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                if (!devices.Any()) return;

                var device = devices.First();
                _micCapture = new WasapiCapture(device);
                _micCapture.DataAvailable += (s, e) =>
                {
                    float max = 0f;
                    for (int i = 0; i < e.BytesRecorded; i += 4)
                    {
                        float sample = Math.Abs(BitConverter.ToSingle(e.Buffer, i));
                        if (sample > max) max = sample;
                    }
                    _agentVoiceLevel = Math.Min(1f, max * _agentVoiceSlider * 2f);
                };
                _micCapture.StartRecording();
            }
            catch (Exception ex)
            {
                Log.Warn($"[Audio] Capture init failed: {ex.Message}");
            }
        }

        // ── Device population ─────────────────────────────────────────────────
        private void PopulateDevices()
        {
            try
            {
                var captures = _deviceEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                foreach (var d in captures) _cboMic.Items.Add(d.FriendlyName);
                if (_cboMic.Items.Count > 0)
                {
                    string saved = AppSettings.Instance.MicDevice;
                    int idx = _cboMic.Items.IndexOf(saved);
                    _cboMic.SelectedIndex = idx >= 0 ? idx : 0;
                }

                var renders = _deviceEnum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (var d in renders) _cboHeadset.Items.Add(d.FriendlyName);
                if (_cboHeadset.Items.Count > 0)
                {
                    string saved = AppSettings.Instance.HeadsetDevice;
                    int idx = _cboHeadset.Items.IndexOf(saved);
                    _cboHeadset.SelectedIndex = idx >= 0 ? idx : 0;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[Audio] Device enumeration failed: {ex.Message}");
            }
        }

        // ── Heartbeat ─────────────────────────────────────────────────────────
        // Owner hardware UUID — this machine bypasses license validation entirely.
        private const string OWNER_UUID = "4C4C4544-0058-3510-8043-B5C04F595733";

        private async void StartHeartbeat()
        {
            string machineId = MachineId.Get();

            // Owner bypass: skip all license checks on the developer's machine.
            if (string.Equals(machineId, OWNER_UUID, StringComparison.OrdinalIgnoreCase))
            {
                if (_lblAgentName != null)
                    _lblAgentName.Text = "Agent: Owner";
                Log.Info("[License] Owner machine detected — license validation bypassed.");
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
                        _lblAgentName.Text = "Agent: " + name;
                }
                HeartbeatService.Instance.Start();
            }

            HeartbeatService.Instance.LicenseInvalid += (s, e) =>
            {
                this.Invoke((Action)(() =>
                {
                    MessageBox.Show(
                        e.Reason + "\n\nPlease visit onevoicesolution.com to renew.",
                        "License Invalid",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
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
                        "Seat Limit Exceeded",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }));
            };
        }

        // ── Settings ──────────────────────────────────────────────────────────
        private void LoadSettings()
        {
            var s = AppSettings.Instance;
            _agentVoiceSlider    = s.GetVolume("agentVoice",    0.62f);
            _agentScriptSlider   = s.GetVolume("agentScript",   0.48f);
            _customerVoiceSlider = s.GetVolume("customerVoice", 0.55f);
            _customerScriptSlider= s.GetVolume("customerScript",0.55f);
            _agentAutoLevel      = s.AgentAutoLevel;
            _customerAutoLevel   = s.CustomerAutoLevel;
        }

        // ── Logo (agency swap) ────────────────────────────────────────────────
        private void LoadLogo()
        {
            string agencyLogo = AppSettings.Instance.AgencyLogoPath;
            string oneLogo    = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "one_logo.png");

            string logoPath = (!string.IsNullOrEmpty(agencyLogo) && File.Exists(agencyLogo))
                ? agencyLogo
                : oneLogo;

            if (File.Exists(logoPath))
                _logoBox.Image = Image.FromFile(logoPath);
        }

        // ── Auto Level-Match toggle ───────────────────────────────────────────
        private void ToggleAutoLevel(bool isLeft, Button btn)
        {
            if (isLeft)
            {
                _agentAutoLevel = !_agentAutoLevel;
                btn.Text = _agentAutoLevel ? "● Auto Level-Match: ON" : "○ Auto Level-Match: OFF";
                AppSettings.Instance.AgentAutoLevel = _agentAutoLevel;
            }
            else
            {
                _customerAutoLevel = !_customerAutoLevel;
                btn.Text = _customerAutoLevel ? "● Auto Level-Match: ON" : "○ Auto Level-Match: OFF";
                AppSettings.Instance.CustomerAutoLevel = _customerAutoLevel;
            }
            AppSettings.Instance.Save();
        }

        // ── Button handlers ───────────────────────────────────────────────────
        private void OnAudioSettingsClick(object sender, EventArgs e)
        {
            MessageBox.Show("Audio Settings dialog coming soon.", "Audio Settings",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnTestMicClick(object sender, EventArgs e)
        {
            MessageBox.Show("Microphone test will play back your mic for 5 seconds.",
                "Test Microphone", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private Label MakeLabel(string text, int x, int y, float size, bool bold = false, Color? color = null)
        {
            var lbl = new Label
            {
                Text      = text,
                ForeColor = color ?? TEXT_WHITE,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
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
