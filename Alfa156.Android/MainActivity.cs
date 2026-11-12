using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidColor = global::Android.Graphics.Color;
using AndroidLayout = global::Android.Resource.Layout;
using Uri = global::Android.Net.Uri;

namespace Alfa156.Android;

[Activity(
    Label = "Alfa 156",
    Icon = "@drawable/icon",
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
    private Button addButton;
    private Button editButton;
    private Button deleteButton;
    private Button filterButton;
    private Button resetFilterButton;
    private Button backupButton;
    private Button restoreButton;
    private string selectedSection = Repairs;
    private string currentCar = "";
    private string filterCategory = AllCategories;
    private string filterDateFrom = "";
    private string filterDateTo = "";
    private string filterText = "";
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
            currentCar = GetSharedPreferences("settings", FileCreationMode.Private)
                .GetString("currentCar", "");
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
                RefreshRecords();
                ShowMessage("Импорт завершён. Данные сохранены на телефоне.");
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

        TextView title = new TextView(this);
        title.Text = "Alfa 156";
        title.TextSize = 23;
        title.SetTextColor(AndroidColor.Rgb(35, 51, 70));
        root.AddView(title, MatchWrap());

        LinearLayout transferRow = NewRow();
        transferRow.AddView(CreateButton("Импорт копии", delegate { ChooseImportFiles(); }), WeightedButton());
        transferRow.AddView(CreateButton("Экспорт ZIP", delegate { ChooseExportFile(); }), WeightedButton());
        root.AddView(transferRow);

        LinearLayout carRow = NewRow();
        TextView carLabel = new TextView(this);
        carLabel.Text = "Авто";
        carLabel.TextSize = 14;
        carLabel.SetTextColor(AndroidColor.Rgb(35, 51, 70));
        carRow.AddView(carLabel, new LinearLayout.LayoutParams(Dp(42), ViewGroup.LayoutParams.WrapContent));
        carSpinner = new Spinner(this);
        carSpinner.ItemSelected += CarSelected;
        carRow.AddView(carSpinner, new LinearLayout.LayoutParams(0,
            ViewGroup.LayoutParams.WrapContent, 1));
        carRow.AddView(CreateButton("Добавить", delegate { AddCar(); }),
            new LinearLayout.LayoutParams(Dp(100), ViewGroup.LayoutParams.WrapContent));
        root.AddView(carRow);

        LinearLayout sectionRow = NewRow();
        fuelButton = CreateButton("Заправки", delegate { SelectSection(Fuel); });
        repairsButton = CreateButton("Ремонт и ТО", delegate { SelectSection(Repairs); });
        utilitiesButton = CreateButton("Коммунальные", delegate { SelectSection(Utilities); });
        Button summaryButton = CreateButton("Сводка и ТО", delegate { ShowSummary(); });
        sectionRow.AddView(fuelButton, WeightedButton());
        sectionRow.AddView(repairsButton, WeightedButton());
        sectionRow.AddView(utilitiesButton, WeightedButton());
        sectionRow.AddView(summaryButton, WeightedButton());
        root.AddView(sectionRow);

        LinearLayout recordActions = NewRow();
        addButton = CreateButton("Добавить", delegate { AddRecord(); });
        editButton = CreateButton("Изменить", delegate { EditSelectedRecord(); });
        deleteButton = CreateButton("Удалить", delegate { DeleteSelectedRecord(); });
        recordActions.AddView(addButton, WeightedButton());
        recordActions.AddView(editButton, WeightedButton());
        recordActions.AddView(deleteButton, WeightedButton());
        root.AddView(recordActions);

        LinearLayout maintenanceActions = NewRow();
        filterButton = CreateButton("Фильтр ремонта", delegate { EditRepairFilter(); });
        resetFilterButton = CreateButton("Сбросить фильтр", delegate { ResetRepairFilter(); });
        maintenanceActions.AddView(filterButton, WeightedButton());
        maintenanceActions.AddView(resetFilterButton, WeightedButton());
        root.AddView(maintenanceActions);

        LinearLayout backupActions = NewRow();
        backupButton = CreateButton("Копия на телефоне", delegate { CreateLocalBackup(); });
        restoreButton = CreateButton("Восстановить", delegate { RestoreLocalBackup(); });
        backupActions.AddView(backupButton, WeightedButton());
        backupActions.AddView(restoreButton, WeightedButton());
        root.AddView(backupActions);

        status = new TextView(this);
        status.TextSize = 12;
        status.SetTextColor(AndroidColor.Rgb(60, 70, 80));
        status.SetPadding(0, Dp(4), 0, Dp(4));
        root.AddView(status, MatchWrap());

        recordList = new ListView(this);
        recordList.ChoiceMode = ChoiceMode.Single;
        recordList.ItemClick += RecordSelected;
        root.AddView(recordList, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, 0, 1));
        SetContentView(root);
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
        return new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
    }

    private Button CreateButton(string text, EventHandler clickHandler)
    {
        Button button = new Button(this);
        button.Text = text;
        button.TextSize = 11;
        button.SetMinHeight(Dp(40));
        button.SetPadding(Dp(3), 0, Dp(3), 0);
        button.Click += clickHandler;
        return button;
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
        UpdateSectionButtons();
        RefreshRecords();
    }

    private void UpdateSectionButtons()
    {
        SetSectionButton(fuelButton, selectedSection == Fuel);
        SetSectionButton(repairsButton, selectedSection == Repairs);
        SetSectionButton(utilitiesButton, selectedSection == Utilities);
    }

    private static void SetSectionButton(Button button, bool selected)
    {
        if (button == null)
            return;
        button.SetBackgroundColor(selected
            ? AndroidColor.Rgb(49, 77, 102)
            : AndroidColor.Rgb(225, 231, 237));
        button.SetTextColor(selected ? AndroidColor.White :
            AndroidColor.Rgb(35, 51, 70));
    }

    private void RefreshRecords()
    {
        bool hasData = IsCompleteData(DataDirectory);
        SetDataActionsEnabled(hasData);
        if (!hasData)
        {
            status.Text = "Импортируйте копию с КПК, чтобы начать работу.";
            allRows = new List<string[]>();
            visibleRows = new List<string[]>();
            visibleIndexes = new List<int>();
            selectedRecordPosition = -1;
            recordList.Adapter = CreateAdapter(new List<string>());
            SetCarOptions(new List<string>());
            return;
        }

        List<string> cars = ExpenseDataStore.LoadCarNames(DataDirectory);
        if (cars.IndexOf(currentCar) < 0)
            currentCar = cars[0];
        SetCarOptions(cars);
        GetSharedPreferences("settings", FileCreationMode.Private)
            .Edit().PutString("currentCar", currentCar).Apply();

        List<string[]> fuelRows = ExpenseDataStore.Load(DataDirectory, "fuel.dat");
        List<string[]> repairRows = ExpenseDataStore.Load(DataDirectory, "repairs.dat");
        List<string[]> utilityRows = ExpenseDataStore.Load(DataDirectory, "utilities.dat");
        List<string[]> carRows = ExpenseDataCodec.ReadRows(Path.Combine(DataDirectory, "cars.dat"));
        status.Text = "Авто: " + currentCar + " · Заправок: " + fuelRows.Count +
            " · Ремонт/ТО: " + repairRows.Count + " · Коммунальных месяцев: " + utilityRows.Count +
            (HasRepairFilter() && selectedSection == Repairs ? " · фильтр включён" : "");

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
            visibleRows.Add(row);
            visibleIndexes.Add(i);
            displayRows.Add(FormatRow(row));
        }
        recordList.Adapter = CreateAdapter(displayRows);
        UpdateRecordActions();
        UpdateSectionButtons();
    }

    private void SetDataActionsEnabled(bool enabled)
    {
        addButton.Enabled = enabled;
        backupButton.Enabled = enabled;
        restoreButton.Enabled = enabled;
        filterButton.Enabled = enabled && selectedSection == Repairs;
        resetFilterButton.Enabled = enabled && selectedSection == Repairs && HasRepairFilter();
        carSpinner.Enabled = enabled;
    }

    private void UpdateRecordActions()
    {
        bool selected = selectedRecordPosition >= 0 &&
            selectedRecordPosition < visibleRows.Count;
        editButton.Enabled = selected;
        deleteButton.Enabled = selected;
        filterButton.Enabled = selectedSection == Repairs && IsCompleteData(DataDirectory);
        resetFilterButton.Enabled = selectedSection == Repairs &&
            IsCompleteData(DataDirectory) && HasRepairFilter();
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
            string formatted = row[0] + "  " + row[2] + " км  " + row[3] +
                " л  " + row[4] + " BYN";
            decimal consumption = FuelConsumption(row);
            if (consumption > 0)
                formatted += "  " + consumption.ToString("0.0", CultureInfo.InvariantCulture) + " л/100";
            return formatted;
        }
        if (selectedSection == Repairs && row.Length >= 9)
            return row[0] + "  " + row[3] + ": " + ShortText(row[4], 20) +
                "  " + row[7] + " BYN";
        if (selectedSection == Utilities && row.Length >= 8)
            return row[0] + "  итого " + ExpenseDataCodec.Sum(row, 1)
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
            return 0;
        }
        return 0;
    }

    private string FormatDetails(string[] row)
    {
        if (selectedSection == Fuel && row.Length >= 5)
            return "Авто: " + row[1] + "\nДата: " + row[0] +
                "\nПробег: " + row[2] + " км\nЛитры: " + row[3] +
                "\nСтоимость: " + row[4] + " BYN";
        if (selectedSection == Repairs && row.Length >= 9)
            return "Авто: " + row[1] + "\nДата: " + row[0] +
                "\nПробег: " + row[2] + " км\nКатегория: " + row[3] +
                "\nТовар: " + row[4] + "\nНаименование: " + row[5] +
                "\nКоличество: " + row[6] + "\nСумма: " + row[7] +
                " BYN\nКомментарий: " + row[8];
        if (selectedSection == Utilities && row.Length >= 8)
        {
            StringBuilder details = new StringBuilder("Месяц: ");
            details.Append(row[0]);
            for (int i = 0; i < ExpenseDataStore.UtilityLabels.Length; i++)
                details.Append("\n").Append(ExpenseDataStore.UtilityLabels[i])
                    .Append(": ").Append(row[i + 1]).Append(" BYN");
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
            ShowRecordEditor(false, new string[]
            {
                DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                currentCar, "", "", ""
            }, -1);
        }
        else if (selectedSection == Repairs)
        {
            ShowRecordEditor(false, new string[]
            {
                DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                currentCar, "", ExpenseDataStore.RepairCategories[0], "", "", "1", "", ""
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
            fields.Add(EditorField.Number("Пробег, км", row[2], true));
            fields.Add(EditorField.Number("Литры", row[3], true));
            fields.Add(EditorField.Number("Стоимость, BYN", row[4], true));
        }
        else if (selectedSection == Repairs)
        {
            fields.Add(EditorField.Date("Дата", row[0], true));
            fields.Add(EditorField.Option("Автомобиль", row[1],
                ExpenseDataStore.LoadCarNames(DataDirectory).ToArray(), true));
            fields.Add(EditorField.Number("Пробег, км", row[2], false));
            fields.Add(EditorField.Option("Категория", row[3],
                ExpenseDataStore.RepairCategories, true));
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

    private void AddCar()
    {
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
        categories.AddRange(ExpenseDataStore.RepairCategories);
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

    private void ResetRepairFilter()
    {
        filterCategory = AllCategories;
        filterDateFrom = "";
        filterDateTo = "";
        filterText = "";
        RefreshRecords();
    }

    private bool HasRepairFilter()
    {
        return filterCategory != AllCategories || filterDateFrom.Length > 0 ||
            filterDateTo.Length > 0 || filterText.Length > 0;
    }

    private bool MatchesRepairFilter(string[] row)
    {
        if (filterCategory != AllCategories && row[3] != filterCategory)
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

    private void ShowSummary()
    {
        if (!IsCompleteData(DataDirectory))
        {
            ShowMessage("Сначала импортируйте резервную копию с КПК.");
            return;
        }
        try
        {
            new AlertDialog.Builder(this)
                .SetTitle("Сводка и ТО")
                .SetMessage(BuildSummary())
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
        decimal lastOilMileage = 0;
        decimal lastTimingMileage = 0;
        Dictionary<string, decimal> byCategory = new Dictionary<string, decimal>();

        for (int i = 0; i < fuels.Count; i++)
        {
            string[] row = fuels[i];
            if (row[1] != currentCar)
                continue;
            fuelTotal += NumberOrZero(row[4]);
            currentMileage = Math.Max(currentMileage, NumberOrZero(row[2]));
        }
        for (int i = 0; i < repairs.Count; i++)
        {
            string[] row = repairs[i];
            if (row[1] != currentCar)
                continue;
            decimal amount = NumberOrZero(row[7]);
            decimal mileage = NumberOrZero(row[2]);
            repairTotal += amount;
            currentMileage = Math.Max(currentMileage, mileage);
            if (!byCategory.ContainsKey(row[3]))
                byCategory.Add(row[3], 0);
            byCategory[row[3]] += amount;
            if (row[3] == "ТО Масло")
                lastOilMileage = Math.Max(lastOilMileage, mileage);
            if (row[3] == "ТО ГРМ")
                lastTimingMileage = Math.Max(lastTimingMileage, mileage);
        }
        for (int i = 0; i < utilities.Count; i++)
            utilityTotal += ExpenseDataCodec.Sum(utilities[i], 1);

        StringBuilder result = new StringBuilder(currentCar);
        result.Append("\n\nЗаправки: ").Append(Money(fuelTotal)).Append(" BYN")
            .Append("\nРемонт и ТО: ").Append(Money(repairTotal)).Append(" BYN")
            .Append("\n\nРемонт по категориям:");
        foreach (KeyValuePair<string, decimal> pair in byCategory)
            result.Append("\n").Append(pair.Key).Append(": ").Append(Money(pair.Value)).Append(" BYN");
        result.Append("\n\nКоммунальные за весь период: ").Append(Money(utilityTotal)).Append(" BYN")
            .Append("\n\nТекущий пробег: ").Append(Money(currentMileage)).Append(" км")
            .Append("\n").Append(ServiceStatus("Замена масла", currentMileage, lastOilMileage, 10000, 1000))
            .Append("\n").Append(ServiceStatus("Ремень ГРМ", currentMileage, lastTimingMileage, 60000, 5000));
        return result.ToString();
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
            InstallImportedData(staging);
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
        }
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
        Directory.CreateDirectory(destination);
        for (int i = 0; i < DataFiles.Length; i++)
            File.Copy(Path.Combine(source, DataFiles[i]),
                Path.Combine(destination, DataFiles[i]), true);
    }

    private static bool IsCompleteData(string directory)
    {
        if (!Directory.Exists(directory))
            return false;
        for (int i = 0; i < DataFiles.Length; i++)
            if (!File.Exists(Path.Combine(directory, DataFiles[i])))
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
        decimal lastMileage, decimal interval, decimal warningDistance)
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

    private void ShowMessage(string message)
    {
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
