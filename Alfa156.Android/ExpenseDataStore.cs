using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Alfa156.Android;

internal static class ExpenseDataStore
{
    internal static readonly string[] RepairCategories =
    {
        "ТО Масло", "ТО ГРМ", "Запчасти", "Жидкости", "Тормоза", "Кузов", "Расходники"
    };

    internal static readonly string[] UtilityLabels =
    {
        "Коммунальные платежи", "Электроэнергия", "Газ", "Стационарный телефон",
        "Домашний интернет", "Налог на аренду", "Аренда жилья"
    };

    internal static List<string> LoadCarNames(string directory)
    {
        List<string[]> rows = ExpenseDataCodec.ReadRows(Path.Combine(directory, "cars.dat"));
        List<string> names = new List<string>();
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].Length == 1 && rows[i][0].Length > 0)
                names.Add(rows[i][0]);
        if (names.Count == 0)
            throw new InvalidDataException("Список автомобилей пуст. Добавьте автомобиль.");
        return names;
    }

    internal static List<string[]> Load(string directory, string fileName)
    {
        if (!ExpenseDataCodec.IsDataFile(fileName) || fileName == "cars.dat")
            throw new ArgumentException("Неверный файл журнала.", "fileName");
        return ExpenseDataCodec.ReadRows(Path.Combine(directory, fileName));
    }

    internal static void AddCar(string directory, string name)
    {
        List<string> existing = LoadCarNames(directory);
        for (int i = 0; i < existing.Count; i++)
            if (String.Equals(existing[i], name, StringComparison.CurrentCultureIgnoreCase))
                throw new InvalidOperationException("Такой автомобиль уже есть.");

        List<string[]> rows = ExpenseDataCodec.ReadRows(Path.Combine(directory, "cars.dat"));
        rows.Add(new string[] { name });
        ExpenseDataCodec.SaveRows(Path.Combine(directory, "cars.dat"), rows);
    }

    internal static void AddRecord(string directory, string fileName, string[] row)
    {
        ValidateRecord(fileName, row);
        List<string[]> rows = Load(directory, fileName);
        if (fileName == "utilities.dat")
            EnsureUniqueUtilityMonth(rows, row[0], -1);
        rows.Add(row);
        ExpenseDataCodec.SaveRows(Path.Combine(directory, fileName), rows);
    }

    internal static void UpdateRecord(string directory, string fileName, int index, string[] row)
    {
        ValidateRecord(fileName, row);
        List<string[]> rows = Load(directory, fileName);
        ValidateIndex(rows, index);
        if (fileName == "utilities.dat")
            EnsureUniqueUtilityMonth(rows, row[0], index);
        rows[index] = row;
        ExpenseDataCodec.SaveRows(Path.Combine(directory, fileName), rows);
    }

    internal static void DeleteRecord(string directory, string fileName, int index)
    {
        List<string[]> rows = Load(directory, fileName);
        ValidateIndex(rows, index);
        rows.RemoveAt(index);
        ExpenseDataCodec.SaveRows(Path.Combine(directory, fileName), rows);
    }

    internal static string CreateBackup(string dataDirectory)
    {
        ExpenseDataCodec.ValidateDirectory(dataDirectory);
        string backupRoot = GetBackupRoot(dataDirectory);
        Directory.CreateDirectory(backupRoot);

        string baseName = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string backupName = baseName;
        string backupPath = Path.Combine(backupRoot, backupName);
        int suffix = 1;
        while (Directory.Exists(backupPath))
        {
            backupName = baseName + "_" + suffix.ToString(CultureInfo.InvariantCulture);
            backupPath = Path.Combine(backupRoot, backupName);
            suffix++;
        }

        Directory.CreateDirectory(backupPath);
        try
        {
            CopyData(dataDirectory, backupPath);
            ExpenseDataCodec.ValidateDirectory(backupPath);
        }
        catch
        {
            Directory.Delete(backupPath, true);
            throw;
        }
        return backupName;
    }

    internal static List<string> ListBackupNames(string dataDirectory)
    {
        string backupRoot = GetBackupRoot(dataDirectory);
        List<string> names = new List<string>();
        if (!Directory.Exists(backupRoot))
            return names;

        string[] directories = Directory.GetDirectories(backupRoot);
        for (int i = 0; i < directories.Length; i++)
        {
            string name = Path.GetFileName(directories[i]);
            bool complete = true;
            for (int j = 0; j < ExpenseDataCodec.DataFiles.Length; j++)
                if (!File.Exists(Path.Combine(directories[i], ExpenseDataCodec.DataFiles[j])))
                    complete = false;
            if (complete)
                names.Add(name);
        }
        names.Sort(StringComparer.Ordinal);
        names.Reverse();
        return names;
    }

    internal static void RestoreBackup(string dataDirectory, string backupName)
    {
        if (String.IsNullOrEmpty(backupName) || Path.GetFileName(backupName) != backupName)
            throw new InvalidDataException("Выбрана неверная резервная копия.");

        List<string> availableBackups = ListBackupNames(dataDirectory);
        if (!availableBackups.Contains(backupName))
            throw new InvalidDataException("Выбрана неверная или неполная резервная копия.");

        string backupRoot = GetBackupRoot(dataDirectory);
        string source = Path.Combine(backupRoot, backupName);
        ExpenseDataCodec.ValidateDirectory(source);
        string safetyBackup = CreateBackup(dataDirectory);
        string safetyPath = Path.Combine(backupRoot, safetyBackup);
        try
        {
            CopyData(source, dataDirectory);
            ExpenseDataCodec.ValidateDirectory(dataDirectory);
        }
        catch (Exception restoreError)
        {
            try
            {
                CopyData(safetyPath, dataDirectory);
            }
            catch (Exception rollbackError)
            {
                throw new IOException("Не удалось восстановить копию: " + restoreError.Message +
                    ". Возврат прежних данных не удался: " + rollbackError.Message +
                    ". Исходная копия сохранена в " + safetyPath);
            }
            throw;
        }
    }

    private static void CopyData(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        for (int i = 0; i < ExpenseDataCodec.DataFiles.Length; i++)
        {
            string fileName = ExpenseDataCodec.DataFiles[i];
            File.Copy(Path.Combine(source, fileName), Path.Combine(destination, fileName), true);
        }
    }

    private static string GetBackupRoot(string dataDirectory)
    {
        string parent = Path.GetDirectoryName(dataDirectory);
        if (String.IsNullOrEmpty(parent))
            throw new ArgumentException("Неверная папка данных.", "dataDirectory");
        return Path.Combine(parent, "Backups");
    }

    private static void EnsureUniqueUtilityMonth(List<string[]> rows, string month, int ignoredIndex)
    {
        for (int i = 0; i < rows.Count; i++)
            if (i != ignoredIndex && rows[i][0] == month)
                throw new InvalidOperationException("Запись за этот месяц уже существует.");
    }

    private static void ValidateRecord(string fileName, string[] row)
    {
        if (fileName == "fuel.dat" && row.Length == 5)
            return;
        if (fileName == "repairs.dat" && row.Length == 9)
            return;
        if (fileName == "utilities.dat" && row.Length == 8)
            return;
        throw new InvalidDataException("Неверное количество полей в записи.");
    }

    private static void ValidateIndex(List<string[]> rows, int index)
    {
        if (index < 0 || index >= rows.Count)
            throw new ArgumentOutOfRangeException("index", "Выбранная запись уже отсутствует.");
    }
}
