using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

using KiloLedger.Localization;
namespace CarExpenses
{
    internal static class DataStore
    {
        private const string DataDirectory = @"\My Documents\KiloLedger";
        private const string CarsFile = "cars.dat";
        private const string FuelFile = "fuel.dat";
        private const string RepairsFile = "repairs.dat";
        private const string UtilitiesFile = "utilities.dat";
        private const string RepairCategoriesFile = "repair-categories.dat";
        private const string ExpensesFile = "expenses.dat";
        private const string ExpenseCategoriesFile = "expense-categories.dat";
        private const string SettingsFile = "settings.dat";
        private const string CarsMetaFile = CarsMeta.FileName;
        private const string TemplatesFile = RecordTemplates.FileName;
        private const string RemindersFile = DateReminders.FileName;
        private const string NotesFile = CarNotes.FileName;
        private const string TripsFile = "trips.dat";
        private const string DeletedFile = "deleted.dat";
        private const string SyncAddressFile = "last-sync-ip.txt";
        private const string StartMenuShortcut = "KiloLedger.lnk";
        private static readonly string[] CoreDataFiles = new string[]
        {
            CarsFile, FuelFile, RepairsFile, UtilitiesFile
        };
        private static readonly string[] DataFiles = new string[]
        {
            CarsFile, FuelFile, RepairsFile, UtilitiesFile, RepairCategoriesFile,
            ExpensesFile, ExpenseCategoriesFile, SettingsFile, CarsMetaFile, TemplatesFile,
            RemindersFile, NotesFile, TripsFile, DeletedFile
        };

        private static readonly string[] DefaultRepairCategories = new string[]
        {
            L10n.Get("service.oil_short"), L10n.Get("service.belt_short"), L10n.Get("category.parts"), L10n.Get("category.fluids"), L10n.Get("category.brakes"), L10n.Get("category.body"), L10n.Get("category.consumables")
        };

        private static readonly string[] DefaultExpenseCategories = new string[]
        {
            L10n.Get("reminder.insurance"), L10n.Get("reminder.inspection"), L10n.Get("category.fines"), L10n.Get("category.wash"), L10n.Get("category.parking"), L10n.Get("label.other")
        };

        internal static readonly string[] UtilityLabels = new string[]
        {
            L10n.Get("utility.label_0"), L10n.Get("utility.label_1"), L10n.Get("utility.label_2"), L10n.Get("utility.label_3"),
            L10n.Get("utility.label_4"), L10n.Get("utility.label_5"), L10n.Get("utility.label_6")
        };
        internal static string GetUtilityLabel(int index)
        {
            if (index >= 0 && index < UtilityLabels.Length)
                return KiloLedger.Localization.L10n.Get("utility.label_" + index);
            return "";
        }


        internal static void Initialize()
        {
            Directory.CreateDirectory(DataDirectory);
            if (!File.Exists(GetPath(CarsFile)))
            {
                using (StreamWriter writer = new StreamWriter(GetPath(CarsFile), false))
                    writer.WriteLine(L10n.Get("label.default_car_name"));
            }
            EnsureFile(FuelFile, null);
            EnsureFile(RepairsFile, null);
            EnsureFile(UtilitiesFile, null);
            EnsureRepairCategoriesFile();
            EnsureRepairCategories();
            EnsureExpenseCategoriesFile();
            EnsureExpenseCategories();
            EnsureExpenseFile();
            ToSettings.EnsureFile();
            CarsMeta.EnsureFile();
            RecordTemplates.EnsureFile();
            DateReminders.EnsureFile();
            CarNotes.EnsureFile();
            EnsureTripsFile();
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
            CarsMeta.EnsureFile();
            RecordTemplates.EnsureFile();
            DateReminders.EnsureFile();
            CarNotes.EnsureFile();
            EnsureTripsFile();
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
            WriteSnapshotMetadata(backupPath);
            try { ToSettings.MarkBackupDone(); }
            catch { /* reminder stamp must not fail backup */ }
            return backupPath;
        }

        /// <summary>Count .dat + Receipts files that a full backup will include.</summary>
        internal static int CountBackupFiles()
        {
            int count = 0;
            for (int i = 0; i < DataFiles.Length; i++)
            {
                if (File.Exists(GetPath(DataFiles[i])))
                    count++;
            }
            string receipts = Path.Combine(DataDirectory, ReceiptsFolderName);
            if (Directory.Exists(receipts))
            {
                string[] files = Directory.GetFiles(receipts);
                count += files.Length;
            }
            return count;
        }

