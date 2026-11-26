using KiloLedger.Localization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using KiloLedger.Desktop.Data;
using KiloLedger.Desktop.Exchange;

namespace KiloLedger.Desktop
{
    internal sealed class MainForm : Form
    {
        private const string AppVersion = "1.39.3";
        private bool chartShowsCostPerKm;
        private bool _firstRun;

        private string Cur()
        {
            return ToSettings.GetCurrency(repository.Settings);
        }

        private readonly KiloLedgerRepository repository = new KiloLedgerRepository();
        private readonly TextBox pathBox = new TextBox();
        private readonly TabControl tabs = new TabControl();
        private readonly DataGridView carsGrid = CreateGrid();
        private readonly DataGridView fuelGrid = CreateGrid();
        private readonly DataGridView repairsGrid = CreateGrid();
        private readonly DataGridView utilitiesGrid = CreateGrid();
        private readonly DataGridView categoriesGrid = CreateGrid();
        private readonly DataGridView expensesGrid = CreateGrid();
        private readonly DataGridView expenseCategoriesGrid = CreateGrid();
        private readonly TextBox summaryBox = new TextBox();
        private readonly MonthlyChartPanel summaryChart = new MonthlyChartPanel();
        private readonly TabPage summaryTab = new TabPage(L10n.Get("tab.summary")) { Tag = "tab.summary" };
        private readonly TabPage settingsTab = new TabPage(L10n.Get("tab.settings")) { Tag = "tab.settings" };
        private readonly List<Control> settingsLocalizable = new List<Control>();
        private readonly TextBox oilIntervalBox = new TextBox();
        private readonly TextBox oilWarnBox = new TextBox();
        private readonly TextBox beltIntervalBox = new TextBox();
        private readonly TextBox beltWarnBox = new TextBox();
        private readonly TextBox currencyBox = new TextBox();
        private readonly TextBox budgetBox = new TextBox();
        private readonly TextBox budgetWarnBox = new TextBox();
        private readonly TextBox backupRemindBox = new TextBox();
        private readonly TextBox reminderWarnBox = new TextBox();
        private readonly Panel sidebarPanel = new Panel();
        private readonly FlowLayoutPanel navList = new FlowLayoutPanel();
        private readonly List<Button> navButtons = new List<Button>();
        private readonly List<ToolStripMenuItem> localizableMenuItems = new List<ToolStripMenuItem>();
        private readonly List<Control> headerActions = new List<Control>();
        private readonly Label brandTitleLabel = new Label();
        private readonly Label brandVersionLabel = new Label();
        private readonly Label sectionTitleLabel = new Label();
        private Button quickFuelButton;
        private Button saveButton;
        private readonly ContextMenuStrip dbMenu = new ContextMenuStrip();
        private readonly ContextMenuStrip exchangeMenu = new ContextMenuStrip();
        private readonly ContextMenuStrip toolsMenu = new ContextMenuStrip();
        private readonly FlowLayoutPanel intervalRow;
        private readonly ComboBox defaultCarBox = new ComboBox();
        private readonly ComboBox languageBox = new ComboBox();
        private readonly StatusStrip statusStrip = new StatusStrip();
        private readonly ToolStripStatusLabel statusLabel = new ToolStripStatusLabel(L10n.Get("hint.open_folder_or_zip"));
        private readonly ToolStripStatusLabel reportLinkLabel = new ToolStripStatusLabel();
        private string lastDetailedReport = "";
        private string lastReportTitle = "KiloLedger";
        private readonly TextBox googleUrlBox = new TextBox();
        private bool suppressDirty;
        private bool dirty;
        private bool refreshingSummary;

        private readonly TextBox fuelFilterFrom = new TextBox();
        private readonly TextBox fuelFilterTo = new TextBox();
        private readonly TextBox fuelFilterText = new TextBox();
        private readonly ComboBox fuelFilterPurpose = new ComboBox();
        private readonly ComboBox fuelFilterStation = new ComboBox();
        private string fuelFilterFromValue = "";
        private string fuelFilterToValue = "";
        private string fuelFilterTextValue = "";
        private string fuelFilterPurposeValue = "";
        private string fuelFilterStationValue = "";

        private readonly TextBox repairFilterFrom = new TextBox();
        private readonly TextBox repairFilterTo = new TextBox();
        private readonly TextBox repairFilterText = new TextBox();
        private readonly ComboBox repairFilterCategory = new ComboBox();
        private string repairFilterFromValue = "";
        private string repairFilterToValue = "";
        private string repairFilterTextValue = "";
        private string repairFilterCategoryValue = "";

        private readonly TextBox expenseFilterFrom = new TextBox();
        private readonly TextBox expenseFilterTo = new TextBox();
        private readonly TextBox expenseFilterText = new TextBox();
        private readonly ComboBox expenseFilterCategory = new ComboBox();
        private string expenseFilterFromValue = "";
        private string expenseFilterToValue = "";
        private string expenseFilterTextValue = "";
        private string expenseFilterCategoryValue = "";

        private readonly TextBox utilityFilterFrom = new TextBox();
        private readonly TextBox utilityFilterTo = new TextBox();
        private readonly TextBox utilityFilterText = new TextBox();
        private string utilityFilterFromValue = "";
        private string utilityFilterToValue = "";
        private string utilityFilterTextValue = "";

        private readonly TextBox globalSearchBox = new TextBox();
        private bool suppressingMileagePrompt;

