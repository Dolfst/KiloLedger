using Alfa156.Localization;
using System;
using System.Collections;
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

        private static string SectionDisplayName(string section)
        {
            if (section == FuelSection) return L10n.Get("tab.fuel");
            if (section == RepairSection) return L10n.Get("tab.repairs");
            if (section == ExpensesSection) return L10n.Get("tab.expenses");
            if (section == UtilitiesSection) return L10n.Get("tab.utilities");
            if (section == "Сводка и ТО") return L10n.Get("button.summary_and_to");
            return section;
        }

        private string currentSection;
        private string currentCar;
        private ListBox recordList;
        private List<string[]> displayedRecords;
        private List<int> displayedRecordIndexes;
        private string screenType;
        private Label homeCarLabel;
        private Panel homeLogoPanel;
        private Panel homeButtonsPanel;
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
            InitializeLanguage();
            try
            {
                DataStore.CreateStartMenuShortcut();
            }
            catch (Exception ex)
            {
                MessageBox.Show((L10n.Get("error.create_shortcut_failed") + " ") + ex.Message, L10n.Get("title.warning"));
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
                    (L10n.Get("label.brief_colon") + "\r\n") +
                    (L10n.Get("about.bullet_wifi_phone") + "\r\n") +
                    (L10n.Get("about.bullet_sd_memory_backups") + "\r\n") +
                    L10n.Get("about.bullet_summary_reminders"),
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
                string age = days < 0 ? L10n.Get("summary.no_backup_yet") : ((L10n.Get("forecast.elapsed") + " ") + days + (" " + L10n.Get("unit.days_short")));
                MessageBox.Show(
                    L10n.Get("reminder.backup_overdue_open") + age + ").\r\n" +
                    (L10n.Get("hint.recommend_sd_backup") + " ") +
                    L10n.Get("backup.all_dat_desc"),
                    L10n.Get("title.backup"));
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
            homeButtonsPanel = null;
            screenHeading = null;
            summaryBox = null;
            recordList = null;
            homeButtons = new List<Button>();
            Text = L10n.Get("app.subtitle");
            MainMenu menu = new MainMenu();
            MenuItem cars = CreateMenuItem(L10n.Get("label.car_short"));
            cars.MenuItems.Add(CreateMenuItem(L10n.Get("button.select_or_add"), new EventHandler(ShowCars)));
            cars.MenuItems.Add(CreateMenuItem(L10n.Get("title.properties"), new EventHandler(EditCarPassport)));
            cars.MenuItems.Add(CreateMenuItem(L10n.Get("label.favorite"), new EventHandler(SetFavoriteCarFromMenu)));
            menu.MenuItems.Add(cars);
            MenuItem data = CreateMenuItem(L10n.Get("menu.data"));
            data.MenuItems.Add(CreateMenuItem(L10n.Get("backup.create_in_pda_memory"), new EventHandler(CreateLocalBackupFromMenu)));
            data.MenuItems.Add(CreateMenuItem(L10n.Get("backup.restore_from_pda_memory"), new EventHandler(RestoreLocalBackupFromMenu)));
            data.MenuItems.Add(CreateMenuItem(L10n.Get("backup.create_on_sd"), new EventHandler(CreateBackupFromMenu)));
            data.MenuItems.Add(CreateMenuItem(L10n.Get("title.restore_backup"), new EventHandler(RestoreBackupFromMenu)));
            data.MenuItems.Add(CreateMenuItem(L10n.Get("sync.wifi_title"), new EventHandler(SyncWithAndroidFromMenu)));
            data.MenuItems.Add(CreateMenuItem(L10n.Get("title.export_summary"), new EventHandler(ExportSummaryFromMenu)));
            data.MenuItems.Add(CreateMenuItem(L10n.Get("title.period_report"), new EventHandler(ExportPeriodReportFromMenu)));
            data.MenuItems.Add(CreateMenuItem(L10n.Get("button.year_report"), new EventHandler(ExportYearReportFromMenu)));
            data.MenuItems.Add(CreateMenuItem(L10n.Get("title.about"), new EventHandler(ShowAbout)));
            menu.MenuItems.Add(data);
            MenuItem actionsHome = CreateMenuItem(L10n.Get("menu.actions"));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("title.search"), new EventHandler(ShowGlobalSearch)));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("button.duplicates"), new EventHandler(ShowDuplicatesFromMenu)));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("button.data_integrity"), new EventHandler(ShowDataIntegrityFromMenu)));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("label.service_intervals"), new EventHandler(EditToIntervals)));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("label.currency"), new EventHandler(EditCurrency)));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("label.language"), new EventHandler(EditLanguage)));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("label.monthly_budget"), new EventHandler(EditBudget)));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("label.default_car_full"), new EventHandler(EditDefaultCar)));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("button.quick_fuel"), new EventHandler(QuickFuelFromMenu)));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("title.new_trip"), new EventHandler(AddTripFromMenu)));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("button.trip_report"), new EventHandler(ShowTripReportFromMenu)));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("label.templates"), new EventHandler(ManageTemplatesFromMenu)));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("label.reminders"), new EventHandler(ManageRemindersFromMenu)));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("label.notes"), new EventHandler(ManageNotesFromMenu)));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("title.backup_reminder"), new EventHandler(EditBackupReminder)));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("button.timeline"), new EventHandler(ShowCarTimeline)));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("button.compare_periods"), new EventHandler(ShowPeriodCompareFromMenu)));
            actionsHome.MenuItems.Add(CreateMenuItem(L10n.Get("title.period_report"), new EventHandler(ExportPeriodReportFromMenu)));
            menu.MenuItems.Add(actionsHome);
            menu.MenuItems.Add(CreateMenuItem(L10n.Get("button.exit"), new EventHandler(ExitApplication)));
            Menu = menu;

            homeLogoPanel = new Panel();
            homeLogoPanel.BackColor = Color.FromArgb(35, 51, 70);
            homeLogoPanel.Paint += new PaintEventHandler(DrawLogo);
            Controls.Add(homeLogoPanel);

            homeCarLabel = new Label();
            homeCarLabel.Text = (L10n.Get("label.vehicle_colon") + " ") + currentCar + CarsMeta.HeaderSuffix(currentCar);
            homeCarLabel.BackColor = Color.FromArgb(232, 238, 244);
            homeCarLabel.ForeColor = Color.FromArgb(35, 51, 70);
            homeCarLabel.Font = new Font("Arial", 10, FontStyle.Bold);
            Controls.Add(homeCarLabel);

            homeButtonsPanel = new Panel();
            homeButtonsPanel.BackColor = Color.White;
            homeButtonsPanel.AutoScroll = true;
            Controls.Add(homeButtonsPanel);

            AddHomeButton(FuelSection);
            AddHomeButton(RepairSection);
            AddHomeButton(ExpensesSection);
            AddHomeButton(UtilitiesSection);
            AddHomeButton(L10n.Get("button.summary_and_to"));
            LayoutScreen();
        }

        private void AddHomeButton(string section)
        {
            Button button = new Button();
            button.Text = "  " + SectionDisplayName(section);
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
            if (homeButtonsPanel != null)
                homeButtonsPanel.Controls.Add(button);
            else
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
            homeButtonsPanel = null;
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
                heading += (" " + L10n.Get("label.filter_bracket"));
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
                MessageBox.Show((L10n.Get("error.read_records_failed") + " ") + ex.Message, L10n.Get("title.error"));
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
                    ? L10n.Get("msg.no_records_for_filter")
                    : L10n.Get("msg.no_records_actions_add"));
            }

            MainMenu menu = new MainMenu();
            MenuItem actions = CreateMenuItem(L10n.Get("menu.actions"));
            actions.MenuItems.Add(CreateMenuItem(L10n.Get("button.add"), new EventHandler(AddCurrentRecord)));
            if (section == FuelSection)
                actions.MenuItems.Add(CreateMenuItem(L10n.Get("button.quick_fuel"), new EventHandler(QuickFuelFromMenu)));
            actions.MenuItems.Add(CreateMenuItem(L10n.Get("label.templates"), new EventHandler(ManageTemplatesFromMenu)));
            actions.MenuItems.Add(CreateMenuItem(L10n.Get("button.details"), new EventHandler(ShowRecordDetails)));
            actions.MenuItems.Add(CreateMenuItem(L10n.Get("button.edit"), new EventHandler(EditCurrentRecord)));
            actions.MenuItems.Add(CreateMenuItem(L10n.Get("button.delete"), new EventHandler(DeleteCurrentRecord)));
            if (section == FuelSection || section == RepairSection || section == ExpensesSection)
            {
                actions.MenuItems.Add(CreateMenuItem(L10n.Get("button.attach_receipt"), new EventHandler(OpenReceiptPhoto)));
                actions.MenuItems.Add(CreateMenuItem(L10n.Get("button.delete_receipt"), new EventHandler(DeleteReceiptPhoto)));
            }
            actions.MenuItems.Add(CreateMenuItem(L10n.Get("title.search"), new EventHandler(ShowGlobalSearch)));
            if (section == FuelSection)
            {
                actions.MenuItems.Add(CreateMenuItem(L10n.Get("label.filter"), new EventHandler(EditFuelFilter)));
                actions.MenuItems.Add(CreateMenuItem(L10n.Get("button.reset_filter"), new EventHandler(ResetFuelFilter)));
            }
            else if (section == RepairSection)
            {
                actions.MenuItems.Add(CreateMenuItem(L10n.Get("label.filter"), new EventHandler(EditRepairFilter)));
                actions.MenuItems.Add(CreateMenuItem(L10n.Get("button.reset_filter"), new EventHandler(ResetRepairFilter)));
                actions.MenuItems.Add(CreateMenuItem(L10n.Get("title.add_category"), new EventHandler(AddRepairCategory)));
                actions.MenuItems.Add(CreateMenuItem(L10n.Get("title.rename_category"), new EventHandler(RenameRepairCategory)));
                actions.MenuItems.Add(CreateMenuItem(L10n.Get("title.delete_category"), new EventHandler(DeleteRepairCategory)));
            }
            else if (section == ExpensesSection)
            {
                actions.MenuItems.Add(CreateMenuItem(L10n.Get("title.add_category"), new EventHandler(AddExpenseCategory)));
                actions.MenuItems.Add(CreateMenuItem(L10n.Get("title.rename_category"), new EventHandler(RenameExpenseCategory)));
                actions.MenuItems.Add(CreateMenuItem(L10n.Get("title.delete_category"), new EventHandler(DeleteExpenseCategory)));
            }
            menu.MenuItems.Add(actions);
            menu.MenuItems.Add(CreateMenuItem(L10n.Get("button.back"), new EventHandler(ShowHomeFromMenu)));
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
                new EntryField(L10n.Get("label.date_from_fmt"), fuelFilterDateFrom, "date", false),
                new EntryField(L10n.Get("label.date_to_fmt"), fuelFilterDateTo, "date", false),
                new EntryField(L10n.Get("error.odometer_liters_cost"), fuelFilterText, "text", false)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.fuel_filter"), fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] values = editor.Values;
                if (values[0] != "" && values[1] != "" &&
                    String.CompareOrdinal(values[0], values[1]) > 0)
                {
                    MessageBox.Show(L10n.Get("error.period_start_after_end"), L10n.Get("label.filter"));
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
                new EntryField(L10n.Get("label.category"), filterCategory, categories.ToArray(), true),
                new EntryField(L10n.Get("label.date_from_fmt"), filterDateFrom, "date", false),
                new EntryField(L10n.Get("label.date_to_fmt"), filterDateTo, "date", false),
                new EntryField(L10n.Get("label.name_or_item"), filterText, "text", false)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.repair_filter"), fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] values = editor.Values;
                if (values[1] != "" && values[2] != "" &&
                    String.CompareOrdinal(values[1], values[2]) > 0)
                {
                    MessageBox.Show(L10n.Get("error.period_start_after_end"), L10n.Get("label.filter"));
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
                new EntryField(L10n.Get("label.name"), "", "text", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.new_category"), fields))
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
                MessageBox.Show(L10n.Get("error.no_categories_to_rename"), L10n.Get("tab.categories"));
                return;
            }
            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("label.category"), categories[0], categories, true),
                new EntryField(L10n.Get("label.new_name"), "", "text", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.rename_category"), fields))
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
                MessageBox.Show(L10n.Get("error.no_categories_to_delete"), L10n.Get("tab.categories"));
                return;
            }
            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("label.category"), categories[0], categories, true)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.delete_category"), fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string name = editor.Values[0];
                if (!ConfirmAction(L10n.Get("title.confirmation"),
                    L10n.Get("confirm.delete_category_detail"),
                    L10n.Get("button.delete")))
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
            string result = row[0] + "  " + row[2] + (" " + L10n.Get("unit.km") + "  ") + row[3] + (" " + L10n.Get("unit.liters_short") + "  ") + row[4] + " " + Cur();
            string station = GetStation(row);
            if (station.Length > 0)
                result += "  " + station;
            string purpose = GetPurpose(row);
            if (purpose.Length > 0)
                result += "  " + purpose;
            decimal consumption = FuelConsumption(row, allRows);
            if (consumption > 0)
                result += "  " + consumption.ToString("0.0", CultureInfo.InvariantCulture) + (" " + L10n.Get("unit.l_per_100_short"));
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

        private void OpenReceiptPhoto(object sender, EventArgs e)
        {
            if (!HasSelectedRecord())
            {
                MessageBox.Show(L10n.Get("error.select_record_first"), L10n.Get("button.attach_receipt"));
                return;
            }
            string[] row = displayedRecords[recordList.SelectedIndex];
            string recordId = RecordIds.GetId(row);
            if (String.IsNullOrEmpty(recordId))
            {
                MessageBox.Show(L10n.Get("error.record_no_stable_id"), L10n.Get("button.attach_receipt"));
                return;
            }
            if (!DataStore.ReceiptExists(recordId))
            {
                MessageBox.Show(L10n.Get("error.no_receipt_for_record"), L10n.Get("button.attach_receipt"));
                return;
            }
            try
            {
                System.Diagnostics.ProcessStartInfo startInfo =
                    new System.Diagnostics.ProcessStartInfo();
                startInfo.FileName = DataStore.ReceiptPathForId(recordId);
                System.Diagnostics.Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show((L10n.Get("error.open_photo_failed") + " ") + ex.Message, L10n.Get("button.attach_receipt"));
            }
        }

        private void DeleteReceiptPhoto(object sender, EventArgs e)
        {
            if (!HasSelectedRecord())
            {
                MessageBox.Show(L10n.Get("error.select_record_first"), L10n.Get("button.attach_receipt"));
                return;
            }
            string[] row = displayedRecords[recordList.SelectedIndex];
            string recordId = RecordIds.GetId(row);
            if (String.IsNullOrEmpty(recordId))
            {
                MessageBox.Show(L10n.Get("error.record_no_stable_id"), L10n.Get("button.attach_receipt"));
                return;
            }
            if (!DataStore.ReceiptExists(recordId))
            {
                MessageBox.Show(L10n.Get("error.no_receipt_for_record"), L10n.Get("button.attach_receipt"));
                return;
            }
            DialogResult answer = MessageBox.Show(
                L10n.Get("confirm.delete_receipt_photo"),
                L10n.Get("button.attach_receipt"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
                return;
            try
            {
                DataStore.DeleteReceipt(recordId);
                MessageBox.Show(L10n.Get("msg.receipt_deleted"), L10n.Get("button.attach_receipt"));
            }
            catch (Exception ex)
            {
                MessageBox.Show((L10n.Get("error.delete_photo_failed") + " ") + ex.Message, L10n.Get("button.attach_receipt"));
            }
        }

        private void ShowRecordDetails(object sender, EventArgs e)
        {
            if (!HasSelectedRecord())
            {
                MessageBox.Show(L10n.Get("error.select_record_first"), L10n.Get("title.accounting"));
                return;
            }

            string[] row = displayedRecords[recordList.SelectedIndex];
            string details;
            if (currentSection == FuelSection)
            {
                details = (L10n.Get("label.car_colon") + " ") + row[1] + ("\r\n" + L10n.Get("label.date_colon") + " ") + row[0] +
                    ("\r\n" + L10n.Get("label.odometer_colon") + " ") + row[2] + (" " + L10n.Get("msg.km_newline_liters") + " ") + row[3] +
                    ("\r\n" + L10n.Get("label.cost_colon") + " ") + row[4] + " " + Cur();
                string storedPrice = FieldAt(row, 5).Trim();
                decimal liters;
                decimal cost;
                if (storedPrice.Length > 0)
                    details += ("\r\n" + L10n.Get("label.price_per_liter_colon") + " ") + storedPrice + " " + Cur();
                else if (DataStore.TryNumber(row[3], out liters) && liters > 0 &&
                    DataStore.TryNumber(row[4], out cost))
                    details += ("\r\n" + L10n.Get("label.price_per_liter_colon") + " ") +
                        (cost / liters).ToString("0.00", CultureInfo.InvariantCulture) + " " + Cur();
                string station = GetStation(row);
                if (station.Length > 0)
                    details += ("\r\n" + L10n.Get("label.station_colon") + " ") + station;
                string comment = FieldAt(row, 6).Trim();
                if (comment.Length > 0)
                    details += ("\r\n" + L10n.Get("label.comment_colon") + " ") + comment;
                details += ("\r\n" + L10n.Get("label.full_tank_colon") + " ") + (IsFullTank(row) ? L10n.Get("common.yes_lower") : L10n.Get("common.no_lower"));
                string purpose = GetPurpose(row);
                if (purpose.Length > 0)
                    details += ("\r\n" + L10n.Get("label.purpose_colon") + " ") + purpose;
                decimal consumption = FuelConsumption(row, DataStore.LoadFuel());
                if (consumption > 0)
                    details += ("\r\n" + L10n.Get("report.consumption_colon") + " ") +
                        consumption.ToString("0.0", CultureInfo.InvariantCulture) + (" " + L10n.Get("unit.l_per_100km"));
            }
            else if (currentSection == RepairSection)
            {
                details = (L10n.Get("label.car_colon") + " ") + row[1] + ("\r\n" + L10n.Get("label.date_colon") + " ") + row[0] +
                    ("\r\n" + L10n.Get("label.odometer_colon") + " ") + row[2] + (" " + L10n.Get("msg.km_newline_category") + " ") + row[3] +
                    ("\r\n" + L10n.Get("label.product_colon") + " ") + row[4] + ("\r\n" + L10n.Get("label.item_name_colon") + " ") + row[5] +
                    ("\r\n" + L10n.Get("label.quantity_colon") + " ") + row[6] + ("\r\n" + L10n.Get("label.amount_colon") + " ") + row[7] +
                    " " + Cur() + ("\r\n" + L10n.Get("label.comment_colon") + " ") + row[8];
            }
            else
            {
                string[] labels = new string[DataStore.UtilityLabels.Length];
                for (int ui = 0; ui < labels.Length; ui++) labels[ui] = DataStore.GetUtilityLabel(ui);
                details = row[0] + "\r\n";
                for (int i = 0; i < labels.Length; i++)
                    details += labels[i] + ": " + row[i + 1] + " " + Cur() + "\r\n";
                details += (L10n.Get("report.subtotal_colon") + " ") + UtilityTotal(row).ToString("0.##", CultureInfo.InvariantCulture) + " " + Cur();
            }
            MessageBox.Show(details, L10n.Get("label.record"));
        }

        private void EditCurrentRecord(object sender, EventArgs e)
        {
            if (!HasSelectedRecord())
            {
                MessageBox.Show(L10n.Get("error.select_record_first"), L10n.Get("title.accounting"));
                return;
            }

            int selectedIndex = recordList.SelectedIndex;
            int sourceIndex = displayedRecordIndexes[selectedIndex];
            string[] row = displayedRecords[selectedIndex];

            if (currentSection == FuelSection)
            {
                EntryField[] fields = new EntryField[]
                {
                    new EntryField(L10n.Get("label.date_fmt_lower"), row[0], "date", true),
                    new EntryField(L10n.Get("label.vehicle"), row[1], DataStore.LoadCarNames().ToArray(), true),
                    new EntryField(L10n.Get("label.odometer_km"), row[2], "number", true),
                    new EntryField(L10n.Get("label.liters"), row[3], "number", true),
                    new EntryField((L10n.Get("label.cost_comma") + " ") + Cur(), row[4], "number", true),
                    new EntryField((L10n.Get("label.price_per_liter_comma") + " ") + Cur(), FieldAt(row, 5), "number", false),
                    new EntryField(L10n.Get("label.comment"), FieldAt(row, 6), "text", false),
                    new EntryField(L10n.Get("label.full_tank"), FieldAt(row, 7).Length == 0 || FieldAt(row, 7) != "0" ? "1" : "0", "check", false),
                    new EntryField(L10n.Get("label.purpose_hint"), GetPurpose(row), PurposeOptions(GetPurpose(row)), false),
                    EntryField.Suggest(L10n.Get("label.station_brand"), GetStation(row), StationOptions(GetStation(row)), false)
                };
                using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.edit_fuel"), fields))
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
                    new EntryField(L10n.Get("label.date_fmt_lower"), row[0], "date", true),
                    new EntryField(L10n.Get("label.vehicle"), row[1], DataStore.LoadCarNames().ToArray(), true),
                    new EntryField(L10n.Get("label.odometer_km"), row[2], "number", false),
                    new EntryField(L10n.Get("label.category"), row[3], DataStore.LoadRepairCategories(), true),
                    new EntryField(L10n.Get("label.product_part"), row[4], "text", true),
                    new EntryField(L10n.Get("label.item_name"), row[5], "text", false),
                    new EntryField(L10n.Get("label.quantity"), row[6], "number", true),
                    new EntryField((L10n.Get("label.amount_comma") + " ") + Cur(), row[7], "number", true),
                    new EntryField(L10n.Get("label.comment"), row[8], "text", false)
                };
                using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.edit_repair"), fields))
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
                    new EntryField(L10n.Get("label.date_fmt_lower"), row[0], "date", true),
                    new EntryField(L10n.Get("label.vehicle"), row[1], DataStore.LoadCarNames().ToArray(), true),
                    new EntryField(L10n.Get("label.category"), row[2], DataStore.LoadExpenseCategories(), true),
                    new EntryField(L10n.Get("label.name"), row[3], "text", true),
                    new EntryField((L10n.Get("label.amount_comma") + " ") + Cur(), row[4], "number", true),
                    new EntryField(L10n.Get("label.comment"), FieldAt(row, 5), "text", false)
                };
                using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.edit_expense"), fields))
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
                using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.edit_utilities"), fields))
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
            fields[0] = new EntryField(L10n.Get("label.month_fmt"), row[0], "month", true);
            for (int i = 0; i < DataStore.UtilityLabels.Length; i++)
                fields[i + 1] = new EntryField(DataStore.GetUtilityLabel(i) + ", " + Cur(),
                    row[i + 1], "number", false);
            return fields;
        }

        private void DeleteCurrentRecord(object sender, EventArgs e)
        {
            if (!HasSelectedRecord())
            {
                MessageBox.Show(L10n.Get("error.select_record_first"), L10n.Get("title.accounting"));
                return;
            }

            int selectedIndex = recordList.SelectedIndex;
            string[] row = displayedRecords[selectedIndex];
            string description;
            if (currentSection == FuelSection)
                description = row[0] + ", " + row[2] + (" " + L10n.Get("unit.km_comma") + " ") + row[4] + " " + Cur();
            else if (currentSection == RepairSection)
                description = row[0] + ", " + row[3] + ": " + ShortText(row[4], 24);
            else if (currentSection == ExpensesSection)
                description = row[0] + ", " + row[2] + ": " + ShortText(row[3], 24);
            else
                description = (L10n.Get("confirm.utility_month_delete_prefix") + " ") + row[0];

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
            return ConfirmAction(L10n.Get("title.confirm_delete"),
                (L10n.Get("confirm.delete_record") + "\r\n") + description, L10n.Get("button.delete"));
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
                cancel.Text = L10n.Get("button.cancel");
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
                    (L10n.Get("confirm.create_sd_backup") + "\r\n") +
                    (L10n.Get("backup.files_to_copy_colon") + " ") + fileCount.ToString(CultureInfo.InvariantCulture) +
                    ("\r\n" + L10n.Get("backup.dat_desc_dot")),
                    L10n.Get("backup.on_sd"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button1);
                if (confirm != DialogResult.Yes)
                    return;
                string path = DataStore.CreateBackup();
                MessageBox.Show((L10n.Get("msg.backup_created_colon") + "\r\n") + path +
                    ("\r\n" + L10n.Get("backup.files_colon") + " ") + fileCount.ToString(CultureInfo.InvariantCulture) +
                    ("\r\n" + L10n.Get("backup.copy_activesync")),
                    L10n.Get("title.backup"));
            }
            catch (Exception ex)
            {
                MessageBox.Show((L10n.Get("error.create_sd_backup_failed") + " ") + ex.Message,
                    L10n.Get("title.backup_error"));
            }
        }

        private void CreateLocalBackupFromMenu(object sender, EventArgs e)
        {
            try
            {
                int fileCount = DataStore.CountBackupFiles();
                DialogResult confirm = MessageBox.Show(
                    (L10n.Get("confirm.create_pda_backup") + "\r\n") +
                    (L10n.Get("backup.files_to_copy_colon") + " ") + fileCount.ToString(CultureInfo.InvariantCulture) +
                    ("\r\n" + L10n.Get("backup.dat_desc_dot")),
                    L10n.Get("backup.in_memory"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button1);
                if (confirm != DialogResult.Yes)
                    return;
                string path = DataStore.CreateLocalBackup();
                MessageBox.Show((L10n.Get("msg.local_backup_created") + "\r\n") + path +
                    ("\r\n" + L10n.Get("backup.files_colon") + " ") + fileCount.ToString(CultureInfo.InvariantCulture),
                    L10n.Get("title.backup"));
            }
            catch (Exception ex)
            {
                MessageBox.Show((L10n.Get("error.create_local_backup_failed") + " ") + ex.Message,
                    L10n.Get("title.backup_error"));
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
                MessageBox.Show((L10n.Get("error.read_local_backups_failed") + " ") + ex.Message,
                    L10n.Get("title.restore_error"));
                return;
            }

            if (backups.Length == 0)
            {
                MessageBox.Show(L10n.Get("error.no_pda_backups"),
                    L10n.Get("title.restore"));
                return;
            }

            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("prompt.choose_backup_date"), backups[0], backups, true)
            };
            string backupName;
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("backup.local_copy"), fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                backupName = editor.Values[0];
            }

            if (!ConfirmAction(L10n.Get("title.confirm_restore"),
                (L10n.Get("confirm.replace_with_local_backup") + " ") +
                backupName + "?", L10n.Get("button.restore")))
                return;

            try
            {
                DataStore.RestoreLocalBackup(backupName);
                currentCar = DataStore.LoadCarNames()[0];
                ShowHome();
                MessageBox.Show((L10n.Get("msg.data_restored_from_local") + " ") + backupName + ".",
                    L10n.Get("title.restore_complete"));
            }
            catch (Exception ex)
            {
                MessageBox.Show((L10n.Get("error.restore_local_failed") + " ") + ex.Message,
                    L10n.Get("title.restore_error"));
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
                MessageBox.Show((L10n.Get("error.read_sd_backups_failed") + " ") + ex.Message,
                    L10n.Get("title.restore_error"));
                return;
            }

            if (backups.Length == 0)
            {
                MessageBox.Show(L10n.Get("error.no_sd_backups"),
                    L10n.Get("title.restore"));
                return;
            }

            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("prompt.choose_backup_date"), backups[0], backups, true)
            };
            string backupName;
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.choose_backup"), fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                backupName = editor.Values[0];
            }

            if (!ConfirmAction(L10n.Get("title.confirm_restore"),
                (L10n.Get("confirm.replace_with_sd_backup") + " ") +
                backupName + "?", L10n.Get("button.restore")))
                return;

            try
            {
                DataStore.RestoreBackup(backupName);
                currentCar = DataStore.LoadCarNames()[0];
                ShowHome();
                MessageBox.Show((L10n.Get("msg.data_restored_from_backup") + " ") + backupName + ".",
                    L10n.Get("title.restore_complete"));
            }
            catch (Exception ex)
            {
                MessageBox.Show((L10n.Get("error.restore_data_failed") + " ") + ex.Message,
                    L10n.Get("title.restore_error"));
            }
        }

        private void SyncWithAndroidFromMenu(object sender, EventArgs e)
        {
            string suggestedIp = DataStore.LoadLastSyncAddress();
            if (suggestedIp == null || suggestedIp.Length == 0)
                suggestedIp = "192.168.43.1";
            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("sync.android_ip_hint"), suggestedIp, "text", true),
                new EntryField(L10n.Get("sync.pairing_code_label"), "", "text", true)
            };
            string address;
            string pairingCode;
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("sync.wifi_title"), fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                address = editor.Values[0].Trim();
                pairingCode = editor.Values[1].Trim();
            }
            DataStore.SaveLastSyncAddress(address);

            if (pairingCode.Length != 6 || !IsDigits(pairingCode))
            {
                MessageBox.Show(L10n.Get("sync.enter_6digit_code"),
                    L10n.Get("title.sync"));
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
                if (detail != null && (detail.IndexOf("отменена") >= 0 || detail.IndexOf(L10n.Get("sync.cancelled_word")) >= 0))
                {
                    MessageBox.Show(L10n.Get("sync.cancelled"), L10n.Get("title.sync"));
                    return;
                }
                MessageBox.Show((L10n.Get("error.sync_failed_colon") + " ") + detail,
                    L10n.Get("title.sync_error"));
                return;
            }

            currentCar = DataStore.LoadCarNames()[0];
            ShowHome();
            SyncJournal journal = SyncMerge.LastJournal;
            if (journal != null)
                MessageBox.Show(journal.FormatSummary(), L10n.Get("title.sync"));
            else
                MessageBox.Show(L10n.Get("msg.sync_complete"), L10n.Get("title.sync"));
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
                (L10n.Get("sync.on_pda_colon") + "\n") + localRow + ("\n\n" + L10n.Get("sync.on_android_colon") + "\n") + incomingRow +
                ("\n\n" + L10n.Get("sync.yes_android_no_pda")),
                (L10n.Get("sync.conflict_colon") + " ") + title,
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
                    throw new IOException((L10n.Get("sync.no_response_from") + " ") + host + ":" +
                        port.ToString(CultureInfo.InvariantCulture) +
                        (L10n.Get("sync.android_open_wifi_hint") + " ") +
                        L10n.Get("sync.pda_phone_same_wifi"));
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
            return io != null && io.Message != null && io.Message.StartsWith(L10n.Get("sync.no_response_from"));
        }

        private static string ExplainSyncFailure(Exception ex, string address)
        {
            if (ex == null)
                return L10n.Get("error.unknown");

            SocketException socket = FindSocketException(ex);
            if (socket != null)
            {
                int code = SocketCode(socket);
                if (code == 10060 || code == 10065 || code == 10051 || code == 10050 || code == 10049)
                    return (L10n.Get("sync.pda_could_not_reach") + " ") + address +
                        (L10n.Get("sync.connect_pda_wifi_hint") + " ") +
                        L10n.Get("sync.and_enter_android_ip");
                if (code == 10061)
                    return (L10n.Get("error.phone_refused_reopen_wifi") + " ") +
                        L10n.Get("sync.keep_window_open");
                if (code == 11001 || code == 11004)
                    return L10n.Get("error.address_open") + address + L10n.Get("error.ip_not_found_enter_android");
                return (L10n.Get("sync.connecting_to") + " ") + address + (" " + L10n.Get("error.failed_code") + " ") +
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
                text += (L10n.Get("error.comma_code") + " ") + SocketCode(socket).ToString(CultureInfo.InvariantCulture);
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

        private void ShowTripReportFromMenu(object sender, EventArgs e)
        {
            List<string[]> trips = DataStore.LoadTrips();
            decimal personal = 0;
            decimal work = 0;
            int count = 0;
            for (int i = 0; i < trips.Count; i++)
            {
                string[] content = RecordIds.StripId(trips[i]);
                if (content.Length < 7 ||
                    String.Compare(content[1] ?? "", currentCar, true,
                        CultureInfo.InvariantCulture) != 0)
                    continue;
                decimal startKm;
                decimal endKm;
                if (!DataStore.TryNumber(content[2], out startKm) ||
                    !DataStore.TryNumber(content[3], out endKm) || endKm < startKm)
                    continue;
                decimal distance = endKm - startKm;
                if (String.Compare(content[4] ?? "", "Работа", true,
                    CultureInfo.InvariantCulture) == 0)
                    work += distance;
                else
                    personal += distance;
                count++;
            }
            MessageBox.Show(
                (L10n.Get("trips.report_title_dot") + " ") + currentCar +
                ("\r\n" + L10n.Get("label.quantity_colon") + " ") + count +
                ("\r\n" + L10n.Get("report.personal_colon") + " ") + personal.ToString("0.##", CultureInfo.InvariantCulture) + (" " + L10n.Get("unit.km")) +
                ("\r\n" + L10n.Get("report.work_colon") + " ") + work.ToString("0.##", CultureInfo.InvariantCulture) + (" " + L10n.Get("unit.km")) +
                ("\r\n" + L10n.Get("report.total_colon") + " ") + (personal + work).ToString("0.##", CultureInfo.InvariantCulture) + (" " + L10n.Get("unit.km")),
                L10n.Get("label.trips"));
        }

        private void AddTripFromMenu(object sender, EventArgs e)
        {
            List<string> cars = DataStore.LoadCarNames();
            if (cars.Count == 0)
            {
                MessageBox.Show(L10n.Get("error.no_cars"), L10n.Get("button.trip"));
                return;
            }
            string car = ToSettings.ResolvePreferredCar(cars, currentCar);
            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("label.date_fmt_lower"), DateTime.Now.ToString("yyyy-MM-dd"), "date", true),
                new EntryField(L10n.Get("label.vehicle"), car, cars.ToArray(), true),
                new EntryField(L10n.Get("label.odometer_start_km"), "", "number", true),
                new EntryField(L10n.Get("label.odometer_end_km"), "", "number", true),
                new EntryField(L10n.Get("label.purpose_alt"), "Личный",
                    new string[] { "Личный", "Работа" }, false),
                new EntryField(L10n.Get("label.driver"), "", "text", false),
                new EntryField(L10n.Get("label.comment"), "", "text", false)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.new_trip"), fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] values = editor.Values;
                decimal startKm;
                decimal endKm;
                if (values == null || values.Length < 7 ||
                    !DataStore.TryNumber(values[2], out startKm) || startKm < 0 ||
                    !DataStore.TryNumber(values[3], out endKm) || endKm < startKm)
                {
                    MessageBox.Show(
                        L10n.Get("error.trip_odometer_range"),
                        L10n.Get("button.trip"));
                    return;
                }
                DataStore.AddTrip(values);
                currentCar = values[1];
                MessageBox.Show((L10n.Get("msg.trip_saved") + " ") +
                    (endKm - startKm).ToString("0.##", CultureInfo.InvariantCulture) + (" " + L10n.Get("unit.km")),
                    L10n.Get("button.trip"));
            }
        }

        private void QuickFuelFromMenu(object sender, EventArgs e)
        {
            QuickFuel();
        }

        private void QuickFuel()
        {
            List<string> carList = DataStore.LoadCarNames();
            if (carList.Count == 0)
            {
                MessageBox.Show(L10n.Get("error.no_cars"), L10n.Get("button.quick_fuel"));
                return;
            }
            string[] cars = carList.ToArray();
            string car = ToSettings.ResolvePreferredCar(carList, currentCar);
            string lastMileage = LastCarMileage(car);
            string lastPrice = LastFuelPricePerLiter(car);
            string fullPrefill = LastFuelFullTank(car) ? "1" : "0";
            // Compact: car, km, liters, cost, price (auto), full tank — date = today on save.
            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("label.vehicle"), car, cars, true),
                new EntryField(lastMileage.Length > 0
                    ? L10n.Get("label.odometer_last_short")
                    : L10n.Get("label.odometer_km"), lastMileage, "number", true),
                new EntryField(L10n.Get("label.liters"), "", "number", true),
                new EntryField((L10n.Get("label.cost_comma") + " ") + Cur(), "", "number", true),
                new EntryField((L10n.Get("label.price_per_l_comma") + " ") + Cur(), lastPrice, "number", false),
                new EntryField(L10n.Get("label.full_tank"), fullPrefill, "check", false)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("button.quick_fuel"), fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] q = editor.Values;
                string[] values = new string[]
                {
                    DateTime.Now.ToString("yyyy-MM-dd"),
                    q.Length > 0 ? q[0] : car,
                    q.Length > 1 ? q[1] : "",
                    q.Length > 2 ? q[2] : "",
                    q.Length > 3 ? q[3] : "",
                    q.Length > 4 ? q[4] : "",
                    "",
                    q.Length > 5 ? q[5] : "1",
                    ""
                };
                decimal km;
                decimal liters;
                decimal cost;
                decimal price;
                if (!DataStore.TryNumber(values[2], out km) || km < 0 ||
                    !DataStore.TryNumber(values[3], out liters) || liters <= 0 ||
                    !DataStore.TryNumber(values[4], out cost) || cost <= 0 ||
                    (values[5].Length > 0 &&
                        (!DataStore.TryNumber(values[5], out price) || price <= 0)))
                {
                    MessageBox.Show(
                        L10n.Get("error.check_fuel_values"),
                        L10n.Get("button.quick_fuel"));
                    return;
                }
                values = NormalizeFuelValues(values);
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

        private static bool LastFuelFullTank(string car)
        {
            List<string[]> fuels = DataStore.LoadFuel();
            string bestDate = "";
            int bestIndex = -1;
            bool full = true;
            for (int i = 0; i < fuels.Count; i++)
            {
                if (fuels[i].Length < 2)
                    continue;
                if ((fuels[i][1] ?? "") != (car ?? ""))
                    continue;
                string date = fuels[i][0] == null ? "" : fuels[i][0];
                if (bestIndex < 0 ||
                    String.Compare(date, bestDate, false, CultureInfo.InvariantCulture) > 0 ||
                    (date == bestDate && i > bestIndex))
                {
                    bestDate = date;
                    bestIndex = i;
                    full = IsFullTank(fuels[i]);
                }
            }
            return full;
        }

        private void AddFuel()
        {
            string[] cars = DataStore.LoadCarNames().ToArray();
            string lastMileage = LastCarMileage(currentCar);
            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("label.date_fmt_lower"), DateTime.Now.ToString("yyyy-MM-dd"), "date", true),
                new EntryField(L10n.Get("label.vehicle"), currentCar, cars, true),
                new EntryField(lastMileage.Length > 0
                    ? L10n.Get("label.odometer_last_record")
                    : L10n.Get("label.odometer_km"), lastMileage, "number", true),
                new EntryField(L10n.Get("label.liters"), "", "number", true),
                new EntryField((L10n.Get("label.cost_comma") + " ") + Cur(), "", "number", true),
                new EntryField((L10n.Get("label.price_per_liter_comma") + " ") + Cur(), LastFuelPricePerLiter(currentCar), "number", false),
                new EntryField(L10n.Get("label.comment"), "", "text", false),
                new EntryField(L10n.Get("label.full_tank"), "1", "check", false),
                new EntryField(L10n.Get("label.purpose_hint"), "", PurposeOptions(""), false),
                EntryField.Suggest(L10n.Get("label.station_brand"), "", StationOptions(""), false)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.new_fuel"), fields))
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
            return MessageBox.Show(warning, L10n.Get("label.odometer"), MessageBoxButtons.YesNo,
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
                return L10n.Get("warn.odometer_lower_open") + x + L10n.Get("confirm.save_anyway_suffix");
            if (newKm - lastKnown > 5000m)
                return L10n.Get("warn.odometer_jump_open") + x +
                    L10n.Get("confirm.save_anyway_suffix");
            return null;
        }

        private void AddRepair()
        {
            string[] cars = DataStore.LoadCarNames().ToArray();
            string[] categories = DataStore.LoadRepairCategories();
            if (categories.Length == 0)
            {
                MessageBox.Show(L10n.Get("error.add_repair_category_first"), L10n.Get("tab.categories"));
                return;
            }
            string lastMileage = LastCarMileage(currentCar);
            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("label.date_fmt_lower"), DateTime.Now.ToString("yyyy-MM-dd"), "date", true),
                new EntryField(L10n.Get("label.vehicle"), currentCar, cars, true),
                new EntryField(lastMileage.Length > 0
                    ? L10n.Get("label.odometer_last_record")
                    : L10n.Get("label.odometer_km"), lastMileage, "number", false),
                new EntryField(L10n.Get("label.category"), categories[0], categories, true),
                new EntryField(L10n.Get("label.product_part"), "", "text", true),
                new EntryField(L10n.Get("label.item_name"), "", "text", false),
                new EntryField(L10n.Get("label.quantity"), "1", "number", true),
                new EntryField((L10n.Get("label.amount_comma") + " ") + Cur(), "", "number", true),
                new EntryField(L10n.Get("label.comment"), "", "text", false)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.new_repair"), fields))
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
            fields[0] = new EntryField(L10n.Get("label.month_fmt"), DateTime.Now.ToString("yyyy-MM"), "month", true);
            for (int i = 0; i < DataStore.UtilityLabels.Length; i++)
                fields[i + 1] = new EntryField(DataStore.GetUtilityLabel(i) + ", " + Cur(), "", "number", false);

            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("label.utilities_full"), fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] values = editor.Values;
                if (DataStore.ContainsUtilityMonth(values[0]))
                {
                    MessageBox.Show(L10n.Get("error.this_month_exists"), L10n.Get("label.utilities_full"));
                    return;
                }
                bool hasAmount = false;
                for (int i = 1; i < values.Length; i++)
                    if (values[i] != "")
                        hasAmount = true;
                if (!hasAmount)
                {
                    MessageBox.Show(L10n.Get("error.enter_at_least_one_amount"), L10n.Get("title.accounting"));
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
            homeButtonsPanel = null;
            screenHeading = null;
            summaryBox = null;
            homeButtons = null;
            Controls.Clear();
            Text = L10n.Get("tab.vehicles");
            screenHeading = new Label();
            screenHeading.Text = (L10n.Get("label.current_car_colon") + " ") + currentCar;
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
                MessageBox.Show((L10n.Get("error.read_cars_failed") + " ") + ex.Message, L10n.Get("title.error"));
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
            MenuItem actions = CreateMenuItem(L10n.Get("menu.actions"));
            actions.MenuItems.Add(CreateMenuItem(L10n.Get("button.choose"), new EventHandler(SelectCar)));
            actions.MenuItems.Add(CreateMenuItem(L10n.Get("button.add"), new EventHandler(AddCar)));
            actions.MenuItems.Add(CreateMenuItem(L10n.Get("title.properties"), new EventHandler(EditCarPassportFromList)));
            actions.MenuItems.Add(CreateMenuItem(L10n.Get("label.favorite"), new EventHandler(SetFavoriteCarFromMenu)));
            actions.MenuItems.Add(CreateMenuItem(L10n.Get("button.rename"), new EventHandler(RenameCar)));
            actions.MenuItems.Add(CreateMenuItem(L10n.Get("button.delete"), new EventHandler(DeleteCar)));
            menu.MenuItems.Add(actions);
            menu.MenuItems.Add(CreateMenuItem(L10n.Get("button.back"), new EventHandler(ShowHomeFromMenu)));
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
                MessageBox.Show(L10n.Get("error.choose_car"), L10n.Get("title.accounting"));
                return;
            }
            EditCarPassportFor(recordList.SelectedItem.ToString());
        }

        private void EditCarPassportFor(string carName)
        {
            CarPassport meta = CarsMeta.Get(carName);
            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("label.year"), meta.Year, "text", false),
                new EntryField(L10n.Get("label.plate"), meta.Plate, "text", false),
                new EntryField("VIN", meta.Vin, "text", false),
                new EntryField(L10n.Get("label.tank_capacity_l"), meta.TankLiters, "number", false),
                new EntryField(L10n.Get("label.note"), meta.Note, "text", false)
            };
            using (EntryEditorForm editor = new EntryEditorForm((L10n.Get("title.properties_dot") + " ") + carName, fields))
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
            homeButtonsPanel = null;
                screenHeading = null;
                recordList = null;
                homeButtons = null;
                Controls.Clear();
                Text = L10n.Get("label.timeline");
                screenHeading = new Label();
                screenHeading.Text = (L10n.Get("title.timeline_dot") + " ") + currentCar + CarsMeta.HeaderSuffix(currentCar);
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
                menu.MenuItems.Add(CreateMenuItem(L10n.Get("button.back"), new EventHandler(ShowHomeFromMenu)));
                Menu = menu;
                LayoutScreen();
            }
            catch (Exception ex)
            {
                MessageBox.Show((L10n.Get("error.build_timeline_failed") + " ") + ex.Message, L10n.Get("title.error"));
            }
        }

        private void SelectCar(object sender, EventArgs e)
        {
            if (recordList.SelectedIndex < 0)
            {
                MessageBox.Show(L10n.Get("error.choose_car"), L10n.Get("title.accounting"));
                return;
            }
            currentCar = recordList.SelectedItem.ToString();
            ShowCars(sender, e);
        }

        private void AddCar(object sender, EventArgs e)
        {
            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("label.car_name"), "", "text", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.add_car"), fields))
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
                MessageBox.Show(L10n.Get("error.choose_car"), L10n.Get("title.accounting"));
                return;
            }
            string oldName = recordList.SelectedItem.ToString();
            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("label.new_name"), oldName, "text", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.rename_car"), fields))
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
                MessageBox.Show(L10n.Get("error.choose_car"), L10n.Get("title.accounting"));
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
            homeButtonsPanel = null;
            screenHeading = null;
            recordList = null;
            homeButtons = null;
            Controls.Clear();
            Text = L10n.Get("tab.summary");
            screenHeading = new Label();
            screenHeading.Text = (L10n.Get("title.summary_dot") + " ") + currentCar + CarsMeta.HeaderSuffix(currentCar);
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
                MessageBox.Show((L10n.Get("error.build_summary_failed") + " ") + ex.Message, L10n.Get("title.error"));
                ShowHome();
                return;
            }
            Controls.Add(summaryBox);

            MainMenu menu = new MainMenu();
            MenuItem actions = CreateMenuItem(L10n.Get("menu.actions"));
            actions.MenuItems.Add(CreateMenuItem(L10n.Get("button.compare_periods"), new EventHandler(ShowPeriodCompareFromMenu)));
            actions.MenuItems.Add(CreateMenuItem(L10n.Get("title.period_report"), new EventHandler(ExportPeriodReportFromMenu)));
            actions.MenuItems.Add(CreateMenuItem(L10n.Get("button.year_report"), new EventHandler(ExportYearReportFromMenu)));
            menu.MenuItems.Add(actions);
            menu.MenuItems.Add(CreateMenuItem(L10n.Get("button.back"), new EventHandler(ShowHomeFromMenu)));
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
                    PeriodCompare.FormatRangeLabel(aFrom, aTo) + (" " + L10n.Get("compare.this_month_paren")),
                    PeriodCompare.FormatRangeLabel(bFrom, bTo) + (" " + L10n.Get("compare.prev_paren")),
                    Cur());
                MessageBox.Show(text, L10n.Get("title.period_compare"));
            }
            catch (Exception ex)
            {
                MessageBox.Show((L10n.Get("error.compare_periods_failed") + " ") + ex.Message, L10n.Get("title.error"));
            }
        }

        private void ExportPeriodReportFromMenu(object sender, EventArgs e)
        {
            try
            {
                string thisFrom, thisTo, prevFrom, prevTo;
                PeriodCompare.ThisVsPreviousMonth(out thisFrom, out thisTo, out prevFrom, out prevTo);
                DialogResult choice = MessageBox.Show(
                    L10n.Get("prompt.this_or_last_month_yes_no"),
                    (L10n.Get("report.title_dot") + " ") + currentCar,
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button1);
                if (choice == DialogResult.Cancel)
                    return;
                string from = choice == DialogResult.Yes ? thisFrom : prevFrom;
                string to = choice == DialogResult.Yes ? thisTo : prevTo;
                SavePeriodReport(from, to);
            }
            catch (Exception ex)
            {
                MessageBox.Show((L10n.Get("error.save_report_failed") + " ") + ex.Message, L10n.Get("title.error"));
            }
        }

        private void ExportYearReportFromMenu(object sender, EventArgs e)
        {
            try
            {
                string year = DateTime.Today.Year.ToString(CultureInfo.InvariantCulture);
                SavePeriodReport(year + "-01-01", year + "-12-31");
            }
            catch (Exception ex)
            {
                MessageBox.Show((L10n.Get("error.save_report_failed") + " ") + ex.Message, L10n.Get("title.error"));
            }
        }

        private void SavePeriodReport(string from, string to)
        {
            {
                List<string[]> fuels = DataStore.LoadFuel();
                List<string[]> repairs = DataStore.LoadRepairs();
                List<string[]> expenses = DataStore.LoadExpenses();
                List<string[]> utilities = DataStore.LoadUtilities();
                PeriodCompare.Totals totals = PeriodCompare.Build(
                    currentCar, fuels, repairs, expenses, utilities, from, to);
                PeriodCompare.AddTripKm(totals, currentCar, DataStore.LoadTrips(), from, to);
                PeriodCompare.ApplyBudget(totals, ToSettings.GetBudgetMonth(), from, to);
                string text = PeriodReport.FormatText(currentCar, from, to, totals, Cur());

                string fileName = "Report_" + from + "_" + to + ".txt";
                string folder = @"\My Documents\Alfa156";
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, fileName);
                using (StreamWriter writer = new StreamWriter(path, false))
                    writer.Write(text);
                string htmlPath = Path.ChangeExtension(path, ".html");
                using (StreamWriter writer = new StreamWriter(htmlPath, false, Encoding.UTF8))
                    writer.Write(PeriodReport.FormatHtml(currentCar, from, to, totals, Cur()));

                MessageBox.Show(text + ("\r\n\r\n" + L10n.Get("msg.saved_colon") + "\r\n") + path + "\r\n" + htmlPath, L10n.Get("title.period_report"));
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
                if (homeButtonsPanel != null)
                {
                    homeButtonsPanel.Location = new Point(4, 92);
                    homeButtonsPanel.Size = new Size(width - 8, height - 96);
                }
                if (homeButtons != null && homeButtons.Count > 0)
                {
                    int gap = 6;
                    int availableHeight = height - 96;
                    int totalNeededHeight = homeButtons.Count * 48 + gap * (homeButtons.Count - 1);
                    int buttonHeight = 48;

                    if (totalNeededHeight <= availableHeight)
                    {
                        buttonHeight = (availableHeight - gap * (homeButtons.Count - 1)) / homeButtons.Count;
                        if (buttonHeight > 60)
                            buttonHeight = 60;
                    }
                    else
                    {
                        buttonHeight = 48;
                    }

                    int buttonY = 4;
                    for (int i = 0; i < homeButtons.Count; i++)
                    {
                        homeButtons[i].Location = new Point(4, buttonY);
                        homeButtons[i].Size = new Size(width - 20, buttonHeight);
                        buttonY += buttonHeight + gap;
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
            Dictionary<string, decimal[]> stationStats = new Dictionary<string, decimal[]>();
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
                    string station = GetStation(row);
                    if (station.Length > 0)
                    {
                        decimal price = DataStore.NumberOrZero(FieldAt(row, 5));
                        if (price <= 0)
                        {
                            decimal liters = DataStore.NumberOrZero(row[3]);
                            if (liters > 0)
                                price = amount / liters;
                        }
                        string key = station;
                        decimal[] bucket = null;
                        if (stationStats.ContainsKey(key))
                            bucket = stationStats[key];
                        else
                        {
                            // Case-insensitive match for CF Dictionary
                            System.Collections.IDictionaryEnumerator en = stationStats.GetEnumerator();
                            while (en.MoveNext())
                            {
                                string existing = en.Key as string;
                                if (existing != null &&
                                    String.Compare(existing, station, StringComparison.OrdinalIgnoreCase) == 0)
                                {
                                    key = existing;
                                    bucket = en.Value as decimal[];
                                    break;
                                }
                            }
                            if (bucket == null)
                            {
                                bucket = new decimal[2];
                                stationStats[station] = bucket;
                                key = station;
                            }
                        }
                        bucket[0] += 1;
                        bucket[1] += price;
                        stationStats[key] = bucket;
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
                    result += (L10n.Get("label.tank_short") + " ") + passport.TankLiters + (" " + L10n.Get("unit.liters_short"));
                }
                if (passport.Note.Length > 0)
                    result += "\r\n" + passport.Note;
            }
            result += ("\r\n\r\n" + L10n.Get("summary.fuel_section_colon") + " ") + Money(fuelTotal) +
                " " + Cur() + ("\r\n" + L10n.Get("summary.repairs_section_colon") + " ") + Money(repairTotal) +
                " " + Cur() + ("\r\n" + L10n.Get("summary.expenses_section_colon") + " ") + Money(expenseTotal) + " " + Cur() + "\r\n";
            if (anyPurpose)
            {
                result += ("\r\n" + L10n.Get("report.fuel_by_purpose_personal") + " ") + Money(purposePersonal) +
                    " " + Cur() + ("\r\n" + L10n.Get("purpose.work_colon") + " ") + Money(purposeWork) + " " + Cur();
                if (purposeOther > 0)
                    result += ("\r\n" + L10n.Get("report.other_colon") + " ") + Money(purposeOther) + " " + Cur();
                result += "\r\n";
            }
            if (stationStats.Count > 0)
            {
                result += ("\r\n" + L10n.Get("label.station_colon"));
                System.Collections.ArrayList stationNames = new System.Collections.ArrayList();
                foreach (string name in stationStats.Keys)
                    stationNames.Add(name);
                stationNames.Sort(new CaseInsensitiveComparer());
                for (int s = 0; s < stationNames.Count; s++)
                {
                    string name = (string)stationNames[s];
                    decimal[] bucket = stationStats[name];
                    int count = (int)bucket[0];
                    decimal avg = count > 0 ? bucket[1] / count : 0;
                    result += "\r\n" + name + ": " + count.ToString(CultureInfo.InvariantCulture) +
                        (" " + L10n.Get("summary.dot_avg") + " ") + Money(avg) + " " + Cur() + L10n.Get("unit.per_liter");
                }
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
            result += (L10n.Get("summary.counted_cost_per_km") + " ");
            if (minimumMileage != Decimal.MaxValue && maximumMileage > minimumMileage)
                result += Money((fuelTotal + repairTotal + expenseTotal) /
                    (maximumMileage - minimumMileage)) + " " + Cur() + (L10n.Get("unit.per_km") + "\r\n");
            else
                result += (L10n.Get("forecast.insufficient_mileage") + "\r\n");

            result += ("\r\n" + L10n.Get("report.repairs_by_cat_colon") + "\r\n");
            foreach (KeyValuePair<string, decimal> pair in byCategory)
                result += pair.Key + ": " + Money(pair.Value) + " " + Cur() + "\r\n";
            result += ("\r\n" + L10n.Get("summary.utilities_all_time") + "\r\n") + Money(utilityTotal) +
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
            result += ("\r\n\r\n" + L10n.Get("summary.year_totals") + " ") + yearPrefix + ":\r\n" +
                (L10n.Get("label.fuel_colon") + " ") + Money(yearFuel) + " " + Cur() + "\r\n" +
                (L10n.Get("label.repairs_colon") + " ") + Money(yearRepair) + " " + Cur() + "\r\n" +
                (L10n.Get("label.expenses_colon") + " ") + Money(yearExpense) + " " + Cur() + "\r\n" +
                (L10n.Get("label.utilities_colon") + " ") + Money(yearUtility) + " " + Cur() + "\r\n" +
                (L10n.Get("report.total_colon") + " ") + Money(yearFuel + yearRepair + yearExpense + yearUtility) +
                " " + Cur() + ("\r\n\r\n" + L10n.Get("summary.monthly_expenses_12") + "\r\n");
            List<string> months = new List<string>(monthlyTotals.Keys);
            months.Sort(StringComparer.Ordinal);
            int firstMonth = Math.Max(0, months.Count - 12);
            for (int i = firstMonth; i < months.Count; i++)
            {
                decimal[] totals = monthlyTotals[months[i]];
                result += months[i] + (L10n.Get("summary.colon_fuel") + " ") + Money(totals[0]) +
                    (L10n.Get("summary.comma_repairs") + " ") + Money(totals[1]) + (L10n.Get("summary.comma_utilities") + " ") +
                    Money(totals[2]) + (L10n.Get("summary.comma_expenses") + " ") +
                    Money(totals.Length > 3 ? totals[3] : 0m) + " " + Cur() + "\r\n";
            }
            result += "\r\n" + CarCompare.FormatBlock(
                CarCompare.Build(cars, fuels, repairs, expenses), Cur()) + "\r\n";
            result += ("\r\n" + L10n.Get("forecast.current_odometer_colon") + " ") + Money(currentMileage) + (" " + L10n.Get("unit.km") + "\r\n");
            ToIntervals intervals = ToSettings.Load();
            result += ServiceStatus(L10n.Get("service.oil_change"), currentMileage, lastOilMileage,
                lastOilDate, intervals.OilInterval, intervals.OilWarn) + "\r\n";
            result += ServiceStatus(L10n.Get("service.timing_belt"), currentMileage, lastTimingMileage,
                lastTimingDate, intervals.BeltInterval, intervals.BeltWarn);
            result += ForecastInsights.FormatBlock(
                currentCar, fuels, repairs, currentMileage,
                lastOilMileage, lastTimingMileage,
                intervals.OilInterval, intervals.BeltInterval,
                DateTime.Today, "\r\n");
            result += DateReminders.FormatUpcomingBlock(
                ToSettings.GetReminderWarnDays(), DateTime.Today);
            result += CarNotes.FormatRecentBlock(currentCar, 3);
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
            // date|car|km|liters|cost|price|comment|fullTank|purpose|station  (id added separately)
            string[] row = values;
            if (row.Length > 10)
            {
                row = new string[10];
                for (int i = 0; i < 10; i++)
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
            if (row.Length > 9)
                row[9] = NormalizeStation(row[9]);
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

        private static string GetStation(string[] row)
        {
            string[] content = RecordIds.StripId(row);
            if (content == null || content.Length <= 9)
                return "";
            return NormalizeStation(content[9]);
        }

        private static string NormalizeStation(string value)
        {
            string text = value == null ? "" : value.Trim().Replace("\r", " ").Replace("\n", " ");
            if (text.Length > 48)
                text = text.Substring(0, 48).Trim();
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

        private static string[] StationOptions(string current)
        {
            System.Collections.ArrayList list = new System.Collections.ArrayList();
            List<string[]> fuels = DataStore.LoadFuel();
            for (int i = 0; i < fuels.Count; i++)
            {
                string station = GetStation(fuels[i]);
                if (station.Length == 0)
                    station = NormalizeStation(FieldAt(fuels[i], 6));
                if (station.Length == 0)
                    continue;
                bool found = false;
                for (int j = 0; j < list.Count; j++)
                {
                    if (String.Compare((string)list[j], station, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    list.Add(station);
            }
            list.Sort(new CaseInsensitiveComparer());
            string value = NormalizeStation(current);
            if (value.Length > 0)
            {
                bool found = false;
                for (int j = 0; j < list.Count; j++)
                {
                    if (String.Compare((string)list[j], value, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    list.Insert(0, value);
            }
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
                MessageBox.Show((L10n.Get("error.save_data_failed") + " ") + ex.Message, L10n.Get("title.error"));
                return false;
            }
        }

        private static string Cur()
        {
            return ToSettings.GetCurrency();
        }

        private const string AppVersionBanner = "Версия 1.39.3";

        private void ShowAbout(object sender, EventArgs e)
        {
            string bodyText =
                L10n.Get("about.kpk_full") + "\r\n" +
                L10n.Get("about.platforms_line") + "\r\n\r\n" +
                (L10n.Get("onboarding.station_short") + "\r\n") +
                (L10n.Get("onboarding.forecast_feature") + "\r\n") +
                (L10n.Get("onboarding.passport_timeline_short") + "\r\n") +
                (L10n.Get("onboarding.templates_fav_feature") + "\r\n") +
                (L10n.Get("onboarding.reminders_feature") + "\r\n") +
                (L10n.Get("onboarding.notes_feature") + "\r\n") +
                (L10n.Get("onboarding.period_compare_feature") + "\r\n") +
                (L10n.Get("onboarding.purpose_year_feature") + "\r\n") +
                (L10n.Get("onboarding.export_dups_search") + "\r\n") +
                L10n.Get("onboarding.full_tank_feature") + "\r\n\r\n" +
                L10n.Get("about.website_label") + "\r\nhttps://github.com/Dolfst/alfa\r\n\r\n" +
                L10n.Get("about.support_hint") + "\r\n" +
                L10n.Get("about.support_url_note");

            using (Form aboutForm = new Form())
            {
                aboutForm.Text = L10n.Get("title.about");
                aboutForm.WindowState = FormWindowState.Maximized;
                aboutForm.MinimizeBox = false;

                TextBox infoBox = new TextBox();
                infoBox.Multiline = true;
                infoBox.ReadOnly = true;
                infoBox.ScrollBars = ScrollBars.Vertical;
                infoBox.Location = new Point(4, 4);
                infoBox.Text = bodyText;
                infoBox.SelectionStart = 0;
                infoBox.SelectionLength = 0;
                aboutForm.Controls.Add(infoBox);

                MainMenu menu = new MainMenu();
                menu.MenuItems.Add(CreateMenuItem(L10n.Get("button.support_project"), new EventHandler(delegate
                {
                    try
                    {
                        System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                        startInfo.FileName = "https://github.com/Dolfst/alfa#support";
                        System.Diagnostics.Process.Start(startInfo);
                    }
                    catch
                    {
                        MessageBox.Show(L10n.Get("about.support_url_note"), L10n.Get("button.support_project"));
                    }
                })));
                menu.MenuItems.Add(CreateMenuItem(L10n.Get("button.close"), new EventHandler(delegate
                {
                    aboutForm.Close();
                })));
                aboutForm.Menu = menu;

                aboutForm.Resize += new EventHandler(delegate
                {
                    infoBox.Size = new Size(aboutForm.ClientSize.Width - 8, aboutForm.ClientSize.Height - 8);
                });
                infoBox.Size = new Size(aboutForm.ClientSize.Width - 8, aboutForm.ClientSize.Height - 8);
                aboutForm.ShowDialog();
            }
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
                MessageBox.Show((L10n.Get("msg.summary_saved_colon") + "\r\n") + path, L10n.Get("title.export_summary"));
            }
            catch (Exception ex)
            {
                MessageBox.Show((L10n.Get("error.save_summary_failed") + " ") + ex.Message, L10n.Get("title.error"));
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
                        text + ("\r\n\r\n" + L10n.Get("confirm.quick_fix_categories")),
                        L10n.Get("button.data_integrity"), MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                    if (answer == DialogResult.Yes)
                    {
                        int n = DataIntegrity.ApplySimpleFixes(report);
                        MessageBox.Show(n > 0
                            ? (L10n.Get("msg.categories_added") + " ") + n + "."
                            : L10n.Get("integrity.nothing_to_fix"),
                            L10n.Get("button.data_integrity"));
                    }
                }
                else
                {
                    MessageBox.Show(text, L10n.Get("button.data_integrity"));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show((L10n.Get("error.integrity_check_failed") + " ") + ex.Message, L10n.Get("button.data_integrity"));
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
                MessageBox.Show((L10n.Get("error.find_duplicates_failed") + " ") + ex.Message, L10n.Get("title.error"));
                return;
            }
            if (groups.Count == 0)
            {
                MessageBox.Show(L10n.Get("msg.no_duplicates"), L10n.Get("button.duplicates"));
                return;
            }

            using (Form dialog = new Form())
            {
                dialog.Text = L10n.Get("button.duplicates");
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
                MenuItem actions = CreateMenuItem(L10n.Get("menu.actions"));
                actions.MenuItems.Add(CreateMenuItem(L10n.Get("button.delete_extra_keep_first"),
                    new EventHandler(delegate
                    {
                        int selected = list.SelectedIndex;
                        if (selected < 0 || selected >= items.Count || items[selected] == null)
                        {
                            MessageBox.Show(L10n.Get("error.select_record_in_dup_group"), L10n.Get("button.duplicates"));
                            return;
                        }
                        DuplicateFinder.Group group = (DuplicateFinder.Group)items[selected];
                        if (MessageBox.Show(
                            (L10n.Get("button.delete") + " ") + (group.Indexes.Count - 1).ToString() +
                            (" " + L10n.Get("dup.confirm_delete_extra_suffix")),
                            L10n.Get("button.duplicates"), MessageBoxButtons.YesNo,
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
                        MessageBox.Show(L10n.Get("msg.extra_records_deleted"), L10n.Get("button.duplicates"));
                    })));
                menu.MenuItems.Add(actions);
                menu.MenuItems.Add(CreateMenuItem(L10n.Get("button.close"), new EventHandler(delegate
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
                searchForm.Text = L10n.Get("title.search");
                searchForm.WindowState = FormWindowState.Maximized;
                searchForm.MinimizeBox = false;

                Label hint = new Label();
                hint.Text = L10n.Get("label.text_search_colon");
                hint.Location = new Point(8, 8);
                hint.Size = new Size(220, 20);
                searchForm.Controls.Add(hint);

                TextBox queryBox = new TextBox();
                queryBox.Location = new Point(8, 30);
                queryBox.Size = new Size(224, 22);
                searchForm.Controls.Add(queryBox);

                Button findButton = new Button();
                findButton.Text = L10n.Get("button.search");
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
                            return FieldAt(row, 6) + " " + GetPurpose(row) + " " +
                                GetStation(row) + " " + FieldAt(row, 1);
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
                                sb.Append(' ').Append(DataStore.GetUtilityLabel(i)).Append(' ')
                                    .Append(row[i + 1]);
                            return sb.ToString();
                        });
                    if (results.Items.Count == 0)
                        results.Items.Add(L10n.Get("search.nothing_found"));
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
                closeButton.Text = L10n.Get("button.close");
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
                MessageBox.Show(L10n.Get("error.add_expense_category_first"), L10n.Get("tab.categories"));
                return;
            }
            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("label.date_fmt_lower"), DateTime.Now.ToString("yyyy-MM-dd"), "date", true),
                new EntryField(L10n.Get("label.vehicle"), currentCar, cars, true),
                new EntryField(L10n.Get("label.category"), categories[0], categories, true),
                new EntryField(L10n.Get("label.name"), "", "text", true),
                new EntryField((L10n.Get("label.amount_comma") + " ") + Cur(), "", "number", true),
                new EntryField(L10n.Get("label.comment"), "", "text", false)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("label.expense_entry"), fields))
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
            EntryField[] fields = new EntryField[] { new EntryField(L10n.Get("label.name"), "", "text", true) };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.new_expense_category"), fields))
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
                MessageBox.Show(L10n.Get("error.no_categories_to_rename"), L10n.Get("tab.categories"));
                return;
            }
            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("label.category"), categories[0], categories, true),
                new EntryField(L10n.Get("label.new_name"), "", "text", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.rename_expense_category"), fields))
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
                MessageBox.Show(L10n.Get("error.no_categories_to_delete"), L10n.Get("tab.categories"));
                return;
            }
            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("label.category"), categories[0], categories, true)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.delete_expense_category"), fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                if (MessageBox.Show(
                    L10n.Get("confirm.delete_category_detail"),
                    L10n.Get("tab.categories"), MessageBoxButtons.YesNo, MessageBoxIcon.Question,
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
                new EntryField(L10n.Get("label.oil_interval_km"), values.OilInterval.ToString(CultureInfo.InvariantCulture), "number", true),
                new EntryField(L10n.Get("label.oil_warn_km"), values.OilWarn.ToString(CultureInfo.InvariantCulture), "number", true),
                new EntryField(L10n.Get("label.belt_interval_km"), values.BeltInterval.ToString(CultureInfo.InvariantCulture), "number", true),
                new EntryField(L10n.Get("label.belt_warn_km"), values.BeltWarn.ToString(CultureInfo.InvariantCulture), "number", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("label.service_intervals"), fields))
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
                    MessageBox.Show(L10n.Get("error.enter_positive_numbers"), L10n.Get("label.service_intervals"));
                    return;
                }
                values.OilInterval = oilInterval;
                values.OilWarn = oilWarn;
                values.BeltInterval = beltInterval;
                values.BeltWarn = beltWarn;
                if (!TrySave(delegate { ToSettings.Save(values); }))
                    return;
                MessageBox.Show(L10n.Get("msg.intervals_saved"), L10n.Get("label.service_intervals"));
            }
        }

        private void EditCurrency(object sender, EventArgs e)
        {
            string current = ToSettings.GetCurrency();
            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("label.currency_examples"), current, "text", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("label.currency"), fields))
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
                    MessageBox.Show(ex.Message, L10n.Get("label.currency"));
                    return;
                }
                MessageBox.Show((L10n.Get("msg.currency_colon") + " ") + ToSettings.GetCurrency() +
                    L10n.Get("msg.currency_no_recalc"), L10n.Get("label.currency"));
                if (summaryBox != null)
                    summaryBox.Text = BuildSummary();
            }
        }

        private void EditLanguage(object sender, EventArgs e)
        {
            string current = ToSettings.GetLanguage();
            string[] languages = L10n.GetAvailableLanguages();
            List<string> labels = new List<string>();
            string currentLabel = "";
            for (int i = 0; i < languages.Length; i++)
            {
                string item = L10n.GetLanguageDisplayName(languages[i]) + " (" + languages[i] + ")";
                labels.Add(item);
                if (languages[i] == current)
                    currentLabel = item;
            }
            if (currentLabel.Length == 0 && labels.Count > 0)
                currentLabel = labels[0];

            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("label.language_colon"), currentLabel, labels.ToArray(), true)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.language_selection"), fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                int selectedIndex = labels.IndexOf(editor.Values[0]);
                if (selectedIndex < 0) selectedIndex = 0;
                string selected = languages[selectedIndex];
                try
                {
                    if (!TrySave(delegate { ToSettings.SetLanguage(selected); }))
                        return;
                    LocalizationManager.SetLanguage(selected);
                    ShowHome();
                    MessageBox.Show(L10n.Get("msg.language_changed_to") + " " + L10n.GetLanguageDisplayName(selected), L10n.Get("label.language"));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, L10n.Get("label.language"));
                }
            }
        }

        private string FormatBudgetBlock(string monthKey, decimal spent, decimal utilitiesMonth)
        {
            decimal budget = ToSettings.GetBudgetMonth();
            if (budget <= 0)
                return "";
            decimal warnPct = ToSettings.GetBudgetWarnPct();
            decimal pct = budget > 0 ? (spent * 100m) / budget : 0;
            string block = ("\r\n\r\n" + L10n.Get("label.monthly_budget") + " ") + monthKey + ":\r\n" +
                (L10n.Get("summary.spent_total_colon") + " ") +
                Money(spent) + " / " + Money(budget) + " " + Cur() +
                " (" + Money(pct) + "%)\r\n" +
                (L10n.Get("summary.utilities_this_month") + " ") + Money(utilitiesMonth) + " " + Cur();
            if (spent >= budget)
                block += ("\r\n" + L10n.Get("summary.budget_exceeded_bang"));
            else if (pct >= warnPct)
                block += ("\r\n" + L10n.Get("summary.budget_warn_gte") + " ") + Money(warnPct) + L10n.Get("summary.pct_of_budget");
            return block;
        }

        private void EditBackupReminder(object sender, EventArgs e)
        {
            int days = ToSettings.GetBackupRemindDays();
            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("label.days_off_default7"),
                    days.ToString(CultureInfo.InvariantCulture), "number", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.backup_reminder"), fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                decimal value;
                if (!DataStore.TryNumber(editor.Values[0], out value) || value < 0 || value > 3650 ||
                    value != Decimal.Truncate(value))
                {
                    MessageBox.Show(L10n.Get("error.interval_0_3650"), L10n.Get("label.reminder"));
                    return;
                }
                try
                {
                    if (!TrySave(delegate { ToSettings.SetBackupRemindDays((int)value); }))
                        return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, L10n.Get("label.reminder"));
                    return;
                }
                if (value <= 0)
                    MessageBox.Show(L10n.Get("msg.backup_reminder_off"), L10n.Get("label.reminder"));
                else
                    MessageBox.Show((L10n.Get("label.backup_remind_if_none") + " ") +
                        ((int)value).ToString(CultureInfo.InvariantCulture) + (" " + L10n.Get("reminder.days_and_longer_dot")),
                        L10n.Get("label.reminder"));
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
                MessageBox.Show(L10n.Get("error.choose_car"), L10n.Get("label.car_short"));
                return;
            }
            if (!TrySave(delegate { ToSettings.SetDefaultCar(name); }))
                return;
            currentCar = name;
            MessageBox.Show((L10n.Get("label.favorite_car_colon") + " ") + name, L10n.Get("label.car_short"));
        }

        private void EditDefaultCar(object sender, EventArgs e)
        {
            List<string> cars = DataStore.LoadCarNames();
            string current = ToSettings.GetDefaultCar();
            string[] options = new string[cars.Count + 1];
            options[0] = L10n.Get("common.not_set");
            for (int i = 0; i < cars.Count; i++)
                options[i + 1] = cars[i];
            string selected = current.Length > 0 ? current : options[0];
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("label.default_car_full"),
                new EntryField[] {
                    new EntryField(L10n.Get("label.vehicle"), selected, options, true)
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
                    ? L10n.Get("msg.default_car_cleared")
                    : (L10n.Get("msg.default_car_set") + " ") + name, L10n.Get("label.default_car_full"));
                if (screenType == "home")
                    ShowHome();
            }
        }

        private void ManageRemindersFromMenu(object sender, EventArgs e)
        {
            DateReminders.EnsureFile();
            MessageBox.Show(DateReminders.FormatAllBlock(DateTime.Today), L10n.Get("label.reminders"));
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("label.reminders"),
                new EntryField[] {
                    new EntryField(L10n.Get("label.action"), L10n.Get("button.add"),
                        new string[] { L10n.Get("button.add"), L10n.Get("button.delete"), L10n.Get("label.warn_term") }, true),
                    new EntryField(L10n.Get("prompt.number_to_delete"), "", "text", false)
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
                        MessageBox.Show(L10n.Get("error.pick_number_from_list"), L10n.Get("label.reminders"));
                        return;
                    }
                    DateReminder item = list[index - 1];
                    if (!TrySave(delegate { DateReminders.DeleteById(item.Id); }))
                        return;
                    MessageBox.Show(L10n.Get("msg.deleted"), L10n.Get("label.reminders"));
                    if (screenType == "summary")
                        summaryBox.Text = BuildSummary();
                    return;
                }
                // Добавить
                string car = currentCar == null ? "" : currentCar;
                string defaultDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd",
                    CultureInfo.InvariantCulture);
                using (EntryEditorForm add = new EntryEditorForm(L10n.Get("title.new_reminder"),
                    new EntryField[] {
                        new EntryField(L10n.Get("label.date"), defaultDate, "date", true),
                        new EntryField(L10n.Get("label.vehicle"), car, "text", true),
                        new EntryField(L10n.Get("label.name"), "", "text", true),
                        new EntryField(L10n.Get("label.type"), L10n.Get("reminder.insurance"),
                            new string[] { L10n.Get("reminder.insurance"), L10n.Get("reminder.inspection"), L10n.Get("reminder.custom") }, true)
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
                    MessageBox.Show(L10n.Get("msg.reminder_saved"), L10n.Get("label.reminders"));
                    if (screenType == "summary")
                        summaryBox.Text = BuildSummary();
                }
            }
        }

        private void ManageNotesFromMenu(object sender, EventArgs e)
        {
            CarNotes.EnsureFile();
            string car = currentCar == null ? "" : currentCar;
            MessageBox.Show(CarNotes.FormatAllBlock(car), L10n.Get("label.notes"));
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("label.notes"),
                new EntryField[] {
                    new EntryField(L10n.Get("label.action"), L10n.Get("button.add"),
                        new string[] { L10n.Get("button.add"), L10n.Get("button.edit"), L10n.Get("button.delete") }, true),
                    new EntryField(L10n.Get("prompt.number_edit_delete"), "", "text", false)
                }))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string action = editor.Values[0];
                List<CarNote> list = CarNotes.ListForCar(car);
                if (action == "Удалить" || action == "Изменить")
                {
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
                        MessageBox.Show(L10n.Get("error.pick_number_from_list"), L10n.Get("label.notes"));
                        return;
                    }
                    CarNote selected = list[index - 1];
                    if (action == "Удалить")
                    {
                        if (!TrySave(delegate { CarNotes.DeleteById(selected.Id); }))
                            return;
                        MessageBox.Show(L10n.Get("msg.deleted"), L10n.Get("label.notes"));
                        if (screenType == "summary" || screenType == "timeline")
                            summaryBox.Text = screenType == "timeline"
                                ? CarTimeline.FormatList(currentCar) : BuildSummary();
                        return;
                    }
                    using (EntryEditorForm edit = new EntryEditorForm(L10n.Get("button.edit_note"),
                        new EntryField[] {
                            new EntryField(L10n.Get("label.date"), selected.Date, "date", true),
                            new EntryField(L10n.Get("label.vehicle"), selected.Car, "text", true),
                            new EntryField(L10n.Get("label.text"), selected.Text, "text", true)
                        }))
                    {
                        if (edit.ShowDialog() != DialogResult.OK)
                            return;
                        selected.Date = edit.Values[0];
                        selected.Car = edit.Values[1];
                        selected.Text = edit.Values[2];
                        if (!TrySave(delegate { CarNotes.Upsert(selected); }))
                            return;
                        MessageBox.Show(L10n.Get("msg.note_saved"), L10n.Get("label.notes"));
                        if (screenType == "summary" || screenType == "timeline")
                            summaryBox.Text = screenType == "timeline"
                                ? CarTimeline.FormatList(currentCar) : BuildSummary();
                        return;
                    }
                }
                string defaultDate = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                using (EntryEditorForm add = new EntryEditorForm(L10n.Get("title.new_note"),
                    new EntryField[] {
                        new EntryField(L10n.Get("label.date"), defaultDate, "date", true),
                        new EntryField(L10n.Get("label.vehicle"), car, "text", true),
                        new EntryField(L10n.Get("label.text"), "", "text", true)
                    }))
                {
                    if (add.ShowDialog() != DialogResult.OK)
                        return;
                    CarNote item = new CarNote();
                    item.Date = add.Values[0];
                    item.Car = add.Values[1];
                    item.Text = add.Values[2];
                    if (!TrySave(delegate { CarNotes.Upsert(item); }))
                        return;
                    MessageBox.Show(L10n.Get("msg.note_saved"), L10n.Get("label.notes"));
                    if (screenType == "summary" || screenType == "timeline")
                        summaryBox.Text = screenType == "timeline"
                            ? CarTimeline.FormatList(currentCar) : BuildSummary();
                }
            }
        }

        private void EditReminderWarnDays()
        {
            int days = ToSettings.GetReminderWarnDays();
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("label.reminder_term"),
                new EntryField[] {
                    new EntryField(L10n.Get("label.days_until_due"),
                        days.ToString(CultureInfo.InvariantCulture), "number", true)
                }))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                decimal value;
                if (!DataStore.TryNumber(editor.Values[0], out value) || value < 0 || value > 3650 ||
                    value != Decimal.Truncate(value))
                {
                    MessageBox.Show(L10n.Get("error.term_0_3650"), L10n.Get("label.reminders"));
                    return;
                }
                if (!TrySave(delegate { ToSettings.SetReminderWarnDays((int)value); }))
                    return;
                MessageBox.Show((L10n.Get("label.warn_before") + " ") +
                    ((int)value).ToString(CultureInfo.InvariantCulture) + (" " + L10n.Get("unit.days_short")), L10n.Get("label.reminders"));
                if (screenType == "summary")
                    summaryBox.Text = BuildSummary();
            }
        }

        private void ManageTemplatesFromMenu(object sender, EventArgs e)
        {
            string[] typeOptions = new string[] { L10n.Get("label.fuel_entry"), L10n.Get("tab.repairs"), L10n.Get("label.expense_entry") };
            string[] typeKeys = new string[] {
                RecordTemplates.TypeFuel, RecordTemplates.TypeRepair, RecordTemplates.TypeExpense
            };
            using (EntryEditorForm pick = new EntryEditorForm(L10n.Get("label.templates"),
                new EntryField[] {
                    new EntryField(L10n.Get("label.type"), typeOptions[0], typeOptions, true)
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
            sb.Append((L10n.Get("title.templates_dot") + " ")).Append(RecordTemplates.TypeLabelRu(type));
            sb.Append("\r\n\r\n");
            if (list.Count == 0)
                sb.Append((L10n.Get("msg.no_templates_yet") + "\r\n"));
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append("\r\n");
                    sb.Append(i + 1).Append(". ").Append(list[i].Name);
                }
            }
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("label.templates"),
                new EntryField[] {
                    new EntryField(L10n.Get("label.action"), L10n.Get("title.add_from_template"),
                        new string[] { L10n.Get("title.add_from_template"), L10n.Get("button.save_current_as_template"), L10n.Get("button.delete_template") }, true),
                    new EntryField(L10n.Get("label.number_or_name"), list.Count > 0 ? list[0].Name : "", "text", false)
                }))
            {
                MessageBox.Show(sb.ToString(), L10n.Get("label.templates"));
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string action = editor.Values[0];
                string name = editor.Values[1] == null ? "" : editor.Values[1].Trim();
                if (action == "Удалить шаблон")
                {
                    if (name.Length == 0)
                    {
                        MessageBox.Show(L10n.Get("error.specify_template_name"), L10n.Get("label.templates"));
                        return;
                    }
                    if (!TrySave(delegate { RecordTemplates.Delete(type, name); }))
                        return;
                    MessageBox.Show((L10n.Get("msg.deleted_colon") + " ") + name, L10n.Get("label.templates"));
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
                    MessageBox.Show(L10n.Get("error.template_not_found"), L10n.Get("label.templates"));
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
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.fuel_template"),
                new EntryField[] {
                    new EntryField(L10n.Get("label.template_name"), "", "text", true),
                    new EntryField(L10n.Get("label.vehicle"), currentCar, cars, true),
                    new EntryField(L10n.Get("label.odometer_km"), LastCarMileage(currentCar), "number", false),
                    new EntryField(L10n.Get("label.liters"), "", "number", false),
                    new EntryField(L10n.Get("label.cost"), "", "number", false),
                    new EntryField(L10n.Get("label.price_per_liter"), LastFuelPricePerLiter(currentCar), "number", false),
                    new EntryField(L10n.Get("label.comment"), "", "text", false),
                    new EntryField(L10n.Get("label.full_tank_10"), "1", "text", false),
                    new EntryField(L10n.Get("label.purpose"), "", "text", false),
                    EntryField.Suggest(L10n.Get("label.station_brand"), "", StationOptions(""), false)
                }))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                string[] v = editor.Values;
                RecordTemplate item = new RecordTemplate();
                item.Type = RecordTemplates.TypeFuel;
                item.Name = v[0];
                item.Payload = new string[] { v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9] };
                if (!TrySave(delegate { RecordTemplates.Upsert(item); }))
                    return;
                MessageBox.Show(L10n.Get("msg.template_saved"), L10n.Get("label.templates"));
            }
        }

        private void SaveRepairAsTemplate()
        {
            string[] cars = DataStore.LoadCarNames().ToArray();
            string[] cats = DataStore.LoadRepairCategories();
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.repair_template"),
                new EntryField[] {
                    new EntryField(L10n.Get("label.template_name"), "", "text", true),
                    new EntryField(L10n.Get("label.vehicle"), currentCar, cars, true),
                    new EntryField(L10n.Get("label.odometer_km"), LastCarMileage(currentCar), "number", false),
                    new EntryField(L10n.Get("label.category"), cats.Length > 0 ? cats[0] : "", cats, true),
                    new EntryField(L10n.Get("label.product"), "", "text", false),
                    new EntryField(L10n.Get("label.item_name"), "", "text", false),
                    new EntryField(L10n.Get("label.qty_short"), "1", "number", false),
                    new EntryField(L10n.Get("label.amount"), "", "number", false),
                    new EntryField(L10n.Get("label.comment"), "", "text", false)
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
                MessageBox.Show(L10n.Get("msg.template_saved"), L10n.Get("label.templates"));
            }
        }

        private void SaveExpenseAsTemplate()
        {
            string[] cars = DataStore.LoadCarNames().ToArray();
            string[] cats = DataStore.LoadExpenseCategories();
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.expense_template"),
                new EntryField[] {
                    new EntryField(L10n.Get("label.template_name"), "", "text", true),
                    new EntryField(L10n.Get("label.vehicle"), currentCar, cars, true),
                    new EntryField(L10n.Get("label.category"), cats.Length > 0 ? cats[0] : "", cats, true),
                    new EntryField(L10n.Get("label.name"), "", "text", false),
                    new EntryField(L10n.Get("label.amount"), "", "number", false),
                    new EntryField(L10n.Get("label.comment"), "", "text", false)
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
                MessageBox.Show(L10n.Get("msg.template_saved"), L10n.Get("label.templates"));
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
                string station = entity.Length > 9 ? entity[9] : "";
                using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.fuel_from_template"),
                    new EntryField[] {
                        new EntryField(L10n.Get("label.date"), date, "date", true),
                        new EntryField(L10n.Get("label.vehicle"), currentCar, cars, true),
                        new EntryField(L10n.Get("label.odometer_km"), km, "number", true),
                        new EntryField(L10n.Get("label.liters"), liters, "number", true),
                        new EntryField(L10n.Get("label.cost"), cost, "number", true),
                        new EntryField(L10n.Get("label.price_per_liter"), price, "number", false),
                        new EntryField(L10n.Get("label.comment"), comment, "text", false),
                        new EntryField(L10n.Get("label.full_tank_10"), full, "text", false),
                        new EntryField(L10n.Get("label.purpose"), purpose, "text", false),
                        EntryField.Suggest(L10n.Get("label.station_brand"), station, StationOptions(station), false)
                    }))
                {
                    if (editor.ShowDialog() != DialogResult.OK)
                        return;
                    string[] values = editor.Values;
                    if (!TrySave(delegate { DataStore.AddFuel(NormalizeFuelValues(values)); }))
                        return;
                    currentCar = values[1];
                    MessageBox.Show(L10n.Get("msg.fuel_added"), L10n.Get("label.templates"));
                }
            }
            else if (chosen.Type == RecordTemplates.TypeRepair)
            {
                string[] cars = DataStore.LoadCarNames().ToArray();
                string[] cats = DataStore.LoadRepairCategories();
                using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.repair_from_template"),
                    new EntryField[] {
                        new EntryField(L10n.Get("label.date"), date, "date", true),
                        new EntryField(L10n.Get("label.vehicle"), currentCar, cars, true),
                        new EntryField(L10n.Get("label.odometer_km"), entity.Length > 2 ? entity[2] : LastCarMileage(currentCar), "number", true),
                        new EntryField(L10n.Get("label.category"), entity.Length > 3 ? entity[3] : (cats.Length > 0 ? cats[0] : ""), cats, true),
                        new EntryField(L10n.Get("label.product"), entity.Length > 4 ? entity[4] : "", "text", false),
                        new EntryField(L10n.Get("label.item_name"), entity.Length > 5 ? entity[5] : "", "text", false),
                        new EntryField(L10n.Get("label.qty_short"), entity.Length > 6 ? entity[6] : "1", "number", true),
                        new EntryField(L10n.Get("label.amount"), entity.Length > 7 ? entity[7] : "", "number", true),
                        new EntryField(L10n.Get("label.comment"), entity.Length > 8 ? entity[8] : "", "text", false)
                    }))
                {
                    if (editor.ShowDialog() != DialogResult.OK)
                        return;
                    string[] values = editor.Values;
                    if (!TrySave(delegate { DataStore.AddRepair(values); }))
                        return;
                    currentCar = values[1];
                    MessageBox.Show(L10n.Get("msg.repair_added"), L10n.Get("label.templates"));
                }
            }
            else
            {
                string[] cars = DataStore.LoadCarNames().ToArray();
                string[] cats = DataStore.LoadExpenseCategories();
                using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("title.expense_from_template"),
                    new EntryField[] {
                        new EntryField(L10n.Get("label.date"), date, "date", true),
                        new EntryField(L10n.Get("label.vehicle"), currentCar, cars, true),
                        new EntryField(L10n.Get("label.category"), entity.Length > 2 ? entity[2] : (cats.Length > 0 ? cats[0] : ""), cats, true),
                        new EntryField(L10n.Get("label.name"), entity.Length > 3 ? entity[3] : "", "text", true),
                        new EntryField(L10n.Get("label.amount"), entity.Length > 4 ? entity[4] : "", "number", true),
                        new EntryField(L10n.Get("label.comment"), entity.Length > 5 ? entity[5] : "", "text", false)
                    }))
                {
                    if (editor.ShowDialog() != DialogResult.OK)
                        return;
                    string[] values = editor.Values;
                    if (!TrySave(delegate { DataStore.AddExpense(values); }))
                        return;
                    currentCar = values[1];
                    MessageBox.Show(L10n.Get("msg.expense_added"), L10n.Get("label.templates"));
                }
            }
        }

        private void EditBudget(object sender, EventArgs e)
        {
            decimal budget = ToSettings.GetBudgetMonth();
            decimal warnPct = ToSettings.GetBudgetWarnPct();
            EntryField[] fields = new EntryField[]
            {
                new EntryField(L10n.Get("label.limit_0_off"),
                    budget.ToString(CultureInfo.InvariantCulture), "number", true),
                new EntryField(L10n.Get("label.warn_pct"),
                    warnPct.ToString(CultureInfo.InvariantCulture), "number", true)
            };
            using (EntryEditorForm editor = new EntryEditorForm(L10n.Get("label.monthly_budget"), fields))
            {
                if (editor.ShowDialog() != DialogResult.OK)
                    return;
                decimal monthLimit, pct;
                if (!DataStore.TryNumber(editor.Values[0], out monthLimit) || monthLimit < 0)
                {
                    MessageBox.Show(L10n.Get("error.limit_invalid"), L10n.Get("label.budget"));
                    return;
                }
                if (!DataStore.TryNumber(editor.Values[1], out pct) || pct <= 0 || pct > 100)
                {
                    MessageBox.Show(L10n.Get("error.warn_pct_range"), L10n.Get("label.budget"));
                    return;
                }
                try
                {
                    if (!TrySave(delegate { ToSettings.SetBudget(monthLimit, pct); }))
                        return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, L10n.Get("label.budget"));
                    return;
                }
                if (monthLimit <= 0)
                    MessageBox.Show(L10n.Get("msg.budget_disabled"), L10n.Get("label.budget"));
                else
                    MessageBox.Show((L10n.Get("summary.monthly_budget_colon") + " ") + Money(monthLimit) + " " + Cur() +
                        (L10n.Get("summary.warn_from") + " ") + Money(pct) + "%.", L10n.Get("label.budget"));
                if (summaryBox != null)
                    summaryBox.Text = BuildSummary();
            }
        }

   
        private void InitializeLanguage()
        {
            try
            {
                string language = ToSettings.GetLanguage();
                LocalizationManager.SetLanguage(language);
            }
            catch
            {
                LocalizationManager.SetLanguage("ru");
            }
        }
    }
}
