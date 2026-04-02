namespace WindowsFormsApp1
{
    partial class ScriptsForm
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ScriptsForm));
            this.dgvScripts = new System.Windows.Forms.DataGridView();
            this.btnGlobalRecord = new System.Windows.Forms.Button();
            this.btnNewScript = new System.Windows.Forms.Button();
            this.btnBold = new System.Windows.Forms.Button();
            this.btnItalic = new System.Windows.Forms.Button();
            this.cmbBoxFonts = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.cmbBoxSize = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.cmbBoxColor = new System.Windows.Forms.ComboBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.panelToolbar = new System.Windows.Forms.Panel();
            this.btnAddTab = new System.Windows.Forms.Button();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.pictureBox2 = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.dgvScripts)).BeginInit();
            this.panelToolbar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).BeginInit();
            this.SuspendLayout();
            // 
            // dgvScripts
            // 
            this.dgvScripts.AllowUserToAddRows = false;
            this.dgvScripts.AllowUserToDeleteRows = false;
            this.dgvScripts.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvScripts.BackgroundColor = System.Drawing.Color.White;
            this.dgvScripts.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.dgvScripts.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.Black;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dgvScripts.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dgvScripts.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Segoe UI", 9F);
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.Black;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(153)))), ((int)(((byte)(255)))));
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dgvScripts.DefaultCellStyle = dataGridViewCellStyle2;
            this.dgvScripts.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvScripts.EnableHeadersVisualStyles = false;
            this.dgvScripts.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.dgvScripts.Location = new System.Drawing.Point(0, 0);
            this.dgvScripts.MultiSelect = false;
            this.dgvScripts.Name = "dgvScripts";
            this.dgvScripts.RowHeadersVisible = false;
            this.dgvScripts.RowHeadersWidth = 62;
            this.dgvScripts.RowTemplate.Height = 35;
            this.dgvScripts.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dgvScripts.Size = new System.Drawing.Size(971, 350);
            this.dgvScripts.TabIndex = 0;
            this.dgvScripts.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvScripts_CellClick);
            this.dgvScripts.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvScripts_CellEndEdit);
            this.dgvScripts.SelectionChanged += new System.EventHandler(this.dgvScripts_SelectionChanged);
            // 
            // btnGlobalRecord
            // 
            this.btnGlobalRecord.Enabled = false;
            this.btnGlobalRecord.Font = new System.Drawing.Font("Yu Gothic UI Semibold", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnGlobalRecord.Location = new System.Drawing.Point(9, 10);
            this.btnGlobalRecord.Margin = new System.Windows.Forms.Padding(2);
            this.btnGlobalRecord.Name = "btnGlobalRecord";
            this.btnGlobalRecord.Size = new System.Drawing.Size(159, 39);
            this.btnGlobalRecord.TabIndex = 1;
            this.btnGlobalRecord.Text = "Enable Recording / Editing";
            this.btnGlobalRecord.UseVisualStyleBackColor = true;
            this.btnGlobalRecord.Visible = false;
            this.btnGlobalRecord.Click += new System.EventHandler(this.btnGlobalRecord_Click);
            // 
            // btnNewScript
            // 
            this.btnNewScript.Font = new System.Drawing.Font("Yu Gothic UI Semibold", 9F, System.Drawing.FontStyle.Bold);
            this.btnNewScript.Location = new System.Drawing.Point(199, 10);
            this.btnNewScript.Margin = new System.Windows.Forms.Padding(2);
            this.btnNewScript.Name = "btnNewScript";
            this.btnNewScript.Size = new System.Drawing.Size(51, 28);
            this.btnNewScript.TabIndex = 20;
            this.btnNewScript.Text = "New Script";
            this.btnNewScript.UseVisualStyleBackColor = true;
            this.btnNewScript.Click += new System.EventHandler(this.btnNewScript_Click);
            // 
            // btnBold
            // 
            this.btnBold.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
            this.btnBold.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.btnBold.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnBold.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnBold.Location = new System.Drawing.Point(11, 4);
            this.btnBold.Margin = new System.Windows.Forms.Padding(2);
            this.btnBold.Name = "btnBold";
            this.btnBold.Size = new System.Drawing.Size(26, 24);
            this.btnBold.TabIndex = 7;
            this.btnBold.Text = "B";
            this.btnBold.UseVisualStyleBackColor = false;
            this.btnBold.Click += new System.EventHandler(this.btnBold_Click);
            // 
            // btnItalic
            // 
            this.btnItalic.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
            this.btnItalic.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.btnItalic.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnItalic.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic);
            this.btnItalic.Location = new System.Drawing.Point(46, 4);
            this.btnItalic.Margin = new System.Windows.Forms.Padding(2);
            this.btnItalic.Name = "btnItalic";
            this.btnItalic.Size = new System.Drawing.Size(26, 24);
            this.btnItalic.TabIndex = 8;
            this.btnItalic.Text = "I";
            this.btnItalic.UseVisualStyleBackColor = false;
            this.btnItalic.Click += new System.EventHandler(this.btnItalic_Click);
            // 
            // cmbBoxFonts
            // 
            this.cmbBoxFonts.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.cmbBoxFonts.FormattingEnabled = true;
            this.cmbBoxFonts.Location = new System.Drawing.Point(147, 4);
            this.cmbBoxFonts.Margin = new System.Windows.Forms.Padding(2);
            this.cmbBoxFonts.Name = "cmbBoxFonts";
            this.cmbBoxFonts.Size = new System.Drawing.Size(129, 23);
            this.cmbBoxFonts.TabIndex = 9;
            this.cmbBoxFonts.SelectedIndexChanged += new System.EventHandler(this.cmbBoxFonts_SelectedIndexChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.label2.Location = new System.Drawing.Point(109, 8);
            this.label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(31, 15);
            this.label2.TabIndex = 10;
            this.label2.Text = "Font";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.label3.Location = new System.Drawing.Point(294, 8);
            this.label3.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(27, 15);
            this.label3.TabIndex = 11;
            this.label3.Text = "Size";
            this.label3.Click += new System.EventHandler(this.label3_Click);
            // 
            // cmbBoxSize
            // 
            this.cmbBoxSize.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.cmbBoxSize.FormattingEnabled = true;
            this.cmbBoxSize.Location = new System.Drawing.Point(326, 4);
            this.cmbBoxSize.Margin = new System.Windows.Forms.Padding(2);
            this.cmbBoxSize.Name = "cmbBoxSize";
            this.cmbBoxSize.Size = new System.Drawing.Size(57, 23);
            this.cmbBoxSize.TabIndex = 12;
            this.cmbBoxSize.SelectedIndexChanged += new System.EventHandler(this.cmbBoxSize_SelectedIndexChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.label5.Location = new System.Drawing.Point(404, 9);
            this.label5.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(36, 15);
            this.label5.TabIndex = 18;
            this.label5.Text = "Color";
            // 
            // cmbBoxColor
            // 
            this.cmbBoxColor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbBoxColor.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.cmbBoxColor.FormattingEnabled = true;
            this.cmbBoxColor.Location = new System.Drawing.Point(444, 4);
            this.cmbBoxColor.Margin = new System.Windows.Forms.Padding(2);
            this.cmbBoxColor.Name = "cmbBoxColor";
            this.cmbBoxColor.Size = new System.Drawing.Size(88, 23);
            this.cmbBoxColor.TabIndex = 17;
            this.cmbBoxColor.SelectedIndexChanged += new System.EventHandler(this.cmbBoxColor_SelectedIndexChanged);
            // 
            // panelToolbar
            // 
            this.panelToolbar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelToolbar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
            this.panelToolbar.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelToolbar.Controls.Add(this.btnBold);
            this.panelToolbar.Controls.Add(this.btnItalic);
            this.panelToolbar.Controls.Add(this.label2);
            this.panelToolbar.Controls.Add(this.cmbBoxFonts);
            this.panelToolbar.Controls.Add(this.label3);
            this.panelToolbar.Controls.Add(this.cmbBoxSize);
            this.panelToolbar.Controls.Add(this.label5);
            this.panelToolbar.Controls.Add(this.cmbBoxColor);
            this.panelToolbar.Location = new System.Drawing.Point(9, 67);
            this.panelToolbar.Margin = new System.Windows.Forms.Padding(2);
            this.panelToolbar.Name = "panelToolbar";
            this.panelToolbar.Size = new System.Drawing.Size(729, 33);
            this.panelToolbar.TabIndex = 19;
            // 
            // btnAddTab
            // 
            this.btnAddTab.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.btnAddTab.FlatAppearance.BorderSize = 0;
            this.btnAddTab.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAddTab.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.btnAddTab.ForeColor = System.Drawing.Color.White;
            this.btnAddTab.Location = new System.Drawing.Point(260, 11);
            this.btnAddTab.Margin = new System.Windows.Forms.Padding(2);
            this.btnAddTab.Name = "btnAddTab";
            this.btnAddTab.Size = new System.Drawing.Size(26, 28);
            this.btnAddTab.TabIndex = 21;
            this.btnAddTab.Text = "+";
            this.btnAddTab.UseVisualStyleBackColor = false;
            this.btnAddTab.Visible = false;
            this.btnAddTab.Click += new System.EventHandler(this.btnAddTab_Click);
            // 
            // tabControl
            // 
            this.tabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.tabControl.Location = new System.Drawing.Point(9, 107);
            this.tabControl.Margin = new System.Windows.Forms.Padding(2);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(728, 317);
            this.tabControl.TabIndex = 22;
            this.tabControl.SelectedIndexChanged += new System.EventHandler(this.tabControl_SelectedIndexChanged);
            // 
            // pictureBox2
            // 
            this.pictureBox2.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("pictureBox2.BackgroundImage")));
            this.pictureBox2.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.pictureBox2.Location = new System.Drawing.Point(261, -33);
            this.pictureBox2.Margin = new System.Windows.Forms.Padding(2);
            this.pictureBox2.Name = "pictureBox2";
            this.pictureBox2.Size = new System.Drawing.Size(232, 135);
            this.pictureBox2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.pictureBox2.TabIndex = 42;
            this.pictureBox2.TabStop = false;
            // 
            // ScriptsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(746, 456);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.panelToolbar);
            this.Controls.Add(this.btnNewScript);
            this.Controls.Add(this.btnAddTab);
            this.Controls.Add(this.btnGlobalRecord);
            this.Controls.Add(this.pictureBox2);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MinimumSize = new System.Drawing.Size(762, 495);
            this.Name = "ScriptsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Scripts Manager";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ScriptsForm_FormClosing);
            this.Load += new System.EventHandler(this.ScriptsForm_Load);
            this.Resize += new System.EventHandler(this.ScriptsForm_Resize);
            ((System.ComponentModel.ISupportInitialize)(this.dgvScripts)).EndInit();
            this.panelToolbar.ResumeLayout(false);
            this.panelToolbar.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dgvScripts;
        private System.Windows.Forms.Button btnGlobalRecord;
        private System.Windows.Forms.Button btnNewScript;
        private System.Windows.Forms.Button btnBold;
        private System.Windows.Forms.Button btnItalic;
        private System.Windows.Forms.ComboBox cmbBoxFonts;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox cmbBoxSize;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox cmbBoxColor;
        private System.Windows.Forms.Panel panelToolbar;
        private System.Windows.Forms.Button btnAddTab;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.PictureBox pictureBox2;
    }
}
