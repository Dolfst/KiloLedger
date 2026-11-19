using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Alfa156.Desktop.Data
{
    internal static class AlfaCodec
    {
        internal static readonly string[] CoreDataFiles = new string[]
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

        internal static readonly string[] DataFiles = new string[]
        {
            "cars.dat", "fuel.dat", "repairs.dat", "utilities.dat", RepairCategoriesFile,
            ExpensesFile, ExpenseCategoriesFile, SettingsFile, CarsMetaFile, TemplatesFile,
            RemindersFile, RecordIds.DeletedFile
        };

        internal static readonly string[] UtilityLabels = new string[]
        {
            "Коммунальные платежи", "Электроэнергия", "Газ", "Стационарный телефон",
            "Домашний интернет", "Налог на аренду", "Аренда жилья"
        };

        internal static readonly string[] DefaultRepairCategories = new string[]
        {
            "ТО Масло", "ТО ГРМ", "Запчасти", "Жидкости", "Тормоза", "Кузов", "Расходники"
        };

        internal static readonly string[] DefaultExpenseCategories = new string[]
        {
            "Страховка", "Техосмотр", "Штрафы", "Мойка", "Парковка", "Прочее"
        };

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        internal static List<string[]> ReadRows(string path)
        {
            List<string[]> rows = new List<string[]>();
            if (!File.Exists(path))
                return rows;

            using (StreamReader reader = new StreamReader(path, Encoding.UTF8, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0)
                        continue;
                    rows.Add(DecodeRow(line));
                }
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
                    if (character == '\r' || character == '\n')
                    {
                        result.Append(' ');
                        continue;
                    }
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
                using (StreamWriter writer = new StreamWriter(temporaryPath, false, Utf8NoBom))
                {
                    for (int i = 0; i < rows.Count; i++)
                        writer.WriteLine(EncodeRow(rows[i]));
                }

                if (File.Exists(path))
                    File.Delete(path);
                File.Move(temporaryPath, path);
            }
            catch (Exception saveError)
            {
                try
                {
                    if (hadOriginal && File.Exists(backupPath))
                        File.Copy(backupPath, path, true);
                }
                catch (Exception restoreError)
                {
                    throw new IOException("Не удалось сохранить файл: " + saveError.Message +
                        ". Не удалось вернуть предыдущий файл: " + restoreError.Message, saveError);
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
                {
                    if (!IsValidColumnCount(CoreDataFiles[i], rows[j].Length))
                        throw new InvalidDataException("Неверный формат записи в " + CoreDataFiles[i] + " (строка " + (j + 1) + ").");
                }
                if (i == 0 && rows.Count == 0)
                    throw new InvalidDataException("В cars.dat нет автомобилей.");
            }

            string expensesPath = Path.Combine(directory, ExpensesFile);
            if (File.Exists(expensesPath))
            {
                List<string[]> expenses = ReadRows(expensesPath);
                for (int j = 0; j < expenses.Count; j++)
                    if (!IsValidColumnCount(ExpensesFile, expenses[j].Length))
                        throw new InvalidDataException("Неверный формат записи в " + ExpensesFile +
                            " (строка " + (j + 1) + ").");
            }

            string expenseCategoriesPath = Path.Combine(directory, ExpenseCategoriesFile);
            if (File.Exists(expenseCategoriesPath))
            {
                List<string[]> expenseCategories = ReadRows(expenseCategoriesPath);
                for (int j = 0; j < expenseCategories.Count; j++)
                    if (!IsValidCategoryRow(expenseCategories[j]))
                        throw new InvalidDataException("Неверный формат записи в " +
                            ExpenseCategoriesFile + ".");
            }

            string settingsPath = Path.Combine(directory, SettingsFile);
            if (File.Exists(settingsPath))
            {
                List<string[]> settings = ReadRows(settingsPath);
                for (int j = 0; j < settings.Count; j++)
                    if (!ToSettings.IsValidRow(settings[j]))
                        throw new InvalidDataException("Неверный формат записи в " + SettingsFile + ".");
            }

            string carsMetaPath = Path.Combine(directory, CarsMetaFile);
            if (File.Exists(carsMetaPath))
            {
                List<string[]> meta = ReadRows(carsMetaPath);
                for (int j = 0; j < meta.Count; j++)
                    if (!CarsMeta.IsValidRow(meta[j]))
                        throw new InvalidDataException("Неверный формат записи в " + CarsMetaFile + ".");
            }

            string templatesPath = Path.Combine(directory, TemplatesFile);
            if (File.Exists(templatesPath))
            {
                List<string[]> templates = ReadRows(templatesPath);
                for (int j = 0; j < templates.Count; j++)
                    if (!RecordTemplates.IsValidRow(templates[j]))
                        throw new InvalidDataException("Неверный формат записи в " + TemplatesFile + ".");
            }

            string remindersPath = Path.Combine(directory, RemindersFile);
            if (File.Exists(remindersPath))
            {
                List<string[]> reminders = ReadRows(remindersPath);
                for (int j = 0; j < reminders.Count; j++)
                    if (!DateReminders.IsValidRow(reminders[j]))
                        throw new InvalidDataException("Неверный формат записи в " + RemindersFile + ".");
            }
        }

        internal static bool IsValidColumnCount(string fileName, int length)
        {
            // Content fields, optionally + trailing stable id.
            // fuel: date|car|km|liters|cost|[price]|[comment]|[fullTank]|[purpose]|[id] — max 10
            if (fileName == "fuel.dat")
                return length >= 5 && length <= 10;
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
            if (fileName == TemplatesFile)
                return length >= 3 && length <= 11;
            if (fileName == RemindersFile)
                return length == 4 || length == 5;
            return false;
        }

        internal static bool IsValidCategoryRow(string[] row)
        {
            if (row == null || row.Length < 1 || row.Length > 2)
                return false;
            if ((row[0] ?? "").Trim().Length == 0)
                return false;
            if (row.Length == 2 && !RecordIds.LooksLikeId(row[1]))
                return false;
            return true;
        }

        internal static bool TryNumber(string text, out decimal value)
        {
            value = 0;
            if (text == null)
                return false;
            string raw = text.Trim().Replace(" ", "");
            if (raw.Length == 0)
                return false;

            int lastComma = raw.LastIndexOf(',');
            int lastDot = raw.LastIndexOf('.');
            if (lastComma >= 0 && lastDot >= 0)
            {
                if (lastComma > lastDot)
                    raw = raw.Replace(".", "").Replace(',', '.');
                else
                    raw = raw.Replace(",", "");
            }
            else if (lastComma >= 0)
                raw = raw.Replace(',', '.');

            return Decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        internal static string FormatNumber(string text)
        {
            decimal value;
            if (!TryNumber(text, out value))
                return (text ?? "").Trim();
            return value.ToString(CultureInfo.InvariantCulture);
        }

        internal static string NormalizeDate(string text)
        {
            string value = (text ?? "").Trim();
            if (value.Length == 0)
                return value;

            DateTime date;
            string[] formats = new string[]
            {
                "yyyy-MM-dd", "dd.MM.yyyy", "dd/MM/yyyy", "d.M.yyyy", "yyyy/MM/dd",
                "dd-MM-yyyy", "M/d/yyyy", "d/M/yyyy"
            };
            if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
                return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return value;
        }

        internal static string NormalizeMonth(string text)
        {
            string value = (text ?? "").Trim();
            if (value.Length == 0)
                return value;

            DateTime date;
            string[] formats = new string[] { "yyyy-MM", "yyyy/MM", "MM.yyyy", "yyyy.MM" };
            if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
                return date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            return value;
        }

        /// <summary>
        /// fullTank flag at content index 7: empty/missing/"1" = full, "0" = partial.
        /// purpose at content index 8 (before trailing id). Layout:
        /// date|car|km|liters|cost|price|comment|fullTank|purpose|id
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
    }
}
