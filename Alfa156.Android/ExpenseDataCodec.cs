using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Alfa156.Android;

internal static class ExpenseDataCodec
{
    internal static readonly string[] CoreDataFiles =
    {
        "cars.dat", "fuel.dat", "repairs.dat", "utilities.dat"
    };

    internal const string RepairCategoriesFile = "repair-categories.dat";
    internal const string ExpensesFile = "expenses.dat";
    internal const string ExpenseCategoriesFile = "expense-categories.dat";
    internal const string SettingsFile = "settings.dat";
    internal const string CarsMetaFile = CarsMeta.FileName;
    internal const string TemplatesFile = RecordTemplates.FileName;
    internal const string RemindersFile = DateReminders.FileName;
    internal const string NotesFile = CarNotes.FileName;

    internal static readonly string[] DefaultExpenseCategories =
    {
        "Страховка", "Техосмотр", "Штрафы", "Мойка", "Парковка", "Прочее"
    };

    internal static readonly string[] DataFiles =
    {
        "cars.dat", "fuel.dat", "repairs.dat", "utilities.dat", RepairCategoriesFile,
        ExpensesFile, ExpenseCategoriesFile, SettingsFile, CarsMetaFile, TemplatesFile,
        RemindersFile, NotesFile, RecordIds.DeletedFile
    };

    internal static bool IsDataFile(string name)
    {
        for (int i = 0; i < DataFiles.Length; i++)
            if (String.Equals(name, DataFiles[i], StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    internal static List<string[]> ReadRows(string path)
    {
        List<string[]> rows = new List<string[]>();
        using (StreamReader reader = new StreamReader(path, Encoding.UTF8, true))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
                if (line.Length > 0)
                    rows.Add(DecodeRow(line));
        }
        return rows;
    }

    internal static string[] DecodeRow(string line)
    {
        List<string> fields = new List<string>();
        StringBuilder field = new StringBuilder();
        bool escaped = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (escaped)
            {
                field.Append(c);
                escaped = false;
            }
            else if (c == '\\')
                escaped = true;
            else if (c == '|')
            {
                fields.Add(field.ToString());
                field.Length = 0;
            }
            else
                field.Append(c);
        }
        if (escaped)
            field.Append('\\');
        fields.Add(field.ToString());
        return fields.ToArray();
    }

    internal static string EncodeRow(string[] values)
    {
        StringBuilder result = new StringBuilder();
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
                result.Append('|');
            string value = values[i] ?? "";
            for (int j = 0; j < value.Length; j++)
            {
                char character = value[j];
                if (character == '\\' || character == '|')
                    result.Append('\\');
                result.Append(character);
            }
        }
        return result.ToString();
    }

