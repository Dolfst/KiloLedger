using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Alfa156.Desktop.Data;
using Alfa156.Desktop.Exchange;

namespace Alfa156.Desktop
{
    internal sealed class MainForm : Form
    {
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

        public MainForm()
        {
            Text = "Alfa 156";
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
            exchange.Controls.Add(MakeButton("Google: настройка", ConfigureGoogleSync));
            exchange.Controls.Add(MakeButton("Синхронизация Google", SyncGoogle));

            tabs.Dock = DockStyle.Fill;
            tabs.TabPages.Add(WrapGrid("Автомобили", carsGrid));
            tabs.TabPages.Add(WrapGrid("Заправки", fuelGrid));
            tabs.TabPages.Add(WrapGrid("Ремонт", repairsGrid));
            tabs.TabPages.Add(WrapGrid("Прочие", expensesGrid));
            tabs.TabPages.Add(WrapGrid("Кат. прочих", expenseCategoriesGrid));
            tabs.TabPages.Add(WrapGrid("Коммуналка", utilitiesGrid));
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
                RowCount = 2,
                Padding = new Padding(8)
            };
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
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
            summaryLayout.Controls.Add(intervalRow, 0, 0);
            summaryLayout.Controls.Add(summaryBox, 0, 1);
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

        private void ConfigureGoogleSync(object sender, EventArgs e)
        {
            GoogleSyncSettings settings = GoogleSyncSettings.Load();
            using (Form dialog = new Form())
            {
                dialog.Text = "Синхронизация Google";
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
                if (!String.IsNullOrEmpty(settings.SpreadsheetUrl))
                {
                    googleUrlBox.Text = settings.SpreadsheetUrl;
                    googleUrlBox.ForeColor = SystemColors.WindowText;
                }
                SetStatus("Настройки Google сохранены.");
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
                BindGrid(fuelGrid, repository.Fuel, 7);
                BindGrid(repairsGrid, repository.Repairs, 9);
                BindGrid(expensesGrid, repository.Expenses, 6);
                BindGrid(expenseCategoriesGrid, repository.ExpenseCategories, 1);
                BindGrid(utilitiesGrid, repository.Utilities, 8);
                BindGrid(categoriesGrid, repository.Categories, 1);
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
            repository.ReplaceAll(
                ReadGrid(carsGrid, 1),
                ReadGrid(fuelGrid, 7),
                ReadGrid(repairsGrid, 9),
                ReadGrid(utilitiesGrid, 8),
                ReadGrid(categoriesGrid, 1),
                ReadGrid(expensesGrid, 6),
                ReadGrid(expenseCategoriesGrid, 1),
                ToSettings.ToRows(intervals));
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
                repository.Repairs.Count == 0 && repository.Utilities.Count == 0)
            {
                summaryBox.Text = "Откройте папку данных, чтобы увидеть сводку и напоминания о ТО.";
                return;
            }
            string car = repository.Cars.Count > 0 ? (repository.Cars[0][0] ?? "") : "";
            summaryBox.Text = BuildSummary(car);
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

        private void SetStatus(string text)
        {
            statusLabel.Text = text;
        }

        private void ShowError(Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Alfa156 Desktop", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Ошибка: " + ex.Message);
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
