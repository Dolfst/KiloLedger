using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Alfa156.Android;

internal static class ExpenseDataStore
{
    private static readonly string[] DefaultRepairCategories =
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
            if (rows[i].Length >= 1 && rows[i][0].Length > 0 &&
                (rows[i].Length == 1 || (rows[i].Length == 2 && RecordIds.LooksLikeId(rows[i][1]))))
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

    internal static void EnsureRepairCategories(string directory)
    {
        string path = Path.Combine(directory, ExpenseDataCodec.RepairCategoriesFile);
        List<string> categories = new List<string>();
        bool changed = !File.Exists(path);
        if (File.Exists(path))
        {
            List<string[]> rows = ExpenseDataCodec.ReadRows(path);
            for (int i = 0; i < rows.Count; i++)
            {
                if (!ExpenseDataCodec.IsValidCategoryRow(rows[i]))
                    throw new InvalidDataException("Неверная запись в " +
                        ExpenseDataCodec.RepairCategoriesFile + ".");
                if (!RecordIds.HasId(rows[i]))
                    changed = true;
                if (ContainsCategory(categories, rows[i][0]))
                    changed = true;
                else
                    categories.Add(rows[i][0]);
            }
        }
        else
        {
            for (int i = 0; i < DefaultRepairCategories.Length; i++)
                AddUniqueCategory(categories, DefaultRepairCategories[i]);
        }

        List<string[]> repairs = Load(directory, "repairs.dat");
        for (int i = 0; i < repairs.Count; i++)
        {
            if (repairs[i][3].Length == 0 || ContainsCategory(categories, repairs[i][3]))
                continue;
            categories.Add(repairs[i][3]);
            changed = true;
        }
        if (changed)
            SaveRepairCategories(directory, categories);
    }

    internal static List<string> LoadRepairCategories(string directory)
    {
        EnsureRepairCategories(directory);
        List<string[]> rows = ExpenseDataCodec.ReadRows(
            Path.Combine(directory, ExpenseDataCodec.RepairCategoriesFile));
        List<string> categories = new List<string>();
        for (int i = 0; i < rows.Count; i++)
            categories.Add(rows[i][0]);
        return categories;
    }

    internal static void AddRepairCategory(string directory, string name)
    {
        name = ValidateRepairCategoryName(name);
        List<string> categories = LoadRepairCategories(directory);
        if (ContainsCategory(categories, name))
            throw new InvalidOperationException("Такая категория уже есть.");
        categories.Add(name);
        SaveRepairCategories(directory, categories);
    }

