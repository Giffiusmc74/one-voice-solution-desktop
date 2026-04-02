using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1.src
{
    public class AudioIntensityMeter : Panel
    {
        private Panel greenPanel = new Panel();
        private Panel yellowPanel = new Panel();
        private Panel redPanel = new Panel();

        public AudioIntensityMeter()
        {
            this.InitializeComponent();
            DoubleBuffered = true;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Base panel setup
            this.BackColor = Color.Black;
            this.Height = 10; // Example height, adjust as needed
            this.Width = 250; // Example width, adjust as needed

            // Green panel setup — shows safe/normal voice level
            greenPanel.BackColor = Color.LimeGreen;
            greenPanel.Height = this.Height;
            greenPanel.Width = 0; // Start with zero width

            // Yellow panel setup — shows elevated/caution level
            yellowPanel.BackColor = Color.Gold;
            yellowPanel.Height = this.Height;
            yellowPanel.Width = 0; // Start with zero width

            // Red panel setup — shows too loud / clipping risk
            redPanel.BackColor = Color.OrangeRed;
            redPanel.Height = this.Height;
            redPanel.Width = 0; // Start with zero width

            // Add panels to the control
            this.Controls.Add(redPanel);
            this.Controls.Add(yellowPanel);
            this.Controls.Add(greenPanel);

            this.ResumeLayout(false);
        }

        public void UpdateIntensity(int percentage)
        {
            this.Invoke(new Action(() =>
            {
                int totalWidth = this.Width;
                int redThreshold = 70, yellowThreshold = 40;

                percentage = (int) (percentage * 2.5);
                percentage = Math.Min(100, percentage);

                if (percentage > redThreshold)
                {
                    greenPanel.Width = (int)(totalWidth * (yellowThreshold / 100f));
                    yellowPanel.Width = (int)(totalWidth * ((redThreshold - yellowThreshold) / 100f));
                    redPanel.Width = (int)(totalWidth * ((percentage - redThreshold) / 100f));
                    yellowPanel.Left = greenPanel.Width;
                    redPanel.Left = greenPanel.Width + yellowPanel.Width;
                }
                else if (percentage > yellowThreshold)
                {
                    greenPanel.Width = (int)(totalWidth * (yellowThreshold / 100f));
                    yellowPanel.Width = (int)(totalWidth * ((percentage - yellowThreshold) / 100f));
                    redPanel.Width = 0; // Hide red
                    yellowPanel.Left = greenPanel.Width;
                    redPanel.Left = greenPanel.Width + yellowPanel.Width;
                }
                else
                {
                    greenPanel.Width = (int)(totalWidth * (percentage / 100f));
                    yellowPanel.Width = 0; // Hide yellow
                    redPanel.Width = 0; // Hide red
                    yellowPanel.Left = greenPanel.Width;
                    redPanel.Left = greenPanel.Width + yellowPanel.Width;
                }
            }));
        }
    }
}
