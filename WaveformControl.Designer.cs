namespace WindowsFormsApp1
{
    partial class WaveformControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.pictureBoxWaveform = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxWaveform)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBoxWaveform
            // 
            this.pictureBoxWaveform.Location = new System.Drawing.Point(33, 16);
            this.pictureBoxWaveform.Name = "pictureBoxWaveform";
            this.pictureBoxWaveform.Size = new System.Drawing.Size(223, 50);
            this.pictureBoxWaveform.TabIndex = 0;
            this.pictureBoxWaveform.TabStop = false;
            // 
            // WaveformControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.pictureBoxWaveform);
            this.Name = "WaveformControl";
            this.Size = new System.Drawing.Size(301, 90);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxWaveform)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBoxWaveform;
    }
}
