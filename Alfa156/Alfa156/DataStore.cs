using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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
        private const string RepairCategoriesFile = "repair-categories.dat";
        private const string StartMenuShortcut = "Alfa 156.lnk";
        private static readonly string[] CoreDataFiles = new string[]
        {
            CarsFile, FuelFile, RepairsFile, UtilitiesFile
        };
        private static readonly string[] DataFiles = new string[]
        {
            CarsFile, FuelFile, RepairsFile, UtilitiesFile, RepairCategoriesFile
        };

        private static readonly string[] DefaultRepairCategories = new string[]
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
            EnsureRepairCategoriesFile();
            EnsureRepairCategories();
        }

        internal static string CreateBackup()
        {
            EnsureRepairCategories();
            string backupRoot = GetBackupRoot();
            Directory.CreateDirectory(backupRoot);

            string folderName = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string backupPath = Path.Combine(backupRoot, folderName);
            int suffix = 1;
            while (Directory.Exists(backupPath))
            {
                backupPath = Path.Combine(backupRoot, folderName + "_" + suffix.ToString(CultureInfo.InvariantCulture));
                suffix++;
            }

            Directory.CreateDirectory(backupPath);
            for (int i = 0; i < DataFiles.Length; i++)
                File.Copy(GetPath(DataFiles[i]), Path.Combine(backupPath, DataFiles[i]), false);
            return backupPath;
        }

        internal static string[] ListBackupNames()
        {
            string backupRoot = GetBackupRoot();
            if (!Directory.Exists(backupRoot))
                return new string[0];

            string[] directories = Directory.GetDirectories(backupRoot);
            List<string> names = new List<string>();
            for (int i = 0; i < directories.Length; i++)
                if (HasCompleteBackup(directories[i]))
                    names.Add(Path.GetFileName(directories[i]));

            for (int i = 0; i < names.Count; i++)
                for (int j = i + 1; j < names.Count; j++)
                    if (String.CompareOrdinal(names[i], names[j]) < 0)
                    {
                        string name = names[i];
                        names[i] = names[j];
                        names[j] = name;
                    }
            return names.ToArray();
        }

        internal static void RestoreBackup(string folderName)
        {
            if (folderName == null || folderName.Length == 0)
                throw new ArgumentException("Выбрана неверная папка резервной копии.", "folderName");

            string backupRoot = GetBackupRoot();
            string[] backupNames = ListBackupNames();
            bool found = false;
            for (int i = 0; i < backupNames.Length; i++)
                if (backupNames[i] == folderName)
                    found = true;
            if (!found)
                throw new InvalidDataException("Выбрана неверная или неполная резервная копия.");

            string backupPath = Path.Combine(backupRoot, folderName);
            if (!HasCompleteBackup(backupPath))
                throw new InvalidDataException("Резервная копия неполная или отсутствует.");

            string safetyBackup = CreateBackup();
            try
            {
                CopyBackupFiles(backupPath);
            }
            catch (Exception restoreError)
            {
                try
                {
                    CopyBackupFiles(safetyBackup);
                }
                catch (Exception rollbackError)
                {
                    throw new IOException("Восстановление прервано: " + restoreError.Message +
                        ". Автоматический возврат текущих данных не удался: " + rollbackError.Message +
                        ". Текущая копия сохранена в " + safetyBackup);
                }
                throw;
            }
        }

        private static string GetBackupRoot()
        {
            string[] cardRoots = new string[]
            {
                @"\Storage Card", @"\SD Card", @"\SDMMC"
            };
            for (int i = 0; i < cardRoots.Length; i++)
                if (Directory.Exists(cardRoots[i]))
                    return Path.Combine(cardRoots[i], "Alfa156_Backups");
            throw new DirectoryNotFoundException(
                "Не найдена SD-карта. Проверьте, что она доступна как «Storage Card» или «SD Card».");
        }

        private static bool HasCompleteBackup(string backupPath)
        {
            if (!Directory.Exists(backupPath))
                return false;
            for (int i = 0; i < CoreDataFiles.Length; i++)
                if (!File.Exists(Path.Combine(backupPath, CoreDataFiles[i])))
                    return false;
            return true;
        }

        private static void CopyBackupFiles(string backupPath)
        {
            for (int i = 0; i < CoreDataFiles.Length; i++)
                File.Copy(Path.Combine(backupPath, CoreDataFiles[i]), GetPath(CoreDataFiles[i]), true);
            string categoriesSource = Path.Combine(backupPath, RepairCategoriesFile);
            if (File.Exists(categoriesSource))
                File.Copy(categoriesSource, GetPath(RepairCategoriesFile), true);
            else
                WriteRepairCategories(CreateRepairCategoryRows(DefaultRepairCategories));
            EnsureRepairCategories();
        }

        internal static void CreateStartMenuShortcut()
        {
            string programsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            if (programsDirectory == null || programsDirectory.Length == 0)
                throw new IOException("Windows Mobile не вернул путь к меню «Пуск → Программы».");

            string shortcutPath = Path.Combine(programsDirectory, StartMenuShortcut);
            if (File.Exists(shortcutPath))
                return;

            Directory.CreateDirectory(programsDirectory);
            string executablePath = GetExecutablePath();
            string target = "\"" + executablePath + "\"";
            using (StreamWriter writer = new StreamWriter(shortcutPath, false, Encoding.ASCII))
            {
                writer.Write(target.Length.ToString(CultureInfo.InvariantCulture));
                writer.Write("#");
                writer.Write(target);
            }
        }

        private static string GetExecutablePath()
        {
            StringBuilder path = new StringBuilder(260);
            int length = GetModuleFileName(IntPtr.Zero, path, path.Capacity);
            if (length <= 0 || length >= path.Capacity)
                throw new IOException("Не удалось определить путь к файлу Alfa156.exe.");
            return path.ToString();
        }

        [DllImport("coredll.dll", CharSet = CharSet.Unicode)]
        private static extern int GetModuleFileName(IntPtr module, StringBuilder fileName, int size);

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

        internal static string[] LoadRepairCategories()
        {
            EnsureRepairCategories();
            List<string[]> rows = ReadRows(RepairCategoriesFile);
            string[] categories = new string[rows.Count];
            for (int i = 0; i < rows.Count; i++)
                categories[i] = rows[i][0];
            return categories;
        }

        internal static void AddRepairCategory(string name)
        {
            name = ValidateRepairCategoryName(name);
            List<string> categories = RepairCategoryNames();
            if (ContainsCategory(categories, name))
                throw new InvalidOperationException("Такая категория уже есть.");
            categories.Add(name);
            SaveRepairCategories(categories);
        }

        internal static void RenameRepairCategory(string oldName, string newName)
        {
            newName = ValidateRepairCategoryName(newName);
            List<string> categories = RepairCategoryNames();
            int categoryIndex = FindCategory(categories, oldName);
            if (categoryIndex < 0)
                throw new InvalidOperationException("Выбранная категория уже отсутствует.");
            if (String.Equals(categories[categoryIndex], newName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Новое название совпадает с текущим.");
            if (ContainsCategory(categories, newName))
                throw new InvalidOperationException("Категория с таким названием уже есть.");

            List<string[]> repairs = LoadRepairs();
            for (int i = 0; i < repairs.Count; i++)
                if (String.Equals(repairs[i][3], categories[categoryIndex], StringComparison.OrdinalIgnoreCase))
                    repairs[i][3] = newName;

            List<string> previousCategories = new List<string>(categories);
            categories[categoryIndex] = newName;
            SaveRepairCategories(categories);
            try
            {
                SaveRows(RepairsFile, repairs);
            }
            catch (Exception renameError)
            {
                try
                {
                    SaveRepairCategories(previousCategories);
                }
                catch (Exception rollbackError)
                {
                    throw new IOException("Не удалось переименовать категорию: " +
                        renameError.Message + ". Не удалось вернуть список категорий: " +
                        rollbackError.Message);
                }
                throw;
            }
        }

        internal static void DeleteRepairCategory(string name)
        {
            List<string> categories = RepairCategoryNames();
            int categoryIndex = FindCategory(categories, name);
            if (categoryIndex < 0)
                throw new InvalidOperationException("Выбранная категория уже отсутствует.");
            List<string[]> repairs = LoadRepairs();
            for (int i = 0; i < repairs.Count; i++)
                if (String.Equals(repairs[i][3], categories[categoryIndex], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "Сначала перенесите записи из этой категории в другую.");
            categories.RemoveAt(categoryIndex);
            SaveRepairCategories(categories);
        }

        private static void EnsureRepairCategoriesFile()
        {
            string path = GetPath(RepairCategoriesFile);
            if (File.Exists(path))
                return;
            WriteRepairCategories(CreateRepairCategoryRows(DefaultRepairCategories));
        }

        private static void EnsureRepairCategories()
        {
            EnsureRepairCategoriesFile();
            List<string[]> categoryRows = ReadRows(RepairCategoriesFile);
            List<string> categories = new List<string>();
            bool changed = false;
            for (int i = 0; i < categoryRows.Count; i++)
            {
                if (categoryRows[i].Length != 1 || categoryRows[i][0].Trim().Length == 0)
                    throw new InvalidDataException("Неверная запись в repair-categories.dat.");
                if (ContainsCategory(categories, categoryRows[i][0]))
                    changed = true;
                else
                    categories.Add(categoryRows[i][0]);
            }

            List<string[]> repairs = LoadRepairs();
            for (int i = 0; i < repairs.Count; i++)
            {
                string category = repairs[i][3];
                if (category.Length > 0 && !ContainsCategory(categories, category))
                {
                    categories.Add(category);
                    changed = true;
                }
            }
            if (changed)
                SaveRepairCategories(categories);
        }

        private static List<string> RepairCategoryNames()
        {
            string[] names = LoadRepairCategories();
            List<string> categories = new List<string>();
            categories.AddRange(names);
            return categories;
        }

        private static void SaveRepairCategories(List<string> categories)
        {
            SaveRows(RepairCategoriesFile, CreateRepairCategoryRows(categories));
        }

        private static void WriteRepairCategories(List<string[]> rows)
        {
            using (StreamWriter writer = new StreamWriter(GetPath(RepairCategoriesFile), false))
                for (int i = 0; i < rows.Count; i++)
                    writer.WriteLine(EncodeRow(rows[i]));
        }

        private static List<string[]> CreateRepairCategoryRows(IList<string> categories)
        {
            List<string[]> rows = new List<string[]>();
            for (int i = 0; i < categories.Count; i++)
                rows.Add(new string[] { categories[i] });
            return rows;
        }

        private static string ValidateRepairCategoryName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0)
                throw new InvalidOperationException("Введите название категории.");
            if (String.Equals(name, "Все категории", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Это название зарезервировано для фильтра.");
            if (name.IndexOf('\r') >= 0 || name.IndexOf('\n') >= 0)
                throw new InvalidOperationException("Название категории не должно содержать перевод строки.");
            return name;
        }

        private static bool ContainsCategory(List<string> categories, string name)
        {
            return FindCategory(categories, name) >= 0;
        }

        private static int FindCategory(List<string> categories, string name)
        {
            for (int i = 0; i < categories.Count; i++)
                if (String.Equals(categories[i], name, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
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

        internal static void UpdateFuelAt(int index, string[] values)
        {
            ReplaceAt(FuelFile, index, values);
        }

        internal static void UpdateRepairAt(int index, string[] values)
        {
            ReplaceAt(RepairsFile, index, values);
        }

        internal static void UpdateUtilityAt(int index, string[] values)
        {
            List<string[]> records = ReadRows(UtilitiesFile);
            ValidateIndex(records, index);
            for (int i = 0; i < records.Count; i++)
                if (i != index && records[i][0] == values[0])
                    throw new InvalidOperationException("Запись за этот месяц уже существует.");
            records[index] = values;
            SaveRows(UtilitiesFile, records);
        }

        internal static void DeleteFuelAt(int index)
        {
            DeleteAt(FuelFile, index);
        }

        internal static void DeleteRepairAt(int index)
        {
            DeleteAt(RepairsFile, index);
        }

        internal static void DeleteUtilityAt(int index)
        {
            DeleteAt(UtilitiesFile, index);
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

        private static void ReplaceAt(string fileName, int index, string[] values)
        {
            List<string[]> records = ReadRows(fileName);
            ValidateIndex(records, index);
            records[index] = values;
            SaveRows(fileName, records);
        }

        private static void DeleteAt(string fileName, int index)
        {
            List<string[]> records = ReadRows(fileName);
            ValidateIndex(records, index);
            records.RemoveAt(index);
            SaveRows(fileName, records);
        }

        private static void ValidateIndex(List<string[]> records, int index)
        {
            if (index < 0 || index >= records.Count)
                throw new ArgumentOutOfRangeException("index", "Выбранная запись уже отсутствует.");
        }

        private static void SaveRows(string fileName, List<string[]> records)
        {
            string path = GetPath(fileName);
            string backupPath = path + ".bak";
            string temporaryPath = path + ".tmp";

            File.Copy(path, backupPath, true);
            try
            {
                using (StreamWriter writer = new StreamWriter(temporaryPath, false))
                    for (int i = 0; i < records.Count; i++)
                        writer.WriteLine(EncodeRow(records[i]));
            }
            catch
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
                throw;
            }

            try
            {
                File.Delete(path);
                File.Move(temporaryPath, path);
            }
            catch
            {
                if (File.Exists(backupPath))
                {
                    if (File.Exists(path))
                        File.Delete(path);
                    File.Copy(backupPath, path, true);
                }
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
                throw;
            }
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

            try
            {
                value = Decimal.Parse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture);
                return true;
            }
            catch (FormatException)
            {
                value = 0;
                return false;
            }
            catch (OverflowException)
            {
                value = 0;
                return false;
            }
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
