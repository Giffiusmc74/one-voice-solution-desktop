using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class InstallationProgressDialog : Form
    {
        private Label statusLabel;
        private ProgressBar progressBar;
        private Label detailLabel;
        private PictureBox iconPictureBox;

        public InstallationProgressDialog()
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.CenterParent;
            this.TopMost = true;
        }

        private void InitializeComponent()
        {
            this.statusLabel = new Label();
            this.progressBar = new ProgressBar();
            this.detailLabel = new Label();
            this.iconPictureBox = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.iconPictureBox)).BeginInit();
            this.SuspendLayout();

            // Form
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(450, 140);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "InstallationProgressDialog";
            this.Text = "Installing Virtual Audio Cable";
            this.BackColor = Color.White;
            this.ShowInTaskbar = false;

            // iconPictureBox
            this.iconPictureBox.Location = new Point(15, 15);
            this.iconPictureBox.Name = "iconPictureBox";
            this.iconPictureBox.Size = new Size(32, 32);
            this.iconPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            this.iconPictureBox.TabIndex = 0;
            this.iconPictureBox.TabStop = false;
            
            // Try to set system information icon
            try
            {
                this.iconPictureBox.Image = SystemIcons.Information.ToBitmap();
            }
            catch
            {
                // If icon loading fails, hide the picture box
                this.iconPictureBox.Visible = false;
            }

            // statusLabel
            this.statusLabel.AutoSize = false;
            this.statusLabel.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            this.statusLabel.Location = new Point(60, 20);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new Size(370, 20);
            this.statusLabel.TabIndex = 1;
            this.statusLabel.Text = "Preparing installation...";

            // detailLabel
            this.detailLabel.AutoSize = false;
            this.detailLabel.Font = new Font("Microsoft Sans Serif", 8F);
            this.detailLabel.ForeColor = Color.Gray;
            this.detailLabel.Location = new Point(60, 45);
            this.detailLabel.Name = "detailLabel";
            this.detailLabel.Size = new Size(370, 15);
            this.detailLabel.TabIndex = 2;
            this.detailLabel.Text = "This may take a few minutes...";

            // progressBar
            this.progressBar.Location = new Point(15, 75);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new Size(415, 23);
            this.progressBar.Style = ProgressBarStyle.Continuous;
            this.progressBar.TabIndex = 3;
            this.progressBar.Value = 0;

            // Add controls to form
            this.Controls.Add(this.iconPictureBox);
            this.Controls.Add(this.statusLabel);
            this.Controls.Add(this.detailLabel);
            this.Controls.Add(this.progressBar);

            ((System.ComponentModel.ISupportInitialize)(this.iconPictureBox)).EndInit();
            this.ResumeLayout(false);
        }

        public void UpdateStatus(string status)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(UpdateStatus), status);
                return;
            }

            this.statusLabel.Text = status;
            Application.DoEvents();
        }

        public void UpdateDetail(string detail)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(UpdateDetail), detail);
                return;
            }

            this.detailLabel.Text = detail;
            Application.DoEvents();
        }

        public void UpdateProgress(int percentage)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<int>(UpdateProgress), percentage);
                return;
            }

            this.progressBar.Value = Math.Max(0, Math.Min(100, percentage));
            Application.DoEvents();
        }

        public void SetIndeterminate(bool indeterminate)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<bool>(SetIndeterminate), indeterminate);
                return;
            }

            this.progressBar.Style = indeterminate ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            Application.DoEvents();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            this.Refresh();
            Application.DoEvents();
        }

        // Prevent user from closing the dialog during installation
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; // Prevent manual closing
            }
            base.OnFormClosing(e);
        }
    }
}
