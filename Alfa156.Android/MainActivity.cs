using Alfa156.Localization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using AndroidBitmap = global::Android.Graphics.Bitmap;
using AndroidBitmapFactory = global::Android.Graphics.BitmapFactory;
using AndroidColor = global::Android.Graphics.Color;
using AndroidTypefaceStyle = global::Android.Graphics.TypefaceStyle;
using AndroidLayout = global::Android.Resource.Layout;
using Uri = global::Android.Net.Uri;

[assembly: Android.App.UsesPermission(Android.Manifest.Permission.Internet)]
[assembly: Android.App.UsesPermission("android.permission.POST_NOTIFICATIONS")]
[assembly: Android.App.UsesPermission(Android.Manifest.Permission.Camera)]

namespace Alfa156.Android;

[Activity(
    Label = "Alfa 156",
    Icon = "@mipmap/ic_launcher",
    MainLauncher = true,
    Exported = true,
    Theme = "@android:style/Theme.Material.Light.NoActionBar")]
public sealed class MainActivity : Activity
{
    private const int ImportRequest = 1001;
    private const int ExportRequest = 1002;
    private const int PhotoCameraRequest = 1003;
    private const int PhotoGalleryRequest = 1004;
    private const int SummaryExportRequest = 1005;
    private const string Fuel = "fuel";
    private const string Repairs = "repairs";
    private const string Expenses = "expenses";
    private const string Utilities = "utilities";
    private const string AllCategories = "Все категории";
    private const string ToReminderChannelId = "to_reminders";
    private const string BudgetChannelId = "budget";
    private const string DateReminderChannelId = "date_reminders";
    private const int BudgetNotificationId = 9001;
    private const int DateReminderNotificationId = 9100;
    private static readonly string[] DataFiles = ExpenseDataCodec.DataFiles;

    private TextView status;
    private ListView recordList;
    private Spinner carSpinner;
    private Button fuelButton;
    private Button repairsButton;
    private Button expensesButton;
    private Button utilitiesButton;
    private Button summaryButton;
    private Button addButton;
    private Button editButton;
    private Button deleteButton;
    private Button filterButton;
    private Button resetFilterButton;
    private Button categoriesButton;
    private Button backupButton;
    private Button restoreButton;
    private Button syncButton;
    private Button dataMenuButton;
    private Button addCarButton;
    private LinearLayout filterRow;
    private TcpListener syncListener;
    private TcpClient syncClient;
    private volatile bool syncActive;
    private string selectedSection = Fuel;
    private string currentCar = "";
    private string filterCategory = AllCategories;
    private string filterDateFrom = "";
    private string filterDateTo = "";
    private string filterText = "";
    private string fuelFilterDateFrom = "";
    private string fuelFilterDateTo = "";
    private string fuelFilterText = "";
    private string fuelFilterPurpose = "";
    private string fuelFilterStation = "";
    private bool updatingCarSpinner;
    private int selectedRecordPosition = -1;
    private List<string[]> allRows = new List<string[]>();
    private List<string[]> visibleRows = new List<string[]>();
    private List<int> visibleIndexes = new List<int>();
    private string editorReceiptId;
    private ImageView editorReceiptThumb;
    private Java.IO.File cameraCaptureFile;
    private string pendingSummaryText;

