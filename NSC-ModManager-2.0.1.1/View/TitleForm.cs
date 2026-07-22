using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using NSC_ModManager.Model;
using NSC_ModManager.ViewModel;

namespace NSC_ModManager.View
{
    /// <summary>
    /// WinForms replacement for the old WPF TitleView/MainWindow. Functionally
    /// equivalent (same TitleViewModel, same commands, same properties) but the
    /// visual layout is a plain, no-frills WinForms UI instead of the original
    /// Naruto-themed WPF chrome -- per explicit request, form over fidelity, as
    /// long as it runs cleanly under Wine/Winlator.
    ///
    /// Simplifications vs. the original (all purely cosmetic, no lost functionality):
    ///  - Screenshot carousel swaps instantly instead of WPF's 0.4s cross-fade.
    ///  - The three WPF "visibility-toggled" panels (mod manager / options / credits)
    ///    are plain TabPages instead of animated overlays.
    ///  - Credits are a scrollable read-only text block instead of individually
    ///    colored/styled labels per name.
    ///  - Color pickers are simple buttons + the system ColorDialog.
    ///  - The Kurama mascot's tail-wiggle animation and the mode-toggle bounce
    ///    animation are omitted; the mascot image and dialogue text remain.
    /// </summary>
    public partial class TitleForm : Form
    {
        private readonly TitleViewModel VM;
        private System.Windows.Forms.Timer _screenshotTimer;

        private DataGridView modGrid;
        private BindingSource modBindingSource;

        private PictureBox screenshotBox;
        private Label screenshotCounterLabel;

        private Label modNameLabel;
        private TextBox modDescriptionBox;
        private PictureBox modIconBox;
        private Label modAuthorLabel, modVersionLabel, modLastUpdateLabel;

        private CheckBox motionBlurCheckBox;
        private ComboBox stretchModeCombo;
        private TextBox backgroundImagePathBox, rootFolderPathBox, rootFolderPathNS4Box, modManagerFolderBox;
        private Button backgroundColorBtn, buttonColorBtn, textColorBtn;

        private RichTextBox creditsBox;
        private Label kuramaDialogLabel, kuramaNameLabel;
        private PictureBox kuramaBox;
        private Label loadingLabel;
        private CheckBox gameModeToggle;

        private TabControl tabs;
        private TabPage modsTab, optionsTab, creditsTab;

