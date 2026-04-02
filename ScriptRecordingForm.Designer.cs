namespace WindowsFormsApp1
{
    partial class ScriptRecordingForm
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
            this.components = new System.ComponentModel.Container();
            this.lblScriptName = new System.Windows.Forms.Label();
            this.txtScriptName = new System.Windows.Forms.TextBox();
            this.lblAudioPath = new System.Windows.Forms.Label();
            this.txtAudioPath = new System.Windows.Forms.TextBox();
            this.btnRecord = new System.Windows.Forms.Button();
            this.btnStopRecord = new System.Windows.Forms.Button();
            this.btnUpload = new System.Windows.Forms.Button();
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.picRecording = new System.Windows.Forms.PictureBox();
            this.lblTimer = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.groupBoxRecording = new System.Windows.Forms.GroupBox();
            this.groupBoxAudio = new System.Windows.Forms.GroupBox();
            ((System.ComponentModel.ISupportInitialize)(this.picRecording)).BeginInit();
            this.groupBoxRecording.SuspendLayout();
            this.groupBoxAudio.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblScriptName
            // 
            this.lblScriptName.AutoSize = true;
            this.lblScriptName.Font = new System.Drawing.Font("Yu Gothic UI Semibold", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblScriptName.Location = new System.Drawing.Point(15, 20);
            this.lblScriptName.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblScriptName.Name = "lblScriptName";
            this.lblScriptName.Size = new System.Drawing.Size(76, 15);
            this.lblScriptName.TabIndex = 0;
            this.lblScriptName.Text = "Script Name:";
            // 
            // txtScriptName
            // 
            this.txtScriptName.Font = new System.Drawing.Font("Yu Gothic UI Semibold", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtScriptName.Location = new System.Drawing.Point(105, 18);
            this.txtScriptName.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.txtScriptName.Name = "txtScriptName";
            this.txtScriptName.Size = new System.Drawing.Size(226, 23);
            this.txtScriptName.TabIndex = 1;
            // 
            // lblAudioPath
            // 
            this.lblAudioPath.AutoSize = true;
            this.lblAudioPath.Font = new System.Drawing.Font("Yu Gothic UI Semibold", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblAudioPath.Location = new System.Drawing.Point(15, 57);
            this.lblAudioPath.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblAudioPath.Name = "lblAudioPath";
            this.lblAudioPath.Size = new System.Drawing.Size(69, 15);
            this.lblAudioPath.TabIndex = 2;
            this.lblAudioPath.Text = "Audio Path:";
            // 
            // txtAudioPath
            // 
            this.txtAudioPath.Font = new System.Drawing.Font("Yu Gothic UI Semibold", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtAudioPath.Location = new System.Drawing.Point(105, 54);
            this.txtAudioPath.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.txtAudioPath.Name = "txtAudioPath";
            this.txtAudioPath.ReadOnly = true;
            this.txtAudioPath.Size = new System.Drawing.Size(226, 23);
            this.txtAudioPath.TabIndex = 3;
            // 
            // btnRecord
            // 
            this.btnRecord.Font = new System.Drawing.Font("Yu Gothic UI Semibold", 9F, System.Drawing.FontStyle.Bold);
            this.btnRecord.Location = new System.Drawing.Point(11, 20);
            this.btnRecord.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnRecord.Name = "btnRecord";
            this.btnRecord.Size = new System.Drawing.Size(60, 41);
            this.btnRecord.TabIndex = 4;
            this.btnRecord.Text = "Record";
            this.btnRecord.UseVisualStyleBackColor = true;
            this.btnRecord.Click += new System.EventHandler(this.btnRecord_Click);
            // 
            // btnStopRecord
            // 
            this.btnStopRecord.Font = new System.Drawing.Font("Yu Gothic UI Semibold", 9F, System.Drawing.FontStyle.Bold);
            this.btnStopRecord.Location = new System.Drawing.Point(79, 20);
            this.btnStopRecord.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnStopRecord.Name = "btnStopRecord";
            this.btnStopRecord.Size = new System.Drawing.Size(60, 41);
            this.btnStopRecord.TabIndex = 5;
            this.btnStopRecord.Text = "Stop";
            this.btnStopRecord.UseVisualStyleBackColor = true;
            this.btnStopRecord.Click += new System.EventHandler(this.btnStopRecord_Click);
            // 
            // btnUpload
            // 
            this.btnUpload.Font = new System.Drawing.Font("Yu Gothic UI Semibold", 9F, System.Drawing.FontStyle.Bold);
            this.btnUpload.Location = new System.Drawing.Point(11, 20);
            this.btnUpload.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnUpload.Name = "btnUpload";
            this.btnUpload.Size = new System.Drawing.Size(94, 28);
            this.btnUpload.TabIndex = 6;
            this.btnUpload.Text = "Upload Audio";
            this.btnUpload.UseVisualStyleBackColor = true;
            this.btnUpload.Click += new System.EventHandler(this.btnUpload_Click);
            // 
            // btnAdd
            // 
            this.btnAdd.Font = new System.Drawing.Font("Yu Gothic UI Semibold", 9F, System.Drawing.FontStyle.Bold);
            this.btnAdd.Location = new System.Drawing.Point(210, 260);
            this.btnAdd.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(60, 28);
            this.btnAdd.TabIndex = 7;
            this.btnAdd.Text = "Add";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Font = new System.Drawing.Font("Yu Gothic UI Semibold", 9F, System.Drawing.FontStyle.Bold);
            this.btnCancel.Location = new System.Drawing.Point(278, 260);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(60, 28);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // picRecording
            // 
            this.picRecording.Location = new System.Drawing.Point(150, 20);
            this.picRecording.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.picRecording.Name = "picRecording";
            this.picRecording.Size = new System.Drawing.Size(38, 41);
            this.picRecording.TabIndex = 9;
            this.picRecording.TabStop = false;
            // 
            // lblTimer
            // 
            this.lblTimer.AutoSize = true;
            this.lblTimer.Font = new System.Drawing.Font("Yu Gothic UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            this.lblTimer.ForeColor = System.Drawing.Color.Red;
            this.lblTimer.Location = new System.Drawing.Point(195, 32);
            this.lblTimer.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblTimer.Name = "lblTimer";
            this.lblTimer.Size = new System.Drawing.Size(45, 20);
            this.lblTimer.TabIndex = 10;
            this.lblTimer.Text = "00:00";
            // 
            // timer1
            // 
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // groupBoxRecording
            // 
            this.groupBoxRecording.Controls.Add(this.btnRecord);
            this.groupBoxRecording.Controls.Add(this.lblTimer);
            this.groupBoxRecording.Controls.Add(this.btnStopRecord);
            this.groupBoxRecording.Controls.Add(this.picRecording);
            this.groupBoxRecording.Font = new System.Drawing.Font("Yu Gothic UI Semibold", 9F, System.Drawing.FontStyle.Bold);
            this.groupBoxRecording.Location = new System.Drawing.Point(18, 89);
            this.groupBoxRecording.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBoxRecording.Name = "groupBoxRecording";
            this.groupBoxRecording.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBoxRecording.Size = new System.Drawing.Size(312, 73);
            this.groupBoxRecording.TabIndex = 11;
            this.groupBoxRecording.TabStop = false;
            this.groupBoxRecording.Text = "Record New Audio";
            // 
            // groupBoxAudio
            // 
            this.groupBoxAudio.Controls.Add(this.btnUpload);
            this.groupBoxAudio.Font = new System.Drawing.Font("Yu Gothic UI Semibold", 9F, System.Drawing.FontStyle.Bold);
            this.groupBoxAudio.Location = new System.Drawing.Point(18, 179);
            this.groupBoxAudio.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBoxAudio.Name = "groupBoxAudio";
            this.groupBoxAudio.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBoxAudio.Size = new System.Drawing.Size(312, 61);
            this.groupBoxAudio.TabIndex = 12;
            this.groupBoxAudio.TabStop = false;
            this.groupBoxAudio.Text = "Upload Existing Audio";
            // 
            // ScriptRecordingForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(362, 301);
            this.Controls.Add(this.groupBoxAudio);
            this.Controls.Add(this.groupBoxRecording);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.txtAudioPath);
            this.Controls.Add(this.lblAudioPath);
            this.Controls.Add(this.txtScriptName);
            this.Controls.Add(this.lblScriptName);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ScriptRecordingForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Script Recording";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ScriptRecordingForm_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.picRecording)).EndInit();
            this.groupBoxRecording.ResumeLayout(false);
            this.groupBoxRecording.PerformLayout();
            this.groupBoxAudio.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblScriptName;
        private System.Windows.Forms.TextBox txtScriptName;
        private System.Windows.Forms.Label lblAudioPath;
        private System.Windows.Forms.TextBox txtAudioPath;
        private System.Windows.Forms.Button btnRecord;
        private System.Windows.Forms.Button btnStopRecord;
        private System.Windows.Forms.Button btnUpload;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.PictureBox picRecording;
        private System.Windows.Forms.Label lblTimer;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.GroupBox groupBoxRecording;
        private System.Windows.Forms.GroupBox groupBoxAudio;
    }
}
