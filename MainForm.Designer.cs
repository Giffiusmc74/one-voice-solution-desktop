namespace WindowsFormsApp1
{
    partial class MainForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.button1 = new System.Windows.Forms.Button();
            this.comboBoxMicroPhone = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.btnScripts = new System.Windows.Forms.Button();
            this.lblTimeRemaining = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.cmbBoxSpeaker = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.btnStop = new System.Windows.Forms.Button();
            this.pictureBox2 = new System.Windows.Forms.PictureBox();
            this.label13 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.cmbBoxSpeaker2 = new System.Windows.Forms.ComboBox();
            this.volumeA1RecIn = new WindowsFormsApp1.VolumeControl();
            this.volumeA2Speaker = new WindowsFormsApp1.VolumeControl();
            this.volumeA1RecOut = new WindowsFormsApp1.VolumeControl();
            this.volumeA1Speaker = new WindowsFormsApp1.VolumeControl();
            this.volumeA1Mic = new WindowsFormsApp1.VolumeControl();
            this.audioIntensityMeterRecOut = new WindowsFormsApp1.src.AudioIntensityMeter();
            this.audioIntensityMeterRecIn = new WindowsFormsApp1.src.AudioIntensityMeter();
            this.audioIntensityMeterMic1 = new WindowsFormsApp1.src.AudioIntensityMeter();
            this.audioIntensityMeterSpeaker22 = new WindowsFormsApp1.src.AudioIntensityMeter();
            this.audioIntensityMeterSpeaker11 = new WindowsFormsApp1.src.AudioIntensityMeter();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).BeginInit();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("button1.BackgroundImage")));
            this.button1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.button1.Cursor = System.Windows.Forms.Cursors.Hand;
            this.button1.Enabled = false;
            this.button1.FlatAppearance.BorderSize = 0;
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button1.ForeColor = System.Drawing.Color.Black;
            this.button1.Location = new System.Drawing.Point(817, 62);
            this.button1.Margin = new System.Windows.Forms.Padding(2);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(195, 49);
            this.button1.TabIndex = 0;
            this.button1.Text = "Macros (Recordings)";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Visible = false;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            this.button1.MouseEnter += new System.EventHandler(this.button1_MouseEnter);
            this.button1.MouseLeave += new System.EventHandler(this.button1_MouseLeave);
            // 
            // comboBoxMicroPhone
            // 
            this.comboBoxMicroPhone.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxMicroPhone.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.comboBoxMicroPhone.ForeColor = System.Drawing.Color.Black;
            this.comboBoxMicroPhone.FormattingEnabled = true;
            this.comboBoxMicroPhone.Location = new System.Drawing.Point(38, 175);
            this.comboBoxMicroPhone.Margin = new System.Windows.Forms.Padding(2);
            this.comboBoxMicroPhone.Name = "comboBoxMicroPhone";
            this.comboBoxMicroPhone.Size = new System.Drawing.Size(232, 25);
            this.comboBoxMicroPhone.TabIndex = 2;
            this.comboBoxMicroPhone.SelectedValueChanged += new System.EventHandler(this.comboBoxMicroPhone_SelectedValueChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.BackColor = System.Drawing.Color.Transparent;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(44, 145);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(231, 24);
            this.label1.TabIndex = 4;
            this.label1.Text = "My Microphone";
            // 
            // btnScripts
            // 
            this.btnScripts.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("btnScripts.BackgroundImage")));
            this.btnScripts.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.btnScripts.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnScripts.FlatAppearance.BorderSize = 0;
            this.btnScripts.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.btnScripts.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.btnScripts.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnScripts.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnScripts.ForeColor = System.Drawing.Color.Black;
            this.btnScripts.Location = new System.Drawing.Point(817, 123);
            this.btnScripts.Margin = new System.Windows.Forms.Padding(2);
            this.btnScripts.Name = "btnScripts";
            this.btnScripts.Size = new System.Drawing.Size(195, 46);
            this.btnScripts.TabIndex = 6;
            this.btnScripts.Text = "Scripts";
            this.btnScripts.Visible = false; // Scripts now managed via web portal member dashboard
            this.btnScripts.UseVisualStyleBackColor = true;
            this.btnScripts.Click += new System.EventHandler(this.btnScripts_Click);
            this.btnScripts.MouseEnter += new System.EventHandler(this.btnScripts_MouseEnter);
            this.btnScripts.MouseLeave += new System.EventHandler(this.btnScripts_MouseLeave);
            // 
            // lblTimeRemaining
            // 
            this.lblTimeRemaining.AutoSize = true;
            this.lblTimeRemaining.BackColor = System.Drawing.Color.Transparent;
            this.lblTimeRemaining.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTimeRemaining.ForeColor = System.Drawing.Color.Red;
            this.lblTimeRemaining.Location = new System.Drawing.Point(887, 667);
            this.lblTimeRemaining.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblTimeRemaining.Name = "lblTimeRemaining";
            this.lblTimeRemaining.Size = new System.Drawing.Size(132, 17);
            this.lblTimeRemaining.TabIndex = 8;
            this.lblTimeRemaining.Text = "Rec. Time: 00:00";
            this.lblTimeRemaining.Visible = false;
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 1000;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // pictureBox1
            // 
            this.pictureBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox1.Image")));
            this.pictureBox1.Location = new System.Drawing.Point(304, 117);
            this.pictureBox1.Margin = new System.Windows.Forms.Padding(2);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(476, 295);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox1.TabIndex = 9;
            this.pictureBox1.TabStop = false;
            this.pictureBox1.Click += new System.EventHandler(this.pictureBox1_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.BackColor = System.Drawing.Color.Transparent;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 16.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.ForeColor = System.Drawing.Color.White;
            this.label2.Location = new System.Drawing.Point(128, 422);
            this.label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(249, 26);
            this.label2.TabIndex = 12;
            this.label2.Text = "Customer Voice (What I Hear)"; // A1 — customer voice in agent headset
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.BackColor = System.Drawing.Color.Transparent;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.ForeColor = System.Drawing.Color.White;
            this.label3.Location = new System.Drawing.Point(56, 229);
            this.label3.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(196, 24);
            this.label3.TabIndex = 13;
            this.label3.Text = "My Headset / Speaker";
            // 
            // cmbBoxSpeaker
            // 
            this.cmbBoxSpeaker.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbBoxSpeaker.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmbBoxSpeaker.FormattingEnabled = true;
            this.cmbBoxSpeaker.Location = new System.Drawing.Point(38, 258);
            this.cmbBoxSpeaker.Margin = new System.Windows.Forms.Padding(2);
            this.cmbBoxSpeaker.Name = "cmbBoxSpeaker";
            this.cmbBoxSpeaker.Size = new System.Drawing.Size(232, 25);
            this.cmbBoxSpeaker.TabIndex = 14;
            this.cmbBoxSpeaker.SelectedValueChanged += new System.EventHandler(this.cmbBoxSpeaker_SelectedValueChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.BackColor = System.Drawing.Color.Transparent;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 16.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.ForeColor = System.Drawing.Color.White;
            this.label6.Location = new System.Drawing.Point(15, 505);
            this.label6.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(532, 26);
            this.label6.TabIndex = 20;
            this.label6.Text = "Script Playback Volume (My Ear)"; // A1 — recording volume in agent headset
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.BackColor = System.Drawing.Color.Transparent;
            this.label7.Enabled = false;
            this.label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 16.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label7.ForeColor = System.Drawing.Color.White;
            this.label7.Location = new System.Drawing.Point(128, 587);
            this.label7.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(272, 26);
            this.label7.TabIndex = 22;
            this.label7.Text = "A2 Volume (Listen Only)"; // A2 — extra agent listening (hidden by default)
            this.label7.Visible = false;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.BackColor = System.Drawing.Color.Transparent;
            this.label8.Font = new System.Drawing.Font("Microsoft Sans Serif", 16.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label8.ForeColor = System.Drawing.Color.White;
            this.label8.Location = new System.Drawing.Point(677, 422);
            this.label8.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(305, 26);
            this.label8.TabIndex = 24;
            this.label8.Text = "My Mic Level (Customer Hears Me)"; // C1 — agent voice going to customer
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.BackColor = System.Drawing.Color.Transparent;
            this.label9.Font = new System.Drawing.Font("Microsoft Sans Serif", 16.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label9.ForeColor = System.Drawing.Color.White;
            this.label9.Location = new System.Drawing.Point(669, 505);
            this.label9.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(228, 26);
            this.label9.TabIndex = 26;
            this.label9.Text = "Script Playback (Customer Hears)"; // C2 — recording volume going to customer
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.BackColor = System.Drawing.Color.Transparent;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.ForeColor = System.Drawing.Color.White;
            this.label5.Location = new System.Drawing.Point(814, 307);
            this.label5.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(142, 20);
            this.label5.TabIndex = 36;
            this.label5.Text = "A1 = Main Agent";
            this.label5.Visible = false; // Labels now plain English — key legend no longer needed
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.BackColor = System.Drawing.Color.Transparent;
            this.label10.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label10.ForeColor = System.Drawing.Color.White;
            this.label10.Location = new System.Drawing.Point(814, 331);
            this.label10.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(236, 20);
            this.label10.TabIndex = 37;
            this.label10.Text = "A2 = Extra Agent (Listening)";
            this.label10.Visible = false;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.BackColor = System.Drawing.Color.Transparent;
            this.label11.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label11.ForeColor = System.Drawing.Color.White;
            this.label11.Location = new System.Drawing.Point(814, 354);
            this.label11.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(128, 20);
            this.label11.TabIndex = 38;
            this.label11.Text = "C1 = Customer";
            this.label11.Visible = false; // Labels now plain English — key legend no longer needed
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.BackColor = System.Drawing.Color.Transparent;
            this.label12.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label12.ForeColor = System.Drawing.Color.White;
            this.label12.Location = new System.Drawing.Point(812, 270);
            this.label12.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(57, 29);
            this.label12.TabIndex = 39;
            this.label12.Text = "Key";
            this.label12.Visible = false; // Labels now plain English — key legend no longer needed
            // 
            // btnStop
            // 
            this.btnStop.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.btnStop.Location = new System.Drawing.Point(1006, 682);
            this.btnStop.Margin = new System.Windows.Forms.Padding(2);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(50, 24);
            this.btnStop.TabIndex = 40;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Visible = false;
            this.btnStop.Click += new System.EventHandler(this.btnPause_Click);
            // 
            // pictureBox2
            // 
            this.pictureBox2.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox2.BackgroundImage")));
            this.pictureBox2.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.pictureBox2.Location = new System.Drawing.Point(382, 10);
            this.pictureBox2.Margin = new System.Windows.Forms.Padding(2);
            this.pictureBox2.Name = "pictureBox2";
            this.pictureBox2.Size = new System.Drawing.Size(315, 101);
            this.pictureBox2.TabIndex = 41;
            this.pictureBox2.TabStop = false;
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.BackColor = System.Drawing.Color.Transparent;
            this.label13.Font = new System.Drawing.Font("Microsoft Sans Serif", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label13.ForeColor = System.Drawing.Color.White;
            this.label13.Location = new System.Drawing.Point(638, 602);
            this.label13.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(336, 24);
            this.label13.TabIndex = 42;
            this.label13.Text = "The Geniusness is in the Simplicity";
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.BackColor = System.Drawing.Color.Transparent;
            this.label14.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label14.ForeColor = System.Drawing.Color.White;
            this.label14.Location = new System.Drawing.Point(754, 646);
            this.label14.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(115, 20);
            this.label14.TabIndex = 43;
            this.label14.Text = "Version 2025";
            // 
            // label15
            // 
            this.label15.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.label15.AutoSize = true;
            this.label15.BackColor = System.Drawing.Color.Transparent;
            this.label15.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label15.ForeColor = System.Drawing.Color.White;
            this.label15.Location = new System.Drawing.Point(470, 646);
            this.label15.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(157, 20);
            this.label15.TabIndex = 44;
            this.label15.Text = "One United Global";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.BackColor = System.Drawing.Color.Transparent;
            this.label4.Enabled = false;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.ForeColor = System.Drawing.Color.White;
            this.label4.Location = new System.Drawing.Point(56, 312);
            this.label4.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(196, 24);
            this.label4.TabIndex = 17;
            this.label4.Text = "Select Speaker (A2)";
            this.label4.Visible = false;
            // 
            // cmbBoxSpeaker2
            // 
            this.cmbBoxSpeaker2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbBoxSpeaker2.Enabled = false;
            this.cmbBoxSpeaker2.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmbBoxSpeaker2.FormattingEnabled = true;
            this.cmbBoxSpeaker2.Location = new System.Drawing.Point(38, 340);
            this.cmbBoxSpeaker2.Margin = new System.Windows.Forms.Padding(2);
            this.cmbBoxSpeaker2.Name = "cmbBoxSpeaker2";
            this.cmbBoxSpeaker2.Size = new System.Drawing.Size(232, 25);
            this.cmbBoxSpeaker2.TabIndex = 18;
            this.cmbBoxSpeaker2.Visible = false;
            this.cmbBoxSpeaker2.SelectedValueChanged += new System.EventHandler(this.cmbBoxSpeaker2_SelectedValueChanged);
            // 
            // volumeA1RecIn
            // 
            this.volumeA1RecIn.BackColor = System.Drawing.Color.Black;
            this.volumeA1RecIn.Bar_Color = System.Drawing.Color.RoyalBlue;
            this.volumeA1RecIn.Location = new System.Drawing.Point(38, 550);
            this.volumeA1RecIn.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.volumeA1RecIn.Max = 100;
            this.volumeA1RecIn.Min = 0;
            this.volumeA1RecIn.Name = "volumeA1RecIn";
            this.volumeA1RecIn.Size = new System.Drawing.Size(414, 19);
            this.volumeA1RecIn.TabIndex = 35;
            this.volumeA1RecIn.Value = 50;
            this.volumeA1RecIn.MouseMove += new System.Windows.Forms.MouseEventHandler(this.volumeA1RecIn_MouseMove);
            // 
            // volumeA2Speaker
            // 
            this.volumeA2Speaker.BackColor = System.Drawing.Color.Black;
            this.volumeA2Speaker.Bar_Color = System.Drawing.Color.RoyalBlue;
            this.volumeA2Speaker.Enabled = false;
            this.volumeA2Speaker.Location = new System.Drawing.Point(38, 633);
            this.volumeA2Speaker.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.volumeA2Speaker.Max = 100;
            this.volumeA2Speaker.Min = 0;
            this.volumeA2Speaker.Name = "volumeA2Speaker";
            this.volumeA2Speaker.Size = new System.Drawing.Size(414, 19);
            this.volumeA2Speaker.TabIndex = 34;
            this.volumeA2Speaker.Value = 50;
            this.volumeA2Speaker.Visible = false;
            this.volumeA2Speaker.MouseMove += new System.Windows.Forms.MouseEventHandler(this.volumeA2Speaker_MouseMove);
            // 
            // volumeA1RecOut
            // 
            this.volumeA1RecOut.BackColor = System.Drawing.Color.Black;
            this.volumeA1RecOut.Bar_Color = System.Drawing.Color.RoyalBlue;
            this.volumeA1RecOut.Location = new System.Drawing.Point(613, 550);
            this.volumeA1RecOut.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.volumeA1RecOut.Max = 100;
            this.volumeA1RecOut.Min = 0;
            this.volumeA1RecOut.Name = "volumeA1RecOut";
            this.volumeA1RecOut.Size = new System.Drawing.Size(414, 19);
            this.volumeA1RecOut.TabIndex = 33;
            this.volumeA1RecOut.Value = 50;
            this.volumeA1RecOut.MouseMove += new System.Windows.Forms.MouseEventHandler(this.volumeA1RecOut_MouseMove);
            // 
            // volumeA1Speaker
            // 
            this.volumeA1Speaker.BackColor = System.Drawing.Color.Black;
            this.volumeA1Speaker.Bar_Color = System.Drawing.Color.RoyalBlue;
            this.volumeA1Speaker.Location = new System.Drawing.Point(38, 471);
            this.volumeA1Speaker.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.volumeA1Speaker.Max = 100;
            this.volumeA1Speaker.Min = 0;
            this.volumeA1Speaker.Name = "volumeA1Speaker";
            this.volumeA1Speaker.Size = new System.Drawing.Size(414, 19);
            this.volumeA1Speaker.TabIndex = 32;
            this.volumeA1Speaker.Value = 50;
            this.volumeA1Speaker.MouseMove += new System.Windows.Forms.MouseEventHandler(this.volumeA1Speaker_MouseMove);
            // 
            // volumeA1Mic
            // 
            this.volumeA1Mic.BackColor = System.Drawing.Color.Black;
            this.volumeA1Mic.Bar_Color = System.Drawing.Color.RoyalBlue;
            this.volumeA1Mic.Location = new System.Drawing.Point(613, 467);
            this.volumeA1Mic.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.volumeA1Mic.Max = 100;
            this.volumeA1Mic.Min = 0;
            this.volumeA1Mic.Name = "volumeA1Mic";
            this.volumeA1Mic.Size = new System.Drawing.Size(414, 19);
            this.volumeA1Mic.TabIndex = 30;
            this.volumeA1Mic.Value = 50;
            this.volumeA1Mic.MouseMove += new System.Windows.Forms.MouseEventHandler(this.volumeA1Mic_MouseMove);
            // 
            // audioIntensityMeterRecOut
            // 
            this.audioIntensityMeterRecOut.BackColor = System.Drawing.Color.Black;
            this.audioIntensityMeterRecOut.Location = new System.Drawing.Point(613, 540);
            this.audioIntensityMeterRecOut.Margin = new System.Windows.Forms.Padding(2);
            this.audioIntensityMeterRecOut.Name = "audioIntensityMeterRecOut";
            this.audioIntensityMeterRecOut.Size = new System.Drawing.Size(414, 8);
            this.audioIntensityMeterRecOut.TabIndex = 29;
            // 
            // audioIntensityMeterRecIn
            // 
            this.audioIntensityMeterRecIn.BackColor = System.Drawing.Color.Black;
            this.audioIntensityMeterRecIn.Location = new System.Drawing.Point(38, 540);
            this.audioIntensityMeterRecIn.Margin = new System.Windows.Forms.Padding(2);
            this.audioIntensityMeterRecIn.Name = "audioIntensityMeterRecIn";
            this.audioIntensityMeterRecIn.Size = new System.Drawing.Size(414, 8);
            this.audioIntensityMeterRecIn.TabIndex = 29;
            // 
            // audioIntensityMeterMic1
            // 
            this.audioIntensityMeterMic1.BackColor = System.Drawing.Color.Black;
            this.audioIntensityMeterMic1.Location = new System.Drawing.Point(613, 457);
            this.audioIntensityMeterMic1.Margin = new System.Windows.Forms.Padding(2);
            this.audioIntensityMeterMic1.Name = "audioIntensityMeterMic1";
            this.audioIntensityMeterMic1.Size = new System.Drawing.Size(414, 8);
            this.audioIntensityMeterMic1.TabIndex = 28;
            // 
            // audioIntensityMeterSpeaker22
            // 
            this.audioIntensityMeterSpeaker22.BackColor = System.Drawing.Color.Black;
            this.audioIntensityMeterSpeaker22.Enabled = false;
            this.audioIntensityMeterSpeaker22.Location = new System.Drawing.Point(38, 622);
            this.audioIntensityMeterSpeaker22.Margin = new System.Windows.Forms.Padding(2);
            this.audioIntensityMeterSpeaker22.Name = "audioIntensityMeterSpeaker22";
            this.audioIntensityMeterSpeaker22.Size = new System.Drawing.Size(414, 8);
            this.audioIntensityMeterSpeaker22.TabIndex = 28;
            this.audioIntensityMeterSpeaker22.Visible = false;
            // 
            // audioIntensityMeterSpeaker11
            // 
            this.audioIntensityMeterSpeaker11.BackColor = System.Drawing.Color.Black;
            this.audioIntensityMeterSpeaker11.Location = new System.Drawing.Point(38, 461);
            this.audioIntensityMeterSpeaker11.Margin = new System.Windows.Forms.Padding(2);
            this.audioIntensityMeterSpeaker11.Name = "audioIntensityMeterSpeaker11";
            this.audioIntensityMeterSpeaker11.Size = new System.Drawing.Size(414, 8);
            this.audioIntensityMeterSpeaker11.TabIndex = 27;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.ClientSize = new System.Drawing.Size(1058, 690);
            this.Controls.Add(this.label15);
            this.Controls.Add(this.label14);
            this.Controls.Add(this.label13);
            this.Controls.Add(this.pictureBox2);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.label12);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.volumeA1RecIn);
            this.Controls.Add(this.volumeA2Speaker);
            this.Controls.Add(this.volumeA1RecOut);
            this.Controls.Add(this.volumeA1Speaker);
            this.Controls.Add(this.volumeA1Mic);
            this.Controls.Add(this.audioIntensityMeterRecOut);
            this.Controls.Add(this.audioIntensityMeterRecIn);
            this.Controls.Add(this.audioIntensityMeterMic1);
            this.Controls.Add(this.audioIntensityMeterSpeaker22);
            this.Controls.Add(this.audioIntensityMeterSpeaker11);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.cmbBoxSpeaker2);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.cmbBoxSpeaker);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.lblTimeRemaining);
            this.Controls.Add(this.btnScripts);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.comboBoxMicroPhone);
            this.Controls.Add(this.button1);
            this.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MinimumSize = new System.Drawing.Size(900, 700);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "One App";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form_Closing);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.ComboBox comboBoxMicroPhone;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnScripts;
        private System.Windows.Forms.Label lblTimeRemaining;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox cmbBoxSpeaker;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label9;
        private src.AudioIntensityMeter audioIntensityMeterSpeaker11;
        private src.AudioIntensityMeter audioIntensityMeterMic1;
        private src.AudioIntensityMeter audioIntensityMeterSpeaker22;
        private src.AudioIntensityMeter audioIntensityMeterRecIn;
        private src.AudioIntensityMeter audioIntensityMeterRecOut;
        private VolumeControl volumeA1Mic;
        private VolumeControl volumeA1Speaker;
        private VolumeControl volumeA1RecOut;
        private VolumeControl volumeA1RecIn;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.PictureBox pictureBox2;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label15;
        private VolumeControl volumeA2Speaker;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox cmbBoxSpeaker2;
    }
}