        private const string ReceiptsFolderName = "Receipts";
        /// <summary>Wi‑Fi receipt limits (1.15+): skip &gt; 2 MB; max 50 files per sync.</summary>
        internal const int MaxReceiptSyncFileBytes = 2 * 1024 * 1024;
        internal const int MaxReceiptSyncFiles = 50;

        internal static string ReceiptPathForId(string recordId)
        {
            if (String.IsNullOrEmpty(recordId) || !RecordIds.LooksLikeId(recordId))
                throw new ArgumentException(L10n.Get("error.invalid_receipt_id"), "recordId");
            return Path.Combine(Path.Combine(DataDirectory, ReceiptsFolderName),
                recordId + ".jpg");
        }

        internal static bool ReceiptExists(string recordId)
        {
            if (String.IsNullOrEmpty(recordId) || !RecordIds.LooksLikeId(recordId))
                return false;
            return File.Exists(ReceiptPathForId(recordId));
        }

        internal static void DeleteReceipt(string recordId)
        {
            if (!ReceiptExists(recordId))
                return;
            File.Delete(ReceiptPathForId(recordId));
        }

        private static void CopyReceiptsFolder(string sourceDataDir, string destDataDir)
        {
            string source = Path.Combine(sourceDataDir, ReceiptsFolderName);
            if (!Directory.Exists(source))
                return;

            string dest = Path.Combine(destDataDir, ReceiptsFolderName);
            if (Directory.Exists(dest))
            {
                string[] oldFiles = Directory.GetFiles(dest);
                for (int i = 0; i < oldFiles.Length; i++)
                    File.Delete(oldFiles[i]);
            }
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
                throw new ArgumentException(L10n.Get("error.invalid_backup_folder"), "folderName");
            if (Path.GetFileName(folderName) != folderName)
                throw new InvalidDataException(L10n.Get("error.invalid_backup_folder"));

            string[] backupNames = ListBackupNames(backupRoot);
            bool found = false;
            for (int i = 0; i < backupNames.Length; i++)
                if (backupNames[i] == folderName)
                    found = true;
            if (!found)
                throw new InvalidDataException(L10n.Get("error.invalid_incomplete_backup"));

            string backupPath = Path.Combine(backupRoot, folderName);
            if (!HasCompleteBackup(backupPath))
                throw new InvalidDataException(L10n.Get("error.backup_incomplete_or_missing"));
            ValidateSnapshotJson(backupPath);

            string safetyBackup = AllocateSafetyBackupPath(backupRoot);
            Directory.CreateDirectory(safetyBackup);
            for (int i = 0; i < DataFiles.Length; i++)
            {
                string sourcePath = GetPath(DataFiles[i]);
                if (File.Exists(sourcePath))
                    File.Copy(sourcePath, Path.Combine(safetyBackup, DataFiles[i]), true);
            }
            CopyReceiptsFolder(DataDirectory, safetyBackup);
            try { ToSettings.MarkBackupDone(); }
            catch { }
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
                    throw new IOException((L10n.Get("error.restore_aborted") + " ") + restoreError.Message +
                        (L10n.Get("error.auto_rollback_failed") + " ") + rollbackError.Message +
                        (L10n.Get("msg.current_backup_saved_in") + " ") + safetyBackup);
                }
                throw;
            }
        }

        internal static void WriteSyncSnapshot(NetworkStream stream, string pairingCode)
        {
            EnsureExpenseCategories();
            ToSettings.EnsureFile();
            CarsMeta.EnsureFile();
            RecordTemplates.EnsureFile();
            DateReminders.EnsureFile();
            CarNotes.EnsureFile();
            EnsureTripsFile();
            EnsureDeletedFile();
            EnsureRecordIdsOnDisk();
            using (SyncCrypto crypto = new SyncCrypto(pairingCode))
            {
                for (int i = 0; i < DataFiles.Length; i++)
                {
                    string path = GetPath(DataFiles[i]);
                    byte[] content = File.Exists(path) ? ReadFileBytes(path) : new byte[0];
                    if (content.Length > 16 * 1024 * 1024)
                        throw new InvalidDataException((L10n.Get("menu.file") + " ") + DataFiles[i] +
                            (" " + L10n.Get("error.exceeds_sync_size")));
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
            string staging = Path.Combine(parent, "KiloLedger_Sync_" + Guid.NewGuid().ToString("N"));
            string merged = Path.Combine(parent, "KiloLedger_SyncMerged_" + Guid.NewGuid().ToString("N"));
            string safety = AllocateSafetyBackupPath(parent);
            Directory.CreateDirectory(staging);
            try
            {
                using (SyncCrypto crypto = new SyncCrypto(pairingCode))
                {
                    for (int i = 0; i < DataFiles.Length; i++)
                    {
                        int length = ReadSyncInt32(stream);
                        if (length < 0 || length > 16 * 1024 * 1024)
                            throw new InvalidDataException(L10n.Get("error.sync_file_too_large"));
                        byte[] content = ReadSyncBytes(stream, length);
                        if (content.Length > 0)
                            crypto.TransformInPlace(content);
                        using (FileStream output = File.Create(Path.Combine(staging, DataFiles[i])))
                            output.Write(content, 0, content.Length);
                    }
                    ReadSyncReceipts(stream, staging, crypto);
                }
                ValidateSyncDirectory(staging);

                SyncPlan defaultPlan = new SyncPlan();
                SyncJournal preview = SyncMerge.BuildPreview(DataDirectory, staging, defaultPlan);
                preview.SafetyPath = safety;
                StringBuilder previewText = new StringBuilder();
                previewText.Append(SyncMerge.FormatSummary(
                    SyncMerge.AnalyzeDirectories(DataDirectory, staging)));
                previewText.Append("\r\n\r\n");
                previewText.Append(preview.FormatPreview());
                SyncPlan plan = resolvePlan(previewText.ToString());
                if (plan == null || plan.Cancelled)
                    throw new InvalidOperationException(L10n.Get("sync.cancelled"));

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
                try { ToSettings.MarkBackupDone(); }
                catch { }
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
                        if (SyncMerge.LastJournal != null)
                            SyncMerge.LastJournal.RollbackReason =
                                installError.Message + (L10n.Get("error.rollback_data_failed") + " ") +
                                rollbackError.Message;
                        throw new IOException((L10n.Get("error.sync_incomplete") + " ") +
                            installError.Message + (L10n.Get("error.rollback_data_failed") + " ") +
                            rollbackError.Message + (L10n.Get("msg.backup_saved_in") + " ") + safety);
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

        private static void WriteSnapshotMetadata(string backupPath)
        {
            string token = ToSettings.GetOrCreateSnapshotDeviceToken(DataDirectory);
            string created = DateTime.UtcNow.ToString(
                "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            string json = "{\"schema\":\"kiloledger.snapshot\",\"schemaVersion\":1,\"appVersion\":\"1.39.3\"," +
                "\"createdUtc\":\"" + created + "\",\"deviceToken\":\"" + token + "\"}";
            using (StreamWriter writer = new StreamWriter(
                Path.Combine(backupPath, "snapshot.json"), false, Encoding.UTF8))
                writer.Write(json);
        }

        private static void ValidateSnapshotJson(string backupPath)
        {
            string path = Path.Combine(backupPath, "snapshot.json");
            if (!File.Exists(path))
                return;
            string json;
            using (StreamReader reader = new StreamReader(path, Encoding.UTF8))
                json = reader.ReadToEnd();
            if (json == null || json.Length == 0)
                throw new InvalidDataException(L10n.Get("error.empty_snapshot"));
            if (json.IndexOf("\"schema\":\"kiloledger.snapshot\"", StringComparison.Ordinal) < 0 &&
                json.IndexOf("\"schema\": \"kiloledger.snapshot\"", StringComparison.Ordinal) < 0)
                throw new InvalidDataException(L10n.Get("error.unsupported_snapshot_schema"));
            if (json.IndexOf("\"schemaVersion\":1", StringComparison.Ordinal) < 0 &&
                json.IndexOf("\"schemaVersion\": 1", StringComparison.Ordinal) < 0)
                throw new InvalidDataException(L10n.Get("error.unsupported_snapshot_version"));
            if (json.IndexOf("\"createdUtc\"", StringComparison.Ordinal) < 0 ||
                json.IndexOf("\"deviceToken\"", StringComparison.Ordinal) < 0)
                throw new InvalidDataException(L10n.Get("error.snapshot_missing_meta"));
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
                throw new InvalidDataException(L10n.Get("error.invalid_sync_message"));
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
                throw new InvalidDataException(L10n.Get("error.invalid_receipt_sync_count"));
            string folder = Path.Combine(directory, ReceiptsFolderName);
            Directory.CreateDirectory(folder);
            for (int i = 0; i < count; i++)
            {
                int nameLength = ReadSyncInt32(stream);
                if (nameLength <= 0 || nameLength > 128)
                    throw new InvalidDataException(L10n.Get("error.invalid_receipt_filename"));
                byte[] nameBytes = ReadSyncBytes(stream, nameLength);
                string name = Encoding.UTF8.GetString(nameBytes, 0, nameBytes.Length);
                if (!IsSafeReceiptFileName(name))
                    throw new InvalidDataException(L10n.Get("error.invalid_receipt_name_dot"));
                int length = ReadSyncInt32(stream);
                if (length < 0 || length > MaxReceiptSyncFileBytes)
                    throw new InvalidDataException(L10n.Get("error.receipt_over_2mb"));
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
            // fuel: date|car|km|liters|cost|[price]|[comment]|[fullTank]|[purpose]|[station]|[id] — max 11
            if (fileName == FuelFile)
                return length >= 5 && length <= 11;
            if (fileName == CarsFile)
                return length == 1 || length == 2;
            if (fileName == RepairsFile)
                return length == 9 || length == 10;
            if (fileName == UtilitiesFile)
                return length == 8 || length == 9;
            if (fileName == ExpensesFile)
                return length == 6 || length == 7;
            if (fileName == CarsMetaFile)
                return length >= 1 && length <= 7;
            if (fileName == TemplatesFile)
                return length >= 3 && length <= 12;
            if (fileName == RemindersFile)
                return length == 4 || length == 5;
            if (fileName == NotesFile)
                return length == 3 || length == 4;
            if (fileName == TripsFile)
                return length == 7 || length == 8;
            return false;
        }

        private static bool IsValidTripRow(string[] row)
        {
            if (row == null)
                return false;
            string[] content = RecordIds.StripId(row);
            if (content.Length != 7)
                return false;
            if ((content[0] ?? "").Trim().Length != 10 ||
                (content[1] ?? "").Trim().Length == 0)
                return false;
            decimal startKm;
            decimal endKm;
            if (!TryNumber(content[2], out startKm) ||
                !TryNumber(content[3], out endKm))
                return false;
            if (startKm < 0 || endKm < startKm)
                return false;
            if (RecordIds.HasId(row) && !RecordIds.LooksLikeId(RecordIds.GetId(row)))
                return false;
            return true;
        }

        private static void ValidateSyncDirectory(string directory)
        {
            for (int i = 0; i < CoreDataFiles.Length; i++)
            {
                string path = Path.Combine(directory, CoreDataFiles[i]);
                if (!File.Exists(path))
                    throw new InvalidDataException((L10n.Get("error.file_not_received") + " ") + CoreDataFiles[i] + ".");
                List<string[]> rows = ReadRowsFromPath(path);
                for (int j = 0; j < rows.Count; j++)
                    if (!IsValidSyncColumnCount(CoreDataFiles[i], rows[j].Length))
                        throw new InvalidDataException((L10n.Get("error.invalid_record_format_in") + " ") +
                            CoreDataFiles[i] + ".");
                if (i == 0 && rows.Count == 0)
                    throw new InvalidDataException(L10n.Get("error.car_list_empty"));
            }

            string categoriesPath = Path.Combine(directory, RepairCategoriesFile);
            if (File.Exists(categoriesPath))
            {
                List<string[]> categories = ReadRowsFromPath(categoriesPath);
                for (int i = 0; i < categories.Count; i++)
                {
                    if (categories[i].Length < 1 || categories[i].Length > 2 ||
                        categories[i][0].Trim().Length == 0)
                        throw new InvalidDataException(L10n.Get("error.invalid_repair_cats_fmt"));
                    if (categories[i].Length == 2 && !RecordIds.LooksLikeId(categories[i][1]))
                        throw new InvalidDataException(L10n.Get("error.invalid_id_repair_cats"));
                }
            }

            string expensesPath = Path.Combine(directory, ExpensesFile);
            if (File.Exists(expensesPath))
            {
                List<string[]> expenses = ReadRowsFromPath(expensesPath);
                for (int i = 0; i < expenses.Count; i++)
                    if (!IsValidSyncColumnCount(ExpensesFile, expenses[i].Length))
                        throw new InvalidDataException(L10n.Get("error.invalid_expenses_dat"));
            }

            string expenseCategoriesPath = Path.Combine(directory, ExpenseCategoriesFile);
            if (File.Exists(expenseCategoriesPath))
            {
                List<string[]> expenseCategories = ReadRowsFromPath(expenseCategoriesPath);
                for (int i = 0; i < expenseCategories.Count; i++)
                {
                    if (expenseCategories[i].Length < 1 || expenseCategories[i].Length > 2 ||
                        expenseCategories[i][0].Trim().Length == 0)
                        throw new InvalidDataException(L10n.Get("error.invalid_expense_cats_fmt"));
                    if (expenseCategories[i].Length == 2 && !RecordIds.LooksLikeId(expenseCategories[i][1]))
                        throw new InvalidDataException(L10n.Get("error.invalid_id_expense_cats"));
                }
            }

            string carsMetaPath = Path.Combine(directory, CarsMetaFile);
            if (File.Exists(carsMetaPath))
            {
                List<string[]> meta = ReadRowsFromPath(carsMetaPath);
                for (int i = 0; i < meta.Count; i++)
                    if (!CarsMeta.IsValidRow(meta[i]))
                        throw new InvalidDataException(L10n.Get("error.invalid_cars_meta_dat"));
            }

            string templatesPath = Path.Combine(directory, TemplatesFile);
            if (File.Exists(templatesPath))
            {
                List<string[]> templates = ReadRowsFromPath(templatesPath);
                for (int i = 0; i < templates.Count; i++)
                    if (!RecordTemplates.IsValidRow(templates[i]))
                        throw new InvalidDataException(L10n.Get("error.invalid_templates_dat"));
            }

            string remindersPath = Path.Combine(directory, RemindersFile);
            if (File.Exists(remindersPath))
            {
                List<string[]> reminders = ReadRowsFromPath(remindersPath);
                for (int i = 0; i < reminders.Count; i++)
                    if (!DateReminders.IsValidRow(reminders[i]))
                        throw new InvalidDataException(L10n.Get("error.invalid_reminders_dat"));
            }

            string notesPath = Path.Combine(directory, NotesFile);
            if (File.Exists(notesPath))
            {
                List<string[]> notes = ReadRowsFromPath(notesPath);
                for (int i = 0; i < notes.Count; i++)
                    if (!CarNotes.IsValidRow(notes[i]))
                        throw new InvalidDataException(L10n.Get("error.invalid_notes_dat"));
            }

            string tripsPath = Path.Combine(directory, TripsFile);
            if (File.Exists(tripsPath))
            {
                List<string[]> trips = ReadRowsFromPath(tripsPath);
                for (int i = 0; i < trips.Count; i++)
                    if (!IsValidTripRow(trips[i]))
                        throw new InvalidDataException(L10n.Get("error.invalid_trips_dat"));
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
            EnsureIdsInFile(CarsMetaFile);
            EnsureIdsInFile(TemplatesFile);
            EnsureIdsInFile(RemindersFile);
            EnsureIdsInFile(NotesFile);
            EnsureIdsInFile(TripsFile);
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
                    throw new InvalidDataException(L10n.Get("error.sync_file_too_big"));
                byte[] content = new byte[(int)input.Length];
                int offset = 0;
                while (offset < content.Length)
                {
                    int count = input.Read(content, offset, content.Length - offset);
                    if (count == 0)
                        throw new EndOfStreamException(L10n.Get("error.read_sync_file_failed"));
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
                    throw new EndOfStreamException(L10n.Get("error.sync_connection_interrupted"));
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
            throw new IOException((L10n.Get("error.phone_no_response_in") + " ") +
                seconds.ToString(CultureInfo.InvariantCulture) +
                (" " + L10n.Get("sync.wait_hint_android")));
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
                    return Path.Combine(cardRoots[i], "KiloLedger_Backups");
            throw new DirectoryNotFoundException(
                L10n.Get("error.sd_card_not_found"));
        }

        private static string GetLocalBackupRoot()
        {
            return Path.Combine(Path.GetDirectoryName(DataDirectory), "KiloLedger_LocalBackups");
        }

        /// <summary>
        /// Allocates Safety_yyyyMMdd_HHmmss under parent (does not create the directory).
        /// </summary>
        internal static string AllocateSafetyBackupPath(string parentDirectory)
        {
            string baseName = "Safety_" +
                DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string path = Path.Combine(parentDirectory, baseName);
            int suffix = 1;
            while (Directory.Exists(path))
            {
                path = Path.Combine(parentDirectory, baseName + "_" +
                    suffix.ToString(CultureInfo.InvariantCulture));
                suffix++;
            }
            return path;
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
            string carsMetaSource = Path.Combine(backupPath, CarsMetaFile);
            if (File.Exists(carsMetaSource))
                File.Copy(carsMetaSource, GetPath(CarsMetaFile), true);
            else
                CarsMeta.EnsureFile();
            string templatesSource = Path.Combine(backupPath, TemplatesFile);
            if (File.Exists(templatesSource))
                File.Copy(templatesSource, GetPath(TemplatesFile), true);
            else
                RecordTemplates.EnsureFile();
            string remindersSource = Path.Combine(backupPath, RemindersFile);
            if (File.Exists(remindersSource))
                File.Copy(remindersSource, GetPath(RemindersFile), true);
            else
                DateReminders.EnsureFile();
            string notesSource = Path.Combine(backupPath, NotesFile);
            if (File.Exists(notesSource))
                File.Copy(notesSource, GetPath(NotesFile), true);
            else
                CarNotes.EnsureFile();
            string tripsSource = Path.Combine(backupPath, TripsFile);
            if (File.Exists(tripsSource))
                File.Copy(tripsSource, GetPath(TripsFile), true);
            else
                SaveRows(TripsFile, new List<string[]>());
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
                throw new IOException(L10n.Get("error.wm_start_menu_path"));
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
                throw new IOException(L10n.Get("error.cannot_find_exe_path"));
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
                    throw new InvalidOperationException((L10n.Get("error.seed_file_not_found") + " ") + resourceName);

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
                throw new InvalidDataException(L10n.Get("error.car_list_empty_dat"));
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
                throw new InvalidOperationException(L10n.Get("error.category_duplicate"));
            categories.Add(name);
            SaveRepairCategories(categories);
        }

        internal static void RenameRepairCategory(string oldName, string newName)
        {
            newName = ValidateRepairCategoryName(newName);
            List<string> categories = RepairCategoryNames();
            int categoryIndex = FindCategory(categories, oldName);
            if (categoryIndex < 0)
                throw new InvalidOperationException(L10n.Get("error.category_missing"));
            if (String.Equals(categories[categoryIndex], newName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(L10n.Get("error.same_name"));
            if (ContainsCategory(categories, newName))
                throw new InvalidOperationException(L10n.Get("error.category_exists"));

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
                    throw new IOException((L10n.Get("error.rename_category_failed") + " ") +
                        renameError.Message + (L10n.Get("error.rollback_categories_failed") + " ") +
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
                throw new InvalidOperationException(L10n.Get("error.category_missing"));
            List<string[]> repairs = LoadRepairs();
            for (int i = 0; i < repairs.Count; i++)
                if (String.Equals(repairs[i][3], categories[categoryIndex], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        L10n.Get("error.move_records_from_category"));
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


        private static void EnsureTripsFile()
        {
            string path = GetPath(TripsFile);
            if (!File.Exists(path))
                SaveRows(TripsFile, new List<string[]>());
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
                    throw new InvalidDataException(L10n.Get("error.invalid_expense_categories_dat"));
                if (categoryRows[i].Length > 2)
                    throw new InvalidDataException(L10n.Get("error.invalid_expense_categories_dat"));
                if (categoryRows[i].Length == 2 && !RecordIds.LooksLikeId(categoryRows[i][1]))
                    throw new InvalidDataException(L10n.Get("error.invalid_expense_categories_dat"));
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
                throw new InvalidOperationException(L10n.Get("error.category_duplicate"));
            categories.Add(name);
            SaveExpenseCategories(categories);
        }

        internal static void RenameExpenseCategory(string oldName, string newName)
        {
            newName = ValidateRepairCategoryName(newName);
            List<string> categories = new List<string>(LoadExpenseCategories());
            int categoryIndex = FindCategory(categories, oldName);
            if (categoryIndex < 0)
                throw new InvalidOperationException(L10n.Get("error.category_missing"));
            if (String.Equals(categories[categoryIndex], newName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(L10n.Get("error.same_name"));
            if (ContainsCategory(categories, newName))
                throw new InvalidOperationException(L10n.Get("error.category_exists"));
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
                    throw new IOException((L10n.Get("error.rename_category_failed") + " ") +
                        renameError.Message + (L10n.Get("error.rollback_categories_failed") + " ") +
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
                throw new InvalidOperationException(L10n.Get("error.category_missing"));
            List<string[]> expenses = ReadRows(ExpensesFile);
            for (int i = 0; i < expenses.Count; i++)
                if (String.Equals(expenses[i][2], categories[categoryIndex], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        L10n.Get("error.move_records_from_category"));
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
                    throw new InvalidDataException(L10n.Get("error.invalid_repair_categories_dat"));
                if (categoryRows[i].Length == 2 && !RecordIds.LooksLikeId(categoryRows[i][1]))
                    throw new InvalidDataException(L10n.Get("error.invalid_repair_categories_dat"));
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
                throw new InvalidOperationException(L10n.Get("error.enter_category_name"));
            if (String.Equals(name, "Все категории", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(L10n.Get("error.reserved_for_filter"));
            if (name.IndexOf('\r') >= 0 || name.IndexOf('\n') >= 0)
                throw new InvalidOperationException(L10n.Get("error.category_name_newline"));
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

        internal static List<string[]> LoadTrips()
        {
            EnsureTripsFile();
            return ReadRows(TripsFile);
        }

        internal static void AddTrip(string[] values)
        {
            EnsureTripsFile();
            Append(TripsFile, values);
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
                    throw new InvalidOperationException(L10n.Get("error.utility_month_exists"));
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
                throw new InvalidOperationException(L10n.Get("error.car_duplicate"));
            Append(CarsFile, RecordIds.EnsureId(new string[] { name },
                RecordIds.PrefixForFile(CarsFile)));
        }

        internal static void RenameCar(string oldName, string newName)
        {
            newName = ValidateCarName(newName);
            List<string> cars = LoadCarNames();
            int carIndex = FindCar(cars, oldName);
            if (carIndex < 0)
                throw new InvalidOperationException(L10n.Get("error.car_missing"));
            if (String.Equals(cars[carIndex], newName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(L10n.Get("error.same_name"));
            if (ContainsCar(newName))
                throw new InvalidOperationException(L10n.Get("error.car_exists"));

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
                CarsMeta.RenameCar(previousName, newName);
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
                    throw new IOException((L10n.Get("error.rename_car_failed") + " ") +
                        renameError.Message + (L10n.Get("error.rollback_cars_failed") + " ") +
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
                throw new InvalidOperationException(L10n.Get("error.car_missing"));
            if (cars.Count <= 1)
                throw new InvalidOperationException(L10n.Get("error.cannot_delete_only_car"));

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
                CarsMeta.DeleteCar(carName);
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
                    throw new IOException((L10n.Get("error.delete_car_failed") + " ") +
                        deleteError.Message + (L10n.Get("error.rollback_cars_failed") + " ") +
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
                throw new InvalidOperationException(L10n.Get("error.enter_car_name"));
            if (name.IndexOf('\n') >= 0 || name.IndexOf('\r') >= 0)
                throw new InvalidOperationException(L10n.Get("error.car_name_newline"));
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
                throw new ArgumentOutOfRangeException("index", L10n.Get("error.record_missing"));
        }

        private static void SaveRows(string fileName, List<string[]> records)
        {
            string path = GetPath(fileName);
            string backupPath = path + ".bak";
            string temporaryPath = path + ".tmp";

            if (File.Exists(path))
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
                if (File.Exists(path))
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
