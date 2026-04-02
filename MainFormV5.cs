/**
 * MainFormV5.cs
 * ONE Voice Solution v5.0
 *
 * Complete rewrite of the main application window to match the approved v5 mockup:
 *
 *  ┌─────────────────────────────────────────────────────────────────┐
 *  │ [ONE logo / Agency logo]  Voice Solution        [LIVE●Connected]│
 *  │                           Audio Control Panel                   │
 *  │                           Agent: {name}                         │
 *  ├─────────────────────────────────────────────────────────────────┤
 *  │  [Microphone ▾]                    [Headset/Speaker ▾]          │
 *  │                                                                 │
 *  │  AGENT AUDIO      ┌──────────────┐     CUSTOMER OUTPUT         │
 *  │  Customer Voice   │              │     My Mic Level             │
 *  │  [━━━━━━━●━━━━━]  │  ONE Video   │     [━━━━━━━●━━━━━]         │
 *  │  Script Playback  │  (looping)   │     Script Playback          │
 *  │  [━━━━━━━●━━━━━]  │              │     [━━━━━━━●━━━━━]         │
 *  │  [●Auto Level-Match: ON]         │     [●Auto Level-Match: ON]  │
 *  │  [⚙ Audio Settings]             └──   [🎤 Test Microphone]     │
 *  │                                                                 │
 *  │         "The Geniusness Is In The Simplicity"                   │
 *  ├─────────────────────────────────────────────────────────────────┤
 *  │ ONE United Global 2026 v5.0    This Desktop App Can Be Minimized│
 *  └─────────────────────────────────────────────────────────────────┘
 *
 * Features:
 *  - Auto-centers on primary screen with ~1.5" margin on launch
 *  - Remembers last position across monitors
 *  - Minimizes to system tray (ONE logo icon)
 *  - Looping WMP video in center panel
 *  - Agency logo replaces ONE logo if configured in settings
 *  - Drag-handle sliders on each meter for volume adjustment
 *  - Auto Level-Match toggle badge
 *  - Heartbeat service started after license validation
 *  - All settings auto-saved on change
 */
using NAudio.CoreAudioApi;
using NLog;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1.src;
using AxWMPLib;

namespace WindowsFormsApp1
{
    public partial class MainFormV5 : Form
    {
        // ── Constants ─────────────────────────────────────────────────────────
        private static readonly Color RED      = Color.FromArgb(0xFE, 0x01, 0x01);
        private static readonly Color DARK_BG  = Color.FromArgb(10, 10, 14);
        private static readonly Color PANEL_BG = Color.FromArgb(18, 18, 24);
        private static readonly Color TEXT_WHITE = Color.FromArgb(230, 230, 230);
        private const string APP_VERSION = "5.0";

        // ── Logger ────────────────────────────────────────────────────────────
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        // ── Services ──────────────────────────────────────────────────────────
        private readonly AudioService _audioService = AudioService.Instance;
        private readonly AppSettings  _settings     = AppSettings.Instance;
        private HeartbeatService      _heartbeat;

        // ── UI Controls ───────────────────────────────────────────────────────
        private PictureBox   _logoBox;
        private Label        _lblVoiceSolution;
        private Label        _lblAudioControlPanel;
        private Label        _lblAgentName;
        private Label        _lblLiveStatus;
        private Panel        _headerPanel;
        private Panel        _redLine;
        private ComboBox     _cboMic;
        private ComboBox     _cboHeadset;
        private Label        _lblMicLabel;
        private Label        _lblHeadsetLabel;

        // Left panel
        private Label        _lblAgentAudio;
        private Label        _lblCustomerVoice;
        private TrackBar     _sliderCustomerVoice;
        private Label        _lblScriptPlayback;
        private TrackBar     _sliderScriptPlayback;
        private Panel        _badgeAutoMatchLeft;
        private Label        _lblAutoMatchLeft;
        private Button       _btnAudioSettings;

        // Center panel
        private AxWindowsMediaPlayer _videoPlayer;
        private Panel        _videoPanel;

