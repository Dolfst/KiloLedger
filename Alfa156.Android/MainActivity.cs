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
using Android.Views;
using Android.Widget;
using AndroidColor = global::Android.Graphics.Color;
using AndroidLayout = global::Android.Resource.Layout;
using Uri = global::Android.Net.Uri;

[assembly: Android.App.UsesPermission(Android.Manifest.Permission.Internet)]

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
    private const string Fuel = "fuel";
    private const string Repairs = "repairs";
    private const string Utilities = "utilities";
    private const string AllCategories = "Все категории";
    private static readonly string[] DataFiles = ExpenseDataCodec.DataFiles;

    private TextView status;
    private ListView recordList;
    private Spinner carSpinner;
    private Button fuelButton;
    private Button repairsButton;
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
    private bool updatingCarSpinner;
    private int selectedRecordPosition = -1;
    private List<string[]> allRows = new List<string[]>();
    private List<string[]> visibleRows = new List<string[]>();
    private List<int> visibleIndexes = new List<int>();

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
            if (savedSection == Fuel || savedSection == Repairs || savedSection == Utilities)
                selectedSection = savedSection;
            CreateLayout();
            RefreshRecords();
        }
        catch (Exception ex)
        {
            ShowStartupError(ex);
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (resultCode != Result.Ok || data == null)
            return;

        try
        {
            if (requestCode == ImportRequest)
            {
                ImportSelection(data);
            }
            else if (requestCode == ExportRequest)
            {
                ExportTo(data.Data);
                ShowMessage("Резервная копия ZIP сохранена.");
            }
        }
        catch (Exception ex)
        {
            ShowMessage("Операция не выполнена: " + ex.Message);
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
        TextView title = new TextView(this);
        title.Text = "Alfa 156";
        title.TextSize = 22;
        title.SetTypeface(title.Typeface, Android.Graphics.TypefaceStyle.Bold);
        title.SetTextColor(AndroidColor.Rgb(35, 51, 70));
        titleRow.AddView(title, new LinearLayout.LayoutParams(0,
            ViewGroup.LayoutParams.WrapContent, 1));
        dataMenuButton = CreateButton("Данные", ButtonIcons.Menu, delegate { ShowDataMenu(); });
        dataMenuButton.TextSize = 12;
        titleRow.AddView(dataMenuButton, new LinearLayout.LayoutParams(
            Dp(126), ViewGroup.LayoutParams.WrapContent));
        root.AddView(titleRow, MatchWrap());

        // Состояние sync/backup/restore хранится флагами; кнопки — тонкие заглушки для старого кода.
        syncButton = CreateButton("Синхронизация по Wi-Fi", ButtonIcons.Sync, delegate { ToggleWiFiSync(); });
        backupButton = CreateButton("Копия на телефоне", ButtonIcons.Backup, delegate { CreateLocalBackup(); });
        restoreButton = CreateButton("Восстановить", ButtonIcons.Restore, delegate { RestoreLocalBackup(); });

        LinearLayout carRow = NewRow();
        carRow.SetGravity(GravityFlags.CenterVertical);
        carRow.SetPadding(0, Dp(2), 0, Dp(2));
        TextView carLabel = new TextView(this);
        carLabel.Text = "Авто";
        carLabel.TextSize = 14;
        carLabel.SetTextColor(AndroidColor.Rgb(35, 51, 70));
        carRow.AddView(carLabel, new LinearLayout.LayoutParams(Dp(42), ViewGroup.LayoutParams.WrapContent));
        carSpinner = new Spinner(this);
        carSpinner.ItemSelected += CarSelected;
        carRow.AddView(carSpinner, new LinearLayout.LayoutParams(0,
            ViewGroup.LayoutParams.WrapContent, 1));
        addCarButton = CreateButton("Добавить", ButtonIcons.Add, delegate { AddCar(); });
        carRow.AddView(addCarButton, new LinearLayout.LayoutParams(Dp(100), ViewGroup.LayoutParams.WrapContent));
        root.AddView(carRow);

        LinearLayout sectionRow1 = NewRow();
        sectionRow1.SetPadding(0, Dp(2), 0, Dp(2));
        fuelButton = CreateSectionButton("Заправки", ButtonIcons.Fuel, delegate { SelectSection(Fuel); });
        repairsButton = CreateSectionButton("Ремонт и ТО", ButtonIcons.Repairs, delegate { SelectSection(Repairs); });
        sectionRow1.AddView(fuelButton, WeightedButton(Dp(3)));
        sectionRow1.AddView(repairsButton, WeightedButton(Dp(3)));
        root.AddView(sectionRow1);

        LinearLayout sectionRow2 = NewRow();
        sectionRow2.SetPadding(0, 0, 0, Dp(2));
        utilitiesButton = CreateSectionButton("Коммунальные", ButtonIcons.Utilities, delegate { SelectSection(Utilities); });
        summaryButton = CreateSectionButton("Сводка и ТО", ButtonIcons.Summary, delegate { ShowSummary(); });
        sectionRow2.AddView(utilitiesButton, WeightedButton(Dp(3)));
        sectionRow2.AddView(summaryButton, WeightedButton(Dp(3)));
        root.AddView(sectionRow2);

        LinearLayout recordActions = NewRow();
        recordActions.SetPadding(0, Dp(2), 0, Dp(2));
        addButton = CreateButton("Добавить", ButtonIcons.Add, delegate { AddRecord(); });
        editButton = CreateButton("Изменить", ButtonIcons.Edit, delegate { EditSelectedRecord(); });
        deleteButton = CreateButton("Удалить", ButtonIcons.Delete, delegate { DeleteSelectedRecord(); });
        recordActions.AddView(addButton, WeightedButton(Dp(2)));
        recordActions.AddView(editButton, WeightedButton(Dp(2)));
        recordActions.AddView(deleteButton, WeightedButton(Dp(2)));
        root.AddView(recordActions);

        filterRow = NewRow();
        filterButton = CreateButton("Фильтр", ButtonIcons.Filter, delegate { EditSectionFilter(); });
        resetFilterButton = CreateButton("Сбросить", ButtonIcons.ResetFilter, delegate { ResetSectionFilter(); });
        categoriesButton = CreateButton("Категории", ButtonIcons.Categories, delegate { ManageRepairCategories(); });
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
            labels.Add("Остановить синхронизацию");
            actions.Add(delegate { ToggleWiFiSync(); });
        }
        else
        {
            labels.Add("Импорт копии");
            actions.Add(delegate { ChooseImportFiles(); });
            if (hasData)
            {
                labels.Add("Экспорт ZIP");
                actions.Add(delegate { ChooseExportFile(); });
            }
            labels.Add("Синхронизация по Wi-Fi");
            actions.Add(delegate { ToggleWiFiSync(); });
            if (hasData)
            {
                labels.Add("Копия на телефоне");
                actions.Add(delegate { CreateLocalBackup(); });
                labels.Add("Восстановить копию");
                actions.Add(delegate { RestoreLocalBackup(); });
            }
        }

        new AlertDialog.Builder(this)
            .SetTitle(syncActive ? "Данные · идёт синхронизация" : "Данные")
            .SetItems(labels.ToArray(), (sender, args) =>
            {
                if (args.Which >= 0 && args.Which < actions.Count)
                    actions[args.Which]();
            })
            .SetNegativeButton("Закрыть", delegate { })
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
        string iconKind = button.Tag as string;
        if (!string.IsNullOrEmpty(iconKind))
            ApplyButtonIcon(button, iconKind, IconColor(selected), iconOnTop);
    }

    private void RefreshRecords()
    {
        bool hasData = IsCompleteData(DataDirectory);
        SetDataActionsEnabled(hasData);
        if (!hasData)
        {
            status.Text = "Откройте «Данные» → «Импорт копии», чтобы начать работу.";
            allRows = new List<string[]>();
            visibleRows = new List<string[]>();
            visibleIndexes = new List<int>();
            selectedRecordPosition = -1;
            recordList.Adapter = CreateAdapter(new List<string>
            {
                "Нет данных. Импортируйте копию с КПК или ZIP."
            });
            SetCarOptions(new List<string>());
            UpdateSectionButtons();
            UpdateFilterRowVisibility();
            UpdateRecordActions();
            return;
        }

        ExpenseDataStore.EnsureRepairCategories(DataDirectory);
        List<string> cars = ExpenseDataStore.LoadCarNames(DataDirectory);
        if (cars.IndexOf(currentCar) < 0)
            currentCar = cars[0];
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

        string sectionTitle = selectedSection == Fuel ? "Заправки"
            : selectedSection == Repairs ? "Ремонт и ТО" : "Коммунальные";
        string scope = selectedSection == Utilities ? "все месяцы" : currentCar;
        string filterNote = HasCurrentFilter() ? " · фильтр" : "";
        if (displayRows.Count == 0)
        {
            status.Text = sectionTitle + " · " + scope + " · нет записей" + filterNote;
            displayRows.Add(HasCurrentFilter()
                ? "Нет записей по фильтру. Сбросьте фильтр или добавьте запись."
                : "Пока нет записей. Нажмите «Добавить».");
        }
        else if (selectedSection == Utilities)
        {
            status.Text = sectionTitle + " · показано " + displayRows.Count +
                " из " + utilityRows.Count + filterNote;
        }
        else
        {
            status.Text = sectionTitle + " · " + currentCar + " · в списке " +
                displayRows.Count + filterNote +
                " · в базе: заправки " + fuelRows.Count +
                ", ремонт " + repairRows.Count + ", комм. " + utilityRows.Count;
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
        categoriesButton.Visibility = selectedSection == Repairs
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
            addCarButton.Enabled = enabled && !syncActive;
        syncButton.Enabled = true;
        fuelButton.Enabled = !syncActive;
        repairsButton.Enabled = !syncActive;
        utilitiesButton.Enabled = !syncActive;
        summaryButton.Enabled = !syncActive;
        recordList.Enabled = !syncActive;
        bool supportsFilter = selectedSection == Repairs || selectedSection == Fuel;
        filterButton.Text = (selectedSection == Fuel ? "Фильтр заправок" : "Фильтр") +
            (HasCurrentFilter() ? " ●" : "");
        filterButton.Enabled = enabled && !syncActive && supportsFilter;
        resetFilterButton.Enabled = enabled && !syncActive && supportsFilter && HasCurrentFilter();
        categoriesButton.Enabled = enabled && !syncActive && selectedSection == Repairs;
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
        filterButton.Text = (selectedSection == Fuel ? "Фильтр заправок" : "Фильтр") +
            (HasCurrentFilter() ? " ●" : "");
        filterButton.Enabled = !syncActive && supportsFilter && hasData;
        resetFilterButton.Enabled = supportsFilter &&
            !syncActive && hasData && HasCurrentFilter();
        categoriesButton.Enabled = !syncActive && selectedSection == Repairs && hasData;
        if (addCarButton != null)
            addCarButton.Enabled = hasData && !syncActive;
        addButton.Enabled = hasData && !syncActive;
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
        title.Text = "Не удалось открыть приложение";
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
                ? "Не удалось определить IP телефона. Проверьте подключение к Wi-Fi."
                : String.Join("\n", addresses);
            string pairingCode = System.Security.Cryptography.RandomNumberGenerator
                .GetInt32(100000, 1000000).ToString(CultureInfo.InvariantCulture);

            TcpListener listener = new TcpListener(IPAddress.Any, ExpenseSyncService.Port);
            listener.Start();
            syncListener = listener;
            syncActive = true;
            syncButton.Text = "Остановить синхронизацию";
            if (dataMenuButton != null)
                dataMenuButton.Text = "Данные ●";
            SetDataActionsEnabled(IsCompleteData(DataDirectory));
            UpdateRecordActions();

            new AlertDialog.Builder(this)
                .SetTitle("Офлайн-синхронизация")
                .SetMessage("Подключите КПК и телефон к одной Wi-Fi сети " +
                    "(интернет не нужен).\n\nIP телефона:\n" + addressText +
                    "\n\nКод подключения: " + pairingCode +
                    "\n\nНа КПК откройте Данные → Синхронизация по Wi-Fi.")
                .SetPositiveButton("Скрыть", delegate { })
                .SetNegativeButton("Остановить", delegate { StopWiFiSync(); })
                .Show();

            TcpClient acceptedClient = null;
            Task.Run(delegate
            {
                try
                {
                    string error = ExpenseSyncService.Serve(listener, pairingCode, DataDirectory,
                        CacheDir.AbsolutePath, ResolveSyncConflict,
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
                                ShowMessage("Синхронизация с КПК завершена.");
                            }
                            else
                                ShowMessage("Синхронизация не выполнена: " + error);
                        }
                    });
                }
                catch (Exception ex)
                {
                    if (syncActive)
                        RunOnUiThread(delegate
                        {
                            if (!IsFinishing)
                                ShowMessage("Синхронизация не выполнена: " + ex.Message);
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
                                syncButton.Text = "Синхронизация по Wi-Fi";
                                if (dataMenuButton != null)
                                    dataMenuButton.Text = "Данные";
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
            ShowMessage("Не удалось запустить синхронизацию: " + ex.Message);
        }
    }

    private SyncConflictChoice ResolveSyncConflict(
        string title, string localRow, string incomingRow)
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
                        .SetTitle("Конфликт: " + title)
                        .SetMessage("На Android:\n" + localRow + "\n\nНа КПК:\n" + incomingRow +
                            "\n\nКакую запись оставить?")
                        .SetCancelable(false)
                        .SetNegativeButton("Оставить Android", delegate
                        {
                            choice = SyncConflictChoice.KeepLocal;
                            completed.Set();
                        })
                        .SetPositiveButton("Оставить КПК", delegate
                        {
                            choice = SyncConflictChoice.KeepIncoming;
                            completed.Set();
                        })
                        .SetNeutralButton("Отменить синхронизацию", delegate
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
                if (!syncActive)
                    return SyncConflictChoice.Cancel;
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
            syncButton.Text = "Синхронизация по Wi-Fi";
            if (dataMenuButton != null)
                dataMenuButton.Text = "Данные";
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
        return "utilities.dat";
    }

    private string FormatRow(string[] row)
    {
        if (selectedSection == Fuel && row.Length >= 5)
        {
            string formatted = row[0] + " · " + row[2] + " км · " + row[3] +
                " л · " + row[4] + " BYN";
            decimal consumption = FuelConsumption(row);
            if (consumption > 0)
                formatted += " · " + consumption.ToString("0.0", CultureInfo.InvariantCulture) + " л/100";
            return formatted;
        }
        if (selectedSection == Repairs && row.Length >= 9)
        {
            string mileage = row[2].Length > 0 ? " · " + row[2] + " км" : "";
            return row[0] + mileage + " · " + row[3] + ": " + ShortText(row[4], 22) +
                " · " + row[7] + " BYN";
        }
        if (selectedSection == Utilities && row.Length >= 8)
            return row[0] + " · итого " + ExpenseDataCodec.Sum(row, 1)
                .ToString("0.##", CultureInfo.InvariantCulture) + " BYN";
        return String.Join(" | ", row);
    }

    private decimal FuelConsumption(string[] row)
    {
        decimal mileage;
        if (!TryNumber(row[2], out mileage))
            return 0;
        int index = allRows.IndexOf(row);
        for (int i = index - 1; i >= 0; i--)
        {
            if (allRows[i][1] != row[1])
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
            details.Append("Авто: ").Append(row[1])
                .Append("\nДата: ").Append(row[0])
                .Append("\nПробег: ").Append(row[2]).Append(" км")
                .Append("\nЛитры: ").Append(row[3])
                .Append("\nСтоимость: ").Append(row[4]).Append(" BYN");
            decimal liters;
            decimal cost;
            if (TryNumber(row[3], out liters) && liters > 0 && TryNumber(row[4], out cost))
                details.Append("\nЦена за литр: ")
                    .Append((cost / liters).ToString("0.00", CultureInfo.InvariantCulture))
                    .Append(" BYN");
            decimal consumption = FuelConsumption(row);
            if (consumption > 0)
                details.Append("\nРасход: ")
                    .Append(consumption.ToString("0.0", CultureInfo.InvariantCulture))
                    .Append(" л/100 км");
            return details.ToString();
        }
        if (selectedSection == Repairs && row.Length >= 9)
        {
            StringBuilder details = new StringBuilder();
            details.Append("Авто: ").Append(row[1])
                .Append("\nДата: ").Append(row[0])
                .Append("\nПробег: ").Append(row[2].Length > 0 ? row[2] + " км" : "—")
                .Append("\nКатегория: ").Append(row[3])
                .Append("\nТовар: ").Append(row[4])
                .Append("\nНаименование: ").Append(row[5].Length > 0 ? row[5] : "—")
                .Append("\nКоличество: ").Append(row[6])
                .Append("\nСумма: ").Append(row[7]).Append(" BYN")
                .Append("\nКомментарий: ").Append(row[8].Length > 0 ? row[8] : "—");
            return details.ToString();
        }
        if (selectedSection == Utilities && row.Length >= 8)
        {
            StringBuilder details = new StringBuilder("Месяц: ");
            details.Append(row[0]);
            for (int i = 0; i < ExpenseDataStore.UtilityLabels.Length; i++)
            {
                string amount = row[i + 1];
                if (amount == null || amount.Trim().Length == 0 || amount == "0")
                    continue;
                details.Append("\n").Append(ExpenseDataStore.UtilityLabels[i])
                    .Append(": ").Append(amount).Append(" BYN");
            }
            details.Append("\nИтого: ")
                .Append(ExpenseDataCodec.Sum(row, 1).ToString("0.##", CultureInfo.InvariantCulture))
                .Append(" BYN");
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
            .SetTitle("Запись")
            .SetMessage(FormatDetails(row))
            .SetPositiveButton("Закрыть", delegate { })
            .SetNeutralButton("Изменить", delegate { ShowRecordEditor(true, row, sourceIndex); })
            .SetNegativeButton("Удалить", delegate { ConfirmDelete(row, sourceIndex); })
            .Show();
    }

    private void AddRecord()
    {
        if (selectedSection == Fuel)
        {
            string lastMileage = LastCarMileage(currentCar);
            ShowRecordEditor(false, new string[]
            {
                DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                currentCar, lastMileage, "", ""
            }, -1);
        }
        else if (selectedSection == Repairs)
        {
            List<string> categories = ExpenseDataStore.LoadRepairCategories(DataDirectory);
            if (categories.Count == 0)
            {
                ShowMessage("Сначала добавьте категорию ремонта.");
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
        else
        {
            ShowRecordEditor(false, new string[]
            {
                DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                "", "", "", "", "", "", ""
            }, -1);
        }
    }

    private void EditSelectedRecord()
    {
        int index = selectedRecordPosition;
        if (index < 0 || index >= visibleRows.Count)
        {
            ShowMessage("Нажмите на запись в списке, чтобы выбрать её.");
            return;
        }
        ShowRecordEditor(true, visibleRows[index], visibleIndexes[index]);
    }

    private void DeleteSelectedRecord()
    {
        int index = selectedRecordPosition;
        if (index < 0 || index >= visibleRows.Count)
        {
            ShowMessage("Нажмите на запись в списке, чтобы выбрать её.");
            return;
        }
        ConfirmDelete(visibleRows[index], visibleIndexes[index]);
    }

    private void ShowRecordEditor(bool editing, string[] row, int sourceIndex)
    {
        string fileName = GetSelectedFile();
        List<EditorField> fields = new List<EditorField>();
        if (selectedSection == Fuel)
        {
            fields.Add(EditorField.Date("Дата", row[0], true));
            fields.Add(EditorField.Option("Автомобиль", row[1],
                ExpenseDataStore.LoadCarNames(DataDirectory).ToArray(), true));
            fields.Add(EditorField.Number(
                !editing && row[2].Length > 0
                    ? "Пробег, км (последняя запись)"
                    : "Пробег, км",
                row[2], true));
            fields.Add(EditorField.Number("Литры", row[3], true));
            fields.Add(EditorField.Number("Стоимость, BYN", row[4], true));
        }
        else if (selectedSection == Repairs)
        {
            fields.Add(EditorField.Date("Дата", row[0], true));
            fields.Add(EditorField.Option("Автомобиль", row[1],
                ExpenseDataStore.LoadCarNames(DataDirectory).ToArray(), true));
            fields.Add(EditorField.Number(
                !editing && row[2].Length > 0
                    ? "Пробег, км (последняя запись)"
                    : "Пробег, км",
                row[2], false));
            List<string> categories = ExpenseDataStore.LoadRepairCategories(DataDirectory);
            fields.Add(EditorField.Option("Категория", row[3],
                categories.ToArray(), true));
            fields.Add(EditorField.Text("Товар / деталь", row[4], true));
            fields.Add(EditorField.Text("Наименование", row[5], false));
            fields.Add(EditorField.Number("Количество", row[6], true));
            fields.Add(EditorField.Number("Сумма, BYN", row[7], true));
            fields.Add(EditorField.Text("Комментарий", row[8], false, true));
        }
        else
        {
            fields.Add(EditorField.Month("Месяц", row[0], true));
            for (int i = 0; i < ExpenseDataStore.UtilityLabels.Length; i++)
                fields.Add(EditorField.Number(
                    ExpenseDataStore.UtilityLabels[i] + ", BYN", row[i + 1], false));
        }

        RenderEditor(editing ? "Изменить запись" : "Новая запись", fields,
            delegate(string[] values)
            {
                if (selectedSection == Utilities && !HasUtilityAmount(values))
                    throw new InvalidOperationException("Введите хотя бы одну сумму.");
                if (editing)
                    ExpenseDataStore.UpdateRecord(DataDirectory, fileName, sourceIndex, values);
                else
                    ExpenseDataStore.AddRecord(DataDirectory, fileName, values);

                if (selectedSection != Utilities)
                    SetCurrentCar(values[1]);
                RefreshRecords();
            });
    }

    private string LastCarMileage(string car)
    {
        decimal best = -1;
        string bestText = "";
        RememberLatestMileage(ExpenseDataStore.Load(DataDirectory, "fuel.dat"), car, 2, ref best, ref bestText);
        RememberLatestMileage(ExpenseDataStore.Load(DataDirectory, "repairs.dat"), car, 2, ref best, ref bestText);
        return bestText;
    }

    private static void RememberLatestMileage(List<string[]> rows, string car, int mileageIndex,
        ref decimal best, ref string bestText)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Length <= mileageIndex || rows[i][1] != car)
                continue;
            decimal mileage;
            if (!TryNumber(rows[i][mileageIndex], out mileage) || mileage < 0)
                continue;
            if (mileage >= best)
            {
                best = mileage;
                bestText = rows[i][mileageIndex];
            }
        }
    }

    private void AddCar()
    {
        if (!IsCompleteData(DataDirectory))
        {
            ShowMessage("Сначала импортируйте копию через «Данные».");
            return;
        }
        if (syncActive)
        {
            ShowMessage("Дождитесь окончания синхронизации.");
            return;
        }
        List<EditorField> fields = new List<EditorField>
        {
            EditorField.Text("Название автомобиля", "", true)
        };
        RenderEditor("Добавить автомобиль", fields,
            delegate(string[] values)
            {
                string name = values[0].Trim();
                if (name.Length == 0)
                    throw new InvalidOperationException("Введите название автомобиля.");
                ExpenseDataStore.AddCar(DataDirectory, name);
                SetCurrentCar(name);
                RefreshRecords();
            });
    }

    private void SetCurrentCar(string name)
    {
        currentCar = name;
        GetSharedPreferences("settings", FileCreationMode.Private)
            .Edit().PutString("currentCar", currentCar).Apply();
    }

    private void RenderEditor(string title, List<EditorField> fields, Action<string[]> save)
    {
        ScrollView scroll = new ScrollView(this);
        LinearLayout form = new LinearLayout(this);
        form.Orientation = Orientation.Vertical;
        form.SetPadding(Dp(8), Dp(4), Dp(8), Dp(4));

        for (int i = 0; i < fields.Count; i++)
            AddEditorField(form, fields[i]);

        LinearLayout buttons = NewRow();
        Button saveButton = CreateButton("Сохранить", null);
        Button cancelButton = CreateButton("Отмена", null);
        buttons.AddView(saveButton, WeightedButton());
        buttons.AddView(cancelButton, WeightedButton());
        form.AddView(buttons, MatchWrap());
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
            try
            {
                save(values);
                dialog.Dismiss();
            }
            catch (Exception ex)
            {
                ShowMessage("Не удалось сохранить: " + ex.Message);
            }
        };
        cancelButton.Click += delegate { dialog.Dismiss(); };
        dialog.Show();
    }

    private void AddEditorField(LinearLayout form, EditorField field)
    {
        TextView label = new TextView(this);
        label.Text = field.Label;
        label.TextSize = 14;
        label.SetTextColor(AndroidColor.Rgb(35, 51, 70));
        label.SetPadding(0, Dp(6), 0, 0);
        form.AddView(label, MatchWrap());

        if (field.Options != null)
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
                enabled.Text = "Указать";
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
            if (field.Spinner != null)
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
                error = field.Label + " — обязательное поле.";
                return values;
            }
            if (values[i].Length == 0)
                continue;

            if (field.Kind == "number")
            {
                decimal number;
                if (!TryNumber(values[i], out number) || number < 0)
                {
                    error = field.Label + " — введите неотрицательное число.";
                    return values;
                }
                values[i] = number.ToString(CultureInfo.InvariantCulture);
            }
            else if (field.Kind == "date" &&
                !DateTime.TryParseExact(values[i], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime date))
            {
                error = "Выберите корректную дату.";
                return values;
            }
            else if (field.Kind == "month" &&
                !DateTime.TryParseExact(values[i] + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime month))
            {
                error = "Выберите корректный месяц.";
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
            ? row[0] + ", " + row[2] + " км, " + row[4] + " BYN"
            : selectedSection == Repairs
                ? row[0] + ", " + row[3] + ": " + ShortText(row[4], 30)
                : "коммунальные платежи за " + row[0];
        new AlertDialog.Builder(this)
            .SetTitle("Удалить запись?")
            .SetMessage(description)
            .SetNegativeButton("Отмена", delegate { })
            .SetPositiveButton("Удалить", delegate
            {
                try
                {
                    ExpenseDataStore.DeleteRecord(DataDirectory, GetSelectedFile(), sourceIndex);
                    RefreshRecords();
                }
                catch (Exception ex)
                {
                    ShowMessage("Не удалось удалить запись: " + ex.Message);
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
            EditorField.Option("Категория", filterCategory, categories.ToArray(), true),
            EditorField.Date("Дата с", filterDateFrom, false),
            EditorField.Date("Дата по", filterDateTo, false),
            EditorField.Text("Название / товар", filterText, false)
        };
        RenderEditor("Фильтр ремонта", fields,
            delegate(string[] values)
            {
                if (values[1].Length > 0 && values[2].Length > 0 &&
                    String.CompareOrdinal(values[1], values[2]) > 0)
                    throw new InvalidOperationException("Начало периода не может быть позже его окончания.");
                filterCategory = values[0];
                filterDateFrom = values[1];
                filterDateTo = values[2];
                filterText = values[3];
                RefreshRecords();
            });
    }

    private void EditFuelFilter()
    {
        List<EditorField> fields = new List<EditorField>
        {
            EditorField.Date("Дата с", fuelFilterDateFrom, false),
            EditorField.Date("Дата по", fuelFilterDateTo, false),
            EditorField.Text("Пробег / литры / стоимость", fuelFilterText, false)
        };
        RenderEditor("Фильтр заправок", fields,
            delegate(string[] values)
            {
                if (values[0].Length > 0 && values[1].Length > 0 &&
                    String.CompareOrdinal(values[0], values[1]) > 0)
                    throw new InvalidOperationException("Начало периода не может быть позже его окончания.");
                fuelFilterDateFrom = values[0];
                fuelFilterDateTo = values[1];
                fuelFilterText = values[2];
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
            ShowMessage("Сначала импортируйте резервную копию с КПК.");
            return;
        }
        List<string> categories = ExpenseDataStore.LoadRepairCategories(DataDirectory);
        AlertDialog.Builder builder = new AlertDialog.Builder(this)
            .SetTitle("Категории ремонта")
            .SetPositiveButton("Закрыть", delegate { })
            .SetNeutralButton("Добавить", delegate { AddRepairCategory(); });
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
            .SetItems(new string[] { "Переименовать", "Удалить" },
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
        RenderEditor("Новая категория", new List<EditorField>
        {
            EditorField.Text("Название", "", true)
        }, delegate(string[] values)
        {
            ExpenseDataStore.AddRepairCategory(DataDirectory, values[0]);
            RefreshRecords();
        });
    }

    private void RenameRepairCategory(string oldName)
    {
        RenderEditor("Переименовать категорию", new List<EditorField>
        {
            EditorField.Text("Новое название", oldName, true)
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
            .SetTitle("Удалить категорию?")
            .SetMessage("Записи не удаляются. Сначала перенесите записи из категории в другую.")
            .SetNegativeButton("Отмена", delegate { })
            .SetPositiveButton("Удалить", delegate
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
                    ShowMessage("Не удалось удалить категорию: " + ex.Message);
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
            fuelFilterText.Length > 0;
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
        if (fuelFilterText.Length > 0)
        {
            string text = (row[2] + " " + row[3] + " " + row[4]).ToLowerInvariant();
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
            ShowMessage("Сначала импортируйте резервную копию с КПК.");
            return;
        }
        try
        {
            ScrollView scroll = new ScrollView(this);
            TextView body = new TextView(this);
            body.Text = BuildSummary();
            body.TextSize = 14;
            body.SetTextIsSelectable(true);
            body.SetPadding(Dp(8), Dp(4), Dp(8), Dp(4));
            body.SetTextColor(AndroidColor.Rgb(35, 51, 70));
            scroll.AddView(body);
            new AlertDialog.Builder(this)
                .SetTitle("Сводка и ТО · " + currentCar)
                .SetView(scroll)
                .SetPositiveButton("Закрыть", delegate { })
                .Show();
        }
        catch (Exception ex)
        {
            ShowMessage("Не удалось построить сводку: " + ex.Message);
        }
    }

    private string BuildSummary()
    {
        List<string[]> fuels = ExpenseDataStore.Load(DataDirectory, "fuel.dat");
        List<string[]> repairs = ExpenseDataStore.Load(DataDirectory, "repairs.dat");
        List<string[]> utilities = ExpenseDataStore.Load(DataDirectory, "utilities.dat");
        decimal fuelTotal = 0;
        decimal repairTotal = 0;
        decimal utilityTotal = 0;
        decimal currentMileage = 0;
        decimal minimumMileage = Decimal.MaxValue;
        decimal maximumMileage = 0;
        decimal lastOilMileage = 0;
        decimal lastTimingMileage = 0;
        string lastOilDate = "";
        string lastTimingDate = "";
        Dictionary<string, decimal> byCategory = new Dictionary<string, decimal>();
        Dictionary<string, decimal[]> monthlyTotals = new Dictionary<string, decimal[]>();
        Dictionary<string, decimal> carTotals = new Dictionary<string, decimal>();
        List<string> cars = ExpenseDataStore.LoadCarNames(DataDirectory);
        for (int i = 0; i < cars.Count; i++)
            carTotals.Add(cars[i], 0);

        for (int i = 0; i < fuels.Count; i++)
        {
            string[] row = fuels[i];
            decimal amount = NumberOrZero(row[4]);
            AddMonthlyAmount(monthlyTotals, row[0], 0, amount);
            if (carTotals.ContainsKey(row[1]))
                carTotals[row[1]] += amount;
            if (row[1] != currentCar)
                continue;
            fuelTotal += amount;
            AddMileageReading(row[2], ref currentMileage, ref minimumMileage, ref maximumMileage);
        }
        for (int i = 0; i < repairs.Count; i++)
        {
            string[] row = repairs[i];
            decimal amount = NumberOrZero(row[7]);
            AddMonthlyAmount(monthlyTotals, row[0], 1, amount);
            if (carTotals.ContainsKey(row[1]))
                carTotals[row[1]] += amount;
            if (row[1] != currentCar)
                continue;
            repairTotal += amount;
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
        for (int i = 0; i < utilities.Count; i++)
        {
            decimal amount = ExpenseDataCodec.Sum(utilities[i], 1);
            utilityTotal += amount;
            AddMonthlyAmount(monthlyTotals, utilities[i][0], 2, amount);
        }

        StringBuilder result = new StringBuilder(currentCar);
        result.Append("\n\nЗаправки: ").Append(Money(fuelTotal)).Append(" BYN")
            .Append("\nРемонт и ТО: ").Append(Money(repairTotal)).Append(" BYN")
            .Append("\nУчтённые расходы на км (примерно): ")
            .Append(minimumMileage != Decimal.MaxValue && maximumMileage > minimumMileage
                ? Money((fuelTotal + repairTotal) / (maximumMileage - minimumMileage)) + " BYN/км"
                : "недостаточно данных о пробеге")
            .Append("\n\nРемонт по категориям:");
        foreach (KeyValuePair<string, decimal> pair in byCategory)
            result.Append("\n").Append(pair.Key).Append(": ").Append(Money(pair.Value)).Append(" BYN");
        result.Append("\n\nКоммунальные за весь период: ").Append(Money(utilityTotal)).Append(" BYN")
            .Append("\n\nРасходы по месяцам (последние 12):");
        List<string> months = new List<string>(monthlyTotals.Keys);
        months.Sort(StringComparer.Ordinal);
        int firstMonth = Math.Max(0, months.Count - 12);
        for (int i = firstMonth; i < months.Count; i++)
        {
            decimal[] totals = monthlyTotals[months[i]];
            result.Append("\n").Append(months[i])
                .Append(": топливо ").Append(Money(totals[0]))
                .Append(", ремонт ").Append(Money(totals[1]))
                .Append(", коммунальные ").Append(Money(totals[2])).Append(" BYN");
        }
        result.Append("\n\nТопливо и ремонт по автомобилям:");
        for (int i = 0; i < cars.Count; i++)
            result.Append("\n").Append(cars[i]).Append(": ")
                .Append(Money(carTotals[cars[i]])).Append(" BYN");
        result
            .Append("\n\nТекущий пробег: ").Append(Money(currentMileage)).Append(" км")
            .Append("\n").Append(ServiceStatus("Замена масла", currentMileage,
                lastOilMileage, lastOilDate, 10000, 1000))
            .Append("\n").Append(ServiceStatus("Ремень ГРМ", currentMileage,
                lastTimingMileage, lastTimingDate, 60000, 5000));
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
            monthly = new decimal[3];
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
            ShowMessage("Резервная копия создана в приложении: " + name +
                "\nДля защиты от потери телефона дополнительно используйте «Экспорт ZIP».");
        }
        catch (Exception ex)
        {
            ShowMessage("Не удалось создать резервную копию: " + ex.Message);
        }
    }

    private void RestoreLocalBackup()
    {
        try
        {
            List<string> backups = ExpenseDataStore.ListBackupNames(DataDirectory);
            if (backups.Count == 0)
            {
                ShowMessage("На телефоне нет резервных копий.");
                return;
            }
            string[] names = backups.ToArray();
            new AlertDialog.Builder(this)
                .SetTitle("Выберите резервную копию")
                .SetItems(names, delegate(object sender, DialogClickEventArgs e)
                {
                    ConfirmRestore(names[e.Which]);
                })
                .SetNegativeButton("Отмена", delegate { })
                .Show();
        }
        catch (Exception ex)
        {
            ShowMessage("Не удалось прочитать резервные копии: " + ex.Message);
        }
    }

    private void ConfirmRestore(string name)
    {
        new AlertDialog.Builder(this)
            .SetTitle("Восстановить копию?")
            .SetMessage("Текущие данные сначала сохранятся отдельной копией. Заменить их копией " + name + "?")
            .SetNegativeButton("Отмена", delegate { })
            .SetPositiveButton("Восстановить", delegate
            {
                try
                {
                    ExpenseDataStore.RestoreBackup(DataDirectory, name);
                    List<string> cars = ExpenseDataStore.LoadCarNames(DataDirectory);
                    if (cars.IndexOf(currentCar) < 0)
                        SetCurrentCar(cars[0]);
                    RefreshRecords();
                    ShowMessage("Данные восстановлены.");
                }
                catch (Exception ex)
                {
                    ShowMessage("Не удалось восстановить копию: " + ex.Message);
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
            ShowMessage("Сначала импортируйте резервную копию с КПК.");
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
            throw new InvalidDataException("Не выбраны файлы резервной копии.");

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
        StringBuilder summary = new StringBuilder("Автомобили: ");
        for (int i = 0; i < cars.Count; i++)
        {
            if (i > 0)
                summary.Append(", ");
            summary.Append(cars[i][0]);
        }
        summary.Append("\n\nЗаправки: ").Append(fuel.Count)
            .Append("\nРемонт и ТО: ").Append(repairs.Count)
            .Append("\nКоммунальные записи: ").Append(utilities.Count)
            .Append("\nКатегории ремонта: ")
            .Append(ExpenseDataStore.LoadRepairCategories(source).Count);
        summary.Append(IsCompleteData(DataDirectory)
            ? "\n\nТекущие данные сначала сохранятся отдельной копией."
            : "\n\nИмпорт заменит текущий набор данных.");

        new AlertDialog.Builder(this)
            .SetTitle("Проверка копии")
            .SetMessage(summary.ToString())
            .SetNegativeButton("Отмена", delegate { DeleteImportStaging(source); })
            .SetPositiveButton("Заменить данные", delegate
            {
                try
                {
                    InstallImportedData(source);
                    RefreshRecords();
                    ShowMessage("Импорт завершён. Данные сохранены на телефоне.");
                }
                catch (Exception ex)
                {
                    ShowMessage("Импорт не выполнен: " + ex.Message);
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
            throw new IOException("Не удалось открыть выбранный ZIP-файл.");
        using (input)
        using (ZipArchive archive = new ZipArchive(input, ZipArchiveMode.Read))
        {
            for (int i = 0; i < archive.Entries.Count; i++)
            {
                ZipArchiveEntry entry = archive.Entries[i];
                string name = Path.GetFileName(entry.FullName);
                if (!ExpenseDataCodec.IsDataFile(name))
                    continue;
                using (Stream entryStream = entry.Open())
                using (FileStream output = File.Create(Path.Combine(destination, name)))
                    entryStream.CopyTo(output);
            }
        }
    }

    private void CopyUriToFile(Uri uri, string destination)
    {
        Stream input = ContentResolver.OpenInputStream(uri);
        if (input == null)
            throw new IOException("Не удалось открыть выбранный файл.");
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
            safetyBackup = ExpenseDataStore.CreateBackup(DataDirectory);

        try
        {
            CopyData(source, DataDirectory);
            ExpenseDataCodec.ValidateDirectory(DataDirectory);
        }
        catch (Exception importError)
        {
            if (safetyBackup != null)
            {
                string safetyPath = Path.Combine(Path.GetDirectoryName(DataDirectory),
                    "Backups", safetyBackup);
                try
                {
                    CopyData(safetyPath, DataDirectory);
                }
                catch (Exception rollbackError)
                {
                    throw new IOException("Импорт не завершён: " + importError.Message +
                        ". Возврат прежних данных не удался: " + rollbackError.Message);
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
            throw new InvalidDataException("Не выбрано место сохранения ZIP-файла.");
        ExpenseDataStore.EnsureRepairCategories(DataDirectory);
        ExpenseDataCodec.ValidateDirectory(DataDirectory);
        Stream output = ContentResolver.OpenOutputStream(destination);
        if (output == null)
            throw new IOException("Не удалось создать ZIP-файл.");
        using (output)
        using (ZipArchive archive = new ZipArchive(output, ZipArchiveMode.Create))
        {
            for (int i = 0; i < DataFiles.Length; i++)
            {
                ZipArchiveEntry entry = archive.CreateEntry(DataFiles[i], CompressionLevel.Optimal);
                using (Stream entryStream = entry.Open())
                using (FileStream input = File.OpenRead(Path.Combine(DataDirectory, DataFiles[i])))
                    input.CopyTo(entryStream);
            }
        }
    }

    private static void CopyData(string source, string destination)
    {
        ExpenseDataStore.CopyDataFiles(source, destination);
    }

    private static bool IsCompleteData(string directory)
    {
        if (!Directory.Exists(directory))
            return false;
        for (int i = 0; i < ExpenseDataCodec.CoreDataFiles.Length; i++)
            if (!File.Exists(Path.Combine(directory, ExpenseDataCodec.CoreDataFiles[i])))
                return false;
        return true;
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

    private void ShowMessage(string message)
    {
        bool looksLikeError = message != null && (
            message.IndexOf("не удалось", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("не выполнен", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("Ошибка", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("Сначала", StringComparison.OrdinalIgnoreCase) >= 0 ||
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
            .SetPositiveButton("ОК", delegate { })
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
    }
}
