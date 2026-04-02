using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1.src
{
    public class Scripts
    {
        public string Id { get; set; } = Guid.NewGuid().ToString(); // Unique identifier for each script
        public string Name { get; set; }
        public string Text { get; set; }
        public string RTFContent { get; set; }
        public List<RichTextBoxEx.LinkInfo> Links { get; set; } = new List<RichTextBoxEx.LinkInfo>(); // Store link information
        
        // New properties for spreadsheet functionality
        public string AudioFilePath { get; set; } = string.Empty;
        public bool IsPlaying { get; set; } = false;
        public bool HasAudio { get; set; } = false;
        
        // Font formatting properties
        public string FontFamily { get; set; } = "Segoe UI";
        public float FontSize { get; set; } = 9f;
        public bool IsBold { get; set; } = false;
        public bool IsItalic { get; set; } = false;
        public string FontColor { get; set; } = "Black";
    }
}
