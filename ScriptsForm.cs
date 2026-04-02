using NAudio.Wave;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1.src;
using Newtonsoft.Json;
using System.Reflection;

namespace WindowsFormsApp1
{
    public partial class ScriptsForm : Form
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        ScriptManager scriptManager;
        MacroManager macroManager;
        MainForm parentMainForm;

        private WaveOutEvent waveO;
        private AudioFileReader audioFileReader2;
        private AudioFileReader audioFileReader;
        private WaveInEvent waveIn;
        private WaveOutEvent waveOut;
        bool isAudioPlaying = false;

        // New properties for spreadsheet functionality
        private bool isRecordingEnabled = false;
        private Scripts currentSelectedScript = null;
        private int inputDeviceNumber = 0;
        
        // Multi-tab functionality
        private Dictionary<TabPage, ScriptManager> tabScriptManagers;
        private Dictionary<TabPage, DataGridView> tabDataGridViews;
        private int tabCounter = 1;
        private const int MAX_TABS = 5;
        private static string TAB_STATE_FILE => DataUtils.TabStatePath;

        public ScriptsForm(ScriptManager manager, MacroManager macroManager, MainForm mainForm = null)
        {
            try
            {
                parentMainForm = mainForm;
                logger.Info("Scripts form started. Initializing Objects.");
                scriptManager = manager;
                InitializeComponent();
                this.macroManager = macroManager;
                waveIn = AudioService.Instance.waveIn;

                this.Text = "One Voice Solution";
                string iconPath = "../../redIcon.ico";
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
                else
                {
                    logger.Warn($"Icon file not found at {iconPath}");
                }

                // Initialize multi-tab functionality
                tabScriptManagers = new Dictionary<TabPage, ScriptManager>();
                tabDataGridViews = new Dictionary<TabPage, DataGridView>();

                tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
                tabControl.DrawItem += TabControl_DrawItem;

                // Initialize with 5 fixed tabs
                CreateFixedTabsWithPlaceholders();
                
                try
                {
                    if (btnNewScript != null) { btnNewScript.Visible = false; btnNewScript.Enabled = false; }
                }
                catch { /* ignore if controls differ */ }

                // Get input device number
                inputDeviceNumber = GetDefaultInputDevice();
                
                // Setup custom font combobox
                SetupFontComboBox();
                
                // Setup volume monitoring from MainForm
                SetupVolumeMonitoring();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private int GetDefaultInputDevice()
        {
            try
            {
                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var capabilities = WaveIn.GetCapabilities(i);
                    if (!capabilities.ProductName.ToLower().Contains("cable"))
                    {
                        return i;
                    }
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private void richTextBox_EscapePressed(object sender, EventArgs e)
        {
            AudioService.Instance.StopAudioRecordings();
        }

        private void ScriptsForm_Load(object sender, EventArgs e)
        {
            try
            {
                PopulateComboBox();
                LoadContextMenu();
                MacroListChangeNotifier.ListChanged += LoadContextMenu;
                // Tab setup is handled in InitializeFirstTab()

                if (this.pictureBox2 != null)
                {
                    pictureBox2.Left = (this.ClientSize.Width - pictureBox2.Width) / 2;
                    //pictureBox2.Top = -1; // Adjust top margin as needed
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void InitializeFirstTab()
        {
            try
            {
                // Create first tab
                TabPage firstTab = new TabPage($"Sheet {tabCounter}");
                tabControl.TabPages.Add(firstTab);
                
                // Create DataGridView for this tab
                DataGridView tabGrid = CreateDataGridViewForTab();
                firstTab.Controls.Add(tabGrid);
                
                // Store references
                tabScriptManagers[firstTab] = scriptManager;
                tabDataGridViews[firstTab] = tabGrid;
                
                // Setup the grid
                SetupDataGridView(tabGrid);
                PopulateScriptsGrid(tabGrid, scriptManager);
                
                tabCounter++;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error initializing first tab: " + ex.Message);
            }
        }

        private DataGridView CreateDataGridViewForTab()
        {
            DataGridView grid = new DataGridView();
            
            // Copy all properties from the original dgvScripts
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.Fixed3D;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 240, 240);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(240, 240, 240);
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            grid.DefaultCellStyle.ForeColor = Color.Black;
            grid.DefaultCellStyle.SelectionBackColor = Color.Red;//Color.FromArgb(51, 153, 255);
            
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.EnableHeadersVisualStyles = false;
            grid.GridColor = Color.FromArgb(200, 200, 200);
            grid.MultiSelect = true;
            grid.RowHeadersVisible = false;
            grid.RowTemplate.Height = 35;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.AllowUserToResizeRows = true;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.Dock = DockStyle.Fill;
            grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            grid.ReadOnly = false;
            
            // Wire up events
            grid.CellClick += TabGrid_CellClick;
            grid.CellEndEdit += TabGrid_CellEndEdit;
            grid.SelectionChanged += TabGrid_SelectionChanged;
            grid.ColumnHeaderMouseClick += TabGrid_ColumnHeaderMouseClick;
            grid.KeyDown += TabGrid_KeyDown;
            grid.CellMouseClick += TabGrid_CellMouseClick;
            grid.CellMouseEnter += TabGrid_CellMouseEnter;
            grid.CellMouseLeave += TabGrid_CellMouseLeave;
            grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 1) // Script Name is now column 1
                {
                    grid.BeginEdit(true);
                }
            };

            // Enable Double buffering to prevent flickering and ghosting
            try
            {
                typeof(DataGridView).InvokeMember("DoubleBuffered",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                    null, grid, new object[] { true });
            }
            catch (Exception ex)
            {
                logger.Warn("Could not enable DoubleBuffered on DataGridView: " + ex.Message);
            }
            
            return grid;
        }

        private void SetupDataGridView()
        {
            SetupDataGridView(dgvScripts);
        }

        private void SetupDataGridView(DataGridView grid)
        {
            try
            {
                grid.Columns.Clear();
                
                // Play/Pause button column (moved to first position)
                var playPauseColumn = new DataGridViewImageColumn
                {
                    Name = "PlayPause",
                    HeaderText = "Play/Pause",
                    FillWeight = 12,
                    ImageLayout = DataGridViewImageCellLayout.Normal, // Use Normal to prevent zooming with row height
                    Width = 70,
                    MinimumWidth = 60
                };
                playPauseColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                grid.Columns.Add(playPauseColumn);

                // Script Name column (moved to second position)
                var nameColumn = new DataGridViewTextBoxColumn
                {
                    Name = "ScriptName",
                    HeaderText = "Script Name",
                    FillWeight = 70,
                    ReadOnly = false,
                    SortMode = DataGridViewColumnSortMode.NotSortable // Disable sorting
                };
                nameColumn.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                grid.Columns.Add(nameColumn);

                // Record button column
                var recordColumn = new DataGridViewImageColumn
                {
                    Name = "Record",
                    HeaderText = "Record",
                    FillWeight = 15,
                    ImageLayout = DataGridViewImageCellLayout.Normal, // Use Normal to prevent zooming with row height
                    Width = 80,
                    MinimumWidth = 70
                };
                recordColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                grid.Columns.Add(recordColumn);
                recordColumn.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

                // Audio status column
                var statusColumn = new DataGridViewTextBoxColumn
                {
                    Name = "AudioStatus",
                    HeaderText = "Audio Status",
                    FillWeight = 10,
                    ReadOnly = true
                };
                grid.Columns.Add(statusColumn);

                grid.Columns["AudioStatus"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                
                grid.AllowUserToAddRows = false;
                grid.AllowUserToDeleteRows = false;
                grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
                grid.MultiSelect = true;
                grid.RowHeadersVisible = false;
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
                grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
                grid.ReadOnly = false;
                grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                
                // Make grid lines darker/black and increase row spacing
                grid.GridColor = Color.Black;
                grid.RowTemplate.Height = 55; // Increased from 45 for larger buttons
                grid.DefaultCellStyle.Padding = new Padding(2, 2, 2, 2); // Reduced padding to allow larger images
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting up DataGridView: " + ex.Message);
            }
        }

        private void PopulateScriptsGrid()
        {
            PopulateScriptsGrid(dgvScripts, scriptManager);
        }

        private void PopulateScriptsGrid(DataGridView grid, ScriptManager manager)
        {
            try
            {
                grid.Rows.Clear();
                
                // Load images for buttons
                Image playImage = LoadImageSafely("1play.png");
                Image pauseImage = LoadImageSafely("1pause.png");
                Image recordImage = LoadImageSafely("1record.png");
                
                // Ensure we always render 200 rows. Use existing scripts if present, otherwise placeholders.
                int totalRows = 200;
                for (int i = 0; i < totalRows; i++)
                {
                    Scripts script = i < manager.scriptList.Count ? manager.scriptList[i] : new Scripts();
                    if (script == null) script = new Scripts(); // Safety check

                    var row = new DataGridViewRow();
                    row.CreateCells(grid);

                    // Column order: PlayPause(0), ScriptName(1), Record(2), AudioStatus(3)
                    row.Cells[0].Value = (script.IsPlaying) ? pauseImage : playImage; // Play/Pause image
                    row.Cells[1].Value = string.IsNullOrEmpty(script.Name) ? string.Empty : script.Name; // Script Name
                    row.Cells[2].Value = recordImage; // Record image
                    row.Cells[2].Style.BackColor = isRecordingEnabled ? Color.LightGreen : Color.LightCoral; // Set initial color
                    row.Cells[3].Value = (script.HasAudio) ? "Audio Available" : "No Audio"; // Status

                    row.Tag = script;

                    // Apply any saved formatting to script name cell (now column 1)
                    ApplySavedFormatting(row.Cells[1], script);

                    grid.Rows.Add(row);
                }

                UpdateRecordButtonStates(grid);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error populating scripts grid: " + ex.Message);
            }
        }

        /// <summary>
        /// Creates the 5 fixed tabs and populates each with a 200-row grid. Loads any saved scripts by tab name.
        /// </summary>
        private void CreateFixedTabsWithPlaceholders()
        {
            try
            {
                // Make tab titles larger and bold
                try { tabControl.Font = new Font(tabControl.Font.FontFamily, 12F, FontStyle.Bold); } catch { }

                // Capture the legacy manager BEFORE potentially reassigning scriptManager
                var legacyManager = this.scriptManager;

                if (RestoreTabState())
                {
                    // Successfully restored saved tabs (multi-tab data found)
                    tabControl.SelectedIndex = 0;
                    scriptManager = tabScriptManagers[tabControl.TabPages[0]];
                    tabCounter = tabControl.TabPages.Count;
                }
                else
                {
                    // No tab_state.json found. Check if we have legacy scripts from Scripts.txt
                    // We consider legacy data present if there's at least one script that isn't the default "Script 1/2/3" or has actual content
                    bool hasLegacyData = legacyManager != null && legacyManager.scriptList != null && legacyManager.scriptList.Count > 0 
                                         && legacyManager.scriptList.Any(s => !string.IsNullOrEmpty(s.Name) && 
                                            !s.Name.StartsWith("Script ", StringComparison.OrdinalIgnoreCase));

                    if (hasLegacyData)
                    {
                        // IF DATA FOUND: Create only ONE tab with these scripts (as requested)
                        tabControl.TabPages.Clear();
                        var tab = new TabPage("Scripts");
                        var grid = CreateDataGridViewForTab();
                        tab.Controls.Add(grid);
                        
                        tabScriptManagers[tab] = legacyManager;
                        tabDataGridViews[tab] = grid;

                        SetupDataGridView(grid);
                        PopulateScriptsGrid(grid, legacyManager);
                        tabControl.TabPages.Add(tab);
                        
                        logger.Info("Legacy data found (Scripts.txt). Created single tab instead of defaults.");
                    }
                    else
                    {
                        // IF NO DATA FOUND: Create 5 default tabs
                        tabControl.TabPages.Clear();
                        string[] defaultTabNames = new[] { "Medicare", "Final Expense", "Solar", "Script 4", "Script 5" };
        
                        for (int i = 0; i < defaultTabNames.Length; i++)
                        {
                            string name = defaultTabNames[i];
                            var tab = new TabPage(name);
                            var grid = CreateDataGridViewForTab();
                            tab.Controls.Add(grid);
        
                            var managerForTab = new ScriptManager();
        
                            tabScriptManagers[tab] = managerForTab;
                            tabDataGridViews[tab] = grid;
        
                            SetupDataGridView(grid);
                            PopulateScriptsGrid(grid, managerForTab);
        
                            tabControl.TabPages.Add(tab);
                        }
                        
                        logger.Info("No legacy data found. Created default placeholder tabs.");
                    }
    
                    // Add context menu for tab name editing
                    SetupTabContextMenu();
    
                    tabControl.SelectedIndex = 0;
                    scriptManager = tabScriptManagers[tabControl.TabPages[0]];
                    tabCounter = tabControl.TabPages.Count;
                }



                // Initial save if we didn't restore (to create the file)
                if (!File.Exists(TAB_STATE_FILE))
                {
                    SaveTabState();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error creating fixed tabs: " + ex.Message);
            }
        }

        /// <summary>
        /// Scans the audio directory and adds any recordings that aren't in the script list.
        /// </summary>

        private Dictionary<string, List<Scripts>> LoadSavedTabDataByName()
        {
            var dict = new Dictionary<string, List<Scripts>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(TAB_STATE_FILE))
                {
                    var json = File.ReadAllText(TAB_STATE_FILE);
                    var tabState = JsonConvert.DeserializeObject<TabStateData>(json);
                    if (tabState?.Tabs != null)
                    {
                        foreach (var t in tabState.Tabs)
                        {
                            dict[t.TabName] = t.Scripts ?? new List<Scripts>();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error loading saved tab data: " + ex.Message);
            }
            return dict;
        }

        /// <summary>
        /// Safely loads an image from the res folder with error handling and proper sizing
        /// </summary>
        private Image LoadImageSafely(string imageName)
        {
            try
            {
                string imagePath = Path.Combine(Application.StartupPath, "res", imageName);
                logger.Info($"Attempting to load image: {imagePath}");
                
                if (File.Exists(imagePath))
                {
                    logger.Info($"Image file found: {imagePath}");
                    // Load the original image
                    using (var originalImage = Image.FromFile(imagePath))
                    {
                        // Resize to a consistent size for the grid while MAINTAINING ASPECT RATIO
                        // targetH=48 ensures heights are comparable. targetW=80 allows wide buttons to grow.
                        int targetW = 80;
                        int targetH = 48;
                        float ratio = Math.Min((float)targetW / originalImage.Width, (float)targetH / originalImage.Height);
                        int newW = (int)(originalImage.Width * ratio);
                        int newH = (int)(originalImage.Height * ratio);

                        var resizedImage = new Bitmap(targetW, targetH);
                        using (var g = Graphics.FromImage(resizedImage))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            g.Clear(Color.Transparent);
                            // Center the image within the 32x32 square
                            g.DrawImage(originalImage, (targetW - newW) / 2, (targetH - newH) / 2, newW, newH);
                        }
                        logger.Info($"Successfully loaded and resized image: {imageName} ({newW}x{newH})");
                        return resizedImage;
                    }
                }
                else
                {
                    logger.Warn($"Image not found: {imagePath}");
                    // Return a default small image if file not found
                    var bitmap = new Bitmap(32, 32);
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.Clear(Color.LightGray);
                        g.DrawString(imageName.Substring(0, 1).ToUpper(), new Font("Arial", 14, FontStyle.Bold), Brushes.Black, 8, 8);
                    }
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error loading image {imageName}: " + ex.Message);
                // Return a default small image on error
                var bitmap = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Red);
                    g.DrawString("!", new Font("Arial", 16, FontStyle.Bold), Brushes.White, 12, 8);
                }
                return bitmap;
            }
        }

        /// <summary>
        /// Sets up context menu for tab name editing
        /// </summary>
        private void SetupTabContextMenu()
        {
            try
            {
                // Only add the mouse click event - don't set ContextMenuStrip
                // This way the context menu only appears when right-clicking tab headers
                tabControl.MouseClick += TabControl_MouseClick;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting up tab context menu: " + ex.Message);
            }
        }

        private void TabControl_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Find which tab was right-clicked
                for (int i = 0; i < tabControl.TabCount; i++)
                {
                    Rectangle tabRect = tabControl.GetTabRect(i);
                    if (tabRect.Contains(e.Location))
                    {
                        tabControl.SelectedIndex = i;
                        
                        // Create and show context menu only for tab headers
                        var contextMenu = new ContextMenuStrip();
                        var renameMenuItem = new ToolStripMenuItem("Rename Tab");
                        renameMenuItem.Click += RenameTab_Click;
                        contextMenu.Items.Add(renameMenuItem);
                        
                        // Show context menu at the clicked location
                        var screenPoint = tabControl.PointToScreen(e.Location);
                        contextMenu.Show(screenPoint);
                        
                        break;
                    }
                }
            }
        }

        private void RenameTab_Click(object sender, EventArgs e)
        {
            try
            {
                var selectedTab = tabControl.SelectedTab;
                if (selectedTab != null)
                {
                    string currentName = selectedTab.Text;
                    string newName = ShowInputDialog("Enter new tab name:", "Rename Tab", currentName);
                    
                    if (!string.IsNullOrWhiteSpace(newName) && newName != currentName)
                    {
                        // Check if name already exists
                        bool nameExists = false;
                        foreach (TabPage tab in tabControl.TabPages)
                        {
                            if (tab != selectedTab && tab.Text.Equals(newName, StringComparison.OrdinalIgnoreCase))
                            {
                                nameExists = true;
                                break;
                            }
                        }
                        
                        if (nameExists)
                        {
                            MessageBox.Show("A tab with this name already exists. Please choose a different name.",
                                "Duplicate Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else
                        {
                            selectedTab.Text = newName;
                            SaveTabState(); // Save the updated tab name
                            logger.Info($"Tab renamed from '{currentName}' to '{newName}'");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error renaming tab: " + ex.Message);
                MessageBox.Show($"Error renaming tab: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Shows a simple input dialog for text entry
        /// </summary>
        private string ShowInputDialog(string text, string caption, string defaultValue = "")
        {
            Form prompt = new Form()
            {
                Width = 400,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label textLabel = new Label() { Left = 20, Top = 20, Width = 350, Text = text };
            TextBox textBox = new TextBox() { Left = 20, Top = 45, Width = 350, Text = defaultValue };
            Button confirmation = new Button() { Text = "OK", Left = 220, Width = 70, Top = 75, DialogResult = DialogResult.OK };
            Button cancel = new Button() { Text = "Cancel", Left = 295, Width = 70, Top = 75, DialogResult = DialogResult.Cancel };

            confirmation.Click += (sender, e) => { prompt.Close(); };
            cancel.Click += (sender, e) => { prompt.Close(); };

            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(cancel);
            prompt.AcceptButton = confirmation;
            prompt.CancelButton = cancel;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : null;
        }

        private void UpdateRecordButtonStates()
        {
            UpdateRecordButtonStates(dgvScripts);
        }

        private void UpdateRecordButtonStates(DataGridView grid)
        {
            try
            {
                Image recordImage = LoadImageSafely("1record.png");
                
                // Suspend layout to prevent flickering
                grid.SuspendLayout();
                
                foreach (DataGridViewRow row in grid.Rows)
                {
                    var recordCell = row.Cells["Record"];
                    if (recordCell != null)
                    {
                        // Since we're using image columns now, just set the image
                        // Use White instead of Transparent to avoid black rendering artifacts
                        recordCell.Value = recordImage;
                        recordCell.Style.BackColor = Color.White;
                    }
                }
                
                // Resume layout and refresh
                grid.ResumeLayout();
                grid.Refresh();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error updating record button states: " + ex.Message);
            }
        }

        private void btnGlobalRecord_Click(object sender, EventArgs e)
        {
            try
            {
                isRecordingEnabled = !isRecordingEnabled;
                btnGlobalRecord.Text = isRecordingEnabled ? "Disable Recording / Editing" : "Enable Recording / Editing";
                btnGlobalRecord.BackColor = isRecordingEnabled ? Color.LightCoral : SystemColors.Control;
                
                // Update ALL tabs, not just the current one
                foreach (TabPage tab in tabControl.TabPages)
                {
                    if (tabDataGridViews.ContainsKey(tab))
                    {
                        UpdateRecordButtonStates(tabDataGridViews[tab]);
                    }
                }
                
                logger.Info($"Global recording {(isRecordingEnabled ? "enabled" : "disabled")}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error toggling global record state: " + ex.Message);
            }
        }

        private void btnNewScript_Click(object sender, EventArgs e)
        {
            CreateNewScript();
        }

        private void dgvScripts_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

                var script = dgvScripts.Rows[e.RowIndex].Tag as Scripts;
                if (script == null) return;

                var columnName = dgvScripts.Columns[e.ColumnIndex].Name;

                switch (columnName)
                {
                    case "PlayPause":
                        HandlePlayPauseClick(script, e.RowIndex);
                        break;
                    case "Record":
                        HandleRecordClick(script, e.RowIndex);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error handling cell click: " + ex.Message);
            }
        }

        private void HandlePlayPauseClick(Scripts script, int rowIndex)
        {
            HandlePlayPauseClick(script, rowIndex, dgvScripts);
        }

        private void HandlePlayPauseClick(Scripts script, int rowIndex, DataGridView grid)
        {
            try
            {
                if (!script.HasAudio || string.IsNullOrEmpty(script.AudioFilePath))
                {
                    MessageBox.Show("No audio file available for this script. Please record or upload audio first.", 
                        "No Audio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!File.Exists(script.AudioFilePath))
                {
                    MessageBox.Show("Audio file not found. Please re-record or upload audio.", 
                        "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Image playImage = LoadImageSafely("1play.png");
                Image pauseImage = LoadImageSafely("1pause.png");

                if (script.IsPlaying)
                {
                    // Stop audio
                    StopAllAudioPlayback();
                    script.IsPlaying = false;
                    grid.Rows[rowIndex].Cells["PlayPause"].Value = playImage;
                }
                else
                {
                    // Stop any currently playing audio
                    StopAllAudioPlayback();
                    
                    // Play audio
                    PlayScriptAudio(script);
                    script.IsPlaying = true;
                    grid.Rows[rowIndex].Cells["PlayPause"].Value = pauseImage;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error handling play/pause: " + ex.Message);
                MessageBox.Show($"Error playing audio: {ex.Message}", "Playback Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void HandleRecordClick(Scripts script, int rowIndex)
        {
            HandleRecordClick(script, rowIndex, dgvScripts);
        }

        private void HandleRecordClick(Scripts script, int rowIndex, DataGridView grid)
        {
            try
            {
                // Removed isRecordingEnabled check - users can now directly click record button
                
                var recordingForm = new ScriptRecordingForm(inputDeviceNumber, script);
                if (recordingForm.ShowDialog() == DialogResult.OK)
                {
                    var updatedScript = recordingForm.Script;
                    if (updatedScript != null)
                    {
                        // Get current tab's script manager
                        var currentTab = tabControl.SelectedTab;
                        if (currentTab != null && tabScriptManagers.ContainsKey(currentTab))
                        {
                            var currentManager = tabScriptManagers[currentTab];
                            
                            // Update the script in the current tab's manager using row index
                            // This is much safer than name matching as names can change in the dialog
                            if (rowIndex >= 0 && rowIndex < currentManager.scriptList.Count)
                            {
                                currentManager.scriptList[rowIndex] = updatedScript;
                            }
                            else
                            {
                                // If list is shorter than row index, grow it
                                while (currentManager.scriptList.Count < rowIndex)
                                {
                                    currentManager.scriptList.Add(new Scripts());
                                }
                                currentManager.scriptList.Insert(rowIndex, updatedScript);
                            }

                            // Save the updated scripts immediately to ensure persistence
                            SaveTabState();
                            
                            // Refresh the current tab's grid
                            PopulateScriptsGrid(grid, currentManager);
                        }
                        
                        logger.Info($"Script '{updatedScript.Name}' updated with audio");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error handling record click: " + ex.Message);
                MessageBox.Show($"Error opening recording dialog: {ex.Message}", "Recording Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CreateNewScript()
        {
            try
            {
                // Removed isRecordingEnabled check - users can now directly create scripts
                
                var recordingForm = new ScriptRecordingForm(inputDeviceNumber, null);
                if (recordingForm.ShowDialog() == DialogResult.OK)
                {
                    var newScript = recordingForm.Script;
                    if (newScript != null)
                    {
                        // Get current tab's script manager
                        var currentTab = tabControl.SelectedTab;
                        if (currentTab != null && tabScriptManagers.ContainsKey(currentTab))
                        {
                            var currentManager = tabScriptManagers[currentTab];
                            var currentGrid = tabDataGridViews[currentTab];
                            
                            // Check if script name already exists in current tab
                            if (currentManager.scriptList.Any(s => s.Name.Equals(newScript.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                MessageBox.Show("A script with this name already exists. Please choose a different name.", 
                                    "Duplicate Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }

                            // Add new script to current tab's manager
                            currentManager.scriptList.Add(newScript);
                            
                            // Save the updated scripts to ensure persistence
                            SaveTabState();
                            
                            // Refresh the current tab's grid
                            PopulateScriptsGrid(currentGrid, currentManager);
                        
                            // Select the new script in the current grid
                            for (int i = 0; i < currentGrid.Rows.Count; i++)
                            {
                                var script = currentGrid.Rows[i].Tag as Scripts;
                                if (script != null && script.Name == newScript.Name)
                                {
                                    currentGrid.Rows[i].Selected = true;
                                    break;
                                }
                            }
                            
                            logger.Info($"New script '{newScript.Name}' added to tab '{currentTab.Text}'");
                        }
                        
                        logger.Info($"New script '{newScript.Name}' created");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error creating new script: " + ex.Message);
                MessageBox.Show($"Error creating new script: {ex.Message}", "Creation Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void dgvScripts_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                if (dgvScripts.SelectedCells.Count > 0)
                {
                    var cell = dgvScripts.SelectedCells[0];
                    var rowIndex = cell.RowIndex;
                    if (rowIndex >= 0 && rowIndex < dgvScripts.Rows.Count)
                    {
                        var script = dgvScripts.Rows[rowIndex].Tag as Scripts;
                        if (script != null)
                        {
                            currentSelectedScript = script;
                            
                            // Update formatting controls to reflect selected cell's formatting
                            if (cell.ColumnIndex == 1) // Only for Script Name column
                            {
                                UpdateFormattingControlsFromCell(cell, script);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error handling selection change: " + ex.Message);
            }
        }

        private void dgvScripts_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            TabGrid_CellEndEdit(sender, e);
        }

        private void TabGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                var grid = sender as DataGridView;
                if (grid != null && e.ColumnIndex == 1 && e.RowIndex >= 0) // Script Name column (now column 1)
                {
                    var script = grid.Rows[e.RowIndex].Tag as Scripts;
                    if (script != null)
                    {
                        var newName = grid.Rows[e.RowIndex].Cells[1].Value?.ToString();
                        if (!string.IsNullOrEmpty(newName) && newName != script.Name)
                        {
                            script.Name = newName;
                            script.Text = newName; // Update text content as well
                            
                            // Ensure this script is persisted in the current tab's manager at this row index
                            var currentTab = tabControl.SelectedTab;
                            if (currentTab != null && tabScriptManagers.ContainsKey(currentTab))
                            {
                                var manager = tabScriptManagers[currentTab];
                                // Expand the list to accommodate this row index
                                while (manager.scriptList.Count <= e.RowIndex)
                                {
                                    manager.scriptList.Add(new Scripts());
                                }
                                manager.scriptList[e.RowIndex] = script;
                            }

                            // Save changes immediately to ensure persistence
                            SaveTabState();

                            logger.Info($"Script name updated to: {newName}");
                        }
                    }
                }
                ((DataGridView)sender).AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TabGrid_CellEndEdit: " + ex.Message);
            }
        }

        private void TabGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                var grid = sender as DataGridView;
                if (grid != null && e.RowIndex >= 0)
                {
                    var script = grid.Rows[e.RowIndex].Tag as Scripts;
                    if (script != null)
                    {
                        if (e.ColumnIndex == 0) // Play/Pause column (now first column)
                        {
                            HandlePlayPauseClick(script, e.RowIndex, grid);
                        }
                        else if (e.ColumnIndex == 2) // Record column
                        {
                            HandleRecordClick(script, e.RowIndex, grid);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TabGrid_CellClick: " + ex.Message);
            }
        }

        private void TabGrid_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                var grid = sender as DataGridView;
                if (grid != null && grid.SelectedCells.Count > 0)
                {
                    var cell = grid.SelectedCells[0];
                    var rowIndex = cell.RowIndex;
                    if (rowIndex >= 0 && rowIndex < grid.Rows.Count)
                    {
                        var script = grid.Rows[rowIndex].Tag as Scripts;
                        if (script != null)
                        {
                            currentSelectedScript = script;
                            
                            // Update formatting controls to reflect selected cell's formatting
                            if (cell.ColumnIndex == 1) // Only for Script Name column
                            {
                                UpdateFormattingControlsFromCell(cell, script);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TabGrid_SelectionChanged: " + ex.Message);
            }
        }

        private void TabGrid_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            try
            {
                var grid = sender as DataGridView;
                if (grid != null && e.ColumnIndex == 1) // Script Name column
                {
                    // Clear current selection
                    grid.ClearSelection();
                    
                    // Select all cells in the Script Name column that have content
                    for (int i = 0; i < grid.Rows.Count; i++)
                    {
                        var cellValue = grid.Rows[i].Cells[1].Value?.ToString();
                        if (!string.IsNullOrEmpty(cellValue))
                        {
                            grid.Rows[i].Cells[1].Selected = true;
                        }
                    }
                    
                    logger.Debug($"Selected all Script Name cells with content");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TabGrid_ColumnHeaderMouseClick: " + ex.Message);
            }
        }

        private void TabGrid_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                var grid = sender as DataGridView;
                if (grid != null && e.Control && e.KeyCode == Keys.A)
                {
                    // Ctrl+A: Select all Script Name cells with content
                    e.Handled = true; // Prevent default Ctrl+A behavior
                    
                    grid.ClearSelection();
                    
                    // Select all cells in the Script Name column that have content
                    for (int i = 0; i < grid.Rows.Count; i++)
                    {
                        var cellValue = grid.Rows[i].Cells[1].Value?.ToString();
                        if (!string.IsNullOrEmpty(cellValue))
                        {
                            grid.Rows[i].Cells[1].Selected = true;
                        }
                    }
                    
                    logger.Debug($"Ctrl+A: Selected all Script Name cells with content");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TabGrid_KeyDown: " + ex.Message);
            }
        }

        private void TabGrid_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            try
            {
                var grid = sender as DataGridView;
                if (grid != null && e.Button == MouseButtons.Right && e.RowIndex >= 0)
                {
                    // Right-click on a row - show context menu for inserting rows
                    ShowRowInsertContextMenu(grid, e.RowIndex, e.Location);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TabGrid_CellMouseClick: " + ex.Message);
            }
        }

        private void TabGrid_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                var grid = sender as DataGridView;
                if (grid == null || e.RowIndex < 0 || e.ColumnIndex < 0) return;

                // Only for action columns: PlayPause(0) and Record(2)
                if (e.ColumnIndex == 0 || e.ColumnIndex == 2)
                {
                    grid.Cursor = Cursors.Hand;
                    grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.FromArgb(230, 230, 230); // light gray highlight
                }
            }
            catch { }
        }

        private void TabGrid_CellMouseLeave(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                var grid = sender as DataGridView;
                if (grid == null || e.RowIndex < 0 || e.ColumnIndex < 0) return;

                if (e.ColumnIndex == 0 || e.ColumnIndex == 2)
                {
                    grid.Cursor = Cursors.Default;
                    // Reset to White which is our base color for these cells
                    grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.White;
                }
            }
            catch { }
        }

        private void ShowRowInsertContextMenu(DataGridView grid, int rowIndex, Point location)
        {
            try
            {
                var contextMenu = new ContextMenuStrip();
                
                // Insert Above option
                var insertAboveItem = new ToolStripMenuItem("Insert New Row Above");
                insertAboveItem.Click += (s, e) => InsertRowAt(grid, rowIndex, true);
                contextMenu.Items.Add(insertAboveItem);
                
                // Insert Below option
                var insertBelowItem = new ToolStripMenuItem("Insert New Row Below");
                insertBelowItem.Click += (s, e) => InsertRowAt(grid, rowIndex, false);
                contextMenu.Items.Add(insertBelowItem);
                
                // Show context menu at the clicked location
                var screenPoint = grid.PointToScreen(location);
                contextMenu.Show(screenPoint);
                
                logger.Debug($"Showed row insert context menu at row {rowIndex}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error showing row insert context menu: " + ex.Message);
            }
        }

        private void InsertRowAt(DataGridView grid, int targetRowIndex, bool insertAbove)
        {
            try
            {
                var currentTab = tabControl.SelectedTab;
                if (currentTab == null || !tabScriptManagers.ContainsKey(currentTab))
                    return;

                var scriptManager = tabScriptManagers[currentTab];
                
                // Calculate the insertion index
                int insertIndex = insertAbove ? targetRowIndex : targetRowIndex + 1;
                
                // Create a new empty script
                var newScript = new Scripts
                {
                    Text = "", // Empty script name
                    FontFamily = "Segoe UI",
                    FontSize = 9,
                    IsBold = false,
                    IsItalic = false,
                    FontColor = "Black"
                };
                
                // Insert the script into the script manager at the correct position
                if (insertIndex >= scriptManager.scriptList.Count)
                {
                    scriptManager.scriptList.Add(newScript);
                }
                else
                {
                    scriptManager.scriptList.Insert(insertIndex, newScript);
                }
                
                // Refresh the grid to show the new row
                PopulateScriptsGrid(grid, scriptManager);
                
                // Update row numbers and select the new row
                UpdateRowNumbers(grid);
                
                // Select the newly inserted row's Script Name cell for immediate editing
                if (insertIndex < grid.Rows.Count)
                {
                    grid.ClearSelection();
                    grid.Rows[insertIndex].Cells[1].Selected = true; // Select Script Name column
                    grid.CurrentCell = grid.Rows[insertIndex].Cells[1];
                    grid.BeginEdit(true); // Start editing immediately
                }
                
                logger.Info($"Inserted new row at index {insertIndex} ({(insertAbove ? "above" : "below")} row {targetRowIndex})");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error inserting row: " + ex.Message);
            }
        }

        private void UpdateRowNumbers(DataGridView grid)
        {
            try
            {
                // Update the Play/Pause column with correct row numbers
                for (int i = 0; i < grid.Rows.Count; i++)
                {
                    if (grid.Rows[i].Cells[0].Value != null)
                    {
                        // Keep the existing play/pause image but could update numbering if needed
                        // The row numbers are typically handled by the grid's row headers or display logic
                    }
                }
                
                logger.Debug($"Updated row numbers for {grid.Rows.Count} rows");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error updating row numbers: " + ex.Message);
            }
        }

        private void PopulateComboBox()
        {
            foreach (FontFamily fontFamily in FontFamily.Families)
            {
                cmbBoxFonts.Items.Add(fontFamily.Name);
            }

            for (int i = 8; i <= 32; i += 2)
            {
                cmbBoxSize.Items.Add(i.ToString());
            }

            cmbBoxColor.Items.Add(Color.Black.Name);
            cmbBoxColor.Items.Add(Color.Red.Name);
            cmbBoxColor.Items.Add(Color.Blue.Name);
            cmbBoxColor.Items.Add(Color.Orange.Name);
            cmbBoxColor.Items.Add(Color.Purple.Name);
            cmbBoxColor.Items.Add(Color.Green.Name);
        }

        private void PlayScriptAudio(Scripts script)
        {
            try
            {
                if (!isAudioPlaying)
                {
                    isAudioPlaying = true;
                    
                    // Set up the WaveOut device to play through the virtual cable output (to customer - C1)
                    waveOut = new WaveOutEvent
                    {
                        DeviceNumber = FindWaveOutDeviceNumber("cable")
                    };
                    
                    // Load the recorded audio file
                    audioFileReader = new AudioFileReader(script.AudioFilePath);
                    
                    // Create a sample provider to monitor audio levels for C1 Recording Level
                    var sampleProvider = audioFileReader.ToSampleProvider();
                    var monitoringProvider = new SampleMonitoringProvider(sampleProvider);
                    monitoringProvider.SampleRead += (sender, args) =>
                    {
                        // Update C1 Recording Level meter (what customer hears)
                        // Intensity is already calculated in SampleMonitoringProvider
                        AudioService.Instance.TriggerRecordingMeterUpdate(args.Intensity);
                    };
                    
                    // Initialize WaveOut with the monitored audio and start playback
                    waveOut.Init(monitoringProvider.ToWaveProvider());
                    
                    // Apply volume control from MainForm (C1 Recording Level - what customer hears)
                    ApplyVolumeFromMainForm(waveOut, "C1Recording");
                    
                    waveOut.Play();
                    
                    // Mute or stop the WaveIn capturing (agent's microphone)
                    if (waveIn != null)
                        waveIn.StopRecording();
                    
                    // Handle Playback Stopped event
                    waveOut.PlaybackStopped += (sender, args) =>
                    {
                        AudioService.Instance.TriggerRecordingMeterUpdate(0);
                        OnPlaybackStopped(sender, args);
                    };

                    // New WaveOut for agent headset (A1 Volume - Recordings level in Agent1 Headset)
                    waveO = new WaveOutEvent
                    {
                        DeviceNumber = 0, // Set the default audio device
                        DesiredLatency = 125 // Adjust latency as needed
                    };

                    audioFileReader2 = new AudioFileReader(script.AudioFilePath);
                    
                    // Create a sample provider to monitor audio levels for A1 Recording playback
                    var sampleProvider2 = audioFileReader2.ToSampleProvider();
                    var monitoringProvider2 = new SampleMonitoringProvider(sampleProvider2);
                    monitoringProvider2.SampleRead += (sender, args) =>
                    {
                        // Update A1 Recording Level meter (agent headset playback)
                        // Intensity is already calculated in SampleMonitoringProvider
                        AudioService.Instance.TriggerSpeakerMeterUpdate(args.Intensity);
                    };
                    
                    waveO.Init(monitoringProvider2.ToWaveProvider());
                    
                    // Apply volume control from MainForm (A1 Recordings level in Agent1 Headset)
                    ApplyVolumeFromMainForm(waveO, "A1Recording");
                    
                    waveO.Play();
                    waveO.PlaybackStopped += (sender, args) =>
                    {
                        AudioService.Instance.TriggerSpeakerMeterUpdate(0);
                        OnPlaybackStopped2(sender, args);
                    };
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error playing script audio: " + ex.Message);
                throw;
            }
        }

        private void StopAudioPlayback()
        {
            if (isAudioPlaying)
            {
                if (waveOut != null && waveOut.PlaybackState == PlaybackState.Playing)
                {
                    waveOut.Stop();
                }

                if (waveO != null && waveO.PlaybackState == PlaybackState.Playing)
                {
                    waveO.Stop();
                }

                isAudioPlaying = false;
            }
        }

        private void StopAllAudioPlayback()
        {
            // Stop current audio and update all script states
            StopAudioPlayback();
            
            Image playImage = LoadImageSafely("1play.png");
            
            foreach (var script in scriptManager.scriptList)
            {
                script.IsPlaying = false;
            }
            
            // Update all Play/Pause buttons in the grid
            for (int i = 0; i < dgvScripts.Rows.Count; i++)
            {
                dgvScripts.Rows[i].Cells["PlayPause"].Value = playImage;
            }
        }

        // Legacy methods - keeping for compatibility but not used in new interface
        private void PopulateScriptsDropDown(string selectedItem = "")
        {
            // This method is no longer used in the new spreadsheet interface
            // Keeping for backward compatibility
        }        

        private void cmbBoxScripts_SelectedIndexChanged(object sender, EventArgs e)
        {
            // This method is no longer used in the new spreadsheet interface
            // Keeping for backward compatibility
        }

        private void ScriptsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                // Save tab state before closing
                SaveTabState();
                
                // Save current script content if one is selected
                if (currentSelectedScript != null)
                {
                    // Script content is now managed directly in the DataGridView cells
                    // No additional saving needed as changes are applied immediately
                }

                // Stop any playing audio
                StopAllAudioPlayback();

                // Dispose audio resources
                if (audioFileReader != null)
                {
                    audioFileReader.Dispose();
                    audioFileReader = null;
                }

                if (waveOut != null)
                    waveOut.Dispose();

                if (audioFileReader2 != null)
                {
                    audioFileReader2.Dispose();
                    audioFileReader2 = null;
                }

                if (waveO != null)
                    waveO.Dispose();

                // Unsubscribe from events
                MacroListChangeNotifier.ListChanged -= LoadContextMenu;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
            }
        }

        private void btnBold_Click(object sender, EventArgs e)
        {
            try
            {
                ApplyFormattingToSelectedCell(FontStyle.Bold);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error applying bold formatting: " + ex.Message);
            }
        }

        private void btnItalic_Click(object sender, EventArgs e)
        {
            try
            {
                ApplyFormattingToSelectedCell(FontStyle.Italic);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error applying italic formatting: " + ex.Message);
            }
        }

        private void ApplyFormattingToSelectedCell(FontStyle style)
        {
            try
            {
                var currentTab = tabControl.SelectedTab;
                if (currentTab != null && tabDataGridViews.ContainsKey(currentTab))
                {
                    var grid = tabDataGridViews[currentTab];
                    if (grid.SelectedCells.Count > 0)
                    {
                        // Apply formatting to all selected cells in Script Name column
                        foreach (DataGridViewCell cell in grid.SelectedCells)
                        {
                            if (cell.ColumnIndex == 1) // Only apply to Script Name column
                            {
                                Font currentFont = cell.Style.Font ?? grid.DefaultCellStyle.Font;
                                FontStyle newStyle = currentFont.Style ^ style;
                                cell.Style.Font = new Font(currentFont.FontFamily, currentFont.Size, newStyle);
                                
                                // Update the script object with formatting properties
                                var script = grid.Rows[cell.RowIndex].Tag as Scripts;
                                if (script != null)
                                {
                                    script.Text = cell.Value?.ToString() ?? "";
                                    script.FontFamily = currentFont.FontFamily.Name;
                                    script.FontSize = currentFont.Size;
                                    script.IsBold = newStyle.HasFlag(FontStyle.Bold);
                                    script.IsItalic = newStyle.HasFlag(FontStyle.Italic);
                                    script.FontColor = cell.Style.ForeColor.IsEmpty ? "Black" : cell.Style.ForeColor.Name;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error applying cell formatting: " + ex.Message);
            }
        }

        private string RemoveCurlyBraces(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // Remove {{ and }} from the input string
            string output = input.Replace("{{", "").Replace("}}", "");
            return output;
        }

        private void richTextBox1_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            try
            {
                string linkText = RemoveCurlyBraces(e.LinkText);
                var macro = macroManager.macroList.Where(x => x.Name == linkText).FirstOrDefault();
                if (macro != null)
                {
                    if (macro.voiceFilePath != string.Empty && File.Exists(macro.voiceFilePath))
                    {
                        AudioService.Instance.PlayAudio(macro);
                    }
                    else
                    {
                        MessageBox.Show("No Voice file is attached with this Key. Please record or upload first.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while performing an operation.");
                logger.Error("ExceptionMessage: " + ex.Message);
                logger.Error("Exception: " + ex.ToString());
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
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var capabilities = WaveOut.GetCapabilities(i);
                if (capabilities.ProductName.ToLower().Contains(targetDeviceName.ToLower()))
                {
                    return i;
                }
            }
            return -1; // Device not found
        }

        /// <summary>
        /// Apply volume control from MainForm to the WaveOut device
        /// </summary>
        /// <param name="waveOutDevice">The WaveOut device to apply volume to</param>
        /// <param name="volumeType">Type of volume control (A1Recording or C1Recording)</param>
        private void ApplyVolumeFromMainForm(WaveOutEvent waveOutDevice, string volumeType)
        {
            try
            {
                if (parentMainForm == null || waveOutDevice == null)
                    return;

                float volumeLevel = 1.0f; // Default volume

                // Get the appropriate volume control value from MainForm
                switch (volumeType)
                {
                    case "A1Recording": // Agent headset volume (A1 Volume - Recordings level in Agent1 Headset)
                        volumeLevel = GetVolumeFromMainForm("volumeA1RecIn");
                        break;
                    case "C1Recording": // Customer call volume (C1 Recording Level)
                        volumeLevel = GetVolumeFromMainForm("volumeA1RecOut");
                        break;
                }

                // Apply the volume to the WaveOut device
                waveOutDevice.Volume = volumeLevel;
                
                logger.Info($"Applied {volumeType} volume: {volumeLevel:F2}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error applying volume for {volumeType}: " + ex.Message);
            }
        }

        /// <summary>
        /// Get volume value from MainForm volume control
        /// </summary>
        /// <param name="controlName">Name of the volume control</param>
        /// <returns>Volume level (0.0 to 1.0)</returns>
        private float GetVolumeFromMainForm(string controlName)
        {
            try
            {
                if (parentMainForm == null)
                {
                    logger.Debug($"parentMainForm is null for {controlName}");
                    return 1.0f;
                }

                // Use reflection to get the volume control from MainForm
                var control = parentMainForm.GetType().GetField(controlName, 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (control != null && control.GetValue(parentMainForm) is VolumeControl volumeControl)
                {
                    float volume = volumeControl.Value / 100f; // Normalize to 0.0 - 1.0
                    logger.Debug($"Got volume from {controlName}: {volume:F2} (VolumeControl value: {volumeControl.Value})");
                    return volume;
                }
                else
                {
                    logger.Debug($"Could not find control {controlName} or it's not a VolumeControl");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error getting volume from {controlName}: " + ex.Message);
            }
            
            return 1.0f; // Default volume if unable to get value
        }

        /// <summary>
        /// Setup real-time volume monitoring from MainForm
        /// </summary>
        private void SetupVolumeMonitoring()
        {
            try
            {
                if (parentMainForm == null)
                    return;

                // Create a timer to periodically check and apply volume changes
                var volumeTimer = new System.Windows.Forms.Timer();
                volumeTimer.Interval = 50; // Check every 50ms for more responsive control
                volumeTimer.Tick += (sender, e) =>
                {
                    try
                    {
                        // Apply volume changes to active WaveOut devices
                        if (waveOut != null && waveOut.PlaybackState == PlaybackState.Playing)
                        {
                            float newVolume = GetVolumeFromMainForm("volumeA1RecOut");
                            if (Math.Abs(waveOut.Volume - newVolume) > 0.005f) // More sensitive detection
                            {
                                waveOut.Volume = newVolume;
                                logger.Debug($"Updated C1 Recording volume to: {newVolume:F2}");
                            }
                        }

                        if (waveO != null && waveO.PlaybackState == PlaybackState.Playing)
                        {
                            float newVolume = GetVolumeFromMainForm("volumeA1RecIn");
                            if (Math.Abs(waveO.Volume - newVolume) > 0.005f) // More sensitive detection
                            {
                                waveO.Volume = newVolume;
                                logger.Debug($"Updated A1 Recording volume to: {newVolume:F2}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error in volume monitoring timer: " + ex.Message);
                    }
                };

                // Start the timer when form loads and stop when form closes
                this.Load += (sender, e) => volumeTimer.Start();
                this.FormClosed += (sender, e) => volumeTimer.Stop();
                
                logger.Info("Volume monitoring setup completed");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting up volume monitoring: " + ex.Message);
            }
        }

        /// <summary>
        /// Public method to manually update volumes (can be called from MainForm)
        /// </summary>
        public void UpdateVolumesFromMainForm()
        {
            try
            {
                logger.Debug("UpdateVolumesFromMainForm called");
                
                if (waveOut != null && waveOut.PlaybackState == PlaybackState.Playing)
                {
                    float newVolume = GetVolumeFromMainForm("volumeA1RecOut");
                    logger.Debug($"Setting waveOut (C1 Recording) volume to: {newVolume:F2}");
                    waveOut.Volume = newVolume;
                }

                if (waveO != null && waveO.PlaybackState == PlaybackState.Playing)
                {
                    float newVolume = GetVolumeFromMainForm("volumeA1RecIn");
                    logger.Debug($"Setting waveO (A1 Recording) volume to: {newVolume:F2}");
                    waveO.Volume = newVolume;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error updating volumes manually: " + ex.Message);
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

                // Reset play button to "Play" state when playback completes
                ResetPlayButtonsToPlayState();

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

        private void ResetPlayButtonsToPlayState()
        {
            try
            {
                Image playImage = LoadImageSafely("1play.png");
                
                // Reset all play buttons in all tabs to "Play" state
                foreach (TabPage tab in tabControl.TabPages)
                {
                    if (tabDataGridViews.ContainsKey(tab))
                    {
                        var grid = tabDataGridViews[tab];
                        foreach (DataGridViewRow row in grid.Rows)
                        {
                            var playPauseCell = row.Cells["PlayPause"];
                            if (playPauseCell != null)
                            {
                                playPauseCell.Value = playImage;
                            }
                        }
                    }
                }
        
                // Also reset the main grid if it exists
                if (dgvScripts != null)
                {
                    foreach (DataGridViewRow row in dgvScripts.Rows)
                    {
                        var playPauseCell = row.Cells["PlayPause"];
                        if (playPauseCell != null)
                        {
                            playPauseCell.Value = playImage;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error resetting play buttons: " + ex.Message);
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

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {           
            // For keys, call the base class implementation
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void LoadContextMenu()
        {
            ContextMenuStrip contextMenu = new ContextMenuStrip();
            Dictionary<string, string> items = new Dictionary<string, string>();
            if (macroManager.macroList.Count > 0)
            {
                foreach (var macro in macroManager.macroList)
                {
                    if (!string.IsNullOrEmpty(macro.voiceFilePath))
                    {
                        items.Add(macro.Name, macro.ColorName);
                    }
                }

                if (items.Count == 0)
                {
                    items.Add("No Macros", Color.Gray.ToString());
                }
            }
            else
            {
                items.Add("No Macros", Color.Gray.ToString());
            }

            foreach (var item in items)
            {
                var menuItem = new ToolStripMenuItem(item.Key);

                if (item.Key != "No Macros")
                {
                    //menuItem.Click += (s, e) => AddLinkToRichTextBox("{{" + item.Key + "}}", Color.FromName(item.Value));
                }
                contextMenu.Items.Add(menuItem);
            }

            // Determine if scrolling is needed
            int threshold = 10;
            if (contextMenu.Items.Count > threshold)
            {
                // Each menu item's height is roughly 22 pixels; adjust as needed for your styling
                int menuItemHeight = 22;

                // Set the maximum height of the context menu to allow for scrolling
                contextMenu.MaximumSize = new Size(0, menuItemHeight * threshold);
            }

            // Removed richTextBox1 context menu - no longer needed
        }

        // Removed AddLinkToRichTextBox - no longer needed without RichTextBox    

        private void cmbBoxFonts_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                var currentTab = tabControl.SelectedTab;
                if (currentTab != null && tabDataGridViews.ContainsKey(currentTab))
                {
                    var grid = tabDataGridViews[currentTab];
                    if (cmbBoxFonts.SelectedItem != null && grid.SelectedCells.Count > 0)
                    {
                        string fontName = cmbBoxFonts.SelectedItem.ToString();
                        
                        // Apply to ALL selected cells in Script Name column
                        foreach (DataGridViewCell cell in grid.SelectedCells)
                        {
                            if (cell.ColumnIndex == 1) // Only apply to Script Name column
                            {
                                Font currentFont = cell.Style.Font ?? grid.DefaultCellStyle.Font;
                                cell.Style.Font = new Font(fontName, currentFont.Size, currentFont.Style);
                                
                                // Update the script object with formatting properties
                                var script = grid.Rows[cell.RowIndex].Tag as Scripts;
                                if (script != null)
                                {
                                    script.FontFamily = fontName;
                                    script.FontSize = currentFont.Size;
                                    script.IsBold = currentFont.Style.HasFlag(FontStyle.Bold);
                                    script.IsItalic = currentFont.Style.HasFlag(FontStyle.Italic);
                                    script.FontColor = cell.Style.ForeColor.IsEmpty ? "Black" : cell.Style.ForeColor.Name;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error changing font: " + ex.Message);
            }
        }

        private void cmbBoxSize_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                var currentTab = tabControl.SelectedTab;
                if (currentTab != null && tabDataGridViews.ContainsKey(currentTab))
                {
                    var grid = tabDataGridViews[currentTab];
                    if (cmbBoxSize.SelectedItem != null && grid.SelectedCells.Count > 0)
                    {
                        float fontSize = float.Parse(cmbBoxSize.SelectedItem.ToString());
                        
                        // Apply to ALL selected cells in Script Name column
                        foreach (DataGridViewCell cell in grid.SelectedCells)
                        {
                            if (cell.ColumnIndex == 1) // Only apply to Script Name column
                            {
                                Font currentFont = cell.Style.Font ?? grid.DefaultCellStyle.Font;
                                cell.Style.Font = new Font(currentFont.FontFamily, fontSize, currentFont.Style);
                                
                                // Update the script object with formatting properties
                                var script = grid.Rows[cell.RowIndex].Tag as Scripts;
                                if (script != null)
                                {
                                    script.FontFamily = currentFont.FontFamily.Name;
                                    script.FontSize = fontSize;
                                    script.IsBold = currentFont.Style.HasFlag(FontStyle.Bold);
                                    script.IsItalic = currentFont.Style.HasFlag(FontStyle.Italic);
                                    script.FontColor = cell.Style.ForeColor.IsEmpty ? "Black" : cell.Style.ForeColor.Name;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error changing font size: " + ex.Message);
            }
        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            // Legacy method - no longer used in new spreadsheet interface
            // Content is automatically saved when switching between scripts
        }
        

        private void cmbBoxColor_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                var currentTab = tabControl.SelectedTab;
                if (currentTab != null && tabDataGridViews.ContainsKey(currentTab))
                {
                    var grid = tabDataGridViews[currentTab];
                    if (cmbBoxColor.SelectedItem != null && grid.SelectedCells.Count > 0)
                    {
                        Color selectedColor = Color.FromName(cmbBoxColor.SelectedItem.ToString());
                        
                        // Apply to ALL selected cells in Script Name column
                        foreach (DataGridViewCell cell in grid.SelectedCells)
                        {
                            if (cell.ColumnIndex == 1) // Only apply to Script Name column
                            {
                                cell.Style.ForeColor = selectedColor;
                                
                                // Update the script object with formatting properties
                                var script = grid.Rows[cell.RowIndex].Tag as Scripts;
                                if (script != null)
                                {
                                    Font currentFont = cell.Style.Font ?? grid.DefaultCellStyle.Font;
                                    script.FontFamily = currentFont.FontFamily.Name;
                                    script.FontSize = currentFont.Size;
                                    script.IsBold = currentFont.Style.HasFlag(FontStyle.Bold);
                                    script.IsItalic = currentFont.Style.HasFlag(FontStyle.Italic);
                                    script.FontColor = selectedColor.Name;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error changing font color: " + ex.Message);
            }
        }

        // Tab management methods
        private void btnAddTab_Click(object sender, EventArgs e)
        {
            try
            {
                if (tabControl.TabPages.Count >= MAX_TABS)
                {
                    MessageBox.Show($"Maximum of {MAX_TABS} tabs allowed.", "Tab Limit", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Create new tab
                TabPage newTab = new TabPage($"Sheet {tabCounter}");
                tabControl.TabPages.Add(newTab);
                
                // Create new DataGridView for this tab
                DataGridView newGrid = CreateDataGridViewForTab();
                newTab.Controls.Add(newGrid);
                
                // Create new ScriptManager for this tab (independent data)
                ScriptManager newManager = new ScriptManager();
                
                // Store references
                tabScriptManagers[newTab] = newManager;
                tabDataGridViews[newTab] = newGrid;
                
                // Setup the grid
                SetupDataGridView(newGrid);
                PopulateScriptsGrid(newGrid, newManager);
                
                // Select the new tab
                tabControl.SelectedTab = newTab;
                
                tabCounter++;
                SaveTabState(); // Persist the new tab immediately
                logger.Info($"New tab created: {newTab.Text}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error creating new tab: " + ex.Message);
            }
        }

        private void tabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                // Update current script manager reference when switching tabs
                var selectedTab = tabControl.SelectedTab;
                if (selectedTab != null && tabScriptManagers.ContainsKey(selectedTab))
                {
                    scriptManager = tabScriptManagers[selectedTab];
                    currentSelectedScript = null;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error switching tabs: " + ex.Message);
            }
        }

        // Tab persistence methods
        [Serializable]
        public class TabStateData
        {
            public List<TabData> Tabs { get; set; } = new List<TabData>();
            public int SelectedTabIndex { get; set; } = 0;
            public int TabCounter { get; set; } = 1;
        }

        [Serializable]
        public class TabData
        {
            public string TabName { get; set; }
            public List<Scripts> Scripts { get; set; } = new List<Scripts>();
        }

        private void SaveTabState()
        {
            try
            {
                var tabState = new TabStateData
                {
                    SelectedTabIndex = tabControl.SelectedIndex,
                    TabCounter = tabCounter
                };

                foreach (TabPage tab in tabControl.TabPages)
                {
                    if (tabScriptManagers.ContainsKey(tab))
                    {
                        var tabData = new TabData
                        {
                            TabName = tab.Text,
                            Scripts = new List<Scripts>(tabScriptManagers[tab].scriptList)
                        };
                        tabState.Tabs.Add(tabData);
                    }
                }

                string json = JsonConvert.SerializeObject(tabState, Formatting.Indented);
                File.WriteAllText(TAB_STATE_FILE, json);
                
                logger.Info($"Tab state saved with {tabState.Tabs.Count} tabs");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error saving tab state: " + ex.Message);
            }
        }

        private bool RestoreTabState()
        {
            try
            {
                if (!File.Exists(TAB_STATE_FILE))
                {
                    logger.Info("No tab state file found, creating default tabs");
                    return false;
                }

                string json = File.ReadAllText(TAB_STATE_FILE);
                var tabState = JsonConvert.DeserializeObject<TabStateData>(json);

                if (tabState?.Tabs == null || tabState.Tabs.Count == 0)
                {
                    logger.Info("Empty tab state, creating default tabs");
                    return false;
                }

                // Clear existing tabs
                tabControl.TabPages.Clear();
                
                foreach (var tabData in tabState.Tabs)
                {
                    var tab = new TabPage(tabData.TabName); // Use saved tab name
                    var grid = CreateDataGridViewForTab();
                    tab.Controls.Add(grid);

                    var managerForTab = new ScriptManager();
                    if (tabData.Scripts != null)
                    {
                        managerForTab.scriptList.AddRange(tabData.Scripts);
                    }

                    tabScriptManagers[tab] = managerForTab;
                    tabDataGridViews[tab] = grid;

                    SetupDataGridView(grid);
                    PopulateScriptsGrid(grid, managerForTab);

                    tabControl.TabPages.Add(tab);
                }

                // Add context menu for tab name editing
                SetupTabContextMenu();

                // Restore selected tab index
                if (tabState.SelectedTabIndex >= 0 && tabState.SelectedTabIndex < tabControl.TabPages.Count)
                {
                    tabControl.SelectedIndex = tabState.SelectedTabIndex;
                }
                else
                {
                    tabControl.SelectedIndex = 0;
                }

                // Restore tab counter
                tabCounter = tabState.TabCounter;

                // Update current script manager reference
                var selectedTab = tabControl.SelectedTab;
                if (selectedTab != null && tabScriptManagers.ContainsKey(selectedTab))
                {
                    scriptManager = tabScriptManagers[selectedTab];
                }

                // Check if any scripts were missing IDs and trigger a save to persist them
                bool needsSave = false;
                foreach (var tabData in tabState.Tabs)
                {
                    if (tabData.Scripts != null)
                    {
                        foreach (var script in tabData.Scripts)
                        {
                            // If Id is null or empty, it should have been initialized by the property initializer, 
                            // but we check just in case or if we want to be explicit.
                            if (string.IsNullOrEmpty(script.Id))
                            {
                                script.Id = Guid.NewGuid().ToString();
                                needsSave = true;
                            }
                        }
                    }
                }

                if (needsSave)
                {
                    logger.Info("Generated missing IDs for scripts, saving tab state.");
                    SaveTabState();
                }

                logger.Info($"Tab state restored with {tabState.Tabs.Count} tabs, selected tab: {tabControl.SelectedIndex}");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error restoring tab state: " + ex.Message);
                return false;
            }
        }

        private void SetupFontComboBox()
        {
            try
            {
                // Set the combobox to owner-drawn mode
                cmbBoxFonts.DrawMode = DrawMode.OwnerDrawFixed;
                cmbBoxFonts.ItemHeight = 20;
                
                // Add font families to the combobox
                cmbBoxFonts.Items.Clear();
                foreach (FontFamily fontFamily in FontFamily.Families)
                {
                    try
                    {
                        // Only add fonts that support regular style
                        if (fontFamily.IsStyleAvailable(FontStyle.Regular))
                        {
                            cmbBoxFonts.Items.Add(fontFamily.Name);
                        }
                    }
                    catch
                    {
                        // Skip fonts that cause issues
                        continue;
                    }
                }
                
                // Set default font
                cmbBoxFonts.SelectedItem = "Segoe UI";
                
                // Add the DrawItem event handler
                cmbBoxFonts.DrawItem += CmbBoxFonts_DrawItem;
                
                logger.Info($"Font combobox setup with {cmbBoxFonts.Items.Count} fonts");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error setting up font combobox: " + ex.Message);
            }
        }

        private void CmbBoxFonts_DrawItem(object sender, DrawItemEventArgs e)
        {
            try
            {
                if (e.Index < 0) return;
                
                ComboBox combo = sender as ComboBox;
                string fontName = combo.Items[e.Index].ToString();
                
                // Draw the background
                e.DrawBackground();
                
                // Create font for this item
                Font itemFont = null;
                try
                {
                    itemFont = new Font(fontName, 9f, FontStyle.Regular);
                }
                catch
                {
                    // Fallback to default font if the font can't be created
                    itemFont = new Font("Segoe UI", 9f, FontStyle.Regular);
                }
                
                // Draw the text in the font's own style
                using (SolidBrush brush = new SolidBrush(e.ForeColor))
                {
                    e.Graphics.DrawString(fontName, itemFont, brush, e.Bounds.Left + 2, e.Bounds.Top + 2);
                }
                
                // Draw focus rectangle
                e.DrawFocusRectangle();
                
                // Dispose the font
                if (itemFont != null)
                {
                    itemFont.Dispose();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error drawing font combobox item: " + ex.Message);
            }
        }

        private void ApplySavedFormatting(DataGridViewCell cell, Scripts script)
        {
            try
            {
                // Create font style based on saved properties
                FontStyle style = FontStyle.Regular;
                if (script.IsBold) style |= FontStyle.Bold;
                if (script.IsItalic) style |= FontStyle.Italic;
                
                // Apply font formatting
                Font scriptFont = new Font(script.FontFamily, script.FontSize, style);
                cell.Style.Font = scriptFont;
                
                // Apply color formatting
                Color fontColor = Color.FromName(script.FontColor);
                if (fontColor.IsKnownColor)
                {
                    cell.Style.ForeColor = fontColor;
                }
                else
                {
                    cell.Style.ForeColor = Color.Black; // Fallback color
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error applying saved formatting: " + ex.Message);
                // Apply default formatting on error
                cell.Style.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                cell.Style.ForeColor = Color.Black;
            }
        }

        private void UpdateFormattingControlsFromCell(DataGridViewCell cell, Scripts script)
        {
            try
            {
                // Temporarily disable event handlers to prevent recursive calls
                cmbBoxFonts.SelectedIndexChanged -= cmbBoxFonts_SelectedIndexChanged;
                cmbBoxSize.SelectedIndexChanged -= cmbBoxSize_SelectedIndexChanged;
                cmbBoxColor.SelectedIndexChanged -= cmbBoxColor_SelectedIndexChanged;

                // Get font from cell or use script properties as fallback
                Font cellFont = cell.Style.Font ?? new Font(script.FontFamily, script.FontSize, 
                    (script.IsBold ? FontStyle.Bold : FontStyle.Regular) | 
                    (script.IsItalic ? FontStyle.Italic : FontStyle.Regular));

                // Update font family combobox
                if (cmbBoxFonts.Items.Contains(cellFont.FontFamily.Name))
                {
                    cmbBoxFonts.SelectedItem = cellFont.FontFamily.Name;
                }
                else
                {
                    cmbBoxFonts.Text = cellFont.FontFamily.Name;
                }

                // Update font size combobox
                string sizeText = cellFont.Size.ToString();
                if (cmbBoxSize.Items.Contains(sizeText))
                {
                    cmbBoxSize.SelectedItem = sizeText;
                }
                else
                {
                    cmbBoxSize.Text = sizeText;
                }

                // Update font color combobox
                Color cellColor = cell.Style.ForeColor.IsEmpty ? Color.Black : cell.Style.ForeColor;
                string colorName = cellColor.IsKnownColor ? cellColor.Name : "Black";
                if (cmbBoxColor.Items.Contains(colorName))
                {
                    cmbBoxColor.SelectedItem = colorName;
                }
                else
                {
                    cmbBoxColor.Text = colorName;
                }

                // Update bold/italic button states (visual feedback)
                btnBold.BackColor = cellFont.Bold ? Color.LightBlue : Color.FromArgb(245, 245, 245);
                btnItalic.BackColor = cellFont.Italic ? Color.LightBlue : Color.FromArgb(245, 245, 245);

                // Re-enable event handlers
                cmbBoxFonts.SelectedIndexChanged += cmbBoxFonts_SelectedIndexChanged;
                cmbBoxSize.SelectedIndexChanged += cmbBoxSize_SelectedIndexChanged;
                cmbBoxColor.SelectedIndexChanged += cmbBoxColor_SelectedIndexChanged;

                logger.Info($"Updated formatting controls for cell: Font={cellFont.FontFamily.Name}, Size={cellFont.Size}, Color={colorName}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error updating formatting controls from cell: " + ex.Message);
                
                // Re-enable event handlers in case of error
                cmbBoxFonts.SelectedIndexChanged += cmbBoxFonts_SelectedIndexChanged;
                cmbBoxSize.SelectedIndexChanged += cmbBoxSize_SelectedIndexChanged;
                cmbBoxColor.SelectedIndexChanged += cmbBoxColor_SelectedIndexChanged;
            }
        }

        private void ScriptsForm_Resize(object sender, EventArgs e)
        {
            if (pictureBox2 != null)
            {
                pictureBox2.Left = (this.ClientSize.Width - pictureBox2.Width) / 2;
            }
        }

        private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            var tc = (TabControl)sender;
            var tabPage = tc.TabPages[e.Index];
            var bounds = tc.GetTabRect(e.Index);

            // Gap between tabs (adjust as needed)
            int gap = 6; // ~1/8 inch
            var drawRect = new Rectangle(bounds.Left + gap, bounds.Top, bounds.Width - 2 * gap, bounds.Height);

            // Fill background with tab's back color
            using (var bgBrush = new SolidBrush(tabPage.BackColor))
            {
                e.Graphics.FillRectangle(bgBrush, drawRect);
            }

            // Determine font weight: bold if selected, regular otherwise
            bool isSelected = e.Index == tc.SelectedIndex;
            FontStyle fontStyle = isSelected ? FontStyle.Bold : FontStyle.Regular;
            using (var tabFont = new Font(tc.Font, fontStyle))
            {
                // Draw text centered
                TextRenderer.DrawText(e.Graphics, tabPage.Text, tabFont, drawRect, tabPage.ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