        public TitleForm()
        {
            VM = new TitleViewModel();
            Text = "NSC Mod Manager";
            Width = 1315;
            Height = 860;
            StartPosition = FormStartPosition.CenterScreen;

            BuildMenu();
            BuildTabs();
            BuildKuramaAndLoadingOverlay();

            AllowDrop = true;
            DragEnter += TitleForm_DragEnter;
            DragDrop += TitleForm_DragDrop;

            InitializeScreenshotTimer();
            RefreshModDetailPanel();

            VM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TitleViewModel.SelectedMod) ||
                    e.PropertyName == nameof(TitleViewModel.CurrentScreenshot) ||
                    e.PropertyName == nameof(TitleViewModel.HasScreenshots))
                {
                    if (InvokeRequired) BeginInvoke(new Action(RefreshModDetailPanel));
                    else RefreshModDetailPanel();
                }
                if (e.PropertyName == nameof(TitleViewModel.KuramaDialog) || e.PropertyName == nameof(TitleViewModel.KuramaName))
                {
                    if (InvokeRequired) BeginInvoke(new Action(RefreshKurama));
                    else RefreshKurama();
                }
            };
        }

        // ---------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------
        private void BuildMenu()
        {
            var menu = new MenuStrip();

            var modManagement = new ToolStripMenuItem(Loc("m_mainWindow_001"));
            modManagement.Click += (s, e) => RunCommand(VM.RosterEditorCommand);

            var moddingApi = new ToolStripMenuItem(Loc("m_mainWindow_006"));
            var installApi = new ToolStripMenuItem(Loc("m_mainWindow_010"));
            installApi.Click += (s, e) => RunCommand(VM.InstallModdingAPICommand);
            var deleteApi = new ToolStripMenuItem(Loc("m_mainWindow_011"));
            deleteApi.Click += (s, e) => RunCommand(VM.DeleteModdingAPICommand);
            moddingApi.DropDownItems.Add(installApi);
            moddingApi.DropDownItems.Add(deleteApi);

            var discord = new ToolStripMenuItem(Loc("m_mainWindow_007"));
            discord.Click += (s, e) => RunCommand(VM.VisitModdingGroupCommand);

            var boosty = new ToolStripMenuItem(Loc("m_mainWindow_012"));
            boosty.Click += (s, e) => RunCommand(VM.BoostyCommand);

            var github = new ToolStripMenuItem(Loc("m_mainWindow_013"));
            github.Click += (s, e) => RunCommand(VM.VisitGitHubPageCommand);

            var options = new ToolStripMenuItem(Loc("m_mainWindow_005"));
            options.Click += (s, e) => tabs.SelectedTab = optionsTab;

            var cleanRoot = new ToolStripMenuItem(Loc("m_modmanager_007"));
            cleanRoot.Click += (s, e) => RunCommand(VM.CleanGameRootCommand);

            var credits = new ToolStripMenuItem(Loc("m_mainWindow_008"));
            credits.Click += (s, e) => tabs.SelectedTab = creditsTab;

            menu.Items.Add(modManagement);
            menu.Items.Add(moddingApi);
            menu.Items.Add(discord);
            menu.Items.Add(boosty);
            menu.Items.Add(github);
            menu.Items.Add(options);
            menu.Items.Add(cleanRoot);
            menu.Items.Add(credits);

            MainMenuStrip = menu;
            Controls.Add(menu);
        }

        // ---------------------------------------------------------------
        // Tabs: Mods / Options / Credits (replaces the original's
        // visibility-toggled overlay panels)
        // ---------------------------------------------------------------
        private void BuildTabs()
        {
            tabs = new TabControl { Dock = DockStyle.Fill };
            modsTab = new TabPage(Loc("m_mainWindow_001"));
            optionsTab = new TabPage(Loc("m_mainWindow_005"));
            creditsTab = new TabPage(Loc("m_mainWindow_008"));

            BuildModsTab();
            BuildOptionsTab();
            BuildCreditsTab();

            tabs.TabPages.Add(modsTab);
            tabs.TabPages.Add(optionsTab);
            tabs.TabPages.Add(creditsTab);
            Controls.Add(tabs);
        }

        private void BuildModsTab()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

            // --- left: mod list + install/delete/compile/refresh buttons ---
            var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            modBindingSource = new BindingSource { DataSource = VM.ModManagerList };
            modGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                DataSource = modBindingSource,
                AutoGenerateColumns = false,
                AllowDrop = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
            };
            modGrid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(ModManagerModel.EnableMod), HeaderText = "On", Width = 40 });
            modGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ModManagerModel.ModName), HeaderText = "Mod", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            modGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ModManagerModel.Author), HeaderText = "Author", Width = 120 });
            modGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ModManagerModel.Version), HeaderText = "Version", Width = 80 });

            modGrid.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var col = modGrid.Columns[e.ColumnIndex];
                if (col.DataPropertyName == nameof(ModManagerModel.EnableMod))
                {
                    var item = modBindingSource[e.RowIndex] as ModManagerModel;
                    if (item != null && VM.EnableModIsCheckedCommand != null && VM.EnableModIsCheckedCommand.CanExecute(item))
                        VM.EnableModIsCheckedCommand.Execute(item);
                }
            };
            modGrid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (modGrid.IsCurrentCellDirty) modGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            modGrid.SelectionChanged += (s, e) =>
            {
                VM.SelectedMod = modGrid.CurrentRow?.DataBoundItem as ModManagerModel;
                RefreshModDetailPanel();
            };
            modGrid.AllowDrop = true;
            modGrid.DragEnter += TitleForm_DragEnter;
            modGrid.DragDrop += TitleForm_DragDrop;

            var buttonRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            var installBtn = new Button { Text = Loc("m_mainWindow_010"), AutoSize = true };
            installBtn.Click += (s, e) => BrowseAndInstallMod();
            var deleteBtn = new Button { Text = Loc("m_mainWindow_011"), AutoSize = true };
            deleteBtn.Click += (s, e) => RunCommand(VM.DeleteModCommand, VM.SelectedMod);
            var refreshBtn = new Button { Text = "Refresh", AutoSize = true };
            refreshBtn.Click += (s, e) => RunCommand(VM.RefreshModListCommand);
            var compileBtn = new Button { Text = "Compile Mods && Launch", AutoSize = true };
            compileBtn.Click += (s, e) => RunCommand(VM.CompileModsCommand);
            var rosterBtn = new Button { Text = Loc("m_rosterEditor"), AutoSize = true };
            rosterBtn.Click += (s, e) => RunCommand(VM.RosterEditorCommand);

            gameModeToggle = new CheckBox { Text = "S4 mode", AutoSize = true, Checked = VM.IsS4 };
            gameModeToggle.CheckedChanged += (s, e) =>
            {
                VM.IsS4 = gameModeToggle.Checked;
                RunCommand(VM.ChangeGame);
            };

            buttonRow.Controls.Add(installBtn);
            buttonRow.Controls.Add(deleteBtn);
            buttonRow.Controls.Add(refreshBtn);
            buttonRow.Controls.Add(compileBtn);
            buttonRow.Controls.Add(rosterBtn);
            buttonRow.Controls.Add(gameModeToggle);

            left.Controls.Add(modGrid, 0, 0);
            left.Controls.Add(buttonRow, 0, 1);

            // --- right: selected mod details + screenshot carousel ---
            var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 8, ColumnCount = 1 };
            modIconBox = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Height = 150, Dock = DockStyle.Top };
            modNameLabel = new Label { Font = new Font(Font, FontStyle.Bold), AutoSize = false, Height = 24, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter };
            modAuthorLabel = new Label { Dock = DockStyle.Top, Height = 20 };
            modVersionLabel = new Label { Dock = DockStyle.Top, Height = 20 };
            modLastUpdateLabel = new Label { Dock = DockStyle.Top, Height = 20 };
            modDescriptionBox = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };

            screenshotBox = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Height = 220, Dock = DockStyle.Top, BackColor = Color.Black };
            screenshotBox.Click += (s, e) => AdvanceScreenshot();
            screenshotCounterLabel = new Label { Dock = DockStyle.Top, Height = 18, TextAlign = ContentAlignment.MiddleRight };

            right.Controls.Add(screenshotBox);
            right.Controls.Add(screenshotCounterLabel);
            right.Controls.Add(modIconBox);
            right.Controls.Add(modNameLabel);
            right.Controls.Add(modAuthorLabel);
            right.Controls.Add(modVersionLabel);
            right.Controls.Add(modLastUpdateLabel);
            right.Controls.Add(modDescriptionBox);

            root.Controls.Add(left, 0, 0);
            root.Controls.Add(right, 1, 0);
            modsTab.Controls.Add(root);
        }

        private void BuildOptionsTab()
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoScroll = true };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            int row = 0;

            void AddRow(string labelKey, Control control, Button browseButton = null)
            {
                panel.RowCount = row + 1;
                panel.Controls.Add(new Label { Text = Loc(labelKey), AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
                control.Dock = DockStyle.Fill;
                panel.Controls.Add(control, 1, row);
                if (browseButton != null)
                {
                    browseButton.Dock = DockStyle.Fill;
                    panel.Controls.Add(browseButton, 2, row);
                }
                row++;
            }

            stretchModeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            stretchModeCombo.Items.AddRange(new object[] { Loc("m_option002"), Loc("m_option003"), Loc("m_option005") });
            stretchModeCombo.SelectedIndex = Math.Max(0, Math.Min(2, VM.StretchMode_field));
            stretchModeCombo.SelectedIndexChanged += (s, e) => VM.StretchMode_field = stretchModeCombo.SelectedIndex;
            AddRow("m_option001", stretchModeCombo);

            backgroundColorBtn = MakeColorButton(VM.BackgroundColor_field, c => VM.BackgroundColor_field = ColorToHex(c));
            AddRow("m_option006", backgroundColorBtn);

            buttonColorBtn = MakeColorButton(VM.ButtonColor_field, c => VM.ButtonColor_field = ColorToHex(c));
            AddRow("m_option007", buttonColorBtn);

            textColorBtn = MakeColorButton(VM.TextColor_field, c => VM.TextColor_field = ColorToHex(c));
            AddRow("m_option011", textColorBtn);

            backgroundImagePathBox = new TextBox { Text = VM.BackgroundImagePath_field };
            backgroundImagePathBox.TextChanged += (s, e) => VM.BackgroundImagePath_field = backgroundImagePathBox.Text;
            var bgBrowse = new Button { Text = Loc("m_option010") };
            bgBrowse.Click += (s, e) => RunCommand(VM.SelectImageBackgroundCommand);
            AddRow("m_option009", backgroundImagePathBox, bgBrowse);

            rootFolderPathBox = new TextBox { Text = VM.RootFolderPath_field };
            rootFolderPathBox.TextChanged += (s, e) => VM.RootFolderPath_field = rootFolderPathBox.Text;
            var rootBrowse = new Button { Text = Loc("m_option012") };
            rootBrowse.Click += (s, e) => RunCommand(VM.SelectRootFolderCommand);
            AddRow("m_option013", rootFolderPathBox, rootBrowse);

            rootFolderPathNS4Box = new TextBox { Text = VM.RootFolderPathNS4_field };
            rootFolderPathNS4Box.TextChanged += (s, e) => VM.RootFolderPathNS4_field = rootFolderPathNS4Box.Text;
            var rootNS4Browse = new Button { Text = Loc("m_option012") };
            rootNS4Browse.Click += (s, e) => RunCommand(VM.SelectRootFolderNS4Command);
            AddRow("m_option017", rootFolderPathNS4Box, rootNS4Browse);

            modManagerFolderBox = new TextBox { Text = VM.ModManagerFolder_field };
            modManagerFolderBox.TextChanged += (s, e) => VM.ModManagerFolder_field = modManagerFolderBox.Text;
            var mmBrowse = new Button { Text = Loc("m_option019") };
            mmBrowse.Click += (s, e) => RunCommand(VM.SelectModManagerFolderCommand);
            AddRow("m_option018", modManagerFolderBox, mmBrowse);

            motionBlurCheckBox = new CheckBox { Text = Loc("m_option015"), Checked = VM.EnableMotionBlur_field, AutoSize = true };
            motionBlurCheckBox.CheckedChanged += (s, e) => VM.EnableMotionBlur_field = motionBlurCheckBox.Checked;
            AddRow("m_option014", motionBlurCheckBox);

            var saveBtn = new Button { Text = Loc("m_option004"), AutoSize = true };
            saveBtn.Click += (s, e) => RunCommand(VM.SaveSettingsCommand);
            var resetBtn = new Button { Text = Loc("m_option009"), AutoSize = true };
            resetBtn.Click += (s, e) => RunCommand(VM.ResetSettingsCommand);
            var saveRow = new FlowLayoutPanel { AutoSize = true };
            saveRow.Controls.Add(saveBtn);
            saveRow.Controls.Add(resetBtn);
            panel.RowCount = row + 1;
            panel.Controls.Add(saveRow, 1, row);
            row++;

            optionsTab.Controls.Add(panel);
        }

        private Button MakeColorButton(string initialHex, Action<System.Drawing.Color> onPicked)
        {
            var btn = new Button { BackColor = HexToColor(initialHex), Text = "" };
            btn.Click += (s, e) =>
            {
                using (var dlg = new ColorDialog { Color = btn.BackColor })
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        btn.BackColor = dlg.Color;
                        onPicked(dlg.Color);
                    }
                }
            };
            return btn;
        }

        private static Color HexToColor(string hex)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(hex))
                    return ColorTranslator.FromHtml(hex.StartsWith("#") ? hex : "#" + hex);
            }
            catch { /* fall through to default */ }
            return Color.Gray;
        }

        private static string ColorToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private void BuildCreditsTab()
        {
            // Simplified from the original's per-name colored-label grid into a single
            // scrollable text block, grouped by year -- same names, same information,
            // far less layout code.
            creditsBox = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.White, BorderStyle = BorderStyle.None };
            creditsBox.Text = BuildCreditsText();
            creditsTab.Controls.Add(creditsBox);
        }

        private string BuildCreditsText()
        {
            // Same contributor lists as the original XAML's 2024/2025/2026 blocks.
            return
                Loc("m_credits_001") + "\n" + Loc("m_credits_002") + "\n\n" +
                "== 2024 ==\n" +
                "Carrington Bennett, justcamtro, EliteAce, Chakra Warrior, Halucygeno, little Damien, HydraBladeZ, Jonathan Nefores, Anime Knight, Alex Olney,\n" +
                "Nate Roberts, Shoii, Steinixos, UltimateOmbuStorm, Amritz kenzy, Naggingclub5, Jonas Martinez, N&D Productions, The Brothers, Shoyo,\n" +
                "G3ku, Hunter Yang, Aleksa Nikolic, Ceaz Azce, Klim Sapranenkov, dark94, Xu Yuqian, Fearless Intelligent, Erik Petrosyan, Moeru Storm, Bully\n\n" +
                "== 2025 ==\n" +
                "Geegeboy, HallucinatingGenius, Jackwans\n\n" +
                "Thanks to everyone who supported this project.";
        }

        // ---------------------------------------------------------------
        // Kurama mascot + loading overlay (decorative; tail-wiggle and
        // storyboard bounce animations from the original are omitted)
        // ---------------------------------------------------------------
        private void BuildKuramaAndLoadingOverlay()
        {
            kuramaBox = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Width = 150, Height = 150 };
            kuramaNameLabel = new Label { AutoSize = true };
            kuramaDialogLabel = new Label { AutoSize = false, Width = 700, Height = 60 };
            loadingLabel = new Label { Text = "Loading...", Visible = false, AutoSize = true, ForeColor = Color.White };

            // These sit on their own bottom strip below the tab control.
            var bottomStrip = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 160, FlowDirection = FlowDirection.LeftToRight };
            bottomStrip.Controls.Add(kuramaBox);
            var dialogPanel = new TableLayoutPanel { AutoSize = true, RowCount = 2 };
            dialogPanel.Controls.Add(kuramaNameLabel, 0, 0);
            dialogPanel.Controls.Add(kuramaDialogLabel, 0, 1);
            bottomStrip.Controls.Add(dialogPanel);
            bottomStrip.Controls.Add(loadingLabel);
            Controls.Add(bottomStrip);

            VM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TitleViewModel.LoadingStatePlay))
                {
                    void Update() => loadingLabel.Visible = VM.LoadingStatePlay == System.Windows.Visibility.Visible;
                    if (InvokeRequired) BeginInvoke((Action)Update); else Update();
                }
            };
        }

        private void RefreshKurama()
        {
            kuramaNameLabel.Text = VM.KuramaName;
            kuramaDialogLabel.Text = VM.KuramaDialog;
        }

        // ---------------------------------------------------------------
        // Screenshot carousel (instant swap instead of WPF cross-fade)
        // ---------------------------------------------------------------
        private void InitializeScreenshotTimer()
        {
            _screenshotTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _screenshotTimer.Tick += (s, e) => AdvanceScreenshot();
            _screenshotTimer.Start();
        }

        private void AdvanceScreenshot()
        {
            if (!VM.HasScreenshots) return;
            var next = VM.GetNextScreenshot();
            if (next?.UnderlyingImage != null)
                screenshotBox.Image = next.UnderlyingImage;
        }

        private void RefreshModDetailPanel()
        {
            if (modNameLabel == null) return; // not constructed yet (can fire during initial DataSource binding)

            modNameLabel.Text = VM.ModName_field;
            modDescriptionBox.Text = VM.ModDescription_field;
            modAuthorLabel.Text = $"{Loc("m_modmanager_003")}: {VM.ModAuthor_field}";
            modVersionLabel.Text = $"{Loc("m_modmanager_004")}: {VM.ModVersion_field}";
            modLastUpdateLabel.Text = $"{Loc("m_modmanager_005")}: {VM.ModLastUpdate_field}";
            modIconBox.Image = VM.ModIconPreview?.UnderlyingImage;

            bool hasShots = VM.HasScreenshots;
            screenshotBox.Visible = hasShots;
            screenshotCounterLabel.Visible = hasShots;
            if (hasShots)
            {
                screenshotBox.Image = VM.CurrentScreenshot?.UnderlyingImage;
                screenshotCounterLabel.Text = $"{VM.CurrentScreenshotIndex}/{VM.TotalScreenshots}";
            }
        }

        // ---------------------------------------------------------------
        // Drag & drop mod install (same logic as the original InstallMod_Drop)
        // ---------------------------------------------------------------
        private void TitleForm_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void TitleForm_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (var modPath in files)
                InstallModFile(modPath);
        }

        private void BrowseAndInstallMod()
        {
            using (var dlg = new OpenFileDialog { Filter = "NSC Mod files (*.nsc;*.ensc;*.uns;*.unse)|*.nsc;*.ensc;*.uns;*.unse|All files (*.*)|*.*" })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                    InstallModFile(dlg.FileName);
            }
        }

        private void InstallModFile(string modPath)
        {
            try
            {
                string modManagerFolder = Properties.Settings.Default.ModManagerFolder;
                if (!Directory.Exists(modManagerFolder))
                {
                    MessageBox.Show("Select Mod folder!");
                    return;
                }
                string installFolder = Path.Combine(modManagerFolder, Path.GetFileNameWithoutExtension(modPath));
                if (Directory.Exists(installFolder))
                    Directory.Delete(installFolder, true);
                Directory.CreateDirectory(installFolder);
                System.IO.Compression.ZipFile.ExtractToDirectory(modPath, installFolder);
                VM.RefreshModList();
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Exclamation.Play();
                MessageBox.Show("Something went wrong.. Report issue on GitHub \n\n" + ex.StackTrace + " \n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------
        private static string Loc(string key)
        {
            var v = System.Windows.Application.Current.Resources[key];
            return v?.ToString() ?? key;
        }

        private static void RunCommand(System.Windows.Input.ICommand cmd, object param = null)
        {
            try
            {
                if (cmd != null && cmd.CanExecute(param))
                    cmd.Execute(param);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Command failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _screenshotTimer?.Stop();
            base.OnFormClosed(e);
        }
    }
}
