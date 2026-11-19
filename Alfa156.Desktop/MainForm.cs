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
        private const string AppVersion = "1.27.0";
        private bool chartShowsCostPerKm;

        private string Cur()
        {
            return ToSettings.GetCurrency(repository.Settings);
        }

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
        private readonly TextBox currencyBox = new TextBox();
        private readonly TextBox budgetBox = new TextBox();
        private readonly TextBox budgetWarnBox = new TextBox();
        private readonly TextBox backupRemindBox = new TextBox();
        private readonly TextBox reminderWarnBox = new TextBox();
        private readonly ComboBox defaultCarBox = new ComboBox();
        private readonly StatusStrip statusStrip = new StatusStrip();
        private readonly ToolStripStatusLabel statusLabel = new ToolStripStatusLabel("Откройте папку данных или ZIP с Android/КПК.");
        private readonly TextBox googleUrlBox = new TextBox();
        private bool suppressDirty;
        private bool dirty;
        private bool refreshingSummary;

        private readonly TextBox fuelFilterFrom = new TextBox();
        private readonly TextBox fuelFilterTo = new TextBox();
        private readonly TextBox fuelFilterText = new TextBox();
        private readonly ComboBox fuelFilterPurpose = new ComboBox();
        private string fuelFilterFromValue = "";
        private string fuelFilterToValue = "";
        private string fuelFilterTextValue = "";
        private string fuelFilterPurposeValue = "";

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
            toolbar.Controls.Add(MakeButton("Экспорт всего (ZIP)", SaveZip));
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
            toolbar.Controls.Add(MakeButton("Дубликаты", OpenDuplicates));
            toolbar.Controls.Add(MakeButton("Проверка данных", OpenDataIntegrity));

            FlowLayoutPanel exchange = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = true
            };
            exchange.Controls.Add(MakeButton("Импорт…", OpenImportWizard));
            exchange.Controls.Add(MakeButton("Экспорт Excel", ExportExcel));
            exchange.Controls.Add(MakeButton("Импорт Excel", ImportExcel));
            exchange.Controls.Add(MakeButton("Экспорт CSV (Google)", ExportCsv));
            exchange.Controls.Add(MakeButton("Импорт CSV-папки", ImportCsvFolder));
            exchange.Controls.Add(MakeButton("Шаблоны CSV", ExportCsvTemplates));
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
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
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
            intervalRow.Controls.Add(new Label { Text = "Валюта", AutoSize = true, Padding = new Padding(16, 8, 0, 0) });
            currencyBox.Width = 60;
            currencyBox.Text = "BYN";
            intervalRow.Controls.Add(currencyBox);
            intervalRow.Controls.Add(MakeButton("Сохранить валюту", SaveCurrency));
            intervalRow.Controls.Add(new Label { Text = "Бюджет/мес", AutoSize = true, Padding = new Padding(16, 8, 0, 0) });
            budgetBox.Width = 70;
            budgetBox.Text = "0";
            intervalRow.Controls.Add(budgetBox);
            intervalRow.Controls.Add(new Label { Text = "предупр.%", AutoSize = true, Padding = new Padding(8, 8, 0, 0) });
            budgetWarnBox.Width = 40;
            budgetWarnBox.Text = "80";
            intervalRow.Controls.Add(budgetWarnBox);
            intervalRow.Controls.Add(MakeButton("Сохранить бюджет", SaveBudget));
            intervalRow.Controls.Add(new Label { Text = "Напом. копии (дн.)", AutoSize = true, Padding = new Padding(16, 8, 0, 0) });
            backupRemindBox.Width = 40;
            backupRemindBox.Text = "7";
            intervalRow.Controls.Add(backupRemindBox);
            intervalRow.Controls.Add(MakeButton("Сохранить напом.", SaveBackupRemind));
            intervalRow.Controls.Add(new Label { Text = "Напом. даты (дн.)", AutoSize = true, Padding = new Padding(16, 8, 0, 0) });
            reminderWarnBox.Width = 40;
            reminderWarnBox.Text = "14";
            intervalRow.Controls.Add(reminderWarnBox);
            intervalRow.Controls.Add(MakeButton("Сохранить срок", SaveReminderWarnDays));
            intervalRow.Controls.Add(new Label { Text = "Авто по умолч.", AutoSize = true, Padding = new Padding(16, 8, 0, 0) });
            defaultCarBox.Width = 140;
            intervalRow.Controls.Add(defaultCarBox);
            intervalRow.Controls.Add(MakeButton("Сохранить авто", SaveDefaultCar));
            intervalRow.Controls.Add(MakeButton("Добавить напоминание", AddDateReminder));
            intervalRow.Controls.Add(MakeButton("Удалить напоминание", DeleteDateReminder));
            intervalRow.Controls.Add(MakeButton("Сохранить сводку…", SaveSummaryText));
            intervalRow.Controls.Add(MakeButton("Сравнить периоды", ShowPeriodCompare));
            intervalRow.Controls.Add(MakeButton("Лента авто", ShowCarTimeline));
            intervalRow.Controls.Add(MakeButton("Из шаблона", ApplyTemplateToActiveGrid));
            intervalRow.Controls.Add(MakeButton("Сохранить как шаблон", SaveActiveRowAsTemplate));
            intervalRow.Controls.Add(MakeButton("График: /км", ToggleCostPerKmChart));
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
            FlowLayoutPanel bar = BuildPeriodTextFilterBar(
                fuelFilterFrom, fuelFilterTo, fuelFilterText,
                delegate { ApplyFuelFilterFromUi(); },
                delegate { ResetFuelFilter(); });
            fuelFilterPurpose.DropDownStyle = ComboBoxStyle.DropDownList;
            fuelFilterPurpose.Width = 110;
            fuelFilterPurpose.Items.Clear();
            fuelFilterPurpose.Items.Add("Все цели");
            fuelFilterPurpose.Items.Add("Личный");
            fuelFilterPurpose.Items.Add("Работа");
            fuelFilterPurpose.SelectedIndex = 0;
            bar.Controls.Add(FilterLabel("Цель"));
            bar.Controls.Add(fuelFilterPurpose);
            // Move apply/reset after purpose: rebuild order by bringing buttons to end.
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
            string bestDate = "";
            string bestPrice = "";
            for (int i = 0; i < fuelGrid.Rows.Count; i++)
            {
                DataGridViewRow row = fuelGrid.Rows[i];
                if (row.IsNewRow)
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

                string safetyPath = repository.AllocateSafetyBackupPath();
                using (SyncOptionsForm options = new SyncOptionsForm(repository, remote, safetyPath))
                {
                    if (options.ShowDialog(this) != DialogResult.OK)
                    {
                        SetStatus("Синхронизация отменена.");
                        return;
                    }
                    Cursor = Cursors.WaitCursor;
                    repository.CreateSafetyBackup(safetyPath);
                    string message = GoogleSyncClient.ApplyAndPush(
                        repository, remote, settings, options.Plan);
                    BindAll();
                    repository.Save();
                    dirty = false;
                    SetStatus(message + "\r\nКопия: " + safetyPath);
                    SyncJournal journal = SyncMerge.LastJournal;
                    if (journal != null)
                        MessageBox.Show(this, journal.FormatSummary() +
                            "\r\n\r\nКопия: " + safetyPath, "Синхронизация Google",
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
            carsGrid.Columns.Add("year", "Год");
            carsGrid.Columns.Add("plate", "Госномер");
            carsGrid.Columns.Add("vin", "VIN");
            carsGrid.Columns.Add("tank", "Бак, л");
            carsGrid.Columns.Add("note", "Заметка");
        }

        private void SetupFuelGrid()
        {
            fuelGrid.Columns.Clear();
            fuelGrid.Columns.Add("date", "Дата");
            fuelGrid.Columns.Add("car", "Автомобиль");
            fuelGrid.Columns.Add("km", "Пробег, км");
            fuelGrid.Columns.Add("liters", "Литры");
            fuelGrid.Columns.Add("sum", "Сумма, " + Cur());
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
            DataGridViewComboBoxColumn purpose = new DataGridViewComboBoxColumn
            {
                Name = "purpose",
                HeaderText = "Цель",
                FlatStyle = FlatStyle.Flat,
                Width = 90,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            };
            purpose.Items.Add("");
            purpose.Items.Add("Личный");
            purpose.Items.Add("Работа");
            fuelGrid.Columns.Add(purpose);
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
            repairsGrid.Columns.Add("sum", "Сумма, " + Cur());
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
            expensesGrid.Columns.Add("sum", "Сумма, " + Cur());
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
                repository.Settings = ToSettings.ApplyIntervals(repository.Settings, values);
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
                SetStatus("Валюта: " + Cur() + " (суммы не пересчитываются).");
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
                if (!AlfaCodec.TryNumber(budgetBox.Text, out monthLimit) || monthLimit < 0)
                    throw new InvalidOperationException("Бюджет месяца: 0 (выкл.) или положительное число.");
                if (!AlfaCodec.TryNumber(budgetWarnBox.Text, out warnPct) || warnPct <= 0 || warnPct > 100)
                    throw new InvalidOperationException("Порог предупреждения: от 1 до 100 %.");
                repository.Settings = ToSettings.SetBudget(repository.Settings, monthLimit, warnPct);
                PullAll();
                repository.Save();
                dirty = false;
                budgetBox.Text = ToSettings.GetBudgetMonth(repository.Settings)
                    .ToString(CultureInfo.InvariantCulture);
                budgetWarnBox.Text = ToSettings.GetBudgetWarnPct(repository.Settings)
                    .ToString(CultureInfo.InvariantCulture);
                if (monthLimit <= 0)
                    SetStatus("Месячный бюджет выключен.");
                else
                    SetStatus("Бюджет месяца: " + Money(monthLimit) + " " + Cur() +
                        ", предупреждение с " + Money(warnPct) + "%.");
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
                if (!AlfaCodec.TryNumber(backupRemindBox.Text, out days) || days < 0 || days > 3650 ||
                    days != Decimal.Truncate(days))
                    throw new InvalidOperationException("Интервал напоминания: 0 (выкл.) или целое 1–3650.");
                repository.Settings = ToSettings.SetBackupRemindDays(repository.Settings, (int)days);
                PullAll();
                repository.Save();
                dirty = false;
                backupRemindBox.Text = ToSettings.GetBackupRemindDays(repository.Settings)
                    .ToString(CultureInfo.InvariantCulture);
                if (days <= 0)
                    SetStatus("Напоминание о резервной копии выключено.");
                else
                    SetStatus("Напоминание о копии: каждые " + ((int)days) + " дн. без бэкапа.");
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
                if (!AlfaCodec.TryNumber(reminderWarnBox.Text, out days) || days < 0 || days > 3650 ||
                    days != Decimal.Truncate(days))
                    throw new InvalidOperationException("Срок напоминаний: целое 0–3650.");
                repository.Settings = ToSettings.SetReminderWarnDays(repository.Settings, (int)days);
                PullAll();
                repository.Save();
                dirty = false;
                reminderWarnBox.Text = ToSettings.GetReminderWarnDays(repository.Settings)
                    .ToString(CultureInfo.InvariantCulture);
                SetStatus("Предупреждать за " + ((int)days) + " дн. до даты напоминания.");
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
                    dialog.Text = "Новое напоминание";
                    dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.ClientSize = new Size(420, 200);
                    dialog.MaximizeBox = false;
                    dialog.MinimizeBox = false;
                    Label dateLabel = new Label { Text = "Дата (ГГГГ-ММ-ДД)", Left = 12, Top = 14, Width = 160 };
                    TextBox dateBox = new TextBox
                    {
                        Left = 180, Top = 12, Width = 220,
                        Text = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    };
                    Label carLabel = new Label { Text = "Автомобиль", Left = 12, Top = 44, Width = 160 };
                    TextBox carBox = new TextBox { Left = 180, Top = 42, Width = 220, Text = car };
                    Label titleLabel = new Label { Text = "Название", Left = 12, Top = 74, Width = 160 };
                    TextBox titleBox = new TextBox { Left = 180, Top = 72, Width = 220 };
                    Label typeLabel = new Label { Text = "Тип", Left = 12, Top = 104, Width = 160 };
                    ComboBox typeBox = new ComboBox
                    {
                        Left = 180, Top = 102, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList
                    };
                    typeBox.Items.AddRange(new object[] { "Страховка", "Техосмотр", "Своё" });
                    typeBox.SelectedIndex = 0;
                    Button ok = new Button { Text = "Сохранить", DialogResult = DialogResult.OK, Left = 220, Top = 150, Width = 90 };
                    Button cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Left = 320, Top = 150, Width = 80 };
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
                    SetStatus("Напоминание сохранено.");
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
                    MessageBox.Show(this, "Нет напоминаний.", "Напоминания",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                using (Form dialog = new Form())
                {
                    dialog.Text = "Удалить напоминание";
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
                    Button ok = new Button { Text = "Удалить", DialogResult = DialogResult.OK, Left = 320, Top = 70, Width = 90 };
                    Button cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Left = 420, Top = 70, Width = 80 };
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
                    SetStatus("Напоминание удалено.");
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
            defaultCarBox.Items.Add("(не задан)");
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
                    ? "Авто по умолчанию сброшен."
                    : "Авто по умолчанию: " + name);
                RefreshSummaryText();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Авто по умолчанию",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private string ActiveTemplateType()
        {
            if (tabs.SelectedTab == null)
                return null;
            string title = tabs.SelectedTab.Text ?? "";
            if (title.StartsWith("Заправки"))
                return RecordTemplates.TypeFuel;
            if (title.StartsWith("Ремонт"))
                return RecordTemplates.TypeRepair;
            if (title.StartsWith("Прочие"))
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
                        "Откройте вкладку Заправки, Ремонт или Прочие.",
                        "Из шаблона", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (repository.TemplatesRows == null)
                    repository.TemplatesRows = new List<string[]>();
                List<RecordTemplate> list = RecordTemplates.ListByType(repository.TemplatesRows, type);
                if (list.Count == 0)
                {
                    MessageBox.Show(this, "Нет шаблонов этого типа. Сначала сохраните строку как шаблон.",
                        "Из шаблона", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                string[] names = new string[list.Count];
                for (int i = 0; i < list.Count; i++)
                    names[i] = list[i].Name;
                string chosen = PickFromList("Из шаблона", names);
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
                SetStatus("Строка из шаблона «" + chosen + "» добавлена. Сохраните .dat.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Из шаблона",
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
                        "Откройте вкладку Заправки, Ремонт или Прочие и выберите строку.",
                        "Шаблон", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (grid.CurrentRow == null || grid.CurrentRow.IsNewRow)
                {
                    MessageBox.Show(this, "Выберите заполненную строку.",
                        "Шаблон", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                string name = PromptText("Сохранить как шаблон", "Название шаблона:",
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
                SetStatus("Шаблон сохранён: " + name);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Шаблон",
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
                Button cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, AutoSize = true };
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
                Button cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, AutoSize = true };
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
                    "Подсказка Alfa 156:\r\n" +
                    "• Синхронизация по Wi‑Fi — обмен .dat с телефоном/КПК\r\n" +
                    "• Экспорт всего (ZIP) — .dat + Receipts/ + settings\r\n" +
                    "• Вкладка «Сводка» — итоги, график и ТО";
                MessageBox.Show(this, tip, "Alfa 156", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SetStatus("Подсказка показана один раз (onboarding_seen).");
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
                string age = days < 0 ? "копии ещё не было" : ("прошло " + days + " дн.");
                MessageBox.Show(this,
                    "Давно не было резервной копии (" + age + ").\r\n" +
                    "Рекомендуем «Экспорт всего (ZIP)» — все .dat, Receipts/ и settings.",
                    "Резервная копия",
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
                    SetStatus("Экспорт всего (ZIP) сохранён: " + dialog.FileName);
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
                    SetStatus("Импорт завершён." +
                        (safety != null ? " Копия Safety: " + safety : ""));
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
                dialog.Description = "Куда сохранить шаблоны CSV (пустые заголовки fuel/repairs/expenses)";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    string folder = Path.Combine(dialog.SelectedPath, "Alfa156-csv-templates");
                    CsvExchange.ExportTemplates(folder);
                    SetStatus("Шаблоны CSV: " + folder);
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
                    string safety = CreateSafetyIfPossible();
                    XlsxExchange.Import(repository, dialog.FileName);
                    BindAll();
                    dirty = true;
                    SetStatus("Импортировано из Excel. Нажмите «Сохранить .dat»." +
                        (safety != null ? " Копия: " + safety : ""));
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
                    string safety = CreateSafetyIfPossible();
                    CsvExchange.ImportFolder(repository, dialog.SelectedPath);
                    BindAll();
                    dirty = true;
                    SetStatus("Импортированы CSV. Нажмите «Сохранить .dat»." +
                        (safety != null ? " Копия: " + safety : ""));
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
                string safety = CreateSafetyIfPossible();
                CsvExchange.ImportGoogleSheetWorkbook(repository, url);
                BindAll();
                dirty = true;
                SetStatus("Импортировано из Google Sheets. Нажмите «Сохранить .dat»." +
                    (safety != null ? " Копия: " + safety : ""));
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
                ToIntervals intervals = ToSettings.FromRows(repository.Settings);
                oilIntervalBox.Text = intervals.OilInterval.ToString(System.Globalization.CultureInfo.InvariantCulture);
                oilWarnBox.Text = intervals.OilWarn.ToString(System.Globalization.CultureInfo.InvariantCulture);
                beltIntervalBox.Text = intervals.BeltInterval.ToString(System.Globalization.CultureInfo.InvariantCulture);
                beltWarnBox.Text = intervals.BeltWarn.ToString(System.Globalization.CultureInfo.InvariantCulture);
                currencyBox.Text = ToSettings.GetCurrency(repository.Settings);
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
            for (int i = 0; i < rows.Count; i++)
            {
                string[] content = RecordIds.StripId(rows[i]);
                object[] values = new object[9];
                for (int c = 0; c < 7; c++)
                    values[c] = c < content.Length ? content[c] : "";
                values[7] = AlfaCodec.IsFullTank(rows[i]);
                string purpose = AlfaCodec.GetPurpose(rows[i]);
                EnsurePurposeComboItem(purpose);
                values[8] = purpose;
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
                string[] values = new string[9];
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
                values[8] = AlfaCodec.NormalizePurpose(CellText(row, 8));
                int length = 9;
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
                summaryChart.SetCompareData(labels, values, Cur() + "/км");
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
                ? "График: сравнение стоимости/км по автомобилям."
                : "График: расходы по месяцам.");
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
                    throw new InvalidOperationException("Нет автомобиля для ленты.");
                CarPassport passport = FindPassport(car);
                string text = CarTimeline.FormatList(
                    CarTimeline.Build(repository.Fuel, repository.Repairs, repository.Expenses, car),
                    car,
                    FormatPassportSuffix(passport));
                using (Form dialog = new Form())
                {
                    dialog.Text = "Лента · " + car;
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
                    throw new InvalidOperationException("Нет автомобиля для сравнения.");

                string aFrom, aTo, bFrom, bTo;
                PeriodCompare.ThisVsPreviousMonth(out aFrom, out aTo, out bFrom, out bTo);

                using (Form dialog = new Form())
                {
                    dialog.Text = "Сравнить периоды · " + car;
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
                    layout.Controls.Add(new Label { Text = "A с", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
                    layout.Controls.Add(aFromBox, 1, 0);
                    layout.Controls.Add(new Label { Text = "A по", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
                    layout.Controls.Add(aToBox, 1, 1);
                    layout.Controls.Add(new Label { Text = "B с", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
                    layout.Controls.Add(bFromBox, 1, 2);
                    layout.Controls.Add(new Label { Text = "B по", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
                    layout.Controls.Add(bToBox, 1, 3);

                    FlowLayoutPanel presets = new FlowLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        WrapContents = true
                    };
                    Button thisVsPrev = new Button { Text = "Этот / прошлый месяц", AutoSize = true };
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
                    layout.Controls.Add(new Label { Text = "Пресет", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
                    layout.Controls.Add(presets, 1, 4);

                    FlowLayoutPanel buttons = new FlowLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        FlowDirection = FlowDirection.RightToLeft
                    };
                    Button cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, AutoSize = true };
                    Button ok = new Button { Text = "Сравнить", DialogResult = DialogResult.OK, AutoSize = true };
                    buttons.Controls.Add(ok);
                    buttons.Controls.Add(cancel);
                    layout.Controls.Add(buttons, 0, 5);
                    layout.SetColumnSpan(buttons, 2);

                    Label hint = new Label
                    {
                        Text = "Даты в формате yyyy-MM-dd. A — текущий период, B — база для Δ (A−B).",
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
                        throw new InvalidOperationException("Укажите даты в формате yyyy-MM-dd.");
                    if (String.CompareOrdinal(af2, at2) > 0 || String.CompareOrdinal(bf2, bt2) > 0)
                        throw new InvalidOperationException("В каждом периоде дата «с» не позже «по».");

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
                        result.Text = "Сравнение · " + car;
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
            decimal yearFuel = 0;
            decimal yearRepair = 0;
            decimal yearExpense = 0;
            decimal yearUtility = 0;
            decimal purposePersonal = 0;
            decimal purposeWork = 0;
            decimal purposeOther = 0;
            bool anyPurpose = false;
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
                    string purpose = AlfaCodec.GetPurpose(row);
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
                    result.Append("бак ").Append(passport.TankLiters).Append(" л");
                }
                if (passport.Note.Length > 0)
                    result.Append("\r\n").Append(passport.Note);
            }
            result.Append("\r\n\r\nЗаправки: ").Append(Money(fuelTotal))

                .Append(" " + Cur() + "\r\nРемонт и ТО: ").Append(Money(repairTotal)).Append(" " + Cur() + "\r\nПрочие расходы: ").Append(Money(expenseTotal)).Append(" " + Cur() + "\r\n");
            if (anyPurpose)
            {
                result.Append("\r\nЗаправки по цели:\r\nЛичный: ").Append(Money(purposePersonal))
                    .Append(" " + Cur() + "\r\nРабота: ").Append(Money(purposeWork)).Append(" " + Cur());
                if (purposeOther > 0)
                    result.Append("\r\nДругое: ").Append(Money(purposeOther)).Append(" " + Cur());
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
                    (maximumMileage - minimumMileage))).Append(" " + Cur() + "/км\r\n");
            else
                result.Append("недостаточно данных о пробеге\r\n");

            result.Append("\r\nРемонт по категориям:\r\n");
            foreach (KeyValuePair<string, decimal> pair in byCategory)
                result.Append(pair.Key).Append(": ").Append(Money(pair.Value)).Append(" " + Cur() + "\r\n");
            result.Append("\r\nКоммунальные за весь период:\r\n").Append(Money(utilityTotal))
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
            result.Append("\r\n\r\nИтого за год ").Append(yearPrefix).Append(":\r\n")
                .Append("Топливо: ").Append(Money(yearFuel)).Append(" " + Cur() + "\r\n")
                .Append("Ремонт: ").Append(Money(yearRepair)).Append(" " + Cur() + "\r\n")
                .Append("Прочие: ").Append(Money(yearExpense)).Append(" " + Cur() + "\r\n")
                .Append("Коммунальные: ").Append(Money(yearUtility)).Append(" " + Cur() + "\r\n")
                .Append("Всего: ").Append(Money(yearFuel + yearRepair + yearExpense + yearUtility))
                .Append(" " + Cur() + "\r\n\r\nРасходы по месяцам (последние 12):\r\n");
            List<string> months = new List<string>(monthlyTotals.Keys);
            months.Sort(StringComparer.Ordinal);
            int firstMonth = Math.Max(0, months.Count - 12);
            for (int i = firstMonth; i < months.Count; i++)
            {
                decimal[] totals = monthlyTotals[months[i]];
                result.Append(months[i]).Append(": топливо ").Append(Money(totals[0]))
                    .Append(", ремонт ").Append(Money(totals[1])).Append(", коммунальные ").Append(Money(totals[2])).Append(", прочие ")
                    .Append(Money(totals.Length > 3 ? totals[3] : 0)).Append(" " + Cur() + "\r\n");
            }
            result.Append("\r\n").Append(CarCompare.FormatBlock(
                CarCompare.Build(cars, fuels, repairs, expenses), Cur(), Money)).Append("\r\n");
            result.Append("\r\nТекущий пробег: ").Append(Money(currentMileage)).Append(" км\r\n");
            ToIntervals intervals = ToSettings.FromRows(repository.Settings);
            result.Append(ServiceStatus("Замена масла", currentMileage, lastOilMileage,
                lastOilDate, intervals.OilInterval, intervals.OilWarn)).Append("\r\n");
            result.Append(ServiceStatus("Ремень ГРМ", currentMileage, lastTimingMileage,
                lastTimingDate, intervals.BeltInterval, intervals.BeltWarn));
            int warnDays = ToSettings.GetReminderWarnDays(repository.Settings);
            result.Append(DateReminders.FormatUpcomingBlock(
                repository.RemindersRows, warnDays, DateTime.Today));
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
            result.Append("\r\n\r\nБюджет месяца ").Append(monthKey).Append(":\r\n")
                .Append("Потрачено (топливо+ремонт+прочие): ")
                .Append(Money(spent)).Append(" / ").Append(Money(budget)).Append(" ").Append(Cur())
                .Append(" (").Append(Money(pct)).Append("%)\r\n")
                .Append("Коммунальные в этом месяце: ")
                .Append(Money(utilitiesMonth)).Append(" ").Append(Cur());
            if (spent >= budget)
                result.Append("\r\n⚠ Превышен бюджет");
            else if (pct >= warnPct)
                result.Append("\r\n⚠ Предупреждение: ≥").Append(Money(warnPct)).Append("% бюджета");
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
            string purpose = fuelFilterPurpose.SelectedItem == null
                ? "" : fuelFilterPurpose.SelectedItem.ToString();
            fuelFilterPurposeValue = String.Equals(purpose, "Все цели", StringComparison.OrdinalIgnoreCase)
                ? "" : (purpose ?? "").Trim();
            ApplyFuelFilterVisibility();
            SetStatus("Фильтр заправок применён (данные не удаляются).");
        }

        private void ResetFuelFilter()
        {
            fuelFilterFromValue = fuelFilterToValue = fuelFilterTextValue = fuelFilterPurposeValue = "";
            fuelFilterFrom.Text = fuelFilterTo.Text = fuelFilterText.Text = "";
            if (fuelFilterPurpose.Items.Count > 0)
                fuelFilterPurpose.SelectedIndex = 0;
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
                if (fuelFilterPurposeValue.Length > 0 &&
                    !String.Equals(CellText(row, 8), fuelFilterPurposeValue, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (fuelFilterTextValue.Length > 0)
                {
                    string hay = (CellText(row, 2) + " " + CellText(row, 3) + " " + CellText(row, 4) +
                        " " + CellText(row, 5) + " " + CellText(row, 6) + " " + CellText(row, 8))
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
                MessageBox.Show(this, "Сначала откройте папку данных.", "Сводка",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "Текст (*.txt)|*.txt";
                dialog.FileName = "Summary_" +
                    DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".txt";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    File.WriteAllText(dialog.FileName, text, Encoding.UTF8);
                    SetStatus("Сводка сохранена: " + dialog.FileName);
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
                    MessageBox.Show(this, "Сначала откройте папку данных.", "Проверка данных",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                PullAll();
                DataIntegrity.Report report = DataIntegrity.Check(repository);
                string text = DataIntegrity.Format(report);
                using (Form dialog = new Form())
                {
                    dialog.Text = "Проверка данных";
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
                    Button close = new Button { Text = "Закрыть", Width = 100 };
                    close.Click += delegate { dialog.Close(); };
                    buttons.Controls.Add(close);
                    if (report.CanSimpleFix)
                    {
                        Button fix = new Button { Text = "Исправить простое", Width = 140 };
                        fix.Click += delegate
                        {
                            try
                            {
                                int n = DataIntegrity.ApplySimpleFixes(repository, report);
                                BindAll();
                                MessageBox.Show(dialog,
                                    n > 0 ? "Добавлено категорий: " + n + "." : "Нечего исправлять.",
                                    "Проверка данных", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show(this, "Дубликатов не найдено.", "Дубликаты",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (Form dialog = new Form())
            {
                dialog.Text = "Дубликаты";
                dialog.Width = 720;
                dialog.Height = 520;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowIcon = false;

                ListBox list = new ListBox { Left = 12, Top = 12, Width = 680, Height = 400 };
                Button deleteExtras = new Button
                {
                    Text = "Удалить лишние в группе",
                    Left = 12,
                    Top = 430,
                    Width = 200
                };
                Button close = new Button { Text = "Закрыть", Left = 580, Top = 430, Width = 110 };
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
                        MessageBox.Show(dialog, "Выберите запись внутри группы.", "Дубликаты",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    DuplicateFinder.Group group = rowGroup[selected];
                    if (MessageBox.Show(dialog,
                        "Удалить " + (group.Indexes.Count - 1) +
                        " лишних записей, оставив первую?",
                        "Дубликаты", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                        return;
                    try
                    {
                        DeleteDuplicateExtras(group);
                        BindAll();
                        MarkDirty();
                        dialog.DialogResult = DialogResult.OK;
                        dialog.Close();
                        SetStatus("Лишние дубликаты удалены (" + group.Title + ").");
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
            if (fileName == AlfaCodec.ExpensesFile)
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
                return CellText(row, 6) + " " + CellText(row, 8) + " " + CellText(row, 1);
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
