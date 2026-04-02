using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class VolumeControl : UserControl
    {
        public VolumeControl()
        {
            InitializeComponent();

            this.Size = new System.Drawing.Size(70, 20);
            this.BackColor = Color.Black;
            DoubleBuffered = true;

        }

        int pb_value = 40;
        int pb_min = 0;
        int pb_max = 100;

        public int Max { get { return pb_max; } set { pb_max = value; Invalidate(); } }

        public int Min { get { return pb_min; } set { pb_min = value; Invalidate(); } }

        public int Value { get { return pb_value; } set { pb_value = value; Invalidate(); } }

        public int gap = 10;

        Color b_color = Color.Aqua;
        public Color Bar_Color { get { return b_color; } set { b_color = value; Invalidate(); } }


        private void VolumeControl_Paint(object sender, PaintEventArgs e)
        {
            int startPoint = 22;
            SolidBrush sb=new SolidBrush(Color.DimGray);

            for(int j=0; j< (Max * ClientSize.Width / Max - 40) / gap; j++)
            {
                e.Graphics.FillRectangle(sb, new Rectangle(startPoint, 0, gap - 5, ClientSize.Height));
                startPoint += gap;
            }

            int bufferPoint = 22;
            SolidBrush br =new SolidBrush(b_color);

            for (int i=0; i < (pb_value * ClientSize.Width / Max - 50) / gap; i++) 
            {
                e.Graphics.FillRectangle((Brush)br, new Rectangle(bufferPoint, 0, gap - 2, ClientSize.Height));
                bufferPoint += gap;
            }

            int thumbSize = 10;
            SolidBrush thumb = new SolidBrush(Color.White);
            e.Graphics.FillRectangle((Brush)thumb, new Rectangle(bufferPoint,0,thumbSize,ClientSize.Height));

            if (pb_value >= Min)
            {
                Image left_Img = Image.FromFile(Path.Combine(Application.StartupPath, "res", "down_img.png"));
                e.Graphics.DrawImage(left_Img, 5, 0, ClientSize.Height, ClientSize.Height);
            }
            if (pb_value <= 50)
            {
                Image right_Img = Image.FromFile(Path.Combine(Application.StartupPath, "res", "mid_img.png"));
                e.Graphics.DrawImage(right_Img, ClientSize.Width - 25, 0, ClientSize.Height + 5, ClientSize.Height);
            }
            if (pb_value <= Min)
            {
                Image left_Img = Image.FromFile(Path.Combine(Application.StartupPath, "res", "mute_img.png"));
                e.Graphics.DrawImage(left_Img, 5, 0, ClientSize.Height, ClientSize.Height);
            }
            if (pb_value >= 50)
            {
                Image right_Img = Image.FromFile(Path.Combine(Application.StartupPath, "res", "high_img.png"));
                e.Graphics.DrawImage(right_Img, ClientSize.Width - 25, 0, ClientSize.Height + 5, ClientSize.Height);
            }

        }

        private void getBarValue(float value)
        {
            if (value < Min)
            {
                value = Min;
            }
            if (value > Max)
            {
                value = Max;
            }
            if (pb_value == value) { return; }
            pb_value = (int) value; 
            this.Refresh();
        }

        private float getThumbValue(int x)
        {
            return Min + (Max - Min) * x / (float)(ClientSize.Width);
        }

        bool mouse = false;

        private void VolumeControl_MouseDown(object sender, MouseEventArgs e)
        {
            mouse = true;
            getBarValue(getThumbValue(e.X));
        }

        private void VolumeControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (!mouse) { return; }
            getBarValue(getThumbValue(e.X));
        }

        private void VolumeControl_MouseUp(object sender, MouseEventArgs e)
        {
            mouse = false;
        }
    }
}
