using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
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
        private const string ExpensesFile = "expenses.dat";
        private const string ExpenseCategoriesFile = "expense-categories.dat";
        private const string SettingsFile = "settings.dat";
        private const string DeletedFile = "deleted.dat";
        private const string SyncAddressFile = "last-sync-ip.txt";
        private const string StartMenuShortcut = "Alfa 156.lnk";
        private static readonly string[] CoreDataFiles = new string[]
        {
            CarsFile, FuelFile, RepairsFile, UtilitiesFile
        };
        private static readonly string[] DataFiles = new string[]
        {
            CarsFile, FuelFile, RepairsFile, UtilitiesFile, RepairCategoriesFile,
            ExpensesFile, ExpenseCategoriesFile, SettingsFile, DeletedFile
        };

        private static readonly string[] DefaultRepairCategories = new string[]
        {
            "ТО Масло", "ТО ГРМ", "Запчасти", "Жидкости", "Тормоза", "Кузов", "Расходники"
        };

        private static readonly string[] DefaultExpenseCategories = new string[]
        {
            "Страховка", "Техосмотр", "Штрафы", "Мойка", "Парковка", "Прочее"
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
            EnsureExpenseCategoriesFile();
            EnsureExpenseCategories();
            ToSettings.EnsureFile();
            EnsureDeletedFile();
            EnsureRecordIdsOnDisk();
        }

        internal static string CreateBackup()
        {
            return CreateBackup(GetBackupRoot());
        }

        internal static string CreateLocalBackup()
        {
            return CreateBackup(GetLocalBackupRoot());
        }

        private static string CreateBackup(string backupRoot)
        {
            EnsureRepairCategories();
            EnsureExpenseCategories();
            ToSettings.EnsureFile();
            EnsureDeletedFile();
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
            CopyReceiptsFolder(DataDirectory, backupPath);
            return backupPath;
        }

        private const string ReceiptsFolderName = "Receipts";
        /// <summary>Wi‑Fi receipt limits (1.15+): skip &gt; 2 MB; max 50 files per sync.</summary>
        internal const int MaxReceiptSyncFileBytes = 2 * 1024 * 1024;
        internal const int MaxReceiptSyncFiles = 50;

        private static void CopyReceiptsFolder(string sourceDataDir, string destDataDir)
        {
            string source = Path.Combine(sourceDataDir, ReceiptsFolderName);
            string dest = Path.Combine(destDataDir, ReceiptsFolderName);
            if (Directory.Exists(dest))
            {
                string[] oldFiles = Directory.GetFiles(dest);
                for (int i = 0; i < oldFiles.Length; i++)
                    File.Delete(oldFiles[i]);
            }
            if (!Directory.Exists(source))
                return;
            Directory.CreateDirectory(dest);
            string[] files = Directory.GetFiles(source, "*.jpg");
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileName(files[i]);
                if (name == null || name.IndexOf("..") >= 0)
                    continue;
                File.Copy(files[i], Path.Combine(dest, name), true);
            }
        }

        internal static string[] ListBackupNames()
        {
            return ListBackupNames(GetBackupRoot());
        }

        internal static string[] ListLocalBackupNames()
        {
            return ListBackupNames(GetLocalBackupRoot());
        }

        private static string[] ListBackupNames(string backupRoot)
        {
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
            RestoreBackup(folderName, GetBackupRoot());
        }

        internal static void RestoreLocalBackup(string folderName)
        {
            RestoreBackup(folderName, GetLocalBackupRoot());
        }

        private static void RestoreBackup(string folderName, string backupRoot)
        {
            if (folderName == null || folderName.Length == 0)
                throw new ArgumentException("Выбрана неверная папка резервной копии.", "folderName");
            if (Path.GetFileName(folderName) != folderName)
                throw new InvalidDataException("Выбрана неверная папка резервной копии.");

            string[] backupNames = ListBackupNames(backupRoot);
            bool found = false;
            for (int i = 0; i < backupNames.Length; i++)
                if (backupNames[i] == folderName)
                    found = true;
            if (!found)
                throw new InvalidDataException("Выбрана неверная или неполная резервная копия.");

            string backupPath = Path.Combine(backupRoot, folderName);
            if (!HasCompleteBackup(backupPath))
                throw new InvalidDataException("Резервная копия неполная или отсутствует.");

            string safetyBackup = CreateBackup(backupRoot);
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

        internal static void WriteSyncSnapshot(NetworkStream stream, string pairingCode)
        {
            EnsureExpenseCategories();
            ToSettings.EnsureFile();
            EnsureDeletedFile();
            EnsureRecordIdsOnDisk();
            using (SyncCrypto crypto = new SyncCrypto(pairingCode))
            {
                for (int i = 0; i < DataFiles.Length; i++)
                {
                    string path = GetPath(DataFiles[i]);
                    byte[] content = File.Exists(path) ? ReadFileBytes(path) : new byte[0];
                    if (content.Length > 16 * 1024 * 1024)
                        throw new InvalidDataException("Файл " + DataFiles[i] +
                            " превышает допустимый размер синхронизации.");
                    if (content.Length > 0)
                        crypto.TransformInPlace(content);
                    WriteSyncInt32(stream, content.Length);
                    if (content.Length > 0)
                        stream.Write(content, 0, content.Length);
                }
                WriteSyncReceipts(stream, DataDirectory, crypto);
            }
            stream.Flush();
        }

        internal static List<string[]> ReadSyncTable(string directory, string fileName)
        {
            string path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
                return new List<string[]>();
            return ReadRowsFromPath(path);
        }

        internal static void WriteSyncTable(string directory, string fileName, List<string[]> rows)
        {
            Directory.CreateDirectory(directory);
            using (StreamWriter writer = new StreamWriter(Path.Combine(directory, fileName), false))
            {
                for (int i = 0; i < rows.Count; i++)
                    writer.WriteLine(EncodeRow(rows[i]));
            }
        }

        internal static void MergeSyncSnapshot(
            NetworkStream stream,
            string pairingCode,
            SyncPlanResolver resolvePlan,
            SyncConflictResolver resolveConflict)
        {
            string parent = Path.GetDirectoryName(DataDirectory);
            string staging = Path.Combine(parent, "Alfa156_Sync_" + Guid.NewGuid().ToString("N"));
            string merged = Path.Combine(parent, "Alfa156_SyncMerged_" + Guid.NewGuid().ToString("N"));
            string safetyName = "Alfa156_SyncSafety_" +
                DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string safety = Path.Combine(parent, safetyName);
            int suffix = 1;
            while (Directory.Exists(safety))
            {
                safety = Path.Combine(parent, safetyName + "_" +
                    suffix.ToString(CultureInfo.InvariantCulture));
                suffix++;
            }
            Directory.CreateDirectory(staging);
            try
            {
                using (SyncCrypto crypto = new SyncCrypto(pairingCode))
                {
                    for (int i = 0; i < DataFiles.Length; i++)
                    {
                        int length = ReadSyncInt32(stream);
                        if (length < 0 || length > 16 * 1024 * 1024)
                            throw new InvalidDataException("Размер файла синхронизации превышает допустимый.");
                        byte[] content = ReadSyncBytes(stream, length);
                        if (content.Length > 0)
                            crypto.TransformInPlace(content);
                        using (FileStream output = File.Create(Path.Combine(staging, DataFiles[i])))
                            output.Write(content, 0, content.Length);
                    }
                    ReadSyncReceipts(stream, staging, crypto);
                }
                ValidateSyncDirectory(staging);

                SyncPlan plan = resolvePlan(SyncMerge.FormatSummary(
                    SyncMerge.AnalyzeDirectories(DataDirectory, staging)));
                if (plan == null || plan.Cancelled)
                    throw new InvalidOperationException("Синхронизация отменена.");

                Directory.CreateDirectory(merged);
                SyncMerge.ApplyPlan(DataDirectory, staging, merged, plan, resolveConflict);
                ValidateSyncDirectory(merged);

                EnsureDeletedFile();
                Directory.CreateDirectory(safety);
                for (int i = 0; i < DataFiles.Length; i++)
                {
                    string sourcePath = GetPath(DataFiles[i]);
                    if (!File.Exists(sourcePath) && DataFiles[i] == DeletedFile)
                    {
                        using (StreamWriter writer = new StreamWriter(
                            Path.Combine(safety, DataFiles[i]), false))
                        {
                        }
                        continue;
                    }
                    File.Copy(sourcePath, Path.Combine(safety, DataFiles[i]), true);
                }
                CopyReceiptsFolder(DataDirectory, safety);
                try
                {
                    CopyBackupFiles(merged);
                    EnsureRepairCategories();
                    EnsureExpenseCategories();
                    ToSettings.EnsureFile();
                    OverlayReceiptsFrom(staging, DataDirectory);
                    LoadCars();
                }
                catch (Exception installError)
                {
                    try
                    {
                        CopyBackupFiles(safety);
                    }
                    catch (Exception rollbackError)
                    {
                        throw new IOException("Синхронизация не завершена: " +
                            installError.Message + ". Не удалось вернуть прежние данные: " +
                            rollbackError.Message + ". Копия сохранена в " + safety);
                    }
                    throw;
                }
            }
            finally
            {
                TryDeleteDirectory(staging);
                TryDeleteDirectory(merged);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (path != null && Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        internal static int ReadSyncInt32(NetworkStream stream)
        {
            byte[] bytes = ReadSyncBytes(stream, 4);
            return bytes[0] | bytes[1] << 8 | bytes[2] << 16 | bytes[3] << 24;
        }

        internal static string ReadSyncMessage(NetworkStream stream)
        {
            int length = ReadSyncInt32(stream);
            if (length < 0 || length > 4096)
                throw new InvalidDataException("Неверное сообщение синхронизации.");
            return Encoding.UTF8.GetString(ReadSyncBytes(stream, length), 0, length);
        }

        internal static void WriteSyncInt32(NetworkStream stream, int value)
        {
            byte[] bytes = new byte[]
            {
                (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)
            };
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }

        private static void WriteSyncReceipts(NetworkStream stream, string dataDirectory, SyncCrypto crypto)
        {
            List<string> files = ListReceiptsForSync(dataDirectory);
            WriteSyncInt32(stream, files.Count);
            for (int i = 0; i < files.Count; i++)
            {
                string name = Path.GetFileName(files[i]);
                byte[] nameBytes = Encoding.UTF8.GetBytes(name);
                WriteSyncInt32(stream, nameBytes.Length);
                stream.Write(nameBytes, 0, nameBytes.Length);
                byte[] content = ReadFileBytes(files[i]);
                if (content.Length > MaxReceiptSyncFileBytes)
                    content = new byte[0];
                if (content.Length > 0)
                    crypto.TransformInPlace(content);
                WriteSyncInt32(stream, content.Length);
                if (content.Length > 0)
                    stream.Write(content, 0, content.Length);
            }
            stream.Flush();
        }

        private static void ReadSyncReceipts(NetworkStream stream, string directory, SyncCrypto crypto)
        {
            int count = ReadSyncInt32(stream);
            if (count < 0 || count > MaxReceiptSyncFiles)
                throw new InvalidDataException("Неверное число фото чеков в синхронизации.");
            string folder = Path.Combine(directory, ReceiptsFolderName);
            Directory.CreateDirectory(folder);
            for (int i = 0; i < count; i++)
            {
                int nameLength = ReadSyncInt32(stream);
                if (nameLength <= 0 || nameLength > 128)
                    throw new InvalidDataException("Неверное имя файла чека.");
                byte[] nameBytes = ReadSyncBytes(stream, nameLength);
                string name = Encoding.UTF8.GetString(nameBytes, 0, nameBytes.Length);
                if (!IsSafeReceiptFileName(name))
                    throw new InvalidDataException("Недопустимое имя файла чека.");
                int length = ReadSyncInt32(stream);
                if (length < 0 || length > MaxReceiptSyncFileBytes)
                    throw new InvalidDataException("Файл чека превышает лимит 2 МБ.");
                byte[] content = ReadSyncBytes(stream, length);
                if (content.Length > 0)
                    crypto.TransformInPlace(content);
                using (FileStream output = File.Create(Path.Combine(folder, name)))
                    output.Write(content, 0, content.Length);
            }
        }

        private static void OverlayReceiptsFrom(string sourceDataDir, string destDataDir)
        {
            string source = Path.Combine(sourceDataDir, ReceiptsFolderName);
            if (!Directory.Exists(source))
                return;
            string dest = Path.Combine(destDataDir, ReceiptsFolderName);
            Directory.CreateDirectory(dest);
            string[] files = Directory.GetFiles(source, "*.jpg");
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileName(files[i]);
                if (!IsSafeReceiptFileName(name))
                    continue;
                File.Copy(files[i], Path.Combine(dest, name), true);
            }
        }

        private static bool IsSafeReceiptFileName(string name)
        {
            if (name == null || name.Length == 0 || name.IndexOf("..") >= 0)
                return false;
            if (!name.EndsWith(".jpg") && !name.EndsWith(".JPG") && !name.EndsWith(".Jpg"))
                return false;
            string id = Path.GetFileNameWithoutExtension(name);
            return RecordIds.LooksLikeId(id);
        }

        private static List<string> ListReceiptsForSync(string dataDirectory)
        {
            List<string> result = new List<string>();
            string folder = Path.Combine(dataDirectory, ReceiptsFolderName);
            if (!Directory.Exists(folder))
                return result;
            Dictionary<string, bool> referenced = CollectReferencedReceiptIds(dataDirectory);
            string[] files = Directory.GetFiles(folder, "*.jpg");
            AddReceiptSyncCandidates(result, files, referenced, true);
            AddReceiptSyncCandidates(result, files, referenced, false);
            return result;
        }

        private static void AddReceiptSyncCandidates(
            List<string> result, string[] files, Dictionary<string, bool> referenced, bool requireReferenced)
        {
            for (int i = 0; i < files.Length; i++)
            {
                if (result.Count >= MaxReceiptSyncFiles)
                    return;
                string name = Path.GetFileName(files[i]);
                if (!IsSafeReceiptFileName(name))
                    continue;
                string id = Path.GetFileNameWithoutExtension(name);
                bool isRef = referenced.ContainsKey(id);
                if (requireReferenced != isRef)
                    continue;
                try
                {
                    FileInfo info = new FileInfo(files[i]);
                    if (info.Length <= 0 || info.Length > MaxReceiptSyncFileBytes)
                        continue;
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                bool already = false;
                for (int j = 0; j < result.Count; j++)
                {
                    if (String.Compare(result[j], files[i], true, CultureInfo.InvariantCulture) == 0)
                    {
                        already = true;
                        break;
                    }
                }
                if (!already)
                    result.Add(files[i]);
            }
        }

        private static Dictionary<string, bool> CollectReferencedReceiptIds(string dataDirectory)
        {
            Dictionary<string, bool> ids = new Dictionary<string, bool>(
                StringComparer.OrdinalIgnoreCase);
            AddReceiptIdsFromFile(ids, Path.Combine(dataDirectory, FuelFile));
            AddReceiptIdsFromFile(ids, Path.Combine(dataDirectory, RepairsFile));
            AddReceiptIdsFromFile(ids, Path.Combine(dataDirectory, ExpensesFile));
            return ids;
        }

        private static void AddReceiptIdsFromFile(Dictionary<string, bool> ids, string path)
        {
            if (!File.Exists(path))
                return;
            List<string[]> rows = ReadRowsFromPath(path);
            for (int i = 0; i < rows.Count; i++)
            {
                string id = RecordIds.GetId(rows[i]);
                if (id != null && !ids.ContainsKey(id))
                    ids.Add(id, true);
            }
        }

        private static bool IsValidSyncColumnCount(string fileName, int length)
        {
            // fuel: date|car|km|liters|cost|[price]|[comment]|[fullTank]|[id] — max 9
            if (fileName == FuelFile)
                return length >= 5 && length <= 9;
            if (fileName == CarsFile)
                return length == 1 || length == 2;
            if (fileName == RepairsFile)
                return length == 9 || length == 10;
            if (fileName == UtilitiesFile)
                return length == 8 || length == 9;
            if (fileName == ExpensesFile)
                return length == 6 || length == 7;
            return false;
        }

        private static void ValidateSyncDirectory(string directory)
        {
            for (int i = 0; i < CoreDataFiles.Length; i++)
            {
                string path = Path.Combine(directory, CoreDataFiles[i]);
                if (!File.Exists(path))
                    throw new InvalidDataException("Не получен файл " + CoreDataFiles[i] + ".");
                List<string[]> rows = ReadRowsFromPath(path);
                for (int j = 0; j < rows.Count; j++)
                    if (!IsValidSyncColumnCount(CoreDataFiles[i], rows[j].Length))
                        throw new InvalidDataException("Неверный формат записи в " +
                            CoreDataFiles[i] + ".");
                if (i == 0 && rows.Count == 0)
                    throw new InvalidDataException("Список автомобилей пуст.");
            }

            string categoriesPath = Path.Combine(directory, RepairCategoriesFile);
            if (File.Exists(categoriesPath))
            {
                List<string[]> categories = ReadRowsFromPath(categoriesPath);
                for (int i = 0; i < categories.Count; i++)
                {
                    if (categories[i].Length < 1 || categories[i].Length > 2 ||
                        categories[i][0].Trim().Length == 0)
                        throw new InvalidDataException("Неверный формат repair-categories.dat.");
                    if (categories[i].Length == 2 && !RecordIds.LooksLikeId(categories[i][1]))
                        throw new InvalidDataException("Неверный id в repair-categories.dat.");
                }
            }

            string expensesPath = Path.Combine(directory, ExpensesFile);
            if (File.Exists(expensesPath))
            {
                List<string[]> expenses = ReadRowsFromPath(expensesPath);
                for (int i = 0; i < expenses.Count; i++)
                    if (!IsValidSyncColumnCount(ExpensesFile, expenses[i].Length))
                        throw new InvalidDataException("Неверный формат expenses.dat.");
            }

            string expenseCategoriesPath = Path.Combine(directory, ExpenseCategoriesFile);
            if (File.Exists(expenseCategoriesPath))
            {
                List<string[]> expenseCategories = ReadRowsFromPath(expenseCategoriesPath);
                for (int i = 0; i < expenseCategories.Count; i++)
                {
                    if (expenseCategories[i].Length < 1 || expenseCategories[i].Length > 2 ||
                        expenseCategories[i][0].Trim().Length == 0)
                        throw new InvalidDataException("Неверный формат expense-categories.dat.");
                    if (expenseCategories[i].Length == 2 && !RecordIds.LooksLikeId(expenseCategories[i][1]))
                        throw new InvalidDataException("Неверный id в expense-categories.dat.");
                }
            }
        }

        private static void EnsureDeletedFile()
        {
            string path = GetPath(DeletedFile);
            if (!File.Exists(path))
            {
                using (StreamWriter writer = new StreamWriter(path, false))
                {
                }
            }
        }

        private static void EnsureRecordIdsOnDisk()
        {
            EnsureIdsInFile(CarsFile);
            EnsureIdsInFile(FuelFile);
            EnsureIdsInFile(RepairsFile);
            EnsureIdsInFile(UtilitiesFile);
            EnsureIdsInFile(RepairCategoriesFile);
            EnsureIdsInFile(ExpensesFile);
            EnsureIdsInFile(ExpenseCategoriesFile);
        }

        private static void EnsureIdsInFile(string fileName)
        {
            string path = GetPath(fileName);
            if (!File.Exists(path))
                return;
            List<string[]> rows = ReadRows(fileName);
            bool changed = false;
            string prefix = RecordIds.PrefixForFile(fileName);
            for (int i = 0; i < rows.Count; i++)
            {
                if (RecordIds.HasId(rows[i]))
                    continue;
                rows[i] = RecordIds.EnsureId(rows[i], prefix);
                changed = true;
            }
            if (changed)
                SaveRows(fileName, rows);
        }

        private static List<string[]> ReadRowsFromPath(string path)
        {
            List<string[]> rows = new List<string[]>();
            using (StreamReader reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                    if (line.Length > 0)
                        rows.Add(DecodeRow(line));
            }
            return rows;
        }

        private static byte[] ReadFileBytes(string path)
        {
            using (FileStream input = File.OpenRead(path))
            {
                if (input.Length > Int32.MaxValue)
                    throw new InvalidDataException("Слишком большой файл синхронизации.");
                byte[] content = new byte[(int)input.Length];
                int offset = 0;
                while (offset < content.Length)
                {
                    int count = input.Read(content, offset, content.Length - offset);
                    if (count == 0)
                        throw new EndOfStreamException("Не удалось прочитать файл для синхронизации.");
                    offset += count;
                }
                return content;
            }
        }

        private static byte[] ReadSyncBytes(NetworkStream stream, int length)
        {
            byte[] bytes = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                WaitForSyncBytes();
                int count = stream.Read(bytes, offset, length - offset);
                if (count == 0)
                    throw new EndOfStreamException("Соединение синхронизации прервано.");
                offset += count;
            }
            return bytes;
        }

        private static Socket activeSyncSocket;
        private static int syncReadTimeoutMicroseconds;
        private static readonly Guid InternetDestination =
            new Guid("436EF144-B4FB-4863-A041-8F905A62C572");
        private static readonly Guid WorkDestination =
            new Guid("A1182988-0D73-439e-87AD-2A5B369F808B");

        internal static void BindSyncSocket(Socket socket, int readTimeoutMicroseconds)
        {
            activeSyncSocket = socket;
            syncReadTimeoutMicroseconds = readTimeoutMicroseconds;
        }

        internal static IntPtr OpenRoute(string address)
        {
            try
            {
                Guid mapped = Guid.Empty;
                int mappedOk = ConnMgrMapURL("http://" + address + "/", ref mapped, 0);
                Guid first = mappedOk == 0 && !mapped.Equals(Guid.Empty)
                    ? mapped
                    : InternetDestination;
                Guid second = first.Equals(WorkDestination)
                    ? InternetDestination
                    : WorkDestination;

                IntPtr handle;
                int status;
                if (TryOpenRoute(first, out handle, out status))
                    return handle;
                ReleaseRouteHandle(handle);
                if (!second.Equals(first) && TryOpenRoute(second, out handle, out status))
                    return handle;
                ReleaseRouteHandle(handle);
            }
            catch (Exception)
            {
            }
            return IntPtr.Zero;
        }

        internal static void CloseRoute(IntPtr handle)
        {
            ReleaseRouteHandle(handle);
        }

        private static bool TryOpenRoute(Guid destination, out IntPtr handle, out int status)
        {
            handle = IntPtr.Zero;
            status = 0;
            ConnMgrConnectionInfo info = new ConnMgrConnectionInfo();
            info.cbSize = Marshal.SizeOf(typeof(ConnMgrConnectionInfo));
            info.dwParams = 0x1;
            info.dwFlags = 0;
            info.dwPriority = 0x8000;
            info.guidDestNet = destination;
            int result = ConnMgrEstablishConnectionSync(ref info, ref handle, 20000, ref status);
            return result == 0 && status == 0x10;
        }

        private static void ReleaseRouteHandle(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
                return;
            try
            {
                ConnMgrReleaseConnection(handle, 1);
            }
            catch (Exception)
            {
            }
        }

        private static void WaitForSyncBytes()
        {
            Socket socket = activeSyncSocket;
            if (socket == null || syncReadTimeoutMicroseconds <= 0)
                return;
            try
            {
                if (socket.Poll(syncReadTimeoutMicroseconds, SelectMode.SelectRead))
                    return;
            }
            catch (NotSupportedException)
            {
                return;
            }

            int seconds = syncReadTimeoutMicroseconds / 1000000;
            throw new IOException("Телефон не ответил за " +
                seconds.ToString(CultureInfo.InvariantCulture) +
                " с. Оставьте на Android открытым окно синхронизации.");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ConnMgrConnectionInfo
        {
            public int cbSize;
            public int dwParams;
            public int dwFlags;
            public int dwPriority;
            public int bExclusive;
            public int bDisabled;
            public Guid guidDestNet;
            public IntPtr hWnd;
            public int uMsg;
            public int lParam;
            public int ulMaxCost;
            public int ulMinRcvBw;
            public int ulMaxConnLatency;
        }

        [DllImport("cellcore.dll", CharSet = CharSet.Unicode)]
        private static extern int ConnMgrMapURL(string url, ref Guid networkGuid, int reserved);

        [DllImport("cellcore.dll")]
        private static extern int ConnMgrEstablishConnectionSync(
            ref ConnMgrConnectionInfo info, ref IntPtr connection, uint timeoutMs, ref int status);

        [DllImport("cellcore.dll")]
        private static extern int ConnMgrReleaseConnection(IntPtr connection, int cache);

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

        private static string GetLocalBackupRoot()
        {
            return Path.Combine(Path.GetDirectoryName(DataDirectory), "Alfa156_LocalBackups");
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
            string expensesSource = Path.Combine(backupPath, ExpensesFile);
            if (File.Exists(expensesSource))
                File.Copy(expensesSource, GetPath(ExpensesFile), true);
            else
                SaveRows(ExpensesFile, new List<string[]>());
            string expenseCategoriesSource = Path.Combine(backupPath, ExpenseCategoriesFile);
            if (File.Exists(expenseCategoriesSource))
                File.Copy(expenseCategoriesSource, GetPath(ExpenseCategoriesFile), true);
            else
                WriteExpenseCategories(CreateRepairCategoryRows(DefaultExpenseCategories));
            string settingsSource = Path.Combine(backupPath, SettingsFile);
            if (File.Exists(settingsSource))
                File.Copy(settingsSource, GetPath(SettingsFile), true);
            else
                ToSettings.EnsureFile();
            string deletedSource = Path.Combine(backupPath, DeletedFile);
            if (File.Exists(deletedSource))
                File.Copy(deletedSource, GetPath(DeletedFile), true);
            else
                EnsureDeletedFile();
            string receiptsDest = Path.Combine(DataDirectory, ReceiptsFolderName);
            if (Directory.Exists(receiptsDest))
            {
                string[] oldFiles = Directory.GetFiles(receiptsDest);
                for (int i = 0; i < oldFiles.Length; i++)
                    File.Delete(oldFiles[i]);
            }
            CopyReceiptsFolder(backupPath, DataDirectory);
            EnsureRepairCategories();
        }

        internal static void CreateStartMenuShortcut()
        {
            string executablePath = GetExecutablePath();

            // «Пуск → Программы»: основной ярлык, который виден в меню на КПК.
            string programsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            if (programsDirectory == null || programsDirectory.Length == 0)
                throw new IOException("Windows Mobile не вернул путь к меню «Пуск → Программы».");
            EnsureStartMenuShortcut(programsDirectory, executablePath);

            // Если старый ярлык лежит в корне «Пуск», перенацелим и его на текущий exe.
            string startMenuDirectory = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            if (startMenuDirectory != null && startMenuDirectory.Length > 0)
            {
                string rootShortcut = Path.Combine(startMenuDirectory, StartMenuShortcut);
                if (File.Exists(rootShortcut))
                    WriteStartMenuShortcut(rootShortcut, executablePath);
            }
        }

        private static void EnsureStartMenuShortcut(string directory, string executablePath)
        {
            Directory.CreateDirectory(directory);
            WriteStartMenuShortcut(Path.Combine(directory, StartMenuShortcut), executablePath);
        }

        private static void WriteStartMenuShortcut(string shortcutPath, string executablePath)
        {
            string target = "\"" + executablePath + "\"";
            string content = target.Length.ToString(CultureInfo.InvariantCulture) + "#" + target;

            if (File.Exists(shortcutPath))
            {
                string existing;
                using (StreamReader reader = new StreamReader(shortcutPath, Encoding.ASCII))
                    existing = reader.ReadToEnd();
                if (string.Equals(existing, content, StringComparison.Ordinal))
                    return;
            }

            using (StreamWriter writer = new StreamWriter(shortcutPath, false, Encoding.ASCII))
                writer.Write(content);
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

        internal static string LoadLastSyncAddress()
        {
            string path = GetPath(SyncAddressFile);
            if (!File.Exists(path))
                return "";
            using (StreamReader reader = new StreamReader(path))
            {
                string line = reader.ReadLine();
                return line == null ? "" : line.Trim();
            }
        }

        internal static void SaveLastSyncAddress(string address)
        {
            if (address == null)
                address = "";
            address = address.Trim();
            Directory.CreateDirectory(DataDirectory);
            using (StreamWriter writer = new StreamWriter(GetPath(SyncAddressFile), false))
                writer.WriteLine(address);
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
        internal static List<string[]> LoadExpenses()
        {
            EnsureExpenseFile();
            return ReadRows(ExpensesFile);
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
            List<string[]> categoryRows = ReadRows(RepairCategoriesFile);
            string deletedId = null;
            for (int i = 0; i < categoryRows.Count; i++)
                if (String.Equals(categoryRows[i][0], categories[categoryIndex], StringComparison.OrdinalIgnoreCase))
                {
                    deletedId = RecordIds.GetId(categoryRows[i]);
                    break;
                }
            categories.RemoveAt(categoryIndex);
            SaveRepairCategories(categories);
            if (deletedId != null)
                RecordIds.AddTombstone(DataDirectory, RepairCategoriesFile, deletedId);
        }


        private static void EnsureExpenseFile()
        {
            string path = GetPath(ExpensesFile);
            if (!File.Exists(path))
                SaveRows(ExpensesFile, new List<string[]>());
        }

        private static void EnsureExpenseCategoriesFile()
        {
            string path = GetPath(ExpenseCategoriesFile);
            if (File.Exists(path))
                return;
            WriteExpenseCategories(CreateRepairCategoryRows(DefaultExpenseCategories));
        }

        private static void EnsureExpenseCategories()
        {
            EnsureExpenseCategoriesFile();
            EnsureExpenseFile();
            List<string[]> categoryRows = ReadRows(ExpenseCategoriesFile);
            List<string> categories = new List<string>();
            bool changed = false;
            for (int i = 0; i < categoryRows.Count; i++)
            {
                if (categoryRows[i].Length < 1 || categoryRows[i][0].Trim().Length == 0)
                    throw new InvalidDataException("Неверная запись в expense-categories.dat.");
                if (categoryRows[i].Length > 2)
                    throw new InvalidDataException("Неверная запись в expense-categories.dat.");
                if (categoryRows[i].Length == 2 && !RecordIds.LooksLikeId(categoryRows[i][1]))
                    throw new InvalidDataException("Неверная запись в expense-categories.dat.");
                if (!RecordIds.HasId(categoryRows[i]))
                    changed = true;
                string name = categoryRows[i][0].Trim();
                if (ContainsCategory(categories, name))
                    changed = true;
                else
                    categories.Add(name);
            }
            List<string[]> expenses = ReadRows(ExpensesFile);
            for (int i = 0; i < expenses.Count; i++)
            {
                if (expenses[i].Length < 3)
                    continue;
                string category = expenses[i][2];
                if (category == null || category.Length == 0 || ContainsCategory(categories, category))
                    continue;
                categories.Add(category);
                changed = true;
            }
            if (categories.Count == 0)
            {
                for (int i = 0; i < DefaultExpenseCategories.Length; i++)
                    categories.Add(DefaultExpenseCategories[i]);
                changed = true;
            }
            if (changed)
                SaveExpenseCategories(categories);
        }

        internal static string[] LoadExpenseCategories()
        {
            EnsureExpenseCategories();
            List<string[]> rows = ReadRows(ExpenseCategoriesFile);
            string[] names = new string[rows.Count];
            for (int i = 0; i < rows.Count; i++)
                names[i] = rows[i][0];
            return names;
        }

        internal static void AddExpenseCategory(string name)
        {
            name = ValidateRepairCategoryName(name);
            List<string> categories = new List<string>(LoadExpenseCategories());
            if (ContainsCategory(categories, name))
                throw new InvalidOperationException("Такая категория уже есть.");
            categories.Add(name);
            SaveExpenseCategories(categories);
        }

        internal static void RenameExpenseCategory(string oldName, string newName)
        {
            newName = ValidateRepairCategoryName(newName);
            List<string> categories = new List<string>(LoadExpenseCategories());
            int categoryIndex = FindCategory(categories, oldName);
            if (categoryIndex < 0)
                throw new InvalidOperationException("Выбранная категория уже отсутствует.");
            if (String.Equals(categories[categoryIndex], newName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Новое название совпадает с текущим.");
            if (ContainsCategory(categories, newName))
                throw new InvalidOperationException("Категория с таким названием уже есть.");
            string previousName = categories[categoryIndex];
            List<string> previousCategories = new List<string>(categories);
            List<string[]> expenses = ReadRows(ExpensesFile);
            for (int i = 0; i < expenses.Count; i++)
                if (String.Equals(expenses[i][2], previousName, StringComparison.OrdinalIgnoreCase))
                    expenses[i][2] = newName;
            categories[categoryIndex] = newName;
            SaveExpenseCategories(categories);
            try
            {
                SaveRows(ExpensesFile, expenses);
            }
            catch (Exception renameError)
            {
                try
                {
                    SaveExpenseCategories(previousCategories);
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

        internal static void DeleteExpenseCategory(string name)
        {
            List<string> categories = new List<string>(LoadExpenseCategories());
            int categoryIndex = FindCategory(categories, name);
            if (categoryIndex < 0)
                throw new InvalidOperationException("Выбранная категория уже отсутствует.");
            List<string[]> expenses = ReadRows(ExpensesFile);
            for (int i = 0; i < expenses.Count; i++)
                if (String.Equals(expenses[i][2], categories[categoryIndex], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "Сначала перенесите записи из этой категории в другую.");
            List<string[]> categoryRows = ReadRows(ExpenseCategoriesFile);
            string deletedId = null;
            for (int i = 0; i < categoryRows.Count; i++)
                if (String.Equals(categoryRows[i][0], categories[categoryIndex], StringComparison.OrdinalIgnoreCase))
                {
                    deletedId = RecordIds.GetId(categoryRows[i]);
                    break;
                }
            categories.RemoveAt(categoryIndex);
            SaveExpenseCategories(categories);
            if (deletedId != null)
                RecordIds.AddTombstone(DataDirectory, ExpenseCategoriesFile, deletedId);
        }

        private static void SaveExpenseCategories(List<string> categories)
        {
            Dictionary<string, string> existingIds = new Dictionary<string, string>();
            if (File.Exists(GetPath(ExpenseCategoriesFile)))
            {
                List<string[]> previous = ReadRows(ExpenseCategoriesFile);
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
                if (existingIds.ContainsKey(categories[i]))
                    row = RecordIds.WithId(row, existingIds[categories[i]]);
                else
                    row = RecordIds.EnsureId(row, RecordIds.PrefixForFile(ExpenseCategoriesFile));
                rows.Add(row);
            }
            SaveRows(ExpenseCategoriesFile, rows);
        }

        private static void WriteExpenseCategories(List<string[]> rows)
        {
            using (StreamWriter writer = new StreamWriter(GetPath(ExpenseCategoriesFile), false))
            {
                for (int i = 0; i < rows.Count; i++)
                    writer.WriteLine(EncodeRow(rows[i]));
            }
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
                if (categoryRows[i].Length < 1 || categoryRows[i].Length > 2 ||
                    categoryRows[i][0].Trim().Length == 0)
                    throw new InvalidDataException("Неверная запись в repair-categories.dat.");
                if (categoryRows[i].Length == 2 && !RecordIds.LooksLikeId(categoryRows[i][1]))
                    throw new InvalidDataException("Неверная запись в repair-categories.dat.");
                if (!RecordIds.HasId(categoryRows[i]))
                    changed = true;
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
            Dictionary<string, string> existingIds = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            if (File.Exists(GetPath(RepairCategoriesFile)))
            {
                List<string[]> previous = ReadRows(RepairCategoriesFile);
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
                    row = RecordIds.EnsureId(row, RecordIds.PrefixForFile(RepairCategoriesFile));
                rows.Add(row);
            }
            SaveRows(RepairCategoriesFile, rows);
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

        internal static void AddExpense(string[] values)
        {
            EnsureExpenseFile();
            Append(ExpensesFile, values);
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

        internal static void UpdateExpenseAt(int index, string[] values)
        {
            EnsureExpenseFile();
            ReplaceAt(ExpensesFile, index, values);
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

        internal static void DeleteExpenseAt(int index)
        {
            EnsureExpenseFile();
            DeleteAt(ExpensesFile, index);
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
            name = ValidateCarName(name);
            if (ContainsCar(name))
                throw new InvalidOperationException("Такой автомобиль уже есть.");
            Append(CarsFile, RecordIds.EnsureId(new string[] { name },
                RecordIds.PrefixForFile(CarsFile)));
        }

        internal static void RenameCar(string oldName, string newName)
        {
            newName = ValidateCarName(newName);
            List<string> cars = LoadCarNames();
            int carIndex = FindCar(cars, oldName);
            if (carIndex < 0)
                throw new InvalidOperationException("Выбранный автомобиль уже отсутствует.");
            if (String.Equals(cars[carIndex], newName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Новое название совпадает с текущим.");
            if (ContainsCar(newName))
                throw new InvalidOperationException("Автомобиль с таким названием уже есть.");

            string previousName = cars[carIndex];
            List<string[]> fuel = LoadFuel();
            List<string[]> repairs = LoadRepairs();
            List<string[]> expenses = LoadExpenses();
            for (int i = 0; i < fuel.Count; i++)
                if (String.Equals(fuel[i][1], previousName, StringComparison.OrdinalIgnoreCase))
                    fuel[i][1] = newName;
            for (int i = 0; i < repairs.Count; i++)
                if (String.Equals(repairs[i][1], previousName, StringComparison.OrdinalIgnoreCase))
                    repairs[i][1] = newName;
            for (int i = 0; i < expenses.Count; i++)
                if (String.Equals(expenses[i][1], previousName, StringComparison.OrdinalIgnoreCase))
                    expenses[i][1] = newName;

            cars[carIndex] = newName;
            SaveCarNames(cars);
            try
            {
                SaveRows(FuelFile, fuel);
                SaveRows(RepairsFile, repairs);
                SaveRows(ExpensesFile, expenses);
            }
            catch (Exception renameError)
            {
                cars[carIndex] = previousName;
                try
                {
                    SaveCarNames(cars);
                }
                catch (Exception rollbackError)
                {
                    throw new IOException("Не удалось переименовать автомобиль: " +
                        renameError.Message + ". Не удалось вернуть список автомобилей: " +
                        rollbackError.Message);
                }
                throw;
            }
        }

        internal static void DeleteCar(string name)
        {
            List<string> cars = LoadCarNames();
            int carIndex = FindCar(cars, name);
            if (carIndex < 0)
                throw new InvalidOperationException("Выбранный автомобиль уже отсутствует.");
            if (cars.Count <= 1)
                throw new InvalidOperationException("Нельзя удалить единственный автомобиль.");

            string carName = cars[carIndex];
            List<string[]> carRows = ReadRows(CarsFile);
            string carId = null;
            for (int i = 0; i < carRows.Count; i++)
                if (String.Equals(carRows[i][0], carName, StringComparison.OrdinalIgnoreCase))
                {
                    carId = RecordIds.GetId(RecordIds.EnsureId(carRows[i],
                        RecordIds.PrefixForFile(CarsFile)));
                    break;
                }
            List<string[]> fuel = LoadFuel();
            List<string[]> repairs = LoadRepairs();
            List<string[]> expenses = LoadExpenses();
            List<string[]> keptFuel = new List<string[]>();
            List<string[]> keptRepairs = new List<string[]>();
            List<string[]> keptExpenses = new List<string[]>();
            for (int i = 0; i < fuel.Count; i++)
            {
                if (!String.Equals(fuel[i][1], carName, StringComparison.OrdinalIgnoreCase))
                    keptFuel.Add(fuel[i]);
                else
                {
                    string id = RecordIds.GetId(RecordIds.EnsureId(fuel[i],
                        RecordIds.PrefixForFile(FuelFile)));
                    if (id != null)
                        RecordIds.AddTombstone(DataDirectory, FuelFile, id);
                }
            }
            for (int i = 0; i < repairs.Count; i++)
            {
                if (!String.Equals(repairs[i][1], carName, StringComparison.OrdinalIgnoreCase))
                    keptRepairs.Add(repairs[i]);
                else
                {
                    string id = RecordIds.GetId(RecordIds.EnsureId(repairs[i],
                        RecordIds.PrefixForFile(RepairsFile)));
                    if (id != null)
                        RecordIds.AddTombstone(DataDirectory, RepairsFile, id);
                }
            }
            for (int i = 0; i < expenses.Count; i++)
            {
                if (!String.Equals(expenses[i][1], carName, StringComparison.OrdinalIgnoreCase))
                    keptExpenses.Add(expenses[i]);
                else
                {
                    string id = RecordIds.GetId(RecordIds.EnsureId(expenses[i],
                        RecordIds.PrefixForFile(ExpensesFile)));
                    if (id != null)
                        RecordIds.AddTombstone(DataDirectory, ExpensesFile, id);
                }
            }

            cars.RemoveAt(carIndex);
            SaveCarNames(cars);
            if (carId != null)
                RecordIds.AddTombstone(DataDirectory, CarsFile, carId);
            try
            {
                SaveRows(FuelFile, keptFuel);
                SaveRows(RepairsFile, keptRepairs);
                SaveRows(ExpensesFile, keptExpenses);
            }
            catch (Exception deleteError)
            {
                cars.Insert(carIndex, carName);
                try
                {
                    SaveCarNames(cars);
                }
                catch (Exception rollbackError)
                {
                    throw new IOException("Не удалось удалить автомобиль: " +
                        deleteError.Message + ". Не удалось вернуть список автомобилей: " +
                        rollbackError.Message);
                }
                throw;
            }
        }

        internal static bool ContainsCar(string name)
        {
            return FindCar(LoadCarNames(), name) >= 0;
        }

        private static string ValidateCarName(string name)
        {
            name = name == null ? "" : name.Trim();
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

        private static void SaveCarNames(List<string> cars)
        {
            Dictionary<string, string> existingIds = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            if (File.Exists(GetPath(CarsFile)))
            {
                List<string[]> previous = ReadRows(CarsFile);
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
                    row = RecordIds.EnsureId(row, RecordIds.PrefixForFile(CarsFile));
                rows.Add(row);
            }
            SaveRows(CarsFile, rows);
        }

        private static void Append(string fileName, string[] values)
        {
            values = RecordIds.EnsureId(values, RecordIds.PrefixForFile(fileName));
            using (StreamWriter writer = new StreamWriter(GetPath(fileName), true))
                writer.WriteLine(EncodeRow(values));
        }

        private static void ReplaceAt(string fileName, int index, string[] values)
        {
            List<string[]> records = ReadRows(fileName);
            ValidateIndex(records, index);
            string oldId = RecordIds.GetId(records[index]);
            if (!RecordIds.HasId(values) && oldId != null)
                values = RecordIds.WithId(values, oldId);
            else
                values = RecordIds.EnsureId(values, RecordIds.PrefixForFile(fileName));
            records[index] = values;
            SaveRows(fileName, records);
        }

        private static void DeleteAt(string fileName, int index)
        {
            List<string[]> records = ReadRows(fileName);
            ValidateIndex(records, index);
            string id = RecordIds.GetId(RecordIds.EnsureId(records[index],
                RecordIds.PrefixForFile(fileName)));
            records.RemoveAt(index);
            SaveRows(fileName, records);
            if (id != null)
                RecordIds.AddTombstone(DataDirectory, fileName, id);
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
