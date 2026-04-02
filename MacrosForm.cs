using NAudio.Wave;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Configuration;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1.src;

namespace WindowsFormsApp1
{
    public partial class MacrosForm : Form
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        MacroManager manager;
        MacroDetails macrDetailsForm;
        int deviceNumber;

        private WaveOutEvent waveO;
        private AudioFileReader audioFileReader2;
        private AudioFileReader audioFileReader;
        private WaveInEvent waveIn;
        private WaveOutEvent waveOut;
        bool isAudioPlaying = false;

        private MacrosInfo audioMacroInfo;

        public MacrosForm(MacroManager mgr)
        {
            logger.Info("Macros form started. Intializing Objects.");
            InitializeComponent();
            manager = mgr;
            deviceNumber = AudioService.Instance.virtualCableInputDeviceNumber;
            waveIn = AudioService.Instance.waveIn;
        }

        private void MacrosForm_Load(object sender, EventArgs e)
        {
            try
            {
                LoadFormInitialControls();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void LoadFormInitialControls()
        {
            panel1.Controls.Clear();

            FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
            flowLayoutPanel.Dock = DockStyle.Fill;
            flowLayoutPanel.FlowDirection = FlowDirection.TopDown;
            flowLayoutPanel.AutoScroll = true;
            flowLayoutPanel.WrapContents = false;
            panel1.Controls.Add(flowLayoutPanel);

            flowLayoutPanel.Controls.Clear();

            // Loop according to number of Macros 
            FlowLayoutPanel currentRowPanel = null;
            for (int i = 0; i < manager.macroList.Count; i++)
            {
                if (i % 3 == 0)
                {
                    currentRowPanel = CreateRowPanel(flowLayoutPanel);
                    flowLayoutPanel.Controls.Add(currentRowPanel);
                }
                CreateMacroPanel(currentRowPanel, i);
            }
        }

        private void CreateMacroPanel(FlowLayoutPanel flowLayoutPanel, int macroIndex)
        {
            var MacroPanel = new Panel();
            int panelWidth = flowLayoutPanel.Width / 3 - 15;
            MacroPanel.Size = new Size(panelWidth, 50); // Adjust the width here
            MacroPanel.Name = manager.macroList[macroIndex].Name;
            MacroPanel.Click += (sender, e) => MacroPanel_Click(manager.macroList[macroIndex]);
            MacroPanel.BackColor = Color.FromName(manager.macroList[macroIndex].ColorName);
            MacroPanel.MouseHover += (sender, e) => { MacroPanel.BackColor = ControlPaint.Light(Color.FromName(manager.macroList[macroIndex].ColorName)); };
            MacroPanel.MouseLeave += (sender, e) => { MacroPanel.BackColor = Color.FromName(manager.macroList[macroIndex].ColorName); };
            MacroPanel.MouseEnter += (sender, e) => { MacroPanel.BackColor = ControlPaint.Light(Color.FromName(manager.macroList[macroIndex].ColorName)); };


            var macroLabel = new Label();
            // macroLabel.Location = new Point(panelWidth, 15);
            macroLabel.Text = manager.macroList[macroIndex].Name;
            //macroLabel.TextAlign = ContentAlignment.MiddleCenter;

            macroLabel.Font = new Font("Montserrat", 10, FontStyle.Bold);
            macroLabel.Click += (sender, e) => MacroPanel_Click(manager.macroList[macroIndex]);
            macroLabel.MouseHover += (sender, e) => { MacroPanel.BackColor = ControlPaint.Light(Color.FromName(manager.macroList[macroIndex].ColorName)); };
            macroLabel.MouseLeave += (sender, e) => { MacroPanel.BackColor = Color.FromName(manager.macroList[macroIndex].ColorName); };
            macroLabel.MouseEnter += (sender, e) => { MacroPanel.BackColor = ControlPaint.Light(Color.FromName(manager.macroList[macroIndex].ColorName)); };
            macroLabel.Cursor = Cursors.Hand;
            macroLabel.Location = new Point(MacroPanel.Size.Width / 4, MacroPanel.Size.Height / 3);
            macroLabel.BackColor = System.Drawing.Color.Transparent;
            macroLabel.ForeColor = System.Drawing.Color.Black;
            int labelWidth = flowLayoutPanel.Width / 4;
            macroLabel.Size = new Size(panelWidth - 10, 30);

            MacroPanel.Controls.Add(macroLabel);

            flowLayoutPanel.Controls.Add(MacroPanel);
        }
        private FlowLayoutPanel CreateRowPanel(FlowLayoutPanel mainLayoutPanel)
        {
            var rowPanel = new FlowLayoutPanel();
            rowPanel.Size = new Size(mainLayoutPanel.Width - 20, 60); // Adjust height as needed
            rowPanel.FlowDirection = FlowDirection.LeftToRight;
            //rowPanel.Dock = DockStyle.Right;
            rowPanel.WrapContents = true;
            rowPanel.AutoScroll = false;
            return rowPanel;
        }

        private void MacroPanel_Click(MacrosInfo info)
        {
            string colorName = info.ColorName;
            macrDetailsForm = new MacroDetails(info, AudioService.Instance.virtualCableInputDeviceNumber);
            macrDetailsForm.ShowDialog();

            MacroListChangeNotifier.NotifyListChanged();

            if (info.ColorName != colorName)
                UpdatePanelColor(info);

            // LoadMacrosControls(); // Refresh form again
        }

        private void UpdatePanelColor(MacrosInfo info)
        {
            Control[] foundControls = this.Controls.Find(info.Name, true);
            if (foundControls.Length > 0)
            {
                Control myControl = foundControls[0];

                // Cast the control to the Panel
                if (myControl is Panel)
                {
                    Panel panel = myControl as Panel;
                    panel.BackColor = Color.FromName(info.ColorName);
                }
            }

        }

        #region

        // Capture keyboard data to start respective audio
        // Capture keyboard data to start respective audio
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Check for Shift + F1
            if (keyData == (Keys.Shift | Keys.F1))
            {
                PerformShiftF1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F2
            if (keyData == (Keys.Shift | Keys.F2))
            {
                PerformShiftF2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F3
            if (keyData == (Keys.Shift | Keys.F3))
            {
                PerformShiftF3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F4
            if (keyData == (Keys.Shift | Keys.F4))
            {
                PerformShiftF4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F5
            if (keyData == (Keys.Shift | Keys.F5))
            {
                // Perform the operation for Shift + F1
                PerformShiftF5Operation();
                return true;
            }

            // Check for Shift + F6
            if (keyData == (Keys.Shift | Keys.F6))
            {
                PerformShiftF6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F7
            if (keyData == (Keys.Shift | Keys.F7))
            {
                PerformShiftF7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F8
            if (keyData == (Keys.Shift | Keys.F8))
            {
                PerformShiftF8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F9
            if (keyData == (Keys.Shift | Keys.F9))
            {
                PerformShiftF9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F10
            if (keyData == (Keys.Shift | Keys.F10))
            {
                PerformShiftF10Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F11
            if (keyData == (Keys.Shift | Keys.F11))
            {
                PerformShiftF11Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F12
            if (keyData == (Keys.Shift | Keys.F12))
            {
                PerformShiftF12Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + A
            if (keyData == (Keys.Shift | Keys.A))
            {
                PerformShiftAOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + B
            if (keyData == (Keys.Shift | Keys.B))
            {
                PerformShiftBOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + C
            if (keyData == (Keys.Shift | Keys.C))
            {
                PerformShiftCOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + D
            if (keyData == (Keys.Shift | Keys.D))
            {
                PerformShiftDOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + E
            if (keyData == (Keys.Shift | Keys.E))
            {
                PerformShiftEOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + F
            if (keyData == (Keys.Shift | Keys.F))
            {
                PerformShiftFOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + G
            if (keyData == (Keys.Shift | Keys.G))
            {
                PerformShiftGOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + H
            if (keyData == (Keys.Shift | Keys.H))
            {
                PerformShiftHOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + I
            if (keyData == (Keys.Shift | Keys.I))
            {
                PerformShiftIOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + J
            if (keyData == (Keys.Shift | Keys.J))
            {
                PerformShiftJOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + K
            if (keyData == (Keys.Shift | Keys.K))
            {
                PerformShiftKOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + L
            if (keyData == (Keys.Shift | Keys.L))
            {
                PerformShiftLOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + M
            if (keyData == (Keys.Shift | Keys.M))
            {
                PerformShiftMOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + N
            if (keyData == (Keys.Shift | Keys.N))
            {
                PerformShiftNOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + O
            if (keyData == (Keys.Shift | Keys.O))
            {
                PerformShiftOOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + P
            if (keyData == (Keys.Shift | Keys.P))
            {
                PerformShiftPOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Q
            if (keyData == (Keys.Shift | Keys.Q))
            {
                PerformShiftQOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + R
            if (keyData == (Keys.Shift | Keys.R))
            {
                PerformShiftROperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + S
            if (keyData == (Keys.Shift | Keys.S))
            {
                PerformShiftSOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + T
            if (keyData == (Keys.Shift | Keys.T))
            {
                PerformShiftTOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + U
            if (keyData == (Keys.Shift | Keys.U))
            {
                PerformShiftUOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + V
            if (keyData == (Keys.Shift | Keys.V))
            {
                PerformShiftVOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + W
            if (keyData == (Keys.Shift | Keys.W))
            {
                PerformShiftWOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + X
            if (keyData == (Keys.Shift | Keys.X))
            {
                PerformShiftXOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Y
            if (keyData == (Keys.Shift | Keys.Y))
            {
                PerformShiftYOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Z
            if (keyData == (Keys.Shift | Keys.Z))
            {
                PerformShiftZOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 1
            if (keyData == (Keys.Shift | Keys.D1))
            {
                PerformShift1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 2
            if (keyData == (Keys.Shift | Keys.D2))
            {
                PerformShift2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 3
            if (keyData == (Keys.Shift | Keys.D3))
            {
                PerformShift3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 4
            if (keyData == (Keys.Shift | Keys.D4))
            {
                PerformShift4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 5
            if (keyData == (Keys.Shift | Keys.D5))
            {
                PerformShift5Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 6
            if (keyData == (Keys.Shift | Keys.D6))
            {
                PerformShift6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 7
            if (keyData == (Keys.Shift | Keys.D7))
            {
                PerformShift7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 8
            if (keyData == (Keys.Shift | Keys.D8))
            {
                PerformShift8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 9
            if (keyData == (Keys.Shift | Keys.D9))
            {
                PerformShift9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + 0
            if (keyData == (Keys.Shift | Keys.D0))
            {
                PerformShift0Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num1
            if (keyData == (Keys.Shift | Keys.NumPad1))
            {
                PerformShiftNum1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num2
            if (keyData == (Keys.Shift | Keys.NumPad2))
            {
                PerformShiftNum2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num3
            if (keyData == (Keys.Shift | Keys.NumPad3))
            {
                PerformShiftNum3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num4
            if (keyData == (Keys.Shift | Keys.NumPad4))
            {
                PerformShiftNum4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num5
            if (keyData == (Keys.Shift | Keys.NumPad5))
            {
                PerformShiftNum5Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num6
            if (keyData == (Keys.Shift | Keys.NumPad6))
            {
                PerformShiftNum6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num7
            if (keyData == (Keys.Shift | Keys.NumPad7))
            {
                PerformShiftNum7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num8
            if (keyData == (Keys.Shift | Keys.NumPad8))
            {
                PerformShiftNum8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num9
            if (keyData == (Keys.Shift | Keys.NumPad9))
            {
                PerformShiftNum9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Num0
            if (keyData == (Keys.Shift | Keys.NumPad0))
            {
                PerformShiftNum0Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + ,
            if (keyData == (Keys.Shift | Keys.Oemcomma))
            {
                PerformShiftCommaOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + .
            if (keyData == (Keys.Shift | Keys.OemPeriod))
            {
                PerformShiftPeriodOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + ;
            if (keyData == (Keys.Shift | Keys.OemSemicolon))
            {
                PerformShiftSemiColonOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + '
            if (keyData == (Keys.Shift | Keys.OemQuotes))
            {
                PerformShiftQuotesOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + [
            if (keyData == (Keys.Shift | Keys.OemOpenBrackets))
            {
                PerformShiftOpenBracketOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + [
            if (keyData == (Keys.Shift | Keys.OemCloseBrackets))
            {
                PerformShiftCloseBracketOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + -
            if (keyData == (Keys.Shift | Keys.OemMinus))
            {
                PerformShiftMinusOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Space
            if (keyData == (Keys.Shift | Keys.Space))
            {
                PerformShiftSpaceOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Shift + Enter
            if (keyData == (Keys.Shift | Keys.Enter))
            {
                PerformShiftEnterOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F1
            if (keyData == (Keys.Alt | Keys.F1))
            {
                PerformAltF1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F2
            if (keyData == (Keys.Alt | Keys.F2))
            {
                PerformAltF2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F3
            if (keyData == (Keys.Alt | Keys.F3))
            {
                PerformAltF3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F4
            if (keyData == (Keys.Alt | Keys.F4))
            {
                PerformAltF4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F5
            if (keyData == (Keys.Alt | Keys.F5))
            {

                PerformAltF5Operation();
                return true;
            }

            // Check for Alt + F6
            if (keyData == (Keys.Alt | Keys.F6))
            {
                PerformAltF6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F7
            if (keyData == (Keys.Alt | Keys.F7))
            {
                PerformAltF7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F8
            if (keyData == (Keys.Alt | Keys.F8))
            {
                PerformAltF8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F9
            if (keyData == (Keys.Alt | Keys.F9))
            {
                PerformAltF9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F10
            if (keyData == (Keys.Alt | Keys.F10))
            {
                PerformAltF10Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F11
            if (keyData == (Keys.Alt | Keys.F11))
            {
                PerformAltF11Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F12
            if (keyData == (Keys.Alt | Keys.F12))
            {
                PerformAltF12Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + A
            if (keyData == (Keys.Alt | Keys.A))
            {
                PerformAltAOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + B
            if (keyData == (Keys.Alt | Keys.B))
            {
                PerformAltBOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + C
            if (keyData == (Keys.Alt | Keys.C))
            {
                PerformAltCOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + D
            if (keyData == (Keys.Alt | Keys.D))
            {
                PerformAltDOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + E
            if (keyData == (Keys.Alt | Keys.E))
            {
                PerformAltEOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + F
            if (keyData == (Keys.Alt | Keys.F))
            {
                PerformAltFOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + G
            if (keyData == (Keys.Alt | Keys.G))
            {
                PerformAltGOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + H
            if (keyData == (Keys.Alt | Keys.H))
            {
                PerformAltHOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + I
            if (keyData == (Keys.Alt | Keys.I))
            {
                PerformAltIOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + J
            if (keyData == (Keys.Alt | Keys.J))
            {
                PerformAltJOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + K
            if (keyData == (Keys.Alt | Keys.K))
            {
                PerformAltKOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + L
            if (keyData == (Keys.Alt | Keys.L))
            {
                PerformAltLOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + M
            if (keyData == (Keys.Alt | Keys.M))
            {
                PerformAltMOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + N
            if (keyData == (Keys.Alt | Keys.N))
            {
                PerformAltNOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + O
            if (keyData == (Keys.Alt | Keys.O))
            {
                PerformAltOOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + P
            if (keyData == (Keys.Alt | Keys.P))
            {
                PerformAltPOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Q
            if (keyData == (Keys.Alt | Keys.Q))
            {
                PerformAltQOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + R
            if (keyData == (Keys.Alt | Keys.R))
            {
                PerformAltROperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + S
            if (keyData == (Keys.Alt | Keys.S))
            {
                PerformAltSOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + T
            if (keyData == (Keys.Alt | Keys.T))
            {
                PerformAltTOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + U
            if (keyData == (Keys.Alt | Keys.U))
            {
                PerformAltUOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + V
            if (keyData == (Keys.Alt | Keys.V))
            {
                PerformAltVOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + W
            if (keyData == (Keys.Alt | Keys.W))
            {
                PerformAltWOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + X
            if (keyData == (Keys.Alt | Keys.X))
            {
                PerformAltXOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Y
            if (keyData == (Keys.Alt | Keys.Y))
            {
                PerformAltYOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Z
            if (keyData == (Keys.Alt | Keys.Z))
            {
                PerformAltZOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 1
            if (keyData == (Keys.Alt | Keys.D1))
            {
                PerformAlt1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 2
            if (keyData == (Keys.Alt | Keys.D2))
            {
                PerformAlt2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 3
            if (keyData == (Keys.Alt | Keys.D3))
            {
                PerformAlt3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 4
            if (keyData == (Keys.Alt | Keys.D4))
            {
                PerformAlt4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 5
            if (keyData == (Keys.Alt | Keys.D5))
            {
                PerformAlt5Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 6
            if (keyData == (Keys.Alt | Keys.D6))
            {
                PerformAlt6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 7
            if (keyData == (Keys.Alt | Keys.D7))
            {
                PerformAlt7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 8
            if (keyData == (Keys.Alt | Keys.D8))
            {
                PerformAlt8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 9
            if (keyData == (Keys.Alt | Keys.D9))
            {
                PerformAlt9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + 0
            if (keyData == (Keys.Alt | Keys.D0))
            {
                PerformAlt0Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num1
            if (keyData == (Keys.Alt | Keys.NumPad1))
            {
                PerformAltNum1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num2
            if (keyData == (Keys.Alt | Keys.NumPad2))
            {
                PerformAltNum2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num3
            if (keyData == (Keys.Alt | Keys.NumPad3))
            {
                PerformAltNum3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num4
            if (keyData == (Keys.Alt | Keys.NumPad4))
            {
                PerformAltNum4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num5
            if (keyData == (Keys.Alt | Keys.NumPad5))
            {
                PerformAltNum5Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num6
            if (keyData == (Keys.Alt | Keys.NumPad6))
            {
                PerformAltNum6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num7
            if (keyData == (Keys.Alt | Keys.NumPad7))
            {
                PerformAltNum7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num8
            if (keyData == (Keys.Alt | Keys.NumPad8))
            {
                PerformAltNum8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num9
            if (keyData == (Keys.Alt | Keys.NumPad9))
            {
                PerformAltNum9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Num0
            if (keyData == (Keys.Alt | Keys.NumPad0))
            {
                PerformAltNum0Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + ,
            if (keyData == (Keys.Alt | Keys.Oemcomma))
            {
                PerformAltCommaOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + .
            if (keyData == (Keys.Alt | Keys.OemPeriod))
            {
                PerformAltPeriodOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + ;
            if (keyData == (Keys.Alt | Keys.OemSemicolon))
            {
                PerformAltSemiColonOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + '
            if (keyData == (Keys.Alt | Keys.OemQuotes))
            {
                PerformAltQuotesOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + [
            if (keyData == (Keys.Alt | Keys.OemOpenBrackets))
            {
                PerformAltOpenBracketOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + [
            if (keyData == (Keys.Alt | Keys.OemCloseBrackets))
            {
                PerformAltCloseBracketOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + -
            if (keyData == (Keys.Alt | Keys.OemMinus))
            {
                PerformAltMinusOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Space
            if (keyData == (Keys.Alt | Keys.Space))
            {
                PerformAltSpaceOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Alt + Enter
            if (keyData == (Keys.Alt | Keys.Enter))
            {
                PerformAltEnterOperation();
                return true; // Return true to indicate that the key has been handled
            }


            // Check for Control + F1
            if (keyData == (Keys.Control | Keys.F1))
            {
                PerformControlF1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F2
            if (keyData == (Keys.Control | Keys.F2))
            {
                PerformControlF2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F3
            if (keyData == (Keys.Control | Keys.F3))
            {
                PerformControlF3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F4
            if (keyData == (Keys.Control | Keys.F4))
            {
                PerformControlF4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F5
            if (keyData == (Keys.Control | Keys.F5))
            {
                PerformControlF5Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F6
            if (keyData == (Keys.Control | Keys.F6))
            {
                PerformControlF6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F7
            if (keyData == (Keys.Control | Keys.F7))
            {
                PerformControlF7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F8
            if (keyData == (Keys.Control | Keys.F8))
            {
                PerformControlF8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F9
            if (keyData == (Keys.Control | Keys.F9))
            {
                PerformControlF9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F10
            if (keyData == (Keys.Control | Keys.F10))
            {
                PerformControlF10Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F11
            if (keyData == (Keys.Control | Keys.F11))
            {
                PerformControlF11Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Control + F12
            if (keyData == (Keys.Control | Keys.F12))
            {
                PerformControlF12Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + A
            if (keyData == (Keys.Control | Keys.A))
            {
                PerformCtrlAOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + B
            if (keyData == (Keys.Control | Keys.B))
            {
                PerformCtrlBOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + C
            if (keyData == (Keys.Control | Keys.C))
            {
                PerformCtrlCOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + D
            if (keyData == (Keys.Control | Keys.D))
            {
                PerformCtrlDOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + E
            if (keyData == (Keys.Control | Keys.E))
            {
                PerformCtrlEOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + F
            if (keyData == (Keys.Control | Keys.F))
            {
                PerformCtrlFOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + G
            if (keyData == (Keys.Control | Keys.G))
            {
                PerformCtrlGOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + H
            if (keyData == (Keys.Control | Keys.H))
            {
                PerformCtrlHOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + I
            if (keyData == (Keys.Control | Keys.I))
            {
                PerformCtrlIOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + J
            if (keyData == (Keys.Control | Keys.J))
            {
                PerformCtrlJOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + K
            if (keyData == (Keys.Control | Keys.K))
            {
                PerformCtrlKOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + L
            if (keyData == (Keys.Control | Keys.L))
            {
                PerformCtrlLOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + M
            if (keyData == (Keys.Control | Keys.M))
            {
                PerformCtrlMOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + N
            if (keyData == (Keys.Control | Keys.N))
            {
                PerformCtrlNOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + O
            if (keyData == (Keys.Control | Keys.O))
            {
                PerformCtrlOOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + P
            if (keyData == (Keys.Control | Keys.P))
            {
                PerformCtrlPOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Q
            if (keyData == (Keys.Control | Keys.Q))
            {
                PerformCtrlQOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + R
            if (keyData == (Keys.Control | Keys.R))
            {
                PerformCtrlROperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + S
            if (keyData == (Keys.Control | Keys.S))
            {
                PerformCtrlSOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + T
            if (keyData == (Keys.Control | Keys.T))
            {
                PerformCtrlTOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + U
            if (keyData == (Keys.Control | Keys.U))
            {
                PerformCtrlUOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + V
            if (keyData == (Keys.Control | Keys.V))
            {
                PerformCtrlVOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + W
            if (keyData == (Keys.Control | Keys.W))
            {
                PerformCtrlWOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + X
            if (keyData == (Keys.Control | Keys.X))
            {
                PerformCtrlXOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Y
            if (keyData == (Keys.Control | Keys.Y))
            {
                PerformCtrlYOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Z
            if (keyData == (Keys.Control | Keys.Z))
            {
                PerformCtrlZOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 1
            if (keyData == (Keys.Control | Keys.D1))
            {
                PerformCtrl1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 2
            if (keyData == (Keys.Control | Keys.D2))
            {
                PerformCtrl2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 3
            if (keyData == (Keys.Control | Keys.D3))
            {
                PerformCtrl3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 4
            if (keyData == (Keys.Control | Keys.D4))
            {
                PerformCtrl4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 5
            if (keyData == (Keys.Control | Keys.D5))
            {
                PerformCtrl5Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 6
            if (keyData == (Keys.Control | Keys.D6))
            {
                PerformCtrl6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 7
            if (keyData == (Keys.Control | Keys.D7))
            {
                PerformCtrl7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 8
            if (keyData == (Keys.Control | Keys.D8))
            {
                PerformCtrl8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 9
            if (keyData == (Keys.Control | Keys.D9))
            {
                PerformCtrl9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + 0
            if (keyData == (Keys.Control | Keys.D0))
            {
                PerformCtrl0Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num1
            if (keyData == (Keys.Control | Keys.NumPad1))
            {
                PerformCtrlNum1Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num2
            if (keyData == (Keys.Control | Keys.NumPad2))
            {
                PerformCtrlNum2Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num3
            if (keyData == (Keys.Control | Keys.NumPad3))
            {
                PerformCtrlNum3Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num4
            if (keyData == (Keys.Control | Keys.NumPad4))
            {
                PerformCtrlNum4Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num5
            if (keyData == (Keys.Control | Keys.NumPad5))
            {
                PerformCtrlNum5Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num6
            if (keyData == (Keys.Control | Keys.NumPad6))
            {
                PerformCtrlNum6Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num7
            if (keyData == (Keys.Control | Keys.NumPad7))
            {
                PerformCtrlNum7Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num8
            if (keyData == (Keys.Control | Keys.NumPad8))
            {
                PerformCtrlNum8Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num9
            if (keyData == (Keys.Control | Keys.NumPad9))
            {
                PerformCtrlNum9Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Num0
            if (keyData == (Keys.Control | Keys.NumPad0))
            {
                PerformCtrlNum0Operation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + ,
            if (keyData == (Keys.Control | Keys.Oemcomma))
            {
                PerformCtrlCommaOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + .
            if (keyData == (Keys.Control | Keys.OemPeriod))
            {
                PerformCtrlPeriodOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + ;
            if (keyData == (Keys.Control | Keys.OemSemicolon))
            {
                PerformCtrlSemiColonOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + '
            if (keyData == (Keys.Control | Keys.OemQuotes))
            {
                PerformCtrlQuotesOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + [
            if (keyData == (Keys.Control | Keys.OemOpenBrackets))
            {
                PerformCtrlOpenBracketOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + [
            if (keyData == (Keys.Control | Keys.OemCloseBrackets))
            {
                PerformCtrlCloseBracketOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + -
            if (keyData == (Keys.Control | Keys.OemMinus))
            {
                PerformCtrlMinusOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Space
            if (keyData == (Keys.Control | Keys.Space))
            {
                PerformCtrlSpaceOperation();
                return true; // Return true to indicate that the key has been handled
            }

            // Check for Ctrl + Enter
            if (keyData == (Keys.Control | Keys.Enter))
            {
                PerformCtrlEnterOperation();
                return true; // Return true to indicate that the key has been handled
            }

            if (keyData == Keys.Escape)
            {
                AudioService.Instance.StopAudioRecordings();//StopAudioRecordings();               
                return true;
            }

            // For other keys, call the base class implementation
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void PerformShiftF1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF10Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F10").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF11Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F11").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftF12Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F12").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftAOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + A").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftBOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + B").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftCOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + C").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftDOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + D").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftEOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + E").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftFOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + F").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftGOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + G").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftHOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + H").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftIOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + I").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftJOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + J").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftKOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + K").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftLOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + L").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftMOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + M").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + N").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftOOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + O").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftPOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + P").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftQOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Q").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftROperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + R").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftSOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + S").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftTOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + T").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftUOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + U").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftVOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + V").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftWOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + W").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftXOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + X").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftYOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Y").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftZOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Z").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShift0Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + 0").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftNum0Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Num0").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftCommaOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + ,").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftPeriodOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + .").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftSemiColonOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + ;").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftQuotesOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + '").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftCloseBracketOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + ]").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftOpenBracketOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + [").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftMinusOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + -").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftSpaceOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Space").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformShiftEnterOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Shift + Enter").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF10Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F10").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF11Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F11").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltF12Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F12").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltAOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + A").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltBOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + B").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltCOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + C").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltDOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + D").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltEOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + E").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltFOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + F").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltGOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + G").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltHOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + H").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltIOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + I").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltJOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + J").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltKOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + K").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltLOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + L").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltMOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + M").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + N").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltOOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + O").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltPOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + P").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltQOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Q").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltROperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + R").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltSOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + S").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltTOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + T").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltUOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + U").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltVOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + V").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltWOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + W").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltXOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + X").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltYOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Y").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltZOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Z").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAlt0Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + 0").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltNum0Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Num0").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltCommaOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + ,").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltPeriodOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + .").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltSemiColonOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + ;").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltQuotesOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + '").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltCloseBracketOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + ]").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltOpenBracketOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + [").FirstOrDefault();
            PerformShortKeyOperation(info);
        }


        private void PerformAltMinusOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + -").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltSpaceOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Space").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformAltEnterOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Alt + Enter").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF10Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F10").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF11Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F11").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformControlF12Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F12").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlAOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + A").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlBOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + B").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlCOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + C").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlDOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + D").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlEOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + E").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlFOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + F").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlGOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + G").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlHOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + H").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlIOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + I").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlJOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + J").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlKOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + K").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlLOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + L").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlMOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + M").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + N").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlOOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + O").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlPOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + P").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlQOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Q").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlROperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + R").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlSOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + S").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlTOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + T").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlUOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + U").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlVOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + V").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlWOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + W").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlXOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + X").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlYOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Y").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlZOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Z").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrl0Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + 0").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum1Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num1").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum2Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num2").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum3Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num3").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum4Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num4").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum5Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num5").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum6Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num6").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum7Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num7").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum8Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num8").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum9Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num9").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlNum0Operation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Num0").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlCommaOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + ,").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlPeriodOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + .").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlSemiColonOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + ;").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlQuotesOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + '").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlCloseBracketOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + ]").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlOpenBracketOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + [").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlMinusOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + -").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlSpaceOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Space").FirstOrDefault();
            PerformShortKeyOperation(info);
        }

        private void PerformCtrlEnterOperation()
        {
            MacrosInfo info = manager.macroList.Where(x => x.Name == "Ctrl + Enter").FirstOrDefault();
            PerformShortKeyOperation(info);
        }


        private void PerformShortKeyOperation(MacrosInfo info)
        {
            if (info != null)
            {
                audioMacroInfo = info;
                if (info.voiceFilePath != string.Empty && File.Exists(info.voiceFilePath))
                {
                    AudioService.Instance.PlayAudio(info);
                }
                else
                {
                    MessageBox.Show("No Voice file is attached with this Key. Please record or upload first.");
                }
            }
            else
            {
                MessageBox.Show("No Handling of this key to play recorded voice.");
            }
        }

        private void PlayAudio(MacrosInfo info)
        {
            try
            {
                if (!isAudioPlaying)
                {
                    // Set up the WaveOut device to play through the virtual cable output for agent's
                    isAudioPlaying = true;
                    waveOut = new WaveOutEvent
                    {
                        DeviceNumber = FindWaveOutDeviceNumber("cable")
                    };
                    // Load the recorded audio file
                    audioFileReader = new AudioFileReader(info.voiceFilePath);
                    // Initialize WaveOut with the audio file and start playback
                    waveOut.Init(audioFileReader);
                    waveOut.Play();
                    // Mute or stop the WaveIn capturing (agent's microphone)
                    if (waveIn != null)
                        waveIn.StopRecording();
                    // Handle Playback Stopped event
                    waveOut.PlaybackStopped += OnPlaybackStopped;


                    timer1.Start(); // timer to update remaining time
                    // new Wavout for agent

                    waveO = new WaveOutEvent
                    {
                        DeviceNumber = 0, // Set the virtual audio cable device number
                        DesiredLatency = 125 // Adjust latency as needed
                    };

                    audioFileReader2 = new AudioFileReader(info.voiceFilePath);
                    waveO.Init(audioFileReader2);
                    waveO.Play();
                    waveO.PlaybackStopped += OnPlaybackStopped2;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:" + ex.Message);
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private int FindWaveOutDeviceNumber(string targetDeviceName)
        {
            try
            {
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    var capabilities = WaveOut.GetCapabilities(i);
                    if (capabilities.ProductName.ToLower().Contains(targetDeviceName.ToLower()))
                    {
                        return i;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
            return -1; // Device not found
        }

        private void OnPlaybackStopped2(object sender, StoppedEventArgs e)
        {
            // Dispose resources
            try
            {
                if (audioFileReader2 != null)
                {
                    audioFileReader2.Dispose();
                    audioFileReader2 = null;
                }

                if (waveO != null)
                    waveO.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:" + ex.Message);
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            // Dispose resources
            try
            {
                isAudioPlaying = false;

                if (audioFileReader != null)
                {
                    audioFileReader.Dispose();
                    audioFileReader = null;
                }

                if (waveOut != null)
                    waveOut.Dispose();

                // Once playback is finished, start capturing from the agent's microphone again
                if (waveIn != null)
                    waveIn.StartRecording();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:" + ex.Message);
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void StopAudioRecordings()
        {
            if (isAudioPlaying)
            {
                if (waveOut != null && waveOut.PlaybackState == PlaybackState.Playing)
                {
                    waveOut.Stop(); // Pause playback to resume later

                    if (waveO != null && waveO.PlaybackState == PlaybackState.Playing)
                    {
                        waveO.Stop();
                    }
                }

                isAudioPlaying = false;
                //cancellationTokenSource?.Cancel();                
            }
        }

        #endregion
    }
}
