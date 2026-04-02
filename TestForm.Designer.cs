namespace WindowsFormsApp1
{
    partial class TestForm
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnStart = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.txtWaveIn = new System.Windows.Forms.TextBox();
            this.txtWaveOut = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.txtDevices = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.btnPlayAudio = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(12, 190);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(249, 87);
            this.btnStart.TabIndex = 0;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // btnStop
            // 
            this.btnStop.Location = new System.Drawing.Point(12, 295);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(543, 87);
            this.btnStop.TabIndex = 0;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // txtWaveIn
            // 
            this.txtWaveIn.Location = new System.Drawing.Point(70, 25);
            this.txtWaveIn.Name = "txtWaveIn";
            this.txtWaveIn.Size = new System.Drawing.Size(100, 20);
            this.txtWaveIn.TabIndex = 1;
            // 
            // txtWaveOut
            // 
            this.txtWaveOut.Location = new System.Drawing.Point(70, 73);
            this.txtWaveOut.Name = "txtWaveOut";
            this.txtWaveOut.Size = new System.Drawing.Size(100, 20);
            this.txtWaveOut.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 28);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(48, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Wave In";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 76);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(56, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Wave Out";
            // 
            // txtDevices
            // 
            this.txtDevices.Location = new System.Drawing.Point(265, 25);
            this.txtDevices.Multiline = true;
            this.txtDevices.Name = "txtDevices";
            this.txtDevices.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtDevices.Size = new System.Drawing.Size(290, 134);
            this.txtDevices.TabIndex = 3;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(203, 28);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(58, 13);
            this.label3.TabIndex = 2;
            this.label3.Text = "Device Ids";
            // 
            // btnPlayAudio
            // 
            this.btnPlayAudio.Location = new System.Drawing.Point(306, 190);
            this.btnPlayAudio.Name = "btnPlayAudio";
            this.btnPlayAudio.Size = new System.Drawing.Size(249, 87);
            this.btnPlayAudio.TabIndex = 0;
            this.btnPlayAudio.Text = "Play Audio";
            this.btnPlayAudio.UseVisualStyleBackColor = true;
            this.btnPlayAudio.Click += new System.EventHandler(this.btnPlayAudio_Click);
            // 
            // TestForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(567, 394);
            this.Controls.Add(this.txtDevices);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtWaveOut);
            this.Controls.Add(this.txtWaveIn);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnPlayAudio);
            this.Controls.Add(this.btnStart);
            this.Name = "TestForm";
            this.Text = "TestForm";
            this.Load += new System.EventHandler(this.TestForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.TextBox txtWaveIn;
        private System.Windows.Forms.TextBox txtWaveOut;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtDevices;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnPlayAudio;
    }
}