    internal static void RenameRepairCategory(string directory, string oldName, string newName)
    {
        newName = ValidateRepairCategoryName(newName);
        List<string> categories = LoadRepairCategories(directory);
        int categoryIndex = FindCategory(categories, oldName);
        if (categoryIndex < 0)
            throw new InvalidOperationException("Выбранная категория уже отсутствует.");
        if (String.Equals(categories[categoryIndex], newName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Новое название совпадает с текущим.");
        if (ContainsCategory(categories, newName))
            throw new InvalidOperationException("Категория с таким названием уже есть.");

        string previousName = categories[categoryIndex];
        List<string> previousCategories = new List<string>(categories);
        List<string[]> repairs = Load(directory, "repairs.dat");
        for (int i = 0; i < repairs.Count; i++)
            if (String.Equals(repairs[i][3], previousName, StringComparison.OrdinalIgnoreCase))
                repairs[i][3] = newName;

        categories[categoryIndex] = newName;
        SaveRepairCategories(directory, categories);
        try
        {
            ExpenseDataCodec.SaveRows(Path.Combine(directory, "repairs.dat"), repairs);
        }
        catch (Exception renameError)
        {
            try
            {
                SaveRepairCategories(directory, previousCategories);
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

    internal static void DeleteRepairCategory(string directory, string name)
    {
        List<string> categories = LoadRepairCategories(directory);
        int categoryIndex = FindCategory(categories, name);
        if (categoryIndex < 0)
            throw new InvalidOperationException("Выбранная категория уже отсутствует.");
        List<string[]> repairs = Load(directory, "repairs.dat");
        for (int i = 0; i < repairs.Count; i++)
            if (String.Equals(repairs[i][3], categories[categoryIndex], StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Сначала перенесите записи из этой категории в другую.");
        List<string[]> categoryRows = ExpenseDataCodec.ReadRows(
            Path.Combine(directory, ExpenseDataCodec.RepairCategoriesFile));
        string deletedId = null;
        for (int i = 0; i < categoryRows.Count; i++)
            if (String.Equals(categoryRows[i][0], categories[categoryIndex], StringComparison.OrdinalIgnoreCase))
            {
                deletedId = RecordIds.GetId(categoryRows[i]);
                break;
            }
        categories.RemoveAt(categoryIndex);
        SaveRepairCategories(directory, categories);
        if (deletedId != null)
            RecordIds.AddTombstone(directory, ExpenseDataCodec.RepairCategoriesFile, deletedId);
    }

    internal static void AddCar(string directory, string name)
    {
        name = ValidateCarName(name);
        List<string> existing = LoadCarNames(directory);
        if (FindCar(existing, name) >= 0)
            throw new InvalidOperationException("Такой автомобиль уже есть.");

        List<string[]> rows = ExpenseDataCodec.ReadRows(Path.Combine(directory, "cars.dat"));
        rows.Add(RecordIds.EnsureId(new string[] { name }, RecordIds.PrefixForFile("cars.dat")));
        ExpenseDataCodec.SaveRows(Path.Combine(directory, "cars.dat"), rows);
    }

    internal static void RenameCar(string directory, string oldName, string newName)
    {
        newName = ValidateCarName(newName);
        List<string> cars = LoadCarNames(directory);
        int carIndex = FindCar(cars, oldName);
        if (carIndex < 0)
            throw new InvalidOperationException("Выбранный автомобиль уже отсутствует.");
        if (String.Equals(cars[carIndex], newName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Новое название совпадает с текущим.");
        if (FindCar(cars, newName) >= 0)
            throw new InvalidOperationException("Автомобиль с таким названием уже есть.");

        string previousName = cars[carIndex];
        List<string[]> fuel = Load(directory, "fuel.dat");
        List<string[]> repairs = Load(directory, "repairs.dat");
        for (int i = 0; i < fuel.Count; i++)
            if (String.Equals(fuel[i][1], previousName, StringComparison.OrdinalIgnoreCase))
                fuel[i][1] = newName;
        for (int i = 0; i < repairs.Count; i++)
            if (String.Equals(repairs[i][1], previousName, StringComparison.OrdinalIgnoreCase))
                repairs[i][1] = newName;

        cars[carIndex] = newName;
        SaveCarNames(directory, cars);
        ExpenseDataCodec.SaveRows(Path.Combine(directory, "fuel.dat"), fuel);
        ExpenseDataCodec.SaveRows(Path.Combine(directory, "repairs.dat"), repairs);
    }

    internal static void DeleteCar(string directory, string name)
    {
        List<string> cars = LoadCarNames(directory);
        int carIndex = FindCar(cars, name);
        if (carIndex < 0)
            throw new InvalidOperationException("Выбранный автомобиль уже отсутствует.");
        if (cars.Count <= 1)
            throw new InvalidOperationException("Нельзя удалить единственный автомобиль.");

        string carName = cars[carIndex];
        List<string[]> carRows = ExpenseDataCodec.ReadRows(Path.Combine(directory, "cars.dat"));
        string carId = null;
        for (int i = 0; i < carRows.Count; i++)
            if (String.Equals(carRows[i][0], carName, StringComparison.OrdinalIgnoreCase))
            {
                carId = RecordIds.GetId(RecordIds.EnsureId(carRows[i], RecordIds.PrefixForFile("cars.dat")));
                break;
            }
        List<string[]> fuel = Load(directory, "fuel.dat");
        List<string[]> repairs = Load(directory, "repairs.dat");
        List<string[]> keptFuel = new List<string[]>();
        List<string[]> keptRepairs = new List<string[]>();
        for (int i = 0; i < fuel.Count; i++)
        {
            if (!String.Equals(fuel[i][1], carName, StringComparison.OrdinalIgnoreCase))
                keptFuel.Add(fuel[i]);
            else
            {
                string id = RecordIds.GetId(RecordIds.EnsureId(fuel[i], RecordIds.PrefixForFile("fuel.dat")));
                if (id != null)
                    RecordIds.AddTombstone(directory, "fuel.dat", id);
            }
        }
        for (int i = 0; i < repairs.Count; i++)
        {
            if (!String.Equals(repairs[i][1], carName, StringComparison.OrdinalIgnoreCase))
                keptRepairs.Add(repairs[i]);
            else
            {
                string id = RecordIds.GetId(RecordIds.EnsureId(repairs[i],
                    RecordIds.PrefixForFile("repairs.dat")));
                if (id != null)
                    RecordIds.AddTombstone(directory, "repairs.dat", id);
            }
        }

        cars.RemoveAt(carIndex);
        SaveCarNames(directory, cars);
        if (carId != null)
            RecordIds.AddTombstone(directory, "cars.dat", carId);
        ExpenseDataCodec.SaveRows(Path.Combine(directory, "fuel.dat"), keptFuel);
        ExpenseDataCodec.SaveRows(Path.Combine(directory, "repairs.dat"), keptRepairs);
    }

    private static string ValidateCarName(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("Введите название автомобиля.");
        if (name.IndexOf('\n') >= 0 || name.IndexOf('\r') >= 0)
            throw new InvalidOperationException("Название автомобиля не должно содержать перевод строки.");
        return name;
    }

    private static int FindCar(List<string> cars, string name)
    {
        for (int i = 0; i < cars.Count; i++)
            if (String.Equals(cars[i], name, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    private static void SaveCarNames(string directory, List<string> cars)
    {
        Dictionary<string, string> existingIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string carsPath = Path.Combine(directory, "cars.dat");
        if (File.Exists(carsPath))
        {
            List<string[]> previous = ExpenseDataCodec.ReadRows(carsPath);
            for (int i = 0; i < previous.Count; i++)
            {
                string id = RecordIds.GetId(previous[i]);
                if (id != null && previous[i].Length > 0 && !existingIds.ContainsKey(previous[i][0]))
                    existingIds.Add(previous[i][0], id);
            }
        }
        List<string[]> rows = new List<string[]>();
        for (int i = 0; i < cars.Count; i++)
        {
            string[] row = new string[] { cars[i] };
            string id;
            if (existingIds.TryGetValue(cars[i], out id))
                row = RecordIds.WithId(row, id);
            else
                row = RecordIds.EnsureId(row, RecordIds.PrefixForFile("cars.dat"));
            rows.Add(row);
        }
        ExpenseDataCodec.SaveRows(carsPath, rows);
    }

    /// <summary>
    /// Создаёт пустой набор данных на телефоне: авто, пустые журналы и категории ремонта.
    /// </summary>
    internal static void CreateNewWorkspace(string directory, string firstCarName)
    {
        firstCarName = (firstCarName ?? "").Trim();
        if (firstCarName.Length == 0)
            throw new InvalidOperationException("Введите название автомобиля.");
        if (firstCarName.IndexOf('\n') >= 0 || firstCarName.IndexOf('\r') >= 0)
            throw new InvalidOperationException("Название автомобиля не должно содержать перевод строки.");

        if (HasCompleteCoreFiles(directory))
            throw new InvalidOperationException(
                "На телефоне уже есть данные. Сначала экспортируйте копию или удалите приложение.");

        Directory.CreateDirectory(directory);
        ExpenseDataCodec.SaveRows(Path.Combine(directory, "cars.dat"),
            new List<string[]>
            {
                RecordIds.EnsureId(new string[] { firstCarName }, RecordIds.PrefixForFile("cars.dat"))
            });
        ExpenseDataCodec.SaveRows(Path.Combine(directory, "fuel.dat"), new List<string[]>());
        ExpenseDataCodec.SaveRows(Path.Combine(directory, "repairs.dat"), new List<string[]>());
        ExpenseDataCodec.SaveRows(Path.Combine(directory, "utilities.dat"), new List<string[]>());
        RecordIds.EnsureDeletedFile(directory);
        EnsureRepairCategories(directory);
        ExpenseDataCodec.ValidateDirectory(directory);
    }

    internal static bool HasCompleteCoreFiles(string directory)
    {
        if (!Directory.Exists(directory))
            return false;
        for (int i = 0; i < ExpenseDataCodec.CoreDataFiles.Length; i++)
            if (!File.Exists(Path.Combine(directory, ExpenseDataCodec.CoreDataFiles[i])))
                return false;
        return true;
    }

    internal static void AddRecord(string directory, string fileName, string[] row)
    {
        row = RecordIds.EnsureId(row, RecordIds.PrefixForFile(fileName));
        ValidateRecord(fileName, row);
        List<string[]> rows = Load(directory, fileName);
        if (fileName == "utilities.dat")
            EnsureUniqueUtilityMonth(rows, row[0], -1);
        rows.Add(row);
        ExpenseDataCodec.SaveRows(Path.Combine(directory, fileName), rows);
    }

    internal static void UpdateRecord(string directory, string fileName, int index, string[] row)
    {
        List<string[]> rows = Load(directory, fileName);
        ValidateIndex(rows, index);
        string oldId = RecordIds.GetId(rows[index]);
        if (!RecordIds.HasId(row) && oldId != null)
            row = RecordIds.WithId(row, oldId);
        else
            row = RecordIds.EnsureId(row, RecordIds.PrefixForFile(fileName));
        ValidateRecord(fileName, row);
        if (fileName == "utilities.dat")
            EnsureUniqueUtilityMonth(rows, row[0], index);
        rows[index] = row;
        ExpenseDataCodec.SaveRows(Path.Combine(directory, fileName), rows);
    }

    internal static void DeleteRecord(string directory, string fileName, int index)
    {
        List<string[]> rows = Load(directory, fileName);
        ValidateIndex(rows, index);
        string id = RecordIds.GetId(RecordIds.EnsureId(rows[index], RecordIds.PrefixForFile(fileName)));
        rows.RemoveAt(index);
        ExpenseDataCodec.SaveRows(Path.Combine(directory, fileName), rows);
        if (id != null)
            RecordIds.AddTombstone(directory, fileName, id);
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
            for (int j = 0; j < ExpenseDataCodec.CoreDataFiles.Length; j++)
                if (!File.Exists(Path.Combine(directories[i], ExpenseDataCodec.CoreDataFiles[j])))
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
        for (int i = 0; i < ExpenseDataCodec.CoreDataFiles.Length; i++)
        {
            string fileName = ExpenseDataCodec.CoreDataFiles[i];
            File.Copy(Path.Combine(source, fileName), Path.Combine(destination, fileName), true);
        }
        string categoriesSource = Path.Combine(source, ExpenseDataCodec.RepairCategoriesFile);
        string categoriesDestination = Path.Combine(destination, ExpenseDataCodec.RepairCategoriesFile);
        if (File.Exists(categoriesSource))
            File.Copy(categoriesSource, categoriesDestination, true);
        else
            SaveRepairCategories(destination, new List<string>(DefaultRepairCategories));
        string deletedSource = Path.Combine(source, RecordIds.DeletedFile);
        string deletedDestination = Path.Combine(destination, RecordIds.DeletedFile);
        if (File.Exists(deletedSource))
            File.Copy(deletedSource, deletedDestination, true);
        else
            RecordIds.EnsureDeletedFile(destination);
        EnsureRepairCategories(destination);
    }

    internal static void CopyDataFiles(string source, string destination)
    {
        CopyData(source, destination);
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
        if (ExpenseDataCodec.IsValidColumnCount(fileName, row.Length))
            return;
        throw new InvalidDataException("Неверное количество полей в записи.");
    }

    private static void ValidateIndex(List<string[]> rows, int index)
    {
        if (index < 0 || index >= rows.Count)
            throw new ArgumentOutOfRangeException("index", "Выбранная запись уже отсутствует.");
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

    private static void SaveRepairCategories(string directory, List<string> categories)
    {
        Dictionary<string, string> existingIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string path = Path.Combine(directory, ExpenseDataCodec.RepairCategoriesFile);
        if (File.Exists(path))
        {
            List<string[]> previous = ExpenseDataCodec.ReadRows(path);
            for (int i = 0; i < previous.Count; i++)
            {
                string id = RecordIds.GetId(previous[i]);
                if (id != null && previous[i].Length > 0 && !existingIds.ContainsKey(previous[i][0]))
                    existingIds.Add(previous[i][0], id);
            }
        }
        List<string[]> rows = new List<string[]>();
        for (int i = 0; i < categories.Count; i++)
        {
            string[] row = new string[] { categories[i] };
            string id;
            if (existingIds.TryGetValue(categories[i], out id))
                row = RecordIds.WithId(row, id);
            else
                row = RecordIds.EnsureId(row, RecordIds.PrefixForFile(ExpenseDataCodec.RepairCategoriesFile));
            rows.Add(row);
        }
        ExpenseDataCodec.SaveRows(path, rows);
    }

    private static void AddUniqueCategory(List<string> categories, string name)
    {
        if (!ContainsCategory(categories, name))
            categories.Add(name);
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
}
