using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using static System.Windows.Forms.LinkLabel;

namespace System.Windows.Forms
{
    public class RichTextBoxEx : RichTextBox
    {
        public event LinkClickedEventHandler LinkClicked;
        public event EventHandler EscapePressed;

        public List<LinkInfo> Links { get; set; } = new List<LinkInfo>();

        public RichTextBoxEx()
        {
            // Subscribe to the MouseUp event to detect clicks
            this.MouseUp += RichTextBoxEx_MouseUp;
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            // Apply link styles (color, underline) when the control is created
            ApplyLinkStyles();
        }

        Match matchKeyDown;
        //protected override void OnKeyDown(KeyEventArgs e)
        //{
        //    var selectionIndex = this.SelectionStart;
        //    matchKeyDown = GetLinkAtPosition(selectionIndex);

        //    // Handling paste operation with Ctrl+V.
        //    if (e.Control && e.KeyCode == Keys.V)
        //    {
        //        CustomPaste();
        //        e.Handled = true; // Prevent the default paste behavior.
        //    }
        //    // Allow navigation keys to function normally.
        //    else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Home || e.KeyCode == Keys.End)
        //    {
        //        base.OnKeyDown(e); // Allow normal navigation.
        //    }
        //    // Handling deletion within a link.
        //    else if (matchKeyDown != null && !string.IsNullOrEmpty(matchKeyDown.Value))
        //    {
        //        var link = Links.FirstOrDefault(x => x.Text == matchKeyDown.Value);
        //        if (link != null && IsDeletion(e))
        //        {
        //            // Handle deletion to remove the entire link text.
        //            var temp = new LinkInfo(link.Text, 0, link.Color);
        //            this.Links.Remove(link);                    
        //            DeleteLinkText(temp, matchKeyDown);
        //            this.SelectionStart = matchKeyDown.Index;
        //            e.Handled = true;
        //        }
        //        else if (link != null && !IsDeletion(e))
        //        {
        //            // Prevent modifications within a link, except for deletions.
        //            e.Handled = true;
        //            return;
        //        }
        //    }
        //    else
        //    {
        //        // For all other conditions, allow the base class to handle the event.
        //        base.OnKeyDown(e);
        //    }
        //}

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            var selectionIndex = this.SelectionStart;
            matchKeyDown = GetLinkAtPosition(selectionIndex);
            // Example: Intercept Ctrl+V for custom paste operation
            if (keyData == (Keys.Control | Keys.V))
            {
                CustomPaste();
                return true; // Prevent the default paste behavior.
            }
            // Check if the Escape key was pressed
            else if (keyData == Keys.Escape)
            {
                OnEscapePressed();
                return true; // Indicate that the key has been handled
            }
            // Allow navigation keys to function normally.
            else if (keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.Home || keyData == Keys.End)
            {
                return base.ProcessCmdKey(ref msg, keyData);  // Allow normal navigation.
            }

            else if (matchKeyDown != null && !string.IsNullOrEmpty(matchKeyDown.Value))
            {
                var link = Links.FirstOrDefault(x => x.Text == matchKeyDown.Value);
                if (link != null && IsDeletion(keyData))
                {
                    // Handle deletion to remove the entire link text.
                    var temp = new LinkInfo(link.Text, 0, link.Color);
                    this.Links.Remove(link);
                    DeleteLinkText(temp, matchKeyDown);
                    this.SelectionStart = matchKeyDown.Index;
                    return true;

                }
                else if (link != null && !IsDeletion(keyData))
                {
                    // Prevent modifications within a link, except for deletions.
                   
                    return true;
                }
            }           

            // Additional command key handling as needed

            return base.ProcessCmdKey(ref msg, keyData); // Default processing for other keys
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // Get the character index under the mouse cursor
            int charIndex = this.GetCharIndexFromPosition(e.Location);
            var match = GetLinkAtPosition(charIndex);
            if (match != null)
            {
                // If over linkable text, change the cursor to a hand cursor
                this.Cursor = Cursors.Hand;
            }
            else
            {
                // Otherwise, revert to the default cursor
                this.Cursor = Cursors.IBeam;
            }
        }

