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

        internal static readonly string[] DataFiles = new string[]
        {
            "cars.dat", "fuel.dat", "repairs.dat", "utilities.dat", RepairCategoriesFile
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

            int[] columns = new int[] { 1, 5, 9, 8 };
            for (int i = 0; i < CoreDataFiles.Length; i++)
            {
                List<string[]> rows = ReadRows(Path.Combine(directory, CoreDataFiles[i]));
                for (int j = 0; j < rows.Count; j++)
                    if (rows[j].Length != columns[i])
                        throw new InvalidDataException("Неверный формат записи в " + CoreDataFiles[i] + " (строка " + (j + 1) + ").");
                if (i == 0 && rows.Count == 0)
                    throw new InvalidDataException("В cars.dat нет автомобилей.");
            }
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
    }
}