        // Right panel
        private Label        _lblCustomerOutput;
        private Label        _lblMyMicLevel;
        private TrackBar     _sliderMyMicLevel;
        private Label        _lblCustomerScriptPlayback;
        private TrackBar     _sliderCustomerScriptPlayback;
        private Panel        _badgeAutoMatchRight;
        private Label        _lblAutoMatchRight;
        private Button       _btnTestMic;

        // Bottom
        private Label        _lblTagline;
        private Label        _lblCopyright;
        private Label        _lblMinimizeHint;

        // Tray
        private NotifyIcon   _trayIcon;
        private ContextMenuStrip _trayMenu;

        // ── Constructor ───────────────────────────────────────────────────────
        public MainFormV5(string licenseKey, string agentName)
        {
            _settings.AgentName = agentName;

            InitializeFormProperties();
            BuildUI();
            WireEvents();
            LoadSettings();
            PopulateDeviceLists();
            StartVideo();
            StartHeartbeat(licenseKey);
            SetupTrayIcon();
            CenterOnScreen();
        }

        // ── Form properties ───────────────────────────────────────────────────
        private void InitializeFormProperties()
        {
            Text            = "ONE Voice Solution";
            BackColor       = DARK_BG;
            FormBorderStyle = FormBorderStyle.None; // custom border drawn in OnPaint
            StartPosition   = FormStartPosition.Manual;
            MinimizeBox     = true;
            MaximizeBox     = false;
            ShowInTaskbar   = true;
            DoubleBuffered  = true;

            // DPI awareness is set in app.manifest (PerMonitorV2)
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        }

        // ── Center on screen with ~1.5" margin ───────────────────────────────
        private void CenterOnScreen()
        {
            // Restore last position if valid
            if (_settings.WindowX >= 0 && _settings.WindowY >= 0)
            {
                Location = new Point(_settings.WindowX, _settings.WindowY);
                return;
            }

            // Default: center on primary screen with ~144px (1.5") margin
            const int MARGIN = 144;
            Screen screen = Screen.PrimaryScreen;
            int w = screen.WorkingArea.Width  - 2 * MARGIN;
            int h = screen.WorkingArea.Height - 2 * MARGIN;
            Size    = new Size(w, h);
            Location = new Point(
                screen.WorkingArea.Left + MARGIN,
                screen.WorkingArea.Top  + MARGIN);
        }

        // ── Build UI ──────────────────────────────────────────────────────────
        private void BuildUI()
        {
            SuspendLayout();

            // ── Header panel ─────────────────────────────────────────────────
            _headerPanel = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 90,
                BackColor = DARK_BG
            };

            _logoBox = new PictureBox
            {
                Size        = new Size(160, 70),
                Location    = new Point(20, 10),
                SizeMode    = PictureBoxSizeMode.Zoom,
                BackColor   = Color.Transparent
            };
            LoadLogo();

            _lblVoiceSolution = MakeLabel("Voice Solution", 13, FontStyle.Regular, TEXT_WHITE);
            _lblVoiceSolution.Location = new Point(200, 14);

            _lblAudioControlPanel = MakeLabel("Audio Control Panel", 11, FontStyle.Regular, Color.FromArgb(160, 160, 160));
            _lblAudioControlPanel.Location = new Point(200, 36);

            _lblAgentName = MakeLabel("Agent: " + _settings.AgentName, 11, FontStyle.Regular, TEXT_WHITE);
            _lblAgentName.Location = new Point(200, 56);

            _lblLiveStatus = MakeLabel("  ● LIVE · Connected  ", 11, FontStyle.Bold, Color.White);
            _lblLiveStatus.BackColor = RED;
            _lblLiveStatus.AutoSize  = false;
            _lblLiveStatus.Size      = new Size(160, 28);
            _lblLiveStatus.TextAlign = ContentAlignment.MiddleCenter;
            _lblLiveStatus.Anchor    = AnchorStyles.Top | AnchorStyles.Right;

