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
        private const string ExpensesSection = "Прочие расходы";
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
            List<string> startCars = DataStore.LoadCarNames();
            currentCar = ToSettings.ResolvePreferredCar(startCars, startCars[0]);
            Resize += new EventHandler(FormResized);
            ShowHome();
            MaybeShowOnboardingTip();
            MaybeShowBackupReminder();
        }

        private void MaybeShowOnboardingTip()
        {
            try
            {
                if (ToSettings.IsOnboardingSeen())
                    return;
                List<string[]> fuels = DataStore.LoadFuel();
                List<string[]> repairs = DataStore.LoadRepairs();
                List<string[]> expenses = DataStore.LoadExpenses();
                List<string[]> utilities = DataStore.LoadUtilities();
                bool empty = fuels.Count == 0 && repairs.Count == 0 &&
                    expenses.Count == 0 && utilities.Count == 0;
                ToSettings.SetOnboardingSeen(true);
                if (!empty)
                    return;
                MessageBox.Show(
                    "Кратко:\r\n" +
                    "• Wi‑Fi sync — обмен с телефоном\r\n" +
                    "• Копии на SD / в памяти (settings + cars-meta + expenses)\r\n" +
                    "• Сводка и ТО — итоги и напоминания",
                    "Alfa 156");
            }
            catch
            {
            }
        }

        private void MaybeShowBackupReminder()
        {
            try
            {
                if (!ToSettings.ShouldShowBackupReminder())
                    return;
                ToSettings.MarkBackupNagShown();
                int days = ToSettings.DaysSinceLastBackup();
                string age = days < 0 ? "копии ещё не было" : ("прошло " + days + " дн.");
                MessageBox.Show(
                    "Давно не было резервной копии (" + age + ").\r\n" +
                    "Рекомендуем «Данные → Создать копию на SD» " +
                    "(все .dat, settings, cars-meta, expenses и Receipts).",
                    "Резервная копия");
            }
            catch
            {
            }
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
            cars.MenuItems.Add(CreateMenuItem("Свойства", new EventHandler(EditCarPassport)));
            cars.MenuItems.Add(CreateMenuItem("Избранный", new EventHandler(SetFavoriteCarFromMenu)));
            menu.MenuItems.Add(cars);
            MenuItem data = CreateMenuItem("Данные");
            data.MenuItems.Add(CreateMenuItem("Создать копию в памяти КПК", new EventHandler(CreateLocalBackupFromMenu)));
            data.MenuItems.Add(CreateMenuItem("Восстановить копию из памяти КПК", new EventHandler(RestoreLocalBackupFromMenu)));
            data.MenuItems.Add(CreateMenuItem("Создать копию на SD", new EventHandler(CreateBackupFromMenu)));
            data.MenuItems.Add(CreateMenuItem("Восстановить копию", new EventHandler(RestoreBackupFromMenu)));
            data.MenuItems.Add(CreateMenuItem("Синхронизация по Wi-Fi", new EventHandler(SyncWithAndroidFromMenu)));
            data.MenuItems.Add(CreateMenuItem("Экспорт сводки", new EventHandler(ExportSummaryFromMenu)));
            data.MenuItems.Add(CreateMenuItem("О программе", new EventHandler(ShowAbout)));
            menu.MenuItems.Add(data);
            MenuItem actionsHome = CreateMenuItem("Действия");
            actionsHome.MenuItems.Add(CreateMenuItem("Поиск", new EventHandler(ShowGlobalSearch)));
            actionsHome.MenuItems.Add(CreateMenuItem("Дубликаты", new EventHandler(ShowDuplicatesFromMenu)));
            actionsHome.MenuItems.Add(CreateMenuItem("Проверка данных", new EventHandler(ShowDataIntegrityFromMenu)));
            actionsHome.MenuItems.Add(CreateMenuItem("Интервалы ТО", new EventHandler(EditToIntervals)));
            actionsHome.MenuItems.Add(CreateMenuItem("Валюта", new EventHandler(EditCurrency)));
            actionsHome.MenuItems.Add(CreateMenuItem("Бюджет месяца", new EventHandler(EditBudget)));
            actionsHome.MenuItems.Add(CreateMenuItem("Авто по умолчанию", new EventHandler(EditDefaultCar)));
            actionsHome.MenuItems.Add(CreateMenuItem("Шаблоны", new EventHandler(ManageTemplatesFromMenu)));
            actionsHome.MenuItems.Add(CreateMenuItem("Напоминания", new EventHandler(ManageRemindersFromMenu)));
            actionsHome.MenuItems.Add(CreateMenuItem("Напоминание о копии", new EventHandler(EditBackupReminder)));
            actionsHome.MenuItems.Add(CreateMenuItem("Лента авто", new EventHandler(ShowCarTimeline)));
            actionsHome.MenuItems.Add(CreateMenuItem("Сравнить периоды", new EventHandler(ShowPeriodCompareFromMenu)));
            menu.MenuItems.Add(actionsHome);
            menu.MenuItems.Add(CreateMenuItem("Выход", new EventHandler(ExitApplication)));
            Menu = menu;

            homeLogoPanel = new Panel();
            homeLogoPanel.BackColor = Color.FromArgb(35, 51, 70);
            homeLogoPanel.Paint += new PaintEventHandler(DrawLogo);
            Controls.Add(homeLogoPanel);

            homeCarLabel = new Label();
            homeCarLabel.Text = "Автомобиль: " + currentCar + CarsMeta.HeaderSuffix(currentCar);
            homeCarLabel.BackColor = Color.FromArgb(232, 238, 244);
            homeCarLabel.ForeColor = Color.FromArgb(35, 51, 70);
            homeCarLabel.Font = new Font("Arial", 10, FontStyle.Bold);
            Controls.Add(homeCarLabel);

            AddHomeButton(FuelSection);
            AddHomeButton(RepairSection);
            AddHomeButton(ExpensesSection);
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
            else if (section == ExpensesSection)
                button.BackColor = Color.FromArgb(120, 70, 90);
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
                else if (section == ExpensesSection)
                    records = DataStore.LoadExpenses();
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
                        ShortText(row[4], 18) + " " + row[7] + " " + Cur());
                else if (section == ExpensesSection)
                    recordList.Items.Add(row[0] + " " + row[2] + ": " +
                        ShortText(row[3], 18) + " " + row[4] + " " + Cur());
                else
                    recordList.Items.Add(row[0] + "  " + UtilityTotal(row).ToString("0.##", CultureInfo.InvariantCulture) + " " + Cur());
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
            actions.MenuItems.Add(CreateMenuItem("Шаблоны", new EventHandler(ManageTemplatesFromMenu)));
            actions.MenuItems.Add(CreateMenuItem("Подробно", new EventHandler(ShowRecordDetails)));
            actions.MenuItems.Add(CreateMenuItem("Изменить", new EventHandler(EditCurrentRecord)));
            actions.MenuItems.Add(CreateMenuItem("Удалить", new EventHandler(DeleteCurrentRecord)));
            actions.MenuItems.Add(CreateMenuItem("Поиск", new EventHandler(ShowGlobalSearch)));
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
            else if (section == ExpensesSection)
            {
                actions.MenuItems.Add(CreateMenuItem("Добавить категорию", new EventHandler(AddExpenseCategory)));
                actions.MenuItems.Add(CreateMenuItem("Переименовать категорию", new EventHandler(RenameExpenseCategory)));
                actions.MenuItems.Add(CreateMenuItem("Удалить категорию", new EventHandler(DeleteExpenseCategory)));
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
            string result = row[0] + "  " + row[2] + " км  " + row[3] + " л  " + row[4] + " " + Cur();
            string purpose = GetPurpose(row);
            if (purpose.Length > 0)
                result += "  " + purpose;
            decimal consumption = FuelConsumption(row, allRows);
            if (consumption > 0)
                result += "  " + consumption.ToString("0.0", CultureInfo.InvariantCulture) + " л/100";
            return result;
        }

        private decimal FuelConsumption(string[] row, List<string[]> allRows)
        {
            // л/100км только между полными баками; частичная заправка не замыкает интервал.
            if (!IsFullTank(row))
                return 0;

            decimal mileage;
            if (!DataStore.TryNumber(row[2], out mileage))
                return 0;

            for (int i = allRows.IndexOf(row) - 1; i >= 0; i--)
            {
                if (allRows[i][1] != row[1])
                    continue;
                if (!IsFullTank(allRows[i]))
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
                    "\r\nСтоимость: " + row[4] + " " + Cur();
                string storedPrice = FieldAt(row, 5).Trim();
                decimal liters;
                decimal cost;
                if (storedPrice.Length > 0)
                    details += "\r\nЦена за литр: " + storedPrice + " " + Cur();
                else if (DataStore.TryNumber(row[3], out liters) && liters > 0 &&
                    DataStore.TryNumber(row[4], out cost))
                    details += "\r\nЦена за литр: " +
                        (cost / liters).ToString("0.00", CultureInfo.InvariantCulture) + " " + Cur();
                string comment = FieldAt(row, 6).Trim();
                if (comment.Length > 0)
                    details += "\r\nАЗС / комментарий: " + comment;
                details += "\r\nПолный бак: " + (IsFullTank(row) ? "да" : "нет");
                string purpose = GetPurpose(row);
                if (purpose.Length > 0)
                    details += "\r\nЦель: " + purpose;
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
                    " " + Cur() + "\r\nКомментарий: " + row[8];
            }
            else
            {
                string[] labels = DataStore.UtilityLabels;
                details = row[0] + "\r\n";
                for (int i = 0; i < labels.Length; i++)
                    details += labels[i] + ": " + row[i + 1] + " " + Cur() + "\r\n";
                details += "Итого: " + UtilityTotal(row).ToString("0.##", CultureInfo.InvariantCulture) + " " + Cur();
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
                    new EntryField("Стоимость, " + Cur(), row[4], "number", true),
                    new EntryField("Цена за литр, " + Cur(), FieldAt(row, 5), "number", false),
                    new EntryField("АЗС / комментарий", FieldAt(row, 6), "text", false),
                    new EntryField("Полный бак", FieldAt(row, 7).Length == 0 || FieldAt(row, 7) != "0" ? "1" : "0", "check", false),
                    new EntryField("Цель (Личный/Работа)", GetPurpose(row), PurposeOptions(GetPurpose(row)), false)
                };
                using (EntryEditorForm editor = new EntryEditorForm("Изменить заправку", fields))
                {
                    if (editor.ShowDialog() != DialogResult.OK)
                        return;
                    string[] values = NormalizeFuelValues(editor.Values);
                    if (!ConfirmMileageIfNeeded(values, "fuel.dat", sourceIndex))
                        return;
                    if (!TrySave(delegate { DataStore.UpdateFuelAt(sourceIndex, values); }))
                        return;
                    currentCar = values[1];
                    string tankHint = CarsMeta.TankNearHint(values[1], values[3]);
                    if (tankHint != null)
                        MessageBox.Show(tankHint, "Alfa 156");
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
                    new EntryField("Сумма, " + Cur(), row[7], "number", true),
                    new EntryField("Комментарий", row[8], "text", false)
                };
                using (EntryEditorForm editor = new EntryEditorForm("Изменить ремонт", fields))
                {
                    if (editor.ShowDialog() != DialogResult.OK)
                        return;
                    string[] values = editor.Values;
                    if (!ConfirmMileageIfNeeded(values, "repairs.dat", sourceIndex))
                        return;
                    if (!TrySave(delegate { DataStore.UpdateRepairAt(sourceIndex, values); }))
                        return;
                    currentCar = values[1];
                }
            }
            else if (currentSection == ExpensesSection)
            {
                EntryField[] fields = new EntryField[]
                {
                    new EntryField("Дата (гггг-мм-дд)", row[0], "date", true),
                    new EntryField("Автомобиль", row[1], DataStore.LoadCarNames().ToArray(), true),
                    new EntryField("Категория", row[2], DataStore.LoadExpenseCategories(), true),
                    new EntryField("Название", row[3], "text", true),
                    new EntryField("Сумма, " + Cur(), row[4], "number", true),
                    new EntryField("Комментарий", FieldAt(row, 5), "text", false)
                };
                using (EntryEditorForm editor = new EntryEditorForm("Изменить прочий расход", fields))
                {
                    if (editor.ShowDialog() != DialogResult.OK)
                        return;
                    string[] values = editor.Values;
                    if (!TrySave(delegate { DataStore.UpdateExpenseAt(sourceIndex, values); }))
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
                fields[i + 1] = new EntryField(DataStore.UtilityLabels[i] + ", " + Cur(),
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
                description = row[0] + ", " + row[2] + " км, " + row[4] + " " + Cur();
            else if (currentSection == RepairSection)
                description = row[0] + ", " + row[3] + ": " + ShortText(row[4], 24);
            else if (currentSection == ExpensesSection)
                description = row[0] + ", " + row[2] + ": " + ShortText(row[3], 24);
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
            else if (currentSection == ExpensesSection)
                saved = TrySave(delegate { DataStore.DeleteExpenseAt(sourceIndex); });
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
                int fileCount = DataStore.CountBackupFiles();
                DialogResult confirm = MessageBox.Show(
                    "Создать полную копию на SD?\r\n" +
                    "Будет скопировано файлов: " + fileCount.ToString(CultureInfo.InvariantCulture) +
                    "\r\n(.dat включая settings, cars-meta, expenses + Receipts).",
                    "Копия на SD",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button1);
                if (confirm != DialogResult.Yes)
                    return;
                string path = DataStore.CreateBackup();
                MessageBox.Show("Резервная копия создана:\r\n" + path +
                    "\r\nФайлов: " + fileCount.ToString(CultureInfo.InvariantCulture) +
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
                int fileCount = DataStore.CountBackupFiles();
                DialogResult confirm = MessageBox.Show(
                    "Создать полную копию в памяти КПК?\r\n" +
                    "Будет скопировано файлов: " + fileCount.ToString(CultureInfo.InvariantCulture) +
                    "\r\n(.dat включая settings, cars-meta, expenses + Receipts).",
                    "Копия в памяти",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button1);
                if (confirm != DialogResult.Yes)
                    return;
                string path = DataStore.CreateLocalBackup();
                MessageBox.Show("Локальная резервная копия создана:\r\n" + path +
                    "\r\nФайлов: " + fileCount.ToString(CultureInfo.InvariantCulture),
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
                            byte[] handshake = Encoding.UTF8.GetBytes(SyncCrypto.ProtocolHeader +
                                pairingCode + "\n");
                            stream.Write(handshake, 0, handshake.Length);
                            stream.Flush();

                            int status = DataStore.ReadSyncInt32(stream);
                            if (status != 0)
                                throw new IOException(DataStore.ReadSyncMessage(stream));

                            DataStore.BindSyncSocket(client.Client, 5 * 60 * 1000 * 1000);
                            DataStore.WriteSyncSnapshot(stream, pairingCode);
                            status = DataStore.ReadSyncInt32(stream);
                            if (status != 0)
                                throw new IOException(DataStore.ReadSyncMessage(stream));

                            Cursor.Current = Cursors.Default;
                            DataStore.MergeSyncSnapshot(
                                stream,
                                pairingCode,
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
            SyncJournal journal = SyncMerge.LastJournal;
            if (journal != null)
                MessageBox.Show(journal.FormatSummary(), "Синхронизация");
            else
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
            else if (currentSection == ExpensesSection)
                AddExpense();
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
                new EntryField("Стоимость, " + Cur(), "", "number", true),
                new EntryField("Цена за литр, " + Cur(), LastFuelPricePerLiter(currentCar), "number", false),
                new EntryField("АЗС / комментарий", "", "text", false),
                new EntryField("Полный бак", "1", "check", false),
                new EntryField("Цель (Личный/Работа)", "", PurposeOptions(""), false)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Новая заправка", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] values = NormalizeFuelValues(editor.Values);
                if (!ConfirmMileageIfNeeded(values, "fuel.dat", -1))
                    return;
                if (!TrySave(delegate { DataStore.AddFuel(values); }))
                    return;
                currentCar = values[1];
                string tankHint = CarsMeta.TankNearHint(values[1], values[3]);
                if (tankHint != null)
                    MessageBox.Show(tankHint, "Alfa 156");
            }
            ShowRecords(FuelSection);
        }

        private static string LastCarMileage(string car)
        {
            decimal best = LastKnownMileage(car, null, -1);
            return best < 0 ? "" : best.ToString(CultureInfo.InvariantCulture);
        }

        private static string LastFuelPricePerLiter(string car)
        {
            List<string[]> fuels = DataStore.LoadFuel();
            string bestDate = "";
            string bestPrice = "";
            int bestIndex = -1;
            for (int i = 0; i < fuels.Count; i++)
            {
                if (fuels[i].Length < 6)
                    continue;
                if ((fuels[i][1] ?? "") != (car ?? ""))
                    continue;
                string price = fuels[i][5] == null ? "" : fuels[i][5].Trim();
                if (price.Length == 0)
                    continue;
                string date = fuels[i][0] == null ? "" : fuels[i][0];
                if (bestIndex < 0 ||
                    String.Compare(date, bestDate, false, CultureInfo.InvariantCulture) > 0 ||
                    (date == bestDate && i > bestIndex))
                {
                    bestDate = date;
                    bestPrice = price;
                    bestIndex = i;
                }
            }
            return bestPrice;
        }

        private bool ConfirmMileageIfNeeded(string[] values, string fileName, int excludeIndex)
        {
            if (values == null || values.Length < 3)
                return true;
            if (fileName == "repairs.dat" && (values[2] ?? "").Trim().Length == 0)
                return true;
            decimal newKm;
            if (!DataStore.TryNumber(values[2], out newKm) || newKm < 0)
                return true;
            decimal last = LastKnownMileage(values[1] ?? "", fileName, excludeIndex);
            string warning = FormatMileageWarning(newKm, last);
            if (warning == null)
                return true;
            return MessageBox.Show(warning, "Пробег", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private static decimal LastKnownMileage(string car, string excludeFile, int excludeIndex)
        {
            decimal best = -1;
            RememberLatestMileage(DataStore.LoadFuel(), car,
                excludeFile == "fuel.dat" ? excludeIndex : -1, ref best);
            RememberLatestMileage(DataStore.LoadRepairs(), car,
                excludeFile == "repairs.dat" ? excludeIndex : -1, ref best);
            return best;
        }

        private static void RememberLatestMileage(List<string[]> rows, string car,
            int excludeIndex, ref decimal best)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (i == excludeIndex)
                    continue;
                if (rows[i].Length < 3 || rows[i][1] != car)
                    continue;
                decimal mileage;
                if (!DataStore.TryNumber(rows[i][2], out mileage) || mileage < 0)
                    continue;
                if (mileage >= best)
                    best = mileage;
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
                new EntryField("Сумма, " + Cur(), "", "number", true),
                new EntryField("Комментарий", "", "text", false)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Новый ремонт", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] values = editor.Values;
                if (!ConfirmMileageIfNeeded(values, "repairs.dat", -1))
                    return;
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
                fields[i + 1] = new EntryField(DataStore.UtilityLabels[i] + ", " + Cur(), "", "number", false);

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
            actions.MenuItems.Add(CreateMenuItem("Свойства", new EventHandler(EditCarPassportFromList)));
            actions.MenuItems.Add(CreateMenuItem("Избранный", new EventHandler(SetFavoriteCarFromMenu)));
            actions.MenuItems.Add(CreateMenuItem("Переименовать", new EventHandler(RenameCar)));
            actions.MenuItems.Add(CreateMenuItem("Удалить", new EventHandler(DeleteCar)));
            menu.MenuItems.Add(actions);
            menu.MenuItems.Add(CreateMenuItem("Назад", new EventHandler(ShowHomeFromMenu)));
            Menu = menu;
            LayoutScreen();
        }

        private void EditCarPassport(object sender, EventArgs e)
        {
            EditCarPassportFor(currentCar);
        }

        private void EditCarPassportFromList(object sender, EventArgs e)
        {
            if (recordList == null || recordList.SelectedIndex < 0)
            {
                MessageBox.Show("Выберите автомобиль.", "Учет");
                return;
            }
            EditCarPassportFor(recordList.SelectedItem.ToString());
        }

        private void EditCarPassportFor(string carName)
        {
            CarPassport meta = CarsMeta.Get(carName);
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Год", meta.Year, "text", false),
                new EntryField("Госномер", meta.Plate, "text", false),
                new EntryField("VIN", meta.Vin, "text", false),
                new EntryField("Объём бака, л", meta.TankLiters, "number", false),
                new EntryField("Заметка", meta.Note, "text", false)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Свойства · " + carName, fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                meta.CarName = carName;
                meta.Year = editor.Values[0];
                meta.Plate = editor.Values[1];
                meta.Vin = editor.Values[2];
                meta.TankLiters = editor.Values[3];
                meta.Note = editor.Values[4];
                if (!TrySave(delegate { CarsMeta.Upsert(meta); }))
                    return;
            }
            if (screenType == "cars")
                ShowCars(null, EventArgs.Empty);
            else
                ShowHome();
        }

        private void ShowCarTimeline(object sender, EventArgs e)
        {
            try
            {
                string text = CarTimeline.FormatList(currentCar);
                screenType = "timeline";
                homeCarLabel = null;
                homeLogoPanel = null;
                screenHeading = null;
                recordList = null;
                homeButtons = null;
                Controls.Clear();
                Text = "Лента";
                screenHeading = new Label();
                screenHeading.Text = "Лента · " + currentCar + CarsMeta.HeaderSuffix(currentCar);
                StyleHeading(screenHeading);
                Controls.Add(screenHeading);
                summaryBox = new TextBox();
                summaryBox.Multiline = true;
                summaryBox.ReadOnly = true;
                summaryBox.ScrollBars = ScrollBars.Vertical;
                summaryBox.BackColor = Color.FromArgb(245, 248, 251);
                summaryBox.ForeColor = Color.FromArgb(35, 51, 70);
                summaryBox.Text = text;
                Controls.Add(summaryBox);
                MainMenu menu = new MainMenu();
                menu.MenuItems.Add(CreateMenuItem("Назад", new EventHandler(ShowHomeFromMenu)));
                Menu = menu;
                LayoutScreen();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось построить ленту: " + ex.Message, "Ошибка");
            }
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
            screenHeading.Text = "Сводка · " + currentCar + CarsMeta.HeaderSuffix(currentCar);
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
            MenuItem actions = CreateMenuItem("Действия");
            actions.MenuItems.Add(CreateMenuItem("Сравнить периоды", new EventHandler(ShowPeriodCompareFromMenu)));
            menu.MenuItems.Add(actions);
            menu.MenuItems.Add(CreateMenuItem("Назад", new EventHandler(ShowHomeFromMenu)));
            Menu = menu;
            LayoutScreen();
        }

        private void ShowPeriodCompareFromMenu(object sender, EventArgs e)
        {
            try
            {
                string aFrom, aTo, bFrom, bTo;
                PeriodCompare.ThisVsPreviousMonth(out aFrom, out aTo, out bFrom, out bTo);
                List<string[]> fuels = DataStore.LoadFuel();
                List<string[]> repairs = DataStore.LoadRepairs();
                List<string[]> expenses = DataStore.LoadExpenses();
                List<string[]> utilities = DataStore.LoadUtilities();
                PeriodCompare.Totals a = PeriodCompare.Build(
                    currentCar, fuels, repairs, expenses, utilities, aFrom, aTo);
                PeriodCompare.Totals b = PeriodCompare.Build(
                    currentCar, fuels, repairs, expenses, utilities, bFrom, bTo);
                string text = PeriodCompare.FormatComparison(
                    a, b,
                    PeriodCompare.FormatRangeLabel(aFrom, aTo) + " (этот месяц)",
                    PeriodCompare.FormatRangeLabel(bFrom, bTo) + " (прошлый)",
                    Cur());
                MessageBox.Show(text, "Сравнение периодов");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сравнить периоды: " + ex.Message, "Ошибка");
            }
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
            else if (screenType == "summary" || screenType == "timeline")
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
            decimal yearFuel = 0;
            decimal yearRepair = 0;
            decimal yearExpense = 0;
            decimal yearUtility = 0;
            decimal purposePersonal = 0;
            decimal purposeWork = 0;
            decimal purposeOther = 0;
            bool anyPurpose = false;
            string yearPrefix = DateTime.Now.ToString("yyyy", CultureInfo.InvariantCulture);
            List<string[]> fuels = DataStore.LoadFuel();
            Dictionary<string, decimal[]> monthlyTotals = new Dictionary<string, decimal[]>();
            List<string> cars = DataStore.LoadCarNames();

            for (int i = 0; i < fuels.Count; i++)
            {
                string[] row = fuels[i];
                decimal amount = DataStore.NumberOrZero(row[4]);
                AddMonthlyAmount(monthlyTotals, row[0], 0, amount);
                if (row[1] == currentCar)
                {
                    fuelTotal += amount;
                    if (row[0] != null && row[0].StartsWith(yearPrefix))
                        yearFuel += amount;
                    string purpose = GetPurpose(row);
                    if (purpose.Length > 0)
                    {
                        anyPurpose = true;
                        if (String.Compare(purpose, "Личный", StringComparison.OrdinalIgnoreCase) == 0)
                            purposePersonal += amount;
                        else if (String.Compare(purpose, "Работа", StringComparison.OrdinalIgnoreCase) == 0)
                            purposeWork += amount;
                        else
                            purposeOther += amount;
                    }
                }
            }

            decimal repairTotal = 0;
            Dictionary<string, decimal> byCategory = new Dictionary<string, decimal>();
            List<string[]> repairs = DataStore.LoadRepairs();
            for (int i = 0; i < repairs.Count; i++)
            {
                string[] row = repairs[i];
                decimal amount = DataStore.NumberOrZero(row[7]);
                AddMonthlyAmount(monthlyTotals, row[0], 1, amount);
                if (row[1] != currentCar)
                    continue;
                repairTotal += amount;
                if (row[0] != null && row[0].StartsWith(yearPrefix))
                    yearRepair += amount;
                string category = row[3];
                if (!byCategory.ContainsKey(category))
                    byCategory.Add(category, 0);
                byCategory[category] += amount;
            }

            decimal expenseTotal = 0;
            List<string[]> expenses = DataStore.LoadExpenses();
            for (int i = 0; i < expenses.Count; i++)
            {
                string[] row = expenses[i];
                if (row.Length < 5)
                    continue;
                decimal amount = DataStore.NumberOrZero(row[4]);
                AddMonthlyAmount(monthlyTotals, row[0], 3, amount);
                if (row[1] == currentCar)
                {
                    expenseTotal += amount;
                    if (row[0] != null && row[0].StartsWith(yearPrefix))
                        yearExpense += amount;
                }
            }

            decimal utilityTotal = 0;
            List<string[]> utilities = DataStore.LoadUtilities();
            for (int i = 0; i < utilities.Count; i++)
            {
                decimal amount = UtilityTotal(utilities[i]);
                utilityTotal += amount;
                AddMonthlyAmount(monthlyTotals, utilities[i][0], 2, amount);
                if (utilities[i][0] != null && utilities[i][0].StartsWith(yearPrefix))
                    yearUtility += amount;
            }

            CarPassport passport = CarsMeta.Get(currentCar);
            string result = currentCar + CarsMeta.HeaderSuffix(currentCar);
            if (passport.Vin.Length > 0 || passport.TankLiters.Length > 0 || passport.Note.Length > 0)
            {
                result += "\r\n";
                if (passport.Vin.Length > 0)
                    result += "VIN: " + passport.Vin;
                if (passport.TankLiters.Length > 0)
                {
                    if (passport.Vin.Length > 0)
                        result += " · ";
                    result += "бак " + passport.TankLiters + " л";
                }
                if (passport.Note.Length > 0)
                    result += "\r\n" + passport.Note;
            }
            result += "\r\n\r\nЗаправки: " + Money(fuelTotal) +
                " " + Cur() + "\r\nРемонт и ТО: " + Money(repairTotal) +
                " " + Cur() + "\r\nПрочие расходы: " + Money(expenseTotal) + " " + Cur() + "\r\n";
            if (anyPurpose)
            {
                result += "\r\nЗаправки по цели:\r\nЛичный: " + Money(purposePersonal) +
                    " " + Cur() + "\r\nРабота: " + Money(purposeWork) + " " + Cur();
                if (purposeOther > 0)
                    result += "\r\nДругое: " + Money(purposeOther) + " " + Cur();
                result += "\r\n";
            }

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
                result += Money((fuelTotal + repairTotal + expenseTotal) /
                    (maximumMileage - minimumMileage)) + " " + Cur() + "/км\r\n";
            else
                result += "недостаточно данных о пробеге\r\n";

            result += "\r\nРемонт по категориям:\r\n";
            foreach (KeyValuePair<string, decimal> pair in byCategory)
                result += pair.Key + ": " + Money(pair.Value) + " " + Cur() + "\r\n";
            result += "\r\nКоммунальные за весь период:\r\n" + Money(utilityTotal) +
                " " + Cur();
            string monthKey = DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            decimal monthFuel = 0, monthRepair = 0, monthExpense = 0, monthUtility = 0;
            decimal[] monthBucket;
            if (monthlyTotals.TryGetValue(monthKey, out monthBucket))
            {
                monthFuel = monthBucket[0];
                monthRepair = monthBucket[1];
                monthUtility = monthBucket[2];
                monthExpense = monthBucket.Length > 3 ? monthBucket[3] : 0m;
            }
            result += FormatBudgetBlock(monthKey, monthFuel + monthRepair + monthExpense, monthUtility);
            result += "\r\n\r\nИтого за год " + yearPrefix + ":\r\n" +
                "Топливо: " + Money(yearFuel) + " " + Cur() + "\r\n" +
                "Ремонт: " + Money(yearRepair) + " " + Cur() + "\r\n" +
                "Прочие: " + Money(yearExpense) + " " + Cur() + "\r\n" +
                "Коммунальные: " + Money(yearUtility) + " " + Cur() + "\r\n" +
                "Всего: " + Money(yearFuel + yearRepair + yearExpense + yearUtility) +
                " " + Cur() + "\r\n\r\nРасходы по месяцам (последние 12):\r\n";
            List<string> months = new List<string>(monthlyTotals.Keys);
            months.Sort(StringComparer.Ordinal);
            int firstMonth = Math.Max(0, months.Count - 12);
            for (int i = firstMonth; i < months.Count; i++)
            {
                decimal[] totals = monthlyTotals[months[i]];
                result += months[i] + ": топливо " + Money(totals[0]) +
                    ", ремонт " + Money(totals[1]) + ", коммунальные " +
                    Money(totals[2]) + ", прочие " +
                    Money(totals.Length > 3 ? totals[3] : 0m) + " " + Cur() + "\r\n";
            }
            result += "\r\n" + CarCompare.FormatBlock(
                CarCompare.Build(cars, fuels, repairs, expenses), Cur()) + "\r\n";
            result += "\r\nТекущий пробег: " + Money(currentMileage) + " км\r\n";
            ToIntervals intervals = ToSettings.Load();
            result += ServiceStatus("Замена масла", currentMileage, lastOilMileage,
                lastOilDate, intervals.OilInterval, intervals.OilWarn) + "\r\n";
            result += ServiceStatus("Ремень ГРМ", currentMileage, lastTimingMileage,
                lastTimingDate, intervals.BeltInterval, intervals.BeltWarn);
            result += DateReminders.FormatUpcomingBlock(
                ToSettings.GetReminderWarnDays(), DateTime.Today);
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
                monthly = new decimal[4];
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
            string[] content = RecordIds.StripId(row);
            if (content == null || index >= content.Length || content[index] == null)
                return "";
            return content[index];
        }

        private static bool IsFullTank(string[] row)
        {
            string[] content = RecordIds.StripId(row);
            if (content == null || content.Length <= 7)
                return true;
            string flag = content[7] == null ? "" : content[7].Trim();
            return flag.Length == 0 || flag == "1";
        }

        private static string NormalizeFullTankFlag(string value)
        {
            string flag = value == null ? "" : value.Trim();
            if (flag == "0" || String.Equals(flag, "false", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(flag, "нет", StringComparison.OrdinalIgnoreCase))
                return "0";
            if (flag.Length == 0)
                return "1";
            return "1";
        }

        private static string[] NormalizeFuelValues(string[] values)
        {
            if (values == null || values.Length < 5)
                return values;
            // date|car|km|liters|cost|price|comment|fullTank|purpose  (id added separately)
            string[] row = values;
            if (row.Length > 9)
            {
                row = new string[9];
                for (int i = 0; i < 9; i++)
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
            if (row.Length > 7)
                row[7] = NormalizeFullTankFlag(row[7]);
            if (row.Length > 8)
                row[8] = NormalizePurpose(row[8]);
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

        private static string GetPurpose(string[] row)
        {
            string[] content = RecordIds.StripId(row);
            if (content == null || content.Length <= 8)
                return "";
            return NormalizePurpose(content[8]);
        }

        private static string NormalizePurpose(string value)
        {
            string text = value == null ? "" : value.Trim().Replace("\r", " ").Replace("\n", " ");
            if (text.Length > 32)
                text = text.Substring(0, 32).Trim();
            return text;
        }

        private static string[] PurposeOptions(string current)
        {
            System.Collections.ArrayList list = new System.Collections.ArrayList();
            list.Add("");
            list.Add("Личный");
            list.Add("Работа");
            string value = NormalizePurpose(current);
            if (value.Length > 0 && list.IndexOf(value) < 0)
                list.Add(value);
            string[] result = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
                result[i] = (string)list[i];
            return result;
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

        private static string Cur()
        {
            return ToSettings.GetCurrency();
        }

        private void ShowAbout(object sender, EventArgs e)
        {
            MessageBox.Show(
                "Alfa 156\r\nВерсия 1.27.0\r\n\r\nУчёт заправок, ремонта, прочих расходов и коммуналки.\r\n" +
                "Паспорт авто (cars-meta.dat) и лента расходов.\r\n" +
                "Напоминания по дате (reminders.dat): страховка / техосмотр / своё.\r\n" +
                "Сравнение периодов: этот месяц vs прошлый.\r\n" +
                "Цель заправки (Личный/Работа); итоги за календарный год в сводке.\r\n" +
                "Экспорт сводки; дубликаты; проверка пробега; поиск.\r\n" +
                "Полный бак: частичные заправки не входят в расчёт л/100 км.",
                "О программе");
        }

        private void ExportSummaryFromMenu(object sender, EventArgs e)
        {
            try
            {
                string text = BuildSummary();
                string fileName = "Summary_" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".txt";
                string folder = @"\My Documents\Alfa156";
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, fileName);
                using (StreamWriter writer = new StreamWriter(path, false))
                    writer.Write(text);
                MessageBox.Show("Сводка сохранена:\r\n" + path, "Экспорт сводки");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить сводку: " + ex.Message, "Ошибка");
            }
        }

        private void ShowDataIntegrityFromMenu(object sender, EventArgs e)
        {
            try
            {
                DataIntegrity.Report report = DataIntegrity.Check();
                string text = DataIntegrity.Format(report);
                if (report.CanSimpleFix)
                {
                    DialogResult answer = MessageBox.Show(
                        text + "\r\n\r\nИсправить простое (добавить категории)?",
                        "Проверка данных", MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                    if (answer == DialogResult.Yes)
                    {
                        int n = DataIntegrity.ApplySimpleFixes(report);
                        MessageBox.Show(n > 0
                            ? "Добавлено категорий: " + n + "."
                            : "Нечего исправлять.",
                            "Проверка данных");
                    }
                }
                else
                {
                    MessageBox.Show(text, "Проверка данных");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось проверить данные: " + ex.Message, "Проверка данных");
            }
        }

        private void ShowDuplicatesFromMenu(object sender, EventArgs e)
        {
            List<DuplicateFinder.Group> groups;
            try
            {
                groups = DuplicateFinder.FindAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось найти дубликаты: " + ex.Message, "Ошибка");
                return;
            }
            if (groups.Count == 0)
            {
                MessageBox.Show("Дубликатов не найдено.", "Дубликаты");
                return;
            }

            using (Form dialog = new Form())
            {
                dialog.Text = "Дубликаты";
                dialog.WindowState = FormWindowState.Maximized;
                dialog.MinimizeBox = false;

                ListBox list = new ListBox();
                list.Location = new Point(4, 4);
                dialog.Controls.Add(list);

                List<object> items = new List<object>();
                for (int g = 0; g < groups.Count; g++)
                {
                    DuplicateFinder.Group group = groups[g];
                    list.Items.Add("--- " + group.Title + " · " + group.Reason +
                        " (" + group.Indexes.Count.ToString() + ") ---");
                    items.Add(null);
                    for (int i = 0; i < group.Rows.Count; i++)
                    {
                        list.Items.Add((i == 0 ? "  ✓ " : "  · ") +
                            DuplicateFinder.FormatRow(group.Rows[i]));
                        items.Add(group);
                    }
                }

                MainMenu menu = new MainMenu();
                MenuItem actions = CreateMenuItem("Действия");
                actions.MenuItems.Add(CreateMenuItem("Удалить лишние (оставить первую)",
                    new EventHandler(delegate
                    {
                        int selected = list.SelectedIndex;
                        if (selected < 0 || selected >= items.Count || items[selected] == null)
                        {
                            MessageBox.Show("Выберите запись внутри группы дубликатов.", "Дубликаты");
                            return;
                        }
                        DuplicateFinder.Group group = (DuplicateFinder.Group)items[selected];
                        if (MessageBox.Show(
                            "Удалить " + (group.Indexes.Count - 1).ToString() +
                            " лишних записей, оставив первую?",
                            "Дубликаты", MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                            return;
                        List<int> toDelete = new List<int>();
                        for (int i = 1; i < group.Indexes.Count; i++)
                            toDelete.Add(group.Indexes[i]);
                        toDelete.Sort();
                        bool ok = TrySave(delegate
                        {
                            for (int i = toDelete.Count - 1; i >= 0; i--)
                            {
                                int index = toDelete[i];
                                if (group.FileName == "fuel.dat")
                                    DataStore.DeleteFuelAt(index);
                                else if (group.FileName == "repairs.dat")
                                    DataStore.DeleteRepairAt(index);
                                else if (group.FileName == "expenses.dat")
                                    DataStore.DeleteExpenseAt(index);
                                else if (group.FileName == "utilities.dat")
                                    DataStore.DeleteUtilityAt(index);
                            }
                        });
                        if (!ok)
                            return;
                        dialog.Close();
                        MessageBox.Show("Лишние записи удалены.", "Дубликаты");
                    })));
                menu.MenuItems.Add(actions);
                menu.MenuItems.Add(CreateMenuItem("Закрыть", new EventHandler(delegate
                {
                    dialog.Close();
                })));
                dialog.Menu = menu;

                dialog.Resize += new EventHandler(delegate
                {
                    list.Size = new Size(dialog.ClientSize.Width - 8, dialog.ClientSize.Height - 8);
                });
                list.Size = new Size(dialog.ClientSize.Width - 8, dialog.ClientSize.Height - 8);
                dialog.ShowDialog();
            }
        }

        private void ShowGlobalSearch(object sender, EventArgs e)
        {
            using (Form searchForm = new Form())
            {
                searchForm.Text = "Поиск";
                searchForm.WindowState = FormWindowState.Maximized;
                searchForm.MinimizeBox = false;

                Label hint = new Label();
                hint.Text = "Текст (комментарий, название…):";
                hint.Location = new Point(8, 8);
                hint.Size = new Size(220, 20);
                searchForm.Controls.Add(hint);

                TextBox queryBox = new TextBox();
                queryBox.Location = new Point(8, 30);
                queryBox.Size = new Size(224, 22);
                searchForm.Controls.Add(queryBox);

                Button findButton = new Button();
                findButton.Text = "Найти";
                findButton.Location = new Point(8, 58);
                findButton.Size = new Size(100, 28);
                searchForm.Controls.Add(findButton);

                ListBox results = new ListBox();
                results.Location = new Point(8, 92);
                results.Size = new Size(224, 160);
                searchForm.Controls.Add(results);

                List<string> hitSections = new List<string>();
                List<string> hitCars = new List<string>();
                List<int> hitIndexes = new List<int>();

                findButton.Click += delegate
                {
                    results.Items.Clear();
                    hitSections.Clear();
                    hitCars.Clear();
                    hitIndexes.Clear();
                    string q = (queryBox.Text ?? "").Trim().ToLower();
                    if (q.Length == 0)
                        return;
                    CollectKpkHits(results, hitSections, hitCars, hitIndexes, FuelSection, "fuel.dat",
                        q, DataStore.LoadFuel(), delegate(string[] row)
                        {
                            return FieldAt(row, 6) + " " + GetPurpose(row) + " " + FieldAt(row, 1);
                        });
                    CollectKpkHits(results, hitSections, hitCars, hitIndexes, RepairSection, "repairs.dat",
                        q, DataStore.LoadRepairs(), delegate(string[] row)
                        {
                            return FieldAt(row, 3) + " " + FieldAt(row, 4) + " " + FieldAt(row, 5) +
                                " " + FieldAt(row, 8) + " " + FieldAt(row, 1);
                        });
                    CollectKpkHits(results, hitSections, hitCars, hitIndexes, ExpensesSection,
                        "expenses.dat", q, DataStore.LoadExpenses(), delegate(string[] row)
                        {
                            return FieldAt(row, 2) + " " + FieldAt(row, 3) + " " + FieldAt(row, 5) +
                                " " + FieldAt(row, 1);
                        });
                    CollectKpkHits(results, hitSections, hitCars, hitIndexes, UtilitiesSection,
                        "utilities.dat", q, DataStore.LoadUtilities(), delegate(string[] row)
                        {
                            StringBuilder sb = new StringBuilder(FieldAt(row, 0));
                            for (int i = 0; i < DataStore.UtilityLabels.Length && i + 1 < row.Length; i++)
                                sb.Append(' ').Append(DataStore.UtilityLabels[i]).Append(' ')
                                    .Append(row[i + 1]);
                            return sb.ToString();
                        });
                    if (results.Items.Count == 0)
                        results.Items.Add("Ничего не найдено");
                };

                results.SelectedIndexChanged += delegate
                {
                    int index = results.SelectedIndex;
                    if (index < 0 || index >= hitSections.Count)
                        return;
                    string section = hitSections[index];
                    string car = hitCars[index];
                    if (car != null && car.Length > 0)
                        currentCar = car;
                    searchForm.DialogResult = DialogResult.OK;
                    searchForm.Close();
                    ShowRecords(section);
                };

                Button closeButton = new Button();
                closeButton.Text = "Закрыть";
                closeButton.Location = new Point(120, 58);
                closeButton.Size = new Size(100, 28);
                closeButton.Click += delegate { searchForm.Close(); };
                searchForm.Controls.Add(closeButton);

                searchForm.ShowDialog();
            }
        }

        private delegate string SearchHaystack(string[] row);

        private static void CollectKpkHits(ListBox results, List<string> sections, List<string> cars,
            List<int> indexes, string section, string fileName, string queryLower,
            List<string[]> rows, SearchHaystack haystack)
        {
            string label = section;
            for (int i = 0; i < rows.Count; i++)
            {
                string text = haystack(rows[i]) ?? "";
                if (text.ToLower().IndexOf(queryLower) < 0)
                    continue;
                string date = FieldAt(rows[i], 0);
                string car = section == UtilitiesSection ? "" : FieldAt(rows[i], 1);
                string line = label + " · " + date +
                    (car.Length > 0 ? " · " + car : "") + " — " + ShortText(text.Trim(), 28);
                results.Items.Add(line);
                sections.Add(section);
                cars.Add(car);
                indexes.Add(i);
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

        private void AddExpense()
        {
            string[] cars = DataStore.LoadCarNames().ToArray();
            string[] categories = DataStore.LoadExpenseCategories();
            if (categories.Length == 0)
            {
                MessageBox.Show("Сначала добавьте категорию прочих расходов.", "Категории");
                return;
            }
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Дата (гггг-мм-дд)", DateTime.Now.ToString("yyyy-MM-dd"), "date", true),
                new EntryField("Автомобиль", currentCar, cars, true),
                new EntryField("Категория", categories[0], categories, true),
                new EntryField("Название", "", "text", true),
                new EntryField("Сумма, " + Cur(), "", "number", true),
                new EntryField("Комментарий", "", "text", false)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Прочий расход", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] values = editor.Values;
                if (!TrySave(delegate { DataStore.AddExpense(values); }))
                    return;
                currentCar = values[1];
            }
            ShowRecords(ExpensesSection);
        }

        private void AddExpenseCategory(object sender, EventArgs e)
        {
            EntryField[] fields = new EntryField[] { new EntryField("Название", "", "text", true) };
            using (EntryEditorForm editor = new EntryEditorForm("Новая кат. прочих", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                if (!TrySave(delegate { DataStore.AddExpenseCategory(editor.Values[0]); }))
                    return;
            }
            ShowRecords(ExpensesSection);
        }

        private void RenameExpenseCategory(object sender, EventArgs e)
        {
            string[] categories = DataStore.LoadExpenseCategories();
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
            using (EntryEditorForm editor = new EntryEditorForm("Переименовать кат. прочих", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] values = editor.Values;
                if (!TrySave(delegate { DataStore.RenameExpenseCategory(values[0], values[1]); }))
                    return;
            }
            ShowRecords(ExpensesSection);
        }

        private void DeleteExpenseCategory(object sender, EventArgs e)
        {
            string[] categories = DataStore.LoadExpenseCategories();
            if (categories.Length == 0)
            {
                MessageBox.Show("Нет категорий для удаления.", "Категории");
                return;
            }
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Категория", categories[0], categories, true)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Удалить кат. прочих", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                if (MessageBox.Show(
                    "Записи не удаляются. Если категория используется, сначала перенесите записи в другую. Удалить категорию?",
                    "Категории", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;
                if (!TrySave(delegate { DataStore.DeleteExpenseCategory(editor.Values[0]); }))
                    return;
            }
            ShowRecords(ExpensesSection);
        }

        private void EditToIntervals(object sender, EventArgs e)
        {
            ToIntervals values = ToSettings.Load();
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Интервал масла, км", values.OilInterval.ToString(CultureInfo.InvariantCulture), "number", true),
                new EntryField("Предупр. масла, км", values.OilWarn.ToString(CultureInfo.InvariantCulture), "number", true),
                new EntryField("Интервал ремня, км", values.BeltInterval.ToString(CultureInfo.InvariantCulture), "number", true),
                new EntryField("Предупр. ремня, км", values.BeltWarn.ToString(CultureInfo.InvariantCulture), "number", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Интервалы ТО", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] raw = editor.Values;
                decimal oilInterval, oilWarn, beltInterval, beltWarn;
                if (!DataStore.TryNumber(raw[0], out oilInterval) || oilInterval <= 0 ||
                    !DataStore.TryNumber(raw[1], out oilWarn) || oilWarn <= 0 ||
                    !DataStore.TryNumber(raw[2], out beltInterval) || beltInterval <= 0 ||
                    !DataStore.TryNumber(raw[3], out beltWarn) || beltWarn <= 0)
                {
                    MessageBox.Show("Введите положительные числа.", "Интервалы ТО");
                    return;
                }
                values.OilInterval = oilInterval;
                values.OilWarn = oilWarn;
                values.BeltInterval = beltInterval;
                values.BeltWarn = beltWarn;
                if (!TrySave(delegate { ToSettings.Save(values); }))
                    return;
                MessageBox.Show("Интервалы ТО сохранены.", "Интервалы ТО");
            }
        }

        private void EditCurrency(object sender, EventArgs e)
        {
            string current = ToSettings.GetCurrency();
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Код валюты (BYN/RUB/USD/EUR)", current, "text", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Валюта", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                try
                {
                    if (!TrySave(delegate { ToSettings.SetCurrency(editor.Values[0]); }))
                        return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Валюта");
                    return;
                }
                MessageBox.Show("Валюта: " + ToSettings.GetCurrency() +
                    ". Суммы не пересчитываются — меняется только подпись.", "Валюта");
                if (summaryBox != null)
                    summaryBox.Text = BuildSummary();
            }
        }

        private string FormatBudgetBlock(string monthKey, decimal spent, decimal utilitiesMonth)
        {
            decimal budget = ToSettings.GetBudgetMonth();
            if (budget <= 0)
                return "";
            decimal warnPct = ToSettings.GetBudgetWarnPct();
            decimal pct = budget > 0 ? (spent * 100m) / budget : 0;
            string block = "\r\n\r\nБюджет месяца " + monthKey + ":\r\n" +
                "Потрачено (топливо+ремонт+прочие): " +
                Money(spent) + " / " + Money(budget) + " " + Cur() +
                " (" + Money(pct) + "%)\r\n" +
                "Коммунальные в этом месяце: " + Money(utilitiesMonth) + " " + Cur();
            if (spent >= budget)
                block += "\r\n! Превышен бюджет";
            else if (pct >= warnPct)
                block += "\r\n! Предупреждение: >= " + Money(warnPct) + "% бюджета";
            return block;
        }

        private void EditBackupReminder(object sender, EventArgs e)
        {
            int days = ToSettings.GetBackupRemindDays();
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Дней (0 = выкл., по умолч. 7)",
                    days.ToString(CultureInfo.InvariantCulture), "number", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Напоминание о копии", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                decimal value;
                if (!DataStore.TryNumber(editor.Values[0], out value) || value < 0 || value > 3650 ||
                    value != Decimal.Truncate(value))
                {
                    MessageBox.Show("Интервал: 0 (выкл.) или целое 1–3650.", "Напоминание");
                    return;
                }
                try
                {
                    if (!TrySave(delegate { ToSettings.SetBackupRemindDays((int)value); }))
                        return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Напоминание");
                    return;
                }
                if (value <= 0)
                    MessageBox.Show("Напоминание о резервной копии выключено.", "Напоминание");
                else
                    MessageBox.Show("Напоминание: если копии нет " +
                        ((int)value).ToString(CultureInfo.InvariantCulture) + " дн. и дольше.",
                        "Напоминание");
            }
        }

        private void SetFavoriteCarFromMenu(object sender, EventArgs e)
        {
            string name = currentCar;
            if (recordList != null && recordList.SelectedItem != null &&
                screenType == "cars")
                name = recordList.SelectedItem.ToString();
            if (name == null || name.Length == 0)
            {
                MessageBox.Show("Выберите автомобиль.", "Авто");
                return;
            }
            if (!TrySave(delegate { ToSettings.SetDefaultCar(name); }))
                return;
            currentCar = name;
            MessageBox.Show("Избранный автомобиль: " + name, "Авто");
        }

        private void EditDefaultCar(object sender, EventArgs e)
        {
            List<string> cars = DataStore.LoadCarNames();
            string current = ToSettings.GetDefaultCar();
            string[] options = new string[cars.Count + 1];
            options[0] = "(не задан)";
            for (int i = 0; i < cars.Count; i++)
                options[i + 1] = cars[i];
            string selected = current.Length > 0 ? current : options[0];
            using (EntryEditorForm editor = new EntryEditorForm("Авто по умолчанию",
                new EntryField[] {
                    new EntryField("Автомобиль", selected, options, true)
                }))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] values = editor.Values;
                string name = values[0] == "(не задан)" ? "" : values[0];
                if (!TrySave(delegate { ToSettings.SetDefaultCar(name); }))
                    return;
                if (name.Length > 0)
                    currentCar = name;
                MessageBox.Show(name.Length == 0
                    ? "Авто по умолчанию сброшен."
                    : "Авто по умолчанию: " + name, "Авто по умолчанию");
                if (screenType == "home")
                    ShowHome();
            }
        }

        private void ManageRemindersFromMenu(object sender, EventArgs e)
        {
            DateReminders.EnsureFile();
            MessageBox.Show(DateReminders.FormatAllBlock(DateTime.Today), "Напоминания");
            using (EntryEditorForm editor = new EntryEditorForm("Напоминания",
                new EntryField[] {
                    new EntryField("Действие", "Добавить",
                        new string[] { "Добавить", "Удалить", "Срок предупреждения" }, true),
                    new EntryField("Номер (для удаления)", "", "text", false)
                }))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string action = editor.Values[0];
                if (action == "Срок предупреждения")
                {
                    EditReminderWarnDays();
                    return;
                }
                if (action == "Удалить")
                {
                    List<DateReminder> list = DateReminders.ListAll();
                    string numText = editor.Values[1] == null ? "" : editor.Values[1].Trim();
                    int index = 0;
                    bool parsed = false;
                    try
                    {
                        index = int.Parse(numText, NumberStyles.Integer, CultureInfo.InvariantCulture);
                        parsed = true;
                    }
                    catch (FormatException) { }
                    catch (OverflowException) { }
                    if (!parsed || index < 1 || index > list.Count)
                    {
                        MessageBox.Show("Укажите номер из списка.", "Напоминания");
                        return;
                    }
                    DateReminder item = list[index - 1];
                    if (!TrySave(delegate { DateReminders.DeleteById(item.Id); }))
                        return;
                    MessageBox.Show("Удалено.", "Напоминания");
                    if (screenType == "summary")
                        summaryBox.Text = BuildSummary();
                    return;
                }
                // Добавить
                string car = currentCar == null ? "" : currentCar;
                string defaultDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd",
                    CultureInfo.InvariantCulture);
                using (EntryEditorForm add = new EntryEditorForm("Новое напоминание",
                    new EntryField[] {
                        new EntryField("Дата", defaultDate, "date", true),
                        new EntryField("Автомобиль", car, "text", true),
                        new EntryField("Название", "", "text", true),
                        new EntryField("Тип", "Страховка",
                            new string[] { "Страховка", "Техосмотр", "Своё" }, true)
                    }))
                {
                    if (add.ShowDialog() != DialogResult.OK)
                        return;
                    DateReminder item = new DateReminder();
                    item.Date = add.Values[0];
                    item.Car = add.Values[1];
                    item.Title = add.Values[2];
                    item.Type = DateReminders.NormalizeType(add.Values[3]);
                    if (!TrySave(delegate { DateReminders.Upsert(item); }))
                        return;
                    MessageBox.Show("Напоминание сохранено.", "Напоминания");
                    if (screenType == "summary")
                        summaryBox.Text = BuildSummary();
                }
            }
        }

        private void EditReminderWarnDays()
        {
            int days = ToSettings.GetReminderWarnDays();
            using (EntryEditorForm editor = new EntryEditorForm("Срок напоминаний",
                new EntryField[] {
                    new EntryField("Дней до срока",
                        days.ToString(CultureInfo.InvariantCulture), "number", true)
                }))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                decimal value;
                if (!DataStore.TryNumber(editor.Values[0], out value) || value < 0 || value > 3650 ||
                    value != Decimal.Truncate(value))
                {
                    MessageBox.Show("Срок: целое 0–3650.", "Напоминания");
                    return;
                }
                if (!TrySave(delegate { ToSettings.SetReminderWarnDays((int)value); }))
                    return;
                MessageBox.Show("Предупреждать за " +
                    ((int)value).ToString(CultureInfo.InvariantCulture) + " дн.", "Напоминания");
                if (screenType == "summary")
                    summaryBox.Text = BuildSummary();
            }
        }

        private void ManageTemplatesFromMenu(object sender, EventArgs e)
        {
            string[] typeOptions = new string[] { "Заправка", "Ремонт", "Прочий расход" };
            string[] typeKeys = new string[] {
                RecordTemplates.TypeFuel, RecordTemplates.TypeRepair, RecordTemplates.TypeExpense
            };
            using (EntryEditorForm pick = new EntryEditorForm("Шаблоны",
                new EntryField[] {
                    new EntryField("Тип", typeOptions[0], typeOptions, true)
                }))
            {
                if (pick.ShowDialog() != DialogResult.OK)
                    return;
                string chosen = pick.Values[0];
                string type = RecordTemplates.TypeFuel;
                for (int i = 0; i < typeOptions.Length; i++)
                    if (typeOptions[i] == chosen)
                        type = typeKeys[i];
                ShowTemplatesForType(type);
            }
        }

        private void ShowTemplatesForType(string type)
        {
            List<RecordTemplate> list = RecordTemplates.ListByType(type);
            StringBuilder sb = new StringBuilder();
            sb.Append("Шаблоны · ").Append(RecordTemplates.TypeLabelRu(type));
            sb.Append("\r\n\r\n");
            if (list.Count == 0)
                sb.Append("Пока нет шаблонов.\r\n");
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append("\r\n");
                    sb.Append(i + 1).Append(". ").Append(list[i].Name);
                }
            }
            using (EntryEditorForm editor = new EntryEditorForm("Шаблоны",
                new EntryField[] {
                    new EntryField("Действие", "Добавить из шаблона",
                        new string[] { "Добавить из шаблона", "Сохранить текущую как шаблон", "Удалить шаблон" }, true),
                    new EntryField("Номер / имя", list.Count > 0 ? list[0].Name : "", "text", false)
                }))
            {
                MessageBox.Show(sb.ToString(), "Шаблоны");
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string action = editor.Values[0];
                string name = editor.Values[1] == null ? "" : editor.Values[1].Trim();
                if (action == "Удалить шаблон")
                {
                    if (name.Length == 0)
                    {
                        MessageBox.Show("Укажите имя шаблона.", "Шаблоны");
                        return;
                    }
                    if (!TrySave(delegate { RecordTemplates.Delete(type, name); }))
                        return;
                    MessageBox.Show("Удалено: " + name, "Шаблоны");
                    return;
                }
                if (action == "Сохранить текущую как шаблон")
                {
                    PromptSaveTemplate(type);
                    return;
                }
                // Добавить из шаблона
                RecordTemplate chosen = null;
                for (int i = 0; i < list.Count; i++)
                    if (String.Compare(list[i].Name, name, true,
                            System.Globalization.CultureInfo.InvariantCulture) == 0)
                        chosen = list[i];
                if (chosen == null && list.Count > 0 && name.Length == 0)
                    chosen = list[0];
                if (chosen == null)
                {
                    MessageBox.Show("Шаблон не найден.", "Шаблоны");
                    return;
                }
                ApplyTemplate(chosen);
            }
        }

        private void PromptSaveTemplate(string type)
        {
            if (type == RecordTemplates.TypeFuel)
                SaveFuelAsTemplate();
            else if (type == RecordTemplates.TypeRepair)
                SaveRepairAsTemplate();
            else
                SaveExpenseAsTemplate();
        }

        private void SaveFuelAsTemplate()
        {
            string[] cars = DataStore.LoadCarNames().ToArray();
            using (EntryEditorForm editor = new EntryEditorForm("Шаблон заправки",
                new EntryField[] {
                    new EntryField("Название шаблона", "", "text", true),
                    new EntryField("Автомобиль", currentCar, cars, true),
                    new EntryField("Пробег, км", LastCarMileage(currentCar), "number", false),
                    new EntryField("Литры", "", "number", false),
                    new EntryField("Стоимость", "", "number", false),
                    new EntryField("Цена за литр", LastFuelPricePerLiter(currentCar), "number", false),
                    new EntryField("АЗС / комментарий", "", "text", false),
                    new EntryField("Полный бак (1/0)", "1", "text", false),
                    new EntryField("Цель", "", "text", false)
                }))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] v = editor.Values;
                RecordTemplate item = new RecordTemplate();
                item.Type = RecordTemplates.TypeFuel;
                item.Name = v[0];
                item.Payload = new string[] { v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8] };
                if (!TrySave(delegate { RecordTemplates.Upsert(item); }))
                    return;
                MessageBox.Show("Шаблон сохранён.", "Шаблоны");
            }
        }

        private void SaveRepairAsTemplate()
        {
            string[] cars = DataStore.LoadCarNames().ToArray();
            string[] cats = DataStore.LoadRepairCategories();
            using (EntryEditorForm editor = new EntryEditorForm("Шаблон ремонта",
                new EntryField[] {
                    new EntryField("Название шаблона", "", "text", true),
                    new EntryField("Автомобиль", currentCar, cars, true),
                    new EntryField("Пробег, км", LastCarMileage(currentCar), "number", false),
                    new EntryField("Категория", cats.Length > 0 ? cats[0] : "", cats, true),
                    new EntryField("Товар", "", "text", false),
                    new EntryField("Наименование", "", "text", false),
                    new EntryField("Кол-во", "1", "number", false),
                    new EntryField("Сумма", "", "number", false),
                    new EntryField("Комментарий", "", "text", false)
                }))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] v = editor.Values;
                RecordTemplate item = new RecordTemplate();
                item.Type = RecordTemplates.TypeRepair;
                item.Name = v[0];
                item.Payload = new string[] { v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8] };
                if (!TrySave(delegate { RecordTemplates.Upsert(item); }))
                    return;
                MessageBox.Show("Шаблон сохранён.", "Шаблоны");
            }
        }

        private void SaveExpenseAsTemplate()
        {
            string[] cars = DataStore.LoadCarNames().ToArray();
            string[] cats = DataStore.LoadExpenseCategories();
            using (EntryEditorForm editor = new EntryEditorForm("Шаблон прочего",
                new EntryField[] {
                    new EntryField("Название шаблона", "", "text", true),
                    new EntryField("Автомобиль", currentCar, cars, true),
                    new EntryField("Категория", cats.Length > 0 ? cats[0] : "", cats, true),
                    new EntryField("Название", "", "text", false),
                    new EntryField("Сумма", "", "number", false),
                    new EntryField("Комментарий", "", "text", false)
                }))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] v = editor.Values;
                RecordTemplate item = new RecordTemplate();
                item.Type = RecordTemplates.TypeExpense;
                item.Name = v[0];
                item.Payload = new string[] { v[1], v[2], v[3], v[4], v[5] };
                if (!TrySave(delegate { RecordTemplates.Upsert(item); }))
                    return;
                MessageBox.Show("Шаблон сохранён.", "Шаблоны");
            }
        }

        private void ApplyTemplate(RecordTemplate chosen)
        {
            string date = DateTime.Today.ToString("yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture);
            string[] entity = RecordTemplates.BuildEntityRow(chosen, date, currentCar);
            if (chosen.Type == RecordTemplates.TypeFuel)
            {
                // Open fuel editor with template values via existing AddFuel flow fields
                string[] cars = DataStore.LoadCarNames().ToArray();
                string km = entity.Length > 2 ? entity[2] : LastCarMileage(currentCar);
                string liters = entity.Length > 3 ? entity[3] : "";
                string cost = entity.Length > 4 ? entity[4] : "";
                string price = entity.Length > 5 ? entity[5] : LastFuelPricePerLiter(currentCar);
                string comment = entity.Length > 6 ? entity[6] : "";
                string full = entity.Length > 7 && entity[7].Length > 0 ? entity[7] : "1";
                string purpose = entity.Length > 8 ? entity[8] : "";
                using (EntryEditorForm editor = new EntryEditorForm("Заправка из шаблона",
                    new EntryField[] {
                        new EntryField("Дата", date, "date", true),
                        new EntryField("Автомобиль", currentCar, cars, true),
                        new EntryField("Пробег, км", km, "number", true),
                        new EntryField("Литры", liters, "number", true),
                        new EntryField("Стоимость", cost, "number", true),
                        new EntryField("Цена за литр", price, "number", false),
                        new EntryField("АЗС / комментарий", comment, "text", false),
                        new EntryField("Полный бак (1/0)", full, "text", false),
                        new EntryField("Цель", purpose, "text", false)
                    }))
                {
                    if (editor.ShowDialog() != DialogResult.OK)
                        return;
                    string[] values = editor.Values;
                    if (!TrySave(delegate { DataStore.AddFuel(NormalizeFuelValues(values)); }))
                        return;
                    currentCar = values[1];
                    MessageBox.Show("Заправка добавлена.", "Шаблоны");
                }
            }
            else if (chosen.Type == RecordTemplates.TypeRepair)
            {
                string[] cars = DataStore.LoadCarNames().ToArray();
                string[] cats = DataStore.LoadRepairCategories();
                using (EntryEditorForm editor = new EntryEditorForm("Ремонт из шаблона",
                    new EntryField[] {
                        new EntryField("Дата", date, "date", true),
                        new EntryField("Автомобиль", currentCar, cars, true),
                        new EntryField("Пробег, км", entity.Length > 2 ? entity[2] : LastCarMileage(currentCar), "number", true),
                        new EntryField("Категория", entity.Length > 3 ? entity[3] : (cats.Length > 0 ? cats[0] : ""), cats, true),
                        new EntryField("Товар", entity.Length > 4 ? entity[4] : "", "text", false),
                        new EntryField("Наименование", entity.Length > 5 ? entity[5] : "", "text", false),
                        new EntryField("Кол-во", entity.Length > 6 ? entity[6] : "1", "number", true),
                        new EntryField("Сумма", entity.Length > 7 ? entity[7] : "", "number", true),
                        new EntryField("Комментарий", entity.Length > 8 ? entity[8] : "", "text", false)
                    }))
                {
                    if (editor.ShowDialog() != DialogResult.OK)
                        return;
                    string[] values = editor.Values;
                    if (!TrySave(delegate { DataStore.AddRepair(values); }))
                        return;
                    currentCar = values[1];
                    MessageBox.Show("Ремонт добавлен.", "Шаблоны");
                }
            }
            else
            {
                string[] cars = DataStore.LoadCarNames().ToArray();
                string[] cats = DataStore.LoadExpenseCategories();
                using (EntryEditorForm editor = new EntryEditorForm("Прочее из шаблона",
                    new EntryField[] {
                        new EntryField("Дата", date, "date", true),
                        new EntryField("Автомобиль", currentCar, cars, true),
                        new EntryField("Категория", entity.Length > 2 ? entity[2] : (cats.Length > 0 ? cats[0] : ""), cats, true),
                        new EntryField("Название", entity.Length > 3 ? entity[3] : "", "text", true),
                        new EntryField("Сумма", entity.Length > 4 ? entity[4] : "", "number", true),
                        new EntryField("Комментарий", entity.Length > 5 ? entity[5] : "", "text", false)
                    }))
                {
                    if (editor.ShowDialog() != DialogResult.OK)
                        return;
                    string[] values = editor.Values;
                    if (!TrySave(delegate { DataStore.AddExpense(values); }))
                        return;
                    currentCar = values[1];
                    MessageBox.Show("Прочий расход добавлен.", "Шаблоны");
                }
            }
        }

        private void EditBudget(object sender, EventArgs e)
        {
            decimal budget = ToSettings.GetBudgetMonth();
            decimal warnPct = ToSettings.GetBudgetWarnPct();
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Лимит (0 = выкл.)",
                    budget.ToString(CultureInfo.InvariantCulture), "number", true),
                new EntryField("Предупр. %",
                    warnPct.ToString(CultureInfo.InvariantCulture), "number", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Бюджет месяца", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                decimal monthLimit, pct;
                if (!DataStore.TryNumber(editor.Values[0], out monthLimit) || monthLimit < 0)
                {
                    MessageBox.Show("Лимит: 0 (выкл.) или положительное число.", "Бюджет");
                    return;
                }
                if (!DataStore.TryNumber(editor.Values[1], out pct) || pct <= 0 || pct > 100)
                {
                    MessageBox.Show("Порог предупреждения: от 1 до 100 %.", "Бюджет");
                    return;
                }
                try
                {
                    if (!TrySave(delegate { ToSettings.SetBudget(monthLimit, pct); }))
                        return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Бюджет");
                    return;
                }
                if (monthLimit <= 0)
                    MessageBox.Show("Месячный бюджет выключен.", "Бюджет");
                else
                    MessageBox.Show("Бюджет месяца: " + Money(monthLimit) + " " + Cur() +
                        ", предупреждение с " + Money(pct) + "%.", "Бюджет");
                if (summaryBox != null)
                    summaryBox.Text = BuildSummary();
            }
        }

    }
}
