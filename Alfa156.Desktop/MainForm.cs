using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Alfa156.Desktop.Data;
using Alfa156.Desktop.Exchange;

namespace Alfa156.Desktop
{
    internal sealed class MainForm : Form
    {
        private const string AppVersion = "1.17.0";

        private readonly AlfaRepository repository = new AlfaRepository();
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
        private readonly TabPage summaryTab = new TabPage("Сводка");
        private readonly TextBox oilIntervalBox = new TextBox();
        private readonly TextBox oilWarnBox = new TextBox();
        private readonly TextBox beltIntervalBox = new TextBox();
        private readonly TextBox beltWarnBox = new TextBox();
        private readonly StatusStrip statusStrip = new StatusStrip();
        private readonly ToolStripStatusLabel statusLabel = new ToolStripStatusLabel("Откройте папку данных или ZIP с Android/КПК.");
        private readonly TextBox googleUrlBox = new TextBox();
        private bool suppressDirty;
        private bool dirty;
        private bool refreshingSummary;

        private readonly TextBox fuelFilterFrom = new TextBox();
        private readonly TextBox fuelFilterTo = new TextBox();
        private readonly TextBox fuelFilterText = new TextBox();
        private string fuelFilterFromValue = "";
        private string fuelFilterToValue = "";
        private string fuelFilterTextValue = "";

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
            Text = "Alfa 156 " + ResolveVersion();
            Width = 1100;
            Height = 720;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 560);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(245, 247, 250);
            ForeColor = Color.FromArgb(35, 51, 70);
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Alfa156.ico");
                if (!File.Exists(iconPath))
                    iconPath = Path.Combine(Application.StartupPath, "..", "..", "Alfa156.ico");
                if (File.Exists(iconPath))
                    Icon = new Icon(iconPath);
            }
            catch { }

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(8),
                BackColor = Color.FromArgb(245, 247, 250)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

            FlowLayoutPanel toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                AutoSize = false
            };
            pathBox.Width = 420;
            pathBox.ReadOnly = true;
            toolbar.Controls.Add(pathBox);
            toolbar.Controls.Add(MakeButton("Открыть папку", OpenFolder));
            toolbar.Controls.Add(MakeButton("Открыть ZIP", OpenZip));
            toolbar.Controls.Add(MakeButton("Новая папка", CreateFolder));
            toolbar.Controls.Add(MakeButton("Сохранить .dat", SaveData));
            toolbar.Controls.Add(MakeButton("Сохранить ZIP", SaveZip));
            toolbar.Controls.Add(MakeButton("Фото чека", AttachReceiptPhoto));
            globalSearchBox.Width = 140;
            globalSearchBox.Text = "Поиск…";
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
            toolbar.Controls.Add(globalSearchBox);
            toolbar.Controls.Add(MakeButton("Найти", OpenGlobalSearch));

            FlowLayoutPanel exchange = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = true
            };
            exchange.Controls.Add(MakeButton("Экспорт Excel", ExportExcel));
            exchange.Controls.Add(MakeButton("Импорт Excel", ImportExcel));
            exchange.Controls.Add(MakeButton("Экспорт CSV (Google)", ExportCsv));
            exchange.Controls.Add(MakeButton("Импорт CSV-папки", ImportCsvFolder));
            googleUrlBox.Width = 280;
            googleUrlBox.Text = "ссылка Google Sheets…";
            googleUrlBox.ForeColor = Color.Gray;
            googleUrlBox.GotFocus += delegate
            {
                if (googleUrlBox.ForeColor == Color.Gray)
                {
                    googleUrlBox.Text = "";
                    googleUrlBox.ForeColor = SystemColors.WindowText;
                }
            };
            exchange.Controls.Add(googleUrlBox);
            exchange.Controls.Add(MakeButton("Импорт Google", ImportGoogle));
            exchange.Controls.Add(MakeButton("Google: мастер", OpenGoogleSyncWizard));
            exchange.Controls.Add(MakeButton("Google: настройка", ConfigureGoogleSync));
            exchange.Controls.Add(MakeButton("Синхронизация Google", SyncGoogle));

            tabs.Dock = DockStyle.Fill;
            tabs.TabPages.Add(WrapGrid("Автомобили", carsGrid));
            tabs.TabPages.Add(WrapFilteredGrid("Заправки", fuelGrid, BuildFuelFilterBar()));
            tabs.TabPages.Add(WrapFilteredGrid("Ремонт", repairsGrid, BuildRepairFilterBar()));
            tabs.TabPages.Add(WrapFilteredGrid("Прочие", expensesGrid, BuildExpenseFilterBar()));
            tabs.TabPages.Add(WrapGrid("Кат. прочих", expenseCategoriesGrid));
            tabs.TabPages.Add(WrapFilteredGrid("Коммуналка", utilitiesGrid, BuildUtilityFilterBar()));
            tabs.TabPages.Add(WrapGrid("Категории", categoriesGrid));

            summaryBox.Multiline = true;
            summaryBox.ReadOnly = true;
            summaryBox.ScrollBars = ScrollBars.Vertical;
            summaryBox.Dock = DockStyle.Fill;
            summaryBox.Font = new Font("Consolas", 10F);
            summaryBox.BackColor = Color.White;
            summaryBox.ForeColor = Color.FromArgb(35, 51, 70);
            summaryBox.BorderStyle = BorderStyle.None;
            summaryBox.Text = "Откройте папку данных, чтобы увидеть сводку и напоминания о ТО.";
            TableLayoutPanel summaryLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            FlowLayoutPanel intervalRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
            intervalRow.Controls.Add(new Label { Text = "Масло интервал", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
            oilIntervalBox.Width = 70;
            intervalRow.Controls.Add(oilIntervalBox);
            intervalRow.Controls.Add(new Label { Text = "предупр.", AutoSize = true, Padding = new Padding(8, 8, 0, 0) });
            oilWarnBox.Width = 60;
            intervalRow.Controls.Add(oilWarnBox);
            intervalRow.Controls.Add(new Label { Text = "Ремень интервал", AutoSize = true, Padding = new Padding(12, 8, 0, 0) });
            beltIntervalBox.Width = 70;
            intervalRow.Controls.Add(beltIntervalBox);
            intervalRow.Controls.Add(new Label { Text = "предупр.", AutoSize = true, Padding = new Padding(8, 8, 0, 0) });
            beltWarnBox.Width = 60;
            intervalRow.Controls.Add(beltWarnBox);
            intervalRow.Controls.Add(MakeButton("Сохранить интервалы ТО", SaveToIntervals));
            summaryChart.Dock = DockStyle.Fill;
            summaryLayout.Controls.Add(intervalRow, 0, 0);
            summaryLayout.Controls.Add(summaryChart, 0, 1);
            summaryLayout.Controls.Add(summaryBox, 0, 2);
            summaryTab.Controls.Add(summaryLayout);
            tabs.TabPages.Add(summaryTab);
            tabs.SelectedIndexChanged += SummaryTabSelected;

            statusStrip.Items.Add(statusLabel);
            statusStrip.Dock = DockStyle.Fill;

            root.Controls.Add(toolbar, 0, 0);
            root.Controls.Add(exchange, 0, 1);
            root.Controls.Add(tabs, 0, 2);
            root.Controls.Add(statusStrip, 0, 3);
            Controls.Add(root);

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
            fuelGrid.CellEndEdit += MileageCellEndEdit;
            repairsGrid.CellEndEdit += MileageCellEndEdit;
            WireDirty(utilitiesGrid);
            WireDirty(categoriesGrid);
        }

        private static DataGridView CreateGrid()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None
            };
        }

        private static TabPage WrapGrid(string title, DataGridView grid)
        {
            TabPage page = new TabPage(title);
            page.Controls.Add(grid);
            return page;
        }

        private static TabPage WrapFilteredGrid(string title, DataGridView grid, Control filterBar)
        {
            TabPage page = new TabPage(title);
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            filterBar.Dock = DockStyle.Fill;
            layout.Controls.Add(filterBar, 0, 0);
            grid.Dock = DockStyle.Fill;
            layout.Controls.Add(grid, 0, 1);
            page.Controls.Add(layout);
            return page;
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
            return BuildPeriodTextFilterBar(
                fuelFilterFrom, fuelFilterTo, fuelFilterText,
                delegate { ApplyFuelFilterFromUi(); },
                delegate { ResetFuelFilter(); });
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
                "Месяц с", "Месяц по", "Поиск");
        }

        private static FlowLayoutPanel BuildPeriodTextFilterBar(
            TextBox fromBox, TextBox toBox, TextBox textBox,
            EventHandler apply, EventHandler reset,
            string fromLabel = "С", string toLabel = "По", string textLabel = "Поиск")
        {
            FlowLayoutPanel bar = new FlowLayoutPanel
            {
                WrapContents = true,
                AutoSize = false,
                Padding = new Padding(2)
            };
            bar.Controls.Add(FilterLabel(fromLabel));
            fromBox.Width = 90;
            bar.Controls.Add(fromBox);
            bar.Controls.Add(FilterLabel(toLabel));
            toBox.Width = 90;
            bar.Controls.Add(toBox);
            bar.Controls.Add(FilterLabel(textLabel));
            textBox.Width = 140;
            bar.Controls.Add(textBox);
            bar.Controls.Add(MakeButton("Применить", apply));
            bar.Controls.Add(MakeButton("Сбросить", reset));
            return bar;
        }

        private static FlowLayoutPanel BuildCategoryPeriodTextFilterBar(
            ComboBox categoryBox, TextBox fromBox, TextBox toBox, TextBox textBox,
            EventHandler apply, EventHandler reset)
        {
            FlowLayoutPanel bar = new FlowLayoutPanel
            {
                WrapContents = true,
                AutoSize = false,
                Padding = new Padding(2)
            };
            categoryBox.DropDownStyle = ComboBoxStyle.DropDownList;
            categoryBox.Width = 140;
            bar.Controls.Add(FilterLabel("Категория"));
            bar.Controls.Add(categoryBox);
            bar.Controls.Add(FilterLabel("С"));
            fromBox.Width = 90;
            bar.Controls.Add(fromBox);
            bar.Controls.Add(FilterLabel("По"));
            toBox.Width = 90;
            bar.Controls.Add(toBox);
            bar.Controls.Add(FilterLabel("Поиск"));
            textBox.Width = 140;
            bar.Controls.Add(textBox);
            bar.Controls.Add(MakeButton("Применить", apply));
            bar.Controls.Add(MakeButton("Сбросить", reset));
            return bar;
        }

        private static Label FilterLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Padding = new Padding(4, 8, 0, 0)
            };
        }

        private void FuelGridDefaultValuesNeeded(object sender, DataGridViewRowEventArgs e)
        {
            if (e.Row.Cells.Count > 7)
                e.Row.Cells[7].Value = true;
        }

        private static Button MakeButton(string text, EventHandler click)
        {
            Button button = new Button
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(4, 4, 4, 4),
                Padding = new Padding(8, 2, 8, 2),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(225, 231, 237),
                ForeColor = Color.FromArgb(35, 51, 70)
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(49, 77, 102);
            button.Click += click;
            return button;
        }

        private void OpenGoogleSyncWizard(object sender, EventArgs e)
        {
            GoogleSyncSettings settings = GoogleSyncSettings.Load();
            using (GoogleSyncWizardForm wizard = new GoogleSyncWizardForm(settings))
            {
                if (wizard.ShowDialog(this) != DialogResult.OK)
                    return;
                ApplyGoogleSettingsToUi(GoogleSyncSettings.Load());
                SetStatus("Google Sync настроен. Можно синхронизировать.");
            }
        }

        private void ConfigureGoogleSync(object sender, EventArgs e)
        {
            GoogleSyncSettings settings = GoogleSyncSettings.Load();
            using (Form dialog = new Form())
            {
                dialog.Text = "Синхронизация Google (расширенная)";
                dialog.Width = 640;
                dialog.Height = 260;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.BackColor = Color.FromArgb(245, 247, 250);
                Label urlLabel = new Label { Text = "URL веб-приложения Apps Script", Left = 12, Top = 12, Width = 600 };
                TextBox urlBox = new TextBox { Left = 12, Top = 36, Width = 600, Text = settings.ScriptUrl ?? "" };
                Label tokenLabel = new Label { Text = "SYNC_TOKEN", Left = 12, Top = 70, Width = 600 };
                TextBox tokenBox = new TextBox { Left = 12, Top = 94, Width = 600, Text = settings.Token ?? "" };
                Label sheetLabel = new Label { Text = "Ссылка на таблицу (для импорта CSV)", Left = 12, Top = 128, Width = 600 };
                TextBox sheetBox = new TextBox { Left = 12, Top = 152, Width = 600, Text = settings.SpreadsheetUrl ?? "" };
                Button ok = new Button { Text = "Сохранить", Left = 430, Top = 190, Width = 90, DialogResult = DialogResult.OK };
                Button cancel = new Button { Text = "Отмена", Left = 530, Top = 190, Width = 80, DialogResult = DialogResult.Cancel };
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
                SetStatus("Настройки Google сохранены.");
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
                AlfaRepository remote = GoogleSyncClient.PullRemote(settings);
                Cursor = Cursors.Default;

                List<SyncTableDiff> diffs = SyncMerge.Analyze(repository, remote);
                using (SyncOptionsForm options = new SyncOptionsForm(diffs))
                {
                    if (options.ShowDialog(this) != DialogResult.OK)
                    {
                        SetStatus("Синхронизация отменена.");
                        return;
                    }
                    Cursor = Cursors.WaitCursor;
                    string message = GoogleSyncClient.ApplyAndPush(
                        repository, remote, settings, options.Plan);
                    BindAll();
                    repository.Save();
                    dirty = false;
                    SetStatus(message);
                    SyncJournal journal = SyncMerge.LastJournal;
                    if (journal != null)
                        MessageBox.Show(this, journal.FormatSummary(), "Синхронизация Google",
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
            carsGrid.Columns.Add("name", "Автомобиль");
        }

        private void SetupFuelGrid()
        {
            fuelGrid.Columns.Clear();
            fuelGrid.Columns.Add("date", "Дата");
            fuelGrid.Columns.Add("car", "Автомобиль");
            fuelGrid.Columns.Add("km", "Пробег, км");
            fuelGrid.Columns.Add("liters", "Литры");
            fuelGrid.Columns.Add("sum", "Сумма, BYN");
            fuelGrid.Columns.Add("price", "Цена/л");
            fuelGrid.Columns.Add("comment", "АЗС/коммент");
            DataGridViewCheckBoxColumn fullTank = new DataGridViewCheckBoxColumn
            {
                Name = "fullTank",
                HeaderText = "Полный бак",
                TrueValue = true,
                FalseValue = false,
                ThreeState = false,
                Width = 70,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            };
            fuelGrid.Columns.Add(fullTank);
        }

        private void SetupRepairsGrid()
        {
            repairsGrid.Columns.Clear();
            repairsGrid.Columns.Add("date", "Дата");
            repairsGrid.Columns.Add("car", "Автомобиль");
            repairsGrid.Columns.Add("km", "Пробег, км");
            repairsGrid.Columns.Add("cat", "Категория");
            repairsGrid.Columns.Add("product", "Товар");
            repairsGrid.Columns.Add("desc", "Описание");
            repairsGrid.Columns.Add("qty", "Кол-во");
            repairsGrid.Columns.Add("sum", "Сумма, BYN");
            repairsGrid.Columns.Add("comment", "Комментарий");
        }

        private void SetupUtilitiesGrid()
        {
            utilitiesGrid.Columns.Clear();
            utilitiesGrid.Columns.Add("month", "Месяц");
            for (int i = 0; i < AlfaCodec.UtilityLabels.Length; i++)
                utilitiesGrid.Columns.Add("u" + i, AlfaCodec.UtilityLabels[i]);
        }

        private void SetupCategoriesGrid()
        {
            categoriesGrid.Columns.Clear();
            categoriesGrid.Columns.Add("name", "Категория");
        }

        private void SetupExpensesGrid()
        {
            expensesGrid.Columns.Clear();
            expensesGrid.Columns.Add("date", "Дата");
            expensesGrid.Columns.Add("car", "Автомобиль");
            expensesGrid.Columns.Add("cat", "Категория");
            expensesGrid.Columns.Add("name", "Название");
            expensesGrid.Columns.Add("sum", "Сумма, BYN");
            expensesGrid.Columns.Add("comment", "Комментарий");
        }

        private void SetupExpenseCategoriesGrid()
        {
            expenseCategoriesGrid.Columns.Clear();
            expenseCategoriesGrid.Columns.Add("name", "Категория");
        }

        private void SaveToIntervals(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                ToIntervals values = new ToIntervals();
                decimal oilInterval, oilWarn, beltInterval, beltWarn;
                if (!AlfaCodec.TryNumber(oilIntervalBox.Text, out oilInterval) || oilInterval <= 0 ||
                    !AlfaCodec.TryNumber(oilWarnBox.Text, out oilWarn) || oilWarn <= 0 ||
                    !AlfaCodec.TryNumber(beltIntervalBox.Text, out beltInterval) || beltInterval <= 0 ||
                    !AlfaCodec.TryNumber(beltWarnBox.Text, out beltWarn) || beltWarn <= 0)
                    throw new InvalidOperationException("Введите положительные числа для интервалов ТО.");
                values.OilInterval = oilInterval;
                values.OilWarn = oilWarn;
                values.BeltInterval = beltInterval;
                values.BeltWarn = beltWarn;
                repository.Settings = ToSettings.ToRows(values);
                PullAll();
                repository.Save();
                dirty = false;
                SetStatus("Интервалы ТО сохранены.");
                RefreshSummaryText();
            }
            catch (Exception ex)
            {
                ShowError(ex);
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
                fileName = AlfaCodec.RepairCategoriesFile;
            else if (Object.ReferenceEquals(grid, expensesGrid))
                fileName = AlfaCodec.ExpensesFile;
            else if (Object.ReferenceEquals(grid, expenseCategoriesGrid))
                fileName = AlfaCodec.ExpenseCategoriesFile;
            repository.NoteDeleted(fileName, id);
        }

        private void MarkDirty()
        {
            if (suppressDirty)
                return;
            dirty = true;
            SetStatus("Есть несохранённые изменения.");
        }

        private void OpenFolder(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Папка с cars.dat / fuel.dat / repairs.dat / utilities.dat";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    repository.LoadDirectory(dialog.SelectedPath);
                    BindAll();
                    pathBox.Text = dialog.SelectedPath;
                    dirty = false;
                    SetStatus("Открыта папка данных.");
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
                dialog.Filter = "ZIP (*.zip)|*.zip|Все файлы|*.*";
                dialog.Title = "ZIP-бэкап Alfa156";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    string extract = Path.Combine(Path.GetTempPath(), "Alfa156Desktop_" + Guid.NewGuid().ToString("N"));
                    repository.LoadZip(dialog.FileName, extract);
                    BindAll();
                    pathBox.Text = extract;
                    dirty = false;
                    SetStatus("Открыт ZIP. Сохраните в постоянную папку при необходимости.");
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
                dialog.Description = "Куда создать новую папку данных Alfa156";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    string target = Path.Combine(dialog.SelectedPath, "Alfa156");
                    repository.CreateNew(target);
                    BindAll();
                    pathBox.Text = target;
                    dirty = false;
                    SetStatus("Создана новая папка данных: " + target);
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
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
                    SetStatus("Сохранение отменено: проверка пробега.");
                    return;
                }
                repository.Save();
                dirty = false;
                SetStatus("Сохранено в .dat: " + repository.DirectoryPath);
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
                dialog.FileName = "Alfa156-data.zip";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    EnsurePath();
                    PullAll();
                    if (!ConfirmAllMileageWarnings())
                    {
                        SetStatus("Сохранение ZIP отменено: проверка пробега.");
                        return;
                    }
                    repository.SaveZip(dialog.FileName);
                    dirty = false;
                    SetStatus("ZIP сохранён: " + dialog.FileName);
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
                dialog.FileName = "Alfa156.xlsx";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    PullAll();
                    XlsxExchange.Export(repository, dialog.FileName);
                    SetStatus("Excel экспортирован: " + dialog.FileName);
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
                    XlsxExchange.Import(repository, dialog.FileName);
                    BindAll();
                    dirty = true;
                    SetStatus("Импортировано из Excel. Нажмите «Сохранить .dat».");
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
                dialog.Description = "Папка для CSV (удобно загрузить в Google Sheets)";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    PullAll();
                    string folder = Path.Combine(dialog.SelectedPath, "Alfa156-csv");
                    CsvExchange.ExportFolder(repository, folder);
                    SetStatus("CSV экспортированы в " + folder + ". В Google Sheets: Файл → Импорт.");
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
                dialog.Description = "Папка с cars.csv, fuel.csv, repairs.csv, utilities.csv";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    EnsurePathForImport();
                    CsvExchange.ImportFolder(repository, dialog.SelectedPath);
                    BindAll();
                    dirty = true;
                    SetStatus("Импортированы CSV. Нажмите «Сохранить .dat».");
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
                MessageBox.Show(this,
                    "Вставьте ссылку на Google Таблицу.\nТаблица должна быть доступна по ссылке (Файл → Доступ → Все, у кого есть ссылка).",
                    "Google Sheets", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                EnsurePathForImport();
                Cursor = Cursors.WaitCursor;
                CsvExchange.ImportGoogleSheetWorkbook(repository, url);
                BindAll();
                dirty = true;
                SetStatus("Импортировано из Google Sheets. Нажмите «Сохранить .dat».");
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

        private void EnsurePath()
        {
            if (!String.IsNullOrEmpty(repository.DirectoryPath))
                return;
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Выберите папку для сохранения данных Alfa156";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    throw new InvalidOperationException("Папка не выбрана.");
                string target = Path.Combine(dialog.SelectedPath, "Alfa156");
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
                "Alfa156");
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
                BindGrid(carsGrid, repository.Cars, 1);
                BindFuelGrid(repository.Fuel);
                BindGrid(repairsGrid, repository.Repairs, 9);
                BindGrid(expensesGrid, repository.Expenses, 6);
                BindGrid(expenseCategoriesGrid, repository.ExpenseCategories, 1);
                BindGrid(utilitiesGrid, repository.Utilities, 8);
                BindGrid(categoriesGrid, repository.Categories, 1);
                RefreshCategoryFilterLists();
                ApplyAllFilterVisibility();
                ToIntervals intervals = ToSettings.FromRows(repository.Settings);
                oilIntervalBox.Text = intervals.OilInterval.ToString(System.Globalization.CultureInfo.InvariantCulture);
                oilWarnBox.Text = intervals.OilWarn.ToString(System.Globalization.CultureInfo.InvariantCulture);
                beltIntervalBox.Text = intervals.BeltInterval.ToString(System.Globalization.CultureInfo.InvariantCulture);
                beltWarnBox.Text = intervals.BeltWarn.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
            for (int i = 0; i < rows.Count; i++)
            {
                string[] content = RecordIds.StripId(rows[i]);
                object[] values = new object[8];
                for (int c = 0; c < 7; c++)
                    values[c] = c < content.Length ? content[c] : "";
                values[7] = AlfaCodec.IsFullTank(rows[i]);
                int index = fuelGrid.Rows.Add(values);
                fuelGrid.Rows[index].Tag = RecordIds.GetId(rows[i]);
            }
        }

        private void PullAll()
        {
            ToIntervals intervals = ToSettings.FromRows(repository.Settings);
            decimal oilInterval, oilWarn, beltInterval, beltWarn;
            if (AlfaCodec.TryNumber(oilIntervalBox.Text, out oilInterval) && oilInterval > 0)
                intervals.OilInterval = oilInterval;
            if (AlfaCodec.TryNumber(oilWarnBox.Text, out oilWarn) && oilWarn > 0)
                intervals.OilWarn = oilWarn;
            if (AlfaCodec.TryNumber(beltIntervalBox.Text, out beltInterval) && beltInterval > 0)
                intervals.BeltInterval = beltInterval;
            if (AlfaCodec.TryNumber(beltWarnBox.Text, out beltWarn) && beltWarn > 0)
                intervals.BeltWarn = beltWarn;
            // ReadGrid includes hidden (filtered-out) rows — фильтр только визуальный.
            repository.ReplaceAll(
                ReadGrid(carsGrid, 1),
                ReadFuelGrid(),
                ReadGrid(repairsGrid, 9),
                ReadGrid(utilitiesGrid, 8),
                ReadGrid(categoriesGrid, 1),
                ReadGrid(expensesGrid, 6),
                ReadGrid(expenseCategoriesGrid, 1),
                ToSettings.ToRows(intervals));
        }

        private List<string[]> ReadFuelGrid()
        {
            List<string[]> rows = new List<string[]>();
            for (int i = 0; i < fuelGrid.Rows.Count; i++)
            {
                DataGridViewRow row = fuelGrid.Rows[i];
                if (row.IsNewRow)
                    continue;
                string[] values = new string[8];
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
                summaryBox.Text = "Не удалось обновить сводку: " + ex.Message;
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
                summaryBox.Text = "Откройте папку данных, чтобы увидеть сводку и напоминания о ТО.";
                summaryChart.SetData(new List<string>(), new List<decimal[]>());
                return;
            }
            string car = repository.Cars.Count > 0 ? (repository.Cars[0][0] ?? "") : "";
            summaryBox.Text = BuildSummary(car);
            RefreshSummaryChart();
        }

        private void RefreshSummaryChart()
        {
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

        private void AttachReceiptPhoto(object sender, EventArgs e)
        {
            try
            {
                EnsurePath();
                string id = GetSelectedRecordId();
                ReceiptStore.OpenOrAttach(repository.DirectoryPath, id, this);
                SetStatus(ReceiptStore.Exists(repository.DirectoryPath, id)
                    ? "Фото чека: " + ReceiptStore.PathForId(repository.DirectoryPath, id)
                    : "Фото чека не сохранено.");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private string GetSelectedRecordId()
        {
            DataGridView grid = null;
            if (tabs.SelectedTab != null)
            {
                if (tabs.SelectedTab.Text == "Заправки")
                    grid = fuelGrid;
                else if (tabs.SelectedTab.Text == "Ремонт")
                    grid = repairsGrid;
                else if (tabs.SelectedTab.Text == "Прочие")
                    grid = expensesGrid;
            }
            if (grid == null || grid.CurrentRow == null || grid.CurrentRow.IsNewRow)
                throw new InvalidOperationException(
                    "Выберите строку на вкладке Заправки, Ремонт или Прочие.");
            string id = grid.CurrentRow.Tag as string;
            if (String.IsNullOrEmpty(id) || !RecordIds.LooksLikeId(id))
                throw new InvalidOperationException(
                    "У выбранной строки нет id. Сохраните .dat и откройте папку снова.");
            return id;
        }

        private string BuildSummary(string currentCar)
        {
            decimal fuelTotal = 0;
            List<string[]> fuels = repository.Fuel;
            Dictionary<string, decimal[]> monthlyTotals = new Dictionary<string, decimal[]>();
            Dictionary<string, decimal> carTotals = new Dictionary<string, decimal>();
            List<string> cars = new List<string>();
            for (int i = 0; i < repository.Cars.Count; i++)
            {
                if (repository.Cars[i].Length == 0)
                    continue;
                string name = (repository.Cars[i][0] ?? "").Trim();
                if (name.Length == 0)
                    continue;
                cars.Add(name);
                if (!carTotals.ContainsKey(name))
                    carTotals.Add(name, 0);
            }

            for (int i = 0; i < fuels.Count; i++)
            {
                string[] row = fuels[i];
                if (row.Length < 5)
                    continue;
                decimal amount = NumberOrZero(row[4]);
                AddMonthlyAmount(monthlyTotals, row[0], 0, amount);
                if (carTotals.ContainsKey(row[1]))
                    carTotals[row[1]] += amount;
                if (row[1] == currentCar)
                    fuelTotal += amount;
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
                if (carTotals.ContainsKey(row[1]))
                    carTotals[row[1]] += amount;
                if (row[1] != currentCar)
                    continue;
                repairTotal += amount;
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
                if (carTotals.ContainsKey(row[1]))
                    carTotals[row[1]] += amount;
                if (row[1] == currentCar)
                    expenseTotal += amount;
            }

            decimal utilityTotal = 0;
            List<string[]> utilities = repository.Utilities;
            for (int i = 0; i < utilities.Count; i++)
            {
                decimal amount = UtilityTotal(utilities[i]);
                utilityTotal += amount;
                AddMonthlyAmount(monthlyTotals, utilities[i][0], 2, amount);
            }

            StringBuilder result = new StringBuilder();
            result.Append(currentCar).Append("\r\n\r\nЗаправки: ").Append(Money(fuelTotal))
                .Append(" BYN\r\nРемонт и ТО: ").Append(Money(repairTotal)).Append(" BYN\r\nПрочие расходы: ").Append(Money(expenseTotal)).Append(" BYN\r\n");

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
                if (AlfaCodec.TryNumber(row[2], out mileage) && mileage >= 0)
                    AddMileageReading(row[2], ref currentMileage,
                        ref minimumMileage, ref maximumMileage);
                if (!AlfaCodec.TryNumber(row[2], out mileage) || mileage < 0)
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
            result.Append("Учтённые расходы на км (примерно): ");
            if (minimumMileage != Decimal.MaxValue && maximumMileage > minimumMileage)
                result.Append(Money((fuelTotal + repairTotal + expenseTotal) /
                    (maximumMileage - minimumMileage))).Append(" BYN/км\r\n");
            else
                result.Append("недостаточно данных о пробеге\r\n");

            result.Append("\r\nРемонт по категориям:\r\n");
            foreach (KeyValuePair<string, decimal> pair in byCategory)
                result.Append(pair.Key).Append(": ").Append(Money(pair.Value)).Append(" BYN\r\n");
            result.Append("\r\nКоммунальные за весь период:\r\n").Append(Money(utilityTotal))
                .Append(" BYN\r\n\r\nРасходы по месяцам (последние 12):\r\n");
            List<string> months = new List<string>(monthlyTotals.Keys);
            months.Sort(StringComparer.Ordinal);
            int firstMonth = Math.Max(0, months.Count - 12);
            for (int i = firstMonth; i < months.Count; i++)
            {
                decimal[] totals = monthlyTotals[months[i]];
                result.Append(months[i]).Append(": топливо ").Append(Money(totals[0]))
                    .Append(", ремонт ").Append(Money(totals[1])).Append(", коммунальные ").Append(Money(totals[2])).Append(", прочие ")
                    .Append(Money(totals.Length > 3 ? totals[3] : 0)).Append(" BYN\r\n");
            }
            result.Append("\r\nТопливо, ремонт и прочие по автомобилям:\r\n");
            for (int i = 0; i < cars.Count; i++)
                result.Append(cars[i]).Append(": ").Append(Money(carTotals[cars[i]])).Append(" BYN\r\n");
            result.Append("\r\nТекущий пробег: ").Append(Money(currentMileage)).Append(" км\r\n");
            ToIntervals intervals = ToSettings.FromRows(repository.Settings);
            result.Append(ServiceStatus("Замена масла", currentMileage, lastOilMileage,
                lastOilDate, intervals.OilInterval, intervals.OilWarn)).Append("\r\n");
            result.Append(ServiceStatus("Ремень ГРМ", currentMileage, lastTimingMileage,
                lastTimingDate, intervals.BeltInterval, intervals.BeltWarn));
            return result.ToString();
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
            if (!AlfaCodec.TryNumber(value, out mileage) || mileage < 0)
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
                return name + ": замена не зафиксирована";
            string lastService = "последняя запись " + lastDate + ", " +
                Money(lastMileage) + " км; ";
            decimal since = currentMileage - lastMileage;
            if (since >= interval)
                return name + ": " + lastService + "требуется замена";
            if (since >= interval - warningDistance)
                return name + ": " + lastService + "скоро замена (" +
                    Money(interval - since) + " км)";
            return name + ": " + lastService + "в норме (" +
                Money(interval - since) + " км)";
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
            return AlfaCodec.TryNumber(value, out number) ? number : 0;
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
            ApplyFuelFilterVisibility();
            SetStatus("Фильтр заправок применён (данные не удаляются).");
        }

        private void ResetFuelFilter()
        {
            fuelFilterFromValue = fuelFilterToValue = fuelFilterTextValue = "";
            fuelFilterFrom.Text = fuelFilterTo.Text = fuelFilterText.Text = "";
            ApplyFuelFilterVisibility();
            SetStatus("Фильтр заправок сброшен.");
        }

        private void ApplyRepairFilterFromUi()
        {
            repairFilterFromValue = (repairFilterFrom.Text ?? "").Trim();
            repairFilterToValue = (repairFilterTo.Text ?? "").Trim();
            repairFilterTextValue = (repairFilterText.Text ?? "").Trim();
            repairFilterCategoryValue = repairFilterCategory.SelectedItem == null
                ? "" : repairFilterCategory.SelectedItem.ToString();
            ApplyRepairFilterVisibility();
            SetStatus("Фильтр ремонта применён (данные не удаляются).");
        }

        private void ResetRepairFilter()
        {
            repairFilterFromValue = repairFilterToValue = repairFilterTextValue = "";
            repairFilterCategoryValue = "Все категории";
            repairFilterFrom.Text = repairFilterTo.Text = repairFilterText.Text = "";
            if (repairFilterCategory.Items.Count > 0)
                repairFilterCategory.SelectedIndex = 0;
            ApplyRepairFilterVisibility();
            SetStatus("Фильтр ремонта сброшен.");
        }

        private void ApplyExpenseFilterFromUi()
        {
            expenseFilterFromValue = (expenseFilterFrom.Text ?? "").Trim();
            expenseFilterToValue = (expenseFilterTo.Text ?? "").Trim();
            expenseFilterTextValue = (expenseFilterText.Text ?? "").Trim();
            expenseFilterCategoryValue = expenseFilterCategory.SelectedItem == null
                ? "" : expenseFilterCategory.SelectedItem.ToString();
            ApplyExpenseFilterVisibility();
            SetStatus("Фильтр прочих расходов применён (данные не удаляются).");
        }

        private void ResetExpenseFilter()
        {
            expenseFilterFromValue = expenseFilterToValue = expenseFilterTextValue = "";
            expenseFilterCategoryValue = "Все категории";
            expenseFilterFrom.Text = expenseFilterTo.Text = expenseFilterText.Text = "";
            if (expenseFilterCategory.Items.Count > 0)
                expenseFilterCategory.SelectedIndex = 0;
            ApplyExpenseFilterVisibility();
            SetStatus("Фильтр прочих расходов сброшен.");
        }

        private void ApplyUtilityFilterFromUi()
        {
            utilityFilterFromValue = (utilityFilterFrom.Text ?? "").Trim();
            utilityFilterToValue = (utilityFilterTo.Text ?? "").Trim();
            utilityFilterTextValue = (utilityFilterText.Text ?? "").Trim();
            ApplyUtilityFilterVisibility();
            SetStatus("Фильтр коммуналки применён (данные не удаляются).");
        }

        private void ResetUtilityFilter()
        {
            utilityFilterFromValue = utilityFilterToValue = utilityFilterTextValue = "";
            utilityFilterFrom.Text = utilityFilterTo.Text = utilityFilterText.Text = "";
            ApplyUtilityFilterVisibility();
            SetStatus("Фильтр коммуналки сброшен.");
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
                if (fuelFilterTextValue.Length > 0)
                {
                    string hay = (CellText(row, 2) + " " + CellText(row, 3) + " " + CellText(row, 4) +
                        " " + CellText(row, 5) + " " + CellText(row, 6)).ToLowerInvariant();
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

        private static string CellText(DataGridViewRow row, int index)
        {
            if (index < 0 || index >= row.Cells.Count)
                return "";
            object value = row.Cells[index].Value;
            return value == null ? "" : Convert.ToString(value).Trim();
        }

        private void SetStatus(string text)
        {
            statusLabel.Text = text;
        }

        private void ShowError(Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Alfa156 Desktop", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Ошибка: " + ex.Message);
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
            if (!AlfaCodec.TryNumber(kmText, out newKm) || newKm < 0)
                return;
            string fileName = Object.ReferenceEquals(grid, fuelGrid) ? "fuel.dat" : "repairs.dat";
            decimal last = LastKnownMileageFromGrids(car, grid, e.RowIndex);
            string warning = FormatMileageWarning(newKm, last);
            if (warning == null)
                return;
            if (MessageBox.Show(this, warning, "Пробег", MessageBoxButtons.YesNo,
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
                if (car.Length == 0 || !AlfaCodec.TryNumber(kmText, out newKm) || newKm < 0)
                    continue;
                decimal lastExcluding = LastKnownMileageFromGrids(car, grid, i);
                string warning = FormatMileageWarning(newKm, lastExcluding);
                if (warning == null)
                    continue;
                string context = fileName == "fuel.dat" ? "Заправка" : "Ремонт";
                string message = context + " " + CellText(row, 0) + " / " + car + ":\r\n" + warning;
                if (MessageBox.Show(this, message, "Пробег", MessageBoxButtons.YesNo,
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
                if (!AlfaCodec.TryNumber(CellText(row, 2), out km) || km < 0)
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
                return "Пробег меньше предыдущего (" + x + "). Сохранить всё равно?";
            if (newKm - lastKnown > 5000m)
                return "Пробег вырос более чем на 5000 км с предыдущего (" + x +
                    "). Сохранить всё равно?";
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

        private void OpenGlobalSearch(object sender, EventArgs e)
        {
            string preset = globalSearchBox.ForeColor == Color.Gray ? "" : (globalSearchBox.Text ?? "").Trim();
            using (Form dialog = new Form())
            {
                dialog.Text = "Поиск по записям";
                dialog.Width = 640;
                dialog.Height = 480;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowIcon = false;

                TextBox query = new TextBox { Left = 12, Top = 12, Width = 480 };
                query.Text = preset;
                Button find = new Button { Text = "Найти", Left = 500, Top = 10, Width = 100 };
                ListBox list = new ListBox { Left = 12, Top = 48, Width = 588, Height = 340 };
                Button open = new Button { Text = "Открыть", Left = 400, Top = 400, Width = 100 };
                Button close = new Button { Text = "Закрыть", Left = 510, Top = 400, Width = 90 };
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
                        list.Items.Add("Ничего не найдено");
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
            CollectDesktopHits(hits, "Заправки", fuelGrid, q, delegate(DataGridViewRow row)
            {
                return CellText(row, 6) + " " + CellText(row, 1);
            });
            CollectDesktopHits(hits, "Ремонт", repairsGrid, q, delegate(DataGridViewRow row)
            {
                return CellText(row, 3) + " " + CellText(row, 4) + " " + CellText(row, 5) + " " +
                    CellText(row, 8) + " " + CellText(row, 1);
            });
            CollectDesktopHits(hits, "Прочие", expensesGrid, q, delegate(DataGridViewRow row)
            {
                return CellText(row, 2) + " " + CellText(row, 3) + " " + CellText(row, 5) + " " +
                    CellText(row, 1);
            });
            CollectDesktopHits(hits, "Коммуналка", utilitiesGrid, q, delegate(DataGridViewRow row)
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
                hit.Car = tab == "Коммуналка" ? "" : CellText(row, 1);
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
            DataGridView grid = hit.Tab == "Заправки" ? fuelGrid
                : hit.Tab == "Ремонт" ? repairsGrid
                : hit.Tab == "Прочие" ? expensesGrid
                : hit.Tab == "Коммуналка" ? utilitiesGrid
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
            SetStatus("Найдено: " + hit.Label);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (dirty)
            {
                DialogResult result = MessageBox.Show(this,
                    "Есть несохранённые изменения. Выйти без сохранения?",
                    "Alfa156 Desktop",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (result != DialogResult.Yes)
                    e.Cancel = true;
            }
            base.OnFormClosing(e);
        }
    }
}
