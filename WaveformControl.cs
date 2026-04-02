using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class WaveformControl : UserControl
    {
        private float[] audioData;
        private int audioDataLength;

        public WaveformControl()
        {
            InitializeComponent();
            this.DoubleBuffered = true; // Helps reduce flickering
        }

        public void AddAudioData(float[] data, int length)
        {
            audioData = data;
            audioDataLength = length;
            this.Invalidate(); // Redraw the control
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (audioData != null)
            {
                DrawWaveform(e.Graphics, audioData, audioDataLength);
            }
        }

        private void DrawWaveform(Graphics g, float[] data, int length)
        {
            g.Clear(this.BackColor);
            int centerLine = this.Height / 2;
            int width = this.Width;
            int samplesPerPixel = length / width;
            if (samplesPerPixel == 0) return;

            for (int x = 0; x < width; x++)
            {
                float maxValue = 0;
                int startSample = x * samplesPerPixel;
                int endSample = Math.Min(startSample + samplesPerPixel, length);

                for (int i = startSample; i < endSample; i++)
                {
                    maxValue = Math.Max(maxValue, Math.Abs(data[i]));
                }

                int lineHeight = (int)(maxValue * centerLine);
                g.DrawLine(Pens.Black, x, centerLine - lineHeight, x, centerLine + lineHeight);
            }
        }
    }
}