            _headerPanel.Controls.AddRange(new Control[] {
                _logoBox, _lblVoiceSolution, _lblAudioControlPanel, _lblAgentName, _lblLiveStatus
            });

            // ── Red separator line ────────────────────────────────────────────
            _redLine = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 3,
                BackColor = RED
            };

            // ── Device selector row ───────────────────────────────────────────
            _lblMicLabel = MakeLabel("Microphone", 10, FontStyle.Regular, Color.FromArgb(180, 180, 180));
            _lblHeadsetLabel = MakeLabel("Headset / Speaker", 10, FontStyle.Regular, Color.FromArgb(180, 180, 180));

            _cboMic = MakeCombo();
            _cboHeadset = MakeCombo();

            // ── Section labels ────────────────────────────────────────────────
            _lblAgentAudio = MakeLabel("AGENT AUDIO", 13, FontStyle.Bold, RED);
            _lblCustomerOutput = MakeLabel("CUSTOMER OUTPUT", 13, FontStyle.Bold, RED);

            // ── Left meters ───────────────────────────────────────────────────
            _lblCustomerVoice = MakeLabel("Customer Voice", 11, FontStyle.Regular, TEXT_WHITE);
            _sliderCustomerVoice = MakeSlider(60);

            _lblScriptPlayback = MakeLabel("Script Playback", 11, FontStyle.Regular, TEXT_WHITE);
            _sliderScriptPlayback = MakeSlider(50);

            _badgeAutoMatchLeft = MakeBadge();
            _lblAutoMatchLeft   = MakeBadgeLabel("● Auto Level-Match: ON");
            _badgeAutoMatchLeft.Controls.Add(_lblAutoMatchLeft);

            _btnAudioSettings = MakeButton("⚙  Audio Settings");
            _btnAudioSettings.Click += (s, e) => ShowAudioSettings();

            // ── Center video panel ────────────────────────────────────────────
            _videoPanel = new Panel
            {
                BackColor   = Color.Black,
                BorderStyle = BorderStyle.None
            };

            // ── Right meters ──────────────────────────────────────────────────
            _lblMyMicLevel = MakeLabel("My Mic Level", 11, FontStyle.Regular, TEXT_WHITE);
            _sliderMyMicLevel = MakeSlider(55);

            _lblCustomerScriptPlayback = MakeLabel("Script Playback", 11, FontStyle.Regular, TEXT_WHITE);
            _sliderCustomerScriptPlayback = MakeSlider(55);

            _badgeAutoMatchRight = MakeBadge();
            _lblAutoMatchRight   = MakeBadgeLabel("● Auto Level-Match: ON");
            _badgeAutoMatchRight.Controls.Add(_lblAutoMatchRight);

            _btnTestMic = MakeButton("🎤  Test Microphone");
            _btnTestMic.Click += (s, e) => TestMicrophone();

            // ── Bottom labels ─────────────────────────────────────────────────
            _lblTagline = MakeLabel("The Geniusness Is In The Simplicity", 20, FontStyle.Bold, RED);
            _lblTagline.TextAlign = ContentAlignment.MiddleCenter;

            _lblCopyright = MakeLabel("ONE United Global  2026  v5.0", 12, FontStyle.Regular, TEXT_WHITE);

            _lblMinimizeHint = MakeLabel(
                "This Desktop App Can Be Minimized and Settings Are Auto Saved",
                11, FontStyle.Regular, Color.FromArgb(140, 140, 140));
            _lblMinimizeHint.TextAlign = ContentAlignment.MiddleCenter;

            // ── Add all to form ───────────────────────────────────────────────
            Controls.AddRange(new Control[] {
                _headerPanel, _redLine,
                _lblMicLabel, _cboMic, _lblHeadsetLabel, _cboHeadset,
                _lblAgentAudio, _lblCustomerOutput,
                _lblCustomerVoice, _sliderCustomerVoice,
                _lblScriptPlayback, _sliderScriptPlayback,
                _badgeAutoMatchLeft, _btnAudioSettings,
                _videoPanel,
                _lblMyMicLevel, _sliderMyMicLevel,
                _lblCustomerScriptPlayback, _sliderCustomerScriptPlayback,
                _badgeAutoMatchRight, _btnTestMic,
                _lblTagline, _lblCopyright, _lblMinimizeHint
            });