        public MainForm()
        {
            // Загрузить сохранённый язык из %AppData%\KiloLedger\language.txt (или настроек базы) до создания UI.
            string savedLang = LoadStartupLanguage();
            if (String.IsNullOrEmpty(savedLang))
                savedLang = ToSettings.GetLanguage(repository.Settings);
            _firstRun = String.IsNullOrEmpty(savedLang);
            LocalizationManager.Instance.CurrentLanguage = _firstRun ? "ru" : savedLang;

            // Обновить текст полей, инициализированных до конструктора
            statusLabel.Text = L10n.Get("hint.open_folder_or_zip");
            summaryTab.Text = L10n.Get("tab.summary");
            settingsTab.Text = L10n.Get("tab.settings");

            Text = "KiloLedger " + ResolveVersion();
            Width = 1180;
            Height = 760;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(940, 600);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(244, 246, 249);
            ForeColor = Color.FromArgb(35, 51, 70);
            Icon loadedIcon = LoadAppIcon();
            if (loadedIcon != null)
                Icon = loadedIcon;

            // Left dark-navy navigation sidebar
            sidebarPanel.Dock = DockStyle.Left;
            sidebarPanel.Width = 212;
            sidebarPanel.BackColor = Color.FromArgb(35, 51, 70);

            Panel brandHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Color.FromArgb(28, 41, 57),
                Padding = new Padding(14, 12, 12, 8)
            };
            PictureBox brandIcon = new PictureBox
            {
                Width = 34,
                Height = 34,
                Dock = DockStyle.Left,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 10, 0)
            };
            try
            {
                if (Icon != null)
                {
                    using (Icon small = new Icon(Icon, new Size(32, 32)))
                        brandIcon.Image = small.ToBitmap();
                }
            }
            catch { }
            Panel brandTextHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 0, 0, 0),
                BackColor = Color.Transparent
            };
            brandTitleLabel.Text = "KiloLedger";
            brandTitleLabel.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            brandTitleLabel.ForeColor = Color.White;
            brandTitleLabel.Dock = DockStyle.Top;
            brandTitleLabel.Height = 24;
            brandVersionLabel.Text = "v" + ResolveVersion();
            brandVersionLabel.Font = new Font("Segoe UI", 8.5F);
            brandVersionLabel.ForeColor = Color.FromArgb(165, 180, 200);
            brandVersionLabel.Dock = DockStyle.Top;
            brandVersionLabel.Height = 18;
            brandTextHost.Controls.Add(brandVersionLabel);
            brandTextHost.Controls.Add(brandTitleLabel);
            Panel brandAccent = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 3,
                BackColor = Color.FromArgb(200, 43, 43)
            };
            brandHeader.Controls.Add(brandTextHost);
            if (brandIcon.Image != null)
                brandHeader.Controls.Add(brandIcon);
            brandHeader.Controls.Add(brandAccent);

            navList.Dock = DockStyle.Fill;
            navList.FlowDirection = FlowDirection.TopDown;
            navList.WrapContents = false;
            navList.AutoScroll = true;
            navList.Padding = new Padding(0, 10, 0, 10);
            navList.BackColor = Color.FromArgb(35, 51, 70);

            sidebarPanel.Controls.Add(navList);
            sidebarPanel.Controls.Add(brandHeader);

            // Responsive two-tier white header (never clips search box or title in windowed/scaled mode)
            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding(16, 8, 16, 6)
            };
            headerPanel.Paint += delegate(object s, PaintEventArgs pe)
            {
                using (Pen p = new Pen(Color.FromArgb(225, 230, 238)))
                    pe.Graphics.DrawLine(p, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };

            Panel headerTopRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.White
            };

            Panel headerLeft = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            sectionTitleLabel.Text = L10n.Get("tab.vehicles");
            sectionTitleLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            sectionTitleLabel.ForeColor = Color.FromArgb(35, 51, 70);
            sectionTitleLabel.Dock = DockStyle.Top;
            sectionTitleLabel.Height = 24;
            pathBox.ReadOnly = true;
            pathBox.BorderStyle = BorderStyle.None;
            pathBox.BackColor = Color.White;
            pathBox.ForeColor = Color.FromArgb(100, 116, 139);
            pathBox.Font = new Font("Segoe UI", 8.5F);
            pathBox.Dock = DockStyle.Top;
            pathBox.Height = 18;
            pathBox.Text = L10n.Get("hint.open_folder_or_zip");
            headerLeft.Controls.Add(pathBox);
            headerLeft.Controls.Add(sectionTitleLabel);

            FlowLayoutPanel searchCluster = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(8, 2, 0, 0),
                BackColor = Color.White
            };
            globalSearchBox.Width = 175;
            globalSearchBox.Margin = new Padding(0, 6, 4, 4);
            globalSearchBox.Text = L10n.Get("button.search_ellipsis");
            globalSearchBox.ForeColor = Color.Gray;
            globalSearchBox.GotFocus += delegate
            {
                if (globalSearchBox.ForeColor == Color.Gray)
                {
                    globalSearchBox.Text = "";
                    globalSearchBox.ForeColor = SystemColors.WindowText;
                }
            };
            globalSearchBox.KeyDown += GlobalSearchKeyDown;
            searchCluster.Controls.Add(globalSearchBox);
            Control searchBtn = MakeButton("button.search", OpenGlobalSearch);
            headerActions.Add(searchBtn);
            searchCluster.Controls.Add(searchBtn);

            headerTopRow.Controls.Add(headerLeft);
            headerTopRow.Controls.Add(searchCluster);

            FlowLayoutPanel actionsBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                Padding = new Padding(0, 4, 0, 2),
                Margin = new Padding(0),
                BackColor = Color.White
            };

            quickFuelButton = MakeAccentButton("button.quick_fuel", OpenQuickFuelEntry);
            headerActions.Add(quickFuelButton);
            actionsBar.Controls.Add(quickFuelButton);

            saveButton = MakeButton("button.save_data", SaveData);
            headerActions.Add(saveButton);
            actionsBar.Controls.Add(saveButton);

            // Database dropdown menu
            dbMenu.Items.Add(MakeMenuItem("button.open_folder", OpenFolder));
            dbMenu.Items.Add(MakeMenuItem("button.open_zip", OpenZip));
            dbMenu.Items.Add(MakeMenuItem("button.new_folder", CreateFolder));
            dbMenu.Items.Add(new ToolStripSeparator());
            dbMenu.Items.Add(MakeMenuItem("button.export_zip", SaveZip));
            Control dbBtn = MakeDropdownButton("menu.database", dbMenu);
            headerActions.Add(dbBtn);
            actionsBar.Controls.Add(dbBtn);

            // Exchange & Google dropdown menu
            exchangeMenu.Items.Add(MakeMenuItem("title.google_sync_hub", OpenGoogleSyncHub));
            exchangeMenu.Items.Add(new ToolStripSeparator());
            exchangeMenu.Items.Add(MakeMenuItem("button.import", OpenImportWizard));
            exchangeMenu.Items.Add(new ToolStripSeparator());
            exchangeMenu.Items.Add(MakeMenuItem("button.export_excel", ExportExcel));
            exchangeMenu.Items.Add(MakeMenuItem("button.import_excel", ImportExcel));
            exchangeMenu.Items.Add(new ToolStripSeparator());
            exchangeMenu.Items.Add(MakeMenuItem("button.export_csv", ExportCsv));
            exchangeMenu.Items.Add(MakeMenuItem("button.import_csv_folder", ImportCsvFolder));
            exchangeMenu.Items.Add(MakeMenuItem("button.csv_templates", ExportCsvTemplates));
            exchangeMenu.Items.Add(new ToolStripSeparator());
            exchangeMenu.Items.Add(MakeMenuItem("button.google_sync", SyncGoogle));
            exchangeMenu.Items.Add(MakeMenuItem("button.import_google", ImportGoogle));
            exchangeMenu.Items.Add(MakeMenuItem("button.google_wizard", OpenGoogleSyncWizard));
            exchangeMenu.Items.Add(MakeMenuItem("button.google_settings", ConfigureGoogleSync));
            Control exBtn = MakeDropdownButton("menu.exchange", exchangeMenu);
            headerActions.Add(exBtn);
            actionsBar.Controls.Add(exBtn);

            // Tools dropdown menu
            toolsMenu.Items.Add(MakeMenuItem("button.trip", OpenTripEntry));
            toolsMenu.Items.Add(MakeMenuItem("button.trip_report", ShowTripReport));
            toolsMenu.Items.Add(new ToolStripSeparator());
            toolsMenu.Items.Add(MakeMenuItem("button.attach_receipt", AttachReceiptPhoto));
            toolsMenu.Items.Add(MakeMenuItem("button.delete_receipt", DeleteReceiptPhoto));
            toolsMenu.Items.Add(new ToolStripSeparator());
            toolsMenu.Items.Add(MakeMenuItem("button.duplicates", OpenDuplicates));
            toolsMenu.Items.Add(MakeMenuItem("button.data_integrity", OpenDataIntegrity));
            toolsMenu.Items.Add(new ToolStripSeparator());
            toolsMenu.Items.Add(MakeMenuItem("title.about", ShowAboutDialog));
            Control toolsBtn = MakeDropdownButton("menu.tools", toolsMenu);
            headerActions.Add(toolsBtn);
            actionsBar.Controls.Add(toolsBtn);

            // DockStyle.Top stacks bottom-most added control at the top.
            headerPanel.Controls.Add(actionsBar);
            headerPanel.Controls.Add(headerTopRow);

            tabs.Dock = DockStyle.Fill;
            tabs.Appearance = TabAppearance.FlatButtons;
            tabs.ItemSize = new Size(0, 1);
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.TabPages.Add(WrapGrid("tab.vehicles", carsGrid));
            tabs.TabPages.Add(WrapFilteredGrid("tab.fuel", fuelGrid, BuildFuelFilterBar(), BuildFuelContextBar()));
            tabs.TabPages.Add(WrapFilteredGrid("tab.repairs", repairsGrid, BuildRepairFilterBar(), BuildRecordContextBar()));
            tabs.TabPages.Add(WrapFilteredGrid("tab.expenses", expensesGrid, BuildExpenseFilterBar(), BuildRecordContextBar()));
            tabs.TabPages.Add(WrapGrid("tab.expense_categories", expenseCategoriesGrid));
            tabs.TabPages.Add(WrapFilteredGrid("tab.utilities", utilitiesGrid, BuildUtilityFilterBar()));
            tabs.TabPages.Add(WrapGrid("tab.categories", categoriesGrid));

            summaryTab.BackColor = Color.FromArgb(244, 246, 249);
            summaryBox.Multiline = true;
            summaryBox.ReadOnly = true;
            summaryBox.ScrollBars = ScrollBars.Vertical;
            summaryBox.Dock = DockStyle.Fill;
            summaryBox.Font = new Font("Consolas", 10F);
            summaryBox.BackColor = Color.White;
            summaryBox.ForeColor = Color.FromArgb(35, 51, 70);
            summaryBox.BorderStyle = BorderStyle.None;
            summaryBox.Text = L10n.Get("message.select_folder");

            TableLayoutPanel summaryLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0),
                BackColor = Color.FromArgb(244, 246, 249)
            };
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 232));
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            intervalRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(6, 4, 6, 4),
                BackColor = Color.White
            };
            intervalRow.Paint += delegate(object s, PaintEventArgs pe)
            {
                using (Pen p = new Pen(Color.FromArgb(225, 230, 238)))
                    pe.Graphics.DrawRectangle(p, 0, 0, intervalRow.Width - 1, intervalRow.Height - 1);
            };
            intervalRow.Controls.Add(MakeButton("button.compare_periods", ShowPeriodCompare));
            intervalRow.Controls.Add(MakeButton("button.period_report", ShowPeriodReport));
            intervalRow.Controls.Add(MakeButton("button.timeline", ShowCarTimeline));
            intervalRow.Controls.Add(MakeButton("button.save_summary", SaveSummaryText));
            intervalRow.Controls.Add(MakeButton("button.chart_cost_per_km", ToggleCostPerKmChart));
            intervalRow.Controls.Add(MakeButton("button.add_reminder", AddDateReminder));
            intervalRow.Controls.Add(MakeButton("button.delete_reminder", DeleteDateReminder));
            intervalRow.Controls.Add(MakeButton("button.add_note", AddCarNote));
            intervalRow.Controls.Add(MakeButton("button.edit_note", EditCarNote));
            intervalRow.Controls.Add(MakeButton("button.delete_note", DeleteCarNote));

            summaryChart.Dock = DockStyle.Fill;
            Panel chartCard = CreateBorderedCard(new Padding(8), new Padding(0, 0, 0, 8));
            chartCard.Dock = DockStyle.Fill;
            chartCard.Controls.Add(summaryChart);

            Panel summaryTextCard = CreateBorderedCard(new Padding(10), new Padding(0));
            summaryTextCard.Dock = DockStyle.Fill;
            summaryTextCard.Controls.Add(summaryBox);

            summaryLayout.Controls.Add(intervalRow, 0, 0);
            summaryLayout.Controls.Add(chartCard, 0, 1);
            summaryLayout.Controls.Add(summaryTextCard, 0, 2);
            summaryTab.Controls.Add(summaryLayout);
            tabs.TabPages.Add(summaryTab);

            settingsTab.BackColor = Color.FromArgb(244, 246, 249);
            settingsTab.Controls.Add(BuildSettingsPage());
            tabs.TabPages.Add(settingsTab);
            tabs.SelectedIndexChanged += SummaryTabSelected;
            tabs.SelectedIndexChanged += delegate
            {
                SyncSidebarSelection();
                if (tabs.SelectedTab == settingsTab)
                    settingsTab.PerformLayout();
            };

            BuildSidebarNavItems();
            SyncSidebarSelection();

            Panel tabsHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 10, 12, 6),
                BackColor = Color.FromArgb(244, 246, 249)
            };
            tabsHost.Controls.Add(tabs);

            statusLabel.Spring = true;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            reportLinkLabel.Text = L10n.Get("button.last_report");
            reportLinkLabel.IsLink = true;
            reportLinkLabel.LinkColor = Color.FromArgb(200, 43, 43);
            reportLinkLabel.Visible = false;
            reportLinkLabel.Click += delegate { ShowLastReportDialog(); };
            statusStrip.SizingGrip = false;
            statusStrip.BackColor = Color.White;
            statusStrip.Padding = new Padding(8, 2, 12, 2);
            statusStrip.Items.Add(statusLabel);
            statusStrip.Items.Add(reportLinkLabel);
            statusStrip.Dock = DockStyle.Bottom;

            Panel workspacePanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(244, 246, 249)
            };
            workspacePanel.Controls.Add(tabsHost);
            workspacePanel.Controls.Add(headerPanel);
            workspacePanel.Controls.Add(statusStrip);

            Controls.Add(workspacePanel);
            Controls.Add(sidebarPanel);

            SetupCarsGrid();
            SetupFuelGrid();
            SetupRepairsGrid();
            SetupExpensesGrid();
            SetupExpenseCategoriesGrid();
            SetupUtilitiesGrid();
            SetupCategoriesGrid();

            WireDirty(carsGrid);
            WireDirty(fuelGrid);
            WireDirty(repairsGrid);
            WireDirty(expensesGrid);
            WireDirty(expenseCategoriesGrid);
            fuelGrid.DefaultValuesNeeded += FuelGridDefaultValuesNeeded;
            repairsGrid.DefaultValuesNeeded += EntityGridDefaultCarNeeded;
            expensesGrid.DefaultValuesNeeded += EntityGridDefaultCarNeeded;
            fuelGrid.CellEndEdit += MileageCellEndEdit;
            fuelGrid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                // ComboBox «Цель» may briefly hold a custom value not yet in Items.
                e.ThrowException = false;
            };
            repairsGrid.CellEndEdit += MileageCellEndEdit;
            WireDirty(utilitiesGrid);
            WireDirty(categoriesGrid);

            defaultCarBox.DropDownStyle = ComboBoxStyle.DropDownList;
            defaultCarBox.Width = 140;

            // При первом запуске — спросить язык, иначе применить сохранённый
            if (_firstRun)
                ShowLanguageSelectionDialog();
            else
                ApplyLanguageToUI();

            dirty = false;
            UpdateSaveDirtyVisual();
            statusLabel.Text = L10n.Get("hint.open_folder_or_zip");
            Shown += delegate
            {
                if (String.IsNullOrEmpty(repository.DirectoryPath) && carsGrid.Rows.Count <= 1 && fuelGrid.Rows.Count <= 1)
                {
                    dirty = false;
                    UpdateSaveDirtyVisual();
                    statusLabel.Text = L10n.Get("hint.open_folder_or_zip");
                }
                MaybeWarnRunningFromDownloads();
            };
        }

        private static Icon LoadAppIcon()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                string[] resNames = new[] { "KiloLedger.ico", asm.GetName().Name + ".KiloLedger.ico" };
                foreach (string resName in resNames)
                {
                    using (Stream s = asm.GetManifestResourceStream(resName))
                    {
                        if (s != null)
                            return new Icon(s);
                    }
                }
            }
            catch { }
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KiloLedger.ico");
                if (!File.Exists(iconPath))
                    iconPath = Path.Combine(Application.StartupPath, "..", "..", "KiloLedger.ico");
                if (File.Exists(iconPath))
                    return new Icon(iconPath);
            }
            catch { }
            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return null;
            }
        }

        private static string StartupLanguageFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(Path.Combine(appDataPath, "KiloLedger"), "language.txt");
        }

        private static string LoadStartupLanguage()
        {
            try
            {
                string path = StartupLanguageFilePath();
                if (!File.Exists(path))
                    return "";
                string code = (File.ReadAllText(path, Encoding.UTF8) ?? "").Trim().ToLowerInvariant();
                return code;
            }
            catch
            {
                return "";
            }
        }

        private static void SaveStartupLanguage(string lang)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(lang))
                    return;
                string path = StartupLanguageFilePath();
                string dir = Path.GetDirectoryName(path);
                if (!String.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, lang.Trim().ToLowerInvariant(), Encoding.UTF8);
            }
            catch { }
        }

        private static string DownloadsTipFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(Path.Combine(appDataPath, "KiloLedger"), "downloads_tip_seen.txt");
        }

        private static bool IsInDownloadsFolder(string folderPath)
        {
            if (String.IsNullOrWhiteSpace(folderPath))
                return false;
            try
            {
                string normalized = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!String.IsNullOrEmpty(profile))
                {
                    string userDownloads = Path.GetFullPath(Path.Combine(profile, "Downloads"))
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (String.Equals(normalized, userDownloads, StringComparison.OrdinalIgnoreCase) ||
                        normalized.StartsWith(userDownloads + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                string[] parts = normalized.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    if (String.Equals(parts[i], "Downloads", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(parts[i], "Загрузки", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private void MaybeWarnRunningFromDownloads()
        {
            try
            {
                string startupDir = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!IsInDownloadsFolder(startupDir))
                    return;
                string flagPath = DownloadsTipFilePath();
                if (File.Exists(flagPath))
                {
                    string seenPath = (File.ReadAllText(flagPath, Encoding.UTF8) ?? "").Trim();
                    if (String.Equals(seenPath, startupDir, StringComparison.OrdinalIgnoreCase))
                        return;
                }
                MessageBox.Show(this,
                    L10n.Get("msg.downloads_folder_warning"),
                    L10n.Get("title.downloads_folder_warning"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                string dir = Path.GetDirectoryName(flagPath);
                if (!String.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(flagPath, startupDir, Encoding.UTF8);
            }
            catch { }
        }

        private bool SettingsExist()
        {
            try
            {
                return File.Exists(StartupLanguageFilePath());
            }
            catch
            {
                return false;
            }
        }

        private void ShowLanguageSelectionDialog()
        {
            string[] langs = L10n.GetAvailableLanguages();
            if (langs.Length == 0)
            {
                LocalizationManager.Instance.CurrentLanguage = "ru";
                ApplyLanguageToUI();
                return;
            }

            using (var dialog = new Form())
            {
                dialog.Text = L10n.Get("dialog.language");
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.ClientSize = new Size(360, 145);
                dialog.BackColor = Color.FromArgb(244, 246, 249);
                dialog.Font = new Font("Segoe UI", 9F);

                Label promptLbl = new Label
                {
                    Text = L10n.Get("msg.choose_language"),
                    Left = 18,
                    Top = 16,
                    Width = 324,
                    ForeColor = Color.FromArgb(35, 51, 70)
                };

                var langCombo = new ComboBox()
                {
                    Left = 18,
                    Top = 42,
                    Width = 324,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                foreach (var lang in langs)
                    langCombo.Items.Add(L10n.GetLanguageDisplayName(lang) + " (" + lang + ")");
                if (langCombo.Items.Count > 0)
                    langCombo.SelectedIndex = 0;

                Button btnOk = MakeAccentButton("button.save", null);
                btnOk.Text = L10n.Get("button.save");
                btnOk.DialogResult = DialogResult.OK;
                btnOk.AutoSize = false;
                btnOk.Width = 104;
                btnOk.Height = 32;
                btnOk.Left = 140;
                btnOk.Top = 92;

                Button btnCancel = MakeButton("button.cancel", null);
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.AutoSize = false;
                btnCancel.Width = 90;
                btnCancel.Height = 32;
                btnCancel.Left = 252;
                btnCancel.Top = 92;

                dialog.AcceptButton = btnOk;
                dialog.CancelButton = btnCancel;
                dialog.Controls.AddRange(new Control[] { promptLbl, langCombo, btnOk, btnCancel });

                string selectedLang = "ru";
                if (dialog.ShowDialog() == DialogResult.OK &&
                    langCombo.SelectedIndex >= 0 &&
                    langCombo.SelectedIndex < langs.Length)
                {
                    selectedLang = langs[langCombo.SelectedIndex];
                }
                LocalizationManager.Instance.CurrentLanguage = selectedLang;
                SaveStartupLanguage(selectedLang);
                repository.Settings = ToSettings.SetLanguage(repository.Settings, selectedLang);
                if (!String.IsNullOrEmpty(repository.DirectoryPath))
                    repository.Save();
                ApplyLanguageToUI();
            }
        }

        private static DataGridView CreateGrid()
        {
            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = true,
                RowHeadersWidth = 26,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(234, 238, 244),
                EnableHeadersVisualStyles = false,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                ColumnHeadersHeight = 34,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single
            };
            grid.RowTemplate.Height = 28;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(35, 51, 70);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(241, 245, 249);
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(35, 51, 70);
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.ForeColor = Color.FromArgb(30, 41, 59);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(226, 234, 245);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(23, 37, 54);
            grid.DefaultCellStyle.Padding = new Padding(6, 2, 6, 2);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 253);
            grid.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            grid.RowHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(200, 43, 43);
            grid.RowHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            return grid;
        }

        private static TabPage WrapGrid(string locKey, DataGridView grid)
        {
            TabPage page = new TabPage(L10n.Get(locKey));
            page.Tag = locKey;
            page.BackColor = Color.FromArgb(244, 246, 249);
            Panel card = CreateBorderedCard(new Padding(1), new Padding(0));
            card.Dock = DockStyle.Fill;
            card.Controls.Add(grid);
            page.Controls.Add(card);
            return page;
        }

        private static TabPage WrapFilteredGrid(string locKey, DataGridView grid, Control filterBar, Control contextBar = null)
        {
            TabPage page = new TabPage(L10n.Get(locKey));
            page.Tag = locKey;
            page.BackColor = Color.FromArgb(244, 246, 249);
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = contextBar != null ? 3 : 2,
                Padding = new Padding(1),
                BackColor = Color.White
            };
            layout.Paint += delegate(object s, PaintEventArgs pe)
            {
                using (Pen p = new Pen(Color.FromArgb(225, 230, 238)))
                    pe.Graphics.DrawRectangle(p, 0, 0, layout.Width - 1, layout.Height - 1);
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            if (contextBar != null)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            filterBar.Dock = DockStyle.Fill;
            layout.Controls.Add(filterBar, 0, 0);
            if (contextBar != null)
            {
                contextBar.Dock = DockStyle.Fill;
                layout.Controls.Add(contextBar, 0, 1);
                grid.Dock = DockStyle.Fill;
                layout.Controls.Add(grid, 0, 2);
            }
            else
            {
                grid.Dock = DockStyle.Fill;
                layout.Controls.Add(grid, 0, 1);
            }
            page.Controls.Add(layout);
            return page;
        }

        private FlowLayoutPanel BuildFuelContextBar()
        {
            FlowLayoutPanel bar = CreateContextStrip();
            bar.Controls.Add(MakeButton("button.apply_template", ApplyTemplateToActiveGrid));
            bar.Controls.Add(MakeButton("button.save_template", SaveActiveRowAsTemplate));
            bar.Controls.Add(MakeButton("button.attach_receipt", AttachReceiptPhoto));
            bar.Controls.Add(MakeButton("button.delete_receipt", DeleteReceiptPhoto));
            bar.Controls.Add(MakeButton("button.trip", OpenTripEntry));
            bar.Controls.Add(MakeButton("button.trip_report", ShowTripReport));
            return bar;
        }

        private FlowLayoutPanel BuildRecordContextBar()
        {
            FlowLayoutPanel bar = CreateContextStrip();
            bar.Controls.Add(MakeButton("button.apply_template", ApplyTemplateToActiveGrid));
            bar.Controls.Add(MakeButton("button.save_template", SaveActiveRowAsTemplate));
            bar.Controls.Add(MakeButton("button.attach_receipt", AttachReceiptPhoto));
            bar.Controls.Add(MakeButton("button.delete_receipt", DeleteReceiptPhoto));
            return bar;
        }

        private static FlowLayoutPanel CreateContextStrip()
        {
            FlowLayoutPanel bar = new FlowLayoutPanel
            {
                WrapContents = true,
                AutoSize = true,
                Margin = new Padding(0),
                Padding = new Padding(6, 3, 6, 3),
                BackColor = Color.FromArgb(249, 250, 252)
            };
            bar.Paint += delegate(object s, PaintEventArgs pe)
            {
                using (Pen p = new Pen(Color.FromArgb(229, 234, 242)))
                {
                    pe.Graphics.DrawLine(p, 0, 0, bar.Width, 0);
                    pe.Graphics.DrawLine(p, 0, bar.Height - 1, bar.Width, bar.Height - 1);
                }
            };
            return bar;
        }

        private static string ResolveVersion()
        {
            try
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                if (version != null)
                    return version.Major + "." + version.Minor + "." + version.Build;
            }
            catch { }
            return AppVersion;
        }

        private FlowLayoutPanel BuildFuelFilterBar()
        {
            FlowLayoutPanel bar = BuildPeriodTextFilterBar(
                fuelFilterFrom, fuelFilterTo, fuelFilterText,
                delegate { ApplyFuelFilterFromUi(); },
                delegate { ResetFuelFilter(); });
            fuelFilterPurpose.DropDownStyle = ComboBoxStyle.DropDownList;
            fuelFilterPurpose.Width = 110;
            fuelFilterPurpose.Margin = new Padding(3, 5, 6, 3);
            fuelFilterPurpose.Items.Clear();
            fuelFilterPurpose.Items.Add("Все цели");
            fuelFilterPurpose.Items.Add("Личный");
            fuelFilterPurpose.Items.Add("Работа");
            fuelFilterPurpose.SelectedIndex = 0;
            bar.Controls.Add(FilterLabel("label.purpose"));
            bar.Controls.Add(fuelFilterPurpose);
            fuelFilterStation.DropDownStyle = ComboBoxStyle.DropDownList;
            fuelFilterStation.Width = 120;
            fuelFilterStation.Margin = new Padding(3, 5, 6, 3);
            fuelFilterStation.Items.Clear();
            fuelFilterStation.Items.Add("Все АЗС");
            fuelFilterStation.SelectedIndex = 0;
            bar.Controls.Add(FilterLabel("label.station"));
            bar.Controls.Add(fuelFilterStation);
            // Move apply/reset after purpose/station: rebuild order by bringing buttons to end.
            Control apply = null;
            Control reset = null;
            for (int i = bar.Controls.Count - 1; i >= 0; i--)
            {
                Button button = bar.Controls[i] as Button;
                if (button == null)
                    continue;
                if (reset == null)
                    reset = button;
                else if (apply == null)
                {
                    apply = button;
                    break;
                }
            }
            if (apply != null)
                bar.Controls.SetChildIndex(apply, bar.Controls.Count - 1);
            if (reset != null)
                bar.Controls.SetChildIndex(reset, bar.Controls.Count - 1);
            return bar;
        }

        private FlowLayoutPanel BuildRepairFilterBar()
        {
            return BuildCategoryPeriodTextFilterBar(
                repairFilterCategory, repairFilterFrom, repairFilterTo, repairFilterText,
                delegate { ApplyRepairFilterFromUi(); },
                delegate { ResetRepairFilter(); });
        }

        private FlowLayoutPanel BuildExpenseFilterBar()
        {
            return BuildCategoryPeriodTextFilterBar(
                expenseFilterCategory, expenseFilterFrom, expenseFilterTo, expenseFilterText,
                delegate { ApplyExpenseFilterFromUi(); },
                delegate { ResetExpenseFilter(); });
        }

        private FlowLayoutPanel BuildUtilityFilterBar()
        {
            return BuildPeriodTextFilterBar(
                utilityFilterFrom, utilityFilterTo, utilityFilterText,
                delegate { ApplyUtilityFilterFromUi(); },
                delegate { ResetUtilityFilter(); },
                "label.month_from", "label.month_to", "title.search");
        }

        private static FlowLayoutPanel BuildPeriodTextFilterBar(
            TextBox fromBox, TextBox toBox, TextBox textBox,
            EventHandler apply, EventHandler reset,
            string fromKey = "label.from", string toKey = "label.to", string textKey = "title.search")
        {
            FlowLayoutPanel bar = new FlowLayoutPanel
            {
                WrapContents = true,
                AutoSize = true,
                Margin = new Padding(0),
                Padding = new Padding(6, 5, 6, 5),
                BackColor = Color.White
            };
            bar.Controls.Add(FilterLabel(fromKey));
            fromBox.Width = 90;
            fromBox.Margin = new Padding(3, 5, 6, 3);
            bar.Controls.Add(fromBox);
            bar.Controls.Add(FilterLabel(toKey));
            toBox.Width = 90;
            toBox.Margin = new Padding(3, 5, 6, 3);
            bar.Controls.Add(toBox);
            bar.Controls.Add(FilterLabel(textKey));
            textBox.Width = 140;
            textBox.Margin = new Padding(3, 5, 6, 3);
            bar.Controls.Add(textBox);
            bar.Controls.Add(MakeButton("button.apply", apply));
            bar.Controls.Add(MakeButton("button.reset", reset));
            return bar;
        }

        private static FlowLayoutPanel BuildCategoryPeriodTextFilterBar(
            ComboBox categoryBox, TextBox fromBox, TextBox toBox, TextBox textBox,
            EventHandler apply, EventHandler reset)
        {
            FlowLayoutPanel bar = new FlowLayoutPanel
            {
                WrapContents = true,
                AutoSize = true,
                Margin = new Padding(0),
                Padding = new Padding(6, 5, 6, 5),
                BackColor = Color.White
            };
            categoryBox.DropDownStyle = ComboBoxStyle.DropDownList;
            categoryBox.Width = 140;
            categoryBox.Margin = new Padding(3, 5, 6, 3);
            bar.Controls.Add(FilterLabel("label.category"));
            bar.Controls.Add(categoryBox);
            bar.Controls.Add(FilterLabel("label.from"));
            fromBox.Width = 90;
            fromBox.Margin = new Padding(3, 5, 6, 3);
            bar.Controls.Add(fromBox);
            bar.Controls.Add(FilterLabel("label.to"));
            toBox.Width = 90;
            toBox.Margin = new Padding(3, 5, 6, 3);
            bar.Controls.Add(toBox);
            bar.Controls.Add(FilterLabel("title.search"));
            textBox.Width = 140;
            textBox.Margin = new Padding(3, 5, 6, 3);
            bar.Controls.Add(textBox);
            bar.Controls.Add(MakeButton("button.apply", apply));
            bar.Controls.Add(MakeButton("button.reset", reset));
            return bar;
        }

        private static Label FilterLabel(string locKey)
        {
            return new Label
            {
                Text = L10n.Get(locKey),
                AutoSize = true,
                Padding = new Padding(4, 9, 2, 0),
                ForeColor = Color.FromArgb(71, 85, 105),
                Tag = locKey
            };
        }

        private void EntityGridDefaultCarNeeded(object sender, DataGridViewRowEventArgs e)
        {
            if (e.Row.Cells.Count > 1)
            {
                string preferred = ToSettings.ResolvePreferredCar(repository.Cars, repository.Settings);
                if (!String.IsNullOrEmpty(preferred) &&
                    (e.Row.Cells[1].Value == null ||
                     String.IsNullOrEmpty(Convert.ToString(e.Row.Cells[1].Value))))
                    e.Row.Cells[1].Value = preferred;
            }
        }

        private void FuelGridDefaultValuesNeeded(object sender, DataGridViewRowEventArgs e)
        {
            if (e.Row.Cells.Count > 1)
            {
                string preferred = ToSettings.ResolvePreferredCar(repository.Cars, repository.Settings);
                if (!String.IsNullOrEmpty(preferred) &&
                    (e.Row.Cells[1].Value == null ||
                     String.IsNullOrEmpty(Convert.ToString(e.Row.Cells[1].Value))))
                    e.Row.Cells[1].Value = preferred;
            }
            if (e.Row.Cells.Count > 7)
                e.Row.Cells[7].Value = true;
            if (e.Row.Cells.Count > 8)
                e.Row.Cells[8].Value = "";
            // Liters stay empty; pre-fill last known price/l when available.
            if (e.Row.Cells.Count > 5)
            {
                string lastPrice = LastFuelPricePerLiterFromGrid();
                if (!String.IsNullOrEmpty(lastPrice) &&
                    (e.Row.Cells[5].Value == null ||
                     String.IsNullOrEmpty(Convert.ToString(e.Row.Cells[5].Value))))
                    e.Row.Cells[5].Value = lastPrice;
            }
        }

        private string LastFuelPricePerLiterFromGrid()
        {
            return LastFuelPricePerLiterForCar(null);
        }

        private string LastFuelPricePerLiterForCar(string car)
        {
            string bestDate = "";
            string bestPrice = "";
            for (int i = 0; i < fuelGrid.Rows.Count; i++)
            {
                DataGridViewRow row = fuelGrid.Rows[i];
                if (row.IsNewRow)
                    continue;
                if (!String.IsNullOrEmpty(car) &&
                    !String.Equals(CellText(row, 1), car, StringComparison.OrdinalIgnoreCase))
                    continue;
                string price = CellText(row, 5).Trim();
                if (price.Length == 0)
                    continue;
                string date = CellText(row, 0);
                if (bestPrice.Length == 0 ||
                    String.Compare(date, bestDate, StringComparison.Ordinal) >= 0)
                {
                    bestDate = date;
                    bestPrice = price;
                }
            }
            return bestPrice;
        }

        private string LastFuelKmForCar(string car)
        {
            decimal best = LastKnownMileageFromGrids(car, null, -1);
            return best < 0 ? "" : best.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private bool LastFuelFullTankForCar(string car)
        {
            string bestDate = "";
            int bestIndex = -1;
            bool full = true;
            for (int i = 0; i < fuelGrid.Rows.Count; i++)
            {
                DataGridViewRow row = fuelGrid.Rows[i];
                if (row.IsNewRow)
                    continue;
                if (!String.IsNullOrEmpty(car) &&
                    !String.Equals(CellText(row, 1), car, StringComparison.OrdinalIgnoreCase))
                    continue;
                string date = CellText(row, 0);
                if (bestIndex < 0 ||
                    String.Compare(date, bestDate, StringComparison.Ordinal) > 0 ||
                    (date == bestDate && i > bestIndex))
                {
                    bestDate = date;
                    bestIndex = i;
                    object val = row.Cells.Count > 7 ? row.Cells[7].Value : null;
                    if (val is bool)
                        full = (bool)val;
                    else
                    {
                        string raw = Convert.ToString(val) ?? "";
                        full = raw != "0" &&
                            !String.Equals(raw, "false", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            return full;
        }

        private void ShowTripReport(object sender, EventArgs e)
        {
            string car = ResolveSummaryCar();
            decimal personal = 0;
            decimal work = 0;
            int count = 0;
            for (int i = 0; i < repository.TripsRows.Count; i++)
            {
                string[] content = RecordIds.StripId(repository.TripsRows[i]);
                if (content.Length < 7 ||
                    !String.Equals(content[1] ?? "", car, StringComparison.OrdinalIgnoreCase))
                    continue;
                decimal startKm;
                decimal endKm;
                if (!KiloLedgerCodec.TryNumber(content[2], out startKm) ||
                    !KiloLedgerCodec.TryNumber(content[3], out endKm) || endKm < startKm)
                    continue;
                decimal distance = endKm - startKm;
                if (String.Equals((content[4] ?? "").Trim(), "Работа",
                    StringComparison.OrdinalIgnoreCase))
                    work += distance;
                else
                    personal += distance;
                count++;
            }
            MessageBox.Show(this,
                (L10n.Get("trips.report_title_dot") + " ") + car +
                ("\n" + L10n.Get("label.quantity_colon") + " ") + count +
                ("\n" + L10n.Get("report.personal_colon") + " ") + personal.ToString("0.##", CultureInfo.InvariantCulture) + (" " + L10n.Get("unit.km")) +
                ("\n" + L10n.Get("report.work_colon") + " ") + work.ToString("0.##", CultureInfo.InvariantCulture) + (" " + L10n.Get("unit.km")) +
                ("\n" + L10n.Get("report.total_colon") + " ") + (personal + work).ToString("0.##", CultureInfo.InvariantCulture) + (" " + L10n.Get("unit.km")),
                L10n.Get("label.trips"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OpenTripEntry(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                if (repository.Cars.Count == 0)
                {
                    MessageBox.Show(this, L10n.Get("error.no_cars"), L10n.Get("button.trip"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                List<string> cars = new List<string>();
                for (int i = 0; i < repository.Cars.Count; i++)
                {
                    string[] content = RecordIds.StripId(repository.Cars[i]);
                    if (content.Length > 0 && !String.IsNullOrEmpty(content[0]))
                        cars.Add(content[0]);
                }
                using (Form dialog = new Form())
                {
                    dialog.Text = L10n.Get("title.new_trip");
                    dialog.Width = 420;
                    dialog.Height = 390;
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dialog.MaximizeBox = false;
                    dialog.MinimizeBox = false;

                    ComboBox carBox = new ComboBox { Left = 16, Top = 82, Width = 370, DropDownStyle = ComboBoxStyle.DropDownList };
                    for (int i = 0; i < cars.Count; i++) carBox.Items.Add(cars[i]);
                    carBox.SelectedIndex = 0;
                    TextBox dateBox = new TextBox { Left = 16, Top = 36, Width = 370 };
                    dateBox.Text = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    TextBox startBox = new TextBox { Left = 16, Top = 126, Width = 180 };
                    TextBox endBox = new TextBox { Left = 206, Top = 126, Width = 180 };
                    ComboBox purposeBox = new ComboBox { Left = 16, Top = 180, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
                    purposeBox.Items.Add("Личный");
                    purposeBox.Items.Add("Работа");
                    purposeBox.SelectedIndex = 0;
                    TextBox driverBox = new TextBox { Left = 206, Top = 180, Width = 180 };
                    TextBox commentBox = new TextBox { Left = 16, Top = 234, Width = 370 };

                    Button ok = new Button { Text = L10n.Get("button.save"), Left = 206, Top = 310, Width = 90, DialogResult = DialogResult.OK };
                    Button cancel = new Button { Text = L10n.Get("button.cancel"), Left = 306, Top = 310, Width = 80, DialogResult = DialogResult.Cancel };
                    dialog.Controls.AddRange(new Control[]
                    {
                        new Label { Text = L10n.Get("label.date"), Left = 16, Top = 16, Width = 370 },
                        dateBox,
                        new Label { Text = L10n.Get("label.vehicle"), Left = 16, Top = 62, Width = 370 },
                        carBox,
                        new Label { Text = L10n.Get("label.odometer_start_km"), Left = 16, Top = 106, Width = 180 },
                        startBox,
                        new Label { Text = L10n.Get("label.odometer_end_km"), Left = 206, Top = 106, Width = 180 },
                        endBox,
                        new Label { Text = L10n.Get("label.purpose_alt"), Left = 16, Top = 160, Width = 180 },
                        purposeBox,
                        new Label { Text = L10n.Get("label.driver"), Left = 206, Top = 160, Width = 180 },
                        driverBox,
                        new Label { Text = L10n.Get("label.comment"), Left = 16, Top = 214, Width = 370 },
                        commentBox,
                        ok, cancel
                    });
                    dialog.AcceptButton = ok;
                    dialog.CancelButton = cancel;
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    decimal startKm;
                    decimal endKm;
                    DateTime parsedTripDate;
                    if (!DateTime.TryParseExact(dateBox.Text.Trim(), "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedTripDate) ||
                        carBox.SelectedItem == null ||
                        !KiloLedgerCodec.TryNumber(startBox.Text, out startKm) || startKm < 0 ||
                        !KiloLedgerCodec.TryNumber(endBox.Text, out endKm) || endKm < startKm)
                    {
                        MessageBox.Show(this,
                            L10n.Get("error.check_trip_date_odometer"),
                            L10n.Get("button.trip"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    string[] trip = new string[]
                    {
                        dateBox.Text.Trim(),
                        carBox.SelectedItem.ToString(),
                        startBox.Text.Trim(),
                        endBox.Text.Trim(),
                        purposeBox.SelectedItem != null ? purposeBox.SelectedItem.ToString() : "",
                        driverBox.Text.Trim(),
                        commentBox.Text.Trim()
                    };
                    repository.TripsRows.Add(trip);
                    repository.Save();
                    BindAll();
                    SetStatus((L10n.Get("msg.trip_saved") + " ") +
                        (endKm - startKm).ToString("0.##", CultureInfo.InvariantCulture) + (" " + L10n.Get("unit.km")));
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void OpenQuickFuelEntry(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                List<string> cars = new List<string>();
                for (int i = 0; i < repository.Cars.Count; i++)
                {
                    string[] content = RecordIds.StripId(repository.Cars[i]);
                    if (content.Length > 0 && !String.IsNullOrEmpty(content[0]))
                        cars.Add(content[0]);
                }
                if (cars.Count == 0)
                {
                    MessageBox.Show(this, L10n.Get("error.no_cars_add_tab"),
                        L10n.Get("button.quick_fuel"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                string preferred = ToSettings.ResolvePreferredCar(repository.Cars, repository.Settings);
                if (String.IsNullOrEmpty(preferred))
                    preferred = cars[0];

                using (Form dialog = new Form())
                {
                    dialog.Text = L10n.Get("button.quick_fuel");
                    dialog.Width = 420;
                    dialog.Height = 340;
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dialog.MaximizeBox = false;
                    dialog.MinimizeBox = false;
                    dialog.BackColor = Color.FromArgb(245, 247, 250);

                    Label carLabel = new Label { Text = L10n.Get("label.vehicle"), Left = 16, Top = 16, Width = 370 };
                    ComboBox carBox = new ComboBox
                    {
                        Left = 16, Top = 36, Width = 370, DropDownStyle = ComboBoxStyle.DropDownList
                    };
                    for (int i = 0; i < cars.Count; i++)
                        carBox.Items.Add(cars[i]);
                    int sel = cars.IndexOf(preferred);
                    carBox.SelectedIndex = sel >= 0 ? sel : 0;

                    Label kmLabel = new Label { Text = L10n.Get("label.odometer_km"), Left = 16, Top = 70, Width = 370 };
                    TextBox kmBox = new TextBox { Left = 16, Top = 90, Width = 370 };
                    Label litersLabel = new Label { Text = L10n.Get("label.liters"), Left = 16, Top = 124, Width = 180 };
                    TextBox litersBox = new TextBox { Left = 16, Top = 144, Width = 180 };
                    Label costLabel = new Label { Text = (L10n.Get("label.cost_comma") + " ") + Cur(), Left = 206, Top = 124, Width = 180 };
                    TextBox costBox = new TextBox { Left = 206, Top = 144, Width = 180 };
                    Label priceLabel = new Label
                    {
                        Text = (L10n.Get("label.price_per_l_comma") + " ") + Cur() + (" " + L10n.Get("label.auto_paren")),
                        Left = 16, Top = 178, Width = 370
                    };
                    TextBox priceBox = new TextBox { Left = 16, Top = 198, Width = 370 };
                    CheckBox fullTankBox = new CheckBox
                    {
                        Text = L10n.Get("label.full_tank"),
                        Left = 16, Top = 232, Width = 200,
                        Checked = true,
                        BackColor = dialog.BackColor
                    };

                    Action refreshPrefill = delegate
                    {
                        string car = carBox.SelectedItem != null ? carBox.SelectedItem.ToString() : "";
                        kmBox.Text = LastFuelKmForCar(car);
                        string lastPrice = LastFuelPricePerLiterForCar(car);
                        if (!String.IsNullOrEmpty(lastPrice))
                            priceBox.Text = lastPrice;
                        fullTankBox.Checked = LastFuelFullTankForCar(car);
                    };
                    refreshPrefill();
                    carBox.SelectedIndexChanged += delegate { refreshPrefill(); };

                    bool priceTouched = priceBox.Text.Trim().Length > 0;
                    priceBox.TextChanged += delegate
                    {
                        if (priceBox.Focused)
                            priceTouched = true;
                    };
                    EventHandler recalc = delegate
                    {
                        if (priceTouched && priceBox.Text.Trim().Length > 0)
                            return;
                        decimal litersVal;
                        decimal costVal;
                        if (!KiloLedgerCodec.TryNumber(litersBox.Text, out litersVal) || litersVal <= 0 ||
                            !KiloLedgerCodec.TryNumber(costBox.Text, out costVal) || costVal <= 0)
                            return;
                        string next = (costVal / litersVal).ToString("0.##", CultureInfo.InvariantCulture);
                        if (priceBox.Text != next)
                            priceBox.Text = next;
                    };
                    litersBox.TextChanged += recalc;
                    costBox.TextChanged += recalc;

                    Button ok = new Button
                    {
                        Text = L10n.Get("button.save"), Left = 206, Top = 268, Width = 90,
                        DialogResult = DialogResult.OK
                    };
                    Button cancel = new Button
                    {
                        Text = L10n.Get("button.cancel"), Left = 306, Top = 268, Width = 80,
                        DialogResult = DialogResult.Cancel
                    };
                    dialog.Controls.AddRange(new Control[]
                    {
                        carLabel, carBox, kmLabel, kmBox, litersLabel, litersBox,
                        costLabel, costBox, priceLabel, priceBox, fullTankBox, ok, cancel
                    });
                    dialog.AcceptButton = ok;
                    dialog.CancelButton = cancel;
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    string carName = carBox.SelectedItem != null ? carBox.SelectedItem.ToString() : "";
                    string km = kmBox.Text.Trim();
                    string liters = litersBox.Text.Trim();
                    string cost = costBox.Text.Trim();
                    string price = priceBox.Text.Trim();
                    if (carName.Length == 0 || km.Length == 0 || liters.Length == 0 || cost.Length == 0)
                    {
                        MessageBox.Show(this, L10n.Get("error.fill_car_km_liters_cost"),
                            L10n.Get("button.quick_fuel"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    decimal kmN;
                    decimal litersN;
                    decimal costN;
                    decimal priceN;
                    if (!KiloLedgerCodec.TryNumber(km, out kmN) || kmN < 0 ||
                        !KiloLedgerCodec.TryNumber(liters, out litersN) || litersN <= 0 ||
                        !KiloLedgerCodec.TryNumber(cost, out costN) || costN <= 0 ||
                        (price.Length > 0 &&
                            (!KiloLedgerCodec.TryNumber(price, out priceN) || priceN <= 0)))
                    {
                        MessageBox.Show(this,
                            L10n.Get("error.check_fuel_values"),
                            L10n.Get("button.quick_fuel"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (price.Length == 0 &&
                        KiloLedgerCodec.TryNumber(liters, out litersN) && litersN > 0 &&
                        KiloLedgerCodec.TryNumber(cost, out costN) && costN > 0)
                        price = (costN / litersN).ToString("0.##", CultureInfo.InvariantCulture);

                    decimal newKm;
                    if (KiloLedgerCodec.TryNumber(km, out newKm))
                    {
                        decimal last = LastKnownMileageFromGrids(carName, null, -1);
                        string warning = FormatMileageWarning(newKm, last);
                        if (warning != null &&
                            MessageBox.Show(this, warning, L10n.Get("label.odometer"), MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                            return;
                    }

                    string date = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    object[] values = new object[]
                    {
                        date, carName, km, liters, cost, price, "",
                        fullTankBox.Checked, "", ""
                    };
                    int index = fuelGrid.Rows.Add(values);
                    fuelGrid.ClearSelection();
                    if (index >= 0 && index < fuelGrid.Rows.Count)
                        fuelGrid.Rows[index].Selected = true;
                    if (tabs.TabPages.Count > 1)
                        tabs.SelectedIndex = 1; // Заправки typically after Автомобили
                    MarkDirty();

                    // Persist immediately when a data folder is open.
                    PullAll();
                    repository.Save();
                    dirty = false;
                    BindAll();
                    SetStatus((L10n.Get("msg.quick_fuel_saved") + " ") + carName + ", " + liters + (" " + L10n.Get("unit.liters_dot_end")));
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private static Button MakeButton(string locKey, EventHandler click)
        {
            Button button = new Button
            {
                Text = L10n.Get(locKey),
                AutoSize = true,
                Margin = new Padding(4, 4, 4, 4),
                Padding = new Padding(10, 4, 10, 4),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(35, 51, 70),
                Cursor = Cursors.Hand,
                Tag = locKey
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 232, 240);
            button.Click += click;
            return button;
        }

        private static Button MakeAccentButton(string locKey, EventHandler click)
        {
            Button button = new Button
            {
                Text = "+ " + L10n.Get(locKey),
                AutoSize = true,
                Margin = new Padding(4, 4, 4, 4),
                Padding = new Padding(12, 4, 12, 4),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(200, 43, 43),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Tag = locKey
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(170, 32, 32);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(178, 34, 34);
            button.Click += click;
            return button;
        }

        private static Button MakeDropdownButton(string locKey, ContextMenuStrip menu)
        {
            Button button = new Button
            {
                Text = L10n.Get(locKey) + " ▾",
                AutoSize = true,
                Margin = new Padding(4, 4, 4, 4),
                Padding = new Padding(10, 4, 10, 4),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(35, 51, 70),
                Cursor = Cursors.Hand,
                Tag = locKey
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
            button.Click += delegate
            {
                menu.Show(button, new Point(0, button.Height));
            };
            return button;
        }

        private ToolStripMenuItem MakeMenuItem(string locKey, EventHandler click)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(L10n.Get(locKey))
            {
                Tag = locKey
            };
            item.Click += click;
            localizableMenuItems.Add(item);
            return item;
        }

        private void BuildSidebarNavItems()
        {
            navList.Controls.Clear();
            navButtons.Clear();
            for (int i = 0; i < tabs.TabPages.Count; i++)
            {
                TabPage page = tabs.TabPages[i];
                if (i == tabs.TabPages.Count - 2)
                {
                    Panel sep = new Panel
                    {
                        Width = 180,
                        Height = 1,
                        Margin = new Padding(16, 8, 16, 8),
                        BackColor = Color.FromArgb(62, 82, 106)
                    };
                    navList.Controls.Add(sep);
                }
                int idx = i;
                string key = page.Tag as string;
                Button navBtn = new Button
                {
                    Text = page.Text,
                    Width = 212,
                    Height = 38,
                    Margin = new Padding(0, 1, 0, 1),
                    Padding = new Padding(18, 0, 12, 0),
                    TextAlign = ContentAlignment.MiddleLeft,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(35, 51, 70),
                    ForeColor = Color.FromArgb(215, 225, 236),
                    Font = new Font("Segoe UI", 9.5F),
                    Cursor = Cursors.Hand,
                    Tag = key
                };
                navBtn.FlatAppearance.BorderSize = 0;
                navBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(47, 67, 91);
                navBtn.Paint += delegate(object s, PaintEventArgs pe)
                {
                    if (tabs.SelectedIndex == idx)
                    {
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 43, 43)))
                            pe.Graphics.FillRectangle(b, 0, 4, 4, navBtn.Height - 8);
                    }
                };
                navBtn.Click += delegate
                {
                    tabs.SelectedIndex = idx;
                };
                navButtons.Add(navBtn);
                navList.Controls.Add(navBtn);
            }
        }

        private void SyncSidebarSelection()
        {
            int selected = tabs.SelectedIndex;
            for (int i = 0; i < navButtons.Count; i++)
            {
                bool active = (i == selected);
                Button btn = navButtons[i];
                btn.BackColor = active ? Color.FromArgb(47, 67, 91) : Color.FromArgb(35, 51, 70);
                btn.ForeColor = active ? Color.White : Color.FromArgb(210, 220, 232);
                btn.Font = new Font("Segoe UI", 9.5F, active ? FontStyle.Bold : FontStyle.Regular);
                btn.Invalidate();
            }
            if (selected >= 0 && selected < tabs.TabPages.Count)
                sectionTitleLabel.Text = tabs.TabPages[selected].Text;
        }

        private void UpdateSaveDirtyVisual()
        {
            if (saveButton == null)
                return;
            if (dirty)
            {
                saveButton.BackColor = Color.FromArgb(254, 242, 242);
                saveButton.ForeColor = Color.FromArgb(185, 28, 28);
                saveButton.FlatAppearance.BorderColor = Color.FromArgb(220, 38, 38);
            }
            else
            {
                saveButton.BackColor = Color.FromArgb(241, 245, 249);
                saveButton.ForeColor = Color.FromArgb(35, 51, 70);
                saveButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            }
        }

        private static Panel CreateBorderedCard(Padding padding, Padding margin)
        {
            Panel card = new Panel
            {
                BackColor = Color.White,
                Padding = padding,
                Margin = margin
            };
            card.Paint += delegate(object s, PaintEventArgs pe)
            {
                using (Pen p = new Pen(Color.FromArgb(225, 230, 238)))
                    pe.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };
            return card;
        }

        private Label MakeSettingsLabel(string locKey)
        {
            Label lbl = new Label
            {
                Text = L10n.Get(locKey),
                AutoSize = true,
                Padding = new Padding(0, 7, 4, 0),
                ForeColor = Color.FromArgb(71, 85, 105),
                Tag = locKey
            };
            settingsLocalizable.Add(lbl);
            return lbl;
        }

        private Button MakeSettingsButton(string locKey, EventHandler click)
        {
            Button btn = MakeButton(locKey, click);
            settingsLocalizable.Add(btn);
            return btn;
        }

        private Control MakeSettingsCard(string titleKey, Control content)
        {
            TableLayoutPanel card = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.White,
                Padding = new Padding(16, 12, 16, 14),
                Margin = new Padding(0, 0, 0, 12)
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 10F));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.Paint += delegate(object s, PaintEventArgs pe)
            {
                using (Pen p = new Pen(Color.FromArgb(225, 230, 238)))
                    pe.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };

            Label header = new Label
            {
                Text = L10n.Get(titleKey),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 51, 70),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 4),
                Tag = titleKey
            };
            settingsLocalizable.Add(header);

            Panel divider = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Color.FromArgb(234, 238, 244),
                Margin = new Padding(0, 4, 0, 5)
            };

            content.Dock = DockStyle.Fill;
            content.Margin = new Padding(0);
            card.Controls.Add(header, 0, 0);
            card.Controls.Add(divider, 0, 1);
            card.Controls.Add(content, 0, 2);
            return card;
        }

        private static FlowLayoutPanel CreateSettingsFlowBody()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Padding = new Padding(0, 2, 0, 0),
                Margin = new Padding(0),
                BackColor = Color.White
            };
        }

        private Control BuildSettingsPage()
        {
            Panel host = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(244, 246, 249),
                Padding = new Padding(4, 2, 12, 12)
            };

            TableLayoutPanel stack = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 6,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int r = 0; r < 6; r++)
                stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Card 1: Maintenance intervals
            FlowLayoutPanel maintBody = CreateSettingsFlowBody();
            ToIntervals defaults = new ToIntervals();
            oilIntervalBox.Width = 76;
            oilIntervalBox.Text = defaults.OilInterval.ToString(CultureInfo.InvariantCulture);
            oilWarnBox.Width = 64;
            oilWarnBox.Text = defaults.OilWarn.ToString(CultureInfo.InvariantCulture);
            beltIntervalBox.Width = 76;
            beltIntervalBox.Text = defaults.BeltInterval.ToString(CultureInfo.InvariantCulture);
            beltWarnBox.Width = 64;
            beltWarnBox.Text = defaults.BeltWarn.ToString(CultureInfo.InvariantCulture);
            maintBody.Controls.Add(MakeSettingsLabel("label.oil_interval"));
            maintBody.Controls.Add(oilIntervalBox);
            maintBody.Controls.Add(MakeSettingsLabel("label.warning"));
            maintBody.Controls.Add(oilWarnBox);
            maintBody.Controls.Add(MakeSettingsLabel("label.belt_interval"));
            maintBody.Controls.Add(beltIntervalBox);
            maintBody.Controls.Add(MakeSettingsLabel("label.warning"));
            maintBody.Controls.Add(beltWarnBox);
            maintBody.Controls.Add(MakeSettingsButton("button.save_intervals", SaveToIntervals));
            stack.Controls.Add(MakeSettingsCard("settings.card_maintenance", maintBody), 0, 0);

            // Card 2: Currency & monthly budget
            FlowLayoutPanel financeBody = CreateSettingsFlowBody();
            currencyBox.Width = 66;
            currencyBox.Text = "BYN";
            budgetBox.Width = 76;
            budgetBox.Text = "0";
            budgetWarnBox.Width = 46;
            budgetWarnBox.Text = "80";
            financeBody.Controls.Add(MakeSettingsLabel("label.currency"));
            financeBody.Controls.Add(currencyBox);
            financeBody.Controls.Add(MakeSettingsButton("button.save_currency", SaveCurrency));
            financeBody.Controls.Add(MakeSettingsLabel("label.budget_month"));
            financeBody.Controls.Add(budgetBox);
            financeBody.Controls.Add(MakeSettingsLabel("label.warn_pct_compact"));
            financeBody.Controls.Add(budgetWarnBox);
            financeBody.Controls.Add(MakeSettingsButton("button.save_budget", SaveBudget));
            stack.Controls.Add(MakeSettingsCard("settings.card_currency_budget", financeBody), 0, 1);

            // Card 3: Backups & reminders
            FlowLayoutPanel remindBody = CreateSettingsFlowBody();
            backupRemindBox.Width = 52;
            backupRemindBox.Text = "7";
            reminderWarnBox.Width = 52;
            reminderWarnBox.Text = "14";
            remindBody.Controls.Add(MakeSettingsLabel("label.backup_remind"));
            remindBody.Controls.Add(backupRemindBox);
            remindBody.Controls.Add(MakeSettingsButton("button.save_backup_remind", SaveBackupRemind));
            remindBody.Controls.Add(MakeSettingsLabel("label.reminder_days"));
            remindBody.Controls.Add(reminderWarnBox);
            remindBody.Controls.Add(MakeSettingsButton("button.save_reminder_days", SaveReminderWarnDays));
            stack.Controls.Add(MakeSettingsCard("settings.card_reminders", remindBody), 0, 2);

            // Card 4: Vehicle & language
            FlowLayoutPanel generalBody = CreateSettingsFlowBody();
            defaultCarBox.Width = 170;
            RefreshDefaultCarBox();
            languageBox.Width = 110;
            languageBox.DropDownStyle = ComboBoxStyle.DropDownList;
            languageBox.Items.Clear();
            foreach (var lang in L10n.GetAvailableLanguages())
                languageBox.Items.Add(lang);
            languageBox.Text = LocalizationManager.Instance.CurrentLanguage;
            generalBody.Controls.Add(MakeSettingsLabel("label.default_car"));
            generalBody.Controls.Add(defaultCarBox);
            generalBody.Controls.Add(MakeSettingsButton("button.save_car", SaveDefaultCar));
            generalBody.Controls.Add(MakeSettingsLabel("dialog.language"));
            generalBody.Controls.Add(languageBox);
            generalBody.Controls.Add(MakeSettingsButton("button.save_language", SaveLanguage));
            stack.Controls.Add(MakeSettingsCard("settings.card_general", generalBody), 0, 3);

            // Card 5: Google Sync & database shortcuts
            FlowLayoutPanel syncBody = CreateSettingsFlowBody();
            syncBody.Controls.Add(MakeSettingsButton("button.open_folder", OpenFolder));
            syncBody.Controls.Add(MakeSettingsButton("button.open_zip", OpenZip));
            syncBody.Controls.Add(MakeSettingsButton("button.google_wizard", OpenGoogleSyncWizard));
            syncBody.Controls.Add(MakeSettingsButton("button.google_settings", ConfigureGoogleSync));
            stack.Controls.Add(MakeSettingsCard("title.google_sync_hub", syncBody), 0, 4);

            // Card 6: About & Project support
            FlowLayoutPanel aboutBody = CreateSettingsFlowBody();
            aboutBody.Controls.Add(MakeSettingsButton("title.about", ShowAboutDialog));
            aboutBody.Controls.Add(MakeSettingsButton("button.support_project", delegate { OpenExternalUrl("https://github.com/Dolfst/alfa#support"); }));
            aboutBody.Controls.Add(MakeSettingsButton("button.support_boosty", delegate { OpenExternalUrl(BoostySupportUrl); }));
            stack.Controls.Add(MakeSettingsCard("title.about", aboutBody), 0, 5);

            host.Controls.Add(stack);
            return host;
        }

        private const string BoostySupportUrl = "https://boosty.to/kiloledger";

        private void OpenExternalUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, url + "\r\n\r\n" + ex.Message, L10n.Get("title.about"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ShowAboutDialog(object sender, EventArgs e)
        {
            using (Form dlg = new Form())
            {
                dlg.Text = L10n.Get("title.about");
                dlg.Width = 520;
                dlg.Height = 460;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = Color.FromArgb(244, 246, 249);
                dlg.Font = new Font("Segoe UI", 9F);
                if (Icon != null)
                    dlg.Icon = Icon;

                Panel banner = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 68,
                    BackColor = Color.FromArgb(35, 51, 70),
                    Padding = new Padding(16, 12, 16, 10)
                };
                PictureBox logo = new PictureBox
                {
                    Width = 40,
                    Height = 40,
                    Left = 16,
                    Top = 14,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Transparent
                };
                try
                {
                    if (Icon != null)
                    {
                        using (Icon small = new Icon(Icon, new Size(40, 40)))
                            logo.Image = small.ToBitmap();
                    }
                }
                catch { }
                Label titleLbl = new Label
                {
                    Text = "KiloLedger  v" + ResolveVersion(),
                    Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                    ForeColor = Color.White,
                    Left = 68,
                    Top = 12,
                    Width = 420,
                    Height = 26
                };
                Label platLbl = new Label
                {
                    Text = L10n.Get("about.platforms_line"),
                    Font = new Font("Segoe UI", 8.5F),
                    ForeColor = Color.FromArgb(180, 195, 214),
                    Left = 68,
                    Top = 38,
                    Width = 420,
                    Height = 20
                };
                banner.Controls.Add(titleLbl);
                banner.Controls.Add(platLbl);
                if (logo.Image != null)
                    banner.Controls.Add(logo);

                Panel body = new Panel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(18, 14, 18, 12),
                    BackColor = Color.White
                };

                Label taglineLbl = new Label
                {
                    Text = L10n.Get("about.tagline"),
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(35, 51, 70),
                    Left = 18,
                    Top = 14,
                    Width = 465,
                    Height = 36
                };

                Label siteLabel = new Label
                {
                    Text = L10n.Get("about.website_label"),
                    ForeColor = Color.FromArgb(71, 85, 105),
                    Left = 18,
                    Top = 58,
                    Width = 465,
                    Height = 20
                };

                LinkLabel repoLink = new LinkLabel
                {
                    Text = "https://github.com/Dolfst/alfa",
                    LinkColor = Color.FromArgb(200, 43, 43),
                    Left = 18,
                    Top = 80,
                    Width = 465,
                    Height = 22
                };
                repoLink.LinkClicked += delegate { OpenExternalUrl("https://github.com/Dolfst/alfa"); };

                Label supportLbl = new Label
                {
                    Text = L10n.Get("about.support_hint"),
                    ForeColor = Color.FromArgb(71, 85, 105),
                    Left = 18,
                    Top = 114,
                    Width = 465,
                    Height = 48
                };

                LinkLabel supportLink = new LinkLabel
                {
                    Text = "https://github.com/Dolfst/alfa#support",
                    LinkColor = Color.FromArgb(35, 51, 70),
                    Left = 18,
                    Top = 166,
                    Width = 465,
                    Height = 22
                };
                supportLink.LinkClicked += delegate { OpenExternalUrl("https://github.com/Dolfst/alfa#support"); };

                LinkLabel boostyLink = new LinkLabel
                {
                    Text = BoostySupportUrl,
                    LinkColor = Color.FromArgb(200, 43, 43),
                    Left = 18,
                    Top = 196,
                    Width = 465,
                    Height = 22
                };
                boostyLink.LinkClicked += delegate { OpenExternalUrl(BoostySupportUrl); };

                Button supportBtn = MakeAccentButton("button.support_boosty", delegate
                {
                    OpenExternalUrl(BoostySupportUrl);
                });
                supportBtn.AutoSize = false;
                supportBtn.Width = 220;
                supportBtn.Height = 34;
                supportBtn.Left = 18;
                supportBtn.Top = 230;

                Button closeBtn = MakeButton("button.close", delegate { dlg.Close(); });
                closeBtn.AutoSize = false;
                closeBtn.Width = 110;
                closeBtn.Height = 34;
                closeBtn.Left = 374;
                closeBtn.Top = 230;

                body.Controls.AddRange(new Control[]
                {
                    taglineLbl, siteLabel, repoLink, supportLbl, supportLink, boostyLink, supportBtn, closeBtn
                });

                dlg.Controls.Add(body);
                dlg.Controls.Add(banner);
                dlg.AcceptButton = closeBtn;
                dlg.CancelButton = closeBtn;
                dlg.ShowDialog(this);
            }
        }

        private void OpenGoogleSyncHub(object sender, EventArgs e)
        {
            GoogleSyncSettings settings = GoogleSyncSettings.Load();
            using (Form hub = new Form())
            {
                hub.Text = L10n.Get("title.google_sync_hub");
                hub.Width = 620;
                hub.Height = 290;
                hub.StartPosition = FormStartPosition.CenterParent;
                hub.FormBorderStyle = FormBorderStyle.FixedDialog;
                hub.MaximizeBox = false;
                hub.MinimizeBox = false;
                hub.BackColor = Color.FromArgb(244, 246, 249);
                hub.Font = new Font("Segoe UI", 9F);

                bool configured = settings != null && !String.IsNullOrEmpty(settings.ScriptUrl);
                Label statusBadge = new Label
                {
                    Text = configured ? L10n.Get("label.script_configured_yes") : L10n.Get("label.script_configured_no"),
                    Left = 18,
                    Top = 16,
                    Width = 568,
                    Height = 28,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 0, 10, 0),
                    BackColor = configured ? Color.FromArgb(236, 253, 245) : Color.FromArgb(254, 242, 242),
                    ForeColor = configured ? Color.FromArgb(21, 128, 61) : Color.FromArgb(185, 28, 28),
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                };

                Label hintLabel = new Label
                {
                    Text = L10n.Get("label.google_hub_hint"),
                    Left = 18,
                    Top = 52,
                    Width = 568,
                    Height = 32,
                    ForeColor = Color.FromArgb(71, 85, 105)
                };

                Label sheetLabel = new Label
                {
                    Text = L10n.Get("label.sheet_url_csv"),
                    Left = 18,
                    Top = 90,
                    Width = 568
                };

                string initialUrl = googleUrlBox.ForeColor != Color.Gray && !String.IsNullOrWhiteSpace(googleUrlBox.Text)
                    ? googleUrlBox.Text.Trim()
                    : (settings != null && settings.SpreadsheetUrl != null ? settings.SpreadsheetUrl : "");
                TextBox sheetInput = new TextBox
                {
                    Left = 18,
                    Top = 112,
                    Width = 568,
                    Text = initialUrl
                };

                Button syncNowBtn = MakeAccentButton("button.google_sync", delegate
                {
                    SaveHubUrl(sheetInput.Text);
                    hub.Close();
                    SyncGoogle(this, EventArgs.Empty);
                });
                syncNowBtn.AutoSize = false;
                syncNowBtn.Width = 175;
                syncNowBtn.Height = 34;
                syncNowBtn.Left = 18;
                syncNowBtn.Top = 158;

                Button importSheetBtn = MakeButton("button.import_google", delegate
                {
                    SaveHubUrl(sheetInput.Text);
                    hub.Close();
                    ImportGoogle(this, EventArgs.Empty);
                });
                importSheetBtn.AutoSize = false;
                importSheetBtn.Width = 150;
                importSheetBtn.Height = 34;
                importSheetBtn.Left = 202;
                importSheetBtn.Top = 158;

                Button wizardBtn = MakeButton("button.google_wizard", delegate
                {
                    SaveHubUrl(sheetInput.Text);
                    hub.Close();
                    OpenGoogleSyncWizard(this, EventArgs.Empty);
                });
                wizardBtn.AutoSize = false;
                wizardBtn.Width = 140;
                wizardBtn.Height = 34;
                wizardBtn.Left = 360;
                wizardBtn.Top = 158;

                Button settingsBtn = MakeButton("button.google_settings", delegate
                {
                    SaveHubUrl(sheetInput.Text);
                    hub.Close();
                    ConfigureGoogleSync(this, EventArgs.Empty);
                });
                settingsBtn.AutoSize = false;
                settingsBtn.Width = 160;
                settingsBtn.Height = 34;
                settingsBtn.Left = 18;
                settingsBtn.Top = 202;

                Button closeBtn = MakeButton("button.close", delegate
                {
                    SaveHubUrl(sheetInput.Text);
                    hub.Close();
                });
                closeBtn.AutoSize = false;
                closeBtn.Width = 110;
                closeBtn.Height = 34;
                closeBtn.Left = 476;
                closeBtn.Top = 202;

                hub.Controls.AddRange(new Control[]
                {
                    statusBadge, hintLabel, sheetLabel, sheetInput,
                    syncNowBtn, importSheetBtn, wizardBtn, settingsBtn, closeBtn
                });
                hub.ShowDialog(this);
            }
        }

        private void SaveHubUrl(string rawUrl)
        {
            string url = (rawUrl ?? "").Trim();
            if (url.Length > 0)
            {
                googleUrlBox.Text = url;
                googleUrlBox.ForeColor = SystemColors.WindowText;
                GoogleSyncSettings s = GoogleSyncSettings.Load();
                s.SpreadsheetUrl = url;
                s.Save();
            }
        }

        private void OpenGoogleSyncWizard(object sender, EventArgs e)
        {
            GoogleSyncSettings settings = GoogleSyncSettings.Load();
            using (GoogleSyncWizardForm wizard = new GoogleSyncWizardForm(settings))
            {
                if (wizard.ShowDialog(this) != DialogResult.OK)
                    return;
                ApplyGoogleSettingsToUi(GoogleSyncSettings.Load());
                SetStatus(L10n.Get("wizard.gs_configured"));
            }
        }

        private void ConfigureGoogleSync(object sender, EventArgs e)
        {
            GoogleSyncSettings settings = GoogleSyncSettings.Load();
            using (Form dialog = new Form())
            {
                dialog.Text = L10n.Get("title.google_sync_advanced");
                dialog.Width = 640;
                dialog.Height = 260;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.BackColor = Color.FromArgb(245, 247, 250);
                Label urlLabel = new Label { Text = L10n.Get("google.script_url_label"), Left = 12, Top = 12, Width = 600 };
                TextBox urlBox = new TextBox { Left = 12, Top = 36, Width = 600, Text = settings.ScriptUrl ?? "" };
                Label tokenLabel = new Label { Text = "SYNC_TOKEN", Left = 12, Top = 70, Width = 600 };
                TextBox tokenBox = new TextBox { Left = 12, Top = 94, Width = 600, Text = settings.Token ?? "" };
                Label sheetLabel = new Label { Text = L10n.Get("label.sheet_url_csv"), Left = 12, Top = 128, Width = 600 };
                TextBox sheetBox = new TextBox { Left = 12, Top = 152, Width = 600, Text = settings.SpreadsheetUrl ?? "" };
                Button ok = new Button { Text = L10n.Get("button.save"), Left = 430, Top = 190, Width = 90, DialogResult = DialogResult.OK };
                Button cancel = new Button { Text = L10n.Get("button.cancel"), Left = 530, Top = 190, Width = 80, DialogResult = DialogResult.Cancel };
                dialog.Controls.AddRange(new Control[] { urlLabel, urlBox, tokenLabel, tokenBox, sheetLabel, sheetBox, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                settings.ScriptUrl = urlBox.Text.Trim();
                settings.Token = tokenBox.Text.Trim();
                settings.SpreadsheetUrl = sheetBox.Text.Trim();
                settings.Save();
                ApplyGoogleSettingsToUi(settings);
                SetStatus(L10n.Get("msg.google_settings_saved"));
            }
        }

        private void ApplyGoogleSettingsToUi(GoogleSyncSettings settings)
        {
            if (settings == null)
                return;
            if (!String.IsNullOrEmpty(settings.SpreadsheetUrl))
            {
                googleUrlBox.Text = settings.SpreadsheetUrl;
                googleUrlBox.ForeColor = SystemColors.WindowText;
            }
        }

        private void SyncGoogle(object sender, EventArgs e)
        {
            try
            {
                EnsurePathForImport();
                PullAll();
                GoogleSyncSettings settings = GoogleSyncSettings.Load();
                Cursor = Cursors.WaitCursor;
                KiloLedgerRepository remote = GoogleSyncClient.PullRemote(settings);
                Cursor = Cursors.Default;

                string safetyPath = repository.AllocateSafetyBackupPath();
                using (SyncOptionsForm options = new SyncOptionsForm(repository, remote, safetyPath))
                {
                    if (options.ShowDialog(this) != DialogResult.OK)
                    {
                        SetStatus(L10n.Get("sync.cancelled"));
                        return;
                    }
                    Cursor = Cursors.WaitCursor;
                    repository.CreateSafetyBackup(safetyPath);
                    string message = GoogleSyncClient.ApplyAndPush(
                        repository, remote, settings, options.Plan);
                    BindAll();
                    repository.Save();
                    dirty = false;
                    UpdateSaveDirtyVisual();
                    SyncJournal journal = SyncMerge.LastJournal;
                    string fullReport = (journal != null ? journal.FormatSummary() : message) +
                        ("\r\n\r\n" + L10n.Get("backup.copy_colon") + " ") + safetyPath;
                    string firstLine = (message ?? "").Replace("\r\n", "\n").Split('\n')[0];
                    SetStatusWithReport(firstLine, L10n.Get("button.google_sync"), fullReport);
                    if (journal != null)
                        MessageBox.Show(this, fullReport, L10n.Get("button.google_sync"),
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void SetupCarsGrid()
        {
            carsGrid.Columns.Clear();
            carsGrid.Columns.Add("name", L10n.Get("label.vehicle"));
            carsGrid.Columns.Add("year", L10n.Get("label.year"));
            carsGrid.Columns.Add("plate", L10n.Get("label.plate"));
            carsGrid.Columns.Add("vin", "VIN");
            carsGrid.Columns.Add("tank", L10n.Get("label.tank_l"));
            carsGrid.Columns.Add("note", L10n.Get("label.note"));
        }

        private void SetupFuelGrid()
        {
            fuelGrid.Columns.Clear();
            fuelGrid.Columns.Add("date", L10n.Get("label.date"));
            fuelGrid.Columns.Add("car", L10n.Get("label.vehicle"));
            fuelGrid.Columns.Add("km", L10n.Get("label.odometer_km"));
            fuelGrid.Columns.Add("liters", L10n.Get("label.liters"));
            fuelGrid.Columns.Add("sum", (L10n.Get("label.amount_comma") + " ") + Cur());
            fuelGrid.Columns.Add("price", L10n.Get("label.price_per_l"));
            fuelGrid.Columns.Add("comment", L10n.Get("label.comment"));
            DataGridViewCheckBoxColumn fullTank = new DataGridViewCheckBoxColumn
            {
                Name = "fullTank",
                HeaderText = L10n.Get("label.full_tank"),
                TrueValue = true,
                FalseValue = false,
                ThreeState = false,
                Width = 70,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            };
            fuelGrid.Columns.Add(fullTank);
            DataGridViewComboBoxColumn purpose = new DataGridViewComboBoxColumn
            {
                Name = "purpose",
                HeaderText = L10n.Get("label.purpose"),
                FlatStyle = FlatStyle.Flat,
                Width = 90,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            };
            purpose.Items.Add("");
            purpose.Items.Add("Личный");
            purpose.Items.Add("Работа");
            fuelGrid.Columns.Add(purpose);
            fuelGrid.Columns.Add("station", L10n.Get("label.station_brand"));
            fuelGrid.EditingControlShowing -= FuelGridEditingControlShowing;
            fuelGrid.EditingControlShowing += FuelGridEditingControlShowing;
        }

        private void FuelGridEditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (fuelGrid.CurrentCell == null || fuelGrid.CurrentCell.ColumnIndex != 9)
                return;
            TextBox box = e.Control as TextBox;
            if (box == null)
                return;
            AutoCompleteStringCollection source = new AutoCompleteStringCollection();
            string[] known = KiloLedgerCodec.CollectStationSuggestions(repository.Fuel);
            for (int i = 0; i < known.Length; i++)
                source.Add(known[i]);
            for (int r = 0; r < fuelGrid.Rows.Count; r++)
            {
                if (fuelGrid.Rows[r].IsNewRow)
                    continue;
                string value = CellText(fuelGrid.Rows[r], 9);
                if (value.Length > 0 && !source.Contains(value))
                    source.Add(value);
            }
            box.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            box.AutoCompleteSource = AutoCompleteSource.CustomSource;
            box.AutoCompleteCustomSource = source;
        }

        private void SetupRepairsGrid()
        {
            repairsGrid.Columns.Clear();
            repairsGrid.Columns.Add("date", L10n.Get("label.date"));
            repairsGrid.Columns.Add("car", L10n.Get("label.vehicle"));
            repairsGrid.Columns.Add("km", L10n.Get("label.odometer_km"));
            repairsGrid.Columns.Add("cat", L10n.Get("label.category"));
            repairsGrid.Columns.Add("product", L10n.Get("label.product"));
            repairsGrid.Columns.Add("desc", L10n.Get("label.description"));
            repairsGrid.Columns.Add("qty", L10n.Get("label.qty_short"));
            repairsGrid.Columns.Add("sum", (L10n.Get("label.amount_comma") + " ") + Cur());
            repairsGrid.Columns.Add("comment", L10n.Get("label.comment"));
        }

        private void SetupUtilitiesGrid()
        {
            utilitiesGrid.Columns.Clear();
            utilitiesGrid.Columns.Add("month", L10n.Get("label.month"));
            for (int i = 0; i < KiloLedgerCodec.UtilityLabels.Length; i++)
                utilitiesGrid.Columns.Add("u" + i, KiloLedgerCodec.GetUtilityLabel(i));
        }

        private void SetupCategoriesGrid()
        {
            categoriesGrid.Columns.Clear();
            categoriesGrid.Columns.Add("name", L10n.Get("label.category"));
        }

        private void SetupExpensesGrid()
        {
            expensesGrid.Columns.Clear();
            expensesGrid.Columns.Add("date", L10n.Get("label.date"));
            expensesGrid.Columns.Add("car", L10n.Get("label.vehicle"));
            expensesGrid.Columns.Add("cat", L10n.Get("label.category"));
            expensesGrid.Columns.Add("name", L10n.Get("label.name"));
            expensesGrid.Columns.Add("sum", (L10n.Get("label.amount_comma") + " ") + Cur());
            expensesGrid.Columns.Add("comment", L10n.Get("label.comment"));
        }

        private void SetupExpenseCategoriesGrid()
        {
            expenseCategoriesGrid.Columns.Clear();
            expenseCategoriesGrid.Columns.Add("name", L10n.Get("label.category"));
        }

        private void SaveToIntervals(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                ToIntervals values = new ToIntervals();
                decimal oilInterval, oilWarn, beltInterval, beltWarn;
                if (!KiloLedgerCodec.TryNumber(oilIntervalBox.Text, out oilInterval) || oilInterval <= 0 ||
                    !KiloLedgerCodec.TryNumber(oilWarnBox.Text, out oilWarn) || oilWarn <= 0 ||
                    !KiloLedgerCodec.TryNumber(beltIntervalBox.Text, out beltInterval) || beltInterval <= 0 ||
                    !KiloLedgerCodec.TryNumber(beltWarnBox.Text, out beltWarn) || beltWarn <= 0)
                    throw new InvalidOperationException(L10n.Get("error.enter_positive_intervals"));
                values.OilInterval = oilInterval;
                values.OilWarn = oilWarn;
                values.BeltInterval = beltInterval;
                values.BeltWarn = beltWarn;
                repository.Settings = ToSettings.ApplyIntervals(repository.Settings, values);
                PullAll();
                repository.Save();
                dirty = false;
                SetStatus(L10n.Get("msg.intervals_saved"));
                RefreshSummaryText();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void SaveCurrency(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                repository.Settings = ToSettings.SetCurrency(repository.Settings, currencyBox.Text);
                PullAll();
                repository.Save();
                dirty = false;
                currencyBox.Text = Cur();
                SetupFuelGrid();
                SetupRepairsGrid();
                SetupExpensesGrid();
                SetStatus((L10n.Get("msg.currency_colon") + " ") + Cur() + (" " + L10n.Get("msg.amounts_not_recalculated")));
                RefreshSummaryText();
                BindAll();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void SaveBudget(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                decimal monthLimit, warnPct;
                if (!KiloLedgerCodec.TryNumber(budgetBox.Text, out monthLimit) || monthLimit < 0)
                    throw new InvalidOperationException(L10n.Get("error.budget_invalid"));
                if (!KiloLedgerCodec.TryNumber(budgetWarnBox.Text, out warnPct) || warnPct <= 0 || warnPct > 100)
                    throw new InvalidOperationException(L10n.Get("error.warn_pct_range"));
                repository.Settings = ToSettings.SetBudget(repository.Settings, monthLimit, warnPct);
                PullAll();
                repository.Save();
                dirty = false;
                budgetBox.Text = ToSettings.GetBudgetMonth(repository.Settings)
                    .ToString(CultureInfo.InvariantCulture);
                budgetWarnBox.Text = ToSettings.GetBudgetWarnPct(repository.Settings)
                    .ToString(CultureInfo.InvariantCulture);
                if (monthLimit <= 0)
                    SetStatus(L10n.Get("msg.budget_disabled"));
                else
                    SetStatus((L10n.Get("summary.monthly_budget_colon") + " ") + Money(monthLimit) + " " + Cur() +
                        (L10n.Get("summary.warn_from") + " ") + Money(warnPct) + "%.");
                RefreshSummaryText();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void SaveBackupRemind(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                decimal days;
                if (!KiloLedgerCodec.TryNumber(backupRemindBox.Text, out days) || days < 0 || days > 3650 ||
                    days != Decimal.Truncate(days))
                    throw new InvalidOperationException(L10n.Get("error.reminder_interval_int"));
                repository.Settings = ToSettings.SetBackupRemindDays(repository.Settings, (int)days);
                PullAll();
                repository.Save();
                dirty = false;
                backupRemindBox.Text = ToSettings.GetBackupRemindDays(repository.Settings)
                    .ToString(CultureInfo.InvariantCulture);
                if (days <= 0)
                    SetStatus(L10n.Get("msg.backup_reminder_off"));
                else
                    SetStatus((L10n.Get("msg.backup_remind_every") + " ") + ((int)days) + (" " + L10n.Get("reminder.days_no_backup")));
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void SaveReminderWarnDays(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                decimal days;
                if (!KiloLedgerCodec.TryNumber(reminderWarnBox.Text, out days) || days < 0 || days > 3650 ||
                    days != Decimal.Truncate(days))
                    throw new InvalidOperationException(L10n.Get("error.reminder_term_int"));
                repository.Settings = ToSettings.SetReminderWarnDays(repository.Settings, (int)days);
                PullAll();
                repository.Save();
                dirty = false;
                reminderWarnBox.Text = ToSettings.GetReminderWarnDays(repository.Settings)
                    .ToString(CultureInfo.InvariantCulture);
                SetStatus((L10n.Get("label.warn_before") + " ") + ((int)days) + (" " + L10n.Get("msg.days_before_reminder_saved")));
                RefreshSummaryText();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void AddDateReminder(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                PullAll();
                string car = ToSettings.ResolvePreferredCar(repository.Cars, repository.Settings);
                using (Form dialog = new Form())
                {
                    dialog.Text = L10n.Get("title.new_reminder");
                    dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.ClientSize = new Size(420, 200);
                    dialog.MaximizeBox = false;
                    dialog.MinimizeBox = false;
                    Label dateLabel = new Label { Text = L10n.Get("label.date_fmt_upper"), Left = 12, Top = 14, Width = 160 };
                    TextBox dateBox = new TextBox
                    {
                        Left = 180, Top = 12, Width = 220,
                        Text = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    };
                    Label carLabel = new Label { Text = L10n.Get("label.vehicle"), Left = 12, Top = 44, Width = 160 };
                    TextBox carBox = new TextBox { Left = 180, Top = 42, Width = 220, Text = car };
                    Label titleLabel = new Label { Text = L10n.Get("label.name"), Left = 12, Top = 74, Width = 160 };
                    TextBox titleBox = new TextBox { Left = 180, Top = 72, Width = 220 };
                    Label typeLabel = new Label { Text = L10n.Get("label.type"), Left = 12, Top = 104, Width = 160 };
                    ComboBox typeBox = new ComboBox
                    {
                        Left = 180, Top = 102, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList
                    };
                    typeBox.Items.AddRange(new object[] { L10n.Get("reminder.insurance"), L10n.Get("reminder.inspection"), L10n.Get("reminder.custom") });
                    typeBox.SelectedIndex = 0;
                    Button ok = new Button { Text = L10n.Get("button.save"), DialogResult = DialogResult.OK, Left = 220, Top = 150, Width = 90 };
                    Button cancel = new Button { Text = L10n.Get("button.cancel"), DialogResult = DialogResult.Cancel, Left = 320, Top = 150, Width = 80 };
                    dialog.Controls.Add(dateLabel);
                    dialog.Controls.Add(dateBox);
                    dialog.Controls.Add(carLabel);
                    dialog.Controls.Add(carBox);
                    dialog.Controls.Add(titleLabel);
                    dialog.Controls.Add(titleBox);
                    dialog.Controls.Add(typeLabel);
                    dialog.Controls.Add(typeBox);
                    dialog.Controls.Add(ok);
                    dialog.Controls.Add(cancel);
                    dialog.AcceptButton = ok;
                    dialog.CancelButton = cancel;
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    DateReminder item = new DateReminder();
                    item.Date = (dateBox.Text ?? "").Trim();
                    item.Car = (carBox.Text ?? "").Trim();
                    item.Title = (titleBox.Text ?? "").Trim();
                    item.Type = DateReminders.NormalizeType(Convert.ToString(typeBox.SelectedItem));
                    if (repository.RemindersRows == null)
                        repository.RemindersRows = new List<string[]>();
                    DateReminders.Upsert(repository.RemindersRows, repository.Deleted, item);
                    repository.Save();
                    dirty = false;
                    SetStatus(L10n.Get("msg.reminder_saved"));
                    RefreshSummaryText();
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void DeleteDateReminder(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                PullAll();
                List<DateReminder> list = DateReminders.FromRows(repository.RemindersRows);
                if (list.Count == 0)
                {
                    MessageBox.Show(this, L10n.Get("msg.no_reminders"), L10n.Get("label.reminders"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                using (Form dialog = new Form())
                {
                    dialog.Text = L10n.Get("button.delete_reminder");
                    dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.ClientSize = new Size(520, 140);
                    dialog.MaximizeBox = false;
                    dialog.MinimizeBox = false;
                    ComboBox pick = new ComboBox
                    {
                        Left = 12, Top = 16, Width = 490, DropDownStyle = ComboBoxStyle.DropDownList
                    };
                    for (int i = 0; i < list.Count; i++)
                        pick.Items.Add(DateReminders.FormatLine(list[i], DateTime.Today));
                    pick.SelectedIndex = 0;
                    Button ok = new Button { Text = L10n.Get("button.delete"), DialogResult = DialogResult.OK, Left = 320, Top = 70, Width = 90 };
                    Button cancel = new Button { Text = L10n.Get("button.cancel"), DialogResult = DialogResult.Cancel, Left = 420, Top = 70, Width = 80 };
                    dialog.Controls.Add(pick);
                    dialog.Controls.Add(ok);
                    dialog.Controls.Add(cancel);
                    dialog.AcceptButton = ok;
                    dialog.CancelButton = cancel;
                    if (dialog.ShowDialog(this) != DialogResult.OK || pick.SelectedIndex < 0)
                        return;
                    DateReminder item = list[pick.SelectedIndex];
                    DateReminders.DeleteById(repository.RemindersRows, repository.Deleted, item.Id);
                    repository.Save();
                    dirty = false;
                    SetStatus(L10n.Get("msg.reminder_deleted"));
                    RefreshSummaryText();
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private string ResolveSummaryCar()
        {
            return ToSettings.ResolvePreferredCar(repository.Cars, repository.Settings);
        }

        private void AddCarNote(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                PullAll();
                string car = ResolveSummaryCar();
                using (Form dialog = new Form())
                {
                    dialog.Text = L10n.Get("title.new_note");
                    dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.ClientSize = new Size(460, 220);
                    dialog.MaximizeBox = false;
                    dialog.MinimizeBox = false;
                    Label dateLabel = new Label { Text = L10n.Get("label.date_fmt_upper"), Left = 12, Top = 14, Width = 160 };
                    TextBox dateBox = new TextBox
                    {
                        Left = 180, Top = 12, Width = 260,
                        Text = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    };
                    Label carLabel = new Label { Text = L10n.Get("label.vehicle"), Left = 12, Top = 44, Width = 160 };
                    TextBox carBox = new TextBox { Left = 180, Top = 42, Width = 260, Text = car };
                    Label textLabel = new Label { Text = L10n.Get("label.text"), Left = 12, Top = 74, Width = 160 };
                    TextBox textBox = new TextBox
                    {
                        Left = 180, Top = 72, Width = 260, Height = 80, Multiline = true,
                        ScrollBars = ScrollBars.Vertical
                    };
                    Button ok = new Button { Text = L10n.Get("button.save"), DialogResult = DialogResult.OK, Left = 260, Top = 170, Width = 90 };
                    Button cancel = new Button { Text = L10n.Get("button.cancel"), DialogResult = DialogResult.Cancel, Left = 360, Top = 170, Width = 80 };
                    dialog.Controls.Add(dateLabel);
                    dialog.Controls.Add(dateBox);
                    dialog.Controls.Add(carLabel);
                    dialog.Controls.Add(carBox);
                    dialog.Controls.Add(textLabel);
                    dialog.Controls.Add(textBox);
                    dialog.Controls.Add(ok);
                    dialog.Controls.Add(cancel);
                    dialog.AcceptButton = ok;
                    dialog.CancelButton = cancel;
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    CarNote item = new CarNote();
                    item.Date = (dateBox.Text ?? "").Trim();
                    item.Car = (carBox.Text ?? "").Trim();
                    item.Text = (textBox.Text ?? "").Trim();
                    if (repository.NotesRows == null)
                        repository.NotesRows = new List<string[]>();
                    CarNotes.Upsert(repository.NotesRows, repository.Deleted, item);
                    repository.Save();
                    dirty = false;
                    SetStatus(L10n.Get("msg.note_saved"));
                    RefreshSummaryText();
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void EditCarNote(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                PullAll();
                string car = ResolveSummaryCar();
                List<CarNote> list = CarNotes.ListForCar(repository.NotesRows, car);
                if (list.Count == 0)
                {
                    MessageBox.Show(this, L10n.Get("msg.no_notes_for_car"), L10n.Get("label.notes"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                using (Form pickDialog = new Form())
                {
                    pickDialog.Text = L10n.Get("button.edit_note");
                    pickDialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    pickDialog.StartPosition = FormStartPosition.CenterParent;
                    pickDialog.ClientSize = new Size(520, 140);
                    pickDialog.MaximizeBox = false;
                    pickDialog.MinimizeBox = false;
                    ComboBox pick = new ComboBox
                    {
                        Left = 12, Top = 16, Width = 490, DropDownStyle = ComboBoxStyle.DropDownList
                    };
                    for (int i = 0; i < list.Count; i++)
                        pick.Items.Add(CarNotes.FormatLine(list[i]));
                    pick.SelectedIndex = 0;
                    Button ok = new Button { Text = L10n.Get("button.edit"), DialogResult = DialogResult.OK, Left = 320, Top = 70, Width = 90 };
                    Button cancel = new Button { Text = L10n.Get("button.cancel"), DialogResult = DialogResult.Cancel, Left = 420, Top = 70, Width = 80 };
                    pickDialog.Controls.Add(pick);
                    pickDialog.Controls.Add(ok);
                    pickDialog.Controls.Add(cancel);
                    pickDialog.AcceptButton = ok;
                    pickDialog.CancelButton = cancel;
                    if (pickDialog.ShowDialog(this) != DialogResult.OK || pick.SelectedIndex < 0)
                        return;
                    CarNote item = list[pick.SelectedIndex];
                    using (Form dialog = new Form())
                    {
                        dialog.Text = L10n.Get("button.edit_note");
                        dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                        dialog.StartPosition = FormStartPosition.CenterParent;
                        dialog.ClientSize = new Size(460, 220);
                        dialog.MaximizeBox = false;
                        dialog.MinimizeBox = false;
                        Label dateLabel = new Label { Text = L10n.Get("label.date_fmt_upper"), Left = 12, Top = 14, Width = 160 };
                        TextBox dateBox = new TextBox { Left = 180, Top = 12, Width = 260, Text = item.Date };
                        Label carLabel = new Label { Text = L10n.Get("label.vehicle"), Left = 12, Top = 44, Width = 160 };
                        TextBox carBox = new TextBox { Left = 180, Top = 42, Width = 260, Text = item.Car };
                        Label textLabel = new Label { Text = L10n.Get("label.text"), Left = 12, Top = 74, Width = 160 };
                        TextBox textBox = new TextBox
                        {
                            Left = 180, Top = 72, Width = 260, Height = 80, Multiline = true,
                            ScrollBars = ScrollBars.Vertical, Text = item.Text
                        };
                        Button save = new Button { Text = L10n.Get("button.save"), DialogResult = DialogResult.OK, Left = 260, Top = 170, Width = 90 };
                        Button cancelEdit = new Button { Text = L10n.Get("button.cancel"), DialogResult = DialogResult.Cancel, Left = 360, Top = 170, Width = 80 };
                        dialog.Controls.Add(dateLabel);
                        dialog.Controls.Add(dateBox);
                        dialog.Controls.Add(carLabel);
                        dialog.Controls.Add(carBox);
                        dialog.Controls.Add(textLabel);
                        dialog.Controls.Add(textBox);
                        dialog.Controls.Add(save);
                        dialog.Controls.Add(cancelEdit);
                        dialog.AcceptButton = save;
                        dialog.CancelButton = cancelEdit;
                        if (dialog.ShowDialog(this) != DialogResult.OK)
                            return;
                        item.Date = (dateBox.Text ?? "").Trim();
                        item.Car = (carBox.Text ?? "").Trim();
                        item.Text = (textBox.Text ?? "").Trim();
                        if (repository.NotesRows == null)
                            repository.NotesRows = new List<string[]>();
                        CarNotes.Upsert(repository.NotesRows, repository.Deleted, item);
                        repository.Save();
                        dirty = false;
                        SetStatus(L10n.Get("msg.note_saved"));
                        RefreshSummaryText();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void DeleteCarNote(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                PullAll();
                string car = ResolveSummaryCar();
                List<CarNote> list = CarNotes.ListForCar(repository.NotesRows, car);
                if (list.Count == 0)
                {
                    MessageBox.Show(this, L10n.Get("msg.no_notes_for_car"), L10n.Get("label.notes"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                using (Form dialog = new Form())
                {
                    dialog.Text = L10n.Get("button.delete_note");
                    dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.ClientSize = new Size(520, 140);
                    dialog.MaximizeBox = false;
                    dialog.MinimizeBox = false;
                    ComboBox pick = new ComboBox
                    {
                        Left = 12, Top = 16, Width = 490, DropDownStyle = ComboBoxStyle.DropDownList
                    };
                    for (int i = 0; i < list.Count; i++)
                        pick.Items.Add(CarNotes.FormatLine(list[i]));
                    pick.SelectedIndex = 0;
                    Button ok = new Button { Text = L10n.Get("button.delete"), DialogResult = DialogResult.OK, Left = 320, Top = 70, Width = 90 };
                    Button cancel = new Button { Text = L10n.Get("button.cancel"), DialogResult = DialogResult.Cancel, Left = 420, Top = 70, Width = 80 };
                    dialog.Controls.Add(pick);
                    dialog.Controls.Add(ok);
                    dialog.Controls.Add(cancel);
                    dialog.AcceptButton = ok;
                    dialog.CancelButton = cancel;
                    if (dialog.ShowDialog(this) != DialogResult.OK || pick.SelectedIndex < 0)
                        return;
                    CarNote item = list[pick.SelectedIndex];
                    CarNotes.DeleteById(repository.NotesRows, repository.Deleted, item.Id);
                    repository.Save();
                    dirty = false;
                    SetStatus(L10n.Get("msg.note_deleted"));
                    RefreshSummaryText();
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void RefreshDefaultCarBox()
        {
            defaultCarBox.Items.Clear();
            defaultCarBox.Items.Add(L10n.Get("common.not_set"));
            string preferred = ToSettings.GetDefaultCar(repository.Settings);
            int selected = 0;
            for (int i = 0; i < repository.Cars.Count; i++)
            {
                string[] content = RecordIds.StripId(repository.Cars[i]);
                string name = content.Length > 0 ? (content[0] ?? "").Trim() : "";
                if (name.Length == 0)
                    continue;
                int index = defaultCarBox.Items.Add(name);
                if (String.Equals(name, preferred, StringComparison.OrdinalIgnoreCase))
                    selected = index;
            }
            if (defaultCarBox.Items.Count > 0)
                defaultCarBox.SelectedIndex = selected;
        }

        private void SaveDefaultCar(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                string name = "";
                if (defaultCarBox.SelectedIndex > 0)
                    name = Convert.ToString(defaultCarBox.SelectedItem) ?? "";
                repository.Settings = ToSettings.SetDefaultCar(repository.Settings, name);
                MarkDirty();
                repository.Save();
                dirty = false;
                SetStatus(name.Length == 0
                    ? L10n.Get("msg.default_car_cleared")
                    : (L10n.Get("msg.default_car_set") + " ") + name);
                RefreshSummaryText();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, L10n.Get("label.default_car_full"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SaveLanguage(object sender, EventArgs e)
        {
            try
            {
                string lang = languageBox.Text ?? "ru";
                LocalizationManager.SetLanguage(lang);
                SaveStartupLanguage(lang);
                if (!String.IsNullOrEmpty(repository.DirectoryPath))
                {
                    repository.Settings = ToSettings.SetLanguage(repository.Settings, lang);
                    repository.Save();
                    dirty = false;
                    UpdateSaveDirtyVisual();
                }
                ApplyLanguageToUI();
                SetStatus(L10n.Get("label.language_colon") + " " + L10n.GetLanguageDisplayName(lang));
                RefreshSummaryText();
                MessageBox.Show(this, L10n.Get("msg.language_changed_to") + " " + L10n.GetLanguageDisplayName(lang), L10n.Get("label.language"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, L10n.Get("label.language"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Применить текущий язык ко всем элементам интерфейса (кнопки, вкладки, метки, заголовки таблиц).
        /// Вызывается при старте и при смене языка — без перезапуска приложения.
        /// </summary>
        private void ApplyLanguageToUI()
        {
            Text = "KiloLedger " + ResolveVersion();
            statusLabel.Text = L10n.Get("hint.open_folder_or_zip");
            reportLinkLabel.Text = L10n.Get("button.last_report");
            if (String.IsNullOrEmpty(repository.DirectoryPath))
                pathBox.Text = L10n.Get("hint.open_folder_or_zip");
            if (globalSearchBox.ForeColor == Color.Gray)
                globalSearchBox.Text = L10n.Get("button.search_ellipsis");
            summaryTab.Text = L10n.Get("tab.summary");
            settingsTab.Text = L10n.Get("tab.settings");

            foreach (Control ctrl in headerActions)
            {
                if (ctrl is Button btn && btn.Tag is string key)
                {
                    if (btn == quickFuelButton)
                        btn.Text = "+ " + L10n.Get(key);
                    else if (key.StartsWith("menu.", StringComparison.Ordinal))
                        btn.Text = L10n.Get(key) + " ▾";
                    else
                        btn.Text = L10n.Get(key);
                }
            }

            foreach (ToolStripMenuItem item in localizableMenuItems)
            {
                if (item.Tag is string mKey)
                    item.Text = L10n.Get(mKey);
            }

            foreach (Button navBtn in navButtons)
            {
                if (navBtn.Tag is string nKey)
                    navBtn.Text = L10n.Get(nKey);
            }

            foreach (Control ctrl in intervalRow.Controls)
            {
                if (ctrl is Button btn3 && btn3.Tag is string key3)
                    btn3.Text = L10n.Get(key3);
            }

            foreach (Control ctrl in settingsLocalizable)
            {
                if (ctrl.Tag is string sKey)
                    ctrl.Text = L10n.Get(sKey);
            }

            UpdateFilterBarButtons(fuelGrid);
            UpdateFilterBarButtons(repairsGrid);
            UpdateFilterBarButtons(expensesGrid);
            UpdateFilterBarButtons(utilitiesGrid);
            UpdateGridHeadersLanguage();

            foreach (TabPage page in tabs.TabPages)
            {
                if (page.Tag is string tabKey)
                    page.Text = L10n.Get(tabKey);
            }

            SyncSidebarSelection();
            summaryChart.Invalidate();
            languageBox.Text = LocalizationManager.Instance.CurrentLanguage;
        }

        private void UpdateGridHeadersLanguage()
        {
            if (carsGrid.Columns.Count >= 6)
            {
                carsGrid.Columns[0].HeaderText = L10n.Get("label.vehicle");
                carsGrid.Columns[1].HeaderText = L10n.Get("label.year");
                carsGrid.Columns[2].HeaderText = L10n.Get("label.plate");
                carsGrid.Columns[3].HeaderText = "VIN";
                carsGrid.Columns[4].HeaderText = L10n.Get("label.tank_l");
                carsGrid.Columns[5].HeaderText = L10n.Get("label.note");
            }
            if (fuelGrid.Columns.Count >= 10)
            {
                fuelGrid.Columns[0].HeaderText = L10n.Get("label.date");
                fuelGrid.Columns[1].HeaderText = L10n.Get("label.vehicle");
                fuelGrid.Columns[2].HeaderText = L10n.Get("label.odometer_km");
                fuelGrid.Columns[3].HeaderText = L10n.Get("label.liters");
                fuelGrid.Columns[4].HeaderText = (L10n.Get("label.amount_comma") + " ") + Cur();
                fuelGrid.Columns[5].HeaderText = L10n.Get("label.price_per_l");
                fuelGrid.Columns[6].HeaderText = L10n.Get("label.comment");
                fuelGrid.Columns[7].HeaderText = L10n.Get("label.full_tank");
                fuelGrid.Columns[8].HeaderText = L10n.Get("label.purpose");
                fuelGrid.Columns[9].HeaderText = L10n.Get("label.station_brand");
            }
            if (repairsGrid.Columns.Count >= 9)
            {
                repairsGrid.Columns[0].HeaderText = L10n.Get("label.date");
                repairsGrid.Columns[1].HeaderText = L10n.Get("label.vehicle");
                repairsGrid.Columns[2].HeaderText = L10n.Get("label.odometer_km");
                repairsGrid.Columns[3].HeaderText = L10n.Get("label.category");
                repairsGrid.Columns[4].HeaderText = L10n.Get("label.product");
                repairsGrid.Columns[5].HeaderText = L10n.Get("label.description");
                repairsGrid.Columns[6].HeaderText = L10n.Get("label.qty_short");
                repairsGrid.Columns[7].HeaderText = (L10n.Get("label.amount_comma") + " ") + Cur();
                repairsGrid.Columns[8].HeaderText = L10n.Get("label.comment");
            }
            if (expensesGrid.Columns.Count >= 6)
            {
                expensesGrid.Columns[0].HeaderText = L10n.Get("label.date");
                expensesGrid.Columns[1].HeaderText = L10n.Get("label.vehicle");
                expensesGrid.Columns[2].HeaderText = L10n.Get("label.category");
                expensesGrid.Columns[3].HeaderText = L10n.Get("label.name");
                expensesGrid.Columns[4].HeaderText = (L10n.Get("label.amount_comma") + " ") + Cur();
                expensesGrid.Columns[5].HeaderText = L10n.Get("label.comment");
            }
            if (utilitiesGrid.Columns.Count >= 1 + KiloLedgerCodec.UtilityLabels.Length)
            {
                utilitiesGrid.Columns[0].HeaderText = L10n.Get("label.month");
                for (int i = 0; i < KiloLedgerCodec.UtilityLabels.Length; i++)
                    utilitiesGrid.Columns[i + 1].HeaderText = KiloLedgerCodec.GetUtilityLabel(i);
            }
            if (categoriesGrid.Columns.Count >= 1)
                categoriesGrid.Columns[0].HeaderText = L10n.Get("label.category");
            if (expenseCategoriesGrid.Columns.Count >= 1)
                expenseCategoriesGrid.Columns[0].HeaderText = L10n.Get("label.category");
        }

        /// <summary>
        /// Обновить кнопки и подписи в панели фильтрации и контекстной панели.
        /// </summary>
        private void UpdateFilterBarButtons(DataGridView grid)
        {
            if (grid == null || grid.Parent == null)
                return;
            foreach (Control ctrl in grid.Parent.Controls)
            {
                if (ctrl is FlowLayoutPanel bar)
                {
                    foreach (Control c in bar.Controls)
                    {
                        if (c is Button b && b.Tag is string key)
                            b.Text = L10n.Get(key);
                        else if (c is Label l && l.Tag is string lKey)
                            l.Text = L10n.Get(lKey);
                    }
                }
            }
        }

        /// <summary>
        /// Показать диалог выбора языка при первом запуске, если язык ещё не сохранён.
        /// </summary>
        private void ShowFirstRunLanguageDialog()
        {
            string[] langs = L10n.GetAvailableLanguages();
            using (Form dialog = new Form())
            {
                dialog.Text = L10n.Get("dialog.language");
                dialog.Width = 420;
                dialog.Height = 240;
                dialog.StartPosition = FormStartPosition.CenterScreen;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.BackColor = Color.FromArgb(245, 247, 250);

                var label = new Label
                {
                    Text = L10n.Get("msg.choose_language"),
                    Left = 16, Top = 20, Width = 380,
                    AutoSize = false
                };

                var combo = new ComboBox
                {
                    Left = 16, Top = 50, Width = 380,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                foreach (var lang in langs)
                    combo.Items.Add(L10n.GetLanguageDisplayName(lang) + " (" + lang + ")");
                combo.SelectedIndex = 0;

                var btnOk = new Button
                {
                    Text = L10n.Get("button.save"),
                    Left = 160, Top = 100, Width = 100,
                    DialogResult = DialogResult.OK
                };
                var btnCancel = new Button
                {
                    Text = L10n.Get("button.cancel"),
                    Left = 270, Top = 100, Width = 100,
                    DialogResult = DialogResult.Cancel
                };

                dialog.Controls.AddRange(new Control[] { label, combo, btnOk, btnCancel });
                dialog.AcceptButton = btnOk;
                dialog.CancelButton = btnCancel;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    string selected = langs[combo.SelectedIndex];
                    LocalizationManager.Instance.CurrentLanguage = selected;
                    repository.Settings = ToSettings.SetLanguage(repository.Settings, selected);
                    repository.Save();
                    languageBox.Items.Clear();
                    foreach (var lang in langs)
                        languageBox.Items.Add(lang);
                    languageBox.Text = selected;
                    ApplyLanguageToUI();
                }
            }
        }

        private string ActiveTemplateType()
        {
            if (tabs.SelectedTab == null)
                return null;
            string title = tabs.SelectedTab.Text ?? "";
            if (title.StartsWith(L10n.Get("tab.fuel")))
                return RecordTemplates.TypeFuel;
            if (title.StartsWith(L10n.Get("tab.repairs")))
                return RecordTemplates.TypeRepair;
            if (title.StartsWith(L10n.Get("tab.expenses")))
                return RecordTemplates.TypeExpense;
            return null;
        }

        private DataGridView ActiveEntityGrid()
        {
            string type = ActiveTemplateType();
            if (type == RecordTemplates.TypeFuel) return fuelGrid;
            if (type == RecordTemplates.TypeRepair) return repairsGrid;
            if (type == RecordTemplates.TypeExpense) return expensesGrid;
            return null;
        }

        private void ApplyTemplateToActiveGrid(object sender, EventArgs e)
        {
            try
            {
                string type = ActiveTemplateType();
                DataGridView grid = ActiveEntityGrid();
                if (type == null || grid == null)
                {
                    MessageBox.Show(this,
                        L10n.Get("error.open_tab_fuel_repairs_expenses"),
                        L10n.Get("button.apply_template"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (repository.TemplatesRows == null)
                    repository.TemplatesRows = new List<string[]>();
                List<RecordTemplate> list = RecordTemplates.ListByType(repository.TemplatesRows, type);
                if (list.Count == 0)
                {
                    MessageBox.Show(this, L10n.Get("msg.no_templates_of_type"),
                        L10n.Get("button.apply_template"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                string[] names = new string[list.Count];
                for (int i = 0; i < list.Count; i++)
                    names[i] = list[i].Name;
                string chosen = PickFromList(L10n.Get("button.apply_template"), names);
                if (chosen == null)
                    return;
                RecordTemplate tpl = null;
                for (int i = 0; i < list.Count; i++)
                    if (String.Equals(list[i].Name, chosen, StringComparison.OrdinalIgnoreCase))
                        tpl = list[i];
                if (tpl == null)
                    return;
                string date = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                string car = ToSettings.ResolvePreferredCar(repository.Cars, repository.Settings);
                string[] entity = RecordTemplates.BuildEntityRow(tpl, date, car);
                int cols = type == RecordTemplates.TypeFuel ? 9
                    : type == RecordTemplates.TypeRepair ? 9 : 6;
                object[] values = new object[cols];
                for (int c = 0; c < cols; c++)
                    values[c] = c < entity.Length ? entity[c] : "";
                if (type == RecordTemplates.TypeFuel && cols > 7 &&
                    (values[7] == null || Convert.ToString(values[7]).Length == 0))
                    values[7] = true;
                int index = grid.Rows.Add(values);
                grid.ClearSelection();
                if (index >= 0 && index < grid.Rows.Count)
                    grid.Rows[index].Selected = true;
                MarkDirty();
                SetStatus(L10n.Get("msg.row_from_template_open") + chosen + L10n.Get("msg.added_save_dat_suffix"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, L10n.Get("button.apply_template"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SaveActiveRowAsTemplate(object sender, EventArgs e)
        {
            try
            {
                string type = ActiveTemplateType();
                DataGridView grid = ActiveEntityGrid();
                if (type == null || grid == null)
                {
                    MessageBox.Show(this,
                        L10n.Get("error.open_tab_select_row"),
                        L10n.Get("label.template"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (grid.CurrentRow == null || grid.CurrentRow.IsNewRow)
                {
                    MessageBox.Show(this, L10n.Get("error.select_filled_row"),
                        L10n.Get("label.template"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                DataGridViewRow row = grid.CurrentRow;
                int cols = type == RecordTemplates.TypeFuel ? 9
                    : type == RecordTemplates.TypeRepair ? 9 : 6;
                string[] entity = new string[cols];
                for (int c = 0; c < cols; c++)
                {
                    object val = c < row.Cells.Count ? row.Cells[c].Value : null;
                    if (type == RecordTemplates.TypeFuel && c == 7)
                    {
                        bool full = true;
                        if (val is bool)
                            full = (bool)val;
                        else
                        {
                            string raw = Convert.ToString(val) ?? "";
                            full = raw != "0" && !String.Equals(raw, "false", StringComparison.OrdinalIgnoreCase);
                        }
                        entity[c] = full ? "1" : "0";
                    }
                    else
                        entity[c] = Convert.ToString(val) ?? "";
                }
                string name = PromptText(L10n.Get("button.save_template"), L10n.Get("label.template_name_colon"),
                    RecordTemplates.TypeLabelRu(type));
                if (name == null)
                    return;
                RecordTemplate item = new RecordTemplate();
                item.Type = type;
                item.Name = name;
                item.Payload = RecordTemplates.PayloadFromEntity(type, entity);
                if (repository.TemplatesRows == null)
                    repository.TemplatesRows = new List<string[]>();
                repository.TemplatesRows = RecordTemplates.Upsert(repository.TemplatesRows, item);
                MarkDirty();
                repository.Save();
                dirty = false;
                SetStatus((L10n.Get("msg.template_saved_colon") + " ") + name);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, L10n.Get("label.template"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string PickFromList(string title, string[] items)
        {
            using (Form dialog = new Form())
            {
                dialog.Text = title;
                dialog.Width = 360;
                dialog.Height = 420;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                ListBox list = new ListBox { Dock = DockStyle.Fill };
                list.Items.AddRange(items);
                if (items.Length > 0)
                    list.SelectedIndex = 0;
                FlowLayoutPanel buttons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    FlowDirection = FlowDirection.RightToLeft,
                    Height = 40
                };
                Button ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
                Button cancel = new Button { Text = L10n.Get("button.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
                buttons.Controls.Add(ok);
                buttons.Controls.Add(cancel);
                dialog.Controls.Add(list);
                dialog.Controls.Add(buttons);
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                if (dialog.ShowDialog() != DialogResult.OK || list.SelectedItem == null)
                    return null;
                return Convert.ToString(list.SelectedItem);
            }
        }

        private static string PromptText(string title, string label, string initial)
        {
            using (Form dialog = new Form())
            {
                dialog.Text = title;
                dialog.Width = 360;
                dialog.Height = 160;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                Label caption = new Label
                {
                    Text = label,
                    Dock = DockStyle.Top,
                    Height = 24,
                    Padding = new Padding(8, 8, 8, 0)
                };
                TextBox box = new TextBox
                {
                    Dock = DockStyle.Top,
                    Height = 28,
                    Text = initial ?? ""
                };
                FlowLayoutPanel buttons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    FlowDirection = FlowDirection.RightToLeft,
                    Height = 40
                };
                Button ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
                Button cancel = new Button { Text = L10n.Get("button.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
                buttons.Controls.Add(ok);
                buttons.Controls.Add(cancel);
                dialog.Controls.Add(buttons);
                dialog.Controls.Add(box);
                dialog.Controls.Add(caption);
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                if (dialog.ShowDialog() != DialogResult.OK)
                    return null;
                string value = (box.Text ?? "").Trim();
                return value.Length == 0 ? null : value;
            }
        }


        private void WireDirty(DataGridView grid)
        {
            grid.CellValueChanged += delegate { MarkDirty(); };
            grid.UserAddedRow += delegate { MarkDirty(); };
            grid.UserDeletingRow += GridUserDeletingRow;
            grid.UserDeletedRow += delegate { MarkDirty(); };
        }

        private void GridUserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (grid == null || e.Row == null || e.Row.IsNewRow)
                return;
            string id = e.Row.Tag as string;
            if (String.IsNullOrEmpty(id) || !RecordIds.LooksLikeId(id))
                return;
            string fileName = "fuel.dat";
            if (Object.ReferenceEquals(grid, carsGrid))
                fileName = "cars.dat";
            else if (Object.ReferenceEquals(grid, repairsGrid))
                fileName = "repairs.dat";
            else if (Object.ReferenceEquals(grid, utilitiesGrid))
                fileName = "utilities.dat";
            else if (Object.ReferenceEquals(grid, categoriesGrid))
                fileName = KiloLedgerCodec.RepairCategoriesFile;
            else if (Object.ReferenceEquals(grid, expensesGrid))
                fileName = KiloLedgerCodec.ExpensesFile;
            else if (Object.ReferenceEquals(grid, expenseCategoriesGrid))
                fileName = KiloLedgerCodec.ExpenseCategoriesFile;
            repository.NoteDeleted(fileName, id);
        }

        private void MarkDirty()
        {
            if (suppressDirty)
                return;
            dirty = true;
            UpdateSaveDirtyVisual();
            SetStatus(L10n.Get("confirm.unsaved_changes"));
        }

        private void OpenFolder(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = L10n.Get("import.dat_files_desc_long");
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    repository.LoadDirectory(dialog.SelectedPath);
                    BindAll();
                    pathBox.Text = dialog.SelectedPath;
                    dirty = false;
                    UpdateSaveDirtyVisual();
                    SetStatus(L10n.Get("msg.data_folder_opened"));
                    MaybeShowOnboardingTip();
                    MaybeShowBackupReminder();
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        }

        private void OpenZip(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = L10n.Get("filter.zip_files");
                dialog.Title = L10n.Get("title.zip_backup");
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    string extract = Path.Combine(Path.GetTempPath(), "KiloLedgerDesktop_" + Guid.NewGuid().ToString("N"));
                    repository.LoadZip(dialog.FileName, extract);
                    BindAll();
                    pathBox.Text = extract;
                    dirty = false;
                    SetStatus(L10n.Get("msg.zip_opened_save_permanent"));
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        }

        private void CreateFolder(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = L10n.Get("prompt.where_create_folder");
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    if (IsInDownloadsFolder(dialog.SelectedPath))
                    {
                        if (MessageBox.Show(this,
                            L10n.Get("msg.downloads_create_confirm"),
                            L10n.Get("title.downloads_folder_warning"),
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning) != DialogResult.Yes)
                        {
                            return;
                        }
                    }
                    string target = Path.Combine(dialog.SelectedPath, "KiloLedger");
                    repository.CreateNew(target);
                    BindAll();
                    pathBox.Text = target;
                    dirty = false;
                    SetStatus((L10n.Get("msg.new_folder_created") + " ") + target);
                    MaybeShowOnboardingTip();
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        }

        private void MaybeShowOnboardingTip()
        {
            try
            {
                if (String.IsNullOrEmpty(repository.DirectoryPath))
                    return;
                if (ToSettings.IsOnboardingSeen(repository.Settings))
                    return;
                bool empty = repository.Fuel.Count == 0 && repository.Repairs.Count == 0 &&
                    repository.Expenses.Count == 0 && repository.Utilities.Count == 0;
                repository.Settings = ToSettings.SetOnboardingSeen(repository.Settings, true);
                try
                {
                    repository.Save();
                }
                catch
                {
                    dirty = true;
                }
                if (!empty)
                    return;
                string tip =
                    (L10n.Get("onboarding.title_colon") + "\r\n") +
                    (L10n.Get("about.bullet_wifi_phone_pda") + "\r\n") +
                    (L10n.Get("about.bullet_export_all_zip") + "\r\n") +
                    L10n.Get("about.bullet_summary_tab");
                MessageBox.Show(this, tip, "KiloLedger", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SetStatus(L10n.Get("onboarding.shown_once_note"));
            }
            catch
            {
                // tip must not block folder open
            }
        }

        private void MaybeShowBackupReminder()
        {
            try
            {
                if (String.IsNullOrEmpty(repository.DirectoryPath))
                    return;
                if (!ToSettings.ShouldShowBackupReminder(repository.Settings))
                    return;
                repository.Settings = ToSettings.MarkBackupNagShown(repository.Settings);
                try { repository.Save(); }
                catch { dirty = true; }
                int days = ToSettings.DaysSinceLastBackup(repository.Settings);
                string age = days < 0 ? L10n.Get("summary.no_backup_yet") : ((L10n.Get("forecast.elapsed") + " ") + days + (" " + L10n.Get("unit.days_short")));
                MessageBox.Show(this,
                    L10n.Get("reminder.backup_overdue_open") + age + ").\r\n" +
                    L10n.Get("hint.recommend_export_all_zip"),
                    L10n.Get("title.backup"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch
            {
                // soft reminder must not block folder open
            }
        }

        private void SaveData(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                PullAll();
                if (!ConfirmAllMileageWarnings())
                {
                    SetStatus(L10n.Get("msg.save_cancelled_odometer"));
                    return;
                }
                repository.Save();
                dirty = false;
                UpdateSaveDirtyVisual();
                SetStatus((L10n.Get("msg.saved_to_dat") + " ") + repository.DirectoryPath);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void SaveZip(object sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "ZIP (*.zip)|*.zip";
                dialog.FileName = "KiloLedger-data.zip";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    EnsurePath();
                    PullAll();
                    if (!ConfirmAllMileageWarnings())
                    {
                        SetStatus(L10n.Get("msg.zip_save_cancelled_odometer"));
                        return;
                    }
                    repository.SaveZip(dialog.FileName);
                    dirty = false;
                    SetStatus((L10n.Get("msg.export_all_zip_saved") + " ") + dialog.FileName);
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        }

        private void OpenImportWizard(object sender, EventArgs e)
        {
            try
            {
                using (ImportWizardForm wizard = new ImportWizardForm(repository, EnsurePathForImport))
                {
                    if (wizard.ShowDialog(this) != DialogResult.OK)
                        return;
                    pathBox.Text = repository.DirectoryPath ?? "";
                    BindAll();
                    dirty = false;
                    string safety = wizard.SafetyPath;
                    SetStatus(L10n.Get("msg.import_complete") +
                        (safety != null ? " " + L10n.Get("backup.safety_copy_colon") + " " + safety : ""));
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void ExportCsvTemplates(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = L10n.Get("prompt.where_save_csv_templates_long");
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    string folder = Path.Combine(dialog.SelectedPath, "KiloLedger-csv-templates");
                    CsvExchange.ExportTemplates(folder);
                    SetStatus((L10n.Get("label.csv_templates_colon") + " ") + folder);
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        }

        private void ExportExcel(object sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel (*.xlsx)|*.xlsx";
                dialog.FileName = "KiloLedger.xlsx";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    PullAll();
                    XlsxExchange.Export(repository, dialog.FileName);
                    SetStatus((L10n.Get("msg.excel_exported") + " ") + dialog.FileName);
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        }

        private void ImportExcel(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Excel (*.xlsx)|*.xlsx";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    EnsurePathForImport();
                    string safety = CreateSafetyIfPossible();
                    XlsxExchange.Import(repository, dialog.FileName);
                    BindAll();
                    dirty = true;
                    SetStatus(L10n.Get("msg.imported_excel_save") +
                        (safety != null ? " " + L10n.Get("backup.copy_colon") + " " + safety : ""));
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        }

        private void ExportCsv(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = L10n.Get("prompt.csv_export_folder");
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    PullAll();
                    string folder = Path.Combine(dialog.SelectedPath, "KiloLedger-csv");
                    CsvExchange.ExportFolder(repository, folder);
                    SetStatus((L10n.Get("msg.csv_exported_to") + " ") + folder + L10n.Get("hint.google_sheets_file_import"));
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        }

        private void ImportCsvFolder(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = L10n.Get("import.csv_files_desc_long");
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    EnsurePathForImport();
                    string safety = CreateSafetyIfPossible();
                    CsvExchange.ImportFolder(repository, dialog.SelectedPath);
                    BindAll();
                    dirty = true;
                    SetStatus(L10n.Get("msg.imported_csv_save") +
                        (safety != null ? " " + L10n.Get("backup.copy_colon") + " " + safety : ""));
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        }

        private void ImportGoogle(object sender, EventArgs e)
        {
            string url = googleUrlBox.ForeColor == Color.Gray ? "" : googleUrlBox.Text.Trim();
            if (url.Length == 0)
            {
                GoogleSyncSettings gs = GoogleSyncSettings.Load();
                url = gs != null && gs.SpreadsheetUrl != null ? gs.SpreadsheetUrl.Trim() : "";
            }
            if (url.Length == 0)
            {
                using (Form prompt = new Form())
                {
                    prompt.Text = "Google Sheets";
                    prompt.Width = 520;
                    prompt.Height = 160;
                    prompt.StartPosition = FormStartPosition.CenterParent;
                    prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                    prompt.MaximizeBox = false;
                    prompt.MinimizeBox = false;
                    Label lbl = new Label { Text = L10n.Get("label.sheet_url_csv"), Left = 14, Top = 14, Width = 475 };
                    TextBox tb = new TextBox { Left = 14, Top = 38, Width = 475 };
                    Button ok = new Button { Text = L10n.Get("button.import"), Left = 290, Top = 78, Width = 100, DialogResult = DialogResult.OK };
                    Button cancel = new Button { Text = L10n.Get("button.cancel"), Left = 398, Top = 78, Width = 90, DialogResult = DialogResult.Cancel };
                    prompt.AcceptButton = ok;
                    prompt.CancelButton = cancel;
                    prompt.Controls.AddRange(new Control[] { lbl, tb, ok, cancel });
                    if (prompt.ShowDialog(this) != DialogResult.OK)
                        return;
                    url = tb.Text.Trim();
                }
            }
            if (url.Length == 0)
            {
                MessageBox.Show(this,
                    L10n.Get("prompt.paste_google_sheet_link"),
                    "Google Sheets", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                EnsurePathForImport();
                Cursor = Cursors.WaitCursor;
                string safety = CreateSafetyIfPossible();
                CsvExchange.ImportGoogleSheetWorkbook(repository, url);
                BindAll();
                dirty = true;
                string imported = L10n.Get("msg.imported_google_save") +
                    (safety != null ? " " + L10n.Get("backup.copy_colon") + " " + safety : "");
                if (DengiFormat.DateNumberWarnings.Count > 0)
                {
                    imported += "\r\n\r\n" + L10n.Get("import.date_number_warnings");
                    for (int w = 0; w < DengiFormat.DateNumberWarnings.Count; w++)
                        imported += "\r\n" + DengiFormat.DateNumberWarnings[w];
                }
                SetStatus(imported);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private string CreateSafetyIfPossible()
        {
            try
            {
                if (String.IsNullOrEmpty(repository.DirectoryPath))
                    return null;
                if (!File.Exists(Path.Combine(repository.DirectoryPath, "cars.dat")))
                    return null;
                return repository.CreateSafetyBackup(null);
            }
            catch
            {
                return null;
            }
        }

        private void EnsurePath()
        {
            if (!String.IsNullOrEmpty(repository.DirectoryPath))
                return;
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = L10n.Get("prompt.choose_folder_save");
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    throw new InvalidOperationException(L10n.Get("error.no_folder_selected"));
                string target = Path.Combine(dialog.SelectedPath, "KiloLedger");
                Directory.CreateDirectory(target);
                repository.CreateNew(target);
                pathBox.Text = target;
            }
        }

        private void EnsurePathForImport()
        {
            if (!String.IsNullOrEmpty(repository.DirectoryPath))
                return;
            string target = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "KiloLedger");
            Directory.CreateDirectory(target);
            if (!File.Exists(Path.Combine(target, "cars.dat")))
                repository.CreateNew(target);
            else
                repository.LoadDirectory(target);
            pathBox.Text = target;
        }

        private void BindAll()
        {
            suppressDirty = true;
            try
            {
                ToIntervals intervals = ToSettings.FromRows(repository.Settings);
                oilIntervalBox.Text = intervals.OilInterval.ToString(System.Globalization.CultureInfo.InvariantCulture);
                oilWarnBox.Text = intervals.OilWarn.ToString(System.Globalization.CultureInfo.InvariantCulture);
                beltIntervalBox.Text = intervals.BeltInterval.ToString(System.Globalization.CultureInfo.InvariantCulture);
                beltWarnBox.Text = intervals.BeltWarn.ToString(System.Globalization.CultureInfo.InvariantCulture);
                currencyBox.Text = ToSettings.GetCurrency(repository.Settings);
                string storedLanguage = ToSettings.GetLanguage(repository.Settings);
                languageBox.Text = storedLanguage;
                LocalizationManager.Instance.CurrentLanguage = storedLanguage;
                budgetBox.Text = ToSettings.GetBudgetMonth(repository.Settings)
                    .ToString(CultureInfo.InvariantCulture);
                budgetWarnBox.Text = ToSettings.GetBudgetWarnPct(repository.Settings)
                    .ToString(CultureInfo.InvariantCulture);
                backupRemindBox.Text = ToSettings.GetBackupRemindDays(repository.Settings)
                    .ToString(CultureInfo.InvariantCulture);
                reminderWarnBox.Text = ToSettings.GetReminderWarnDays(repository.Settings)
                    .ToString(CultureInfo.InvariantCulture);
                RefreshDefaultCarBox();
                // Column headers include currency code — refresh before binding rows.
                SetupFuelGrid();
                SetupRepairsGrid();
                SetupExpensesGrid();
                BindCarsGrid();
                BindFuelGrid(repository.Fuel);
                BindGrid(repairsGrid, repository.Repairs, 9);
                BindGrid(expensesGrid, repository.Expenses, 6);
                BindGrid(expenseCategoriesGrid, repository.ExpenseCategories, 1);
                BindGrid(utilitiesGrid, repository.Utilities, 8);
                BindGrid(categoriesGrid, repository.Categories, 1);
                RefreshCategoryFilterLists();
                ApplyAllFilterVisibility();
                RefreshSummaryText();
            }
            finally
            {
                suppressDirty = false;
            }
        }

        private static void BindGrid(DataGridView grid, List<string[]> rows, int columns)
        {
            grid.Rows.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                string[] content = RecordIds.StripId(rows[i]);
                object[] values = new object[columns];
                for (int c = 0; c < columns; c++)
                    values[c] = c < content.Length ? content[c] : "";
                int index = grid.Rows.Add(values);
                grid.Rows[index].Tag = RecordIds.GetId(rows[i]);
            }
        }

        private void BindFuelGrid(List<string[]> rows)
        {
            fuelGrid.Rows.Clear();
            RefreshStationFilterItems(rows);
            for (int i = 0; i < rows.Count; i++)
            {
                string[] content = RecordIds.StripId(rows[i]);
                object[] values = new object[10];
                for (int c = 0; c < 7; c++)
                    values[c] = c < content.Length ? content[c] : "";
                values[7] = KiloLedgerCodec.IsFullTank(rows[i]);
                string purpose = KiloLedgerCodec.GetPurpose(rows[i]);
                EnsurePurposeComboItem(purpose);
                values[8] = purpose;
                values[9] = KiloLedgerCodec.GetStation(rows[i]);
                int index = fuelGrid.Rows.Add(values);
                fuelGrid.Rows[index].Tag = RecordIds.GetId(rows[i]);
            }
        }

        private void EnsurePurposeComboItem(string purpose)
        {
            if (String.IsNullOrEmpty(purpose) || fuelGrid.Columns.Count < 9)
                return;
            DataGridViewComboBoxColumn column = fuelGrid.Columns[8] as DataGridViewComboBoxColumn;
            if (column == null)
                return;
            for (int i = 0; i < column.Items.Count; i++)
                if (String.Equals(Convert.ToString(column.Items[i]), purpose, StringComparison.Ordinal))
                    return;
            column.Items.Add(purpose);
        }

        private void RefreshStationFilterItems(List<string[]> rows)
        {
            string previous = fuelFilterStation.SelectedItem == null
                ? "" : fuelFilterStation.SelectedItem.ToString();
            fuelFilterStation.Items.Clear();
            fuelFilterStation.Items.Add("Все АЗС");
            string[] known = KiloLedgerCodec.CollectStationSuggestions(rows);
            for (int i = 0; i < known.Length; i++)
                fuelFilterStation.Items.Add(known[i]);
            int selected = 0;
            if (!String.IsNullOrEmpty(previous))
            {
                for (int i = 0; i < fuelFilterStation.Items.Count; i++)
                {
                    if (String.Equals(Convert.ToString(fuelFilterStation.Items[i]), previous,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        selected = i;
                        break;
                    }
                }
            }
            if (fuelFilterStation.Items.Count > 0)
                fuelFilterStation.SelectedIndex = selected;
        }

        private void PullAll()
        {
            ToIntervals intervals = ToSettings.FromRows(repository.Settings);
            decimal oilInterval, oilWarn, beltInterval, beltWarn;
            if (KiloLedgerCodec.TryNumber(oilIntervalBox.Text, out oilInterval) && oilInterval > 0)
                intervals.OilInterval = oilInterval;
            if (KiloLedgerCodec.TryNumber(oilWarnBox.Text, out oilWarn) && oilWarn > 0)
                intervals.OilWarn = oilWarn;
            if (KiloLedgerCodec.TryNumber(beltIntervalBox.Text, out beltInterval) && beltInterval > 0)
                intervals.BeltInterval = beltInterval;
            if (KiloLedgerCodec.TryNumber(beltWarnBox.Text, out beltWarn) && beltWarn > 0)
                intervals.BeltWarn = beltWarn;
            // ReadGrid includes hidden (filtered-out) rows — фильтр только визуальный.
            List<string[]> cars;
            List<string[]> carsMeta;
            ReadCarsGrid(out cars, out carsMeta);
            List<string[]> settings = ToSettings.ApplyIntervals(repository.Settings, intervals);
            string currencyCode = ToSettings.NormalizeCurrency(currencyBox.Text);
            if (currencyCode != null)
                settings = ToSettings.SetCurrency(settings, currencyCode);
            repository.ReplaceAll(
                cars,
                ReadFuelGrid(),
                ReadGrid(repairsGrid, 9),
                ReadGrid(utilitiesGrid, 8),
                ReadGrid(categoriesGrid, 1),
                ReadGrid(expensesGrid, 6),
                ReadGrid(expenseCategoriesGrid, 1),
                settings);
            repository.CarsMetaRows = carsMeta;
        }

        private void BindCarsGrid()
        {
            carsGrid.Rows.Clear();
            Dictionary<string, CarPassport> metaMap =
                new Dictionary<string, CarPassport>(StringComparer.OrdinalIgnoreCase);
            if (repository.CarsMetaRows != null)
            {
                for (int i = 0; i < repository.CarsMetaRows.Count; i++)
                {
                    CarPassport meta = CarsMeta.FromRow(repository.CarsMetaRows[i]);
                    if (meta.CarName.Length > 0 && !metaMap.ContainsKey(meta.CarName))
                        metaMap.Add(meta.CarName, meta);
                }
            }
            for (int i = 0; i < repository.Cars.Count; i++)
            {
                string[] content = RecordIds.StripId(repository.Cars[i]);
                string name = content.Length > 0 ? (content[0] ?? "") : "";
                CarPassport meta;
                if (!metaMap.TryGetValue(name, out meta))
                    meta = new CarPassport { CarName = name };
                int index = carsGrid.Rows.Add(name, meta.Year, meta.Plate, meta.Vin,
                    meta.TankLiters, meta.Note);
                carsGrid.Rows[index].Tag = RecordIds.GetId(repository.Cars[i]);
            }
        }

        private void ReadCarsGrid(out List<string[]> cars, out List<string[]> carsMeta)
        {
            cars = new List<string[]>();
            carsMeta = new List<string[]>();
            Dictionary<string, string> metaIds =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (repository.CarsMetaRows != null)
            {
                for (int i = 0; i < repository.CarsMetaRows.Count; i++)
                {
                    CarPassport existing = CarsMeta.FromRow(repository.CarsMetaRows[i]);
                    if (existing.CarName.Length > 0 && !String.IsNullOrEmpty(existing.Id) &&
                        !metaIds.ContainsKey(existing.CarName))
                        metaIds.Add(existing.CarName, existing.Id);
                }
            }
            for (int i = 0; i < carsGrid.Rows.Count; i++)
            {
                DataGridViewRow row = carsGrid.Rows[i];
                if (row.IsNewRow)
                    continue;
                string name = CellText(row, 0).Trim();
                if (name.Length == 0)
                    continue;
                string[] carRow = new string[] { name };
                string id = row.Tag as string;
                if (!String.IsNullOrEmpty(id) && RecordIds.LooksLikeId(id))
                    carRow = RecordIds.WithId(carRow, id);
                cars.Add(carRow);

                CarPassport meta = new CarPassport();
                meta.CarName = name;
                meta.Year = CellText(row, 1);
                meta.Plate = CellText(row, 2);
                meta.Vin = CellText(row, 3);
                meta.TankLiters = CellText(row, 4);
                meta.Note = CellText(row, 5);
                string metaId;
                if (metaIds.TryGetValue(name, out metaId))
                    meta.Id = metaId;
                bool hasMeta = meta.Year.Length > 0 || meta.Plate.Length > 0 ||
                    meta.Vin.Length > 0 || meta.TankLiters.Length > 0 || meta.Note.Length > 0 ||
                    !String.IsNullOrEmpty(meta.Id);
                if (hasMeta)
                    carsMeta.Add(CarsMeta.ToRow(meta));
            }
        }

        private static string CellText(DataGridViewRow row, int column)
        {
            if (row == null || column < 0 || column >= row.Cells.Count || row.Cells[column].Value == null)
                return "";
            return (Convert.ToString(row.Cells[column].Value) ?? "").Trim();
        }

        private List<string[]> ReadFuelGrid()
        {
            List<string[]> rows = new List<string[]>();
            for (int i = 0; i < fuelGrid.Rows.Count; i++)
            {
                DataGridViewRow row = fuelGrid.Rows[i];
                if (row.IsNewRow)
                    continue;
                string[] values = new string[10];
                bool empty = true;
                for (int c = 0; c < 7; c++)
                {
                    object cell = row.Cells[c].Value;
                    values[c] = cell == null ? "" : Convert.ToString(cell);
                    if (!String.IsNullOrEmpty(values[c].Trim()))
                        empty = false;
                }
                if (empty)
                    continue;
                object fullCell = row.Cells[7].Value;
                bool full = true;
                if (fullCell is bool)
                    full = (bool)fullCell;
                else if (fullCell != null)
                {
                    string text = Convert.ToString(fullCell).Trim();
                    full = text.Length == 0 || text == "1" ||
                        String.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
                }
                values[7] = full ? "1" : "0";
                values[8] = KiloLedgerCodec.NormalizePurpose(CellText(row, 8));
                values[9] = KiloLedgerCodec.NormalizeStation(CellText(row, 9));
                int length = 10;
                while (length > 5)
                {
                    if ((values[length - 1] ?? "").Trim().Length > 0)
                        break;
                    length--;
                }
                if (length < values.Length)
                {
                    string[] trimmed = new string[length];
                    Array.Copy(values, trimmed, length);
                    values = trimmed;
                }
                string id = row.Tag as string;
                if (!String.IsNullOrEmpty(id) && RecordIds.LooksLikeId(id))
                    values = RecordIds.WithId(values, id);
                rows.Add(values);
            }
            return rows;
        }

        private void SummaryTabSelected(object sender, EventArgs e)
        {
            if (tabs.SelectedTab != summaryTab || refreshingSummary)
                return;
            try
            {
                refreshingSummary = true;
                if (!String.IsNullOrEmpty(repository.DirectoryPath) ||
                    carsGrid.Rows.Count > 1 || fuelGrid.Rows.Count > 1)
                    PullAll();
                RefreshSummaryText();
            }
            catch (Exception ex)
            {
                summaryBox.Text = (L10n.Get("error.refresh_summary_failed") + " ") + ex.Message;
            }
            finally
            {
                refreshingSummary = false;
            }
        }

        private void RefreshSummaryText()
        {
            if (repository.Cars.Count == 0 && repository.Fuel.Count == 0 &&
                repository.Repairs.Count == 0 && repository.Utilities.Count == 0 &&
                repository.Expenses.Count == 0)
            {
                summaryBox.Text = L10n.Get("message.select_folder");
                chartShowsCostPerKm = false;
                summaryChart.SetData(new List<string>(), new List<decimal[]>());
                return;
            }
            string car = ToSettings.ResolvePreferredCar(repository.Cars, repository.Settings);
            summaryBox.Text = BuildSummary(car);
            RefreshSummaryChart();
        }

        private void RefreshSummaryChart()
        {
            if (chartShowsCostPerKm)
            {
                List<string> cars = new List<string>();
                for (int i = 0; i < repository.Cars.Count; i++)
                {
                    string[] content = RecordIds.StripId(repository.Cars[i]);
                    if (content.Length == 0)
                        continue;
                    string name = (content[0] ?? "").Trim();
                    if (name.Length > 0)
                        cars.Add(name);
                }
                List<CarCompare.Stats> stats = CarCompare.Build(
                    cars, repository.Fuel, repository.Repairs, repository.Expenses);
                List<string> labels = new List<string>();
                List<decimal> values = new List<decimal>();
                for (int i = 0; i < stats.Count; i++)
                {
                    decimal cpk = CarCompare.CostPerKm(stats[i]);
                    if (cpk <= 0)
                        continue;
                    labels.Add(stats[i].Name);
                    values.Add(cpk);
                }
                summaryChart.SetCompareData(labels, values, Cur() + L10n.Get("unit.per_km"));
                return;
            }

            Dictionary<string, decimal[]> monthlyTotals = new Dictionary<string, decimal[]>();
            for (int i = 0; i < repository.Fuel.Count; i++)
            {
                if (repository.Fuel[i].Length < 5)
                    continue;
                AddMonthlyAmount(monthlyTotals, repository.Fuel[i][0], 0, NumberOrZero(repository.Fuel[i][4]));
            }
            for (int i = 0; i < repository.Repairs.Count; i++)
            {
                if (repository.Repairs[i].Length < 8)
                    continue;
                AddMonthlyAmount(monthlyTotals, repository.Repairs[i][0], 1, NumberOrZero(repository.Repairs[i][7]));
            }
            for (int i = 0; i < repository.Expenses.Count; i++)
            {
                if (repository.Expenses[i].Length < 5)
                    continue;
                AddMonthlyAmount(monthlyTotals, repository.Expenses[i][0], 3, NumberOrZero(repository.Expenses[i][4]));
            }
            for (int i = 0; i < repository.Utilities.Count; i++)
                AddMonthlyAmount(monthlyTotals, repository.Utilities[i][0], 2, UtilityTotal(repository.Utilities[i]));

            List<string> months = new List<string>();
            List<decimal[]> totals = new List<decimal[]>();
            DateTime cursor = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            for (int i = 11; i >= 0; i--)
            {
                DateTime month = cursor.AddMonths(-i);
                string key = month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                months.Add(key);
                decimal[] values;
                if (!monthlyTotals.TryGetValue(key, out values))
                    values = new decimal[4];
                totals.Add(values);
            }
            summaryChart.SetData(months, totals);
        }

        private void ToggleCostPerKmChart(object sender, EventArgs e)
        {
            chartShowsCostPerKm = !chartShowsCostPerKm;
            RefreshSummaryChart();
            SetStatus(chartShowsCostPerKm
                ? L10n.Get("chart.caption_cost_per_km")
                : L10n.Get("chart.caption_monthly"));
        }

        private void DeleteReceiptPhoto(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                string id = GetSelectedRecordId();
                if (!ReceiptStore.Exists(repository.DirectoryPath, id))
                {
                    MessageBox.Show(this, L10n.Get("error.no_receipt_for_record"),
                        L10n.Get("button.attach_receipt"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                DialogResult answer = MessageBox.Show(this,
                    L10n.Get("confirm.delete_receipt_photo"),
                    L10n.Get("button.attach_receipt"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                {
                    ReceiptStore.Delete(repository.DirectoryPath, id);
                    SetStatus(L10n.Get("msg.receipt_deleted"));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, L10n.Get("button.attach_receipt"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AttachReceiptPhoto(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                string id = GetSelectedRecordId();
                ReceiptStore.OpenOrAttach(repository.DirectoryPath, id, this);
                SetStatus(ReceiptStore.Exists(repository.DirectoryPath, id)
                    ? (L10n.Get("label.receipt_photo_colon") + " ") + ReceiptStore.PathForId(repository.DirectoryPath, id)
                    : L10n.Get("error.receipt_not_saved"));
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void ShowCarTimeline(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                PullAll();
                string car = repository.Cars.Count > 0
                    ? (RecordIds.StripId(repository.Cars[0])[0] ?? "")
                    : "";
                if (car.Length == 0)
                    throw new InvalidOperationException(L10n.Get("error.no_car_for_timeline"));
                CarPassport passport = FindPassport(car);
                string text = CarTimeline.FormatList(
                    CarTimeline.Build(repository.Fuel, repository.Repairs, repository.Expenses,
                        repository.NotesRows, car),
                    car,
                    FormatPassportSuffix(passport));
                using (Form dialog = new Form())
                {
                    dialog.Text = (L10n.Get("title.timeline_dot") + " ") + car;
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.Size = new Size(640, 480);
                    dialog.MinimizeBox = false;
                    dialog.MaximizeBox = true;
                    TextBox box = new TextBox
                    {
                        Multiline = true,
                        ReadOnly = true,
                        ScrollBars = ScrollBars.Vertical,
                        Dock = DockStyle.Fill,
                        Font = new Font("Consolas", 10F),
                        Text = text
                    };
                    dialog.Controls.Add(box);
                    dialog.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void ShowPeriodCompare(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                PullAll();
                string car = repository.Cars.Count > 0
                    ? (RecordIds.StripId(repository.Cars[0])[0] ?? "")
                    : "";
                if (car.Length == 0)
                    throw new InvalidOperationException(L10n.Get("error.no_car_for_compare"));

                string aFrom, aTo, bFrom, bTo;
                PeriodCompare.ThisVsPreviousMonth(out aFrom, out aTo, out bFrom, out bTo);

                using (Form dialog = new Form())
                {
                    dialog.Text = (L10n.Get("compare.periods_dot") + " ") + car;
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.Size = new Size(520, 420);
                    dialog.MinimizeBox = false;
                    dialog.MaximizeBox = false;
                    dialog.FormBorderStyle = FormBorderStyle.FixedDialog;

                    TableLayoutPanel layout = new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        ColumnCount = 2,
                        RowCount = 7,
                        Padding = new Padding(12)
                    };
                    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
                    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    for (int r = 0; r < 6; r++)
                        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
                    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                    TextBox aFromBox = new TextBox { Text = aFrom, Dock = DockStyle.Fill };
                    TextBox aToBox = new TextBox { Text = aTo, Dock = DockStyle.Fill };
                    TextBox bFromBox = new TextBox { Text = bFrom, Dock = DockStyle.Fill };
                    TextBox bToBox = new TextBox { Text = bTo, Dock = DockStyle.Fill };
                    layout.Controls.Add(new Label { Text = L10n.Get("compare.a_from"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
                    layout.Controls.Add(aFromBox, 1, 0);
                    layout.Controls.Add(new Label { Text = L10n.Get("compare.a_to"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
                    layout.Controls.Add(aToBox, 1, 1);
                    layout.Controls.Add(new Label { Text = L10n.Get("compare.b_from"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
                    layout.Controls.Add(bFromBox, 1, 2);
                    layout.Controls.Add(new Label { Text = L10n.Get("compare.b_to"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
                    layout.Controls.Add(bToBox, 1, 3);

                    FlowLayoutPanel presets = new FlowLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        WrapContents = true
                    };
                    Button thisVsPrev = new Button { Text = L10n.Get("preset.this_or_last_month"), AutoSize = true };
                    thisVsPrev.Click += delegate
                    {
                        string af, at, bf, bt;
                        PeriodCompare.ThisVsPreviousMonth(out af, out at, out bf, out bt);
                        aFromBox.Text = af;
                        aToBox.Text = at;
                        bFromBox.Text = bf;
                        bToBox.Text = bt;
                    };
                    presets.Controls.Add(thisVsPrev);
                    layout.Controls.Add(new Label { Text = L10n.Get("label.preset"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
                    layout.Controls.Add(presets, 1, 4);

                    FlowLayoutPanel buttons = new FlowLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        FlowDirection = FlowDirection.RightToLeft
                    };
                    Button cancel = new Button { Text = L10n.Get("button.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
                    Button ok = new Button { Text = L10n.Get("button.compare"), DialogResult = DialogResult.OK, AutoSize = true };
                    buttons.Controls.Add(ok);
                    buttons.Controls.Add(cancel);
                    layout.Controls.Add(buttons, 0, 5);
                    layout.SetColumnSpan(buttons, 2);

                    Label hint = new Label
                    {
                        Text = L10n.Get("compare.dates_hint"),
                        AutoSize = true,
                        Dock = DockStyle.Top
                    };
                    layout.Controls.Add(hint, 0, 6);
                    layout.SetColumnSpan(hint, 2);

                    dialog.Controls.Add(layout);
                    dialog.AcceptButton = ok;
                    dialog.CancelButton = cancel;
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    string af2 = (aFromBox.Text ?? "").Trim();
                    string at2 = (aToBox.Text ?? "").Trim();
                    string bf2 = (bFromBox.Text ?? "").Trim();
                    string bt2 = (bToBox.Text ?? "").Trim();
                    if (!IsDay(af2) || !IsDay(at2) || !IsDay(bf2) || !IsDay(bt2))
                        throw new InvalidOperationException(L10n.Get("error.dates_yyyy_mm_dd"));
                    if (String.CompareOrdinal(af2, at2) > 0 || String.CompareOrdinal(bf2, bt2) > 0)
                        throw new InvalidOperationException(L10n.Get("error.period_from_after_to"));

                    PeriodCompare.Totals ta = PeriodCompare.Build(
                        car, repository.Fuel, repository.Repairs, repository.Expenses,
                        repository.Utilities, af2, at2);
                    PeriodCompare.Totals tb = PeriodCompare.Build(
                        car, repository.Fuel, repository.Repairs, repository.Expenses,
                        repository.Utilities, bf2, bt2);
                    string text = PeriodCompare.FormatComparison(
                        ta, tb,
                        PeriodCompare.FormatRangeLabel(af2, at2),
                        PeriodCompare.FormatRangeLabel(bf2, bt2),
                        Cur(), Money);

                    using (Form result = new Form())
                    {
                        result.Text = (L10n.Get("compare.short_dot") + " ") + car;
                        result.StartPosition = FormStartPosition.CenterParent;
                        result.Size = new Size(560, 480);
                        result.MinimizeBox = false;
                        TextBox box = new TextBox
                        {
                            Multiline = true,
                            ReadOnly = true,
                            ScrollBars = ScrollBars.Vertical,
                            Dock = DockStyle.Fill,
                            Font = new Font("Consolas", 10F),
                            Text = text
                        };
                        result.Controls.Add(box);
                        result.ShowDialog(this);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private static bool IsDay(string value)
        {
            DateTime parsed;
            return value != null && value.Length == 10 &&
                DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out parsed);
        }

        private void ShowPeriodReport(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                PullAll();
                string car = ToSettings.ResolvePreferredCar(repository.Cars, repository.Settings);
                if (car == null || car.Length == 0)
                    throw new InvalidOperationException(L10n.Get("error.no_car_for_report"));

                string from, to, prevFrom, prevTo;
                PeriodCompare.ThisVsPreviousMonth(out from, out to, out prevFrom, out prevTo);

                using (Form dialog = new Form())
                {
                    dialog.Text = (L10n.Get("report.title_dot") + " ") + car;
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.Size = new Size(480, 280);
                    dialog.MinimizeBox = false;
                    dialog.MaximizeBox = false;
                    dialog.FormBorderStyle = FormBorderStyle.FixedDialog;

                    TableLayoutPanel layout = new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        ColumnCount = 2,
                        RowCount = 5,
                        Padding = new Padding(12)
                    };
                    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
                    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    for (int r = 0; r < 4; r++)
                        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
                    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                    TextBox fromBox = new TextBox { Text = from, Dock = DockStyle.Fill };
                    TextBox toBox = new TextBox { Text = to, Dock = DockStyle.Fill };
                    layout.Controls.Add(new Label { Text = L10n.Get("label.from"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
                    layout.Controls.Add(fromBox, 1, 0);
                    layout.Controls.Add(new Label { Text = L10n.Get("label.to"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
                    layout.Controls.Add(toBox, 1, 1);

                    FlowLayoutPanel presets = new FlowLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        WrapContents = true
                    };
                    Button thisMonth = new Button { Text = L10n.Get("preset.this_month"), AutoSize = true };
                    thisMonth.Click += delegate
                    {
                        string af, at, bf, bt;
                        PeriodCompare.ThisVsPreviousMonth(out af, out at, out bf, out bt);
                        fromBox.Text = af;
                        toBox.Text = at;
                    };
                    Button prevMonth = new Button { Text = L10n.Get("preset.last_month"), AutoSize = true };
                    prevMonth.Click += delegate
                    {
                        string af, at, bf, bt;
                        PeriodCompare.ThisVsPreviousMonth(out af, out at, out bf, out bt);
                        fromBox.Text = bf;
                        toBox.Text = bt;
                    };
                    Button thisYear = new Button { Text = L10n.Get("preset.this_year"), AutoSize = true };
                    thisYear.Click += delegate
                    {
                        DateTime today = DateTime.Today;
                        fromBox.Text = PeriodCompare.FormatDay(new DateTime(today.Year, 1, 1));
                        toBox.Text = PeriodCompare.FormatDay(today);
                    };
                    presets.Controls.Add(thisMonth);
                    presets.Controls.Add(prevMonth);
                    presets.Controls.Add(thisYear);
                    layout.Controls.Add(new Label { Text = L10n.Get("label.preset"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
                    layout.Controls.Add(presets, 1, 2);

                    FlowLayoutPanel buttons = new FlowLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        FlowDirection = FlowDirection.RightToLeft
                    };
                    Button cancel = new Button { Text = L10n.Get("button.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
                    Button ok = new Button { Text = L10n.Get("button.show"), DialogResult = DialogResult.OK, AutoSize = true };
                    buttons.Controls.Add(ok);
                    buttons.Controls.Add(cancel);
                    layout.Controls.Add(buttons, 0, 3);
                    layout.SetColumnSpan(buttons, 2);

                    Label hint = new Label
                    {
                        Text = L10n.Get("report.dates_hint"),
                        AutoSize = true,
                        Dock = DockStyle.Top
                    };
                    layout.Controls.Add(hint, 0, 4);
                    layout.SetColumnSpan(hint, 2);

                    dialog.Controls.Add(layout);
                    dialog.AcceptButton = ok;
                    dialog.CancelButton = cancel;
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    string f = (fromBox.Text ?? "").Trim();
                    string t = (toBox.Text ?? "").Trim();
                    if (!IsDay(f) || !IsDay(t))
                        throw new InvalidOperationException(L10n.Get("error.dates_yyyy_mm_dd"));
                    if (String.CompareOrdinal(f, t) > 0)
                        throw new InvalidOperationException(L10n.Get("error.date_from_after_to"));

                    PeriodCompare.Totals totals = PeriodCompare.Build(
                        car, repository.Fuel, repository.Repairs, repository.Expenses,
                        repository.Utilities, f, t);
                    PeriodCompare.AddTripKm(totals, car, repository.TripsRows, f, t);
                    PeriodCompare.ApplyBudget(totals, ToSettings.GetBudgetMonth(repository.Settings), f, t);
                    string text = PeriodReport.FormatText(car, f, t, totals, Cur(), Money);
                    string html = PeriodReport.FormatHtml(car, f, t, totals, Cur(), Money);
                    ShowPeriodReportResult(car, f, t, text, html);
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void ShowPeriodReportResult(string car, string from, string to, string text, string html)
        {
            using (Form result = new Form())
            {
                result.Text = (L10n.Get("report.short_dot") + " ") + car + " · " + PeriodCompare.FormatRangeLabel(from, to);
                result.StartPosition = FormStartPosition.CenterParent;
                result.Size = new Size(560, 520);
                result.MinimizeBox = false;

                TableLayoutPanel layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    Padding = new Padding(8)
                };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                FlowLayoutPanel buttons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    WrapContents = false
                };
                Button saveTxt = new Button { Text = L10n.Get("button.save_txt"), AutoSize = true };
                Button saveHtml = new Button { Text = L10n.Get("button.save_html"), AutoSize = true };
                Button print = new Button { Text = L10n.Get("button.print_ellipsis"), AutoSize = true };
                Button close = new Button { Text = L10n.Get("button.close"), DialogResult = DialogResult.OK, AutoSize = true };
                saveTxt.Click += delegate
                {
                    SavePeriodReportFile(text, L10n.Get("filter.txt_files"),
                        "Report_" + from + "_" + to + ".txt", Encoding.UTF8);
                };
                saveHtml.Click += delegate
                {
                    SavePeriodReportFile(html, "HTML (*.html)|*.html",
                        "Report_" + from + "_" + to + ".html", Encoding.UTF8);
                };
                print.Click += delegate { PrintPeriodReport(text); };
                buttons.Controls.Add(saveTxt);
                buttons.Controls.Add(saveHtml);
                buttons.Controls.Add(print);
                buttons.Controls.Add(close);

                TextBox box = new TextBox
                {
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Dock = DockStyle.Fill,
                    Font = new Font("Consolas", 10F),
                    Text = text
                };
                layout.Controls.Add(buttons, 0, 0);
                layout.Controls.Add(box, 0, 1);
                result.Controls.Add(layout);
                result.AcceptButton = close;
                result.ShowDialog(this);
            }
        }

        private void SavePeriodReportFile(string content, string filter, string fileName, Encoding encoding)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = filter;
                dialog.FileName = fileName;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    File.WriteAllText(dialog.FileName, content ?? "", encoding);
                    SetStatus((L10n.Get("msg.report_saved_colon") + " ") + dialog.FileName);
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        }

        private void PrintPeriodReport(string text)
        {
            try
            {
                string[] lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                int index = 0;
                PrintDocument doc = new PrintDocument();
                doc.DocumentName = L10n.Get("report.header");
                doc.PrintPage += delegate(object s, PrintPageEventArgs e)
                {
                    Font font = new Font("Consolas", 10F);
                    float y = e.MarginBounds.Top;
                    float lineHeight = font.GetHeight(e.Graphics);
                    while (index < lines.Length)
                    {
                        if (y + lineHeight > e.MarginBounds.Bottom)
                        {
                            e.HasMorePages = true;
                            return;
                        }
                        e.Graphics.DrawString(lines[index], font, Brushes.Black,
                            e.MarginBounds.Left, y);
                        y += lineHeight;
                        index++;
                    }
                    e.HasMorePages = false;
                };
                using (PrintDialog dialog = new PrintDialog())
                {
                    dialog.Document = doc;
                    dialog.AllowSomePages = false;
                    dialog.UseEXDialog = true;
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        doc.Print();
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private CarPassport FindPassport(string carName)
        {
            if (repository.CarsMetaRows != null)
            {
                for (int i = 0; i < repository.CarsMetaRows.Count; i++)
                {
                    CarPassport meta = CarsMeta.FromRow(repository.CarsMetaRows[i]);
                    if (String.Equals(meta.CarName, carName, StringComparison.OrdinalIgnoreCase))
                        return meta;
                }
            }
            return new CarPassport { CarName = carName };
        }

        private static string FormatPassportSuffix(CarPassport meta)
        {
            if (meta == null)
                return "";
            StringBuilder sb = new StringBuilder();
            if (meta.Year.Length > 0)
                sb.Append(" · ").Append(meta.Year);
            if (meta.Plate.Length > 0)
                sb.Append(" · ").Append(meta.Plate);
            return sb.ToString();
        }

        private string GetSelectedRecordId()
        {
            DataGridView grid = null;
            if (tabs.SelectedTab != null)
            {
                if (tabs.SelectedTab.Text == L10n.Get("tab.fuel"))
                    grid = fuelGrid;
                else if (tabs.SelectedTab.Text == L10n.Get("tab.repairs"))
                    grid = repairsGrid;
                else if (tabs.SelectedTab.Text == L10n.Get("tab.expenses"))
                    grid = expensesGrid;
            }
            if (grid == null || grid.CurrentRow == null || grid.CurrentRow.IsNewRow)
                throw new InvalidOperationException(
                    L10n.Get("error.select_row_on_tab"));
            string id = grid.CurrentRow.Tag as string;
            if (String.IsNullOrEmpty(id) || !RecordIds.LooksLikeId(id))
                throw new InvalidOperationException(
                    L10n.Get("error.row_no_id_save_reopen"));
            return id;
        }

        private string BuildSummary(string currentCar)
        {
            decimal fuelTotal = 0;
            decimal yearFuel = 0;
            decimal yearRepair = 0;
            decimal yearExpense = 0;
            decimal yearUtility = 0;
            decimal purposePersonal = 0;
            decimal purposeWork = 0;
            decimal purposeOther = 0;
            bool anyPurpose = false;
            Dictionary<string, decimal[]> stationStats = new Dictionary<string, decimal[]>(
                StringComparer.OrdinalIgnoreCase);
            string yearPrefix = DateTime.Now.ToString("yyyy", CultureInfo.InvariantCulture);
            List<string[]> fuels = repository.Fuel;
            Dictionary<string, decimal[]> monthlyTotals = new Dictionary<string, decimal[]>();
            List<string> cars = new List<string>();
            for (int i = 0; i < repository.Cars.Count; i++)
            {
                string[] carRow = RecordIds.StripId(repository.Cars[i]);
                if (carRow.Length == 0)
                    continue;
                string name = (carRow[0] ?? "").Trim();
                if (name.Length == 0)
                    continue;
                cars.Add(name);
            }

            for (int i = 0; i < fuels.Count; i++)
            {
                string[] row = fuels[i];
                if (row.Length < 5)
                    continue;
                decimal amount = NumberOrZero(row[4]);
                AddMonthlyAmount(monthlyTotals, row[0], 0, amount);
                if (row[1] == currentCar)
                {
                    fuelTotal += amount;
                    if (row[0] != null && row[0].StartsWith(yearPrefix, StringComparison.Ordinal))
                        yearFuel += amount;
                    string purpose = KiloLedgerCodec.GetPurpose(row);
                    if (purpose.Length > 0)
                    {
                        anyPurpose = true;
                        if (String.Equals(purpose, "Личный", StringComparison.OrdinalIgnoreCase))
                            purposePersonal += amount;
                        else if (String.Equals(purpose, "Работа", StringComparison.OrdinalIgnoreCase))
                            purposeWork += amount;
                        else
                            purposeOther += amount;
                    }
                    string station = KiloLedgerCodec.GetStation(row);
                    if (station.Length > 0)
                    {
                        decimal price = NumberOrZero(row.Length > 5 ? row[5] : "");
                        if (price <= 0)
                        {
                            decimal liters = NumberOrZero(row[3]);
                            if (liters > 0)
                                price = amount / liters;
                        }
                        decimal[] bucket;
                        if (!stationStats.TryGetValue(station, out bucket))
                        {
                            bucket = new decimal[2];
                            stationStats[station] = bucket;
                        }
                        bucket[0] += 1;
                        bucket[1] += price;
                    }
                }
            }

            decimal repairTotal = 0;
            Dictionary<string, decimal> byCategory = new Dictionary<string, decimal>();
            List<string[]> repairs = repository.Repairs;
            for (int i = 0; i < repairs.Count; i++)
            {
                string[] row = repairs[i];
                if (row.Length < 8)
                    continue;
                decimal amount = NumberOrZero(row[7]);
                AddMonthlyAmount(monthlyTotals, row[0], 1, amount);
                if (row[1] != currentCar)
                    continue;
                repairTotal += amount;
                if (row[0] != null && row[0].StartsWith(yearPrefix, StringComparison.Ordinal))
                    yearRepair += amount;
                string category = row[3];
                if (!byCategory.ContainsKey(category))
                    byCategory.Add(category, 0);
                byCategory[category] += amount;
            }

            decimal expenseTotal = 0;
            List<string[]> expenses = repository.Expenses;
            for (int i = 0; i < expenses.Count; i++)
            {
                string[] row = expenses[i];
                if (row.Length < 5)
                    continue;
                decimal amount = NumberOrZero(row[4]);
                AddMonthlyAmount(monthlyTotals, row[0], 3, amount);
                if (row[1] == currentCar)
                {
                    expenseTotal += amount;
                    if (row[0] != null && row[0].StartsWith(yearPrefix, StringComparison.Ordinal))
                        yearExpense += amount;
                }
            }

            decimal utilityTotal = 0;
            List<string[]> utilities = repository.Utilities;
            for (int i = 0; i < utilities.Count; i++)
            {
                decimal amount = UtilityTotal(utilities[i]);
                utilityTotal += amount;
                AddMonthlyAmount(monthlyTotals, utilities[i][0], 2, amount);
                if (utilities[i][0] != null &&
                    utilities[i][0].StartsWith(yearPrefix, StringComparison.Ordinal))
                    yearUtility += amount;
            }

            StringBuilder result = new StringBuilder();
            CarPassport passport = FindPassport(currentCar);
            result.Append(currentCar).Append(FormatPassportSuffix(passport));
            if (passport.Vin.Length > 0 || passport.TankLiters.Length > 0 || passport.Note.Length > 0)
            {
                result.Append("\r\n");
                if (passport.Vin.Length > 0)
                    result.Append("VIN: ").Append(passport.Vin);
                if (passport.TankLiters.Length > 0)
                {
                    if (passport.Vin.Length > 0)
                        result.Append(" · ");
                    result.Append((L10n.Get("label.tank_short") + " ")).Append(passport.TankLiters).Append((" " + L10n.Get("unit.liters_short")));
                }
                if (passport.Note.Length > 0)
                    result.Append("\r\n").Append(passport.Note);
            }
            result.Append(("\r\n\r\n" + L10n.Get("summary.fuel_section_colon") + " ")).Append(Money(fuelTotal))

                .Append(" " + Cur() + ("\r\n" + L10n.Get("summary.repairs_section_colon") + " ")).Append(Money(repairTotal)).Append(" " + Cur() + ("\r\n" + L10n.Get("summary.expenses_section_colon") + " ")).Append(Money(expenseTotal)).Append(" " + Cur() + "\r\n");
            if (anyPurpose)
            {
                result.Append(("\r\n" + L10n.Get("report.fuel_by_purpose_personal") + " ")).Append(Money(purposePersonal))
                    .Append(" " + Cur() + ("\r\n" + L10n.Get("purpose.work_colon") + " ")).Append(Money(purposeWork)).Append(" " + Cur());
                if (purposeOther > 0)
                    result.Append(("\r\n" + L10n.Get("report.other_colon") + " ")).Append(Money(purposeOther)).Append(" " + Cur());
                result.Append("\r\n");
            }
            if (stationStats.Count > 0)
            {
                result.Append(("\r\n" + L10n.Get("label.station_colon")));
                List<string> stationNames = new List<string>(stationStats.Keys);
                stationNames.Sort(StringComparer.OrdinalIgnoreCase);
                for (int s = 0; s < stationNames.Count; s++)
                {
                    decimal[] bucket = stationStats[stationNames[s]];
                    int count = (int)bucket[0];
                    decimal avg = count > 0 ? bucket[1] / count : 0;
                    result.Append("\r\n").Append(stationNames[s])
                        .Append(": ").Append(count.ToString(CultureInfo.InvariantCulture))
                        .Append((" " + L10n.Get("summary.dot_avg") + " ")).Append(Money(avg)).Append(" " + Cur()).Append(L10n.Get("unit.per_liter"));
                }
                result.Append("\r\n");
            }

            decimal minimumMileage = Decimal.MaxValue;
            decimal maximumMileage = 0;
            decimal currentMileage = 0;
            decimal lastOilMileage = 0;
            decimal lastTimingMileage = 0;
            string lastOilDate = "";
            string lastTimingDate = "";
            for (int i = 0; i < fuels.Count; i++)
                if (fuels[i].Length > 2 && fuels[i][1] == currentCar)
                    AddMileageReading(fuels[i][2], ref currentMileage,
                        ref minimumMileage, ref maximumMileage);
            for (int i = 0; i < repairs.Count; i++)
            {
                string[] row = repairs[i];
                if (row.Length < 4 || row[1] != currentCar)
                    continue;
                decimal mileage;
                if (KiloLedgerCodec.TryNumber(row[2], out mileage) && mileage >= 0)
                    AddMileageReading(row[2], ref currentMileage,
                        ref minimumMileage, ref maximumMileage);
                if (!KiloLedgerCodec.TryNumber(row[2], out mileage) || mileage < 0)
                    continue;
                if (row[3] == "ТО Масло" &&
                    (mileage > lastOilMileage ||
                    (mileage == lastOilMileage && String.CompareOrdinal(row[0], lastOilDate) > 0)))
                {
                    lastOilMileage = mileage;
                    lastOilDate = row[0];
                }
                if (row[3] == "ТО ГРМ" &&
                    (mileage > lastTimingMileage ||
                    (mileage == lastTimingMileage && String.CompareOrdinal(row[0], lastTimingDate) > 0)))
                {
                    lastTimingMileage = mileage;
                    lastTimingDate = row[0];
                }
            }
            result.Append((L10n.Get("summary.counted_cost_per_km") + " "));
            if (minimumMileage != Decimal.MaxValue && maximumMileage > minimumMileage)
                result.Append(Money((fuelTotal + repairTotal + expenseTotal) /
                    (maximumMileage - minimumMileage))).Append(" " + Cur() + (L10n.Get("unit.per_km") + "\r\n"));
            else
                result.Append((L10n.Get("forecast.insufficient_mileage") + "\r\n"));

            result.Append(("\r\n" + L10n.Get("report.repairs_by_cat_colon") + "\r\n"));
            foreach (KeyValuePair<string, decimal> pair in byCategory)
                result.Append(pair.Key).Append(": ").Append(Money(pair.Value)).Append(" " + Cur() + "\r\n");
            result.Append(("\r\n" + L10n.Get("summary.utilities_all_time") + "\r\n")).Append(Money(utilityTotal))
                .Append(" " + Cur());
            string monthKey = DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            decimal monthFuel = 0, monthRepair = 0, monthExpense = 0, monthUtility = 0;
            decimal[] monthBucket;
            if (monthlyTotals.TryGetValue(monthKey, out monthBucket))
            {
                monthFuel = monthBucket[0];
                monthRepair = monthBucket[1];
                monthUtility = monthBucket[2];
                monthExpense = monthBucket.Length > 3 ? monthBucket[3] : 0;
            }
            AppendBudgetBlock(result, monthKey, monthFuel + monthRepair + monthExpense, monthUtility);
            result.Append(("\r\n\r\n" + L10n.Get("summary.year_totals") + " ")).Append(yearPrefix).Append(":\r\n")
                .Append((L10n.Get("label.fuel_colon") + " ")).Append(Money(yearFuel)).Append(" " + Cur() + "\r\n")
                .Append((L10n.Get("label.repairs_colon") + " ")).Append(Money(yearRepair)).Append(" " + Cur() + "\r\n")
                .Append((L10n.Get("label.expenses_colon") + " ")).Append(Money(yearExpense)).Append(" " + Cur() + "\r\n")
                .Append((L10n.Get("label.utilities_colon") + " ")).Append(Money(yearUtility)).Append(" " + Cur() + "\r\n")
                .Append((L10n.Get("report.total_colon") + " ")).Append(Money(yearFuel + yearRepair + yearExpense + yearUtility))
                .Append(" " + Cur() + ("\r\n\r\n" + L10n.Get("summary.monthly_expenses_12") + "\r\n"));
            List<string> months = new List<string>(monthlyTotals.Keys);
            months.Sort(StringComparer.Ordinal);
            int firstMonth = Math.Max(0, months.Count - 12);
            for (int i = firstMonth; i < months.Count; i++)
            {
                decimal[] totals = monthlyTotals[months[i]];
                result.Append(months[i]).Append((L10n.Get("summary.colon_fuel") + " ")).Append(Money(totals[0]))
                    .Append((L10n.Get("summary.comma_repairs") + " ")).Append(Money(totals[1])).Append((L10n.Get("summary.comma_utilities") + " ")).Append(Money(totals[2])).Append((L10n.Get("summary.comma_expenses") + " "))
                    .Append(Money(totals.Length > 3 ? totals[3] : 0)).Append(" " + Cur() + "\r\n");
            }
            result.Append("\r\n").Append(CarCompare.FormatBlock(
                CarCompare.Build(cars, fuels, repairs, expenses), Cur(), Money)).Append("\r\n");
            result.Append(("\r\n" + L10n.Get("forecast.current_odometer_colon") + " ")).Append(Money(currentMileage)).Append((" " + L10n.Get("unit.km") + "\r\n"));
            ToIntervals intervals = ToSettings.FromRows(repository.Settings);
            result.Append(ServiceStatus(L10n.Get("service.oil_change"), currentMileage, lastOilMileage,
                lastOilDate, intervals.OilInterval, intervals.OilWarn)).Append("\r\n");
            result.Append(ServiceStatus(L10n.Get("service.timing_belt"), currentMileage, lastTimingMileage,
                lastTimingDate, intervals.BeltInterval, intervals.BeltWarn));
            result.Append(ForecastInsights.FormatBlock(
                currentCar, fuels, repairs, currentMileage,
                lastOilMileage, lastTimingMileage,
                intervals.OilInterval, intervals.BeltInterval,
                DateTime.Today, "\r\n"));
            int warnDays = ToSettings.GetReminderWarnDays(repository.Settings);
            result.Append(DateReminders.FormatUpcomingBlock(
                repository.RemindersRows, warnDays, DateTime.Today));
            result.Append(CarNotes.FormatRecentBlock(repository.NotesRows, currentCar, 3));
            return result.ToString();
        }

        private void AppendBudgetBlock(StringBuilder result, string monthKey,
            decimal spent, decimal utilitiesMonth)
        {
            decimal budget = ToSettings.GetBudgetMonth(repository.Settings);
            if (budget <= 0)
                return;
            decimal warnPct = ToSettings.GetBudgetWarnPct(repository.Settings);
            decimal pct = budget > 0 ? (spent * 100m) / budget : 0;
            result.Append(("\r\n\r\n" + L10n.Get("label.monthly_budget") + " ")).Append(monthKey).Append(":\r\n")
                .Append((L10n.Get("summary.spent_total_colon") + " "))
                .Append(Money(spent)).Append(" / ").Append(Money(budget)).Append(" ").Append(Cur())
                .Append(" (").Append(Money(pct)).Append("%)\r\n")
                .Append((L10n.Get("summary.utilities_this_month") + " "))
                .Append(Money(utilitiesMonth)).Append(" ").Append(Cur());
            if (spent >= budget)
                result.Append(("\r\n" + L10n.Get("summary.budget_exceeded_warn")));
            else if (pct >= warnPct)
                result.Append(("\r\n" + L10n.Get("summary.budget_warn_symbol"))).Append(Money(warnPct)).Append(L10n.Get("summary.pct_of_budget"));
        }

        private static bool TryParseYearMonth(string value, out DateTime month)
        {
            month = DateTime.MinValue;
            if (value == null || value.Length < 7 || value[4] != '-')
                return false;
            try
            {
                int year = int.Parse(value.Substring(0, 4), NumberStyles.None, CultureInfo.InvariantCulture);
                int monthNumber = int.Parse(value.Substring(5, 2), NumberStyles.None, CultureInfo.InvariantCulture);
                if (year < 1 || monthNumber < 1 || monthNumber > 12)
                    return false;
                month = new DateTime(year, monthNumber, 1);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static void AddMonthlyAmount(Dictionary<string, decimal[]> totals,
            string date, int category, decimal amount)
        {
            DateTime month;
            if (date == null || date.Length < 7 || !TryParseYearMonth(date.Substring(0, 7), out month))
                return;

            string key = month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            decimal[] monthly;
            if (!totals.TryGetValue(key, out monthly))
            {
                monthly = new decimal[4];
                totals.Add(key, monthly);
            }
            monthly[category] += amount;
        }

        private static void AddMileageReading(string value, ref decimal currentMileage,
            ref decimal minimumMileage, ref decimal maximumMileage)
        {
            decimal mileage;
            if (!KiloLedgerCodec.TryNumber(value, out mileage) || mileage < 0)
                return;
            if (mileage > currentMileage)
                currentMileage = mileage;
            if (mileage < minimumMileage)
                minimumMileage = mileage;
            if (mileage > maximumMileage)
                maximumMileage = mileage;
        }

        private static string ServiceStatus(string name, decimal currentMileage,
            decimal lastMileage, string lastDate, decimal interval, decimal warningDistance)
        {
            if (lastMileage == 0)
                return name + L10n.Get("forecast.no_replacement");
            string lastService = (L10n.Get("summary.last_record") + " ") + lastDate + ", " +
                Money(lastMileage) + (" " + L10n.Get("unit.km_semi") + " ");
            decimal since = currentMileage - lastMileage;
            if (since >= interval)
                return name + ": " + lastService + L10n.Get("forecast.replacement_needed");
            if (since >= interval - warningDistance)
                return name + ": " + lastService + L10n.Get("forecast.due_soon_open") +
                    Money(interval - since) + (" " + L10n.Get("unit.km_close"));
            return name + ": " + lastService + L10n.Get("forecast.ok_open") +
                Money(interval - since) + (" " + L10n.Get("unit.km_close"));
        }

        private static decimal UtilityTotal(string[] row)
        {
            decimal total = 0;
            for (int i = 1; i < row.Length && i < 8; i++)
                total += NumberOrZero(row[i]);
            return total;
        }

        private static decimal NumberOrZero(string value)
        {
            decimal number;
            return KiloLedgerCodec.TryNumber(value, out number) ? number : 0;
        }

        private static string Money(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static List<string[]> ReadGrid(DataGridView grid, int columns)
        {
            List<string[]> rows = new List<string[]>();
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                DataGridViewRow row = grid.Rows[i];
                if (row.IsNewRow)
                    continue;
                string[] values = new string[columns];
                bool empty = true;
                for (int c = 0; c < columns; c++)
                {
                    object cell = row.Cells[c].Value;
                    values[c] = cell == null ? "" : Convert.ToString(cell);
                    if (!String.IsNullOrEmpty(values[c].Trim()))
                        empty = false;
                }
                if (empty)
                    continue;
                string id = row.Tag as string;
                if (!String.IsNullOrEmpty(id) && RecordIds.LooksLikeId(id))
                    values = RecordIds.WithId(values, id);
                rows.Add(values);
            }
            return rows;
        }

        private void RefreshCategoryFilterLists()
        {
            FillCategoryCombo(repairFilterCategory, repository.Categories, repairFilterCategoryValue);
            FillCategoryCombo(expenseFilterCategory, repository.ExpenseCategories, expenseFilterCategoryValue);
        }

        private static void FillCategoryCombo(ComboBox combo, List<string[]> categories, string selected)
        {
            string previous = selected;
            if (String.IsNullOrEmpty(previous) && combo.SelectedItem != null)
                previous = combo.SelectedItem.ToString();
            combo.Items.Clear();
            combo.Items.Add("Все категории");
            for (int i = 0; i < categories.Count; i++)
            {
                string[] content = RecordIds.StripId(categories[i]);
                if (content.Length == 0)
                    continue;
                string name = (content[0] ?? "").Trim();
                if (name.Length > 0)
                    combo.Items.Add(name);
            }
            int index = 0;
            if (!String.IsNullOrEmpty(previous))
            {
                for (int i = 0; i < combo.Items.Count; i++)
                {
                    if (String.Equals(combo.Items[i].ToString(), previous, StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        break;
                    }
                }
            }
            if (combo.Items.Count > 0)
                combo.SelectedIndex = index;
        }

        private void ApplyAllFilterVisibility()
        {
            ApplyFuelFilterVisibility();
            ApplyRepairFilterVisibility();
            ApplyExpenseFilterVisibility();
            ApplyUtilityFilterVisibility();
        }

        private void ApplyFuelFilterFromUi()
        {
            fuelFilterFromValue = (fuelFilterFrom.Text ?? "").Trim();
            fuelFilterToValue = (fuelFilterTo.Text ?? "").Trim();
            fuelFilterTextValue = (fuelFilterText.Text ?? "").Trim();
            string purpose = fuelFilterPurpose.SelectedItem == null
                ? "" : fuelFilterPurpose.SelectedItem.ToString();
            fuelFilterPurposeValue = String.Equals(purpose, "Все цели", StringComparison.OrdinalIgnoreCase)
                ? "" : (purpose ?? "").Trim();
            string station = fuelFilterStation.SelectedItem == null
                ? "" : fuelFilterStation.SelectedItem.ToString();
            fuelFilterStationValue = String.Equals(station, "Все АЗС", StringComparison.OrdinalIgnoreCase)
                ? "" : (station ?? "").Trim();
            ApplyFuelFilterVisibility();
            SetStatus(L10n.Get("status.fuel_filter_applied"));
        }

        private void ResetFuelFilter()
        {
            fuelFilterFromValue = fuelFilterToValue = fuelFilterTextValue =
                fuelFilterPurposeValue = fuelFilterStationValue = "";
            fuelFilterFrom.Text = fuelFilterTo.Text = fuelFilterText.Text = "";
            if (fuelFilterPurpose.Items.Count > 0)
                fuelFilterPurpose.SelectedIndex = 0;
            if (fuelFilterStation.Items.Count > 0)
                fuelFilterStation.SelectedIndex = 0;
            ApplyFuelFilterVisibility();
            SetStatus(L10n.Get("status.fuel_filter_reset"));
        }

        private void ApplyRepairFilterFromUi()
        {
            repairFilterFromValue = (repairFilterFrom.Text ?? "").Trim();
            repairFilterToValue = (repairFilterTo.Text ?? "").Trim();
            repairFilterTextValue = (repairFilterText.Text ?? "").Trim();
            repairFilterCategoryValue = repairFilterCategory.SelectedItem == null
                ? "" : repairFilterCategory.SelectedItem.ToString();
            ApplyRepairFilterVisibility();
            SetStatus(L10n.Get("status.repairs_filter_applied"));
        }

        private void ResetRepairFilter()
        {
            repairFilterFromValue = repairFilterToValue = repairFilterTextValue = "";
            repairFilterCategoryValue = "Все категории";
            repairFilterFrom.Text = repairFilterTo.Text = repairFilterText.Text = "";
            if (repairFilterCategory.Items.Count > 0)
                repairFilterCategory.SelectedIndex = 0;
            ApplyRepairFilterVisibility();
            SetStatus(L10n.Get("status.repairs_filter_reset"));
        }

        private void ApplyExpenseFilterFromUi()
        {
            expenseFilterFromValue = (expenseFilterFrom.Text ?? "").Trim();
            expenseFilterToValue = (expenseFilterTo.Text ?? "").Trim();
            expenseFilterTextValue = (expenseFilterText.Text ?? "").Trim();
            expenseFilterCategoryValue = expenseFilterCategory.SelectedItem == null
                ? "" : expenseFilterCategory.SelectedItem.ToString();
            ApplyExpenseFilterVisibility();
            SetStatus(L10n.Get("status.expenses_filter_applied"));
        }

        private void ResetExpenseFilter()
        {
            expenseFilterFromValue = expenseFilterToValue = expenseFilterTextValue = "";
            expenseFilterCategoryValue = "Все категории";
            expenseFilterFrom.Text = expenseFilterTo.Text = expenseFilterText.Text = "";
            if (expenseFilterCategory.Items.Count > 0)
                expenseFilterCategory.SelectedIndex = 0;
            ApplyExpenseFilterVisibility();
            SetStatus(L10n.Get("status.expenses_filter_reset"));
        }

        private void ApplyUtilityFilterFromUi()
        {
            utilityFilterFromValue = (utilityFilterFrom.Text ?? "").Trim();
            utilityFilterToValue = (utilityFilterTo.Text ?? "").Trim();
            utilityFilterTextValue = (utilityFilterText.Text ?? "").Trim();
            ApplyUtilityFilterVisibility();
            SetStatus(L10n.Get("status.utilities_filter_applied"));
        }

        private void ResetUtilityFilter()
        {
            utilityFilterFromValue = utilityFilterToValue = utilityFilterTextValue = "";
            utilityFilterFrom.Text = utilityFilterTo.Text = utilityFilterText.Text = "";
            ApplyUtilityFilterVisibility();
            SetStatus(L10n.Get("status.utilities_filter_reset"));
        }

        private void ApplyFuelFilterVisibility()
        {
            ApplyRowVisibility(fuelGrid, delegate(DataGridViewRow row)
            {
                string date = CellText(row, 0);
                if (fuelFilterFromValue.Length > 0 && String.CompareOrdinal(date, fuelFilterFromValue) < 0)
                    return false;
                if (fuelFilterToValue.Length > 0 && String.CompareOrdinal(date, fuelFilterToValue) > 0)
                    return false;
                if (fuelFilterPurposeValue.Length > 0 &&
                    !String.Equals(CellText(row, 8), fuelFilterPurposeValue, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (fuelFilterStationValue.Length > 0 &&
                    !String.Equals(CellText(row, 9), fuelFilterStationValue, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (fuelFilterTextValue.Length > 0)
                {
                    string hay = (CellText(row, 2) + " " + CellText(row, 3) + " " + CellText(row, 4) +
                        " " + CellText(row, 5) + " " + CellText(row, 6) + " " + CellText(row, 8) +
                        " " + CellText(row, 9))
                        .ToLowerInvariant();
                    if (hay.IndexOf(fuelFilterTextValue.ToLowerInvariant(), StringComparison.Ordinal) < 0)
                        return false;
                }
                return true;
            });
        }

        private void ApplyRepairFilterVisibility()
        {
            ApplyRowVisibility(repairsGrid, delegate(DataGridViewRow row)
            {
                string date = CellText(row, 0);
                if (repairFilterFromValue.Length > 0 && String.CompareOrdinal(date, repairFilterFromValue) < 0)
                    return false;
                if (repairFilterToValue.Length > 0 && String.CompareOrdinal(date, repairFilterToValue) > 0)
                    return false;
                if (repairFilterCategoryValue.Length > 0 &&
                    !String.Equals(repairFilterCategoryValue, "Все категории", StringComparison.OrdinalIgnoreCase) &&
                    !String.Equals(CellText(row, 3), repairFilterCategoryValue, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (repairFilterTextValue.Length > 0)
                {
                    string hay = (CellText(row, 4) + " " + CellText(row, 5) + " " + CellText(row, 8))
                        .ToLowerInvariant();
                    if (hay.IndexOf(repairFilterTextValue.ToLowerInvariant(), StringComparison.Ordinal) < 0)
                        return false;
                }
                return true;
            });
        }

        private void ApplyExpenseFilterVisibility()
        {
            ApplyRowVisibility(expensesGrid, delegate(DataGridViewRow row)
            {
                string date = CellText(row, 0);
                if (expenseFilterFromValue.Length > 0 && String.CompareOrdinal(date, expenseFilterFromValue) < 0)
                    return false;
                if (expenseFilterToValue.Length > 0 && String.CompareOrdinal(date, expenseFilterToValue) > 0)
                    return false;
                if (expenseFilterCategoryValue.Length > 0 &&
                    !String.Equals(expenseFilterCategoryValue, "Все категории", StringComparison.OrdinalIgnoreCase) &&
                    !String.Equals(CellText(row, 2), expenseFilterCategoryValue, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (expenseFilterTextValue.Length > 0)
                {
                    string hay = (CellText(row, 3) + " " + CellText(row, 5)).ToLowerInvariant();
                    if (hay.IndexOf(expenseFilterTextValue.ToLowerInvariant(), StringComparison.Ordinal) < 0)
                        return false;
                }
                return true;
            });
        }

        private void ApplyUtilityFilterVisibility()
        {
            ApplyRowVisibility(utilitiesGrid, delegate(DataGridViewRow row)
            {
                string month = CellText(row, 0);
                if (utilityFilterFromValue.Length > 0 && String.CompareOrdinal(month, utilityFilterFromValue) < 0)
                    return false;
                if (utilityFilterToValue.Length > 0 && String.CompareOrdinal(month, utilityFilterToValue) > 0)
                    return false;
                if (utilityFilterTextValue.Length > 0)
                {
                    StringBuilder hay = new StringBuilder(month);
                    for (int c = 1; c < row.Cells.Count; c++)
                        hay.Append(' ').Append(CellText(row, c));
                    if (hay.ToString().ToLowerInvariant()
                            .IndexOf(utilityFilterTextValue.ToLowerInvariant(), StringComparison.Ordinal) < 0)
                        return false;
                }
                return true;
            });
        }

        private static void ApplyRowVisibility(DataGridView grid, Predicate<DataGridViewRow> matches)
        {
            // Сброс текущей ячейки: иначе Visible=false у активной строки кидает исключение.
            grid.CurrentCell = null;
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                DataGridViewRow row = grid.Rows[i];
                if (row.IsNewRow)
                {
                    row.Visible = true;
                    continue;
                }
                bool visible = matches(row);
                if (row.Visible != visible)
                    row.Visible = visible;
            }
        }

        private void SetStatus(string text)
        {
            string raw = text ?? "";
            if (raw.IndexOf('\n') >= 0 || raw.Length > 140)
            {
                lastReportTitle = "KiloLedger";
                lastDetailedReport = raw;
                reportLinkLabel.Text = L10n.Get("button.last_report");
                reportLinkLabel.Visible = true;
            }
            string compact = raw.Replace("\r\n", " · ").Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (compact.Length > 140)
                compact = compact.Substring(0, 137) + "…";
            statusLabel.Text = compact;
        }

        private void SetStatusWithReport(string shortText, string reportTitle, string fullReport)
        {
            lastReportTitle = String.IsNullOrEmpty(reportTitle) ? "KiloLedger" : reportTitle;
            lastDetailedReport = fullReport ?? "";
            reportLinkLabel.Text = L10n.Get("button.last_report");
            reportLinkLabel.Visible = lastDetailedReport.Length > 0;
            SetStatus(shortText);
        }

        private void ShowLastReportDialog()
        {
            if (String.IsNullOrEmpty(lastDetailedReport))
                return;
            using (Form dlg = new Form())
            {
                dlg.Text = lastReportTitle;
                dlg.Width = 640;
                dlg.Height = 460;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.BackColor = Color.FromArgb(244, 246, 249);
                TextBox box = new TextBox
                {
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Dock = DockStyle.Fill,
                    Font = new Font("Consolas", 9.5F),
                    BackColor = Color.White,
                    Text = lastDetailedReport
                };
                Panel bottom = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(8) };
                Button close = new Button { Text = L10n.Get("button.close"), Width = 100, Dock = DockStyle.Right, DialogResult = DialogResult.OK };
                bottom.Controls.Add(close);
                dlg.AcceptButton = close;
                dlg.Controls.Add(box);
                dlg.Controls.Add(bottom);
                dlg.ShowDialog(this);
            }
        }

        private void ShowError(Exception ex)
        {
            MessageBox.Show(this, ex.Message, "KiloLedger Desktop", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus((L10n.Get("label.error_colon") + " ") + ex.Message);
        }

        private void MileageCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (suppressingMileagePrompt || e.RowIndex < 0 || e.ColumnIndex != 2)
                return;
            DataGridView grid = sender as DataGridView;
            if (grid == null)
                return;
            DataGridViewRow row = grid.Rows[e.RowIndex];
            if (row.IsNewRow)
                return;
            string car = CellText(row, 1);
            string kmText = CellText(row, 2);
            if (Object.ReferenceEquals(grid, repairsGrid) && kmText.Length == 0)
                return;
            decimal newKm;
            if (!KiloLedgerCodec.TryNumber(kmText, out newKm) || newKm < 0)
                return;
            string fileName = Object.ReferenceEquals(grid, fuelGrid) ? "fuel.dat" : "repairs.dat";
            decimal last = LastKnownMileageFromGrids(car, grid, e.RowIndex);
            string warning = FormatMileageWarning(newKm, last);
            if (warning == null)
                return;
            if (MessageBox.Show(this, warning, L10n.Get("label.odometer"), MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                return;
            suppressingMileagePrompt = true;
            try
            {
                // Revert to last known when user declines (or clear if none).
                row.Cells[2].Value = last >= 0
                    ? last.ToString("0.##", CultureInfo.InvariantCulture)
                    : "";
            }
            finally
            {
                suppressingMileagePrompt = false;
            }
        }

        private bool ConfirmAllMileageWarnings()
        {
            if (!ConfirmGridMileage(fuelGrid, "fuel.dat"))
                return false;
            if (!ConfirmGridMileage(repairsGrid, "repairs.dat"))
                return false;
            return true;
        }

        private bool ConfirmGridMileage(DataGridView grid, string fileName)
        {
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                DataGridViewRow row = grid.Rows[i];
                if (row.IsNewRow)
                    continue;
                string car = CellText(row, 1);
                string kmText = CellText(row, 2);
                if (fileName == "repairs.dat" && kmText.Length == 0)
                    continue;
                decimal newKm;
                if (car.Length == 0 || !KiloLedgerCodec.TryNumber(kmText, out newKm) || newKm < 0)
                    continue;
                decimal lastExcluding = LastKnownMileageFromGrids(car, grid, i);
                string warning = FormatMileageWarning(newKm, lastExcluding);
                if (warning == null)
                    continue;
                string context = fileName == "fuel.dat" ? "Заправка" : L10n.Get("tab.repairs");
                string message = context + " " + CellText(row, 0) + " / " + car + ":\r\n" + warning;
                if (MessageBox.Show(this, message, L10n.Get("label.odometer"), MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return false;
            }
            return true;
        }

        private decimal LastKnownMileageFromGrids(string car, DataGridView excludeGrid, int excludeRow)
        {
            decimal best = -1;
            ScanMileageGrid(fuelGrid, car, Object.ReferenceEquals(excludeGrid, fuelGrid) ? excludeRow : -1, ref best);
            ScanMileageGrid(repairsGrid, car, Object.ReferenceEquals(excludeGrid, repairsGrid) ? excludeRow : -1, ref best);
            return best;
        }

        private static void ScanMileageGrid(DataGridView grid, string car, int excludeRow, ref decimal best)
        {
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                if (i == excludeRow)
                    continue;
                DataGridViewRow row = grid.Rows[i];
                if (row.IsNewRow)
                    continue;
                if (!String.Equals(CellText(row, 1), car, StringComparison.OrdinalIgnoreCase))
                    continue;
                decimal km;
                if (!KiloLedgerCodec.TryNumber(CellText(row, 2), out km) || km < 0)
                    continue;
                if (km >= best)
                    best = km;
            }
        }

        private static string FormatMileageWarning(decimal newKm, decimal lastKnown)
        {
            if (lastKnown < 0)
                return null;
            string x = lastKnown.ToString("0.##", CultureInfo.InvariantCulture);
            if (newKm < lastKnown)
                return L10n.Get("warn.odometer_lower_open") + x + L10n.Get("confirm.save_anyway_suffix");
            if (newKm - lastKnown > 5000m)
                return L10n.Get("warn.odometer_jump_open") + x +
                    L10n.Get("confirm.save_anyway_suffix");
            return null;
        }

        private void GlobalSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                OpenGlobalSearch(sender, e);
            }
        }

        private void SaveSummaryText(object sender, EventArgs e)
        {
            try
            {
                if (!String.IsNullOrEmpty(repository.DirectoryPath) ||
                    carsGrid.Rows.Count > 1 || fuelGrid.Rows.Count > 1)
                    PullAll();
                RefreshSummaryText();
            }
            catch (Exception ex)
            {
                ShowError(ex);
                return;
            }
            string text = summaryBox.Text ?? "";
            if (text.Length == 0 || text.IndexOf("Откройте папку", StringComparison.Ordinal) >= 0)
            {
                MessageBox.Show(this, L10n.Get("error.open_folder_first"), L10n.Get("tab.summary"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = L10n.Get("filter.txt_files");
                dialog.FileName = "Summary_" +
                    DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".txt";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    File.WriteAllText(dialog.FileName, text, Encoding.UTF8);
                    SetStatus((L10n.Get("msg.summary_saved_colon") + " ") + dialog.FileName);
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        }

        private void OpenDataIntegrity(object sender, EventArgs e)
        {
            try
            {
                if (String.IsNullOrEmpty(repository.DirectoryPath))
                {
                    MessageBox.Show(this, L10n.Get("error.open_folder_first"), L10n.Get("button.data_integrity"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                PullAll();
                DataIntegrity.Report report = DataIntegrity.Check(repository);
                string text = DataIntegrity.Format(report);
                using (Form dialog = new Form())
                {
                    dialog.Text = L10n.Get("button.data_integrity");
                    dialog.Width = 640;
                    dialog.Height = 480;
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.MinimizeBox = false;
                    dialog.MaximizeBox = true;
                    TextBox box = new TextBox
                    {
                        Multiline = true,
                        ReadOnly = true,
                        ScrollBars = ScrollBars.Vertical,
                        Dock = DockStyle.Fill,
                        Font = new Font("Consolas", 10F),
                        Text = text
                    };
                    FlowLayoutPanel buttons = new FlowLayoutPanel
                    {
                        Dock = DockStyle.Bottom,
                        Height = 40,
                        FlowDirection = FlowDirection.RightToLeft
                    };
                    Button close = new Button { Text = L10n.Get("button.close"), Width = 100 };
                    close.Click += delegate { dialog.Close(); };
                    buttons.Controls.Add(close);
                    if (report.CanSimpleFix)
                    {
                        Button fix = new Button { Text = L10n.Get("button.fix_simple"), Width = 140 };
                        fix.Click += delegate
                        {
                            try
                            {
                                int n = DataIntegrity.ApplySimpleFixes(repository, report);
                                BindAll();
                                MessageBox.Show(dialog,
                                    n > 0 ? (L10n.Get("msg.categories_added") + " ") + n + "." : L10n.Get("integrity.nothing_to_fix"),
                                    L10n.Get("button.data_integrity"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                                dialog.Close();
                            }
                            catch (Exception ex)
                            {
                                ShowError(ex);
                            }
                        };
                        buttons.Controls.Add(fix);
                    }
                    dialog.Controls.Add(box);
                    dialog.Controls.Add(buttons);
                    dialog.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void OpenDuplicates(object sender, EventArgs e)
        {
            try
            {
                if (!String.IsNullOrEmpty(repository.DirectoryPath))
                    PullAll();
            }
            catch (Exception ex)
            {
                ShowError(ex);
                return;
            }

            List<DuplicateFinder.Group> groups = DuplicateFinder.FindAll(repository);
            if (groups.Count == 0)
            {
                MessageBox.Show(this, L10n.Get("msg.no_duplicates"), L10n.Get("button.duplicates"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (Form dialog = new Form())
            {
                dialog.Text = L10n.Get("button.duplicates");
                dialog.Width = 720;
                dialog.Height = 520;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowIcon = false;

                ListBox list = new ListBox { Left = 12, Top = 12, Width = 680, Height = 400 };
                Button deleteExtras = new Button
                {
                    Text = L10n.Get("button.delete_extra_in_group"),
                    Left = 12,
                    Top = 430,
                    Width = 200
                };
                Button close = new Button { Text = L10n.Get("button.close"), Left = 580, Top = 430, Width = 110 };
                dialog.Controls.Add(list);
                dialog.Controls.Add(deleteExtras);
                dialog.Controls.Add(close);

                List<DuplicateFinder.Group> rowGroup = new List<DuplicateFinder.Group>();
                for (int g = 0; g < groups.Count; g++)
                {
                    DuplicateFinder.Group group = groups[g];
                    list.Items.Add("--- " + group.Title + " · " + group.Reason +
                        " (" + group.Indexes.Count + ") ---");
                    rowGroup.Add(null);
                    for (int i = 0; i < group.Rows.Count; i++)
                    {
                        list.Items.Add((i == 0 ? "  ✓ " : "  · ") +
                            DuplicateFinder.FormatRow(group.Rows[i]));
                        rowGroup.Add(group);
                    }
                }

                deleteExtras.Click += delegate
                {
                    int selected = list.SelectedIndex;
                    if (selected < 0 || selected >= rowGroup.Count || rowGroup[selected] == null)
                    {
                        MessageBox.Show(dialog, L10n.Get("error.select_record_in_group"), L10n.Get("button.duplicates"),
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    DuplicateFinder.Group group = rowGroup[selected];
                    if (MessageBox.Show(dialog,
                        (L10n.Get("button.delete") + " ") + (group.Indexes.Count - 1) +
                        (" " + L10n.Get("dup.confirm_delete_extra_suffix")),
                        L10n.Get("button.duplicates"), MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                        return;
                    try
                    {
                        DeleteDuplicateExtras(group);
                        BindAll();
                        MarkDirty();
                        dialog.DialogResult = DialogResult.OK;
                        dialog.Close();
                        SetStatus(L10n.Get("msg.extra_dups_deleted_open") + group.Title + ").");
                    }
                    catch (Exception ex)
                    {
                        ShowError(ex);
                    }
                };
                close.Click += delegate { dialog.Close(); };
                dialog.CancelButton = close;
                dialog.ShowDialog(this);
            }
        }

        private void DeleteDuplicateExtras(DuplicateFinder.Group group)
        {
            List<string[]> table = TableForFile(group.FileName);
            if (table == null)
                return;
            List<int> toDelete = new List<int>();
            for (int i = 1; i < group.Indexes.Count; i++)
                toDelete.Add(group.Indexes[i]);
            toDelete.Sort();
            for (int i = toDelete.Count - 1; i >= 0; i--)
            {
                int index = toDelete[i];
                if (index < 0 || index >= table.Count)
                    continue;
                string id = RecordIds.GetId(table[index]);
                if (!String.IsNullOrEmpty(id))
                    repository.NoteDeleted(group.FileName, id);
                table.RemoveAt(index);
            }
        }

        private List<string[]> TableForFile(string fileName)
        {
            if (fileName == "fuel.dat")
                return repository.Fuel;
            if (fileName == "repairs.dat")
                return repository.Repairs;
            if (fileName == KiloLedgerCodec.ExpensesFile)
                return repository.Expenses;
            if (fileName == "utilities.dat")
                return repository.Utilities;
            return null;
        }

        private void OpenGlobalSearch(object sender, EventArgs e)
        {
            string preset = globalSearchBox.ForeColor == Color.Gray ? "" : (globalSearchBox.Text ?? "").Trim();
            using (Form dialog = new Form())
            {
                dialog.Text = L10n.Get("title.search_records");
                dialog.Width = 640;
                dialog.Height = 480;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowIcon = false;

                TextBox query = new TextBox { Left = 12, Top = 12, Width = 480 };
                query.Text = preset;
                Button find = new Button { Text = L10n.Get("button.search"), Left = 500, Top = 10, Width = 100 };
                ListBox list = new ListBox { Left = 12, Top = 48, Width = 588, Height = 340 };
                Button open = new Button { Text = L10n.Get("button.open"), Left = 400, Top = 400, Width = 100 };
                Button close = new Button { Text = L10n.Get("button.close"), Left = 510, Top = 400, Width = 90 };
                dialog.Controls.Add(query);
                dialog.Controls.Add(find);
                dialog.Controls.Add(list);
                dialog.Controls.Add(open);
                dialog.Controls.Add(close);

                List<DesktopSearchHit> hits = new List<DesktopSearchHit>();
                Action run = delegate
                {
                    hits.Clear();
                    list.Items.Clear();
                    string q = (query.Text ?? "").Trim();
                    if (q.Length == 0)
                        return;
                    hits.AddRange(FindDesktopHits(q));
                    for (int i = 0; i < hits.Count; i++)
                        list.Items.Add(hits[i].Label);
                    if (list.Items.Count == 0)
                        list.Items.Add(L10n.Get("search.nothing_found"));
                };
                find.Click += delegate { run(); };
                query.KeyDown += delegate(object s, KeyEventArgs ke)
                {
                    if (ke.KeyCode == Keys.Enter)
                    {
                        ke.Handled = true;
                        ke.SuppressKeyPress = true;
                        run();
                    }
                };
                Action openSelected = delegate
                {
                    int index = list.SelectedIndex;
                    if (index < 0 || index >= hits.Count)
                        return;
                    DesktopSearchHit hit = hits[index];
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                    JumpToSearchHit(hit);
                };
                open.Click += delegate { openSelected(); };
                list.DoubleClick += delegate { openSelected(); };
                close.Click += delegate { dialog.Close(); };
                dialog.AcceptButton = find;
                dialog.CancelButton = close;
                if (preset.Length > 0)
                    run();
                dialog.ShowDialog(this);
            }
        }

        private sealed class DesktopSearchHit
        {
            public string Tab;
            public string Label;
            public int RowIndex;
            public string Car;
        }

        private List<DesktopSearchHit> FindDesktopHits(string query)
        {
            string q = query.ToLowerInvariant();
            List<DesktopSearchHit> hits = new List<DesktopSearchHit>();
            CollectDesktopHits(hits, L10n.Get("tab.fuel"), fuelGrid, q, delegate(DataGridViewRow row)
            {
                return CellText(row, 6) + " " + CellText(row, 8) + " " + CellText(row, 1);
            });
            CollectDesktopHits(hits, L10n.Get("tab.repairs"), repairsGrid, q, delegate(DataGridViewRow row)
            {
                return CellText(row, 3) + " " + CellText(row, 4) + " " + CellText(row, 5) + " " +
                    CellText(row, 8) + " " + CellText(row, 1);
            });
            CollectDesktopHits(hits, L10n.Get("tab.expenses"), expensesGrid, q, delegate(DataGridViewRow row)
            {
                return CellText(row, 2) + " " + CellText(row, 3) + " " + CellText(row, 5) + " " +
                    CellText(row, 1);
            });
            CollectDesktopHits(hits, L10n.Get("tab.utilities"), utilitiesGrid, q, delegate(DataGridViewRow row)
            {
                StringBuilder sb = new StringBuilder(CellText(row, 0));
                for (int c = 1; c < row.Cells.Count; c++)
                    sb.Append(' ').Append(CellText(row, c));
                return sb.ToString();
            });
            return hits;
        }

        private static void CollectDesktopHits(List<DesktopSearchHit> hits, string tab,
            DataGridView grid, string queryLower, Func<DataGridViewRow, string> haystack)
        {
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                DataGridViewRow row = grid.Rows[i];
                if (row.IsNewRow)
                    continue;
                string text = haystack(row) ?? "";
                if (text.ToLowerInvariant().IndexOf(queryLower, StringComparison.Ordinal) < 0)
                    continue;
                DesktopSearchHit hit = new DesktopSearchHit();
                hit.Tab = tab;
                hit.RowIndex = i;
                hit.Car = tab == L10n.Get("tab.utilities") ? "" : CellText(row, 1);
                string date = CellText(row, 0);
                string detail = text.Trim();
                if (detail.Length > 48)
                    detail = detail.Substring(0, 48) + "…";
                hit.Label = tab + " · " + date +
                    (hit.Car.Length > 0 ? " · " + hit.Car : "") + " — " + detail;
                hits.Add(hit);
            }
        }

        private void JumpToSearchHit(DesktopSearchHit hit)
        {
            for (int i = 0; i < tabs.TabPages.Count; i++)
            {
                if (tabs.TabPages[i].Text != hit.Tab)
                    continue;
                tabs.SelectedIndex = i;
                break;
            }
            DataGridView grid = hit.Tab == L10n.Get("tab.fuel") ? fuelGrid
                : hit.Tab == L10n.Get("tab.repairs") ? repairsGrid
                : hit.Tab == L10n.Get("tab.expenses") ? expensesGrid
                : hit.Tab == L10n.Get("tab.utilities") ? utilitiesGrid
                : null;
            if (grid == null || hit.RowIndex < 0 || hit.RowIndex >= grid.Rows.Count)
                return;
            DataGridViewRow row = grid.Rows[hit.RowIndex];
            if (!row.Visible)
                row.Visible = true;
            grid.ClearSelection();
            row.Selected = true;
            if (row.Cells.Count > 0 && row.Cells[0].Visible)
                grid.CurrentCell = row.Cells[0];
            try { grid.FirstDisplayedScrollingRowIndex = hit.RowIndex; } catch { }
            SetStatus((L10n.Get("search.found_colon") + " ") + hit.Label);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (dirty)
            {
                DialogResult result = MessageBox.Show(this,
                    L10n.Get("confirm.unsaved_exit"),
                    "KiloLedger Desktop",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (result != DialogResult.Yes)
                    e.Cancel = true;
            }
            base.OnFormClosing(e);
        }
    }
}
