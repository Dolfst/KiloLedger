using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CarExpenses
{
    public partial class Form1 : Form
    {
        private const string FuelSection = "Заправки";
        private const string RepairSection = "Ремонт и ТО";
        private const string UtilitiesSection = "Коммунальные";

        private string currentSection;
        private string currentCar;
        private ListBox recordList;
        private List<string[]> displayedRecords;
        private List<int> displayedRecordIndexes;
        private string screenType;
        private Label homeCarLabel;
        private Panel homeLogoPanel;
        private Label screenHeading;
        private TextBox summaryBox;
        private List<Button> homeButtons;
        private string filterCategory = "Все категории";
        private string filterDateFrom = "";
        private string filterDateTo = "";
        private string filterText = "";
        private string fuelFilterDateFrom = "";
        private string fuelFilterDateTo = "";
        private string fuelFilterText = "";

        public Form1()
        {
            InitializeComponent();
            DataStore.Initialize();
            try
            {
                DataStore.CreateStartMenuShortcut();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось создать ярлык в меню «Пуск»: " + ex.Message, "Предупреждение");
            }
            currentCar = DataStore.LoadCarNames()[0];
            Resize += new EventHandler(FormResized);
            ShowHome();
        }

        private void ShowHome()
        {
            screenType = "home";
            Controls.Clear();
            homeCarLabel = null;
            homeLogoPanel = null;
            screenHeading = null;
            summaryBox = null;
            recordList = null;
            homeButtons = new List<Button>();
            Text = "Учет расходов";
            MainMenu menu = new MainMenu();
            MenuItem cars = CreateMenuItem("Авто");
            cars.MenuItems.Add(CreateMenuItem("Выбрать / добавить", new EventHandler(ShowCars)));
            menu.MenuItems.Add(cars);
            MenuItem data = CreateMenuItem("Данные");
            data.MenuItems.Add(CreateMenuItem("Создать копию в памяти КПК", new EventHandler(CreateLocalBackupFromMenu)));
            data.MenuItems.Add(CreateMenuItem("Восстановить копию из памяти КПК", new EventHandler(RestoreLocalBackupFromMenu)));
            data.MenuItems.Add(CreateMenuItem("Создать копию на SD", new EventHandler(CreateBackupFromMenu)));
            data.MenuItems.Add(CreateMenuItem("Восстановить копию", new EventHandler(RestoreBackupFromMenu)));
            data.MenuItems.Add(CreateMenuItem("Синхронизация по Wi-Fi", new EventHandler(SyncWithAndroidFromMenu)));
            menu.MenuItems.Add(data);
            menu.MenuItems.Add(CreateMenuItem("Выход", new EventHandler(ExitApplication)));
            Menu = menu;

            homeLogoPanel = new Panel();
            homeLogoPanel.BackColor = Color.FromArgb(35, 51, 70);
            homeLogoPanel.Paint += new PaintEventHandler(DrawLogo);
            Controls.Add(homeLogoPanel);

            homeCarLabel = new Label();
            homeCarLabel.Text = "Автомобиль: " + currentCar;
            homeCarLabel.BackColor = Color.FromArgb(232, 238, 244);
            homeCarLabel.ForeColor = Color.FromArgb(35, 51, 70);
            homeCarLabel.Font = new Font("Arial", 10, FontStyle.Bold);
            Controls.Add(homeCarLabel);

            AddHomeButton(FuelSection);
            AddHomeButton(RepairSection);
            AddHomeButton(UtilitiesSection);
            AddHomeButton("Сводка и ТО");
            LayoutScreen();
        }

        private void AddHomeButton(string section)
        {
            Button button = new Button();
            button.Text = "  " + section;
            button.Tag = section;
            if (section == FuelSection)
                button.BackColor = Color.FromArgb(23, 122, 86);
            else if (section == RepairSection)
                button.BackColor = Color.FromArgb(196, 101, 30);
            else if (section == UtilitiesSection)
                button.BackColor = Color.FromArgb(79, 104, 179);
            else
                button.BackColor = Color.FromArgb(130, 88, 180);
            button.ForeColor = Color.White;
            button.Font = new Font("Arial", 11, FontStyle.Bold);
            button.Click += new EventHandler(OpenSection);
            Controls.Add(button);
            homeButtons.Add(button);
        }

        private void DrawLogo(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(35, 51, 70));
            using (SolidBrush badge = new SolidBrush(Color.FromArgb(160, 32, 45)))
            using (SolidBrush whiteBrush = new SolidBrush(Color.White))
            using (Font markFont = new Font("Arial", 17, FontStyle.Bold))
            using (Font titleFont = new Font("Arial", 13, FontStyle.Bold))
            {
                e.Graphics.FillEllipse(badge, 8, 8, 36, 36);
                e.Graphics.DrawString("A", markFont, whiteBrush, 16, 8);
                e.Graphics.DrawString("ALFA 156", titleFont, whiteBrush, 56, 15);
            }
        }

        private void OpenSection(object sender, EventArgs e)
        {
            string section = (string)((Control)sender).Tag;
            if (section == "Сводка и ТО")
                ShowSummary();
            else
                ShowRecords(section);
        }

        private void ShowRecords(string section)
        {
            screenType = "records";
            homeCarLabel = null;
            homeLogoPanel = null;
            screenHeading = null;
            summaryBox = null;
            homeButtons = null;
            currentSection = section;
            Controls.Clear();
            Text = section;

            screenHeading = new Label();
            string heading = section == UtilitiesSection ? section : section + ": " + currentCar;
            bool filterOn = (section == FuelSection &&
                    (fuelFilterDateFrom != "" || fuelFilterDateTo != "" || fuelFilterText != "")) ||
                (section == RepairSection &&
                    (filterCategory != "Все категории" || filterDateFrom != "" ||
                     filterDateTo != "" || filterText != ""));
            if (filterOn)
                heading += " [фильтр]";
            screenHeading.Text = heading;
            StyleHeading(screenHeading);
            Controls.Add(screenHeading);

            recordList = new ListBox();
            recordList.BackColor = Color.White;
            recordList.ForeColor = Color.FromArgb(35, 51, 70);
            Controls.Add(recordList);

            displayedRecords = new List<string[]>();
            displayedRecordIndexes = new List<int>();
            List<string[]> records;
            try
            {
                if (section == FuelSection)
                    records = DataStore.LoadFuel();
                else if (section == RepairSection)
                    records = DataStore.LoadRepairs();
                else
                    records = DataStore.LoadUtilities();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось прочитать записи: " + ex.Message, "Ошибка");
                ShowHome();
                return;
            }

            for (int i = records.Count - 1; i >= 0; i--)
            {
                string[] row = records[i];
                if (section != UtilitiesSection && row[1] != currentCar)
                    continue;
                if (section == RepairSection && !MatchesRepairFilter(row))
                    continue;
                if (section == FuelSection && !MatchesFuelFilter(row))
                    continue;

                displayedRecords.Add(row);
                displayedRecordIndexes.Add(i);
                if (section == FuelSection)
                    recordList.Items.Add(FormatFuel(row, records));
                else if (section == RepairSection)
                    recordList.Items.Add(row[0] + " " + row[3] + ": " +
                        ShortText(row[4], 18) + " " + row[7] + " BYN");
                else
                    recordList.Items.Add(row[0] + "  " + UtilityTotal(row).ToString("0.##", CultureInfo.InvariantCulture) + " BYN");
            }
            if (recordList.Items.Count == 0)
            {
                bool hasFilter = (section == FuelSection &&
                        (fuelFilterDateFrom != "" || fuelFilterDateTo != "" || fuelFilterText != "")) ||
                    (section == RepairSection &&
                        (filterCategory != "Все категории" || filterDateFrom != "" ||
                         filterDateTo != "" || filterText != ""));
                recordList.Items.Add(hasFilter
                    ? "Нет записей по фильтру"
                    : "Нет записей. Меню Действия - Добавить");
            }

            MainMenu menu = new MainMenu();
            MenuItem actions = CreateMenuItem("Действия");
            actions.MenuItems.Add(CreateMenuItem("Добавить", new EventHandler(AddCurrentRecord)));
            actions.MenuItems.Add(CreateMenuItem("Подробно", new EventHandler(ShowRecordDetails)));
            actions.MenuItems.Add(CreateMenuItem("Изменить", new EventHandler(EditCurrentRecord)));
            actions.MenuItems.Add(CreateMenuItem("Удалить", new EventHandler(DeleteCurrentRecord)));
            if (section == FuelSection)
            {
                actions.MenuItems.Add(CreateMenuItem("Фильтр", new EventHandler(EditFuelFilter)));
                actions.MenuItems.Add(CreateMenuItem("Сбросить фильтр", new EventHandler(ResetFuelFilter)));
            }
            else if (section == RepairSection)
            {
                actions.MenuItems.Add(CreateMenuItem("Фильтр", new EventHandler(EditRepairFilter)));
                actions.MenuItems.Add(CreateMenuItem("Сбросить фильтр", new EventHandler(ResetRepairFilter)));
                actions.MenuItems.Add(CreateMenuItem("Добавить категорию", new EventHandler(AddRepairCategory)));
                actions.MenuItems.Add(CreateMenuItem("Переименовать категорию", new EventHandler(RenameRepairCategory)));
                actions.MenuItems.Add(CreateMenuItem("Удалить категорию", new EventHandler(DeleteRepairCategory)));
            }
            menu.MenuItems.Add(actions);
            menu.MenuItems.Add(CreateMenuItem("Назад", new EventHandler(ShowHomeFromMenu)));
            Menu = menu;
            LayoutScreen();
        }

        private bool MatchesRepairFilter(string[] row)
        {
            if (filterCategory != "Все категории" &&
                !String.Equals(row[3], filterCategory, StringComparison.OrdinalIgnoreCase))
                return false;
            if (filterDateFrom != "" && String.CompareOrdinal(row[0], filterDateFrom) < 0)
                return false;
            if (filterDateTo != "" && String.CompareOrdinal(row[0], filterDateTo) > 0)
                return false;
            if (filterText != "")
            {
                string searchableText = (row[4] + " " + row[5]).ToLower(CultureInfo.CurrentCulture);
                if (searchableText.IndexOf(filterText.ToLower(CultureInfo.CurrentCulture)) < 0)
                    return false;
            }
            return true;
        }

        private bool MatchesFuelFilter(string[] row)
        {
            if (fuelFilterDateFrom != "" &&
                String.CompareOrdinal(row[0], fuelFilterDateFrom) < 0)
                return false;
            if (fuelFilterDateTo != "" &&
                String.CompareOrdinal(row[0], fuelFilterDateTo) > 0)
                return false;
            if (fuelFilterText != "")
            {
                string searchableText = (row[2] + " " + row[3] + " " + row[4] + " " +
                    FieldAt(row, 5) + " " + FieldAt(row, 6))
                    .ToLower(CultureInfo.CurrentCulture);
                if (searchableText.IndexOf(
                    fuelFilterText.ToLower(CultureInfo.CurrentCulture)) < 0)
                    return false;
            }
            return true;
        }

        private void EditFuelFilter(object sender, EventArgs e)
        {
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Дата с (гггг-мм-дд)", fuelFilterDateFrom, "date", false),
                new EntryField("Дата по (гггг-мм-дд)", fuelFilterDateTo, "date", false),
                new EntryField("Пробег / литры / стоимость", fuelFilterText, "text", false)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Фильтр заправок", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] values = editor.Values;
                if (values[0] != "" && values[1] != "" &&
                    String.CompareOrdinal(values[0], values[1]) > 0)
                {
                    MessageBox.Show("Начало периода не может быть позже его окончания.", "Фильтр");
                    return;
                }
                fuelFilterDateFrom = values[0];
                fuelFilterDateTo = values[1];
                fuelFilterText = values[2];
            }
            ShowRecords(FuelSection);
        }

        private void ResetFuelFilter(object sender, EventArgs e)
        {
            fuelFilterDateFrom = "";
            fuelFilterDateTo = "";
            fuelFilterText = "";
            ShowRecords(FuelSection);
        }

        private void EditRepairFilter(object sender, EventArgs e)
        {
            List<string> categories = new List<string>();
            categories.Add("Все категории");
            categories.AddRange(DataStore.LoadRepairCategories());
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Категория", filterCategory, categories.ToArray(), true),
                new EntryField("Дата с (гггг-мм-дд)", filterDateFrom, "date", false),
                new EntryField("Дата по (гггг-мм-дд)", filterDateTo, "date", false),
                new EntryField("Название / товар", filterText, "text", false)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Фильтр ремонта", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] values = editor.Values;
                if (values[1] != "" && values[2] != "" &&
                    String.CompareOrdinal(values[1], values[2]) > 0)
                {
                    MessageBox.Show("Начало периода не может быть позже его окончания.", "Фильтр");
                    return;
                }
                filterCategory = values[0];
                filterDateFrom = values[1];
                filterDateTo = values[2];
                filterText = values[3];
            }
            ShowRecords(RepairSection);
        }

        private void ResetRepairFilter(object sender, EventArgs e)
        {
            filterCategory = "Все категории";
            filterDateFrom = "";
            filterDateTo = "";
            filterText = "";
            ShowRecords(RepairSection);
        }

        private void AddRepairCategory(object sender, EventArgs e)
        {
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Название", "", "text", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Новая категория", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] values = editor.Values;
                if (!TrySave(delegate { DataStore.AddRepairCategory(values[0]); }))
                    return;
            }
            ShowRecords(RepairSection);
        }

        private void RenameRepairCategory(object sender, EventArgs e)
        {
            string[] categories = DataStore.LoadRepairCategories();
            if (categories.Length == 0)
            {
                MessageBox.Show("Нет категорий для переименования.", "Категории");
                return;
            }
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Категория", categories[0], categories, true),
                new EntryField("Новое название", "", "text", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Переименовать категорию", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] values = editor.Values;
                if (!TrySave(delegate
                {
                    DataStore.RenameRepairCategory(values[0], values[1]);
                }))
                    return;
                if (filterCategory == values[0])
                    filterCategory = values[1].Trim();
            }
            ShowRecords(RepairSection);
        }

        private void DeleteRepairCategory(object sender, EventArgs e)
        {
            string[] categories = DataStore.LoadRepairCategories();
            if (categories.Length == 0)
            {
                MessageBox.Show("Нет категорий для удаления.", "Категории");
                return;
            }
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Категория", categories[0], categories, true)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Удалить категорию", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string name = editor.Values[0];
                if (!ConfirmAction("Подтверждение",
                    "Записи не удаляются. Если категория используется, сначала перенесите записи в другую. Удалить категорию?",
                    "Удалить"))
                    return;
                if (!TrySave(delegate { DataStore.DeleteRepairCategory(name); }))
                    return;
                if (filterCategory == name)
                    filterCategory = "Все категории";
            }
            ShowRecords(RepairSection);
        }

        private string FormatFuel(string[] row, List<string[]> allRows)
        {
            string result = row[0] + "  " + row[2] + " км  " + row[3] + " л  " + row[4] + " BYN";
            decimal consumption = FuelConsumption(row, allRows);
            if (consumption > 0)
                result += "  " + consumption.ToString("0.0", CultureInfo.InvariantCulture) + " л/100";
            return result;
        }

        private decimal FuelConsumption(string[] row, List<string[]> allRows)
        {
            decimal mileage;
            if (!DataStore.TryNumber(row[2], out mileage))
                return 0;

            for (int i = allRows.IndexOf(row) - 1; i >= 0; i--)
            {
                if (allRows[i][1] != row[1])
                    continue;

                decimal previousMileage;
                decimal liters;
                if (DataStore.TryNumber(allRows[i][2], out previousMileage) &&
                    DataStore.TryNumber(row[3], out liters) &&
                    mileage > previousMileage)
                    return liters * 100 / (mileage - previousMileage);
            }
            return 0;
        }

        private static string ShortText(string value, int maxLength)
        {
            if (value == null)
                return "";
            string text = value.Replace("\r", " ").Replace("\n", " ");
            if (text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength - 3) + "...";
        }

        private decimal UtilityTotal(string[] row)
        {
            decimal total = 0;
            for (int i = 1; i < row.Length && i <= 7; i++)
            {
                decimal value;
                if (DataStore.TryNumber(row[i], out value))
                    total += value;
            }
            return total;
        }

        private bool HasSelectedRecord()
        {
            return recordList != null && displayedRecords != null &&
                recordList.SelectedIndex >= 0 &&
                recordList.SelectedIndex < displayedRecords.Count;
        }

        private void ShowRecordDetails(object sender, EventArgs e)
        {
            if (!HasSelectedRecord())
            {
                MessageBox.Show("Сначала выберите запись.", "Учет");
                return;
            }

            string[] row = displayedRecords[recordList.SelectedIndex];
            string details;
            if (currentSection == FuelSection)
            {
                details = "Авто: " + row[1] + "\r\nДата: " + row[0] +
                    "\r\nПробег: " + row[2] + " км\r\nЛитры: " + row[3] +
                    "\r\nСтоимость: " + row[4] + " BYN";
                string storedPrice = FieldAt(row, 5).Trim();
                decimal liters;
                decimal cost;
                if (storedPrice.Length > 0)
                    details += "\r\nЦена за литр: " + storedPrice + " BYN";
                else if (DataStore.TryNumber(row[3], out liters) && liters > 0 &&
                    DataStore.TryNumber(row[4], out cost))
                    details += "\r\nЦена за литр: " +
                        (cost / liters).ToString("0.00", CultureInfo.InvariantCulture) + " BYN";
                string comment = FieldAt(row, 6).Trim();
                if (comment.Length > 0)
                    details += "\r\nАЗС / комментарий: " + comment;
                decimal consumption = FuelConsumption(row, DataStore.LoadFuel());
                if (consumption > 0)
                    details += "\r\nРасход: " +
                        consumption.ToString("0.0", CultureInfo.InvariantCulture) + " л/100 км";
            }
            else if (currentSection == RepairSection)
            {
                details = "Авто: " + row[1] + "\r\nДата: " + row[0] +
                    "\r\nПробег: " + row[2] + " км\r\nКатегория: " + row[3] +
                    "\r\nТовар: " + row[4] + "\r\nНаименование: " + row[5] +
                    "\r\nКоличество: " + row[6] + "\r\nСумма: " + row[7] +
                    " BYN\r\nКомментарий: " + row[8];
            }
            else
            {
                string[] labels = DataStore.UtilityLabels;
                details = row[0] + "\r\n";
                for (int i = 0; i < labels.Length; i++)
                    details += labels[i] + ": " + row[i + 1] + " BYN\r\n";
                details += "Итого: " + UtilityTotal(row).ToString("0.##", CultureInfo.InvariantCulture) + " BYN";
            }
            MessageBox.Show(details, "Запись");
        }

        private void EditCurrentRecord(object sender, EventArgs e)
        {
            if (!HasSelectedRecord())
            {
                MessageBox.Show("Сначала выберите запись.", "Учет");
                return;
            }

            int selectedIndex = recordList.SelectedIndex;
            int sourceIndex = displayedRecordIndexes[selectedIndex];
            string[] row = displayedRecords[selectedIndex];

            if (currentSection == FuelSection)
            {
                EntryField[] fields = new EntryField[]
                {
                    new EntryField("Дата (гггг-мм-дд)", row[0], "date", true),
                    new EntryField("Автомобиль", row[1], DataStore.LoadCarNames().ToArray(), true),
                    new EntryField("Пробег, км", row[2], "number", true),
                    new EntryField("Литры", row[3], "number", true),
                    new EntryField("Стоимость, BYN", row[4], "number", true),
                    new EntryField("Цена за литр, BYN", FieldAt(row, 5), "number", false),
                    new EntryField("АЗС / комментарий", FieldAt(row, 6), "text", false)
                };
                using (EntryEditorForm editor = new EntryEditorForm("Изменить заправку", fields))
                {
                    if (editor.ShowDialog() != DialogResult.OK)
                        return;
                    string[] values = NormalizeFuelValues(editor.Values);
                    if (!TrySave(delegate { DataStore.UpdateFuelAt(sourceIndex, values); }))
                        return;
                    currentCar = values[1];
                }
            }
            else if (currentSection == RepairSection)
            {
                EntryField[] fields = new EntryField[]
                {
                    new EntryField("Дата (гггг-мм-дд)", row[0], "date", true),
                    new EntryField("Автомобиль", row[1], DataStore.LoadCarNames().ToArray(), true),
                    new EntryField("Пробег, км", row[2], "number", false),
                    new EntryField("Категория", row[3], DataStore.LoadRepairCategories(), true),
                    new EntryField("Товар / деталь", row[4], "text", true),
                    new EntryField("Наименование", row[5], "text", false),
                    new EntryField("Количество", row[6], "number", true),
                    new EntryField("Сумма, BYN", row[7], "number", true),
                    new EntryField("Комментарий", row[8], "text", false)
                };
                using (EntryEditorForm editor = new EntryEditorForm("Изменить ремонт", fields))
                {
                    if (editor.ShowDialog() != DialogResult.OK)
                        return;
                    string[] values = editor.Values;
                    if (!TrySave(delegate { DataStore.UpdateRepairAt(sourceIndex, values); }))
                        return;
                    currentCar = values[1];
                }
            }
            else
            {
                EntryField[] fields = CreateUtilityFields(row);
                using (EntryEditorForm editor = new EntryEditorForm("Изменить коммунальные", fields))
                {
                    if (editor.ShowDialog() != DialogResult.OK)
                        return;
                    string[] values = editor.Values;
                    if (!TrySave(delegate { DataStore.UpdateUtilityAt(sourceIndex, values); }))
                        return;
                }
            }

            ShowRecords(currentSection);
        }

        private EntryField[] CreateUtilityFields(string[] row)
        {
            EntryField[] fields = new EntryField[8];
            fields[0] = new EntryField("Месяц (гггг-мм)", row[0], "month", true);
            for (int i = 0; i < DataStore.UtilityLabels.Length; i++)
                fields[i + 1] = new EntryField(DataStore.UtilityLabels[i] + ", BYN",
                    row[i + 1], "number", false);
            return fields;
        }

        private void DeleteCurrentRecord(object sender, EventArgs e)
        {
            if (!HasSelectedRecord())
            {
                MessageBox.Show("Сначала выберите запись.", "Учет");
                return;
            }

            int selectedIndex = recordList.SelectedIndex;
            string[] row = displayedRecords[selectedIndex];
            string description;
            if (currentSection == FuelSection)
                description = row[0] + ", " + row[2] + " км, " + row[4] + " BYN";
            else if (currentSection == RepairSection)
                description = row[0] + ", " + row[3] + ": " + ShortText(row[4], 24);
            else
                description = "коммунальные платежи за " + row[0];

            if (!ConfirmDelete(description))
                return;

            int sourceIndex = displayedRecordIndexes[selectedIndex];
            bool saved;
            if (currentSection == FuelSection)
                saved = TrySave(delegate { DataStore.DeleteFuelAt(sourceIndex); });
            else if (currentSection == RepairSection)
                saved = TrySave(delegate { DataStore.DeleteRepairAt(sourceIndex); });
            else
                saved = TrySave(delegate { DataStore.DeleteUtilityAt(sourceIndex); });

            if (saved)
                ShowRecords(currentSection);
        }

        private bool ConfirmDelete(string description)
        {
            return ConfirmAction("Подтверждение удаления",
                "Удалить запись?\r\n" + description, "Удалить");
        }

        private bool ConfirmAction(string title, string messageText, string acceptText)
        {
            using (Form confirmation = new Form())
            {
                confirmation.Text = title;
                confirmation.ClientSize = new Size(240, 150);
                confirmation.MinimizeBox = false;
                confirmation.WindowState = FormWindowState.Maximized;

                Label message = new Label();
                message.Text = messageText;
                message.Location = new Point(10, 10);
                message.Size = new Size(confirmation.ClientSize.Width - 20,
                    confirmation.ClientSize.Height - 90);
                confirmation.Controls.Add(message);

                int buttonLeft = (confirmation.ClientSize.Width - 210) / 2;
                int buttonTop = confirmation.ClientSize.Height - 55;
                Button delete = new Button();
                delete.Text = acceptText;
                delete.Location = new Point(buttonLeft, buttonTop);
                delete.Size = new Size(100, 40);
                delete.Click += delegate { confirmation.DialogResult = DialogResult.Yes; };
                confirmation.Controls.Add(delete);

                Button cancel = new Button();
                cancel.Text = "Отмена";
                cancel.Location = new Point(buttonLeft + 110, buttonTop);
                cancel.Size = new Size(100, 40);
                cancel.Click += delegate { confirmation.DialogResult = DialogResult.No; };
                confirmation.Controls.Add(cancel);

                return confirmation.ShowDialog() == DialogResult.Yes;
            }
        }

        private void CreateBackupFromMenu(object sender, EventArgs e)
        {
            try
            {
                string path = DataStore.CreateBackup();
                MessageBox.Show("Резервная копия создана:\r\n" + path +
                    "\r\nСкопируйте эту папку на компьютер через ActiveSync.",
                    "Резервная копия");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось создать копию на SD-карте: " + ex.Message,
                    "Ошибка резервного копирования");
            }
        }

        private void CreateLocalBackupFromMenu(object sender, EventArgs e)
        {
            try
            {
                string path = DataStore.CreateLocalBackup();
                MessageBox.Show("Локальная резервная копия создана:\r\n" + path,
                    "Резервная копия");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось создать локальную копию: " + ex.Message,
                    "Ошибка резервного копирования");
            }
        }

        private void RestoreLocalBackupFromMenu(object sender, EventArgs e)
        {
            string[] backups;
            try
            {
                backups = DataStore.ListLocalBackupNames();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось прочитать локальные копии: " + ex.Message,
                    "Ошибка восстановления");
                return;
            }

            if (backups.Length == 0)
            {
                MessageBox.Show("В памяти КПК нет полных резервных копий Alfa 156.",
                    "Восстановление");
                return;
            }

            EntryField[] fields = new EntryField[]
            {
                new EntryField("Выберите дату копии", backups[0], backups, true)
            };
            string backupName;
            using (EntryEditorForm editor = new EntryEditorForm("Локальная копия", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                backupName = editor.Values[0];
            }

            if (!ConfirmAction("Подтверждение восстановления",
                "Текущие данные сначала сохранятся в отдельную локальную копию.\r\nЗаменить их копией " +
                backupName + "?", "Восстановить"))
                return;

            try
            {
                DataStore.RestoreLocalBackup(backupName);
                currentCar = DataStore.LoadCarNames()[0];
                ShowHome();
                MessageBox.Show("Данные восстановлены из локальной копии " + backupName + ".",
                    "Восстановление завершено");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось восстановить локальную копию: " + ex.Message,
                    "Ошибка восстановления");
            }
        }

        private void RestoreBackupFromMenu(object sender, EventArgs e)
        {
            string[] backups;
            try
            {
                backups = DataStore.ListBackupNames();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось прочитать копии с SD-карты: " + ex.Message,
                    "Ошибка восстановления");
                return;
            }

            if (backups.Length == 0)
            {
                MessageBox.Show("На SD-карте нет полных резервных копий Alfa 156.",
                    "Восстановление");
                return;
            }

            EntryField[] fields = new EntryField[]
            {
                new EntryField("Выберите дату копии", backups[0], backups, true)
            };
            string backupName;
            using (EntryEditorForm editor = new EntryEditorForm("Выбор копии", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                backupName = editor.Values[0];
            }

            if (!ConfirmAction("Подтверждение восстановления",
                "Текущие данные сначала сохранятся отдельной копией на SD-карту.\r\nЗаменить их копией " +
                backupName + "?", "Восстановить"))
                return;

            try
            {
                DataStore.RestoreBackup(backupName);
                currentCar = DataStore.LoadCarNames()[0];
                ShowHome();
                MessageBox.Show("Данные восстановлены из копии " + backupName + ".",
                    "Восстановление завершено");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось восстановить данные: " + ex.Message,
                    "Ошибка восстановления");
            }
        }

        private void SyncWithAndroidFromMenu(object sender, EventArgs e)
        {
            string suggestedIp = DataStore.LoadLastSyncAddress();
            if (suggestedIp == null || suggestedIp.Length == 0)
                suggestedIp = "192.168.43.1";
            EntryField[] fields = new EntryField[]
            {
                new EntryField("IP Android (точка доступа обычно 192.168.43.1)", suggestedIp, "text", true),
                new EntryField("Код подключения", "", "text", true)
            };
            string address;
            string pairingCode;
            using (EntryEditorForm editor = new EntryEditorForm("Синхронизация по Wi-Fi", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                address = editor.Values[0].Trim();
                pairingCode = editor.Values[1].Trim();
            }
            DataStore.SaveLastSyncAddress(address);

            if (pairingCode.Length != 6 || !IsDigits(pairingCode))
            {
                MessageBox.Show("Введите шестизначный код с экрана Android.",
                    "Синхронизация");
                return;
            }

            bool synced = false;
            Exception lastError = null;
            IntPtr route = IntPtr.Zero;
            Cursor previousCursor = Cursor.Current;
            try
            {
                for (int attempt = 0; attempt < 2 && !synced; attempt++)
                {
                    if (attempt == 1)
                    {
                        Cursor.Current = previousCursor;
                        route = DataStore.OpenRoute(address);
                    }

                    Cursor.Current = Cursors.WaitCursor;
                    using (TcpClient client = new TcpClient())
                    {
                        bool connected = false;
                        try
                        {
                            // Windows Mobile не поддерживает SO_SNDTIMEO/SO_RCVTIMEO.
                            // Установка SendTimeout или ReceiveTimeout бросает SocketException,
                            // а без пакета сообщений .NET CF на экране остаётся только просьба
                            // установить NETCFv35.Messages.
                            ConnectWithin(client, address, 45816, attempt == 0 ? 8000 : 20000);
                            connected = true;
                            try
                            {
                                client.NoDelay = true;
                            }
                            catch (SocketException)
                            {
                            }

                            DataStore.BindSyncSocket(client.Client, 30 * 1000 * 1000);
                            NetworkStream stream = client.GetStream();
                            byte[] handshake = Encoding.UTF8.GetBytes("ALFA156SYNC1|" +
                                pairingCode + "\n");
                            stream.Write(handshake, 0, handshake.Length);
                            stream.Flush();

                            int status = DataStore.ReadSyncInt32(stream);
                            if (status != 0)
                                throw new IOException(DataStore.ReadSyncMessage(stream));

                            DataStore.BindSyncSocket(client.Client, 5 * 60 * 1000 * 1000);
                            DataStore.WriteSyncSnapshot(stream);
                            status = DataStore.ReadSyncInt32(stream);
                            if (status != 0)
                                throw new IOException(DataStore.ReadSyncMessage(stream));

                            Cursor.Current = Cursors.Default;
                            DataStore.MergeSyncSnapshot(
                                stream,
                                new SyncPlanResolver(ResolveAndroidSyncPlan),
                                new SyncConflictResolver(ResolveAndroidSyncConflict));
                            synced = true;
                        }
                        catch (Exception ex)
                        {
                            lastError = ex;
                            if (attempt == 0 && !connected && ShouldRetryWithRoute(ex))
                                continue;
                            break;
                        }
                        finally
                        {
                            DataStore.BindSyncSocket(null, 0);
                        }
                    }
                }
            }
            finally
            {
                Cursor.Current = previousCursor;
                DataStore.CloseRoute(route);
            }

            if (!synced)
            {
                string detail = ExplainSyncFailure(lastError, address);
                if (detail != null && detail.IndexOf("отменена") >= 0)
                {
                    MessageBox.Show("Синхронизация отменена.", "Синхронизация");
                    return;
                }
                MessageBox.Show("Синхронизация не выполнена: " + detail,
                    "Ошибка синхронизации");
                return;
            }

            currentCar = DataStore.LoadCarNames()[0];
            ShowHome();
            MessageBox.Show("Синхронизация завершена.", "Синхронизация");
        }

        private SyncPlan ResolveAndroidSyncPlan(string summary)
        {
            using (SyncOptionsForm form = new SyncOptionsForm(summary))
            {
                if (form.ShowDialog() != DialogResult.OK)
                {
                    SyncPlan cancelled = new SyncPlan();
                    cancelled.Cancelled = true;
                    return cancelled;
                }
                return form.Plan;
            }
        }

        private SyncConflictChoice ResolveAndroidSyncConflict(
            string title, string localRow, string incomingRow)
        {
            DialogResult result = MessageBox.Show(
                "На КПК:\n" + localRow + "\n\nНа Android:\n" + incomingRow +
                "\n\nДа = Android, Нет = КПК, Отмена = прервать.",
                "Конфликт: " + title,
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (result == DialogResult.Yes)
                return SyncConflictChoice.KeepIncoming;
            if (result == DialogResult.No)
                return SyncConflictChoice.KeepLocal;
            return SyncConflictChoice.Cancel;
        }

        private sealed class ConnectAttempt
        {
            internal TcpClient Client;
            internal bool TimedOut;
            internal Exception Error;
            internal readonly object Gate = new object();
            internal ManualResetEvent Finished = new ManualResetEvent(false);
        }

        private static void ConnectWithin(TcpClient client, string host, int port, int timeoutMs)
        {
            ConnectAttempt attempt = new ConnectAttempt();
            attempt.Client = client;
            try
            {
                IPAddress parsed;
                if (!TryParseIpAddress(host, out parsed))
                {
                    client.Connect(host, port);
                    return;
                }

                try
                {
                    // CF: BeginConnect только с EndPoint (не host/IP+port).
                    IPEndPoint endPoint = new IPEndPoint(parsed, port);
                    client.Client.BeginConnect(endPoint,
                        new AsyncCallback(FinishConnect), attempt);
                }
                catch (NotSupportedException)
                {
                    client.Connect(host, port);
                    return;
                }

                if (!attempt.Finished.WaitOne(timeoutMs, false))
                {
                    lock (attempt.Gate)
                    {
                        // EndConnect мог успеть завершиться, пока WaitOne уже вернул false.
                        bool open = false;
                        if (attempt.Error == null)
                        {
                            try
                            {
                                open = client.Client != null && client.Client.Connected;
                            }
                            catch (Exception)
                            {
                                open = false;
                            }
                        }
                        if (open)
                            return;
                        attempt.TimedOut = true;
                        try
                        {
                            client.Close();
                        }
                        catch (Exception)
                        {
                        }
                    }
                    throw new IOException("Нет ответа от " + host + ":" +
                        port.ToString(CultureInfo.InvariantCulture) +
                        ". На Android откройте Данные → Синхронизация по Wi-Fi и введите IP с этого экрана. " +
                        "КПК и телефон должны быть в одной Wi-Fi сети.");
                }
                if (attempt.Error != null)
                    throw attempt.Error;
            }
            finally
            {
                try
                {
                    attempt.Finished.Close();
                }
                catch (Exception)
                {
                }
            }
        }

        private static bool TryParseIpAddress(string host, out IPAddress address)
        {
            address = null;
            if (host == null || host.Length == 0)
                return false;
            try
            {
                address = IPAddress.Parse(host);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static void FinishConnect(IAsyncResult result)
        {
            ConnectAttempt attempt = (ConnectAttempt)result.AsyncState;
            lock (attempt.Gate)
            {
                try
                {
                    attempt.Client.Client.EndConnect(result);
                }
                catch (Exception ex)
                {
                    attempt.Error = ex;
                }
                if (attempt.TimedOut || attempt.Error != null)
                {
                    try
                    {
                        attempt.Client.Close();
                    }
                    catch (Exception)
                    {
                    }
                }
                try
                {
                    attempt.Finished.Set();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        private static bool ShouldRetryWithRoute(Exception ex)
        {
            SocketException socket = FindSocketException(ex);
            if (socket != null)
            {
                int code = SocketCode(socket);
                if (code == 10061 || code == 11001 || code == 11004)
                    return false;
                return true;
            }

            IOException io = ex as IOException;
            return io != null && io.Message != null && io.Message.StartsWith("Нет ответа от ");
        }

        private static string ExplainSyncFailure(Exception ex, string address)
        {
            if (ex == null)
                return "неизвестная ошибка";

            SocketException socket = FindSocketException(ex);
            if (socket != null)
            {
                int code = SocketCode(socket);
                if (code == 10060 || code == 10065 || code == 10051 || code == 10050 || code == 10049)
                    return "КПК не достучался до " + address +
                        ". Подключите КПК к той же Wi-Fi сети, что и телефон, или к точке доступа телефона, " +
                        "и введите IP с экрана синхронизации Android.";
                if (code == 10061)
                    return "Телефон отклонил подключение. На Android ещё раз откройте Данные → Синхронизация по Wi-Fi " +
                        "и не закрывайте это окно.";
                if (code == 11001 || code == 11004)
                    return "Адрес «" + address + "» не найден. Введите IP с экрана Android, например 192.168.1.20.";
                return "Подключение к " + address + " не удалось, код " +
                    code.ToString(CultureInfo.InvariantCulture) + ".";
            }
            return VisibleExceptionText(ex);
        }

        private static SocketException FindSocketException(Exception ex)
        {
            while (ex != null)
            {
                SocketException socket = ex as SocketException;
                if (socket != null)
                    return socket;
                ex = ex.InnerException;
            }
            return null;
        }

        private static int SocketCode(SocketException socket)
        {
            if (socket.ErrorCode != 0)
                return socket.ErrorCode;
            return socket.NativeErrorCode;
        }

        private static string VisibleExceptionText(Exception ex)
        {
            string message = ex.Message;
            if (message != null && message.Length > 0 &&
                message.IndexOf("NETCFv35.Messages") < 0)
                return message;

            string text = ex.GetType().Name;
            SocketException socket = ex as SocketException;
            if (socket != null)
                text += ", код " + SocketCode(socket).ToString(CultureInfo.InvariantCulture);
            if (ex.InnerException != null)
                text += " / " + VisibleExceptionText(ex.InnerException);
            return text;
        }

        private static bool IsDigits(string value)
        {
            for (int i = 0; i < value.Length; i++)
                if (value[i] < '0' || value[i] > '9')
                    return false;
            return true;
        }

        private void AddCurrentRecord(object sender, EventArgs e)
        {
            if (currentSection == FuelSection)
                AddFuel();
            else if (currentSection == RepairSection)
                AddRepair();
            else
                AddUtility();
        }

        private void AddFuel()
        {
            string[] cars = DataStore.LoadCarNames().ToArray();
            string lastMileage = LastCarMileage(currentCar);
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Дата (гггг-мм-дд)", DateTime.Now.ToString("yyyy-MM-dd"), "date", true),
                new EntryField("Автомобиль", currentCar, cars, true),
                new EntryField(lastMileage.Length > 0
                    ? "Пробег, км (последняя запись)"
                    : "Пробег, км", lastMileage, "number", true),
                new EntryField("Литры", "", "number", true),
                new EntryField("Стоимость, BYN", "", "number", true),
                new EntryField("Цена за литр, BYN", "", "number", false),
                new EntryField("АЗС / комментарий", "", "text", false)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Новая заправка", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] values = NormalizeFuelValues(editor.Values);
                if (!TrySave(delegate { DataStore.AddFuel(values); }))
                    return;
                currentCar = values[1];
            }
            ShowRecords(FuelSection);
        }

        private static string LastCarMileage(string car)
        {
            decimal best = -1;
            string bestText = "";
            RememberLatestMileage(DataStore.LoadFuel(), car, ref best, ref bestText);
            RememberLatestMileage(DataStore.LoadRepairs(), car, ref best, ref bestText);
            return bestText;
        }

        private static void RememberLatestMileage(List<string[]> rows, string car,
            ref decimal best, ref string bestText)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Length < 3 || rows[i][1] != car)
                    continue;
                decimal mileage;
                if (!DataStore.TryNumber(rows[i][2], out mileage) || mileage < 0)
                    continue;
                if (mileage >= best)
                {
                    best = mileage;
                    bestText = rows[i][2];
                }
            }
        }

        private void AddRepair()
        {
            string[] cars = DataStore.LoadCarNames().ToArray();
            string[] categories = DataStore.LoadRepairCategories();
            if (categories.Length == 0)
            {
                MessageBox.Show("Сначала добавьте категорию ремонта.", "Категории");
                return;
            }
            string lastMileage = LastCarMileage(currentCar);
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Дата (гггг-мм-дд)", DateTime.Now.ToString("yyyy-MM-dd"), "date", true),
                new EntryField("Автомобиль", currentCar, cars, true),
                new EntryField(lastMileage.Length > 0
                    ? "Пробег, км (последняя запись)"
                    : "Пробег, км", lastMileage, "number", false),
                new EntryField("Категория", categories[0], categories, true),
                new EntryField("Товар / деталь", "", "text", true),
                new EntryField("Наименование", "", "text", false),
                new EntryField("Количество", "1", "number", true),
                new EntryField("Сумма, BYN", "", "number", true),
                new EntryField("Комментарий", "", "text", false)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Новый ремонт", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] values = editor.Values;
                if (!TrySave(delegate { DataStore.AddRepair(values); }))
                    return;
                currentCar = values[1];
            }
            ShowRecords(RepairSection);
        }

        private void AddUtility()
        {
            EntryField[] fields = new EntryField[8];
            fields[0] = new EntryField("Месяц (гггг-мм)", DateTime.Now.ToString("yyyy-MM"), "month", true);
            for (int i = 0; i < DataStore.UtilityLabels.Length; i++)
                fields[i + 1] = new EntryField(DataStore.UtilityLabels[i] + ", BYN", "", "number", false);

            using (EntryEditorForm editor = new EntryEditorForm("Коммунальные", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] values = editor.Values;
                if (DataStore.ContainsUtilityMonth(values[0]))
                {
                    MessageBox.Show("Этот месяц уже есть в учете.", "Коммунальные");
                    return;
                }
                bool hasAmount = false;
                for (int i = 1; i < values.Length; i++)
                    if (values[i] != "")
                        hasAmount = true;
                if (!hasAmount)
                {
                    MessageBox.Show("Введите хотя бы одну сумму.", "Учет");
                    return;
                }
                if (!TrySave(delegate { DataStore.AddUtility(values); }))
                    return;
            }
            ShowRecords(UtilitiesSection);
        }

        private void ShowCars(object sender, EventArgs e)
        {
            screenType = "cars";
            homeCarLabel = null;
            homeLogoPanel = null;
            screenHeading = null;
            summaryBox = null;
            homeButtons = null;
            Controls.Clear();
            Text = "Автомобили";
            screenHeading = new Label();
            screenHeading.Text = "Текущий автомобиль: " + currentCar;
            StyleHeading(screenHeading);
            Controls.Add(screenHeading);

            recordList = new ListBox();
            recordList.BackColor = Color.White;
            recordList.ForeColor = Color.FromArgb(35, 51, 70);
            Controls.Add(recordList);
            string[] cars;
            try
            {
                cars = DataStore.LoadCarNames().ToArray();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось прочитать автомобили: " + ex.Message, "Ошибка");
                ShowHome();
                return;
            }
            for (int i = 0; i < cars.Length; i++)
            {
                recordList.Items.Add(cars[i]);
                if (cars[i] == currentCar)
                    recordList.SelectedIndex = i;
            }

            MainMenu menu = new MainMenu();
            MenuItem actions = CreateMenuItem("Действия");
            actions.MenuItems.Add(CreateMenuItem("Выбрать", new EventHandler(SelectCar)));
            actions.MenuItems.Add(CreateMenuItem("Добавить", new EventHandler(AddCar)));
            actions.MenuItems.Add(CreateMenuItem("Переименовать", new EventHandler(RenameCar)));
            actions.MenuItems.Add(CreateMenuItem("Удалить", new EventHandler(DeleteCar)));
            menu.MenuItems.Add(actions);
            menu.MenuItems.Add(CreateMenuItem("Назад", new EventHandler(ShowHomeFromMenu)));
            Menu = menu;
            LayoutScreen();
        }

        private void SelectCar(object sender, EventArgs e)
        {
            if (recordList.SelectedIndex < 0)
            {
                MessageBox.Show("Выберите автомобиль.", "Учет");
                return;
            }
            currentCar = recordList.SelectedItem.ToString();
            ShowCars(sender, e);
        }

        private void AddCar(object sender, EventArgs e)
        {
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Название автомобиля", "", "text", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Добавить автомобиль", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string name = editor.Values[0].Trim();
                if (!TrySave(delegate { DataStore.AddCar(name); }))
                    return;
                currentCar = name;
            }
            ShowCars(sender, e);
        }

        private void RenameCar(object sender, EventArgs e)
        {
            if (recordList.SelectedIndex < 0)
            {
                MessageBox.Show("Выберите автомобиль.", "Учет");
                return;
            }
            string oldName = recordList.SelectedItem.ToString();
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Новое название", oldName, "text", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Переименовать автомобиль", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string newName = editor.Values[0].Trim();
                if (!TrySave(delegate { DataStore.RenameCar(oldName, newName); }))
                    return;
                if (currentCar == oldName)
                    currentCar = newName;
            }
            ShowCars(sender, e);
        }

        private void DeleteCar(object sender, EventArgs e)
        {
            if (recordList.SelectedIndex < 0)
            {
                MessageBox.Show("Выберите автомобиль.", "Учет");
                return;
            }
            string name = recordList.SelectedItem.ToString();
            if (!TrySave(delegate { DataStore.DeleteCar(name); }))
                return;
            if (currentCar == name)
                currentCar = DataStore.LoadCarNames()[0];
            ShowCars(sender, e);
        }

        private void ShowSummary()
        {
            screenType = "summary";
            homeCarLabel = null;
            homeLogoPanel = null;
            screenHeading = null;
            recordList = null;
            homeButtons = null;
            Controls.Clear();
            Text = "Сводка";
            screenHeading = new Label();
            screenHeading.Text = "Сводка расходов";
            StyleHeading(screenHeading);
            Controls.Add(screenHeading);

            summaryBox = new TextBox();
            summaryBox.Multiline = true;
            summaryBox.ReadOnly = true;
            summaryBox.ScrollBars = ScrollBars.Vertical;
            summaryBox.BackColor = Color.FromArgb(245, 248, 251);
            summaryBox.ForeColor = Color.FromArgb(35, 51, 70);
            try
            {
                summaryBox.Text = BuildSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось построить сводку: " + ex.Message, "Ошибка");
                ShowHome();
                return;
            }
            Controls.Add(summaryBox);

            MainMenu menu = new MainMenu();
            menu.MenuItems.Add(CreateMenuItem("Назад", new EventHandler(ShowHomeFromMenu)));
            Menu = menu;
            LayoutScreen();
        }

        private static void StyleHeading(Label label)
        {
            label.BackColor = Color.FromArgb(35, 51, 70);
            label.ForeColor = Color.White;
            label.Font = new Font("Arial", 10, FontStyle.Bold);
        }

        private void FormResized(object sender, EventArgs e)
        {
            LayoutScreen();
        }

        private void LayoutScreen()
        {
            int width = ClientSize.Width;
            int height = ClientSize.Height;
            if (width <= 0 || height <= 0)
                return;

            if (screenType == "home")
            {
                if (homeLogoPanel != null)
                {
                    homeLogoPanel.Location = new Point(4, 4);
                    homeLogoPanel.Size = new Size(width - 8, 52);
                }
                if (homeCarLabel != null)
                {
                    homeCarLabel.Location = new Point(8, 60);
                    homeCarLabel.Size = new Size(width - 16, 26);
                }
                if (homeButtons != null && homeButtons.Count > 0)
                {
                    int gap = 8;
                    int buttonHeight = (height - 108 - gap * (homeButtons.Count - 1)) / homeButtons.Count;
                    if (buttonHeight > 108)
                        buttonHeight = 108;
                    if (buttonHeight < 40)
                        buttonHeight = 40;
                    int firstButtonTop = 92;
                    for (int i = 0; i < homeButtons.Count; i++)
                    {
                        homeButtons[i].Location = new Point(4, firstButtonTop + i * (buttonHeight + gap));
                        homeButtons[i].Size = new Size(width - 8, buttonHeight);
                    }
                }
            }
            else if (screenType == "records" || screenType == "cars")
            {
                if (screenHeading != null)
                {
                    screenHeading.Location = new System.Drawing.Point(4, 4);
                    screenHeading.Size = new System.Drawing.Size(width - 8, 26);
                }
                if (recordList != null)
                {
                    recordList.Location = new System.Drawing.Point(4, 36);
                    recordList.Size = new System.Drawing.Size(width - 8, height - 40);
                }
            }
            else if (screenType == "summary")
            {
                if (screenHeading != null)
                {
                    screenHeading.Location = new System.Drawing.Point(4, 4);
                    screenHeading.Size = new System.Drawing.Size(width - 8, 26);
                }
                if (summaryBox != null)
                {
                    summaryBox.Location = new System.Drawing.Point(4, 36);
                    summaryBox.Size = new System.Drawing.Size(width - 8, height - 40);
                }
            }
        }

        private string BuildSummary()
        {
            decimal fuelTotal = 0;
            List<string[]> fuels = DataStore.LoadFuel();
            Dictionary<string, decimal[]> monthlyTotals = new Dictionary<string, decimal[]>();
            Dictionary<string, decimal> carTotals = new Dictionary<string, decimal>();
            List<string> cars = DataStore.LoadCarNames();
            for (int i = 0; i < cars.Count; i++)
                carTotals.Add(cars[i], 0);

            for (int i = 0; i < fuels.Count; i++)
            {
                string[] row = fuels[i];
                decimal amount = DataStore.NumberOrZero(row[4]);
                AddMonthlyAmount(monthlyTotals, row[0], 0, amount);
                if (carTotals.ContainsKey(row[1]))
                    carTotals[row[1]] += amount;
                if (row[1] == currentCar)
                    fuelTotal += amount;
            }

            decimal repairTotal = 0;
            Dictionary<string, decimal> byCategory = new Dictionary<string, decimal>();
            List<string[]> repairs = DataStore.LoadRepairs();
            for (int i = 0; i < repairs.Count; i++)
            {
                string[] row = repairs[i];
                decimal amount = DataStore.NumberOrZero(row[7]);
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

            decimal utilityTotal = 0;
            List<string[]> utilities = DataStore.LoadUtilities();
            for (int i = 0; i < utilities.Count; i++)
            {
                decimal amount = UtilityTotal(utilities[i]);
                utilityTotal += amount;
                AddMonthlyAmount(monthlyTotals, utilities[i][0], 2, amount);
            }

            string result = currentCar + "\r\n\r\nЗаправки: " + Money(fuelTotal) +
                " BYN\r\nРемонт и ТО: " + Money(repairTotal) + " BYN\r\n";

            decimal minimumMileage = Decimal.MaxValue;
            decimal maximumMileage = 0;
            decimal currentMileage = 0;
            decimal lastOilMileage = 0;
            decimal lastTimingMileage = 0;
            string lastOilDate = "";
            string lastTimingDate = "";
            for (int i = 0; i < fuels.Count; i++)
                if (fuels[i][1] == currentCar)
                    AddMileageReading(fuels[i][2], ref currentMileage,
                        ref minimumMileage, ref maximumMileage);
            for (int i = 0; i < repairs.Count; i++)
            {
                string[] row = repairs[i];
                if (row[1] != currentCar)
                    continue;
                decimal mileage;
                if (DataStore.TryNumber(row[2], out mileage) && mileage >= 0)
                    AddMileageReading(row[2], ref currentMileage,
                        ref minimumMileage, ref maximumMileage);
                if (!DataStore.TryNumber(row[2], out mileage) || mileage < 0)
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
            result += "Учтённые расходы на км (примерно): ";
            if (minimumMileage != Decimal.MaxValue && maximumMileage > minimumMileage)
                result += Money((fuelTotal + repairTotal) /
                    (maximumMileage - minimumMileage)) + " BYN/км\r\n";
            else
                result += "недостаточно данных о пробеге\r\n";

            result += "\r\nРемонт по категориям:\r\n";
            foreach (KeyValuePair<string, decimal> pair in byCategory)
                result += pair.Key + ": " + Money(pair.Value) + " BYN\r\n";
            result += "\r\nКоммунальные за весь период:\r\n" + Money(utilityTotal) +
                " BYN\r\n\r\nРасходы по месяцам (последние 12):\r\n";
            List<string> months = new List<string>(monthlyTotals.Keys);
            months.Sort(StringComparer.Ordinal);
            int firstMonth = Math.Max(0, months.Count - 12);
            for (int i = firstMonth; i < months.Count; i++)
            {
                decimal[] totals = monthlyTotals[months[i]];
                result += months[i] + ": топливо " + Money(totals[0]) +
                    ", ремонт " + Money(totals[1]) + ", коммунальные " +
                    Money(totals[2]) + " BYN\r\n";
            }
            result += "\r\nТопливо и ремонт по автомобилям:\r\n";
            for (int i = 0; i < cars.Count; i++)
                result += cars[i] + ": " + Money(carTotals[cars[i]]) + " BYN\r\n";
            result += "\r\nТекущий пробег: " + Money(currentMileage) + " км\r\n";
            result += ServiceStatus("Замена масла", currentMileage, lastOilMileage,
                lastOilDate, 10000, 1000) + "\r\n";
            result += ServiceStatus("Ремень ГРМ", currentMileage, lastTimingMileage,
                lastTimingDate, 60000, 5000);
            return result;
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
                monthly = new decimal[3];
                totals.Add(key, monthly);
            }
            monthly[category] += amount;
        }

        private static void AddMileageReading(string value, ref decimal currentMileage,
            ref decimal minimumMileage, ref decimal maximumMileage)
        {
            decimal mileage;
            if (!DataStore.TryNumber(value, out mileage) || mileage < 0)
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

        private static string Money(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FieldAt(string[] row, int index)
        {
            if (row == null || index >= row.Length || row[index] == null)
                return "";
            return row[index];
        }

        private static string[] NormalizeFuelValues(string[] values)
        {
            if (values == null || values.Length < 5)
                return values;
            string[] row = values;
            if (row.Length > 7)
            {
                row = new string[7];
                for (int i = 0; i < 7; i++)
                    row[i] = i < values.Length ? values[i] : "";
            }
            if (row.Length > 5)
            {
                string price = FieldAt(row, 5).Trim();
                if (price.Length == 0)
                {
                    decimal liters;
                    decimal cost;
                    if (DataStore.TryNumber(row[3], out liters) && liters > 0 &&
                        DataStore.TryNumber(row[4], out cost) && cost > 0)
                        row[5] = (cost / liters).ToString("0.##", CultureInfo.InvariantCulture);
                }
            }
            int length = row.Length;
            while (length > 5)
            {
                if (FieldAt(row, length - 1).Trim().Length > 0)
                    break;
                length--;
            }
            if (length == row.Length)
                return row;
            string[] trimmed = new string[length];
            for (int i = 0; i < length; i++)
                trimmed[i] = row[i];
            return trimmed;
        }

        private bool TrySave(Action saveAction)
        {
            try
            {
                saveAction();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить данные: " + ex.Message, "Ошибка");
                return false;
            }
        }

        private static MenuItem CreateMenuItem(string text)
        {
            MenuItem item = new MenuItem();
            item.Text = text;
            return item;
        }

        private static MenuItem CreateMenuItem(string text, EventHandler handler)
        {
            MenuItem item = CreateMenuItem(text);
            item.Click += handler;
            return item;
        }

        private void ShowHomeFromMenu(object sender, EventArgs e)
        {
            ShowHome();
        }

        private void ExitApplication(object sender, EventArgs e)
        {
            Close();
        }
    }
}