    internal static void SaveRows(string path, List<string[]> rows)
    {
        string temporaryPath = path + ".tmp";
        string backupPath = path + ".bak";
        bool hadOriginal = File.Exists(path);
        if (hadOriginal)
            File.Copy(path, backupPath, true);

        try
        {
            using (StreamWriter writer = new StreamWriter(temporaryPath, false, new UTF8Encoding(false)))
                for (int i = 0; i < rows.Count; i++)
                    writer.WriteLine(EncodeRow(rows[i]));

            File.Move(temporaryPath, path, true);
        }
        catch (Exception saveError)
        {
            try
            {
                if (hadOriginal && File.Exists(backupPath))
                    File.Copy(backupPath, path, true);
                else if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception restoreError)
            {
                throw new IOException("Не удалось сохранить файл: " + saveError.Message +
                    ". Не удалось вернуть предыдущий файл: " + restoreError.Message);
            }
            throw;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    internal static void ValidateDirectory(string directory)
    {
        for (int i = 0; i < CoreDataFiles.Length; i++)
            if (!File.Exists(Path.Combine(directory, CoreDataFiles[i])))
                throw new InvalidDataException("Нужны все четыре файла: cars.dat, fuel.dat, repairs.dat и utilities.dat.");

        for (int i = 0; i < CoreDataFiles.Length; i++)
        {
            List<string[]> rows = ReadRows(Path.Combine(directory, CoreDataFiles[i]));
            for (int j = 0; j < rows.Count; j++)
                if (!IsValidColumnCount(CoreDataFiles[i], rows[j].Length))
                    throw new InvalidDataException("Неверный формат записи в " + CoreDataFiles[i] + ".");
            if (i == 0 && rows.Count == 0)
                throw new InvalidDataException("В cars.dat нет автомобилей.");
        }

        string categoriesPath = Path.Combine(directory, RepairCategoriesFile);
        if (File.Exists(categoriesPath))
        {
            List<string[]> categories = ReadRows(categoriesPath);
            for (int i = 0; i < categories.Count; i++)
                if (!IsValidCategoryRow(categories[i]))
                    throw new InvalidDataException("Неверный формат записи в " + RepairCategoriesFile + ".");
        }

        string expensesPath = Path.Combine(directory, ExpensesFile);
        if (File.Exists(expensesPath))
        {
            List<string[]> expenses = ReadRows(expensesPath);
            for (int i = 0; i < expenses.Count; i++)
                if (!IsValidColumnCount(ExpensesFile, expenses[i].Length))
                    throw new InvalidDataException("Неверный формат записи в " + ExpensesFile + ".");
        }

        string expenseCategoriesPath = Path.Combine(directory, ExpenseCategoriesFile);
        if (File.Exists(expenseCategoriesPath))
        {
            List<string[]> expenseCategories = ReadRows(expenseCategoriesPath);
            for (int i = 0; i < expenseCategories.Count; i++)
                if (!IsValidCategoryRow(expenseCategories[i]))
                    throw new InvalidDataException("Неверный формат записи в " + ExpenseCategoriesFile + ".");
        }

        string settingsPath = Path.Combine(directory, SettingsFile);
        if (File.Exists(settingsPath))
        {
            List<string[]> settings = ReadRows(settingsPath);
            for (int i = 0; i < settings.Count; i++)
                if (!ToSettings.IsValidRow(settings[i]))
                    throw new InvalidDataException("Неверный формат записи в " + SettingsFile + ".");
        }

        string carsMetaPath = Path.Combine(directory, CarsMetaFile);
        if (File.Exists(carsMetaPath))
        {
            List<string[]> meta = ReadRows(carsMetaPath);
            for (int i = 0; i < meta.Count; i++)
                if (!CarsMeta.IsValidRow(meta[i]))
                    throw new InvalidDataException("Неверный формат записи в " + CarsMetaFile + ".");
        }

        string templatesPath = Path.Combine(directory, TemplatesFile);
        if (File.Exists(templatesPath))
        {
            List<string[]> templates = ReadRows(templatesPath);
            for (int i = 0; i < templates.Count; i++)
                if (!RecordTemplates.IsValidRow(templates[i]))
                    throw new InvalidDataException("Неверный формат записи в " + TemplatesFile + ".");
        }

        string remindersPath = Path.Combine(directory, RemindersFile);
        if (File.Exists(remindersPath))
        {
            List<string[]> reminders = ReadRows(remindersPath);
            for (int i = 0; i < reminders.Count; i++)
                if (!DateReminders.IsValidRow(reminders[i]))
                    throw new InvalidDataException("Неверный формат записи в " + RemindersFile + ".");
        }

        string notesPath = Path.Combine(directory, NotesFile);
        if (File.Exists(notesPath))
        {
            List<string[]> notes = ReadRows(notesPath);
            for (int i = 0; i < notes.Count; i++)
                if (!CarNotes.IsValidRow(notes[i]))
                    throw new InvalidDataException("Неверный формат записи в " + NotesFile + ".");
        }
    }

    internal static bool IsValidCategoryRow(string[] row)
    {
        if (row == null || row.Length < 1 || row.Length > 2)
            return false;
        if (row[0].Trim().Length == 0)
            return false;
        if (row.Length == 2 && !RecordIds.LooksLikeId(row[1]))
            return false;
        return true;
    }

    internal static bool IsValidColumnCount(string fileName, int length)
    {
        // Content fields, optionally + trailing stable id.
        // fuel: date|car|km|liters|cost|[price]|[comment]|[fullTank]|[purpose]|[station]|[id] — max 11
        if (fileName == "fuel.dat")
            return length >= 5 && length <= 11;
        if (fileName == "cars.dat")
            return length == 1 || length == 2;
        if (fileName == "repairs.dat")
            return length == 9 || length == 10;
        if (fileName == "utilities.dat")
            return length == 8 || length == 9;
        if (fileName == ExpensesFile)
            return length == 6 || length == 7;
        if (fileName == CarsMetaFile)
            return length >= 1 && length <= 7;
        // templates: type|name|payload...|id — fuel/repair max 11, expense max 8
            if (fileName == TemplatesFile)
            return length >= 3 && length <= 12;
        // reminders: date|car|title|type|[id]
        if (fileName == RemindersFile)
            return length == 4 || length == 5;
        // notes: date|car|text|[id]
        if (fileName == NotesFile)
            return length == 3 || length == 4;
        return false;
    }

    internal static decimal Sum(string[] row, int startIndex)
    {
        decimal total = 0;
        for (int i = startIndex; i < row.Length; i++)
        {
            decimal amount;
            string value = row[i].Trim().Replace(',', '.');
            if (Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
                total += amount;
        }
        return total;
    }

    /// <summary>
    /// fullTank at content index 7: empty/missing/"1" = full, "0" = partial.
    /// purpose at content index 8; station/brand at content index 9 (before trailing id). Layout:
    /// date|car|km|liters|cost|price|comment|fullTank|purpose|station|id
    /// </summary>
    internal static bool IsFullTank(string[] row)
    {
        string[] content = RecordIds.StripId(row);
        if (content == null || content.Length <= 7)
            return true;
        string flag = (content[7] ?? "").Trim();
        return flag.Length == 0 || flag == "1";
    }

    internal static string NormalizeFullTankFlag(string value)
    {
        string flag = (value ?? "").Trim();
        if (flag == "0" || String.Equals(flag, "false", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(flag, "нет", StringComparison.OrdinalIgnoreCase))
            return "0";
        if (flag.Length == 0)
            return "1";
        return "1";
    }

    /// <summary>purpose at content index 8; missing/empty is OK (old rows).</summary>
    internal static string GetPurpose(string[] row)
    {
        string[] content = RecordIds.StripId(row);
        if (content == null || content.Length <= 8)
            return "";
        return NormalizePurpose(content[8]);
    }

    internal static string NormalizePurpose(string value)
    {
        string text = (value ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
        if (text.Length > 32)
            text = text.Substring(0, 32).Trim();
        return text;
    }

    /// <summary>station/brand at content index 9; missing/empty is OK (old rows).</summary>
    internal static string GetStation(string[] row)
    {
        string[] content = RecordIds.StripId(row);
        if (content == null || content.Length <= 9)
            return "";
        return NormalizeStation(content[9]);
    }

    internal static string NormalizeStation(string value)
    {
        string text = (value ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
        if (text.Length > 48)
            text = text.Substring(0, 48).Trim();
        return text;
    }

    /// <summary>
    /// Unique station suggestions: dedicated station field, plus legacy comment values
    /// (pre-1.32 «АЗС/коммент») when station is empty.
    /// </summary>
    internal static string[] CollectStationSuggestions(List<string[]> fuels)
    {
        List<string> result = new List<string>();
        System.Collections.Generic.HashSet<string> seen =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (fuels == null)
            return result.ToArray();
        for (int i = 0; i < fuels.Count; i++)
        {
            string station = GetStation(fuels[i]);
            if (station.Length == 0)
            {
                string[] content = RecordIds.StripId(fuels[i]);
                if (content != null && content.Length > 6)
                    station = NormalizeStation(content[6]);
            }
            if (station.Length == 0 || seen.Contains(station))
                continue;
            seen.Add(station);
            result.Add(station);
        }
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result.ToArray();
    }
}
