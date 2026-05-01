using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    /// <summary>
    /// Full-screen dark update splash shown during auto-update download and install.
    /// Replaces the blank black command prompt windows.
    /// </summary>
    public class UpdateProgressForm : Form
    {
        // ── Controls ──────────────────────────────────────────────────────────
        private Label  _titleLabel;
        private Label  _statusLabel;
        private Label  _detailLabel;
        private Label  _countdownLabel;
        private Panel  _progressTrack;
        private Panel  _progressFill;
        private System.Windows.Forms.Timer _marqueeTimer;
        private System.Windows.Forms.Timer _countdownTimer;

        private int    _marqueePos   = 0;
        private int    _countdownSec = 45;
        private string _remoteVer    = "";

        // ONE Voice brand colours
        private static readonly Color BG       = Color.FromArgb(10, 10, 10);
        private static readonly Color RED      = Color.FromArgb(254, 1, 1);
        private static readonly Color DIMWHITE = Color.FromArgb(200, 200, 200);
        private static readonly Color DIMGRAY  = Color.FromArgb(120, 120, 120);

        public UpdateProgressForm(string remoteVersion)
        {
            _remoteVer = remoteVersion;
            BuildUI();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetStatus(string status)
        {
            if (InvokeRequired) { Invoke(new Action<string>(SetStatus), status); return; }
            _statusLabel.Text = status;
            Refresh();
        }

        public void SetDetail(string detail)
        {
            if (InvokeRequired) { Invoke(new Action<string>(SetDetail), detail); return; }
            _detailLabel.Text = detail;
            Refresh();
        }

        public void SetProgress(int pct)
        {
            if (InvokeRequired) { Invoke(new Action<int>(SetProgress), pct); return; }
            // Stop marquee, show real progress
            _marqueeTimer.Stop();
            int w = (int)(_progressTrack.Width * Math.Max(0, Math.Min(100, pct)) / 100.0);
            _progressFill.Width = w;
            Refresh();
        }

        public void StartMarquee()
        {
            if (InvokeRequired) { Invoke(new Action(StartMarquee)); return; }
            _progressFill.Width = 60;
            _marqueeTimer.Start();
        }

        public void StartCountdown()
        {
            if (InvokeRequired) { Invoke(new Action(StartCountdown)); return; }
            _countdownTimer.Start();
        }

        // ── Build UI ──────────────────────────────────────────────────────────

        private void BuildUI()
        {
            // Form
            Text            = "ONE Voice Solution — Updating";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.CenterScreen;
            Size            = new Size(520, 300);
            BackColor       = BG;
            TopMost         = true;
            ShowInTaskbar   = true;

            // Red top bar
            var topBar = new Panel { BackColor = RED, Bounds = new Rectangle(0, 0, 520, 4) };
            Controls.Add(topBar);

            // ONE VOICE title
            _titleLabel = new Label
            {
                Text      = "ONE VOICE SOLUTION",
                Font      = new Font("Barlow Condensed", 22f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(30, 28)
            };
            Controls.Add(_titleLabel);

            // Version badge
            var badge = new Label
            {
                Text      = $"v{_remoteVer}",
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = RED,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(30, 62)
            };
            Controls.Add(badge);

            // Status label
            _statusLabel = new Label
            {
                Text      = "Downloading update...",
                Font      = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize  = false,
                Size      = new Size(460, 28),
                Location  = new Point(30, 110)
            };
            Controls.Add(_statusLabel);

            // Detail label
            _detailLabel = new Label
            {
                Text      = "Please wait — do not close this window.",
                Font      = new Font("Segoe UI", 9f),
                ForeColor = DIMGRAY,
                BackColor = Color.Transparent,
                AutoSize  = false,
                Size      = new Size(460, 20),
                Location  = new Point(30, 142)
            };
            Controls.Add(_detailLabel);

            // Progress track
            _progressTrack = new Panel
            {
                BackColor = Color.FromArgb(35, 35, 35),
                Bounds    = new Rectangle(30, 175, 460, 8)
            };
            Controls.Add(_progressTrack);

            // Progress fill
            _progressFill = new Panel
            {
                BackColor = RED,
                Bounds    = new Rectangle(0, 0, 0, 8)
            };
            _progressTrack.Controls.Add(_progressFill);

            // Countdown label
            _countdownLabel = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 9f),
                ForeColor = DIMGRAY,
                BackColor = Color.Transparent,
                AutoSize  = false,
                Size      = new Size(460, 20),
                Location  = new Point(30, 196),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(_countdownLabel);

            // Footer
            var footer = new Label
            {
                Text      = "The app will relaunch automatically when the update is complete.",
                Font      = new Font("Segoe UI", 8f),
                ForeColor = DIMGRAY,
                BackColor = Color.Transparent,
                AutoSize  = false,
                Size      = new Size(460, 18),
                Location  = new Point(30, 255),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(footer);

            // Marquee timer
            _marqueeTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _marqueeTimer.Tick += (s, e) =>
            {
                _marqueePos += 3;
                int trackW = _progressTrack.Width;
                int fillW  = 80;
                if (_marqueePos > trackW + fillW) _marqueePos = -fillW;
                int x = _marqueePos - fillW;
                _progressFill.Location = new Point(Math.Max(0, x), 0);
                _progressFill.Width    = Math.Min(fillW, trackW - Math.Max(0, x));
            };

            // Countdown timer
            _countdownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _countdownTimer.Tick += (s, e) =>
            {
                if (_countdownSec > 0)
                {
                    _countdownLabel.Text = $"Estimated time remaining: {_countdownSec}s";
                    _countdownSec--;
                }
                else
                {
                    _countdownLabel.Text = "Almost done...";
                }
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Subtle border
            using (var pen = new Pen(Color.FromArgb(50, 50, 50), 1))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        // Prevent user closing during update
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
                e.Cancel = true;
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _marqueeTimer?.Stop();
                _marqueeTimer?.Dispose();
                _countdownTimer?.Stop();
                _countdownTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
