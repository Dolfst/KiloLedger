using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
        private readonly StatusStrip statusStrip = new StatusStrip();
        private readonly ToolStripStatusLabel statusLabel = new ToolStripStatusLabel("Откройте папку данных или ZIP с Android/КПК.");
        private readonly TextBox googleUrlBox = new TextBox();
        private bool suppressDirty;
        private bool dirty;

        public MainForm()
        {
            Text = "Alfa156 Desktop";
            Width = 1100;
            Height = 720;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 560);
            Font = new Font("Segoe UI", 9F);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
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
                WrapContents = false
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

            tabs.Dock = DockStyle.Fill;
            tabs.TabPages.Add(WrapGrid("Автомобили", carsGrid));
            tabs.TabPages.Add(WrapGrid("Заправки", fuelGrid));
            tabs.TabPages.Add(WrapGrid("Ремонт", repairsGrid));
            tabs.TabPages.Add(WrapGrid("Коммуналка", utilitiesGrid));
            tabs.TabPages.Add(WrapGrid("Категории", categoriesGrid));

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
            SetupUtilitiesGrid();
            SetupCategoriesGrid();

            WireDirty(carsGrid);
            WireDirty(fuelGrid);
            WireDirty(repairsGrid);
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
                Padding = new Padding(8, 2, 8, 2)
            };
            button.Click += click;
            return button;
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

        private void WireDirty(DataGridView grid)
        {
            grid.CellValueChanged += delegate { MarkDirty(); };
            grid.UserAddedRow += delegate { MarkDirty(); };
            grid.UserDeletedRow += delegate { MarkDirty(); };
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
                BindGrid(fuelGrid, repository.Fuel, 5);
                BindGrid(repairsGrid, repository.Repairs, 9);
                BindGrid(utilitiesGrid, repository.Utilities, 8);
                BindGrid(categoriesGrid, repository.Categories, 1);
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
                object[] values = new object[columns];
                for (int c = 0; c < columns; c++)
                    values[c] = c < rows[i].Length ? rows[i][c] : "";
                grid.Rows.Add(values);
            }
        }

        private void PullAll()
        {
            repository.ReplaceAll(
                ReadGrid(carsGrid, 1),
                ReadGrid(fuelGrid, 5),
                ReadGrid(repairsGrid, 9),
                ReadGrid(utilitiesGrid, 8),
                ReadGrid(categoriesGrid, 1));
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
                if (!empty)
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