            ResumeLayout(false);
        }

        // ── Layout (called on resize) ─────────────────────────────────────────
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (_headerPanel == null) return;

            int W = ClientSize.Width;
            int H = ClientSize.Height;

            // Header
            _headerPanel.Width = W;
            _lblLiveStatus.Location = new Point(W - 180, 31);

            // Red line
            _redLine.Width = W;

            // Layout constants
            int TOP_CONTENT = _headerPanel.Height + _redLine.Height + 12;
            int PAD         = 30;   // side padding
            int PANEL_W     = (int)((W - 2 * PAD) * 0.28); // ~28% each side
            int VIDEO_W     = W - 2 * PAD - 2 * PANEL_W - 20; // remaining center
            int LX          = PAD;
            int VX          = LX + PANEL_W + 10;
            int RX          = VX + VIDEO_W + 10;

            // Device selectors
            int devY = TOP_CONTENT;
            int devW = (W - 2 * PAD - 20) / 2;
            _lblMicLabel.SetBounds(LX + (devW - _lblMicLabel.PreferredWidth) / 2, devY, _lblMicLabel.PreferredWidth, 18);
            _cboMic.SetBounds(LX, devY + 20, devW, 28);
            _lblHeadsetLabel.SetBounds(LX + devW + 20 + (devW - _lblHeadsetLabel.PreferredWidth) / 2, devY, _lblHeadsetLabel.PreferredWidth, 18);
            _cboHeadset.SetBounds(LX + devW + 20, devY + 20, devW, 28);

            // Section labels
            int secY = devY + 60;
            _lblAgentAudio.SetBounds(LX, secY, PANEL_W, 22);
            _lblCustomerOutput.SetBounds(RX, secY, PANEL_W, 22);

            // Meters
            int meterY1 = secY + 28;
            int meterH  = 22;
            int sliderH = 28;
            int meterSpacing = 52;

            _lblCustomerVoice.SetBounds(LX, meterY1, PANEL_W, 18);
            _sliderCustomerVoice.SetBounds(LX, meterY1 + 20, PANEL_W, sliderH);

            int meterY2 = meterY1 + meterSpacing;
            _lblScriptPlayback.SetBounds(LX, meterY2, PANEL_W, 18);
            _sliderScriptPlayback.SetBounds(LX, meterY2 + 20, PANEL_W, sliderH);

            // Video panel — starts at secY, ends at button bottom
            int videoTop    = secY;
            int videoBottom = meterY2 + 20 + sliderH + 10 + 36 + 10 + 36; // badge + btn
            int videoH      = videoBottom - videoTop;
            _videoPanel.SetBounds(VX, videoTop, VIDEO_W, videoH);

            // Auto-match badges
            int badgeY = meterY2 + 20 + sliderH + 10;
            _badgeAutoMatchLeft.SetBounds(LX, badgeY, PANEL_W, 30);
            _badgeAutoMatchRight.SetBounds(RX, badgeY, PANEL_W, 30);

            // Buttons — bottom-align with video
            int btnY = badgeY + 36;
            _btnAudioSettings.SetBounds(LX, btnY, PANEL_W, 36);
            _btnTestMic.SetBounds(RX, btnY, PANEL_W, 36);

            // Right meters
            _lblMyMicLevel.SetBounds(RX, meterY1, PANEL_W, 18);
            _sliderMyMicLevel.SetBounds(RX, meterY1 + 20, PANEL_W, sliderH);
            _lblCustomerScriptPlayback.SetBounds(RX, meterY2, PANEL_W, 18);
            _sliderCustomerScriptPlayback.SetBounds(RX, meterY2 + 20, PANEL_W, sliderH);

            // Tagline
            int taglineY = btnY + 46;
            _lblTagline.SetBounds(PAD, taglineY, W - 2 * PAD, 36);

            // Bottom bar
            int bottomY = H - 30;
            _lblCopyright.SetBounds(LX, bottomY, 260, 20);
            _lblMinimizeHint.SetBounds(PAD, bottomY, W - 2 * PAD, 20);
        }

        // ── Custom border paint ───────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(RED, 3))
                e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
        }

        // ── Logo loading (agency logo swap) ───────────────────────────────────
        private void LoadLogo()
        {
            try
            {
                string agencyLogo = _settings.AgencyLogoPath;
                if (!string.IsNullOrEmpty(agencyLogo) && File.Exists(agencyLogo))
                {
                    _logoBox.Image = Image.FromFile(agencyLogo);
                    return;
                }
            }
            catch { }

            // Fall back to ONE logo embedded resource
            try
            {
                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "one_logo.png");
                if (File.Exists(logoPath))
                    _logoBox.Image = Image.FromFile(logoPath);
            }
            catch { }
        }

        // ── Video player ──────────────────────────────────────────────────────
        private void StartVideo()
        {
            try
            {
                _videoPlayer = new AxWindowsMediaPlayer
                {
                    Dock = DockStyle.Fill
                };
                _videoPanel.Controls.Add(_videoPlayer);
                _videoPlayer.CreateControl();

                string videoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "1ONEDigitalVideo.mp4");
                if (File.Exists(videoPath))
                {
                    _videoPlayer.settings.autoStart = true;
                    _videoPlayer.settings.setMode("loop", true);
                    _videoPlayer.URL = videoPath;
                    _videoPlayer.Ctlcontrols.play();
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[Video] Could not start video: {ex.Message}");
            }
        }

        // ── Device lists ──────────────────────────────────────────────────────
        private void PopulateDeviceLists()
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();

                // Microphone (capture) devices
                var captureDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                _cboMic.Items.Clear();
                foreach (var d in captureDevices)
                    _cboMic.Items.Add(d.FriendlyName);

                if (!string.IsNullOrEmpty(_settings.MicrophoneDevice))
                {
                    int idx = _cboMic.Items.IndexOf(_settings.MicrophoneDevice);
                    _cboMic.SelectedIndex = idx >= 0 ? idx : (_cboMic.Items.Count > 0 ? 0 : -1);
                }
                else if (_cboMic.Items.Count > 0)
                    _cboMic.SelectedIndex = 0;

                // Headset/Speaker (render) devices
                var renderDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                _cboHeadset.Items.Clear();
                foreach (var d in renderDevices)
                    _cboHeadset.Items.Add(d.FriendlyName);

                if (!string.IsNullOrEmpty(_settings.HeadsetDevice))
                {
                    int idx = _cboHeadset.Items.IndexOf(_settings.HeadsetDevice);
                    _cboHeadset.SelectedIndex = idx >= 0 ? idx : (_cboHeadset.Items.Count > 0 ? 0 : -1);
                }
                else if (_cboHeadset.Items.Count > 0)
                    _cboHeadset.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Log.Warn($"[Devices] Could not enumerate audio devices: {ex.Message}");
            }
        }

        // ── Load settings into controls ───────────────────────────────────────
        private void LoadSettings()
        {
            _sliderCustomerVoice.Value          = Clamp(_settings.CustomerVoiceVolume);
            _sliderScriptPlayback.Value         = Clamp(_settings.AgentScriptPlaybackVolume);
            _sliderMyMicLevel.Value             = Clamp(_settings.MyMicLevelVolume);
            _sliderCustomerScriptPlayback.Value = Clamp(_settings.CustomerScriptPlaybackVolume);
            UpdateAutoMatchBadge(_settings.AutoLevelMatchEnabled);
        }

        // ── Wire events ───────────────────────────────────────────────────────
        private void WireEvents()
        {
            _sliderCustomerVoice.ValueChanged += (s, e) => {
                _settings.CustomerVoiceVolume = _sliderCustomerVoice.Value;
                _settings.Save();
                ApplyVolumes();
            };
            _sliderScriptPlayback.ValueChanged += (s, e) => {
                _settings.AgentScriptPlaybackVolume = _sliderScriptPlayback.Value;
                _settings.Save();
                ApplyVolumes();
            };
            _sliderMyMicLevel.ValueChanged += (s, e) => {
                _settings.MyMicLevelVolume = _sliderMyMicLevel.Value;
                _settings.Save();
                ApplyVolumes();
            };
            _sliderCustomerScriptPlayback.ValueChanged += (s, e) => {
                _settings.CustomerScriptPlaybackVolume = _sliderCustomerScriptPlayback.Value;
                _settings.Save();
                ApplyVolumes();
            };

            _cboMic.SelectedIndexChanged += (s, e) => {
                _settings.MicrophoneDevice = _cboMic.SelectedItem?.ToString() ?? "";
                _settings.Save();
                _audioService.SetInputDevice(_cboMic.SelectedIndex);
            };
            _cboHeadset.SelectedIndexChanged += (s, e) => {
                _settings.HeadsetDevice = _cboHeadset.SelectedItem?.ToString() ?? "";
                _settings.Save();
                _audioService.SetOutputDevice(_cboHeadset.SelectedIndex);
            };

            _badgeAutoMatchLeft.Click  += ToggleAutoMatch;
            _lblAutoMatchLeft.Click    += ToggleAutoMatch;
            _badgeAutoMatchRight.Click += ToggleAutoMatch;
            _lblAutoMatchRight.Click   += ToggleAutoMatch;

            // Save window position on move
            LocationChanged += (s, e) => {
                _settings.WindowX = Location.X;
                _settings.WindowY = Location.Y;
                _settings.Save();
            };

            // Minimize to tray
            Resize += (s, e) => {
                if (WindowState == FormWindowState.Minimized)
                {
                    Hide();
                    _trayIcon.Visible = true;
                }
            };

            FormClosing += (s, e) => {
                _heartbeat?.Stop();
                _trayIcon?.Dispose();
                _videoPlayer?.Ctlcontrols.stop();
            };
        }

        // ── Auto Level-Match toggle ───────────────────────────────────────────
        private void ToggleAutoMatch(object sender, EventArgs e)
        {
            _settings.AutoLevelMatchEnabled = !_settings.AutoLevelMatchEnabled;
            _settings.Save();
            UpdateAutoMatchBadge(_settings.AutoLevelMatchEnabled);
        }

        private void UpdateAutoMatchBadge(bool enabled)
        {
            string text = enabled ? "● Auto Level-Match: ON" : "○ Auto Level-Match: OFF";
            _lblAutoMatchLeft.Text  = text;
            _lblAutoMatchRight.Text = text;
        }

        // ── Volume application ────────────────────────────────────────────────
        private void ApplyVolumes()
        {
            try
            {
                float cv  = _sliderCustomerVoice.Value / 100f;
                float sp  = _sliderScriptPlayback.Value / 100f;
                float ml  = _sliderMyMicLevel.Value / 100f;
                float csp = _sliderCustomerScriptPlayback.Value / 100f;

                if (_audioService.waveOut != null)
                    _audioService.waveOut.Volume = cv;
                if (_audioService.waveOutSecond != null)
                    _audioService.waveOutSecond.Volume = sp;
                if (_audioService.waveO != null)
                    _audioService.waveO.Volume = ml;
            }
            catch (Exception ex)
            {
                Log.Warn($"[Volume] {ex.Message}");
            }
        }

        // ── Heartbeat ─────────────────────────────────────────────────────────
        private void StartHeartbeat(string licenseKey)
        {
            _heartbeat = HeartbeatService.Instance;
            _heartbeat.LicenseInvalid += OnLicenseInvalid;
            _heartbeat.Start(licenseKey, MachineId.Get());
        }

        private void OnLicenseInvalid(object sender, LicenseInvalidEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnLicenseInvalid(sender, e)));
                return;
            }

            MessageBox.Show(
                $"Your ONE Voice Solution license is no longer active.\n\nReason: {e.Reason}\n\nPlease renew at onevoicesolution.com.",
                "License Inactive",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            // Lock the UI
            _sliderCustomerVoice.Enabled          = false;
            _sliderScriptPlayback.Enabled         = false;
            _sliderMyMicLevel.Enabled             = false;
            _sliderCustomerScriptPlayback.Enabled = false;
            _btnAudioSettings.Enabled             = false;
            _btnTestMic.Enabled                   = false;
        }

        // ── Audio Settings dialog ─────────────────────────────────────────────
        private void ShowAudioSettings()
        {
            // Opens the existing AudioSettings / VolumeControl form
            var vc = new VolumeControl();
            vc.ShowDialog(this);
        }

        // ── Test Microphone ───────────────────────────────────────────────────
        private void TestMicrophone()
        {
            MessageBox.Show(
                "Speak into your microphone — you should hear yourself in your headset.",
                "Test Microphone",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // ── Tray icon ─────────────────────────────────────────────────────────
        private void SetupTrayIcon()
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Open ONE Voice", null, (s, e) => RestoreFromTray());
            _trayMenu.Items.Add("Exit",            null, (s, e) => Application.Exit());

            _trayIcon = new NotifyIcon
            {
                Text            = "ONE Voice Solution",
                ContextMenuStrip = _trayMenu,
                Visible         = false
            };

            // Load ONE logo as tray icon
            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "one_logo.ico");
                if (File.Exists(icoPath))
                    _trayIcon.Icon = new Icon(icoPath);
                else
                    _trayIcon.Icon = SystemIcons.Application;
            }
            catch
            {
                _trayIcon.Icon = SystemIcons.Application;
            }

            _trayIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState   = FormWindowState.Normal;
            _trayIcon.Visible = false;
            Activate();
        }

        // ── Drag to move (no title bar) ───────────────────────────────────────
        private bool _dragging;
        private Point _dragStart;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging  = true;
                _dragStart = e.Location;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
                Location = new Point(
                    Location.X + e.X - _dragStart.X,
                    Location.Y + e.Y - _dragStart.Y);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            base.OnMouseUp(e);
        }

        // ── Factory helpers ───────────────────────────────────────────────────
        private static Label MakeLabel(string text, float size, FontStyle style, Color color)
        {
            return new Label
            {
                Text      = text,
                ForeColor = color,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", size, style, GraphicsUnit.Point),
                AutoSize  = true
            };
        }

        private static TrackBar MakeSlider(int value)
        {
            var tb = new TrackBar
            {
                Minimum     = 0,
                Maximum     = 100,
                Value       = Clamp(value),
                TickStyle   = TickStyle.None,
                BackColor   = Color.FromArgb(18, 18, 24)
            };
            return tb;
        }

        private static Panel MakeBadge()
        {
            return new Panel
            {
                BackColor   = Color.FromArgb(30, 0, 0),
                BorderStyle = BorderStyle.None,
                Cursor      = Cursors.Hand
            };
        }

        private static Label MakeBadgeLabel(string text)
        {
            return new Label
            {
                Text      = text,
                ForeColor = RED,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor    = Cursors.Hand
            };
        }

        private static Button MakeButton(string text)
        {
            return new Button
            {
                Text      = text,
                ForeColor = TEXT_WHITE,
                BackColor = Color.FromArgb(30, 30, 38),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point),
                Cursor    = Cursors.Hand
            };
        }

        private static ComboBox MakeCombo()
        {
            return new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle     = FlatStyle.Flat,
                BackColor     = Color.FromArgb(28, 28, 36),
                ForeColor     = TEXT_WHITE,
                Font          = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point)
            };
        }

        private static int Clamp(int v) => Math.Max(0, Math.Min(100, v));
    }
}
