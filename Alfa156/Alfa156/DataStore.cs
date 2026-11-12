using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace CarExpenses
{
    internal static class DataStore
    {
        private const string DataDirectory = @"\My Documents\Alfa156";
        private const string CarsFile = "cars.dat";
        private const string FuelFile = "fuel.dat";
        private const string RepairsFile = "repairs.dat";
        private const string UtilitiesFile = "utilities.dat";

        internal static readonly string[] RepairCategories = new string[]
        {
            "ТО Масло", "ТО ГРМ", "Запчасти", "Жидкости", "Тормоза", "Кузов", "Расходники"
        };

        internal static readonly string[] UtilityLabels = new string[]
        {
            "Коммунальные платежи", "Электроэнергия", "Газ", "Стационарный телефон",
            "Домашний интернет", "Налог на аренду", "Аренда жилья"
        };

        internal static void Initialize()
        {
            Directory.CreateDirectory(DataDirectory);
            EnsureFile(CarsFile, "Alfa156.SeedData.Vehicles.seed");
            EnsureFile(FuelFile, null);
            EnsureFile(RepairsFile, "Alfa156.SeedData.Repairs.seed");
            EnsureFile(UtilitiesFile, "Alfa156.SeedData.Utilities.seed");
        }

        private static void EnsureFile(string fileName, string resourceName)
        {
            string path = GetPath(fileName);
            if (File.Exists(path))
                return;

            using (StreamWriter writer = new StreamWriter(path, false))
            {
                if (resourceName == null)
                    return;

                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
                if (stream == null)
                    throw new InvalidOperationException("Не найден файл начальных данных: " + resourceName);

                using (stream)
                using (StreamReader reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                        writer.WriteLine(line);
                }
            }
        }

        private static string GetPath(string fileName)
        {
            return Path.Combine(DataDirectory, fileName);
        }

        internal static List<string[]> LoadCars()
        {
            List<string[]> rows = ReadRows(CarsFile);
            List<string[]> cars = new List<string[]>();
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Length > 0 && rows[i][0] != "")
                    cars.Add(new string[] { rows[i][0] });
            if (cars.Count == 0)
                throw new InvalidDataException("Список автомобилей пуст. Добавьте автомобиль в файл cars.dat.");
            return cars;
        }

        internal static List<string> LoadCarNames()
        {
            List<string[]> rows = LoadCars();
            List<string> names = new List<string>();
            for (int i = 0; i < rows.Count; i++)
                names.Add(rows[i][0]);
            return names;
        }

        internal static List<string[]> LoadFuel()
        {
            return ReadRows(FuelFile);
        }

        internal static List<string[]> LoadRepairs()
        {
            return ReadRows(RepairsFile);
        }

        internal static List<string[]> LoadUtilities()
        {
            return ReadRows(UtilitiesFile);
        }

        internal static void AddFuel(string[] values)
        {
            Append(FuelFile, values);
        }

        internal static void AddRepair(string[] values)
        {
            Append(RepairsFile, values);
        }

        internal static void AddUtility(string[] values)
        {
            Append(UtilitiesFile, values);
        }

        internal static bool ContainsUtilityMonth(string month)
        {
            List<string[]> records = LoadUtilities();
            for (int i = 0; i < records.Count; i++)
                if (records[i][0] == month)
                    return true;
            return false;
        }

        internal static void AddCar(string name)
        {
            Append(CarsFile, new string[] { name });
        }

        internal static bool ContainsCar(string name)
        {
            List<string[]> cars = LoadCars();
            for (int i = 0; i < cars.Count; i++)
                if (String.Compare(cars[i][0], name, true, CultureInfo.CurrentCulture) == 0)
                    return true;
            return false;
        }

        private static void Append(string fileName, string[] values)
        {
            using (StreamWriter writer = new StreamWriter(GetPath(fileName), true))
                writer.WriteLine(EncodeRow(values));
        }

        private static List<string[]> ReadRows(string fileName)
        {
            List<string[]> rows = new List<string[]>();
            using (StreamReader reader = new StreamReader(GetPath(fileName)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                    if (line.Length > 0)
                        rows.Add(DecodeRow(line));
            }
            return rows;
        }

        private static string EncodeRow(string[] values)
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    result.Append('|');
                string value = values[i] == null ? "" : values[i];
                for (int j = 0; j < value.Length; j++)
                {
                    char c = value[j];
                    if (c == '\\' || c == '|')
                        result.Append('\\');
                    if (c == '\r' || c == '\n')
                        result.Append(' ');
                    else
                        result.Append(c);
                }
            }
            return result.ToString();
        }

        private static string[] DecodeRow(string line)
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

        internal static bool TryNumber(string text, out decimal value)
        {
            string normalized = text == null ? "" : text.Trim().Replace(" ", "");
            int comma = normalized.LastIndexOf(',');
            int dot = normalized.LastIndexOf('.');
            if (comma >= 0 && dot >= 0)
            {
                if (comma > dot)
                    normalized = normalized.Replace(".", "").Replace(',', '.');
                else
                    normalized = normalized.Replace(",", "");
            }
            else
                normalized = normalized.Replace(',', '.');

            return Decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out value);
        }

        internal static decimal NumberOrZero(string text)
        {
            decimal value;
            if (TryNumber(text, out value))
                return value;
            return 0;
        }
    }
}
