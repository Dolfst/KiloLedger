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
    private static readonly string[] DataFiles = ExpenseDataCodec.DataFiles;

    private TextView status;
    private ListView recordList;
    private Button fuelButton;
    private Button repairsButton;
    private Button utilitiesButton;
    private string selectedSection = Repairs;
    private List<string[]> visibleRows = new List<string[]>();

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
                ShowMessage("Резервная копия ZIP сохранена. Её можно перенести на компьютер или другое устройство.");
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
        root.SetPadding(Dp(16), Dp(16), Dp(16), Dp(8));
        root.SetBackgroundColor(AndroidColor.Rgb(245, 247, 250));

        TextView title = new TextView(this);
        title.Text = "Alfa 156";
        title.TextSize = 25;
        title.SetTextColor(AndroidColor.Rgb(35, 51, 70));
        root.AddView(title, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

        LinearLayout transferButtons = new LinearLayout(this);
        transferButtons.Orientation = Orientation.Horizontal;
        Button importButton = CreateButton("Импорт копии", delegate { ChooseImportFiles(); });
        Button exportButton = CreateButton("Экспорт ZIP", delegate { ChooseExportFile(); });
        transferButtons.AddView(importButton, new LinearLayout.LayoutParams(0,
            ViewGroup.LayoutParams.WrapContent, 1));
        transferButtons.AddView(exportButton, new LinearLayout.LayoutParams(0,
            ViewGroup.LayoutParams.WrapContent, 1));
        root.AddView(transferButtons);

        LinearLayout sections = new LinearLayout(this);
        sections.Orientation = Orientation.Horizontal;
        fuelButton = CreateButton("Заправки", delegate { SelectSection(Fuel); });
        repairsButton = CreateButton("Ремонт", delegate { SelectSection(Repairs); });
        utilitiesButton = CreateButton("Коммунальные", delegate { SelectSection(Utilities); });
        sections.AddView(fuelButton, new LinearLayout.LayoutParams(0,
            ViewGroup.LayoutParams.WrapContent, 1));
        sections.AddView(repairsButton, new LinearLayout.LayoutParams(0,
            ViewGroup.LayoutParams.WrapContent, 1));
        sections.AddView(utilitiesButton, new LinearLayout.LayoutParams(0,
            ViewGroup.LayoutParams.WrapContent, 1));
        root.AddView(sections);

        status = new TextView(this);
        status.TextSize = 14;
        status.SetTextColor(AndroidColor.Rgb(60, 70, 80));
        status.SetPadding(0, Dp(8), 0, Dp(8));
        root.AddView(status, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

        recordList = new ListView(this);
        recordList.ItemClick += RecordSelected;
        root.AddView(recordList, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, 0, 1));
        SetContentView(root);
        UpdateSectionButtons();
    }

    private Button CreateButton(string text, EventHandler clickHandler)
    {
        Button button = new Button(this);
        button.Text = text;
        button.TextSize = 12;
        button.Click += clickHandler;
        return button;
    }

    private int Dp(int value)
    {
        return (int)(value * Resources.DisplayMetrics.Density + 0.5f);
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
        button.SetBackgroundColor(selected
            ? AndroidColor.Rgb(49, 77, 102)
            : AndroidColor.Rgb(225, 231, 237));
        button.SetTextColor(selected ? AndroidColor.White :
            AndroidColor.Rgb(35, 51, 70));
    }

    private void RefreshRecords()
    {
        if (!IsCompleteData(DataDirectory))
        {
            status.Text = "Импортируйте копию с КПК, чтобы просматривать записи.";
            visibleRows = new List<string[]>();
            recordList.Adapter = CreateAdapter(new List<string>());
            return;
        }

        List<string[]> fuelRows = ExpenseDataCodec.ReadRows(Path.Combine(DataDirectory, "fuel.dat"));
        List<string[]> repairRows = ExpenseDataCodec.ReadRows(Path.Combine(DataDirectory, "repairs.dat"));
        List<string[]> utilityRows = ExpenseDataCodec.ReadRows(Path.Combine(DataDirectory, "utilities.dat"));
        List<string[]> carRows = ExpenseDataCodec.ReadRows(Path.Combine(DataDirectory, "cars.dat"));
        status.Text = "Автомобилей: " + carRows.Count + "; заправок: " + fuelRows.Count +
            "; ремонт/ТО: " + repairRows.Count + "; коммунальных месяцев: " + utilityRows.Count;

        visibleRows = ExpenseDataCodec.ReadRows(Path.Combine(DataDirectory, GetSelectedFile()));
        List<string> displayRows = new List<string>();
        for (int i = visibleRows.Count - 1; i >= 0; i--)
            displayRows.Add(FormatRow(visibleRows[i]));
        recordList.Adapter = CreateAdapter(displayRows);
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
        root.AddView(title, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

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
            return row[0] + "  " + row[1] + "  " + row[3] + " л  " + row[4] + " BYN";
        if (selectedSection == Repairs && row.Length >= 9)
            return row[0] + "  " + row[3] + ": " + row[4] + "  " + row[7] + " BYN";
        if (selectedSection == Utilities && row.Length >= 8)
            return row[0] + "  итого " + ExpenseDataCodec.Sum(row, 1)
                .ToString("0.##", CultureInfo.InvariantCulture) + " BYN";
        return String.Join(" | ", row);
    }

    private string FormatDetails(string[] row)
    {
        if (selectedSection == Fuel && row.Length >= 5)
            return "Дата: " + row[0] + "\r\nАвтомобиль: " + row[1] +
                "\r\nПробег: " + row[2] + " км\r\nЛитры: " + row[3] +
                "\r\nСтоимость: " + row[4] + " BYN";
        if (selectedSection == Repairs && row.Length >= 9)
            return "Дата: " + row[0] + "\r\nАвтомобиль: " + row[1] +
                "\r\nПробег: " + row[2] + " км\r\nКатегория: " + row[3] +
                "\r\nТовар: " + row[4] + "\r\nНаименование: " + row[5] +
                "\r\nКоличество: " + row[6] + "\r\nСумма: " + row[7] +
                " BYN\r\nКомментарий: " + row[8];
        if (selectedSection == Utilities && row.Length >= 8)
        {
            string[] labels = {
                "Коммунальные платежи", "Электроэнергия", "Газ",
                "Стационарный телефон", "Домашний интернет",
                "Налог на аренду", "Аренда жилья"
            };
            StringBuilder details = new StringBuilder("Месяц: ");
            details.Append(row[0]);
            for (int i = 0; i < labels.Length; i++)
                details.Append("\r\n").Append(labels[i]).Append(": ").Append(row[i + 1]).Append(" BYN");
            details.Append("\r\nИтого: ")
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
        int sourceIndex = visibleRows.Count - 1 - e.Position;
        new AlertDialog.Builder(this)
            .SetTitle("Запись")
            .SetMessage(FormatDetails(visibleRows[sourceIndex]))
            .SetPositiveButton("Закрыть", delegate { })
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
                else if (IsDataFile(displayName))
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
                if (!IsDataFile(name))
                    continue;
                string filePath = Path.Combine(destination, name);
                using (Stream entryStream = entry.Open())
                using (FileStream output = File.Create(filePath))
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
        {
            string backupRoot = Path.Combine(FilesDir.AbsolutePath, "Backups");
            Directory.CreateDirectory(backupRoot);
            safetyBackup = Path.Combine(backupRoot,
                DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
            int suffix = 1;
            while (Directory.Exists(safetyBackup))
            {
                safetyBackup = Path.Combine(backupRoot,
                    DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_" + suffix);
                suffix++;
            }
            CopyData(DataDirectory, safetyBackup);
        }

        Directory.CreateDirectory(DataDirectory);
        try
        {
            CopyData(source, DataDirectory);
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
                    throw new IOException("Импорт не завершён: " + importError.Message +
                        ". Возврат прежних данных не удался: " + rollbackError.Message);
                }
            }
            throw;
        }
    }

    private void ExportTo(Uri destination)
    {
        if (destination == null)
            throw new InvalidDataException("Не выбрано место сохранения ZIP-файла.");
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

    private static bool IsDataFile(string name)
    {
        return ExpenseDataCodec.IsDataFile(name);
    }

    private void ShowMessage(string message)
    {
        new AlertDialog.Builder(this)
            .SetTitle("Alfa 156")
            .SetMessage(message)
            .SetPositiveButton("ОК", delegate { })
            .Show();
    }
}