    private string DataDirectory
    {
        get { return Path.Combine(FilesDir.AbsolutePath, "Alfa156"); }
    }

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        try
        {
            Title = "Alfa 156";
            var settings = GetSharedPreferences("settings", FileCreationMode.Private);
            currentCar = settings.GetString("currentCar", "");
            string savedSection = settings.GetString("selectedSection", Fuel);
            if (savedSection == Fuel || savedSection == Repairs || savedSection == Expenses ||
                savedSection == Utilities)
                selectedSection = savedSection;
            InitializeLanguage();
            CreateLayout();
            EnsureNotificationChannel();
            EnsureBudgetNotificationChannel();
            EnsureDateReminderNotificationChannel();
            RefreshRecords();
            RefreshServiceNotifications();
            RefreshBudgetNotification();
            MaybeShowOnboardingTip();
        }
        catch (Exception ex)
        {
            ShowStartupError(ex);
        }
    }

    private void MaybeShowOnboardingTip()
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            if (ToSettings.IsOnboardingSeen(DataDirectory))
                return;
            if (IsCompleteData(DataDirectory))
            {
                ToSettings.SetOnboardingSeen(DataDirectory, true);
                return;
            }
            ToSettings.SetOnboardingSeen(DataDirectory, true);
            new AlertDialog.Builder(this)
                .SetTitle(L10n.Get("title.welcome"))
                .SetMessage(
                    (L10n.Get("label.brief_colon") + "\n") +
                    (L10n.Get("about.bullet_wifi_pda_pc") + "\n") +
                    (L10n.Get("about.bullet_full_zip") + "\n") +
                    (L10n.Get("about.bullet_summary_chart_reminders") + "\n\n") +
                    L10n.Get("hint.start_with_data_menu"))
                .SetPositiveButton(L10n.Get("button.ok"), delegate { })
                .Show();
        }
        catch
        {
            // подсказка не должна мешать запуску
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
        try
        {
            RefreshServiceNotifications();
            RefreshBudgetNotification();
            RefreshDateReminderNotifications();
            MaybeShowBackupReminder();
        }
        catch
        {
            // уведомления не должны ломать основной UI
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (resultCode != Result.Ok)
            return;

        try
        {
            if (requestCode == ImportRequest)
            {
                if (data == null)
                    return;
                ImportSelection(data);
            }
            else if (requestCode == ExportRequest)
            {
                if (data == null)
                    return;
                ExportTo(data.Data);
                ShowMessage(L10n.Get("msg.full_zip_saved_desc"));
            }
            else if (requestCode == SummaryExportRequest)
            {
                if (data == null || data.Data == null || String.IsNullOrEmpty(pendingSummaryText))
                    return;
                using (Stream output = ContentResolver.OpenOutputStream(data.Data))
                {
                    if (output == null)
                        throw new IOException(L10n.Get("error.open_file_write_failed"));
                    byte[] bytes = Encoding.UTF8.GetBytes(pendingSummaryText);
                    output.Write(bytes, 0, bytes.Length);
                }
                pendingSummaryText = null;
                ShowMessage(L10n.Get("msg.summary_saved_txt"));
            }
            else if (requestCode == PhotoGalleryRequest)
            {
                if (data == null || data.Data == null)
                    return;
                SaveReceiptFromUri(data.Data);
            }
            else if (requestCode == PhotoCameraRequest)
            {
                SaveReceiptFromCamera(data);
            }
        }
        catch (Exception ex)
        {
            ShowMessage((L10n.Get("error.operation_failed_colon") + " ") + ex.Message);
        }
    }

    private void CreateLayout()
    {
        LinearLayout root = new LinearLayout(this);
        root.Orientation = Orientation.Vertical;
        root.SetPadding(Dp(12), Dp(8), Dp(12), Dp(4));
        root.SetBackgroundColor(AndroidColor.Rgb(245, 247, 250));

        LinearLayout titleRow = NewRow();
        titleRow.SetGravity(GravityFlags.CenterVertical);
        ImageView logo = new ImageView(this);
        logo.SetImageResource(Resource.Drawable.icon);
        logo.SetScaleType(ImageView.ScaleType.FitCenter);
        titleRow.AddView(logo, new LinearLayout.LayoutParams(Dp(36), Dp(36)));
        TextView title = new TextView(this);
        title.Text = "  Alfa 156";
        title.TextSize = 22;
        title.SetTypeface(title.Typeface, AndroidTypefaceStyle.Bold);
        title.SetTextColor(AndroidColor.Rgb(180, 40, 40));
        titleRow.AddView(title, new LinearLayout.LayoutParams(0,
            ViewGroup.LayoutParams.WrapContent, 1));
        dataMenuButton = CreateButton(L10n.Get("menu.data"), ButtonIcons.Menu, delegate { ShowDataMenu(); });
        dataMenuButton.TextSize = 12;
        titleRow.AddView(dataMenuButton, new LinearLayout.LayoutParams(
            Dp(126), ViewGroup.LayoutParams.WrapContent));
        root.AddView(titleRow, MatchWrap());

        // Состояние sync/backup/restore хранится флагами; кнопки — тонкие заглушки для старого кода.
        syncButton = CreateButton(L10n.Get("sync.wifi_title"), ButtonIcons.Sync, delegate { ToggleWiFiSync(); });
        backupButton = CreateButton(L10n.Get("backup.on_phone"), ButtonIcons.Backup, delegate { CreateLocalBackup(); });
        restoreButton = CreateButton(L10n.Get("button.restore"), ButtonIcons.Restore, delegate { RestoreLocalBackup(); });

        LinearLayout carRow = NewRow();
        carRow.SetGravity(GravityFlags.CenterVertical);
        carRow.SetPadding(0, Dp(2), 0, Dp(2));
        TextView carLabel = new TextView(this);
        carLabel.Text = L10n.Get("label.car_short");
        carLabel.TextSize = 14;
        carLabel.SetTextColor(AndroidColor.Rgb(35, 51, 70));
        carRow.AddView(carLabel, new LinearLayout.LayoutParams(Dp(42), ViewGroup.LayoutParams.WrapContent));
        carSpinner = new Spinner(this);
        carSpinner.ItemSelected += CarSelected;
        carRow.AddView(carSpinner, new LinearLayout.LayoutParams(0,
            ViewGroup.LayoutParams.WrapContent, 1));
        addCarButton = CreateButton(L10n.Get("label.car_short"), ButtonIcons.Menu, delegate { ManageCars(); });
        carRow.AddView(addCarButton, new LinearLayout.LayoutParams(Dp(88), ViewGroup.LayoutParams.WrapContent));
        root.AddView(carRow);

        LinearLayout sectionRow1 = NewRow();
        sectionRow1.SetPadding(0, Dp(2), 0, Dp(2));
        fuelButton = CreateSectionButton(L10n.Get("tab.fuel"), ButtonIcons.Fuel, delegate { SelectSection(Fuel); });
        repairsButton = CreateSectionButton(L10n.Get("section.repairs_and_service"), ButtonIcons.Repairs, delegate { SelectSection(Repairs); });
        sectionRow1.AddView(fuelButton, WeightedButton(Dp(3)));
        sectionRow1.AddView(repairsButton, WeightedButton(Dp(3)));
        root.AddView(sectionRow1);

        LinearLayout sectionRow2 = NewRow();
        sectionRow2.SetPadding(0, 0, 0, Dp(2));
        expensesButton = CreateSectionButton(L10n.Get("tab.expenses"), ButtonIcons.Categories, delegate { SelectSection(Expenses); });
        utilitiesButton = CreateSectionButton(L10n.Get("label.utilities_full"), ButtonIcons.Utilities, delegate { SelectSection(Utilities); });
        sectionRow2.AddView(expensesButton, WeightedButton(Dp(3)));
        sectionRow2.AddView(utilitiesButton, WeightedButton(Dp(3)));
        root.AddView(sectionRow2);

        LinearLayout sectionRow3 = NewRow();
        sectionRow3.SetPadding(0, 0, 0, Dp(2));
        summaryButton = CreateSectionButton(L10n.Get("button.summary_and_to"), ButtonIcons.Summary, delegate { ShowSummary(); });
        Button quickFuelButton = CreateSectionButton(L10n.Get("button.quick_fuel"), ButtonIcons.Fuel,
            delegate { ShowQuickFuelEntry(); });
        sectionRow3.AddView(summaryButton, WeightedButton(Dp(3)));
        sectionRow3.AddView(quickFuelButton, WeightedButton(Dp(3)));
        root.AddView(sectionRow3);

        LinearLayout recordActions = NewRow();
        recordActions.SetPadding(0, Dp(2), 0, Dp(2));
        addButton = CreateButton(L10n.Get("button.add"), ButtonIcons.Add, delegate { AddRecord(); });
        editButton = CreateButton(L10n.Get("button.edit"), ButtonIcons.Edit, delegate { EditSelectedRecord(); });
        deleteButton = CreateButton(L10n.Get("button.delete"), ButtonIcons.Delete, delegate { DeleteSelectedRecord(); });
        recordActions.AddView(addButton, WeightedButton(Dp(2)));
        recordActions.AddView(editButton, WeightedButton(Dp(2)));
        recordActions.AddView(deleteButton, WeightedButton(Dp(2)));
        root.AddView(recordActions);

        filterRow = NewRow();
        filterButton = CreateButton(L10n.Get("label.filter"), ButtonIcons.Filter, delegate { EditSectionFilter(); });
        resetFilterButton = CreateButton(L10n.Get("button.reset"), ButtonIcons.ResetFilter, delegate { ResetSectionFilter(); });
        categoriesButton = CreateButton(L10n.Get("tab.categories"), ButtonIcons.Categories, delegate
        {
            if (selectedSection == Expenses)
                ManageExpenseCategories();
            else
                ManageRepairCategories();
        });
        filterRow.AddView(filterButton, WeightedButton(Dp(2)));
        filterRow.AddView(resetFilterButton, WeightedButton(Dp(2)));
        filterRow.AddView(categoriesButton, WeightedButton(Dp(2)));
        root.AddView(filterRow);

        status = new TextView(this);
        status.TextSize = 12;
        status.SetTextColor(AndroidColor.Rgb(60, 70, 80));
        status.SetPadding(0, Dp(6), 0, Dp(4));
        root.AddView(status, MatchWrap());

        recordList = new ListView(this);
        recordList.ChoiceMode = ChoiceMode.Single;
        recordList.ItemClick += RecordSelected;
        root.AddView(recordList, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, 0, 1));
        SetContentView(root);
    }

    private void ShowDataMenu()
    {
        bool hasData = IsCompleteData(DataDirectory);
        List<string> labels = new List<string>();
        List<System.Action> actions = new List<System.Action>();

        if (syncActive)
        {
            labels.Add(L10n.Get("button.stop_sync"));
            actions.Add(delegate { ToggleWiFiSync(); });
        }
        else
        {
            if (!hasData)
            {
                labels.Add(L10n.Get("button.start_fresh"));
                actions.Add(delegate { StartFromScratch(); });
            }
            labels.Add(L10n.Get("button.import_backup"));
            actions.Add(delegate { ChooseImportFiles(); });
            if (hasData)
            {
                labels.Add(L10n.Get("button.full_zip_export"));
                actions.Add(delegate { ChooseExportFile(); });
            }
            labels.Add(L10n.Get("sync.wifi_title"));
            actions.Add(delegate { ToggleWiFiSync(); });
            labels.Add(L10n.Get("button.google_sync"));
            actions.Add(delegate { SyncGoogle(); });
            labels.Add(L10n.Get("button.google_settings"));
            actions.Add(delegate { ConfigureGoogleSync(); });
            if (hasData)
            {
                labels.Add(L10n.Get("title.search"));
                actions.Add(delegate { ShowGlobalSearch(); });
                labels.Add(L10n.Get("button.duplicates"));
                actions.Add(delegate { ShowDuplicates(); });
                labels.Add(L10n.Get("button.data_integrity"));
                actions.Add(delegate { ShowDataIntegrity(); });
                labels.Add(L10n.Get("label.service_intervals"));
                actions.Add(delegate { EditToIntervals(); });
                labels.Add(L10n.Get("label.currency"));
                actions.Add(delegate { EditCurrency(); });
                labels.Add(L10n.Get("label.language"));
                actions.Add(delegate { EditLanguage(); });
                labels.Add(L10n.Get("label.monthly_budget"));
                actions.Add(delegate { EditBudget(); });
                labels.Add(L10n.Get("label.default_car_full"));
                actions.Add(delegate { EditDefaultCar(); });
                labels.Add(L10n.Get("button.quick_fuel"));
                actions.Add(delegate { ShowQuickFuelEntry(); });
                labels.Add(L10n.Get("button.trip"));
                actions.Add(delegate { ShowTripEntry(); });
                labels.Add(L10n.Get("button.trip_report"));
                actions.Add(delegate { ShowTripReport(); });
                labels.Add(L10n.Get("title.backup_reminder"));
                actions.Add(delegate { EditBackupReminder(); });
                labels.Add(L10n.Get("label.reminders"));
                actions.Add(delegate { ManageDateReminders(); });
                labels.Add(L10n.Get("label.reminder_term"));
                actions.Add(delegate { EditReminderWarnDays(); });
                labels.Add(L10n.Get("label.notes"));
                actions.Add(delegate { ManageCarNotes(); });
                labels.Add(L10n.Get("backup.on_phone"));
                actions.Add(delegate { CreateLocalBackup(); });
                labels.Add(L10n.Get("title.restore_backup"));
                actions.Add(delegate { RestoreLocalBackup(); });
            }
        }
        labels.Add(L10n.Get("title.about"));
        actions.Add(delegate { ShowAbout(); });

        new AlertDialog.Builder(this)
            .SetTitle(syncActive ? L10n.Get("status.data_syncing") : L10n.Get("menu.data"))
            .SetItems(labels.ToArray(), (sender, args) =>
            {
                if (args.Which >= 0 && args.Which < actions.Count)
                    actions[args.Which]();
            })
            .SetNegativeButton(L10n.Get("button.close"), delegate { })
            .Show();
    }

    private LinearLayout NewRow()
    {
        LinearLayout row = new LinearLayout(this);
        row.Orientation = Orientation.Horizontal;
        return row;
    }

    private LinearLayout.LayoutParams MatchWrap()
    {
        return new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
    }

    private LinearLayout.LayoutParams WeightedButton()
    {
        return WeightedButton(0);
    }

    private LinearLayout.LayoutParams WeightedButton(int margin)
    {
        LinearLayout.LayoutParams parameters = new LinearLayout.LayoutParams(
            0, ViewGroup.LayoutParams.WrapContent, 1);
        if (margin > 0)
            parameters.SetMargins(margin, 0, margin, 0);
        return parameters;
    }

    private Button CreateButton(string text, EventHandler clickHandler)
    {
        return CreateButton(text, null, clickHandler);
    }

    private Button CreateButton(string text, string iconKind, EventHandler clickHandler)
    {
        Button button = new Button(this);
        button.Text = text;
        button.TextSize = 11;
        button.SetAllCaps(false);
        button.SetMinHeight(Dp(40));
        button.SetPadding(Dp(6), 0, Dp(6), 0);
        if (!string.IsNullOrEmpty(iconKind))
        {
            button.Tag = iconKind;
            ApplyButtonIcon(button, iconKind, IconColor(false), false);
        }
        if (clickHandler != null)
            button.Click += clickHandler;
        return button;
    }

    private Button CreateSectionButton(string text, string iconKind, EventHandler clickHandler)
    {
        Button button = new Button(this);
        button.Text = text;
        button.TextSize = 12;
        button.SetMinHeight(Dp(64));
        button.SetPadding(Dp(4), Dp(8), Dp(4), Dp(8));
        button.SetAllCaps(false);
        if (!string.IsNullOrEmpty(iconKind))
        {
            button.Tag = iconKind;
            ApplyButtonIcon(button, iconKind, IconColor(false), true);
        }
        if (clickHandler != null)
            button.Click += clickHandler;
        return button;
    }

    private void ApplyButtonIcon(Button button, string iconKind, AndroidColor color, bool iconOnTop)
    {
        if (button == null || string.IsNullOrEmpty(iconKind))
            return;
        var icon = ButtonIcons.Create(this, iconKind, color);
        if (iconOnTop)
            button.SetCompoundDrawablesWithIntrinsicBounds(null, icon, null, null);
        else
            button.SetCompoundDrawablesWithIntrinsicBounds(icon, null, null, null);
        button.CompoundDrawablePadding = Dp(4);
    }

    private static AndroidColor IconColor(bool selected)
    {
        return selected ? AndroidColor.White : AndroidColor.Rgb(35, 51, 70);
    }

    private int Dp(int value)
    {
        return (int)(value * Resources.DisplayMetrics.Density + 0.5f);
    }

    private void CarSelected(object sender, AdapterView.ItemSelectedEventArgs e)
    {
        if (updatingCarSpinner || e.Position < 0 || !IsCompleteData(DataDirectory))
            return;
        ArrayAdapter<string> adapter = carSpinner.Adapter as ArrayAdapter<string>;
        if (adapter == null || e.Position >= adapter.Count)
            return;
        currentCar = adapter.GetItem(e.Position);
        GetSharedPreferences("settings", FileCreationMode.Private)
            .Edit().PutString("currentCar", currentCar).Apply();
        RefreshRecords();
    }

    private void SelectSection(string section)
    {
        selectedSection = section;
        GetSharedPreferences("settings", FileCreationMode.Private)
            .Edit().PutString("selectedSection", selectedSection).Apply();
        UpdateSectionButtons();
        RefreshRecords();
    }

    private void UpdateSectionButtons()
    {
        SetSectionButton(fuelButton, selectedSection == Fuel, true);
        SetSectionButton(repairsButton, selectedSection == Repairs, true);
        SetSectionButton(expensesButton, selectedSection == Expenses, true);
        SetSectionButton(utilitiesButton, selectedSection == Utilities, true);
        SetSectionButton(summaryButton, false, true);
    }

    private void SetSectionButton(Button button, bool selected, bool iconOnTop)
    {
        if (button == null)
            return;
        button.SetBackgroundColor(selected
            ? AndroidColor.Rgb(49, 77, 102)
            : AndroidColor.Rgb(225, 231, 237));
        button.SetTextColor(selected ? AndroidColor.White :
            AndroidColor.Rgb(35, 51, 70));
        string iconKind = button.Tag != null ? button.Tag.ToString() : null;
        if (!string.IsNullOrEmpty(iconKind))
            ApplyButtonIcon(button, iconKind, IconColor(selected), iconOnTop);
    }

    private void RefreshRecords()
    {
        bool hasData = IsCompleteData(DataDirectory);
        if (hasData)
        {
            try { ExpenseDataStore.EnsureCompanionFiles(DataDirectory); }
            catch { }
        }
        SetDataActionsEnabled(hasData);
        if (!hasData)
        {
            status.Text = L10n.Get("hint.data_start_or_import");
            allRows = new List<string[]>();
            visibleRows = new List<string[]>();
            visibleIndexes = new List<int>();
            selectedRecordPosition = -1;
            recordList.Adapter = CreateAdapter(new List<string>
            {
                L10n.Get("msg.no_data_start_or_import")
            });
            SetCarOptions(new List<string>());
            UpdateSectionButtons();
            UpdateFilterRowVisibility();
            UpdateRecordActions();
            return;
        }

        ExpenseDataStore.EnsureRepairCategories(DataDirectory);
        List<string> cars = ExpenseDataStore.LoadCarNames(DataDirectory);
        currentCar = ToSettings.ResolvePreferredCar(DataDirectory, cars, currentCar);
        SetCarOptions(cars);
        GetSharedPreferences("settings", FileCreationMode.Private)
            .Edit().PutString("currentCar", currentCar).Apply();

        List<string[]> fuelRows = ExpenseDataStore.Load(DataDirectory, "fuel.dat");
        List<string[]> repairRows = ExpenseDataStore.Load(DataDirectory, "repairs.dat");
        List<string[]> utilityRows = ExpenseDataStore.Load(DataDirectory, "utilities.dat");
        allRows = ExpenseDataStore.Load(DataDirectory, GetSelectedFile());
        visibleRows = new List<string[]>();
        visibleIndexes = new List<int>();
        selectedRecordPosition = -1;
        List<string> displayRows = new List<string>();
        for (int i = allRows.Count - 1; i >= 0; i--)
        {
            string[] row = allRows[i];
            if (selectedSection != Utilities && row[1] != currentCar)
                continue;
            if (selectedSection == Repairs && !MatchesRepairFilter(row))
                continue;
            if (selectedSection == Fuel && !MatchesFuelFilter(row))
                continue;
            visibleRows.Add(row);
            visibleIndexes.Add(i);
            displayRows.Add(FormatRow(row));
        }

        string sectionTitle = selectedSection == Fuel ? L10n.Get("tab.fuel")
            : selectedSection == Repairs ? L10n.Get("section.repairs_and_service")
            : selectedSection == Expenses ? L10n.Get("label.other_expenses") : L10n.Get("label.utilities_full");
        string scope = selectedSection == Utilities ? L10n.Get("label.all_months") : currentCar;
        string filterNote = HasCurrentFilter() ? (" " + L10n.Get("status.filter")) : "";
        if (displayRows.Count == 0)
        {
            status.Text = sectionTitle + " · " + scope + (" " + L10n.Get("status.no_records")) + filterNote;
            displayRows.Add(HasCurrentFilter()
                ? L10n.Get("msg.no_records_filter_reset")
                : L10n.Get("msg.no_records_tap_add"));
        }
        else if (selectedSection == Utilities)
        {
            status.Text = sectionTitle + (" " + L10n.Get("status.shown") + " ") + displayRows.Count +
                (" " + L10n.Get("common.of_preposition") + " ") + utilityRows.Count + filterNote;
        }
        else
        {
            status.Text = sectionTitle + " · " + currentCar + (" " + L10n.Get("status.in_list") + " ") +
                displayRows.Count + filterNote +
                (" " + L10n.Get("status.in_db_fuel") + " ") + fuelRows.Count +
                (L10n.Get("summary.comma_repairs") + " ") + repairRows.Count + (L10n.Get("summary.comma_util_short") + " ") + utilityRows.Count;
        }
        recordList.Adapter = CreateAdapter(displayRows);
        UpdateRecordActions();
        UpdateSectionButtons();
        UpdateFilterRowVisibility();
    }

    private void UpdateFilterRowVisibility()
    {
        if (filterRow == null)
            return;
        bool showFilters = selectedSection == Fuel || selectedSection == Repairs;
        filterRow.Visibility = showFilters ? ViewStates.Visible : ViewStates.Gone;
        categoriesButton.Visibility = (selectedSection == Repairs || selectedSection == Expenses)
            ? ViewStates.Visible : ViewStates.Gone;
    }

    private void SetDataActionsEnabled(bool enabled)
    {
        addButton.Enabled = enabled && !syncActive;
        backupButton.Enabled = enabled && !syncActive;
        restoreButton.Enabled = enabled && !syncActive;
        if (dataMenuButton != null)
            dataMenuButton.Enabled = true;
        if (addCarButton != null)
            // Без данных кнопка запускает «Начать с нуля».
            addCarButton.Enabled = !syncActive;
        syncButton.Enabled = true;
        fuelButton.Enabled = !syncActive;
        repairsButton.Enabled = !syncActive;
        utilitiesButton.Enabled = !syncActive;
        summaryButton.Enabled = !syncActive;
        recordList.Enabled = !syncActive;
        bool supportsFilter = selectedSection == Repairs || selectedSection == Fuel;
        UpdateFilterButtonAppearance(enabled && !syncActive && supportsFilter);
        resetFilterButton.Enabled = enabled && !syncActive && supportsFilter && HasCurrentFilter();
        categoriesButton.Enabled = enabled && !syncActive && (selectedSection == Repairs || selectedSection == Expenses);
        carSpinner.Enabled = enabled && !syncActive;
        UpdateFilterRowVisibility();
    }

    private void UpdateRecordActions()
    {
        bool selected = selectedRecordPosition >= 0 &&
            selectedRecordPosition < visibleRows.Count;
        bool hasData = IsCompleteData(DataDirectory);
        editButton.Enabled = selected && !syncActive && hasData;
        deleteButton.Enabled = selected && !syncActive && hasData;
        bool supportsFilter = selectedSection == Repairs || selectedSection == Fuel;
        UpdateFilterButtonAppearance(!syncActive && supportsFilter && hasData);
        resetFilterButton.Enabled = supportsFilter &&
            !syncActive && hasData && HasCurrentFilter();
        categoriesButton.Enabled = !syncActive && (selectedSection == Repairs || selectedSection == Expenses) && hasData;
        if (addCarButton != null)
            addCarButton.Enabled = !syncActive;
        addButton.Enabled = hasData && !syncActive;
    }

    private void UpdateFilterButtonAppearance(bool enabled)
    {
        if (filterButton == null)
            return;
        bool active = HasCurrentFilter();
        filterButton.Text = (selectedSection == Fuel ? L10n.Get("title.fuel_filter") : L10n.Get("label.filter")) +
            (active ? " ●" : "");
        filterButton.Enabled = enabled;
        filterButton.SetBackgroundColor(active
            ? AndroidColor.Rgb(196, 101, 30)
            : AndroidColor.Rgb(225, 231, 237));
        filterButton.SetTextColor(active
            ? AndroidColor.White
            : AndroidColor.Rgb(35, 51, 70));
        string iconKind = filterButton.Tag != null ? filterButton.Tag.ToString() : ButtonIcons.Filter;
        ApplyButtonIcon(filterButton, iconKind, IconColor(active), false);
    }

    private void SetCarOptions(List<string> cars)
    {
        updatingCarSpinner = true;
        ArrayAdapter<string> adapter = new ArrayAdapter<string>(
            this, AndroidLayout.SimpleSpinnerItem);
        for (int i = 0; i < cars.Count; i++)
            adapter.Add(cars[i]);
        adapter.SetDropDownViewResource(AndroidLayout.SimpleSpinnerDropDownItem);
        carSpinner.Adapter = adapter;
        int selected = cars.IndexOf(currentCar);
        if (selected >= 0)
            carSpinner.SetSelection(selected);
        updatingCarSpinner = false;
    }

    private ArrayAdapter<string> CreateAdapter(List<string> rows)
    {
        ArrayAdapter<string> adapter = new ArrayAdapter<string>(
            this, AndroidLayout.SimpleListItem1, global::Android.Resource.Id.Text1);
        for (int i = 0; i < rows.Count; i++)
            adapter.Add(rows[i]);
        return adapter;
    }

    private void ShowStartupError(Exception exception)
    {
        LinearLayout root = new LinearLayout(this);
        root.Orientation = Orientation.Vertical;
        root.SetPadding(Dp(16), Dp(16), Dp(16), Dp(16));
        root.SetBackgroundColor(AndroidColor.Rgb(245, 247, 250));

        TextView title = new TextView(this);
        title.Text = L10n.Get("error.open_app_failed");
        title.TextSize = 20;
        title.SetTextColor(AndroidColor.Rgb(150, 30, 30));
        root.AddView(title, MatchWrap());

        ScrollView scroll = new ScrollView(this);
        TextView details = new TextView(this);
        details.Text = exception.ToString();
        details.TextSize = 12;
        details.SetTextColor(AndroidColor.Rgb(35, 51, 70));
        details.SetTextIsSelectable(true);
        scroll.AddView(details);
        root.AddView(scroll, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, 0, 1));
        SetContentView(root);
    }

    protected override void OnDestroy()
    {
        StopWiFiSync();
        base.OnDestroy();
    }

    private void ConfigureGoogleSync()
    {
        string scriptUrl;
        string token;
        GoogleSyncService.LoadSettings(this, out scriptUrl, out token);
        EditText urlInput = new EditText(this) { Hint = "URL Apps Script", Text = scriptUrl };
        EditText tokenInput = new EditText(this) { Hint = "SYNC_TOKEN", Text = token };
        LinearLayout box = new LinearLayout(this) { Orientation = Orientation.Vertical };
        box.SetPadding(Dp(20), Dp(8), Dp(20), Dp(8));
        TextView help = new TextView(this);
        help.Text = (L10n.Get("wizard.gs_summary") + " ") +
            L10n.Get("hint.desktop_wizard_readme");
        help.TextSize = 13;
        help.SetPadding(0, 0, 0, Dp(8));
        box.AddView(help);
        box.AddView(urlInput);
        box.AddView(tokenInput);
        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("button.google_settings"))
            .SetView(box)
            .SetPositiveButton(L10n.Get("button.save"), delegate
            {
                GoogleSyncService.SaveSettings(this, urlInput.Text, tokenInput.Text);
                ShowMessage(L10n.Get("msg.google_settings_saved"));
            })
            .SetNeutralButton(L10n.Get("menu.help"), delegate { ShowGoogleSyncHelp(); })
            .SetNegativeButton(L10n.Get("button.cancel"), delegate { })
            .Show();
    }

    private void ShowGoogleSyncHelp()
    {
        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("title.google_sync_help"))
            .SetMessage(
                (L10n.Get("wizard.gs_step1_open") + "\n") +
                (L10n.Get("wizard.gs_step2_code") + "\n") +
                (L10n.Get("wizard.gs_step3_token") + "\n") +
                (L10n.Get("wizard.gs_step4_deploy") + "\n\n") +
                (L10n.Get("hint.desktop_google_wizard_btn") + "\n") +
                L10n.Get("hint.github_google_sync_readme"))
            .SetPositiveButton("OK", delegate { })
            .Show();
    }

    private void SyncGoogle()
    {
        if (syncActive)
        {
            ShowMessage(L10n.Get("error.stop_wifi_sync_first"));
            return;
        }
        if (!IsCompleteData(DataDirectory))
        {
            ShowMessage(L10n.Get("error.create_or_import_first"));
            return;
        }
        status.Text = L10n.Get("status.google_syncing");
        Task.Run(async delegate
        {
            try
            {
                string message = await GoogleSyncService.SyncAsync(
                    this,
                    DataDirectory,
                    delegate(string summary) { return ResolveSyncPlan(summary, "Google", false); },
                    delegate(string title, string localRow, string incomingRow)
                    {
                        return ResolveSyncConflict(title, localRow, incomingRow, "Google", false);
                    }).ConfigureAwait(false);
                RunOnUiThread(delegate
                {
                    if (IsFinishing) return;
                    RefreshRecords();
                    ShowMessage(message);
                });
            }
            catch (System.OperationCanceledException)
            {
                RunOnUiThread(delegate
                {
                    if (!IsFinishing)
                        ShowMessage(L10n.Get("msg.google_sync_cancelled"));
                });
            }
            catch (Exception ex)
            {
                RunOnUiThread(delegate
                {
                    if (!IsFinishing)
                        ShowMessage("Google sync: " + ex.Message);
                });
            }
        });
    }

    private void ToggleWiFiSync()
    {
        if (syncActive)
        {
            StopWiFiSync();
            return;
        }

        try
        {
            string[] addresses = ExpenseSyncService.GetLocalIpv4Addresses().ToArray();
            string addressText = addresses.Length == 0
                ? L10n.Get("sync.ip_not_found_hint")
                : String.Join("\n", addresses);
            bool hasHotspot = false;
            for (int i = 0; i < addresses.Length; i++)
                if (addresses[i].IndexOf("точка доступа", StringComparison.OrdinalIgnoreCase) >= 0)
                    hasHotspot = true;
            if (!hasHotspot && addresses.Length > 0)
                addressText += ("\n\n" + L10n.Get("sync.hotspot_missing_hint1") + " ") +
                    L10n.Get("sync.pda_try_192_168_43_1");
            string pairingCode = System.Security.Cryptography.RandomNumberGenerator
                .GetInt32(100000, 1000000).ToString(CultureInfo.InvariantCulture);

            TcpListener listener = new TcpListener(IPAddress.Any, ExpenseSyncService.Port);
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Start(8);
            syncListener = listener;
            syncActive = true;
            syncButton.Text = L10n.Get("button.stop_sync");
            if (dataMenuButton != null)
                dataMenuButton.Text = L10n.Get("status.data_dirty");
            SetDataActionsEnabled(IsCompleteData(DataDirectory));
            UpdateRecordActions();

            new AlertDialog.Builder(this)
                .SetTitle(L10n.Get("title.sync_with_pda"))
                .SetMessage((L10n.Get("sync.option_a_hotspot") + " ") +
                    (L10n.Get("sync.no_mobile_net_needed") + "\n") +
                    (L10n.Get("sync.option_b_same_wifi") + "\n\n") +
                    (L10n.Get("sync.phone_ip_colon") + "\n") + addressText +
                    ("\n\n" + L10n.Get("sync.code_colon") + " ") + pairingCode +
                    ("\n\n" + L10n.Get("sync.on_pda_wifi_steps")))
                .SetPositiveButton(L10n.Get("button.hide"), delegate { })
                .SetNegativeButton(L10n.Get("button.stop"), delegate { StopWiFiSync(); })
                .Show();

            TcpClient acceptedClient = null;
            Task.Run(delegate
            {
                try
                {
                    string error = ExpenseSyncService.Serve(
                        listener,
                        pairingCode,
                        DataDirectory,
                        CacheDir.AbsolutePath,
                        delegate(string summary) { return ResolveSyncPlan(summary, L10n.Get("platform.pda"), true); },
                        delegate(string title, string localRow, string incomingRow)
                        {
                            return ResolveSyncConflict(title, localRow, incomingRow, L10n.Get("platform.pda"), true);
                        },
                        delegate(TcpClient client)
                        {
                            acceptedClient = client;
                            syncClient = client;
                        });
                    RunOnUiThread(delegate
                    {
                        if (!IsFinishing)
                        {
                            if (error == null)
                            {
                                RefreshRecords();
                                SyncJournal journal = ExpenseSyncService.LastJournal;
                                if (journal != null)
                                    ShowMessage((L10n.Get("msg.pda_sync_complete") + "\n\n") +
                                        journal.FormatSummary());
                                else
                                    ShowMessage(L10n.Get("msg.pda_sync_complete"));
                            }
                            else
                                ShowMessage((L10n.Get("error.sync_failed_colon") + " ") + error);
                        }
                    });
                }
                catch (Exception ex)
                {
                    if (syncActive)
                        RunOnUiThread(delegate
                        {
                            if (!IsFinishing)
                                ShowMessage((L10n.Get("error.sync_failed_colon") + " ") + ex.Message);
                        });
                }
                finally
                {
                    if (Object.ReferenceEquals(syncClient, acceptedClient))
                        syncClient = null;
                    if (Object.ReferenceEquals(syncListener, listener))
                    {
                        syncListener = null;
                        syncActive = false;
                        RunOnUiThread(delegate
                        {
                            if (syncButton != null)
                            {
                                syncButton.Text = L10n.Get("sync.wifi_title");
                                if (dataMenuButton != null)
                                    dataMenuButton.Text = L10n.Get("menu.data");
                                SetDataActionsEnabled(IsCompleteData(DataDirectory));
                                UpdateRecordActions();
                            }
                        });
                    }
                }
            });
        }
        catch (Exception ex)
        {
            StopWiFiSync();
            ShowMessage((L10n.Get("error.start_sync_failed") + " ") + ex.Message);
        }
    }

    private SyncPlan ResolveSyncPlan(string summary, string remoteName, bool requireSyncActive)
    {
        SyncPlan plan = new SyncPlan { Cancelled = true };
        using (ManualResetEventSlim completed = new ManualResetEventSlim(false))
        {
            try
            {
                RunOnUiThread(delegate
                {
                    if (IsFinishing)
                    {
                        completed.Set();
                        return;
                    }

                    LinearLayout root = new LinearLayout(this);
                    root.Orientation = Orientation.Vertical;
                    root.SetPadding(40, 24, 40, 8);

                    TextView summaryView = new TextView(this);
                    summaryView.Text = summary;
                    summaryView.SetTextIsSelectable(true);
                    ScrollView scroll = new ScrollView(this);
                    scroll.AddView(summaryView);
                    root.AddView(scroll, new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MatchParent,
                        (int)(280 * Resources.DisplayMetrics.Density)));

                    CheckBox addBox = new CheckBox(this);
                    addBox.Text = (L10n.Get("sync.add_only_on") + " ") + remoteName;
                    addBox.Checked = true;
                    root.AddView(addBox);

                    CheckBox removeBox = new CheckBox(this);
                    removeBox.Text = (L10n.Get("sync.delete_local_missing_on") + " ") + remoteName;
                    removeBox.Checked = false;
                    root.AddView(removeBox);

                    TextView conflictLabel = new TextView(this);
                    conflictLabel.Text = L10n.Get("sync.if_same_differs");
                    conflictLabel.SetPadding(0, 16, 0, 0);
                    root.AddView(conflictLabel);

                    RadioGroup conflictGroup = new RadioGroup(this);
                    RadioButton preferLocal = new RadioButton(this);
                    preferLocal.Id = View.GenerateViewId();
                    preferLocal.Text = L10n.Get("sync.keep_android_version");
                    RadioButton preferRemote = new RadioButton(this);
                    preferRemote.Id = View.GenerateViewId();
                    preferRemote.Text = (L10n.Get("sync.take_version_from") + " ") + remoteName;
                    RadioButton askEach = new RadioButton(this);
                    askEach.Id = View.GenerateViewId();
                    askEach.Text = L10n.Get("sync.ask_per_record");
                    conflictGroup.AddView(preferLocal);
                    conflictGroup.AddView(preferRemote);
                    conflictGroup.AddView(askEach);
                    preferLocal.Checked = true;
                    root.AddView(conflictGroup);

                    new AlertDialog.Builder(this)
                        .SetTitle((L10n.Get("label.preview_colon") + " ") + remoteName)
                        .SetView(root)
                        .SetCancelable(false)
                        .SetPositiveButton(L10n.Get("button.apply"), delegate
                        {
                            plan.Cancelled = false;
                            plan.AddIncoming = addBox.Checked;
                            plan.RemoveLocalOnly = removeBox.Checked;
                            if (preferRemote.Checked)
                                plan.ConflictPolicy = SyncConflictPolicy.PreferIncoming;
                            else if (askEach.Checked)
                                plan.ConflictPolicy = SyncConflictPolicy.AskEach;
                            else
                                plan.ConflictPolicy = SyncConflictPolicy.PreferLocal;
                            completed.Set();
                        })
                        .SetNegativeButton(L10n.Get("button.cancel"), delegate
                        {
                            plan.Cancelled = true;
                            completed.Set();
                        })
                        .Show();
                });
            }
            catch
            {
                completed.Set();
                throw;
            }

            while (!completed.Wait(1000))
            {
                if (requireSyncActive && !syncActive)
                {
                    plan.Cancelled = true;
                    return plan;
                }
            }
        }
        return plan;
    }

    private SyncConflictChoice ResolveSyncConflict(
        string title, string localRow, string incomingRow, string remoteName, bool requireSyncActive)
    {
        SyncConflictChoice choice = SyncConflictChoice.Cancel;
        using (ManualResetEventSlim completed = new ManualResetEventSlim(false))
        {
            try
            {
                RunOnUiThread(delegate
                {
                    if (IsFinishing)
                    {
                        completed.Set();
                        return;
                    }
                    new AlertDialog.Builder(this)
                        .SetTitle((L10n.Get("sync.conflict_colon") + " ") + title)
                        .SetMessage((L10n.Get("sync.on_android_colon") + "\n") + localRow + ("\n\n" + L10n.Get("common.on_preposition") + " ") + remoteName + ":\n" +
                            incomingRow + ("\n\n" + L10n.Get("sync.which_record_to_keep")))
                        .SetCancelable(false)
                        .SetNegativeButton(L10n.Get("sync.keep_android"), delegate
                        {
                            choice = SyncConflictChoice.KeepLocal;
                            completed.Set();
                        })
                        .SetPositiveButton((L10n.Get("button.keep") + " ") + remoteName, delegate
                        {
                            choice = SyncConflictChoice.KeepIncoming;
                            completed.Set();
                        })
                        .SetNeutralButton(L10n.Get("button.cancel_sync"), delegate
                        {
                            choice = SyncConflictChoice.Cancel;
                            completed.Set();
                        })
                        .Show();
                });
            }
            catch
            {
                completed.Set();
                throw;
            }
            while (!completed.Wait(1000))
            {
                if (requireSyncActive && !syncActive)
                    return SyncConflictChoice.Cancel;
            }
        }
        return choice;
    }

    private void StopWiFiSync()
    {
        TcpListener listener = syncListener;
        TcpClient client = syncClient;
        syncListener = null;
        syncClient = null;
        syncActive = false;
        if (listener != null)
            listener.Stop();
        if (client != null)
            client.Close();
        if (syncButton != null)
        {
            syncButton.Text = L10n.Get("sync.wifi_title");
            if (dataMenuButton != null)
                dataMenuButton.Text = L10n.Get("menu.data");
            SetDataActionsEnabled(IsCompleteData(DataDirectory));
            UpdateRecordActions();
        }
    }

    private string GetSelectedFile()
    {
        if (selectedSection == Fuel)
            return "fuel.dat";
        if (selectedSection == Repairs)
            return "repairs.dat";
        if (selectedSection == Expenses)
            return ExpenseDataCodec.ExpensesFile;
        return "utilities.dat";
    }

    private string FormatRow(string[] row)
    {
        if (selectedSection == Fuel && row.Length >= 5)
        {
            string formatted = row[0] + " · " + row[2] + (" " + L10n.Get("unit.km_dot") + " ") + row[3] +
                (" " + L10n.Get("unit.liters_dot") + " ") + row[4] + " " + Cur();
            string station = ExpenseDataCodec.GetStation(row);
            if (station.Length > 0)
                formatted += " · " + station;
            string purpose = ExpenseDataCodec.GetPurpose(row);
            if (purpose.Length > 0)
                formatted += " · " + purpose;
            decimal consumption = FuelConsumption(row);
            if (consumption > 0)
                formatted += " · " + consumption.ToString("0.0", CultureInfo.InvariantCulture) + (" " + L10n.Get("unit.l_per_100_short"));
            return formatted;
        }
        if (selectedSection == Repairs && row.Length >= 9)
        {
            string mileage = row[2].Length > 0 ? " · " + row[2] + (" " + L10n.Get("unit.km")) : "";
            return row[0] + mileage + " · " + row[3] + ": " + ShortText(row[4], 22) +
                " · " + row[7] + " " + Cur();
        }
        if (selectedSection == Expenses)
        {
            string[] content = RecordIds.StripId(row);
            if (content.Length >= 5)
                return content[0] + " · " + content[2] + ": " + ShortText(content[3], 22) +
                    " · " + content[4] + " " + Cur();
        }
        if (selectedSection == Utilities && row.Length >= 8)
            return row[0] + (" " + L10n.Get("status.total") + " ") + ExpenseDataCodec.Sum(row, 1)
                .ToString("0.##", CultureInfo.InvariantCulture) + " " + Cur();
        return String.Join(" | ", row);
    }

    private decimal FuelConsumption(string[] row)
    {
        // л/100км только между полными баками; частичная заправка не замыкает интервал.
        if (!ExpenseDataCodec.IsFullTank(row))
            return 0;
        decimal mileage;
        if (!TryNumber(row[2], out mileage))
            return 0;
        int index = allRows.IndexOf(row);
        for (int i = index - 1; i >= 0; i--)
        {
            if (allRows[i][1] != row[1])
                continue;
            if (!ExpenseDataCodec.IsFullTank(allRows[i]))
                continue;
            decimal previousMileage;
            decimal liters;
            if (TryNumber(allRows[i][2], out previousMileage) &&
                TryNumber(row[3], out liters) && mileage > previousMileage)
                return liters * 100 / (mileage - previousMileage);
        }
        return 0;
    }

    private string FormatDetails(string[] row)
    {
        if (selectedSection == Fuel && row.Length >= 5)
        {
            StringBuilder details = new StringBuilder();
            details.Append((L10n.Get("label.car_colon") + " ")).Append(row[1])
                .Append(("\n" + L10n.Get("label.date_colon") + " ")).Append(row[0])
                .Append(("\n" + L10n.Get("label.odometer_colon") + " ")).Append(row[2]).Append((" " + L10n.Get("unit.km")))
                .Append(("\n" + L10n.Get("label.liters_colon") + " ")).Append(row[3])
                .Append(("\n" + L10n.Get("label.cost_colon") + " ")).Append(row[4]).Append(" " + Cur());
            string storedPrice = FieldAt(row, 5).Trim();
            decimal liters;
            decimal cost;
            if (storedPrice.Length > 0)
                details.Append(("\n" + L10n.Get("label.price_per_liter_colon") + " ")).Append(storedPrice).Append(" " + Cur());
            else if (TryNumber(row[3], out liters) && liters > 0 && TryNumber(row[4], out cost))
                details.Append(("\n" + L10n.Get("label.price_per_liter_colon") + " "))
                    .Append((cost / liters).ToString("0.00", CultureInfo.InvariantCulture))
                    .Append(" " + Cur());
            string station = ExpenseDataCodec.GetStation(row);
            if (station.Length > 0)
                details.Append(("\n" + L10n.Get("label.station_colon") + " ")).Append(station);
            string comment = FieldAt(row, 6).Trim();
            if (comment.Length > 0)
                details.Append(("\n" + L10n.Get("label.comment_colon") + " ")).Append(comment);
            details.Append(("\n" + L10n.Get("label.full_tank_colon") + " ")).Append(ExpenseDataCodec.IsFullTank(row) ? L10n.Get("common.yes_lower") : L10n.Get("common.no_lower"));
            string purpose = ExpenseDataCodec.GetPurpose(row);
            if (purpose.Length > 0)
                details.Append(("\n" + L10n.Get("label.purpose_colon") + " ")).Append(purpose);
            decimal consumption = FuelConsumption(row);
            if (consumption > 0)
                details.Append(("\n" + L10n.Get("report.consumption_colon") + " "))
                    .Append(consumption.ToString("0.0", CultureInfo.InvariantCulture))
                    .Append((" " + L10n.Get("unit.l_per_100km")));
            return details.ToString();
        }
        if (selectedSection == Repairs && row.Length >= 9)
        {
            StringBuilder details = new StringBuilder();
            details.Append((L10n.Get("label.car_colon") + " ")).Append(row[1])
                .Append(("\n" + L10n.Get("label.date_colon") + " ")).Append(row[0])
                .Append(("\n" + L10n.Get("label.odometer_colon") + " ")).Append(row[2].Length > 0 ? row[2] + (" " + L10n.Get("unit.km")) : "—")
                .Append(("\n" + L10n.Get("label.category_colon") + " ")).Append(row[3])
                .Append(("\n" + L10n.Get("label.product_colon") + " ")).Append(row[4])
                .Append(("\n" + L10n.Get("label.item_name_colon") + " ")).Append(row[5].Length > 0 ? row[5] : "—")
                .Append(("\n" + L10n.Get("label.quantity_colon") + " ")).Append(row[6])
                .Append(("\n" + L10n.Get("label.amount_colon") + " ")).Append(row[7]).Append(" " + Cur())
                .Append(("\n" + L10n.Get("label.comment_colon") + " ")).Append(row[8].Length > 0 ? row[8] : "—");
            return details.ToString();
        }
        if (selectedSection == Utilities && row.Length >= 8)
        {
            StringBuilder details = new StringBuilder((L10n.Get("label.month_colon") + " "));
            details.Append(row[0]);
            for (int i = 0; i < ExpenseDataStore.UtilityLabels.Length; i++)
            {
                string amount = row[i + 1];
                if (amount == null || amount.Trim().Length == 0 || amount == "0")
                    continue;
                details.Append("\n").Append(ExpenseDataStore.GetUtilityLabel(i))
                    .Append(": ").Append(amount).Append(" " + Cur());
            }
            details.Append(("\n" + L10n.Get("report.subtotal_colon") + " "))
                .Append(ExpenseDataCodec.Sum(row, 1).ToString("0.##", CultureInfo.InvariantCulture))
                .Append(" " + Cur());
            return details.ToString();
        }
        return String.Join(" | ", row);
    }

    private void RecordSelected(object sender, AdapterView.ItemClickEventArgs e)
    {
        if (e.Position < 0 || e.Position >= visibleRows.Count)
            return;
        selectedRecordPosition = e.Position;
        UpdateRecordActions();
        string[] row = visibleRows[e.Position];
        int sourceIndex = visibleIndexes[e.Position];
        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("label.record"))
            .SetMessage(FormatDetails(row))
            .SetPositiveButton(L10n.Get("button.close"), delegate { })
            .SetNeutralButton(L10n.Get("button.edit"), delegate { ShowRecordEditor(true, row, sourceIndex); })
            .SetNegativeButton(L10n.Get("button.delete"), delegate { ConfirmDelete(row, sourceIndex); })
            .Show();
    }

    private void AddRecord()
    {
        if (selectedSection == Fuel || selectedSection == Repairs || selectedSection == Expenses)
        {
            string type = selectedSection == Fuel ? RecordTemplates.TypeFuel
                : selectedSection == Repairs ? RecordTemplates.TypeRepair
                : RecordTemplates.TypeExpense;
            List<RecordTemplate> templates = RecordTemplates.ListByType(DataDirectory, type);
            if (templates.Count > 0)
            {
                List<string> labels = new List<string>();
                labels.Add(L10n.Get("button.blank_form"));
                labels.Add(L10n.Get("button.from_template_ellipsis"));
                new AlertDialog.Builder(this)
                    .SetTitle(L10n.Get("button.add"))
                    .SetItems(labels.ToArray(), delegate(object sender, DialogClickEventArgs e)
                    {
                        if (e.Which == 1)
                            AddRecordFromTemplate(type);
                        else
                            AddRecordBlank();
                    })
                    .SetNegativeButton(L10n.Get("button.cancel"), delegate { })
                    .Show();
                return;
            }
        }
        AddRecordBlank();
    }

    private void AddRecordBlank()
    {
        if (selectedSection == Fuel)
        {
            string lastMileage = LastCarMileage(currentCar);
            ShowRecordEditor(false, new string[]
            {
                DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                currentCar, lastMileage, "", "", LastFuelPricePerLiter(currentCar), "", "1"
            }, -1);
        }
        else if (selectedSection == Repairs)
        {
            List<string> categories = ExpenseDataStore.LoadRepairCategories(DataDirectory);
            if (categories.Count == 0)
            {
                ShowMessage(L10n.Get("error.add_repair_category_first"));
                ManageRepairCategories();
                return;
            }
            string lastMileage = LastCarMileage(currentCar);
            ShowRecordEditor(false, new string[]
            {
                DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                currentCar, lastMileage, categories[0], "", "", "1", "", ""
            }, -1);
        }
        else if (selectedSection == Expenses)
        {
            List<string> categories = ExpenseDataStore.LoadExpenseCategories(DataDirectory);
            if (categories.Count == 0)
            {
                ShowMessage(L10n.Get("error.add_expense_category_first"));
                ManageExpenseCategories();
                return;
            }
            ShowRecordEditor(false, new string[]
            {
                DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                currentCar, categories[0], "", "", ""
            }, -1);
        }
        else
        {
            ShowRecordEditor(false, new string[]
            {
                DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                "", "", "", "", "", "", ""
            }, -1);
        }
    }

    private void AddRecordFromTemplate(string type)
    {
        List<RecordTemplate> templates = RecordTemplates.ListByType(DataDirectory, type);
        if (templates.Count == 0)
        {
            ShowMessage(L10n.Get("msg.no_templates_save_from_form"));
            AddRecordBlank();
            return;
        }
        string[] labels = new string[templates.Count];
        for (int i = 0; i < templates.Count; i++)
            labels[i] = templates[i].Name;
        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("button.apply_template"))
            .SetItems(labels, delegate(object sender, DialogClickEventArgs e)
            {
                if (e.Which < 0 || e.Which >= templates.Count)
                    return;
                RecordTemplate tpl = templates[e.Which];
                string date = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                string[] entity = RecordTemplates.BuildEntityRow(tpl, date, currentCar);
                if (type == RecordTemplates.TypeFuel)
                {
                    // Ensure price/fullTank slots exist for editor.
                    List<string> fields = new List<string>(entity);
                    while (fields.Count < 8)
                        fields.Add("");
                    if (fields[7].Length == 0)
                        fields[7] = "1";
                    if (fields[5].Length == 0)
                        fields[5] = LastFuelPricePerLiter(currentCar);
                    if (fields[2].Length == 0)
                        fields[2] = LastCarMileage(currentCar);
                    entity = fields.ToArray();
                }
                else if (type == RecordTemplates.TypeRepair)
                {
                    List<string> fields = new List<string>(entity);
                    while (fields.Count < 9)
                        fields.Add("");
                    if (fields[2].Length == 0)
                        fields[2] = LastCarMileage(currentCar);
                    entity = fields.ToArray();
                }
                ShowRecordEditor(false, entity, -1);
            })
            .SetNegativeButton(L10n.Get("button.cancel"), delegate { })
            .Show();
    }

    private void SaveTemplateFromValues(string type, string[] values)
    {
        EditText nameBox = new EditText(this);
        nameBox.Hint = L10n.Get("label.template_name");
        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("button.save_template"))
            .SetView(nameBox)
            .SetPositiveButton(L10n.Get("button.save"), delegate
            {
                try
                {
                    RecordTemplate item = new RecordTemplate();
                    item.Type = type;
                    item.Name = (nameBox.Text ?? "").Trim();
                    item.Payload = RecordTemplates.PayloadFromEntity(type, values);
                    RecordTemplates.Upsert(DataDirectory, item);
                    ShowMessage((L10n.Get("msg.template_saved_colon") + " ") + item.Name);
                }
                catch (Exception ex)
                {
                    ShowMessage(ex.Message);
                }
            })
            .SetNegativeButton(L10n.Get("button.cancel"), delegate { })
            .Show();
    }

    private void ShowTripReport()
    {
        List<string[]> trips = ExpenseDataStore.LoadOptional(
            DataDirectory, ExpenseDataCodec.TripsFile);
        decimal personal = 0;
        decimal work = 0;
        int count = 0;
        for (int i = 0; i < trips.Count; i++)
        {
            string[] content = RecordIds.StripId(trips[i]);
            if (content.Length < 7 ||
                !String.Equals(content[1] ?? "", currentCar, StringComparison.OrdinalIgnoreCase))
                continue;
            decimal startKm;
            decimal endKm;
            if (!TryNumber(content[2], out startKm) || !TryNumber(content[3], out endKm) ||
                endKm < startKm)
                continue;
            decimal distance = endKm - startKm;
            if (String.Equals((content[4] ?? "").Trim(), "Работа",
                StringComparison.OrdinalIgnoreCase))
                work += distance;
            else
                personal += distance;
            count++;
        }
        ShowMessage((L10n.Get("trips.report_title_dot") + " ") + currentCar +
            ("\n" + L10n.Get("label.quantity_colon") + " ") + count +
            ("\n" + L10n.Get("report.personal_colon") + " ") + personal.ToString("0.##", CultureInfo.InvariantCulture) + (" " + L10n.Get("unit.km")) +
            ("\n" + L10n.Get("report.work_colon") + " ") + work.ToString("0.##", CultureInfo.InvariantCulture) + (" " + L10n.Get("unit.km")) +
            ("\n" + L10n.Get("report.total_colon") + " ") + (personal + work).ToString("0.##", CultureInfo.InvariantCulture) + (" " + L10n.Get("unit.km")));
    }

    private void ShowTripEntry()
    {
        if (!IsCompleteData(DataDirectory))
        {
            ShowMessage(L10n.Get("error.import_or_create_first"));
            return;
        }
        List<string> cars = ExpenseDataStore.LoadCarNames(DataDirectory);
        if (cars.Count == 0)
        {
            ShowMessage(L10n.Get("error.no_cars"));
            return;
        }
        string car = ToSettings.ResolvePreferredCar(DataDirectory, cars, currentCar);
        List<EditorField> fields = new List<EditorField>();
        fields.Add(EditorField.Date(L10n.Get("label.date"), DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), true));
        fields.Add(EditorField.Option(L10n.Get("label.vehicle"), car, cars.ToArray(), true));
        fields.Add(EditorField.Number(L10n.Get("label.odometer_start_km"), "", true));
        fields.Add(EditorField.Number(L10n.Get("label.odometer_end_km"), "", true));
        fields.Add(EditorField.Option(L10n.Get("label.purpose_alt"), "Личный", new string[] { "Личный", "Работа" }, false));
        fields.Add(EditorField.Text(L10n.Get("label.driver"), "", false));
        fields.Add(EditorField.Text(L10n.Get("label.comment"), "", false));

        RenderEditor(L10n.Get("title.new_trip"), fields,
            delegate(string[] values)
            {
                if (values == null || values.Length < 7)
                    throw new InvalidOperationException(L10n.Get("error.fill_trip_required"));
                decimal startKm;
                decimal endKm;
                if (!TryNumber(values[2], out startKm) || startKm < 0 ||
                    !TryNumber(values[3], out endKm) || endKm < startKm)
                    throw new InvalidOperationException(
                        L10n.Get("error.trip_odometer_range"));
                string[] trip = new string[]
                {
                    values[0], values[1], values[2], values[3],
                    values[4], values[5], values[6]
                };
                ExpenseDataStore.AddRecord(DataDirectory, ExpenseDataCodec.TripsFile, trip);
                SetCurrentCar(trip[1]);
                RefreshRecords();
                ShowMessage((L10n.Get("msg.trip_saved") + " ") + (endKm - startKm).ToString("0.##",
                    CultureInfo.InvariantCulture) + (" " + L10n.Get("unit.km")));
            }, false);
    }

    private void ShowQuickFuelEntry()
    {
        if (!IsCompleteData(DataDirectory))
        {
            ShowMessage(L10n.Get("error.import_or_create_first"));
            return;
        }
        List<string> cars = ExpenseDataStore.LoadCarNames(DataDirectory);
        if (cars.Count == 0)
        {
            ShowMessage(L10n.Get("error.no_cars_add"));
            return;
        }
        string car = ToSettings.ResolvePreferredCar(DataDirectory, cars, currentCar);
        string lastKm = LastCarMileage(car);
        string lastPrice = LastFuelPricePerLiter(car);
        bool lastFull = LastFuelFullTank(car);
        List<EditorField> fields = new List<EditorField>();
        fields.Add(EditorField.Option(L10n.Get("label.vehicle"), car, cars.ToArray(), true));
        fields.Add(EditorField.Number(
            lastKm.Length > 0 ? L10n.Get("label.odometer_last_record") : L10n.Get("label.odometer_km"),
            lastKm, true));
        fields.Add(EditorField.Number(L10n.Get("label.liters"), "", true));
        fields.Add(EditorField.Number((L10n.Get("label.cost_comma") + " ") + Cur(), "", true));
        fields.Add(EditorField.Number((L10n.Get("label.price_per_liter_comma") + " ") + Cur(), lastPrice, false));
        fields.Add(EditorField.Check(L10n.Get("label.full_tank"), lastFull));

        Func<string[], string> mileageConfirm = delegate(string[] values)
        {
            // Quick form: car|km|liters|cost|price|fullTank — map to fuel layout for check.
            string[] fuelLike = new string[]
            {
                DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                values[0], values[1], values[2], values[3]
            };
            return MileageWarningForSave(fuelLike, "fuel.dat", -1);
        };

        RenderEditor(L10n.Get("button.quick_fuel"), fields,
            delegate(string[] values)
            {
                if (values == null || values.Length < 6)
                    throw new InvalidOperationException(L10n.Get("error.fill_all_required"));
                decimal km;
                decimal liters;
                decimal cost;
                decimal price;
                if (!TryNumber(values[1], out km) || km < 0 ||
                    !TryNumber(values[2], out liters) || liters <= 0 ||
                    !TryNumber(values[3], out cost) || cost <= 0 ||
                    ((!String.IsNullOrEmpty(values[4])) &&
                        (!TryNumber(values[4], out price) || price <= 0)))
                    throw new InvalidOperationException(
                        L10n.Get("error.check_fuel_values"));
                string[] fuel = BuildQuickFuelRow(values);
                fuel = NormalizeFuelValues(fuel);
                ExpenseDataStore.AddRecord(DataDirectory, "fuel.dat", fuel);
                SetCurrentCar(fuel[1]);
                selectedSection = Fuel;
                GetSharedPreferences("settings", FileCreationMode.Private)
                    .Edit().PutString("selectedSection", selectedSection).Apply();
                UpdateSectionButtons();
                RefreshRecords();
                RefreshServiceNotifications();
                string tankHint = CarsMeta.TankNearHint(DataDirectory, fuel[1], fuel[3]);
                if (tankHint != null)
                    ShowMessage(tankHint);
            }, false, null, mileageConfirm, null);
        // liters/cost/price indices differ from full fuel editor — wire after fields bind.
        WireQuickFuelPrice(fields);
    }

    /// <summary>car|km|liters|cost|price|fullTank → full fuel row (today, empty comment/purpose).</summary>
    private static string[] BuildQuickFuelRow(string[] values)
    {
        string date = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string fullTank = values != null && values.Length > 5
            ? ExpenseDataCodec.NormalizeFullTankFlag(values[5])
            : "1";
        return new string[]
        {
            date,
            values != null && values.Length > 0 ? (values[0] ?? "") : "",
            values != null && values.Length > 1 ? (values[1] ?? "") : "",
            values != null && values.Length > 2 ? (values[2] ?? "") : "",
            values != null && values.Length > 3 ? (values[3] ?? "") : "",
            values != null && values.Length > 4 ? (values[4] ?? "") : "",
            "",
            fullTank,
            ""
        };
    }

    private void WireQuickFuelPrice(List<EditorField> fields)
    {
        if (fields == null || fields.Count < 5)
            return;
        // liters=2, cost=3, price=4
        if (fields[2].TextInput == null || fields[3].TextInput == null || fields[4].TextInput == null)
            return;
        WireFuelPriceAutoCalc(fields[2], fields[3], fields[4]);
    }

    private bool LastFuelFullTank(string car)
    {
        List<string[]> fuels = ExpenseDataStore.Load(DataDirectory, "fuel.dat");
        string bestDate = "";
        int bestIndex = -1;
        bool full = true;
        for (int i = 0; i < fuels.Count; i++)
        {
            if (fuels[i].Length < 2 || fuels[i][1] != car)
                continue;
            string date = fuels[i][0] ?? "";
            if (bestIndex < 0 ||
                String.Compare(date, bestDate, StringComparison.Ordinal) > 0 ||
                (date == bestDate && i > bestIndex))
            {
                bestDate = date;
                bestIndex = i;
                full = ExpenseDataCodec.IsFullTank(fuels[i]);
            }
        }
        return full;
    }

    private void EditDefaultCar()
    {
        if (!IsCompleteData(DataDirectory))
        {
            ShowMessage(L10n.Get("error.import_or_create_first"));
            return;
        }
        List<string> cars = ExpenseDataStore.LoadCarNames(DataDirectory);
        if (cars.Count == 0)
        {
            ShowMessage(L10n.Get("error.no_cars"));
            return;
        }
        string current = ToSettings.GetDefaultCar(DataDirectory);
        List<string> labels = new List<string>();
        labels.Add(L10n.Get("common.not_set"));
        labels.AddRange(cars);
        int selected = 0;
        for (int i = 0; i < cars.Count; i++)
            if (String.Equals(cars[i], current, StringComparison.OrdinalIgnoreCase))
                selected = i + 1;
        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("label.default_car_full"))
            .SetSingleChoiceItems(labels.ToArray(), selected, delegate(object sender, DialogClickEventArgs e)
            {
                selected = e.Which;
            })
            .SetPositiveButton(L10n.Get("button.save"), delegate
            {
                string name = selected <= 0 ? "" : cars[selected - 1];
                ToSettings.SetDefaultCar(DataDirectory, name);
                if (name.Length > 0)
                {
                    SetCurrentCar(name);
                    RefreshRecords();
                }
                ShowMessage(name.Length == 0
                    ? L10n.Get("msg.default_car_cleared")
                    : (L10n.Get("msg.default_car_set") + " ") + name);
            })
            .SetNegativeButton(L10n.Get("button.cancel"), delegate { })
            .Show();
    }

    private void EditSelectedRecord()
    {
        int index = selectedRecordPosition;
        if (index < 0 || index >= visibleRows.Count)
        {
            ShowMessage(L10n.Get("hint.tap_record_to_select"));
            return;
        }
        ShowRecordEditor(true, visibleRows[index], visibleIndexes[index]);
    }

    private void DeleteSelectedRecord()
    {
        int index = selectedRecordPosition;
        if (index < 0 || index >= visibleRows.Count)
        {
            ShowMessage(L10n.Get("hint.tap_record_to_select"));
            return;
        }
        ConfirmDelete(visibleRows[index], visibleIndexes[index]);
    }

    private void ShowRecordEditor(bool editing, string[] row, int sourceIndex)
    {
        string fileName = GetSelectedFile();
        bool supportReceipt = selectedSection == Fuel || selectedSection == Repairs ||
            selectedSection == Expenses;
        string receiptId = null;
        if (supportReceipt)
        {
            row = RecordIds.EnsureId(row, RecordIds.PrefixForFile(fileName));
            receiptId = RecordIds.GetId(row);
        }
        List<EditorField> fields = new List<EditorField>();
        if (selectedSection == Fuel)
        {
            fields.Add(EditorField.Date(L10n.Get("label.date"), row[0], true));
            fields.Add(EditorField.Option(L10n.Get("label.vehicle"), row[1],
                ExpenseDataStore.LoadCarNames(DataDirectory).ToArray(), true));
            fields.Add(EditorField.Number(
                !editing && row[2].Length > 0
                    ? L10n.Get("label.odometer_last_record")
                    : L10n.Get("label.odometer_km"),
                row[2], true));
            fields.Add(EditorField.Number(L10n.Get("label.liters"), row[3], true));
            fields.Add(EditorField.Number((L10n.Get("label.cost_comma") + " ") + Cur(), row[4], true));
            fields.Add(EditorField.Number((L10n.Get("label.price_per_liter_comma") + " ") + Cur(), FieldAt(row, 5), false));
            fields.Add(EditorField.Text(L10n.Get("label.comment"), FieldAt(row, 6), false));
            string fullTank = FieldAt(row, 7);
            if (fullTank.Length == 0)
                fullTank = "1";
            fields.Add(EditorField.Check(L10n.Get("label.full_tank"), fullTank != "0"));
            string purpose = ExpenseDataCodec.GetPurpose(row);
            fields.Add(EditorField.Option(L10n.Get("label.purpose_hint"), purpose,
                PurposeFieldOptions(purpose), false));
            string station = ExpenseDataCodec.GetStation(row);
            fields.Add(EditorField.Suggest(L10n.Get("label.station_brand"), station,
                StationFieldOptions(station), false));
        }
        else if (selectedSection == Repairs)
        {
            fields.Add(EditorField.Date(L10n.Get("label.date"), row[0], true));
            fields.Add(EditorField.Option(L10n.Get("label.vehicle"), row[1],
                ExpenseDataStore.LoadCarNames(DataDirectory).ToArray(), true));
            fields.Add(EditorField.Number(
                !editing && row[2].Length > 0
                    ? L10n.Get("label.odometer_last_record")
                    : L10n.Get("label.odometer_km"),
                row[2], false));
            List<string> categories = ExpenseDataStore.LoadRepairCategories(DataDirectory);
            fields.Add(EditorField.Option(L10n.Get("label.category"), row[3],
                categories.ToArray(), true));
            fields.Add(EditorField.Text(L10n.Get("label.product_part"), row[4], true));
            fields.Add(EditorField.Text(L10n.Get("label.item_name"), row[5], false));
            fields.Add(EditorField.Number(L10n.Get("label.quantity"), row[6], true));
            fields.Add(EditorField.Number((L10n.Get("label.amount_comma") + " ") + Cur(), row[7], true));
            fields.Add(EditorField.Text(L10n.Get("label.comment"), row[8], false, true));
        }
        else if (selectedSection == Expenses)
        {
            string[] content = RecordIds.StripId(row);
            fields.Add(EditorField.Date(L10n.Get("label.date"), FieldAt(content, 0), true));
            fields.Add(EditorField.Option(L10n.Get("label.vehicle"), FieldAt(content, 1),
                ExpenseDataStore.LoadCarNames(DataDirectory).ToArray(), true));
            List<string> categories = ExpenseDataStore.LoadExpenseCategories(DataDirectory);
            fields.Add(EditorField.Option(L10n.Get("label.category"), FieldAt(content, 2),
                categories.ToArray(), true));
            fields.Add(EditorField.Text(L10n.Get("label.name"), FieldAt(content, 3), true));
            fields.Add(EditorField.Number((L10n.Get("label.amount_comma") + " ") + Cur(), FieldAt(content, 4), true));
            fields.Add(EditorField.Text(L10n.Get("label.comment"), FieldAt(content, 5), false, true));
        }
        else
        {
            fields.Add(EditorField.Month(L10n.Get("label.month"), row[0], true));
            for (int i = 0; i < ExpenseDataStore.UtilityLabels.Length; i++)
                fields.Add(EditorField.Number(
                    ExpenseDataStore.GetUtilityLabel(i) + ", " + Cur(), row[i + 1], false));
        }

        Func<string[], string> mileageConfirm = null;
        if (selectedSection == Fuel || selectedSection == Repairs)
        {
            mileageConfirm = delegate(string[] values)
            {
                return MileageWarningForSave(values, fileName, editing ? sourceIndex : -1);
            };
        }

        string templateType = selectedSection == Fuel ? RecordTemplates.TypeFuel
            : selectedSection == Repairs ? RecordTemplates.TypeRepair
            : selectedSection == Expenses ? RecordTemplates.TypeExpense
            : null;
        RenderEditor(editing ? L10n.Get("title.edit_record") : L10n.Get("title.new_record"), fields,
            delegate(string[] values)
            {
                if (selectedSection == Utilities && !HasUtilityAmount(values))
                    throw new InvalidOperationException(L10n.Get("error.enter_at_least_one_amount"));
                if (selectedSection == Fuel)
                    values = NormalizeFuelValues(values);
                if (supportReceipt && !String.IsNullOrEmpty(receiptId))
                    values = RecordIds.WithId(RecordIds.StripId(values), receiptId);
                if (editing)
                    ExpenseDataStore.UpdateRecord(DataDirectory, fileName, sourceIndex, values);
                else
                    ExpenseDataStore.AddRecord(DataDirectory, fileName, values);

                if (selectedSection != Utilities)
                    SetCurrentCar(values[1]);
                RefreshRecords();
                RefreshServiceNotifications();
                if (selectedSection == Fuel && values.Length > 3)
                {
                    string tankHint = CarsMeta.TankNearHint(DataDirectory, values[1], values[3]);
                    if (tankHint != null)
                        ShowMessage(tankHint);
                }
            }, selectedSection == Fuel, receiptId, mileageConfirm, templateType);
    }

    private static string FieldAt(string[] row, int index)
    {
        string[] content = RecordIds.StripId(row);
        return content != null && index < content.Length ? (content[index] ?? "") : "";
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
            Array.Copy(values, row, 10);
        }
        if (row.Length > 5)
        {
            string price = (row[5] ?? "").Trim();
            if (price.Length == 0)
            {
                decimal liters;
                decimal cost;
                if (TryNumber(row[3], out liters) && liters > 0 &&
                    TryNumber(row[4], out cost) && cost > 0)
                    row[5] = (cost / liters).ToString("0.##", CultureInfo.InvariantCulture);
            }
        }
        if (row.Length > 7)
            row[7] = ExpenseDataCodec.NormalizeFullTankFlag(row[7]);
        if (row.Length > 8)
            row[8] = ExpenseDataCodec.NormalizePurpose(row[8]);
        if (row.Length > 9)
            row[9] = ExpenseDataCodec.NormalizeStation(row[9]);
        int length = row.Length;
        while (length > 5)
        {
            if ((row[length - 1] ?? "").Trim().Length > 0)
                break;
            length--;
        }
        if (length == row.Length)
            return row;
        string[] trimmed = new string[length];
        Array.Copy(row, trimmed, length);
        return trimmed;
    }

    private static string[] PurposeFieldOptions(string current)
    {
        List<string> options = new List<string> { "", "Личный", "Работа" };
        string value = ExpenseDataCodec.NormalizePurpose(current);
        if (value.Length > 0 && options.IndexOf(value) < 0)
            options.Add(value);
        return options.ToArray();
    }

    private string[] StationFieldOptions(string current)
    {
        List<string> options = new List<string>();
        string[] known = ExpenseDataCodec.CollectStationSuggestions(
            ExpenseDataStore.Load(DataDirectory, "fuel.dat"));
        for (int i = 0; i < known.Length; i++)
            options.Add(known[i]);
        string value = ExpenseDataCodec.NormalizeStation(current);
        if (value.Length > 0)
        {
            bool found = false;
            for (int i = 0; i < options.Count; i++)
            {
                if (String.Equals(options[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
                options.Insert(0, value);
        }
        return options.ToArray();
    }

    private string LastCarMileage(string car)
    {
        decimal best = LastKnownMileage(car, null, -1);
        return best < 0 ? "" : best.ToString(CultureInfo.InvariantCulture);
    }

    private string LastFuelPricePerLiter(string car)
    {
        List<string[]> fuels = ExpenseDataStore.Load(DataDirectory, "fuel.dat");
        string bestDate = "";
        string bestPrice = "";
        int bestIndex = -1;
        for (int i = 0; i < fuels.Count; i++)
        {
            if (fuels[i].Length < 6 || fuels[i][1] != car)
                continue;
            string price = (fuels[i][5] ?? "").Trim();
            if (price.Length == 0)
                continue;
            string date = fuels[i][0] ?? "";
            if (bestIndex < 0 ||
                String.Compare(date, bestDate, StringComparison.Ordinal) > 0 ||
                (date == bestDate && i > bestIndex))
            {
                bestDate = date;
                bestPrice = price;
                bestIndex = i;
            }
        }
        return bestPrice;
    }

    private string MileageWarningForSave(string[] values, string fileName, int excludeIndex)
    {
        if (values == null || values.Length < 3)
            return null;
        string car = values[1] ?? "";
        decimal newKm;
        if (!TryNumber(values[2], out newKm) || newKm < 0)
            return null;
        // Empty optional repair mileage — skip checks.
        if (fileName == "repairs.dat" && (values[2] ?? "").Trim().Length == 0)
            return null;
        decimal last = LastKnownMileage(car, fileName, excludeIndex);
        return FormatMileageWarning(newKm, last);
    }

    private decimal LastKnownMileage(string car, string excludeFile, int excludeIndex)
    {
        decimal best = -1;
        RememberLatestMileage(ExpenseDataStore.Load(DataDirectory, "fuel.dat"), car, 2,
            excludeFile == "fuel.dat" ? excludeIndex : -1, ref best);
        RememberLatestMileage(ExpenseDataStore.Load(DataDirectory, "repairs.dat"), car, 2,
            excludeFile == "repairs.dat" ? excludeIndex : -1, ref best);
        return best;
    }

    private static void RememberLatestMileage(List<string[]> rows, string car, int mileageIndex,
        int excludeIndex, ref decimal best)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (i == excludeIndex)
                continue;
            if (rows[i].Length <= mileageIndex || rows[i][1] != car)
                continue;
            decimal mileage;
            if (!TryNumber(rows[i][mileageIndex], out mileage) || mileage < 0)
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

    private void ShowGlobalSearch()
    {
        if (!IsCompleteData(DataDirectory))
        {
            ShowMessage(L10n.Get("error.load_or_create_first"));
            return;
        }
        EditText queryBox = new EditText(this);
        queryBox.Hint = L10n.Get("hint.search_query");
        queryBox.SetSingleLine(true);
        LinearLayout layout = new LinearLayout(this);
        layout.Orientation = Orientation.Vertical;
        layout.SetPadding(Dp(12), Dp(8), Dp(12), Dp(4));
        layout.AddView(queryBox, MatchWrap());
        ListView resultsList = new ListView(this);
        layout.AddView(resultsList, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, Dp(280)));

        List<SearchHit> hits = new List<SearchHit>();
        ArrayAdapter<string> adapter = new ArrayAdapter<string>(this,
            AndroidLayout.SimpleListItem1, new List<string>());
        resultsList.Adapter = adapter;

        AlertDialog dialog = new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("title.search"))
            .SetView(layout)
            .SetNegativeButton(L10n.Get("button.close"), delegate { })
            .Create();

        Action runSearch = delegate
        {
            string q = (queryBox.Text ?? "").Trim();
            hits.Clear();
            adapter.Clear();
            if (q.Length == 0)
            {
                adapter.NotifyDataSetChanged();
                return;
            }
            hits.AddRange(FindSearchHits(q));
            for (int i = 0; i < hits.Count; i++)
                adapter.Add(hits[i].Label);
            adapter.NotifyDataSetChanged();
        };
        queryBox.TextChanged += delegate { runSearch(); };
        resultsList.ItemClick += delegate(object sender, AdapterView.ItemClickEventArgs e)
        {
            if (e.Position < 0 || e.Position >= hits.Count)
                return;
            SearchHit hit = hits[e.Position];
            dialog.Dismiss();
            OpenSearchHit(hit);
        };
        dialog.Show();
    }

    private sealed class SearchHit
    {
        public string Section;
        public string Label;
        public int SourceIndex;
        public string Car;
        public string[] Row;
    }

    private List<SearchHit> FindSearchHits(string query)
    {
        string q = query.ToLowerInvariant();
        List<SearchHit> hits = new List<SearchHit>();
        CollectSearchHits(hits, Fuel, "fuel.dat", L10n.Get("label.fuel_entry"), q, delegate(string[] row)
        {
            return FieldAt(row, 6) + " " + ExpenseDataCodec.GetPurpose(row) + " " +
                ExpenseDataCodec.GetStation(row) + " " + FieldAt(row, 1);
        });
        CollectSearchHits(hits, Repairs, "repairs.dat", L10n.Get("tab.repairs"), q, delegate(string[] row)
        {
            return FieldAt(row, 3) + " " + FieldAt(row, 4) + " " + FieldAt(row, 5) + " " +
                FieldAt(row, 8) + " " + FieldAt(row, 1);
        });
        CollectSearchHits(hits, Expenses, ExpenseDataCodec.ExpensesFile, L10n.Get("tab.expenses"), q,
            delegate(string[] row)
            {
                return FieldAt(row, 2) + " " + FieldAt(row, 3) + " " + FieldAt(row, 5) + " " +
                    FieldAt(row, 1);
            });
        CollectSearchHits(hits, Utilities, "utilities.dat", L10n.Get("tab.utilities"), q, delegate(string[] row)
        {
            StringBuilder sb = new StringBuilder(FieldAt(row, 0));
            for (int i = 0; i < ExpenseDataStore.UtilityLabels.Length && i + 1 < row.Length; i++)
                sb.Append(' ').Append(ExpenseDataStore.GetUtilityLabel(i)).Append(' ').Append(row[i + 1]);
            return sb.ToString();
        });
        return hits;
    }

    private void CollectSearchHits(List<SearchHit> hits, string section, string fileName,
        string sectionLabel, string queryLower, Func<string[], string> haystack)
    {
        List<string[]> rows = ExpenseDataStore.LoadOptional(DataDirectory, fileName);
        for (int i = 0; i < rows.Count; i++)
        {
            string[] row = rows[i];
            string text = haystack(row) ?? "";
            if (text.ToLowerInvariant().IndexOf(queryLower, StringComparison.Ordinal) < 0)
                continue;
            string date = FieldAt(row, 0);
            string detail = ShortText(text.Trim(), 40);
            SearchHit hit = new SearchHit();
            hit.Section = section;
            hit.SourceIndex = i;
            hit.Car = section == Utilities ? "" : FieldAt(row, 1);
            hit.Row = row;
            hit.Label = sectionLabel + " · " + date +
                (hit.Car.Length > 0 ? " · " + hit.Car : "") + " — " + detail;
            hits.Add(hit);
        }
    }

    private void OpenSearchHit(SearchHit hit)
    {
        if (!String.IsNullOrEmpty(hit.Car))
            SetCurrentCar(hit.Car);
        SelectSection(hit.Section);
        // Select matching visible row if still present after car/section filter.
        for (int i = 0; i < visibleIndexes.Count; i++)
        {
            if (visibleIndexes[i] != hit.SourceIndex)
                continue;
            int visiblePos = i;
            int sourceIdx = visibleIndexes[i];
            string[] visibleRow = visibleRows[i];
            selectedRecordPosition = visiblePos;
            recordList.SetSelection(visiblePos);
            UpdateRecordActions();
            new AlertDialog.Builder(this)
                .SetTitle(L10n.Get("label.record"))
                .SetMessage(FormatDetails(visibleRow))
                .SetPositiveButton(L10n.Get("button.close"), delegate { })
                .SetNeutralButton(L10n.Get("button.edit"), delegate
                {
                    ShowRecordEditor(true, visibleRow, sourceIdx);
                })
                .Show();
            return;
        }
        ShowMessage(L10n.Get("msg.record_hidden_by_filter"));
    }

    private void ShowDataIntegrity()
    {
        if (!IsCompleteData(DataDirectory))
        {
            ShowMessage(L10n.Get("error.load_or_create_first"));
            return;
        }
        DataIntegrity.Report report;
        try
        {
            report = DataIntegrity.Check(DataDirectory);
        }
        catch (Exception ex)
        {
            ShowMessage((L10n.Get("error.integrity_check_failed") + " ") + ex.Message);
            return;
        }
        string text = DataIntegrity.Format(report);
        AlertDialog.Builder builder = new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("button.data_integrity"))
            .SetMessage(text)
            .SetNegativeButton(L10n.Get("button.close"), delegate { });
        if (report.CanSimpleFix)
        {
            builder.SetPositiveButton(L10n.Get("button.fix_simple"), (sender, args) =>
            {
                try
                {
                    int n = DataIntegrity.ApplySimpleFixes(DataDirectory, report);
                    ShowMessage(n > 0
                        ? (L10n.Get("msg.categories_added") + " ") + n + "."
                        : L10n.Get("integrity.nothing_to_fix"));
                }
                catch (Exception ex)
                {
                    ShowMessage((L10n.Get("error.fix_failed") + " ") + ex.Message);
                }
            });
        }
        builder.Show();
    }

    private void ShowDuplicates()
    {
        if (!IsCompleteData(DataDirectory))
        {
            ShowMessage(L10n.Get("error.load_or_create_first"));
            return;
        }
        List<DuplicateFinder.Group> groups;
        try
        {
            groups = DuplicateFinder.FindAll(DataDirectory);
        }
        catch (Exception ex)
        {
            ShowMessage((L10n.Get("error.find_duplicates_failed") + " ") + ex.Message);
            return;
        }
        if (groups.Count == 0)
        {
            ShowMessage(L10n.Get("msg.no_duplicates"));
            return;
        }

        List<string> labels = new List<string>();
        List<DuplicateFinder.Group> labelGroups = new List<DuplicateFinder.Group>();
        for (int g = 0; g < groups.Count; g++)
        {
            DuplicateFinder.Group group = groups[g];
            labels.Add(group.Title + " · " + group.Reason + " ×" + group.Indexes.Count);
            labelGroups.Add(group);
        }

        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("button.duplicates"))
            .SetItems(labels.ToArray(), (sender, args) =>
            {
                if (args.Which < 0 || args.Which >= labelGroups.Count)
                    return;
                ShowDuplicateGroup(labelGroups[args.Which]);
            })
            .SetNegativeButton(L10n.Get("button.close"), delegate { })
            .Show();
    }

    private void ShowDuplicateGroup(DuplicateFinder.Group group)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(group.Title).Append(" · ").Append(group.Reason).Append('\n');
        for (int i = 0; i < group.Rows.Count; i++)
        {
            sb.Append(i == 0 ? "\n✓ оставить: " : "\n· лишняя: ");
            sb.Append(DuplicateFinder.FormatRow(group.Rows[i]));
        }
        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("title.duplicate_group"))
            .SetMessage(sb.ToString())
            .SetPositiveButton(L10n.Get("button.delete_extra"), (sender, args) =>
            {
                try
                {
                    List<int> toDelete = new List<int>();
                    for (int i = 1; i < group.Indexes.Count; i++)
                        toDelete.Add(group.Indexes[i]);
                    toDelete.Sort();
                    for (int i = toDelete.Count - 1; i >= 0; i--)
                        ExpenseDataStore.DeleteRecord(DataDirectory, group.FileName, toDelete[i]);
                    RefreshRecords();
                    ShowMessage(L10n.Get("msg.extra_records_deleted_keep_first"));
                }
                catch (Exception ex)
                {
                    ShowMessage((L10n.Get("error.delete_failed") + " ") + ex.Message);
                }
            })
            .SetNegativeButton(L10n.Get("button.cancel"), delegate { })
            .Show();
    }

    private void ManageCars()
    {
        if (syncActive)
        {
            ShowMessage(L10n.Get("error.wait_sync_finish"));
            return;
        }
        if (!IsCompleteData(DataDirectory))
        {
            StartFromScratch();
            return;
        }
        List<string> cars = ExpenseDataStore.LoadCarNames(DataDirectory);
        AlertDialog.Builder builder = new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("tab.vehicles"))
            .SetPositiveButton(L10n.Get("button.close"), delegate { })
            .SetNeutralButton(L10n.Get("button.add"), delegate { AddCar(); });
        if (cars.Count > 0)
        {
            string[] names = cars.ToArray();
            builder.SetItems(names, delegate(object sender, DialogClickEventArgs e)
            {
                ShowCarActions(names[e.Which]);
            });
        }
        builder.Show();
    }

    private void ShowCarActions(string name)
    {
        new AlertDialog.Builder(this)
            .SetTitle(name)
            .SetItems(new string[] { L10n.Get("button.choose"), L10n.Get("title.properties"), L10n.Get("label.favorite"), L10n.Get("label.timeline"), L10n.Get("button.rename"), L10n.Get("button.delete") },
                delegate(object sender, DialogClickEventArgs e)
                {
                    if (e.Which == 0)
                    {
                        SetCurrentCar(name);
                        RefreshRecords();
                    }
                    else if (e.Which == 1)
                        EditCarPassport(name);
                    else if (e.Which == 2)
                    {
                        ToSettings.SetDefaultCar(DataDirectory, name);
                        SetCurrentCar(name);
                        ShowMessage((L10n.Get("label.favorite_car_colon") + " ") + name);
                        RefreshRecords();
                    }
                    else if (e.Which == 3)
                        ShowCarTimeline(name);
                    else if (e.Which == 4)
                        RenameCar(name);
                    else
                        DeleteCar(name);
                })
            .Show();
    }

    private void EditCarPassport(string carName)
    {
        CarPassport meta = CarsMeta.Get(DataDirectory, carName);
        RenderEditor((L10n.Get("title.properties_dot") + " ") + carName, new List<EditorField>
        {
            EditorField.Text(L10n.Get("label.year"), meta.Year, false),
            EditorField.Text(L10n.Get("label.plate"), meta.Plate, false),
            EditorField.Text("VIN", meta.Vin, false),
            EditorField.Number(L10n.Get("label.tank_capacity_l"), meta.TankLiters, false),
            EditorField.Text(L10n.Get("label.note"), meta.Note, false, true)
        }, delegate(string[] values)
        {
            meta.CarName = carName;
            meta.Year = values[0];
            meta.Plate = values[1];
            meta.Vin = values[2];
            meta.TankLiters = values[3];
            meta.Note = values[4];
            CarsMeta.Upsert(DataDirectory, meta);
            RefreshRecords();
        });
    }

    private void ShowCarTimeline(string carName)
    {
        if (!IsCompleteData(DataDirectory))
        {
            ShowMessage(L10n.Get("error.import_or_create_first"));
            return;
        }
        try
        {
            ScrollView scroll = new ScrollView(this);
            TextView body = new TextView(this);
            body.Text = CarTimeline.FormatList(DataDirectory, carName);
            body.TextSize = 14;
            body.SetTextIsSelectable(true);
            body.SetTextColor(AndroidColor.Rgb(35, 51, 70));
            body.SetPadding(Dp(12), Dp(8), Dp(12), Dp(8));
            scroll.AddView(body);
            new AlertDialog.Builder(this)
                .SetTitle((L10n.Get("title.timeline_dot") + " ") + carName)
                .SetView(scroll)
                .SetPositiveButton(L10n.Get("button.close"), delegate { })
                .Show();
        }
        catch (Exception ex)
        {
            ShowMessage((L10n.Get("error.build_timeline_failed") + " ") + ex.Message);
        }
    }

    private void AddCar()
    {
        if (syncActive)
        {
            ShowMessage(L10n.Get("error.wait_sync_finish"));
            return;
        }
        if (!IsCompleteData(DataDirectory))
        {
            StartFromScratch();
            return;
        }
        List<EditorField> fields = new List<EditorField>
        {
            EditorField.Text(L10n.Get("label.car_name"), "", true)
        };
        RenderEditor(L10n.Get("title.add_car"), fields,
            delegate(string[] values)
            {
                ExpenseDataStore.AddCar(DataDirectory, values[0]);
                SetCurrentCar(values[0].Trim());
                RefreshRecords();
            });
    }

    private void RenameCar(string oldName)
    {
        RenderEditor(L10n.Get("title.rename_car"), new List<EditorField>
        {
            EditorField.Text(L10n.Get("label.new_name"), oldName, true)
        }, delegate(string[] values)
        {
            string newName = values[0].Trim();
            ExpenseDataStore.RenameCar(DataDirectory, oldName, newName);
            if (String.Equals(currentCar, oldName, StringComparison.OrdinalIgnoreCase))
                SetCurrentCar(newName);
            RefreshRecords();
        });
    }

    private void DeleteCar(string name)
    {
        try
        {
            ExpenseDataStore.DeleteCar(DataDirectory, name);
            if (String.Equals(currentCar, name, StringComparison.OrdinalIgnoreCase))
                SetCurrentCar(ExpenseDataStore.LoadCarNames(DataDirectory)[0]);
            RefreshRecords();
        }
        catch (Exception ex)
        {
            ShowMessage((L10n.Get("error.delete_car_failed") + " ") + ex.Message);
        }
    }

    private void StartFromScratch()
    {
        if (syncActive)
        {
            ShowMessage(L10n.Get("error.wait_sync_finish"));
            return;
        }
        if (IsCompleteData(DataDirectory))
        {
            ShowMessage(L10n.Get("msg.data_exists_use_add"));
            return;
        }
        List<EditorField> fields = new List<EditorField>
        {
            EditorField.Text(L10n.Get("label.first_car_name"), "", true)
        };
        RenderEditor(L10n.Get("button.start_fresh"), fields,
            delegate(string[] values)
            {
                string name = values[0].Trim();
                ExpenseDataStore.CreateNewWorkspace(DataDirectory, name);
                SetCurrentCar(name);
                selectedSection = Fuel;
                GetSharedPreferences("settings", FileCreationMode.Private)
                    .Edit().PutString("selectedSection", selectedSection).Apply();
                RefreshRecords();
                Toast.MakeText(this,
                    L10n.Get("msg.ready_add_fuel_repairs"),
                    ToastLength.Long).Show();
            });
    }

    private void SetCurrentCar(string name)
    {
        currentCar = name;
        GetSharedPreferences("settings", FileCreationMode.Private)
            .Edit().PutString("currentCar", currentCar).Apply();
    }

    private void RenderEditor(string title, List<EditorField> fields, Action<string[]> save,
        bool wireFuelPrice = false, string receiptRecordId = null,
        Func<string[], string> confirmMessage = null, string templateType = null)
    {
        ScrollView scroll = new ScrollView(this);
        LinearLayout form = new LinearLayout(this);
        form.Orientation = Orientation.Vertical;
        form.SetPadding(Dp(8), Dp(4), Dp(8), Dp(4));

        for (int i = 0; i < fields.Count; i++)
            AddEditorField(form, fields[i]);

        if (wireFuelPrice && fields.Count >= 7)
            WireFuelPriceAutoCalc(fields[3], fields[4], fields[5]);
        // fields[7] = Полный бак when editing fuel

        if (!String.IsNullOrEmpty(receiptRecordId))
            AddReceiptControls(form, receiptRecordId);

        LinearLayout buttons = NewRow();
        Button saveButton = CreateButton(L10n.Get("button.save"), null);
        Button cancelButton = CreateButton(L10n.Get("button.cancel"), null);
        buttons.AddView(saveButton, WeightedButton());
        buttons.AddView(cancelButton, WeightedButton());
        form.AddView(buttons, MatchWrap());
        if (!String.IsNullOrEmpty(templateType))
        {
            Button templateButton = CreateButton(L10n.Get("button.save_template"), null);
            templateButton.Click += delegate
            {
                string validationError;
                string[] values = ReadEditorValues(fields, out validationError);
                if (validationError != null)
                {
                    ShowMessage(validationError);
                    return;
                }
                if (templateType == RecordTemplates.TypeFuel)
                    values = NormalizeFuelValues(values);
                SaveTemplateFromValues(templateType, values);
            };
            form.AddView(templateButton, MatchWrap());
        }
        scroll.AddView(form);

        AlertDialog dialog = new AlertDialog.Builder(this)
            .SetTitle(title)
            .SetView(scroll)
            .Create();
        saveButton.Click += delegate
        {
            string validationError;
            string[] values = ReadEditorValues(fields, out validationError);
            if (validationError != null)
            {
                ShowMessage(validationError);
                return;
            }
            Action commit = delegate
            {
                try
                {
                    save(values);
                    editorReceiptId = null;
                    editorReceiptThumb = null;
                    dialog.Dismiss();
                }
                catch (Exception ex)
                {
                    ShowMessage((L10n.Get("error.save_failed") + " ") + ex.Message);
                }
            };
            string confirm = confirmMessage != null ? confirmMessage(values) : null;
            if (!String.IsNullOrEmpty(confirm))
            {
                new AlertDialog.Builder(this)
                    .SetTitle(L10n.Get("label.odometer"))
                    .SetMessage(confirm)
                    .SetPositiveButton(L10n.Get("button.yes"), delegate { commit(); })
                    .SetNegativeButton(L10n.Get("button.no"), delegate { })
                    .Show();
                return;
            }
            commit();
        };
        cancelButton.Click += delegate
        {
            editorReceiptId = null;
            editorReceiptThumb = null;
            dialog.Dismiss();
        };
        dialog.Show();
    }

    private void AddReceiptControls(LinearLayout form, string recordId)
    {
        editorReceiptId = recordId;
        TextView label = new TextView(this);
        label.Text = L10n.Get("button.attach_receipt");
        label.SetTextColor(AndroidColor.Rgb(35, 51, 70));
        label.SetPadding(0, Dp(8), 0, Dp(4));
        form.AddView(label, MatchWrap());

        LinearLayout row = NewRow();
        editorReceiptThumb = new ImageView(this);
        editorReceiptThumb.SetScaleType(ImageView.ScaleType.CenterCrop);
        LinearLayout.LayoutParams thumbParams = new LinearLayout.LayoutParams(Dp(72), Dp(72));
        thumbParams.RightMargin = Dp(8);
        row.AddView(editorReceiptThumb, thumbParams);
        Button photoButton = CreateButton(L10n.Get("button.photo"), null);
        photoButton.Click += delegate { ChooseReceiptSource(); };
        row.AddView(photoButton, WeightedButton());
        form.AddView(row, MatchWrap());
        RefreshReceiptThumbnail();
    }

    private void ChooseReceiptSource()
    {
        if (String.IsNullOrEmpty(editorReceiptId))
        {
            ShowMessage(L10n.Get("error.open_record_for_photo_first"));
            return;
        }
        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("button.attach_receipt"))
            .SetItems(new string[] { L10n.Get("button.camera"), L10n.Get("button.gallery"), L10n.Get("button.delete_receipt") },
                delegate(object sender, DialogClickEventArgs e)
                {
                    if (e.Which == 0)
                        CaptureReceiptPhoto();
                    else if (e.Which == 1)
                        PickReceiptFromGallery();
                    else if (e.Which == 2)
                    {
                        ReceiptStore.Delete(DataDirectory, editorReceiptId);
                        RefreshReceiptThumbnail();
                    }
                })
            .SetNegativeButton(L10n.Get("button.cancel"), delegate { })
            .Show();
    }

    private void CaptureReceiptPhoto()
    {
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
#pragma warning disable CA1416
                if (CheckSelfPermission(global::Android.Manifest.Permission.Camera) !=
                    global::Android.Content.PM.Permission.Granted)
                {
                    RequestPermissions(new string[] { global::Android.Manifest.Permission.Camera }, 110);
                    Toast.MakeText(this,
                        L10n.Get("error.allow_camera_retry"),
                        ToastLength.Long).Show();
                    return;
                }
#pragma warning restore CA1416
            }
            // Without FileProvider/AndroidX: camera returns a thumbnail in Intent extras.
            // Gallery path still saves a full downscaled JPEG.
            cameraCaptureFile = null;
            Intent intent = new Intent(MediaStore.ActionImageCapture);
            StartActivityForResult(intent, PhotoCameraRequest);
        }
        catch (Exception ex)
        {
            ShowMessage((L10n.Get("error.camera_unavailable") + " ") + ex.Message);
        }
    }

    private void PickReceiptFromGallery()
    {
        Intent intent = new Intent(Intent.ActionGetContent);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("image/*");
        StartActivityForResult(Intent.CreateChooser(intent, L10n.Get("button.attach_receipt")), PhotoGalleryRequest);
    }

    private void SaveReceiptFromUri(Uri uri)
    {
        if (String.IsNullOrEmpty(editorReceiptId) || uri == null)
            return;
        Stream input = ContentResolver.OpenInputStream(uri);
        if (input == null)
            throw new IOException(L10n.Get("error.open_image_failed"));
        using (input)
            ReceiptStore.SaveJpegFromStream(DataDirectory, editorReceiptId, input);
        RefreshReceiptThumbnail();
        Toast.MakeText(this, L10n.Get("msg.receipt_saved"), ToastLength.Short).Show();
    }

    private void SaveReceiptFromCamera(Intent data)
    {
        if (String.IsNullOrEmpty(editorReceiptId))
            return;
        if (cameraCaptureFile != null && cameraCaptureFile.Exists())
        {
            using (FileStream input = File.OpenRead(cameraCaptureFile.AbsolutePath))
                ReceiptStore.SaveJpegFromStream(DataDirectory, editorReceiptId, input);
            try { cameraCaptureFile.Delete(); } catch { }
            cameraCaptureFile = null;
            RefreshReceiptThumbnail();
            Toast.MakeText(this, L10n.Get("msg.receipt_saved"), ToastLength.Short).Show();
            return;
        }
        if (data != null && data.Extras != null)
        {
            Java.Lang.Object raw = data.Extras.Get("data");
            AndroidBitmap bitmap = raw as AndroidBitmap;
            if (bitmap != null)
            {
                ReceiptStore.SaveJpegFromBitmap(DataDirectory, editorReceiptId, bitmap);
                RefreshReceiptThumbnail();
                Toast.MakeText(this, L10n.Get("msg.receipt_saved"), ToastLength.Short).Show();
            }
        }
    }

    private void RefreshReceiptThumbnail()
    {
        if (editorReceiptThumb == null || String.IsNullOrEmpty(editorReceiptId))
            return;
        if (!ReceiptStore.Exists(DataDirectory, editorReceiptId))
        {
            editorReceiptThumb.SetImageDrawable(null);
            editorReceiptThumb.SetBackgroundColor(AndroidColor.Rgb(225, 231, 237));
            return;
        }
        string path = ReceiptStore.PathForId(DataDirectory, editorReceiptId);
        AndroidBitmap thumb = AndroidBitmapFactory.DecodeFile(path);
        if (thumb != null)
            editorReceiptThumb.SetImageBitmap(thumb);
        else
            editorReceiptThumb.SetBackgroundColor(AndroidColor.Rgb(225, 231, 237));
    }

    private void WireFuelPriceAutoCalc(EditorField litersField, EditorField costField, EditorField priceField)
    {
        if (litersField.TextInput == null || costField.TextInput == null || priceField.TextInput == null)
            return;
        bool priceTouched = (priceField.TextInput.Text ?? "").Trim().Length > 0;
        priceField.TextInput.TextChanged += delegate
        {
            if (priceField.TextInput.HasFocus)
                priceTouched = true;
        };
        void Recalc(object sender, global::Android.Text.TextChangedEventArgs e)
        {
            if (priceTouched && (priceField.TextInput.Text ?? "").Trim().Length > 0)
                return;
            decimal liters;
            decimal cost;
            if (!TryNumber(litersField.TextInput.Text, out liters) || liters <= 0 ||
                !TryNumber(costField.TextInput.Text, out cost) || cost <= 0)
                return;
            string next = (cost / liters).ToString("0.##", CultureInfo.InvariantCulture);
            if (priceField.TextInput.Text != next)
                priceField.TextInput.Text = next;
        }
        litersField.TextInput.TextChanged += Recalc;
        costField.TextInput.TextChanged += Recalc;
    }

    private void AddEditorField(LinearLayout form, EditorField field)
    {
        if (field.Kind == "check")
        {
            CheckBox check = new CheckBox(this);
            check.Text = field.Label;
            check.Checked = field.Value != "0";
            check.SetTextColor(AndroidColor.Rgb(35, 51, 70));
            check.SetPadding(0, Dp(6), 0, 0);
            field.CheckInput = check;
            form.AddView(check, MatchWrap());
            return;
        }

        TextView label = new TextView(this);
        label.Text = field.Label;
        label.TextSize = 14;
        label.SetTextColor(AndroidColor.Rgb(35, 51, 70));
        label.SetPadding(0, Dp(6), 0, 0);
        form.AddView(label, MatchWrap());


        if (field.Kind == "suggest")
        {
            AutoCompleteTextView input = new AutoCompleteTextView(this);
            input.Text = field.Value;
            input.SetSingleLine(true);
            input.SetTextColor(AndroidColor.Rgb(35, 51, 70));
            if (field.Options != null && field.Options.Length > 0)
            {
                ArrayAdapter<string> adapter = new ArrayAdapter<string>(
                    this, AndroidLayout.SimpleDropDownItem1Line, field.Options);
                input.Adapter = adapter;
                input.Threshold = 1;
            }
            field.TextInput = input;
            form.AddView(input, MatchWrap());
        }
        else if (field.Options != null)
        {
            Spinner spinner = new Spinner(this);
            ArrayAdapter<string> adapter = new ArrayAdapter<string>(
                this, AndroidLayout.SimpleSpinnerItem);
            for (int i = 0; i < field.Options.Length; i++)
                adapter.Add(field.Options[i]);
            adapter.SetDropDownViewResource(AndroidLayout.SimpleSpinnerDropDownItem);
            spinner.Adapter = adapter;
            int selected = Array.IndexOf(field.Options, field.Value);
            spinner.SetSelection(selected >= 0 ? selected : 0);
            field.Spinner = spinner;
            form.AddView(spinner, MatchWrap());
        }
        else if (field.Kind == "date" || field.Kind == "month")
        {
            LinearLayout dateRow = NewRow();
            Button dateButton = CreateButton(DateButtonText(field), null);
            field.DateButton = dateButton;
            if (!field.Required)
            {
                CheckBox enabled = new CheckBox(this);
                enabled.Text = L10n.Get("button.specify");
                enabled.Checked = field.Value.Length > 0;
                dateButton.Enabled = enabled.Checked;
                enabled.Click += delegate { dateButton.Enabled = enabled.Checked; };
                field.DateEnabled = enabled;
                dateRow.AddView(enabled, new LinearLayout.LayoutParams(
                    Dp(105), ViewGroup.LayoutParams.WrapContent));
            }
            dateButton.Click += delegate { ShowDatePicker(field); };
            dateRow.AddView(dateButton, new LinearLayout.LayoutParams(
                0, ViewGroup.LayoutParams.WrapContent, 1));
            form.AddView(dateRow, MatchWrap());
        }
        else
        {
            EditText input = new EditText(this);
            input.Text = field.Value;
            input.SetSingleLine(!field.MultiLine);
            input.SetTextColor(AndroidColor.Rgb(35, 51, 70));
            if (field.Kind == "number")
                input.InputType = global::Android.Text.InputTypes.ClassNumber |
                    global::Android.Text.InputTypes.NumberFlagDecimal |
                    global::Android.Text.InputTypes.NumberFlagSigned;
            else if (field.MultiLine)
                input.InputType = global::Android.Text.InputTypes.ClassText |
                    global::Android.Text.InputTypes.TextFlagMultiLine;
            field.TextInput = input;
            form.AddView(input, MatchWrap());
        }
    }

    private void ShowDatePicker(EditorField field)
    {
        DateTime initial = DateTime.Today;
        string format = field.Kind == "month" ? "yyyy-MM" : "yyyy-MM-dd";
        if (DateTime.TryParseExact(field.Value, format, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateTime parsed))
            initial = parsed;
        DatePickerDialog picker = new DatePickerDialog(this,
            delegate(object sender, DatePickerDialog.DateSetEventArgs e)
            {
                DateTime selected = new DateTime(e.Year, e.Month + 1, e.DayOfMonth);
                field.Value = selected.ToString(format, CultureInfo.InvariantCulture);
                field.DateButton.Text = DateButtonText(field);
                if (field.DateEnabled != null)
                    field.DateEnabled.Checked = true;
            }, initial.Year, initial.Month - 1, initial.Day);
        picker.Show();
    }

    private static string DateButtonText(EditorField field)
    {
        if (field.Value.Length == 0)
            return field.Kind == "month" ? "Выбрать месяц" : "Выбрать дату";
        return field.Value;
    }

    private string[] ReadEditorValues(List<EditorField> fields, out string error)
    {
        string[] values = new string[fields.Count];
        error = null;
        for (int i = 0; i < fields.Count; i++)
        {
            EditorField field = fields[i];
            if (field.CheckInput != null)
                values[i] = field.CheckInput.Checked ? "1" : "0";
            else if (field.Spinner != null)
                values[i] = field.Spinner.SelectedItem == null
                    ? "" : field.Spinner.SelectedItem.ToString();
            else if (field.TextInput != null)
                values[i] = field.TextInput.Text.Trim();
            else if (field.DateEnabled != null && !field.DateEnabled.Checked)
                values[i] = "";
            else
                values[i] = field.Value;

            if (field.Required && values[i].Length == 0)
            {
                error = field.Label + (" " + L10n.Get("error.required_field_suffix"));
                return values;
            }
            if (values[i].Length == 0)
                continue;

            if (field.Kind == "number")
            {
                decimal number;
                if (!TryNumber(values[i], out number) || number < 0)
                {
                    error = field.Label + (" " + L10n.Get("error.enter_non_negative_suffix"));
                    return values;
                }
                values[i] = number.ToString(CultureInfo.InvariantCulture);
            }
            else if (field.Kind == "date" &&
                !DateTime.TryParseExact(values[i], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime date))
            {
                error = L10n.Get("error.select_valid_date");
                return values;
            }
            else if (field.Kind == "month" &&
                !DateTime.TryParseExact(values[i] + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime month))
            {
                error = L10n.Get("error.select_valid_month");
                return values;
            }
        }
        return values;
    }

    private static bool HasUtilityAmount(string[] values)
    {
        for (int i = 1; i < values.Length; i++)
            if (values[i].Length > 0)
                return true;
        return false;
    }

    private void ConfirmDelete(string[] row, int sourceIndex)
    {
        string description = selectedSection == Fuel
            ? row[0] + ", " + row[2] + (" " + L10n.Get("unit.km_comma") + " ") + row[4] + " " + Cur()
            : selectedSection == Repairs
                ? row[0] + ", " + row[3] + ": " + ShortText(row[4], 30)
                : selectedSection == Expenses
                    ? row[0] + ", " + FieldAt(row, 2) + ": " + ShortText(FieldAt(row, 3), 30)
                    : (L10n.Get("confirm.utility_month_delete_prefix") + " ") + row[0];
        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("confirm.delete_record"))
            .SetMessage(description)
            .SetNegativeButton(L10n.Get("button.cancel"), delegate { })
            .SetPositiveButton(L10n.Get("button.delete"), delegate
            {
                try
                {
                    ExpenseDataStore.DeleteRecord(DataDirectory, GetSelectedFile(), sourceIndex);
                    RefreshRecords();
                }
                catch (Exception ex)
                {
                    ShowMessage((L10n.Get("error.delete_record_failed") + " ") + ex.Message);
                }
            })
            .Show();
    }

    private void EditRepairFilter()
    {
        List<string> categories = new List<string> { AllCategories };
        categories.AddRange(ExpenseDataStore.LoadRepairCategories(DataDirectory));
        List<EditorField> fields = new List<EditorField>
        {
            EditorField.Option(L10n.Get("label.category"), filterCategory, categories.ToArray(), true),
            EditorField.Date(L10n.Get("label.date_from"), filterDateFrom, false),
            EditorField.Date(L10n.Get("label.date_to"), filterDateTo, false),
            EditorField.Text(L10n.Get("label.name_or_item"), filterText, false)
        };
        RenderEditor(L10n.Get("title.repair_filter"), fields,
            delegate(string[] values)
            {
                if (values[1].Length > 0 && values[2].Length > 0 &&
                    String.CompareOrdinal(values[1], values[2]) > 0)
                    throw new InvalidOperationException(L10n.Get("error.period_start_after_end"));
                filterCategory = values[0];
                filterDateFrom = values[1];
                filterDateTo = values[2];
                filterText = values[3];
                RefreshRecords();
            });
    }

    private void EditFuelFilter()
    {
        List<string> stationFilterOptions = new List<string> { "" };
        string[] knownStations = StationFieldOptions(fuelFilterStation);
        for (int i = 0; i < knownStations.Length; i++)
        {
            if (knownStations[i].Length > 0)
                stationFilterOptions.Add(knownStations[i]);
        }
        List<EditorField> fields = new List<EditorField>
        {
            EditorField.Date(L10n.Get("label.date_from"), fuelFilterDateFrom, false),
            EditorField.Date(L10n.Get("label.date_to"), fuelFilterDateTo, false),
            EditorField.Text(L10n.Get("error.odometer_liters_cost"), fuelFilterText, false),
            EditorField.Option(L10n.Get("label.purpose"), fuelFilterPurpose,
                PurposeFieldOptions(fuelFilterPurpose), false),
            EditorField.Option(L10n.Get("label.station_brand"), fuelFilterStation,
                stationFilterOptions.ToArray(), false)
        };
        RenderEditor(L10n.Get("title.fuel_filter"), fields,
            delegate(string[] values)
            {
                if (values[0].Length > 0 && values[1].Length > 0 &&
                    String.CompareOrdinal(values[0], values[1]) > 0)
                    throw new InvalidOperationException(L10n.Get("error.period_start_after_end"));
                fuelFilterDateFrom = values[0];
                fuelFilterDateTo = values[1];
                fuelFilterText = values[2];
                fuelFilterPurpose = values[3];
                fuelFilterStation = values.Length > 4 ? values[4] : "";
                RefreshRecords();
            });
    }

    private void EditSectionFilter()
    {
        if (selectedSection == Fuel)
            EditFuelFilter();
        else if (selectedSection == Repairs)
            EditRepairFilter();
    }

    private void ManageRepairCategories()
    {
        if (!IsCompleteData(DataDirectory))
        {
            ShowMessage(L10n.Get("error.import_kpk_first"));
            return;
        }
        List<string> categories = ExpenseDataStore.LoadRepairCategories(DataDirectory);
        AlertDialog.Builder builder = new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("title.repair_categories"))
            .SetPositiveButton(L10n.Get("button.close"), delegate { })
            .SetNeutralButton(L10n.Get("button.add"), delegate { AddRepairCategory(); });
        if (categories.Count > 0)
        {
            string[] names = categories.ToArray();
            builder.SetItems(names, delegate(object sender, DialogClickEventArgs e)
            {
                ShowRepairCategoryActions(names[e.Which]);
            });
        }
        builder.Show();
    }

    private void ShowRepairCategoryActions(string name)
    {
        new AlertDialog.Builder(this)
            .SetTitle(name)
            .SetItems(new string[] { L10n.Get("button.rename"), L10n.Get("button.delete") },
                delegate(object sender, DialogClickEventArgs e)
                {
                    if (e.Which == 0)
                        RenameRepairCategory(name);
                    else
                        ConfirmDeleteRepairCategory(name);
                })
            .Show();
    }

    private void AddRepairCategory()
    {
        RenderEditor(L10n.Get("title.new_category"), new List<EditorField>
        {
            EditorField.Text(L10n.Get("label.name"), "", true)
        }, delegate(string[] values)
        {
            ExpenseDataStore.AddRepairCategory(DataDirectory, values[0]);
            RefreshRecords();
        });
    }

    private void RenameRepairCategory(string oldName)
    {
        RenderEditor(L10n.Get("title.rename_category"), new List<EditorField>
        {
            EditorField.Text(L10n.Get("label.new_name"), oldName, true)
        }, delegate(string[] values)
        {
            ExpenseDataStore.RenameRepairCategory(DataDirectory, oldName, values[0]);
            if (filterCategory == oldName)
                filterCategory = values[0].Trim();
            RefreshRecords();
        });
    }

    private void ConfirmDeleteRepairCategory(string name)
    {
        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("confirm.delete_category"))
            .SetMessage(L10n.Get("error.move_records_no_delete"))
            .SetNegativeButton(L10n.Get("button.cancel"), delegate { })
            .SetPositiveButton(L10n.Get("button.delete"), delegate
            {
                try
                {
                    ExpenseDataStore.DeleteRepairCategory(DataDirectory, name);
                    if (filterCategory == name)
                        filterCategory = AllCategories;
                    RefreshRecords();
                }
                catch (Exception ex)
                {
                    ShowMessage((L10n.Get("error.delete_category_failed") + " ") + ex.Message);
                }
            })
            .Show();
    }

    private void ResetSectionFilter()
    {
        if (selectedSection == Fuel)
        {
            fuelFilterDateFrom = "";
            fuelFilterDateTo = "";
            fuelFilterText = "";
            fuelFilterPurpose = "";
            fuelFilterStation = "";
        }
        else if (selectedSection == Repairs)
        {
            filterCategory = AllCategories;
            filterDateFrom = "";
            filterDateTo = "";
            filterText = "";
        }
        RefreshRecords();
    }

    private bool HasCurrentFilter()
    {
        return selectedSection == Fuel ? HasFuelFilter() :
            selectedSection == Repairs && HasRepairFilter();
    }

    private bool HasFuelFilter()
    {
        return fuelFilterDateFrom.Length > 0 || fuelFilterDateTo.Length > 0 ||
            fuelFilterText.Length > 0 || fuelFilterPurpose.Length > 0 ||
            fuelFilterStation.Length > 0;
    }

    private bool HasRepairFilter()
    {
        return filterCategory != AllCategories || filterDateFrom.Length > 0 ||
            filterDateTo.Length > 0 || filterText.Length > 0;
    }

    private bool MatchesRepairFilter(string[] row)
    {
        if (filterCategory != AllCategories &&
            !String.Equals(row[3], filterCategory, StringComparison.OrdinalIgnoreCase))
            return false;
        if (filterDateFrom.Length > 0 && String.CompareOrdinal(row[0], filterDateFrom) < 0)
            return false;
        if (filterDateTo.Length > 0 && String.CompareOrdinal(row[0], filterDateTo) > 0)
            return false;
        if (filterText.Length > 0)
        {
            string text = (row[4] + " " + row[5]).ToLowerInvariant();
            if (text.IndexOf(filterText.Trim().ToLowerInvariant(), StringComparison.Ordinal) < 0)
                return false;
        }
        return true;
    }

    private bool MatchesFuelFilter(string[] row)
    {
        if (fuelFilterDateFrom.Length > 0 &&
            String.CompareOrdinal(row[0], fuelFilterDateFrom) < 0)
            return false;
        if (fuelFilterDateTo.Length > 0 &&
            String.CompareOrdinal(row[0], fuelFilterDateTo) > 0)
            return false;
        if (fuelFilterPurpose.Length > 0 &&
            !String.Equals(ExpenseDataCodec.GetPurpose(row), fuelFilterPurpose,
                StringComparison.OrdinalIgnoreCase))
            return false;
        if (fuelFilterStation.Length > 0 &&
            !String.Equals(ExpenseDataCodec.GetStation(row), fuelFilterStation,
                StringComparison.OrdinalIgnoreCase))
            return false;
        if (fuelFilterText.Length > 0)
        {
            string text = (row[2] + " " + row[3] + " " + row[4] + " " +
                FieldAt(row, 5) + " " + FieldAt(row, 6) + " " +
                ExpenseDataCodec.GetPurpose(row) + " " +
                ExpenseDataCodec.GetStation(row)).ToLowerInvariant();
            if (text.IndexOf(fuelFilterText.Trim().ToLowerInvariant(),
                StringComparison.Ordinal) < 0)
                return false;
        }
        return true;
    }

    private void ShowSummary()
    {
        if (!IsCompleteData(DataDirectory))
        {
            ShowMessage(L10n.Get("error.import_kpk_first"));
            return;
        }
        try
        {
            ScrollView scroll = new ScrollView(this);
            LinearLayout content = new LinearLayout(this);
            content.Orientation = Orientation.Vertical;
            content.SetPadding(Dp(8), Dp(4), Dp(8), Dp(4));

            TextView chartTitle = new TextView(this);
            chartTitle.Text = L10n.Get("title.expense_chart_12m");
            chartTitle.SetTextColor(AndroidColor.Rgb(180, 40, 40));
            chartTitle.SetTypeface(null, AndroidTypefaceStyle.Bold);
            chartTitle.TextSize = 15;
            content.AddView(chartTitle, MatchWrap());

            MonthlyExpenseChartView chart = new MonthlyExpenseChartView(this);
            List<string> months;
            List<decimal[]> totals;
            BuildMonthlyChartData(out months, out totals);
            chart.SetData(months, totals);
            LinearLayout.LayoutParams chartParams = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, Dp(260));
            chartParams.TopMargin = Dp(4);
            chartParams.BottomMargin = Dp(8);
            content.AddView(chart, chartParams);

            Button compareButton = CreateButton(L10n.Get("button.compare_periods"), null);
            LinearLayout.LayoutParams compareParams = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            compareParams.BottomMargin = Dp(8);
            content.AddView(compareButton, compareParams);

            Button reportButton = CreateButton(L10n.Get("title.period_report"), null);
            LinearLayout.LayoutParams reportParams = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            reportParams.BottomMargin = Dp(8);
            content.AddView(reportButton, reportParams);

            TextView body = new TextView(this);
            body.Text = BuildSummary();
            body.TextSize = 14;
            body.SetTextIsSelectable(true);
            body.SetTextColor(AndroidColor.Rgb(35, 51, 70));
            content.AddView(body, MatchWrap());
            scroll.AddView(content);
            string summaryText = body.Text;
            compareButton.Click += delegate { ShowPeriodCompare(); };
            reportButton.Click += delegate { ShowPeriodReport(); };
            new AlertDialog.Builder(this)
                .SetTitle((L10n.Get("title.summary_to_dot") + " ") + currentCar + CarsMeta.HeaderSuffix(DataDirectory, currentCar))
                .SetView(scroll)
                .SetPositiveButton(L10n.Get("button.share"), (sender, args) =>
                {
                    ShareSummaryText(summaryText);
                })
                .SetNeutralButton(L10n.Get("label.timeline"), delegate { ShowCarTimeline(currentCar); })
                .SetNegativeButton(L10n.Get("button.export"), (sender, args) =>
                {
                    ExportSummaryText(summaryText);
                })
                .Show();
        }
        catch (Exception ex)
        {
            ShowMessage((L10n.Get("error.build_summary_failed") + " ") + ex.Message);
        }
    }

    private void ShowPeriodCompare()
    {
        if (!IsCompleteData(DataDirectory))
        {
            ShowMessage(L10n.Get("error.import_kpk_first"));
            return;
        }
        string aFrom, aTo, bFrom, bTo;
        PeriodCompare.ThisVsPreviousMonth(out aFrom, out aTo, out bFrom, out bTo);
        new AlertDialog.Builder(this)
            .SetTitle((L10n.Get("compare.periods_dot") + " ") + currentCar)
            .SetItems(new string[]
            {
                L10n.Get("preset.this_month_last_month"),
                L10n.Get("preset.custom_dates")
            }, (sender, args) =>
            {
                if (args.Which == 0)
                    ShowPeriodCompareResult(aFrom, aTo, bFrom, bTo);
                else if (args.Which == 1)
                    PromptCustomPeriodCompare(aFrom, aTo, bFrom, bTo);
            })
            .SetNegativeButton(L10n.Get("button.close"), delegate { })
            .Show();
    }

    private void PromptCustomPeriodCompare(string aFrom, string aTo, string bFrom, string bTo)
    {
        RenderEditor(L10n.Get("title.periods_a_b"), new List<EditorField>
        {
            EditorField.Date(L10n.Get("compare.a_from"), aFrom, true),
            EditorField.Date(L10n.Get("compare.a_to"), aTo, true),
            EditorField.Date(L10n.Get("compare.b_from"), bFrom, true),
            EditorField.Date(L10n.Get("compare.b_to"), bTo, true)
        }, delegate(string[] raw)
        {
            if (String.CompareOrdinal(raw[0], raw[1]) > 0 ||
                String.CompareOrdinal(raw[2], raw[3]) > 0)
                throw new InvalidOperationException(L10n.Get("error.period_from_after_to"));
            ShowPeriodCompareResult(raw[0], raw[1], raw[2], raw[3]);
        }, false);
    }

    private void ShowPeriodCompareResult(string aFrom, string aTo, string bFrom, string bTo)
    {
        try
        {
            List<string[]> fuels = ExpenseDataStore.Load(DataDirectory, "fuel.dat");
            List<string[]> repairs = ExpenseDataStore.Load(DataDirectory, "repairs.dat");
            List<string[]> expenses = ExpenseDataStore.LoadOptional(DataDirectory, ExpenseDataCodec.ExpensesFile);
            List<string[]> utilities = ExpenseDataStore.Load(DataDirectory, "utilities.dat");
            PeriodCompare.Totals a = PeriodCompare.Build(
                currentCar, fuels, repairs, expenses, utilities, aFrom, aTo);
            PeriodCompare.Totals b = PeriodCompare.Build(
                currentCar, fuels, repairs, expenses, utilities, bFrom, bTo);
            string text = PeriodCompare.FormatComparison(
                a, b,
                PeriodCompare.FormatRangeLabel(aFrom, aTo),
                PeriodCompare.FormatRangeLabel(bFrom, bTo),
                Cur(), Money, "\n");
            ScrollView scroll = new ScrollView(this);
            TextView body = new TextView(this);
            body.Text = text;
            body.TextSize = 14;
            body.SetTextIsSelectable(true);
            body.SetTextColor(AndroidColor.Rgb(35, 51, 70));
            body.SetPadding(Dp(8), Dp(4), Dp(8), Dp(4));
            scroll.AddView(body);
            new AlertDialog.Builder(this)
                .SetTitle((L10n.Get("compare.short_dot") + " ") + currentCar)
                .SetView(scroll)
                .SetPositiveButton(L10n.Get("button.ok"), delegate { })
                .Show();
        }
        catch (Exception ex)
        {
            ShowMessage((L10n.Get("error.compare_periods_failed") + " ") + ex.Message);
        }
    }

    private void ShareSummaryText(string text)
    {
        ShareSummaryText(text, (L10n.Get("summary.file_prefix_dot") + " ") + currentCar, L10n.Get("title.share_summary"));
    }

    private void ShareSummaryText(string text, string subject, string chooserTitle)
    {
        if (String.IsNullOrEmpty(text))
            return;
        Intent share = new Intent(Intent.ActionSend);
        share.SetType("text/plain");
        share.PutExtra(Intent.ExtraSubject, subject);
        share.PutExtra(Intent.ExtraText, text);
        StartActivity(Intent.CreateChooser(share, chooserTitle));
    }

    private void ShareSummaryHtml(string html, string subject)
    {
        if (String.IsNullOrEmpty(html))
            return;
        Intent share = new Intent(Intent.ActionSend);
        share.SetType("text/html");
        share.PutExtra(Intent.ExtraSubject, subject);
        share.PutExtra(Intent.ExtraText, html);
        share.PutExtra(Intent.ExtraHtmlText, html);
        StartActivity(Intent.CreateChooser(share, L10n.Get("button.share_html")));
    }

    private void ExportSummaryText(string text)
    {
        ExportSummaryText(text,
            "Summary_" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".txt",
            "text/plain");
    }

    private void ExportSummaryText(string text, string fileName, string mime)
    {
        if (String.IsNullOrEmpty(text))
            return;
        pendingSummaryText = text;
        Intent intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType(String.IsNullOrEmpty(mime) ? "text/plain" : mime);
        intent.PutExtra(Intent.ExtraTitle, fileName);
        StartActivityForResult(intent, SummaryExportRequest);
    }

    private void ShowPeriodReport()
    {
        if (!IsCompleteData(DataDirectory))
        {
            ShowMessage(L10n.Get("error.import_kpk_first"));
            return;
        }
        string from, to, prevFrom, prevTo;
        PeriodCompare.ThisVsPreviousMonth(out from, out to, out prevFrom, out prevTo);
        new AlertDialog.Builder(this)
            .SetTitle((L10n.Get("report.title_dot") + " ") + currentCar)
            .SetItems(new string[]
            {
                L10n.Get("preset.this_month"),
                L10n.Get("preset.last_month"),
                L10n.Get("preset.custom_dates")
            }, (sender, args) =>
            {
                if (args.Which == 0)
                    ShowPeriodReportResult(from, to);
                else if (args.Which == 1)
                    ShowPeriodReportResult(prevFrom, prevTo);
                else if (args.Which == 2)
                    PromptCustomPeriodReport(from, to);
            })
            .SetNegativeButton(L10n.Get("button.close"), delegate { })
            .Show();
    }

    private void PromptCustomPeriodReport(string from, string to)
    {
        RenderEditor(L10n.Get("title.report_period"), new List<EditorField>
        {
            EditorField.Date(L10n.Get("label.from"), from, true),
            EditorField.Date(L10n.Get("label.to"), to, true)
        }, delegate(string[] raw)
        {
            if (String.CompareOrdinal(raw[0], raw[1]) > 0)
                throw new InvalidOperationException(L10n.Get("error.date_from_after_to"));
            ShowPeriodReportResult(raw[0], raw[1]);
        }, false);
    }

    private void ShowPeriodReportResult(string from, string to)
    {
        try
        {
            List<string[]> fuels = ExpenseDataStore.Load(DataDirectory, "fuel.dat");
            List<string[]> repairs = ExpenseDataStore.Load(DataDirectory, "repairs.dat");
            List<string[]> expenses = ExpenseDataStore.LoadOptional(DataDirectory, ExpenseDataCodec.ExpensesFile);
            List<string[]> utilities = ExpenseDataStore.Load(DataDirectory, "utilities.dat");
            PeriodCompare.Totals totals = PeriodCompare.Build(
                currentCar, fuels, repairs, expenses, utilities, from, to);
            List<string[]> trips = ExpenseDataStore.LoadOptional(
                DataDirectory, ExpenseDataCodec.TripsFile);
            PeriodCompare.AddTripKm(totals, currentCar, trips, from, to);
            PeriodCompare.ApplyBudget(totals, ToSettings.GetBudgetMonth(DataDirectory), from, to);
            string text = PeriodReport.FormatText(
                currentCar, from, to, totals, Cur(), Money, "\n");
            string html = PeriodReport.FormatHtml(
                currentCar, from, to, totals, Cur(), Money);
            string range = PeriodCompare.FormatRangeLabel(from, to);
            string subject = (L10n.Get("report.file_prefix_dot") + " ") + currentCar + " · " + range;

            ScrollView scroll = new ScrollView(this);
            TextView body = new TextView(this);
            body.Text = text;
            body.TextSize = 14;
            body.SetTextIsSelectable(true);
            body.SetTextColor(AndroidColor.Rgb(35, 51, 70));
            body.SetPadding(Dp(8), Dp(4), Dp(8), Dp(4));
            scroll.AddView(body);
            new AlertDialog.Builder(this)
                .SetTitle((L10n.Get("report.short_dot") + " ") + currentCar)
                .SetView(scroll)
                .SetPositiveButton(L10n.Get("button.share"), (sender, args) =>
                {
                    ShareSummaryText(text, subject, L10n.Get("title.share_report"));
                })
                .SetNeutralButton("HTML", (sender, args) =>
                {
                    new AlertDialog.Builder(this)
                        .SetTitle(L10n.Get("report.html_title"))
                        .SetItems(new string[]
                        {
                            L10n.Get("button.share_html"),
                            L10n.Get("button.save_html")
                        }, (s2, a2) =>
                        {
                            if (a2.Which == 0)
                                ShareSummaryHtml(html, subject);
                            else if (a2.Which == 1)
                                ExportSummaryText(html,
                                    "Report_" + from + "_" + to + ".html", "text/html");
                        })
                        .SetNegativeButton(L10n.Get("button.cancel"), delegate { })
                        .Show();
                })
                .SetNegativeButton(L10n.Get("button.save"), (sender, args) =>
                {
                    ExportSummaryText(text,
                        "Report_" + from + "_" + to + ".txt", "text/plain");
                })
                .Show();
        }
        catch (Exception ex)
        {
            ShowMessage((L10n.Get("error.build_report_failed") + " ") + ex.Message);
        }
    }

    private void BuildMonthlyChartData(out List<string> months, out List<decimal[]> totals)
    {
        Dictionary<string, decimal[]> monthlyTotals = new Dictionary<string, decimal[]>();
        List<string[]> fuels = ExpenseDataStore.Load(DataDirectory, "fuel.dat");
        List<string[]> repairs = ExpenseDataStore.Load(DataDirectory, "repairs.dat");
        List<string[]> expenses = ExpenseDataStore.LoadOptional(DataDirectory, ExpenseDataCodec.ExpensesFile);
        List<string[]> utilities = ExpenseDataStore.Load(DataDirectory, "utilities.dat");
        for (int i = 0; i < fuels.Count; i++)
            AddMonthlyAmount(monthlyTotals, fuels[i][0], 0, NumberOrZero(fuels[i][4]));
        for (int i = 0; i < repairs.Count; i++)
            AddMonthlyAmount(monthlyTotals, repairs[i][0], 1, NumberOrZero(repairs[i][7]));
        for (int i = 0; i < expenses.Count; i++)
        {
            if (expenses[i].Length < 5)
                continue;
            AddMonthlyAmount(monthlyTotals, expenses[i][0], 3, NumberOrZero(expenses[i][4]));
        }
        for (int i = 0; i < utilities.Count; i++)
            AddMonthlyAmount(monthlyTotals, utilities[i][0], 2,
                ExpenseDataCodec.Sum(utilities[i], 1));

        // Always show last 12 calendar months ending with current month.
        months = new List<string>();
        totals = new List<decimal[]>();
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
    }

    private string BuildSummary()
    {
        List<string[]> fuels = ExpenseDataStore.Load(DataDirectory, "fuel.dat");
        List<string[]> repairs = ExpenseDataStore.Load(DataDirectory, "repairs.dat");
        List<string[]> expenses = ExpenseDataStore.LoadOptional(DataDirectory, ExpenseDataCodec.ExpensesFile);
        List<string[]> utilities = ExpenseDataStore.Load(DataDirectory, "utilities.dat");
        decimal fuelTotal = 0;
        decimal repairTotal = 0;
        decimal expenseTotal = 0;
        decimal utilityTotal = 0;
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
        decimal currentMileage = 0;
        decimal minimumMileage = Decimal.MaxValue;
        decimal maximumMileage = 0;
        decimal lastOilMileage = 0;
        decimal lastTimingMileage = 0;
        string lastOilDate = "";
        string lastTimingDate = "";
        Dictionary<string, decimal> byCategory = new Dictionary<string, decimal>();
        Dictionary<string, decimal[]> monthlyTotals = new Dictionary<string, decimal[]>();
        List<string> cars = ExpenseDataStore.LoadCarNames(DataDirectory);

        for (int i = 0; i < fuels.Count; i++)
        {
            string[] row = fuels[i];
            decimal amount = NumberOrZero(row[4]);
            AddMonthlyAmount(monthlyTotals, row[0], 0, amount);
            if (row[1] != currentCar)
                continue;
            fuelTotal += amount;
            if (row[0] != null && row[0].StartsWith(yearPrefix, StringComparison.Ordinal))
                yearFuel += amount;
            string purpose = ExpenseDataCodec.GetPurpose(row);
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
            string station = ExpenseDataCodec.GetStation(row);
            if (station.Length > 0)
            {
                decimal price = NumberOrZero(FieldAt(row, 5));
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
            AddMileageReading(row[2], ref currentMileage, ref minimumMileage, ref maximumMileage);
        }
        for (int i = 0; i < repairs.Count; i++)
        {
            string[] row = repairs[i];
            decimal amount = NumberOrZero(row[7]);
            AddMonthlyAmount(monthlyTotals, row[0], 1, amount);
            if (row[1] != currentCar)
                continue;
            repairTotal += amount;
            if (row[0] != null && row[0].StartsWith(yearPrefix, StringComparison.Ordinal))
                yearRepair += amount;
            decimal mileage;
            if (TryNumber(row[2], out mileage) && mileage >= 0)
                AddMileageReading(row[2], ref currentMileage, ref minimumMileage, ref maximumMileage);
            if (!byCategory.ContainsKey(row[3]))
                byCategory.Add(row[3], 0);
            byCategory[row[3]] += amount;
            decimal serviceMileage;
            if (!TryNumber(row[2], out serviceMileage) || serviceMileage < 0)
                continue;
            if (row[3] == "ТО Масло" &&
                (serviceMileage > lastOilMileage ||
                (serviceMileage == lastOilMileage &&
                String.CompareOrdinal(row[0], lastOilDate) > 0)))
            {
                lastOilMileage = serviceMileage;
                lastOilDate = row[0];
            }
            if (row[3] == "ТО ГРМ" &&
                (serviceMileage > lastTimingMileage ||
                (serviceMileage == lastTimingMileage &&
                String.CompareOrdinal(row[0], lastTimingDate) > 0)))
            {
                lastTimingMileage = serviceMileage;
                lastTimingDate = row[0];
            }
        }
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
        for (int i = 0; i < utilities.Count; i++)
        {
            decimal amount = ExpenseDataCodec.Sum(utilities[i], 1);
            utilityTotal += amount;
            AddMonthlyAmount(monthlyTotals, utilities[i][0], 2, amount);
            if (utilities[i][0] != null &&
                utilities[i][0].StartsWith(yearPrefix, StringComparison.Ordinal))
                yearUtility += amount;
        }

        StringBuilder result = new StringBuilder(currentCar);
        result.Append(CarsMeta.HeaderSuffix(DataDirectory, currentCar));
        CarPassport passport = CarsMeta.Get(DataDirectory, currentCar);
        if (passport.Vin.Length > 0 || passport.TankLiters.Length > 0 || passport.Note.Length > 0)
        {
            result.Append("\n");
            if (passport.Vin.Length > 0)
                result.Append("VIN: ").Append(passport.Vin);
            if (passport.TankLiters.Length > 0)
            {
                if (passport.Vin.Length > 0)
                    result.Append(" · ");
                result.Append((L10n.Get("label.tank_short") + " ")).Append(passport.TankLiters).Append((" " + L10n.Get("unit.liters_short")));
            }
            if (passport.Note.Length > 0)
                result.Append("\n").Append(passport.Note);
        }
        result.Append(("\n\n" + L10n.Get("summary.fuel_section_colon") + " ")).Append(Money(fuelTotal)).Append(" " + Cur())
            .Append(("\n" + L10n.Get("summary.repairs_section_colon") + " ")).Append(Money(repairTotal)).Append(" " + Cur())
            .Append(("\n" + L10n.Get("summary.expenses_section_colon") + " ")).Append(Money(expenseTotal)).Append(" " + Cur());
        if (anyPurpose)
        {
            result.Append(("\n\n" + L10n.Get("report.fuel_by_purpose_colon")))
                .Append(("\n" + L10n.Get("purpose.personal_colon") + " ")).Append(Money(purposePersonal)).Append(" " + Cur())
                .Append(("\n" + L10n.Get("purpose.work_colon") + " ")).Append(Money(purposeWork)).Append(" " + Cur());
            if (purposeOther > 0)
                result.Append(("\n" + L10n.Get("report.other_colon") + " ")).Append(Money(purposeOther)).Append(" " + Cur());
        }
        if (stationStats.Count > 0)
        {
            result.Append(("\n\n" + L10n.Get("label.station_colon")));
            List<string> stationNames = new List<string>(stationStats.Keys);
            stationNames.Sort(StringComparer.OrdinalIgnoreCase);
            for (int s = 0; s < stationNames.Count; s++)
            {
                decimal[] bucket = stationStats[stationNames[s]];
                int count = (int)bucket[0];
                decimal avg = count > 0 ? bucket[1] / count : 0;
                result.Append("\n").Append(stationNames[s])
                    .Append(": ").Append(count.ToString(CultureInfo.InvariantCulture))
                    .Append((" " + L10n.Get("summary.dot_avg") + " ")).Append(Money(avg)).Append(" " + Cur()).Append(L10n.Get("unit.per_liter"));
            }
        }
        result.Append(("\n" + L10n.Get("summary.counted_cost_per_km") + " "))
            .Append(minimumMileage != Decimal.MaxValue && maximumMileage > minimumMileage
                ? Money((fuelTotal + repairTotal + expenseTotal) / (maximumMileage - minimumMileage)) + " " + Cur() + L10n.Get("unit.per_km")
                : L10n.Get("forecast.insufficient_mileage"))
            .Append(("\n\n" + L10n.Get("report.repairs_by_cat_colon")));
        foreach (KeyValuePair<string, decimal> pair in byCategory)
            result.Append("\n").Append(pair.Key).Append(": ").Append(Money(pair.Value)).Append(" " + Cur());
        result.Append(("\n\n" + L10n.Get("summary.utilities_all_time") + " ")).Append(Money(utilityTotal)).Append(" " + Cur());
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
        AppendBudgetBlock(result, "\n", monthKey,
            monthFuel + monthRepair + monthExpense, monthUtility);
        result.Append(("\n\n" + L10n.Get("summary.year_totals") + " ")).Append(yearPrefix).Append(":")
            .Append(("\n" + L10n.Get("label.fuel_colon") + " ")).Append(Money(yearFuel)).Append(" " + Cur())
            .Append(("\n" + L10n.Get("label.repairs_colon") + " ")).Append(Money(yearRepair)).Append(" " + Cur())
            .Append(("\n" + L10n.Get("label.expenses_colon") + " ")).Append(Money(yearExpense)).Append(" " + Cur())
            .Append(("\n" + L10n.Get("label.utilities_colon") + " ")).Append(Money(yearUtility)).Append(" " + Cur())
            .Append(("\n" + L10n.Get("report.total_colon") + " ")).Append(Money(yearFuel + yearRepair + yearExpense + yearUtility))
            .Append(" " + Cur())
            .Append(("\n\n" + L10n.Get("summary.monthly_expenses_12")));
        List<string> months = new List<string>(monthlyTotals.Keys);
        months.Sort(StringComparer.Ordinal);
        int firstMonth = Math.Max(0, months.Count - 12);
        for (int i = firstMonth; i < months.Count; i++)
        {
            decimal[] totals = monthlyTotals[months[i]];
            result.Append("\n").Append(months[i])
                .Append((L10n.Get("summary.colon_fuel") + " ")).Append(Money(totals[0]))
                .Append((L10n.Get("summary.comma_repairs") + " ")).Append(Money(totals[1]))
                .Append((L10n.Get("summary.comma_utilities") + " ")).Append(Money(totals[2]))
                .Append((L10n.Get("summary.comma_expenses") + " ")).Append(Money(totals.Length > 3 ? totals[3] : 0)).Append(" " + Cur());
        }
        result.Append("\n\n").Append(CarCompare.FormatBlock(
            CarCompare.Build(cars, fuels, repairs, expenses), Cur(), Money));
        ToIntervals toIntervals = ToSettings.Load(DataDirectory);
        result
            .Append(("\n\n" + L10n.Get("forecast.current_odometer_colon") + " ")).Append(Money(currentMileage)).Append((" " + L10n.Get("unit.km")))
            .Append("\n").Append(ServiceStatus(L10n.Get("service.oil_change"), currentMileage,
                lastOilMileage, lastOilDate, toIntervals.OilInterval, toIntervals.OilWarn))
            .Append("\n").Append(ServiceStatus(L10n.Get("service.timing_belt"), currentMileage,
                lastTimingMileage, lastTimingDate, toIntervals.BeltInterval, toIntervals.BeltWarn));
        result.Append(ForecastInsights.FormatBlock(
            currentCar, fuels, repairs, currentMileage,
            lastOilMileage, lastTimingMileage,
            toIntervals.OilInterval, toIntervals.BeltInterval,
            DateTime.Today, "\n"));
        int warnDays = ToSettings.GetReminderWarnDays(DataDirectory);
        result.Append(DateReminders.FormatUpcomingBlock(DataDirectory, warnDays, DateTime.Today, "\n"));
        result.Append(CarNotes.FormatRecentBlock(DataDirectory, currentCar, 3, "\n"));
        return result.ToString();
    }

    private static void AddMonthlyAmount(Dictionary<string, decimal[]> totals,
        string date, int category, decimal amount)
    {
        DateTime month;
        if (date == null || date.Length < 7 ||
            !DateTime.TryParseExact(date.Substring(0, 7), "yyyy-MM",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out month))
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
        if (!TryNumber(value, out mileage) || mileage < 0)
            return;
        currentMileage = Math.Max(currentMileage, mileage);
        minimumMileage = Math.Min(minimumMileage, mileage);
        maximumMileage = Math.Max(maximumMileage, mileage);
    }

    private void CreateLocalBackup()
    {
        try
        {
            string name = ExpenseDataStore.CreateBackup(DataDirectory);
            ShowMessage((L10n.Get("msg.backup_created_in_app") + " ") + name +
                ("\n" + L10n.Get("backup.phone_loss_tip")));
        }
        catch (Exception ex)
        {
            ShowMessage((L10n.Get("error.create_backup_failed") + " ") + ex.Message);
        }
    }

    private void RestoreLocalBackup()
    {
        try
        {
            List<string> backups = ExpenseDataStore.ListBackupNames(DataDirectory);
            if (backups.Count == 0)
            {
                ShowMessage(L10n.Get("error.no_phone_backups"));
                return;
            }
            string[] names = backups.ToArray();
            new AlertDialog.Builder(this)
                .SetTitle(L10n.Get("prompt.select_backup"))
                .SetItems(names, delegate(object sender, DialogClickEventArgs e)
                {
                    ConfirmRestore(names[e.Which]);
                })
                .SetNegativeButton(L10n.Get("button.cancel"), delegate { })
                .Show();
        }
        catch (Exception ex)
        {
            ShowMessage((L10n.Get("error.read_backups_failed") + " ") + ex.Message);
        }
    }

    private void ConfirmRestore(string name)
    {
        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("confirm.restore_backup"))
            .SetMessage((L10n.Get("confirm.replace_with_backup_inline") + " ") + name + "?")
            .SetNegativeButton(L10n.Get("button.cancel"), delegate { })
            .SetPositiveButton(L10n.Get("button.restore"), delegate
            {
                try
                {
                    ExpenseDataStore.RestoreBackup(DataDirectory, name);
                    List<string> cars = ExpenseDataStore.LoadCarNames(DataDirectory);
                    if (cars.IndexOf(currentCar) < 0)
                        SetCurrentCar(cars[0]);
                    RefreshRecords();
                    ShowMessage(L10n.Get("msg.data_restored"));
                }
                catch (Exception ex)
                {
                    ShowMessage((L10n.Get("error.restore_backup_failed") + " ") + ex.Message);
                }
            })
            .Show();
    }

    private void ChooseImportFiles()
    {
        Intent intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.PutExtra(Intent.ExtraAllowMultiple, true);
        StartActivityForResult(intent, ImportRequest);
    }

    private void ChooseExportFile()
    {
        if (!IsCompleteData(DataDirectory))
        {
            ShowMessage(L10n.Get("error.import_kpk_first"));
            return;
        }
        Intent intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("application/zip");
        intent.PutExtra(Intent.ExtraTitle,
            "Alfa156_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".zip");
        StartActivityForResult(intent, ExportRequest);
    }

    private void ImportSelection(Intent selection)
    {
        List<Uri> uris = new List<Uri>();
        if (selection.ClipData != null)
            for (int i = 0; i < selection.ClipData.ItemCount; i++)
                uris.Add(selection.ClipData.GetItemAt(i).Uri);
        else if (selection.Data != null)
            uris.Add(selection.Data);
        if (uris.Count == 0)
            throw new InvalidDataException(L10n.Get("error.no_backup_files_selected"));

        string staging = Path.Combine(CacheDir.AbsolutePath, "import_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        bool awaitingConfirmation = false;
        try
        {
            for (int i = 0; i < uris.Count; i++)
            {
                string displayName = GetDisplayName(uris[i]);
                if (displayName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    ImportZip(uris[i], staging);
                else if (ExpenseDataCodec.IsDataFile(displayName))
                    CopyUriToFile(uris[i], Path.Combine(staging, Path.GetFileName(displayName)));
            }

            ExpenseDataCodec.ValidateDirectory(staging);
            ExpenseDataStore.EnsureRepairCategories(staging);
            ConfirmImportedData(staging);
            awaitingConfirmation = true;
        }
        finally
        {
            if (!awaitingConfirmation && Directory.Exists(staging))
                Directory.Delete(staging, true);
        }
    }

    private void ConfirmImportedData(string source)
    {
        List<string[]> cars = ExpenseDataCodec.ReadRows(Path.Combine(source, "cars.dat"));
        List<string[]> fuel = ExpenseDataCodec.ReadRows(Path.Combine(source, "fuel.dat"));
        List<string[]> repairs = ExpenseDataCodec.ReadRows(Path.Combine(source, "repairs.dat"));
        List<string[]> utilities = ExpenseDataCodec.ReadRows(Path.Combine(source, "utilities.dat"));
        List<string[]> expenses = ExpenseDataStore.LoadOptional(source, ExpenseDataCodec.ExpensesFile);
        List<string[]> templates = ExpenseDataStore.LoadOptional(source, ExpenseDataCodec.TemplatesFile);
        List<string[]> reminders = ExpenseDataStore.LoadOptional(source, ExpenseDataCodec.RemindersFile);
        List<string[]> notes = ExpenseDataStore.LoadOptional(source, ExpenseDataCodec.NotesFile);
        int receiptCount = 0;
        string receiptsDir = Path.Combine(source, "Receipts");
        if (Directory.Exists(receiptsDir))
            receiptCount = Directory.GetFiles(receiptsDir).Length;

        StringBuilder summary = new StringBuilder();
        summary.Append(L10n.Get("import.preview_header_cars")).Append(cars.Count).Append("): ");
        if (cars.Count == 0)
            summary.Append("—");
        else
        {
            for (int i = 0; i < cars.Count; i++)
            {
                if (i > 0)
                    summary.Append(", ");
                summary.Append(cars[i][0]);
            }
        }
        summary.Append(("\n\n" + L10n.Get("summary.fuel_section_colon") + " ")).Append(fuel.Count)
            .Append(("\n" + L10n.Get("summary.repairs_section_colon") + " ")).Append(repairs.Count)
            .Append(("\n" + L10n.Get("summary.expenses_section_colon") + " ")).Append(expenses.Count)
            .Append(("\n" + L10n.Get("label.utilities_colon") + " ")).Append(utilities.Count)
            .Append(("\n" + L10n.Get("label.repair_cat_colon") + " "))
            .Append(ExpenseDataStore.LoadRepairCategories(source).Count);
        if (templates.Count > 0)
            summary.Append(("\n" + L10n.Get("label.templates_colon") + " ")).Append(templates.Count);
        if (reminders.Count > 0)
            summary.Append(("\n" + L10n.Get("summary.reminders_colon") + " ")).Append(reminders.Count);
        if (notes.Count > 0)
            summary.Append(("\n" + L10n.Get("summary.notes_colon") + " ")).Append(notes.Count);
        if (receiptCount > 0)
            summary.Append(("\n" + L10n.Get("label.receipt_photos_colon") + " ")).Append(receiptCount);
        summary.Append(IsCompleteData(DataDirectory)
            ? ("\n\n" + L10n.Get("hint.safety_backup_before_replace"))
            : ("\n\n" + L10n.Get("import.will_write_to_phone")));

        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("button.import_backup"))
            .SetMessage(summary.ToString())
            .SetNegativeButton(L10n.Get("button.cancel"), delegate { DeleteImportStaging(source); })
            .SetPositiveButton(L10n.Get("button.replace_data"), delegate
            {
                try
                {
                    InstallImportedData(source);
                    RefreshRecords();
                    ShowMessage(L10n.Get("msg.import_complete_phone"));
                }
                catch (Exception ex)
                {
                    ShowMessage((L10n.Get("error.import_failed_colon") + " ") + ex.Message);
                }
                finally
                {
                    DeleteImportStaging(source);
                }
            })
            .SetCancelable(false)
            .Show();
    }

    private static void DeleteImportStaging(string directory)
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }

    private void ImportZip(Uri uri, string destination)
    {
        Stream input = ContentResolver.OpenInputStream(uri);
        if (input == null)
            throw new IOException(L10n.Get("error.open_zip_failed"));
        using (input)
        using (ZipArchive archive = new ZipArchive(input, ZipArchiveMode.Read))
        {
            for (int i = 0; i < archive.Entries.Count; i++)
            {
                ZipArchiveEntry snapshotEntry = archive.Entries[i];
                if (!String.Equals(Path.GetFileName(snapshotEntry.FullName), "snapshot.json",
                    StringComparison.OrdinalIgnoreCase))
                    continue;
                using (Stream snapshotStream = snapshotEntry.Open())
                using (StreamReader snapshotReader = new StreamReader(snapshotStream, Encoding.UTF8))
                    ValidateSnapshotJson(snapshotReader.ReadToEnd());
            }
            for (int i = 0; i < archive.Entries.Count; i++)
            {
                ZipArchiveEntry entry = archive.Entries[i];
                if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                    continue;
                string name = Path.GetFileName(entry.FullName);
                string normalized = (entry.FullName ?? "").Replace('\\', '/');
                if (ExpenseDataCodec.IsDataFile(name) &&
                    normalized.IndexOf('/') < 0)
                {
                    using (Stream entryStream = entry.Open())
                    using (FileStream output = File.Create(Path.Combine(destination, name)))
                        entryStream.CopyTo(output);
                }
                else if (ReceiptStore.IsSafeReceiptFileName(name))
                {
                    ReceiptStore.ExtractFromZipEntry(entry, destination);
                }
            }
        }
    }

    private static void ValidateSnapshotJson(string json)
    {
        if (String.IsNullOrEmpty(json))
            throw new InvalidDataException(L10n.Get("error.empty_snapshot"));
        if (json.IndexOf("\"schema\":\"alfa156.snapshot\"", StringComparison.Ordinal) < 0 &&
            json.IndexOf("\"schema\": \"alfa156.snapshot\"", StringComparison.Ordinal) < 0)
            throw new InvalidDataException(L10n.Get("error.unsupported_snapshot_schema"));
        if (json.IndexOf("\"schemaVersion\":1", StringComparison.Ordinal) < 0 &&
            json.IndexOf("\"schemaVersion\": 1", StringComparison.Ordinal) < 0)
            throw new InvalidDataException(L10n.Get("error.unsupported_snapshot_version"));
        if (json.IndexOf("\"createdUtc\"", StringComparison.Ordinal) < 0 ||
            json.IndexOf("\"deviceToken\"", StringComparison.Ordinal) < 0)
            throw new InvalidDataException(L10n.Get("error.snapshot_missing_meta"));
    }

    private void CopyUriToFile(Uri uri, string destination)
    {
        Stream input = ContentResolver.OpenInputStream(uri);
        if (input == null)
            throw new IOException(L10n.Get("error.open_file_failed"));
        using (input)
        using (FileStream output = File.Create(destination))
            input.CopyTo(output);
    }

    private string GetDisplayName(Uri uri)
    {
        using (var cursor = ContentResolver.Query(uri, new string[] { "_display_name" },
            null, null, null))
        {
            if (cursor != null && cursor.MoveToFirst())
            {
                int column = cursor.GetColumnIndex("_display_name");
                if (column >= 0)
                    return cursor.GetString(column);
            }
        }
        return uri.LastPathSegment ?? "";
    }

    private void InstallImportedData(string source)
    {
        string safetyBackup = null;
        if (IsCompleteData(DataDirectory))
            safetyBackup = ExpenseDataStore.CreateSafetyBackup(DataDirectory, null);

        try
        {
            CopyData(source, DataDirectory);
            ExpenseDataCodec.ValidateDirectory(DataDirectory);
        }
        catch (Exception importError)
        {
            if (safetyBackup != null)
            {
                try
                {
                    CopyData(safetyBackup, DataDirectory);
                }
                catch (Exception rollbackError)
                {
                    throw new IOException((L10n.Get("error.import_incomplete_colon") + " ") + importError.Message +
                        (L10n.Get("error.rollback_prev_data_failed") + " ") + rollbackError.Message +
                        (L10n.Get("msg.dot_backup_colon") + " ") + safetyBackup);
                }
            }
            else
            {
                for (int i = 0; i < DataFiles.Length; i++)
                {
                    string partialFile = Path.Combine(DataDirectory, DataFiles[i]);
                    if (File.Exists(partialFile))
                        File.Delete(partialFile);
                }
            }
            throw;
        }
    }

    private void ExportTo(Uri destination)
    {
        if (destination == null)
            throw new InvalidDataException(L10n.Get("error.no_zip_save_location"));
        ExpenseDataStore.EnsureRepairCategories(DataDirectory);
        ToSettings.EnsureFile(DataDirectory);
        CarsMeta.EnsureFile(DataDirectory);
        RecordTemplates.EnsureFile(DataDirectory);
        DateReminders.EnsureFile(DataDirectory);
        CarNotes.EnsureFile(DataDirectory);
        RecordIds.EnsureDeletedFile(DataDirectory);
        ExpenseDataCodec.ValidateDirectory(DataDirectory);
        Stream output = ContentResolver.OpenOutputStream(destination);
        if (output == null)
            throw new IOException(L10n.Get("error.create_zip_failed"));
        using (output)
        using (ZipArchive archive = new ZipArchive(output, ZipArchiveMode.Create))
        {
            for (int i = 0; i < DataFiles.Length; i++)
            {
                string path = Path.Combine(DataDirectory, DataFiles[i]);
                if (!File.Exists(path))
                    continue;
                ZipArchiveEntry entry = archive.CreateEntry(DataFiles[i], CompressionLevel.Optimal);
                using (Stream entryStream = entry.Open())
                using (FileStream input = File.OpenRead(path))
                    input.CopyTo(entryStream);
            }
            ReceiptStore.AddFolderToZip(archive, DataDirectory);
            string snapshot = BuildSnapshotJson();
            ZipArchiveEntry snapshotEntry = archive.CreateEntry("snapshot.json", CompressionLevel.Optimal);
            using (StreamWriter writer = new StreamWriter(snapshotEntry.Open(), new UTF8Encoding(false)))
                writer.Write(snapshot);
        }
        try { ToSettings.MarkBackupDone(DataDirectory); }
        catch { /* reminder stamp must not fail ZIP export */ }
    }

    private static void CopyData(string source, string destination)
    {
        ExpenseDataStore.CopyDataFiles(source, destination);
    }

    private static bool IsCompleteData(string directory)
    {
        return ExpenseDataStore.HasCompleteCoreFiles(directory);
    }

    private static bool TryNumber(string value, out decimal number)
    {
        return Decimal.TryParse((value ?? "").Trim().Replace(',', '.'),
            NumberStyles.Number, CultureInfo.InvariantCulture, out number);
    }

    private static decimal NumberOrZero(string value)
    {
        decimal number;
        return TryNumber(value, out number) ? number : 0;
    }

    private static string Money(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string ShortText(string value, int maxLength)
    {
        string text = (value ?? "").Replace("\r", " ").Replace("\n", " ");
        return text.Length <= maxLength ? text : text.Substring(0, maxLength - 3) + "...";
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

    private void EnsureNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;
#pragma warning disable CA1416
        NotificationManager manager = (NotificationManager)GetSystemService(NotificationService);
        if (manager == null)
            return;
        NotificationChannel channel = new NotificationChannel(
            ToReminderChannelId,
            L10n.Get("title.service_reminders"),
            NotificationImportance.Default);
        channel.Description = L10n.Get("title.oil_and_belt");
        manager.CreateNotificationChannel(channel);
#pragma warning restore CA1416
    }

    private void EnsureBudgetNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;
#pragma warning disable CA1416
        NotificationManager manager = (NotificationManager)GetSystemService(NotificationService);
        if (manager == null)
            return;
        NotificationChannel channel = new NotificationChannel(
            BudgetChannelId,
            L10n.Get("label.monthly_budget"),
            NotificationImportance.Default);
        channel.Description = L10n.Get("notif.budget_exceeded_body");
        manager.CreateNotificationChannel(channel);
#pragma warning restore CA1416
    }

    private void EnsureDateReminderNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;
#pragma warning disable CA1416
        NotificationManager manager = (NotificationManager)GetSystemService(NotificationService);
        if (manager == null)
            return;
        NotificationChannel channel = new NotificationChannel(
            DateReminderChannelId,
            L10n.Get("title.date_reminders"),
            NotificationImportance.Default);
        channel.Description = L10n.Get("hint.insurance_inspection_other");
        manager.CreateNotificationChannel(channel);
#pragma warning restore CA1416
    }

    private void RefreshDateReminderNotifications()
    {
        if (!ExpenseDataStore.HasCompleteCoreFiles(DataDirectory))
            return;
        EnsureDateReminderNotificationChannel();
        MaybeRequestNotificationPermission();
        NotificationManager manager = (NotificationManager)GetSystemService(NotificationService);
        if (manager == null)
            return;

        int warnDays = ToSettings.GetReminderWarnDays(DataDirectory);
        List<DateReminder> due = DateReminders.ListDueWithin(DataDirectory, warnDays, DateTime.Today);
        if (due.Count == 0)
        {
            manager.Cancel(DateReminderNotificationId);
            return;
        }

        StringBuilder text = new StringBuilder();
        int limit = Math.Min(due.Count, 5);
        for (int i = 0; i < limit; i++)
        {
            if (i > 0)
                text.Append("; ");
            text.Append(DateReminders.FormatLine(due[i], DateTime.Today));
        }
        if (due.Count > limit)
            text.Append((" " + L10n.Get("summary.and_more") + " ")).Append(due.Count - limit);

        Intent intent = new Intent(this, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        PendingIntentFlags flags = PendingIntentFlags.UpdateCurrent;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            flags |= PendingIntentFlags.Immutable;
        PendingIntent pending = PendingIntent.GetActivity(this, DateReminderNotificationId, intent, flags);

#pragma warning disable CA1416, CA1422
        Notification.Builder builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(this, DateReminderChannelId)
            : new Notification.Builder(this);
        builder.SetContentTitle(L10n.Get("notif.reminders_title_open") + due.Count + ")")
            .SetContentText(text.ToString())
            .SetStyle(new Notification.BigTextStyle().BigText(text.ToString()))
            .SetSmallIcon(Resource.Drawable.icon)
            .SetContentIntent(pending)
            .SetAutoCancel(true)
            .SetOnlyAlertOnce(true);
        manager.Notify(DateReminderNotificationId, builder.Build());
#pragma warning restore CA1416, CA1422
    }

    private void AppendBudgetBlock(StringBuilder result, string nl, string monthKey,
        decimal spent, decimal utilitiesMonth)
    {
        decimal budget = ToSettings.GetBudgetMonth(DataDirectory);
        if (budget <= 0)
            return;
        decimal warnPct = ToSettings.GetBudgetWarnPct(DataDirectory);
        decimal pct = budget > 0 ? (spent * 100m) / budget : 0;
        result.Append(nl).Append(nl).Append((L10n.Get("label.monthly_budget") + " ")).Append(monthKey).Append(":")
            .Append(nl).Append((L10n.Get("summary.spent_total_colon") + " "))
            .Append(Money(spent)).Append(" / ").Append(Money(budget)).Append(" ").Append(Cur())
            .Append(" (").Append(Money(pct)).Append("%)")
            .Append(nl).Append((L10n.Get("summary.utilities_this_month") + " "))
            .Append(Money(utilitiesMonth)).Append(" ").Append(Cur());
        if (spent >= budget)
            result.Append(nl).Append(L10n.Get("summary.budget_exceeded_warn"));
        else if (pct >= warnPct)
            result.Append(nl).Append(L10n.Get("summary.budget_warn_symbol")).Append(Money(warnPct)).Append(L10n.Get("summary.pct_of_budget"));
    }

    private void RefreshBudgetNotification()
    {
        if (!ExpenseDataStore.HasCompleteCoreFiles(DataDirectory))
            return;
        EnsureBudgetNotificationChannel();
        MaybeRequestNotificationPermission();
        NotificationManager manager = (NotificationManager)GetSystemService(NotificationService);
        if (manager == null)
            return;

        decimal budget = ToSettings.GetBudgetMonth(DataDirectory);
        if (budget <= 0)
        {
            manager.Cancel(BudgetNotificationId);
            return;
        }

        decimal spent;
        decimal utilitiesMonth;
        ComputeMonthSpend(out spent, out utilitiesMonth);
        if (spent < budget)
        {
            manager.Cancel(BudgetNotificationId);
            return;
        }

        string monthKey = DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        string text = (L10n.Get("label.spent") + " ") + Money(spent) + " / " + Money(budget) + " " + Cur() +
            (" " + L10n.Get("common.for_preposition") + " ") + monthKey +
            (" " + L10n.Get("summary.utilities_paren_open") + " ") + Money(utilitiesMonth) + " " + Cur() + (" " + L10n.Get("summary.separately_close"));

        Intent intent = new Intent(this, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        PendingIntentFlags flags = PendingIntentFlags.UpdateCurrent;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            flags |= PendingIntentFlags.Immutable;
        PendingIntent pending = PendingIntent.GetActivity(this, BudgetNotificationId, intent, flags);

#pragma warning disable CA1416, CA1422
        Notification.Builder builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(this, BudgetChannelId)
            : new Notification.Builder(this);
        builder.SetContentTitle(L10n.Get("notif.budget_exceeded_title"))
            .SetContentText(text)
            .SetStyle(new Notification.BigTextStyle().BigText(text))
            .SetSmallIcon(Resource.Drawable.icon)
            .SetContentIntent(pending)
            .SetAutoCancel(true)
            .SetOnlyAlertOnce(true);
        manager.Notify(BudgetNotificationId, builder.Build());
#pragma warning restore CA1416, CA1422
    }

    private void ComputeMonthSpend(out decimal spent, out decimal utilitiesMonth)
    {
        spent = 0;
        utilitiesMonth = 0;
        string monthKey = DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        List<string[]> fuels = ExpenseDataStore.Load(DataDirectory, "fuel.dat");
        List<string[]> repairs = ExpenseDataStore.Load(DataDirectory, "repairs.dat");
        List<string[]> expenses = ExpenseDataStore.LoadOptional(DataDirectory, ExpenseDataCodec.ExpensesFile);
        List<string[]> utilities = ExpenseDataStore.Load(DataDirectory, "utilities.dat");
        for (int i = 0; i < fuels.Count; i++)
        {
            if (fuels[i].Length > 4 && fuels[i][0] != null &&
                fuels[i][0].StartsWith(monthKey, StringComparison.Ordinal))
                spent += NumberOrZero(fuels[i][4]);
        }
        for (int i = 0; i < repairs.Count; i++)
        {
            if (repairs[i].Length > 7 && repairs[i][0] != null &&
                repairs[i][0].StartsWith(monthKey, StringComparison.Ordinal))
                spent += NumberOrZero(repairs[i][7]);
        }
        for (int i = 0; i < expenses.Count; i++)
        {
            if (expenses[i].Length > 4 && expenses[i][0] != null &&
                expenses[i][0].StartsWith(monthKey, StringComparison.Ordinal))
                spent += NumberOrZero(expenses[i][4]);
        }
        for (int i = 0; i < utilities.Count; i++)
        {
            if (utilities[i][0] != null &&
                utilities[i][0].StartsWith(monthKey, StringComparison.Ordinal))
                utilitiesMonth += ExpenseDataCodec.Sum(utilities[i], 1);
        }
    }

    private void RefreshServiceNotifications()
    {
        ToIntervals intervals = ToSettings.Load(DataDirectory);
        if (!ExpenseDataStore.HasCompleteCoreFiles(DataDirectory))
            return;
        EnsureNotificationChannel();
        MaybeRequestNotificationPermission();

        List<string> cars = ExpenseDataStore.LoadCarNames(DataDirectory);
        List<string[]> fuels = ExpenseDataStore.Load(DataDirectory, "fuel.dat");
        List<string[]> repairs = ExpenseDataStore.Load(DataDirectory, "repairs.dat");
        NotificationManager manager = (NotificationManager)GetSystemService(NotificationService);
        if (manager == null)
            return;
        for (int i = 0; i < cars.Count; i++)
        {
            string car = cars[i];
            decimal currentMileage;
            decimal lastOilMileage;
            decimal lastTimingMileage;
            string lastOilDate;
            string lastTimingDate;
            ComputeServiceMileage(car, fuels, repairs,
                out currentMileage, out lastOilMileage, out lastTimingMileage,
                out lastOilDate, out lastTimingDate);

            PostServiceNotification(manager, car, L10n.Get("service.oil_short"), L10n.Get("service.oil"),
                currentMileage, lastOilMileage, intervals.OilInterval, intervals.OilWarn, 1000 + i * 10);
            PostServiceNotification(manager, car, L10n.Get("service.belt_short"), L10n.Get("service.timing_belt"),
                currentMileage, lastTimingMileage, intervals.BeltInterval, intervals.BeltWarn, 1001 + i * 10);
        }
    }

    private void MaybeRequestNotificationPermission()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            return;
        if (CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) ==
            global::Android.Content.PM.Permission.Granted)
            return;
        RequestPermissions(new string[] { global::Android.Manifest.Permission.PostNotifications }, 2001);
    }

    private static void ComputeServiceMileage(
        string car,
        List<string[]> fuels,
        List<string[]> repairs,
        out decimal currentMileage,
        out decimal lastOilMileage,
        out decimal lastTimingMileage,
        out string lastOilDate,
        out string lastTimingDate)
    {
        currentMileage = 0;
        lastOilMileage = 0;
        lastTimingMileage = 0;
        lastOilDate = "";
        lastTimingDate = "";
        decimal minimumMileage = Decimal.MaxValue;
        decimal maximumMileage = 0;
        for (int i = 0; i < fuels.Count; i++)
        {
            if (fuels[i].Length > 2 && fuels[i][1] == car)
                AddMileageReading(fuels[i][2], ref currentMileage, ref minimumMileage, ref maximumMileage);
        }
        for (int i = 0; i < repairs.Count; i++)
        {
            string[] row = repairs[i];
            if (row.Length < 4 || row[1] != car)
                continue;
            decimal mileage;
            if (TryNumber(row[2], out mileage) && mileage >= 0)
                AddMileageReading(row[2], ref currentMileage, ref minimumMileage, ref maximumMileage);
            if (!TryNumber(row[2], out mileage) || mileage < 0)
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
    }

    private void PostServiceNotification(
        NotificationManager manager,
        string car,
        string categoryLabel,
        string shortName,
        decimal currentMileage,
        decimal lastMileage,
        decimal interval,
        decimal warningDistance,
        int notificationId)
    {
        if (lastMileage <= 0)
        {
            manager.Cancel(notificationId);
            return;
        }
        decimal since = currentMileage - lastMileage;
        string text;
        if (since >= interval)
            text = (L10n.Get("notif.due_now_title") + " ") + shortName + " — " + car;
        else if (since >= interval - warningDistance)
            text = (L10n.Get("notif.due_soon_title") + " ") + shortName + (" " + L10n.Get("forecast.dash_remaining") + " ") +
                Money(interval - since) + (" " + L10n.Get("unit.km_dot") + " ") + car;
        else
        {
            manager.Cancel(notificationId);
            return;
        }

        Intent intent = new Intent(this, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        PendingIntentFlags flags = PendingIntentFlags.UpdateCurrent;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            flags |= PendingIntentFlags.Immutable;
        PendingIntent pending = PendingIntent.GetActivity(this, notificationId, intent, flags);

#pragma warning disable CA1416, CA1422
        Notification.Builder builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(this, ToReminderChannelId)
            : new Notification.Builder(this);
        builder.SetContentTitle("Alfa 156 · " + categoryLabel)
            .SetContentText(text)
            .SetStyle(new Notification.BigTextStyle().BigText(text))
            .SetSmallIcon(Resource.Drawable.icon)
            .SetContentIntent(pending)
            .SetAutoCancel(true)
            .SetOnlyAlertOnce(true);
        manager.Notify(notificationId, builder.Build());
#pragma warning restore CA1416, CA1422
    }

    private string Cur()
    {
        return ToSettings.GetCurrency(DataDirectory);
    }

    private void ShowAbout()
    {
        string version = "1.38.1";
        try
        {
            var info = PackageManager.GetPackageInfo(PackageName, 0);
            if (info != null && !String.IsNullOrEmpty(info.VersionName))
                version = info.VersionName;
        }
        catch { }
        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("title.about"))
            .SetMessage((L10n.Get("about.android_version_prefix") + " ") + version +
                ("\n\n" + L10n.Get("about.tagline")) +
                ("\n" + L10n.Get("onboarding.forecast_feature")) +
                ("\n" + L10n.Get("onboarding.passport_templates_feature")) +
                ("\n" + L10n.Get("onboarding.reminders_feature")) +
                ("\n" + L10n.Get("onboarding.notes_feature")) +
                ("\n" + L10n.Get("onboarding.station_feature")) +
                ("\n" + L10n.Get("onboarding.chart_feature")) +
                ("\n" + L10n.Get("onboarding.receipts_feature")) +
                ("\n" + L10n.Get("onboarding.full_tank_feature")))
            .SetPositiveButton(L10n.Get("button.ok"), delegate { })
            .Show();
    }

    private void ManageDateReminders()
    {
        if (!IsCompleteData(DataDirectory))
        {
            ShowMessage(L10n.Get("error.import_data_first"));
            return;
        }
        DateReminders.EnsureFile(DataDirectory);
        List<DateReminder> list = DateReminders.ListAll(DataDirectory);
        string message = DateReminders.FormatAllBlock(DataDirectory, DateTime.Today, "\n");
        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("label.reminders"))
            .SetMessage(message)
            .SetPositiveButton(L10n.Get("button.add"), (sender, args) => { AddDateReminder(); })
            .SetNeutralButton(list.Count > 0 ? L10n.Get("button.delete_ellipsis") : L10n.Get("button.close"), (sender, args) =>
            {
                if (list.Count > 0)
                    DeleteDateReminder(list);
            })
            .SetNegativeButton(L10n.Get("button.ok"), delegate { })
            .Show();
    }

    private void ManageCarNotes()
    {
        if (!IsCompleteData(DataDirectory))
        {
            ShowMessage(L10n.Get("error.import_data_first"));
            return;
        }
        CarNotes.EnsureFile(DataDirectory);
        string car = String.IsNullOrEmpty(currentCar) ? "" : currentCar;
        List<CarNote> list = CarNotes.ListForCar(DataDirectory, car);
        string message = CarNotes.FormatAllBlock(DataDirectory, car, "\n");
        new AlertDialog.Builder(this)
            .SetTitle(String.IsNullOrEmpty(car) ? L10n.Get("label.notes") : (L10n.Get("title.notes_dot") + " ") + car)
            .SetMessage(message)
            .SetPositiveButton(L10n.Get("button.add"), (sender, args) => { AddCarNote(); })
            .SetNeutralButton(list.Count > 0 ? L10n.Get("button.edit_ellipsis") : L10n.Get("button.close"), (sender, args) =>
            {
                if (list.Count > 0)
                    EditOrDeleteCarNote(list);
            })
            .SetNegativeButton(L10n.Get("button.ok"), delegate { })
            .Show();
    }

    private void AddCarNote()
    {
        string car = String.IsNullOrEmpty(currentCar) ? "" : currentCar;
        string defaultDate = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        RenderEditor(L10n.Get("title.new_note"), new List<EditorField>
        {
            EditorField.Date(L10n.Get("label.date"), defaultDate, true),
            EditorField.Text(L10n.Get("label.vehicle"), car, true),
            EditorField.Text(L10n.Get("label.text"), "", true, true)
        }, delegate(string[] raw)
        {
            CarNote item = new CarNote();
            item.Date = raw[0];
            item.Car = raw[1];
            item.Text = raw[2];
            CarNotes.Upsert(DataDirectory, item);
            ShowMessage(L10n.Get("msg.note_saved"));
        }, false);
    }

    private void EditOrDeleteCarNote(List<CarNote> list)
    {
        if (list == null || list.Count == 0)
            return;
        string[] labels = new string[list.Count];
        for (int i = 0; i < list.Count; i++)
            labels[i] = CarNotes.FormatLine(list[i]);
        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("prompt.select_note"))
            .SetItems(labels, (sender, args) =>
            {
                if (args.Which < 0 || args.Which >= list.Count)
                    return;
                CarNote item = list[args.Which];
                new AlertDialog.Builder(this)
                    .SetTitle(CarNotes.FormatLine(item))
                    .SetMessage(item.Text ?? "")
                    .SetPositiveButton(L10n.Get("button.edit"), (s2, a2) => { EditCarNote(item); })
                    .SetNeutralButton(L10n.Get("button.delete"), (s2, a2) =>
                    {
                        if (!String.IsNullOrEmpty(item.Id))
                            CarNotes.DeleteById(DataDirectory, item.Id);
                        ShowMessage(L10n.Get("msg.deleted"));
                    })
                    .SetNegativeButton(L10n.Get("button.cancel"), delegate { })
                    .Show();
            })
            .SetNegativeButton(L10n.Get("button.cancel"), delegate { })
            .Show();
    }

    private void EditCarNote(CarNote item)
    {
        if (item == null)
            return;
        RenderEditor(L10n.Get("button.edit_note"), new List<EditorField>
        {
            EditorField.Date(L10n.Get("label.date"), item.Date ?? "", true),
            EditorField.Text(L10n.Get("label.vehicle"), item.Car ?? "", true),
            EditorField.Text(L10n.Get("label.text"), item.Text ?? "", true, true)
        }, delegate(string[] raw)
        {
            item.Date = raw[0];
            item.Car = raw[1];
            item.Text = raw[2];
            CarNotes.Upsert(DataDirectory, item);
            ShowMessage(L10n.Get("msg.note_saved"));
        }, false);
    }

    private void AddDateReminder()
    {
        string car = String.IsNullOrEmpty(currentCar) ? "" : currentCar;
        string defaultDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        RenderEditor(L10n.Get("title.new_reminder"), new List<EditorField>
        {
            EditorField.Date(L10n.Get("label.date"), defaultDate, true),
            EditorField.Text(L10n.Get("label.vehicle"), car, true),
            EditorField.Text(L10n.Get("label.name"), "", true),
            EditorField.Option(L10n.Get("label.type"), DateReminders.TypeLabelRu(DateReminders.TypeInsurance),
                new string[] { L10n.Get("reminder.insurance"), L10n.Get("reminder.inspection"), L10n.Get("reminder.custom") }, true)
        }, delegate(string[] raw)
        {
            DateReminder item = new DateReminder();
            item.Date = raw[0];
            item.Car = raw[1];
            item.Title = raw[2];
            item.Type = DateReminders.NormalizeType(raw[3]);
            DateReminders.Upsert(DataDirectory, item);
            RefreshDateReminderNotifications();
            ShowMessage(L10n.Get("msg.reminder_saved"));
        }, false);
    }

    private void DeleteDateReminder(List<DateReminder> list)
    {
        if (list == null || list.Count == 0)
            return;
        string[] labels = new string[list.Count];
        for (int i = 0; i < list.Count; i++)
            labels[i] = DateReminders.FormatLine(list[i], DateTime.Today);
        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("button.delete_reminder"))
            .SetItems(labels, (sender, args) =>
            {
                if (args.Which < 0 || args.Which >= list.Count)
                    return;
                DateReminder item = list[args.Which];
                if (!String.IsNullOrEmpty(item.Id))
                    DateReminders.DeleteById(DataDirectory, item.Id);
                RefreshDateReminderNotifications();
                ShowMessage(L10n.Get("msg.deleted"));
            })
            .SetNegativeButton(L10n.Get("button.cancel"), delegate { })
            .Show();
    }

    private void EditReminderWarnDays()
    {
        int days = ToSettings.GetReminderWarnDays(DataDirectory);
        RenderEditor(L10n.Get("label.reminder_term"), new List<EditorField>
        {
            EditorField.Number(L10n.Get("label.days_until_due_default14"),
                days.ToString(CultureInfo.InvariantCulture), true)
        }, delegate(string[] raw)
        {
            decimal value;
            if (!TryNumber(raw[0], out value) || value < 0 || value > 3650 || value != Math.Floor(value))
                throw new InvalidOperationException(L10n.Get("error.term_0_3650"));
            ToSettings.SetReminderWarnDays(DataDirectory, (int)value);
            RefreshDateReminderNotifications();
            ShowMessage((L10n.Get("label.warn_before") + " ") + ((int)value) + (" " + L10n.Get("msg.days_before_date")));
        }, false);
    }

    private void ShowMessage(string message)
    {
        bool looksLikeError = message != null && (
            message.IndexOf("не удалось", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("не выполнен", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("Ошибка", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("Сначала", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("first", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("échec", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("fehler", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf('\n') >= 0 ||
            message.Length > 120);
        if (!looksLikeError)
        {
            Toast.MakeText(this, message, ToastLength.Long).Show();
            return;
        }
        new AlertDialog.Builder(this)
            .SetTitle("Alfa 156")
            .SetMessage(message)
            .SetPositiveButton(L10n.Get("button.ok"), delegate { })
            .Show();
    }

    private sealed class EditorField
    {
        internal string Label;
        internal string Kind;
        internal string Value;
        internal bool Required;
        internal bool MultiLine;
        internal string[] Options;
        internal EditText TextInput;
        internal Spinner Spinner;
        internal Button DateButton;
        internal CheckBox DateEnabled;
        internal CheckBox CheckInput;

        private EditorField(string label, string value, string kind, bool required)
        {
            Label = label;
            Value = value ?? "";
            Kind = kind;
            Required = required;
        }

        internal static EditorField Text(string label, string value, bool required, bool multiLine = false)
        {
            EditorField field = new EditorField(label, value, "text", required);
            field.MultiLine = multiLine;
            return field;
        }

        internal static EditorField Number(string label, string value, bool required)
        {
            return new EditorField(label, value, "number", required);
        }

        internal static EditorField Date(string label, string value, bool required)
        {
            return new EditorField(label, value, "date", required);
        }

        internal static EditorField Month(string label, string value, bool required)
        {
            return new EditorField(label, value, "month", required);
        }

        internal static EditorField Option(string label, string value, string[] options, bool required)
        {
            EditorField field = new EditorField(label, value, "option", required);
            field.Options = options;
            return field;
        }

        internal static EditorField Suggest(string label, string value, string[] options, bool required)
        {
            EditorField field = new EditorField(label, value, "suggest", required);
            field.Options = options ?? Array.Empty<string>();
            return field;
        }

        internal static EditorField Check(string label, bool isChecked)
        {
            return new EditorField(label, isChecked ? "1" : "0", "check", false);
        }
    }

    private void ManageExpenseCategories()
    {
        List<string> categories = ExpenseDataStore.LoadExpenseCategories(DataDirectory);
        List<string> labels = new List<string>();
        labels.Add(L10n.Get("title.add_category"));
        if (categories.Count > 0)
        {
            labels.Add(L10n.Get("title.rename_category"));
            labels.Add(L10n.Get("title.delete_category"));
        }
        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("title.expense_categories"))
            .SetItems(labels.ToArray(), (sender, args) =>
            {
                if (args.Which == 0)
                    PromptAddExpenseCategory();
                else if (args.Which == 1)
                    PromptRenameExpenseCategory();
                else if (args.Which == 2)
                    PromptDeleteExpenseCategory();
            })
            .SetNegativeButton(L10n.Get("button.close"), delegate { })
            .Show();
    }

    private void PromptAddExpenseCategory()
    {
        RenderEditor(L10n.Get("title.new_expense_category"), new List<EditorField>
        {
            EditorField.Text(L10n.Get("label.name"), "", true)
        }, delegate(string[] values)
        {
            ExpenseDataStore.AddExpenseCategory(DataDirectory, values[0]);
            RefreshRecords();
        }, false);
    }

    private void PromptRenameExpenseCategory()
    {
        List<string> categories = ExpenseDataStore.LoadExpenseCategories(DataDirectory);
        RenderEditor(L10n.Get("title.rename_expense_category"), new List<EditorField>
        {
            EditorField.Option(L10n.Get("label.category"), categories[0], categories.ToArray(), true),
            EditorField.Text(L10n.Get("label.new_name"), "", true)
        }, delegate(string[] values)
        {
            ExpenseDataStore.RenameExpenseCategory(DataDirectory, values[0], values[1]);
            RefreshRecords();
        }, false);
    }

    private void PromptDeleteExpenseCategory()
    {
        List<string> categories = ExpenseDataStore.LoadExpenseCategories(DataDirectory);
        RenderEditor(L10n.Get("title.delete_expense_category"), new List<EditorField>
        {
            EditorField.Option(L10n.Get("label.category"), categories[0], categories.ToArray(), true)
        }, delegate(string[] values)
        {
            new AlertDialog.Builder(this)
                .SetTitle(L10n.Get("confirm.delete_category"))
                .SetMessage(L10n.Get("error.move_records_no_delete"))
                .SetPositiveButton(L10n.Get("button.delete"), delegate
                {
                    try
                    {
                        ExpenseDataStore.DeleteExpenseCategory(DataDirectory, values[0]);
                        RefreshRecords();
                    }
                    catch (Exception ex)
                    {
                        ShowMessage((L10n.Get("error.delete_category_failed") + " ") + ex.Message);
                    }
                })
                .SetNegativeButton(L10n.Get("button.cancel"), delegate { })
                .Show();
        }, false);
    }

    private void EditToIntervals()
    {
        ToIntervals values = ToSettings.Load(DataDirectory);
        RenderEditor(L10n.Get("label.service_intervals"), new List<EditorField>
        {
            EditorField.Number(L10n.Get("label.oil_interval_km"),
                values.OilInterval.ToString(CultureInfo.InvariantCulture), true),
            EditorField.Number(L10n.Get("label.oil_warn_km"),
                values.OilWarn.ToString(CultureInfo.InvariantCulture), true),
            EditorField.Number(L10n.Get("label.belt_interval_km"),
                values.BeltInterval.ToString(CultureInfo.InvariantCulture), true),
            EditorField.Number(L10n.Get("label.belt_warn_km"),
                values.BeltWarn.ToString(CultureInfo.InvariantCulture), true)
        }, delegate(string[] raw)
        {
            decimal oilInterval, oilWarn, beltInterval, beltWarn;
            if (!TryNumber(raw[0], out oilInterval) || oilInterval <= 0 ||
                !TryNumber(raw[1], out oilWarn) || oilWarn <= 0 ||
                !TryNumber(raw[2], out beltInterval) || beltInterval <= 0 ||
                !TryNumber(raw[3], out beltWarn) || beltWarn <= 0)
                throw new InvalidOperationException(L10n.Get("error.enter_positive_numbers"));
            values.OilInterval = oilInterval;
            values.OilWarn = oilWarn;
            values.BeltInterval = beltInterval;
            values.BeltWarn = beltWarn;
            ToSettings.Save(DataDirectory, values);
            RefreshServiceNotifications();
            ShowMessage(L10n.Get("msg.intervals_saved"));
        }, false);
    }

    private void EditCurrency()
    {
        string current = ToSettings.GetCurrency(DataDirectory);
        RenderEditor(L10n.Get("label.currency"), new List<EditorField>
        {
            EditorField.Text(L10n.Get("label.currency_examples"), current, true)
        }, delegate(string[] raw)
        {
            ToSettings.SetCurrency(DataDirectory, raw[0]);
            RefreshRecords();
            RefreshServiceNotifications();
            ShowMessage((L10n.Get("msg.currency_colon") + " ") + ToSettings.GetCurrency(DataDirectory) +
                L10n.Get("msg.currency_no_recalc"));
        }, false);
    }

    private void EditLanguage()
    {
        string current = ToSettings.GetLanguage(DataDirectory);
        List<string> languages = new List<string>(L10n.GetAvailableLanguages());
        int currentIndex = languages.IndexOf(current);
        if (currentIndex < 0) currentIndex = 0;

        string[] displayNames = new string[languages.Count];
        for (int i = 0; i < languages.Count; i++)
            displayNames[i] = L10n.GetLanguageDisplayName(languages[i]) + " (" + languages[i] + ")";
        new AlertDialog.Builder(this)
            .SetTitle(L10n.Get("dialog.language"))
            .SetSingleChoiceItems(displayNames, currentIndex, (sender, args) =>
            {
                string selected = languages[args.Which];
                ToSettings.SetLanguage(DataDirectory, selected);
                LocalizationManager.SetLanguage(selected);
                ((AlertDialog)sender).Dismiss();
                Recreate();
            })
            .SetNegativeButton(L10n.Get("button.cancel"), delegate { })
            .Show();
    }

    private void EditBudget()
    {
        decimal budget = ToSettings.GetBudgetMonth(DataDirectory);
        decimal warnPct = ToSettings.GetBudgetWarnPct(DataDirectory);
        RenderEditor(L10n.Get("label.monthly_budget"), new List<EditorField>
        {
            EditorField.Number(L10n.Get("label.limit_0_off"),
                budget.ToString(CultureInfo.InvariantCulture), true),
            EditorField.Number(L10n.Get("label.warn_pct"),
                warnPct.ToString(CultureInfo.InvariantCulture), true)
        }, delegate(string[] raw)
        {
            decimal monthLimit, pct;
            if (!TryNumber(raw[0], out monthLimit) || monthLimit < 0)
                throw new InvalidOperationException(L10n.Get("error.limit_invalid"));
            if (!TryNumber(raw[1], out pct) || pct <= 0 || pct > 100)
                throw new InvalidOperationException(L10n.Get("error.warn_pct_range"));
            ToSettings.SetBudget(DataDirectory, monthLimit, pct);
            RefreshBudgetNotification();
            if (monthLimit <= 0)
                ShowMessage(L10n.Get("msg.budget_disabled"));
            else
                ShowMessage((L10n.Get("summary.monthly_budget_colon") + " ") + Money(monthLimit) + " " + Cur() +
                    (L10n.Get("summary.warn_from") + " ") + Money(pct) + "%.");
        }, false);
    }

    private void EditBackupReminder()
    {
        int days = ToSettings.GetBackupRemindDays(DataDirectory);
        RenderEditor(L10n.Get("title.backup_reminder"), new List<EditorField>
        {
            EditorField.Number(L10n.Get("label.days_off_default7"),
                days.ToString(CultureInfo.InvariantCulture), true)
        }, delegate(string[] raw)
        {
            decimal value;
            if (!TryNumber(raw[0], out value) || value < 0 || value > 3650 || value != Math.Floor(value))
                throw new InvalidOperationException(L10n.Get("error.interval_0_3650"));
            ToSettings.SetBackupRemindDays(DataDirectory, (int)value);
            if (value <= 0)
                ShowMessage(L10n.Get("msg.backup_reminder_off"));
            else
                ShowMessage((L10n.Get("label.backup_remind_if_none") + " ") + ((int)value) + (" " + L10n.Get("reminder.days_and_longer_dot")));
        }, false);
    }

    private string BuildSnapshotJson()
    {
        string token = ToSettings.GetOrCreateSnapshotDeviceToken(DataDirectory);
        string created = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'",
            CultureInfo.InvariantCulture);
        string version = "1.38.1";
        try
        {
            var info = PackageManager.GetPackageInfo(PackageName, 0);
            if (info != null && !String.IsNullOrEmpty(info.VersionName))
                version = info.VersionName;
        }
        catch { }
        return "{\"schema\":\"alfa156.snapshot\",\"schemaVersion\":1,\"appVersion\":" +
            QuoteSnapshotJson(version) + ",\"createdUtc\":" + QuoteSnapshotJson(created) +
            ",\"deviceToken\":" + QuoteSnapshotJson(token) + "}";
    }

    private static string QuoteSnapshotJson(string value)
    {
        if (value == null) value = "";
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    }

    private void MaybeShowBackupReminder()
    {
        try
        {
            if (!IsCompleteData(DataDirectory))
                return;
            if (!ToSettings.ShouldShowBackupReminder(DataDirectory))
                return;
            ToSettings.MarkBackupNagShown(DataDirectory);
            int days = ToSettings.DaysSinceLastBackup(DataDirectory);
            string age = days < 0 ? L10n.Get("summary.no_backup_yet") : ((L10n.Get("forecast.elapsed") + " ") + days + (" " + L10n.Get("unit.days_short")));
            new AlertDialog.Builder(this)
                .SetTitle(L10n.Get("title.backup"))
                .SetMessage(L10n.Get("reminder.backup_overdue_open") + age + ").\n" +
                    L10n.Get("hint.recommend_full_zip_or_phone"))
                .SetPositiveButton(L10n.Get("button.export_zip_short"), (sender, args) => { ChooseExportFile(); })
                .SetNeutralButton(L10n.Get("button.later"), delegate { })
                .Show();
        }
        catch
        {
            // soft reminder must not break resume
        }
    }

    private void InitializeLanguage()
    {
        try
        {
            string language = ToSettings.GetLanguage(DataDirectory);
            LocalizationManager.SetLanguage(language);
        }
        catch
        {
            LocalizationManager.SetLanguage("ru");
        }
    }
}