        private bool IsDeletion(Keys keydata)
        {
            // Adjust this method based on how you identify deletion keystrokes
            return keydata == Keys.Back || keydata == Keys.Delete;
        }

        private void DeleteLinkText(LinkInfo link, Match match)
        {
            int length = match.Index + link.Text.Length + 2;
            int removeLength = (length > this.Text.Length ? (length - this.Text.Length) : 0);

            //this.Text = this.Text.Remove(match.Index, link.Text.Length + 2 - removeLength); // +4 for the curly braces
            //this.SelectionStart = match.Index; // Move cursor to the start position of the removed link

            this.Select(match.Index, link.Text.Length + 2 - removeLength); // Select the text including curly braces
            this.SelectedRtf = ""; // Remove the selected RTF, which effectively deletes the link with its formatting
            this.SelectionStart = match.Index; // Position cursor at the start of the former link

            this.SelectionColor = this.ForeColor; // Default color
            this.SelectionFont = new Font(this.Font, FontStyle.Regular);

            ApplyLinkStyles();                                              
        }        

        private void CustomPaste()
        {
            if (Clipboard.ContainsText())
            {
                var clipboardText = Clipboard.GetText();
                var sanitizedText = RemoveSpecialTexts(clipboardText);

                // Perform the paste operation with sanitized text
                this.SelectedText = sanitizedText;
            }
        }

        private string RemoveSpecialTexts(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // Remove {{ and }} from the input string
            string output = input.Replace("{{", "").Replace("}}", "");
            return output;
        }

        private void RichTextBoxEx_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int charIndex = this.GetCharIndexFromPosition(e.Location);
                
                var match = GetLinkAtPosition(charIndex);
                if (match != null && !string.IsNullOrEmpty(match.Value))
                {
                    var link = Links.Where(x=> x.Text == match.Value).FirstOrDefault();
                    if (link != null)
                    {
                        OnLinkClicked(new LinkClickedEventArgs(link.Text));
                    }
                }
            }
        }

        protected virtual void OnEscapePressed()
        {
            EscapePressed?.Invoke(this, EventArgs.Empty);
        }

        public void ApplyLinkStyles()
        {
            ModifyLinkStartIndex();
            if (Links != null)
            {
                foreach (var link in Links)
                {
                    this.Select(link.StartIndex, link.Text.Length);
                    this.SelectionColor = link.Color;
                    this.SelectionFont = new Font(this.Font, FontStyle.Underline);
                }
            }
            this.DeselectAll();
        }

        private Match GetLinkAtPosition(int charIndex)
        {
            string text = this.Text;
            MatchCollection matches = Regex.Matches(text, @"\{\{(.+?)\}\}");
            foreach (Match match in matches)
            {
                if (match.Index <= charIndex && (match.Index + match.Length + 2) >= charIndex)
                {
                    return match; // Return the text inside double curly braces
                }
            }
            return null; // No link at the given position
        }

        private void ModifyLinkStartIndex()
        {
            string text = this.Text;
            MatchCollection matches = Regex.Matches(text, @"\{\{(.+?)\}\}");
            foreach (Match match in matches)
            {
                var link = Links.FirstOrDefault(x => x.Text == match.Value);
                if (link != null)
                {
                    link.StartIndex = match.Index;
                }
            }
        }


        protected virtual void OnLinkClicked(LinkClickedEventArgs e)
        {
            LinkClicked?.Invoke(this, e);
        }
        [Serializable]
        public class LinkInfo
        {
            public string Text { get; set; }
            public int StartIndex { get; set; }

            public Color Color { get; set; }
            public int EndIndex => StartIndex + Text.Length - 1;

            public LinkInfo(string text, int startIndex, Color color)
            {
                Text = text;
                StartIndex = startIndex;
                Color = color;
            }
        }
    }
}