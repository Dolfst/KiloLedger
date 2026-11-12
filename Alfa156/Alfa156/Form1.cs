using System;
using System.Collections.Generic;
using System.Globalization;
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

        public Form1()
        {
            InitializeComponent();
            DataStore.Initialize();
            currentCar = DataStore.LoadCarNames()[0];
            ShowHome();
        }

        private void ShowHome()
        {
            Controls.Clear();
            Text = "Учет расходов";
            MainMenu menu = new MainMenu();
            MenuItem cars = new MenuItem("Авто");
            cars.MenuItems.Add(new MenuItem("Выбрать / добавить", new EventHandler(ShowCars)));
            menu.MenuItems.Add(cars);
            menu.MenuItems.Add(new MenuItem("Выход", new EventHandler(ExitApplication)));
            Menu = menu;

            Label selectedCar = new Label();
            selectedCar.Location = new System.Drawing.Point(4, 4);
            selectedCar.Size = new System.Drawing.Size(225, 28);
            selectedCar.Text = "Автомобиль: " + currentCar;
            Controls.Add(selectedCar);

            AddHomeButton(FuelSection, 35);
            AddHomeButton(RepairSection, 78);
            AddHomeButton(UtilitiesSection, 121);
            AddHomeButton("Сводка и ТО", 164);
        }

        private void AddHomeButton(string section, int top)
        {
            Button button = new Button();
            button.Location = new System.Drawing.Point(5, top);
            button.Size = new System.Drawing.Size(225, 38);
            button.Text = section;
            button.Tag = section;
            button.Click += new EventHandler(OpenSection);
            Controls.Add(button);
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
            currentSection = section;
            Controls.Clear();
            Text = section;

            Label heading = new Label();
            heading.Location = new System.Drawing.Point(4, 3);
            heading.Size = new System.Drawing.Size(225, 22);
            heading.Text = section == UtilitiesSection ? section : section + ": " + currentCar;
            Controls.Add(heading);

            recordList = new ListBox();
            recordList.Location = new System.Drawing.Point(3, 26);
            recordList.Size = new System.Drawing.Size(230, 225);
            recordList.HorizontalScrollbar = true;
            Controls.Add(recordList);

            displayedRecords = new List<string[]>();
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

                displayedRecords.Add(row);
                if (section == FuelSection)
                    recordList.Items.Add(FormatFuel(row, records));
                else if (section == RepairSection)
                    recordList.Items.Add(row[0] + " " + row[3] + ": " +
                        ShortText(row[4], 18) + " " + row[7] + " BYN");
                else
                    recordList.Items.Add(row[0] + "  " + UtilityTotal(row).ToString("0.##", CultureInfo.InvariantCulture) + " BYN");
            }

            MainMenu menu = new MainMenu();
            MenuItem actions = new MenuItem("Действия");
            actions.MenuItems.Add(new MenuItem("Добавить", new EventHandler(AddCurrentRecord)));
            actions.MenuItems.Add(new MenuItem("Подробно", new EventHandler(ShowRecordDetails)));
            menu.MenuItems.Add(actions);
            menu.MenuItems.Add(new MenuItem("Назад", new EventHandler(ShowHome)));
            Menu = menu;
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
                return 0;
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

        private void ShowRecordDetails(object sender, EventArgs e)
        {
            if (recordList.SelectedIndex < 0 || displayedRecords == null)
            {
                MessageBox.Show("Сначала выберите запись.", "Учет");
                return;
            }

            string[] row = displayedRecords[recordList.SelectedIndex];
            string details;
            if (currentSection == FuelSection)
            {
                details = "Авто: " + row[1] + "\nДата: " + row[0] +
                    "\nПробег: " + row[2] + " км\nЛитры: " + row[3] +
                    "\nСтоимость: " + row[4] + " BYN";
            }
            else if (currentSection == RepairSection)
            {
                details = "Авто: " + row[1] + "\nДата: " + row[0] +
                    "\nПробег: " + row[2] + " км\nКатегория: " + row[3] +
                    "\nТовар: " + row[4] + "\nНаименование: " + row[5] +
                    "\nКоличество: " + row[6] + "\nСумма: " + row[7] +
                    " BYN\nКомментарий: " + row[8];
            }
            else
            {
                string[] labels = DataStore.UtilityLabels;
                details = row[0] + "\n";
                for (int i = 0; i < labels.Length; i++)
                    details += labels[i] + ": " + row[i + 1] + " BYN\n";
                details += "Итого: " + UtilityTotal(row).ToString("0.##", CultureInfo.InvariantCulture) + " BYN";
            }
            MessageBox.Show(details, "Запись");
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
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Дата (гггг-мм-дд)", DateTime.Now.ToString("yyyy-MM-dd"), "date", true),
                new EntryField("Автомобиль", currentCar, cars, true),
                new EntryField("Пробег, км", "", "number", true),
                new EntryField("Литры", "", "number", true),
                new EntryField("Стоимость, BYN", "", "number", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm("Новая заправка", fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] values = editor.Values;
                if (!TrySave(delegate
                {
                    DataStore.AddFuel(new string[] { values[0], values[1], values[2], values[3], values[4] });
                }))
                    return;
                currentCar = values[1];
            }
            ShowRecords(FuelSection);
        }

        private void AddRepair()
        {
            string[] cars = DataStore.LoadCarNames().ToArray();
            EntryField[] fields = new EntryField[]
            {
                new EntryField("Дата (гггг-мм-дд)", DateTime.Now.ToString("yyyy-MM-dd"), "date", true),
                new EntryField("Автомобиль", currentCar, cars, true),
                new EntryField("Пробег, км", "", "number", false),
                new EntryField("Категория", DataStore.RepairCategories[0], DataStore.RepairCategories, true),
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
            Controls.Clear();
            Text = "Автомобили";
            Label heading = new Label();
            heading.Location = new System.Drawing.Point(4, 3);
            heading.Size = new System.Drawing.Size(225, 22);
            heading.Text = "Текущий автомобиль: " + currentCar;
            Controls.Add(heading);

            recordList = new ListBox();
            recordList.Location = new System.Drawing.Point(3, 26);
            recordList.Size = new System.Drawing.Size(230, 225);
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
            MenuItem actions = new MenuItem("Действия");
            actions.MenuItems.Add(new MenuItem("Выбрать", new EventHandler(SelectCar)));
            actions.MenuItems.Add(new MenuItem("Добавить", new EventHandler(AddCar)));
            menu.MenuItems.Add(actions);
            menu.MenuItems.Add(new MenuItem("Назад", new EventHandler(ShowHome)));
            Menu = menu;
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
                if (DataStore.ContainsCar(name))
                {
                    MessageBox.Show("Такой автомобиль уже есть.", "Учет");
                    return;
                }
                if (!TrySave(delegate { DataStore.AddCar(name); }))
                    return;
                currentCar = name;
            }
            ShowCars(sender, e);
        }

        private void ShowSummary()
        {
            Controls.Clear();
            Text = "Сводка";
            Label heading = new Label();
            heading.Location = new System.Drawing.Point(4, 3);
            heading.Size = new System.Drawing.Size(225, 22);
            heading.Text = "Сводка расходов";
            Controls.Add(heading);

            TextBox summary = new TextBox();
            summary.Location = new System.Drawing.Point(3, 26);
            summary.Size = new System.Drawing.Size(230, 225);
            summary.Multiline = true;
            summary.ReadOnly = true;
            summary.ScrollBars = ScrollBars.Vertical;
            try
            {
                summary.Text = BuildSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось построить сводку: " + ex.Message, "Ошибка");
                ShowHome();
                return;
            }
            Controls.Add(summary);

            MainMenu menu = new MainMenu();
            menu.MenuItems.Add(new MenuItem("Назад", new EventHandler(ShowHome)));
            Menu = menu;
        }

        private string BuildSummary()
        {
            decimal fuelTotal = 0;
            List<string[]> fuels = DataStore.LoadFuel();
            for (int i = 0; i < fuels.Count; i++)
                if (fuels[i][1] == currentCar)
                    fuelTotal += DataStore.NumberOrZero(fuels[i][4]);

            decimal repairTotal = 0;
            Dictionary<string, decimal> byCategory = new Dictionary<string, decimal>();
            List<string[]> repairs = DataStore.LoadRepairs();
            for (int i = 0; i < repairs.Count; i++)
            {
                if (repairs[i][1] != currentCar)
                    continue;
                decimal amount = DataStore.NumberOrZero(repairs[i][7]);
                repairTotal += amount;
                string category = repairs[i][3];
                if (!byCategory.ContainsKey(category))
                    byCategory.Add(category, 0);
                byCategory[category] += amount;
            }

            decimal utilityTotal = 0;
            List<string[]> utilities = DataStore.LoadUtilities();
            for (int i = 0; i < utilities.Count; i++)
                utilityTotal += UtilityTotal(utilities[i]);

            string result = currentCar + "\n\nЗаправки: " + Money(fuelTotal) +
                " BYN\nРемонт и ТО: " + Money(repairTotal) + " BYN\n\nРемонт по категориям:\n";
            foreach (KeyValuePair<string, decimal> pair in byCategory)
                result += pair.Key + ": " + Money(pair.Value) + " BYN\n";
            result += "\nКоммунальные за весь период:\n" + Money(utilityTotal) + " BYN\n\n";

            decimal currentMileage = 0;
            decimal lastOilMileage = 0;
            decimal lastTimingMileage = 0;
            List<string[]> fuelsForCar = DataStore.LoadFuel();
            for (int i = 0; i < fuelsForCar.Count; i++)
                if (fuelsForCar[i][1] == currentCar)
                {
                    decimal fuelMileage = DataStore.NumberOrZero(fuelsForCar[i][2]);
                    if (fuelMileage > currentMileage)
                        currentMileage = fuelMileage;
                }
            for (int i = 0; i < repairs.Count; i++)
            {
                string[] row = repairs[i];
                if (row[1] != currentCar)
                    continue;
                decimal mileage = DataStore.NumberOrZero(row[2]);
                if (mileage > currentMileage)
                    currentMileage = mileage;
                if (row[3] == "ТО Масло" && mileage > lastOilMileage)
                    lastOilMileage = mileage;
                if (row[3] == "ТО ГРМ" && mileage > lastTimingMileage)
                    lastTimingMileage = mileage;
            }
            result += "Текущий пробег: " + Money(currentMileage) + " км\n";
            result += ServiceStatus("Замена масла", currentMileage, lastOilMileage, 10000, 1000) + "\n";
            result += ServiceStatus("Ремень ГРМ", currentMileage, lastTimingMileage, 60000, 5000);
            return result;
        }

        private static string ServiceStatus(string name, decimal currentMileage, decimal lastMileage, decimal interval, decimal warningDistance)
        {
            if (lastMileage == 0)
                return name + ": замена не зафиксирована";
            decimal since = currentMileage - lastMileage;
            if (since >= interval)
                return name + ": требуется замена";
            if (since >= interval - warningDistance)
                return name + ": скоро замена (" + Money(interval - since) + " км)";
            return name + ": в норме (" + Money(interval - since) + " км)";
        }

        private static string Money(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
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

        private void ExitApplication(object sender, EventArgs e)
        {
            Close();
        }
    }
}
