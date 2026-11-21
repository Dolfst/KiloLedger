using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Alfa156.Desktop.Data
{
    internal sealed class AlfaRepository
    {
        private const string AppVersion = "1.36.0";
        public string DirectoryPath { get; private set; }
        public List<string[]> Cars { get; private set; }
        public List<string[]> Fuel { get; private set; }
        public List<string[]> Repairs { get; private set; }
        public List<string[]> Utilities { get; private set; }
        public List<string[]> Categories { get; private set; }
        public List<string[]> Expenses { get; private set; }
        public List<string[]> ExpenseCategories { get; private set; }
        public List<string[]> Settings { get; set; }
        public List<string[]> CarsMetaRows { get; set; }
        public List<string[]> TemplatesRows { get; set; }
        public List<string[]> RemindersRows { get; set; }
        public List<string[]> NotesRows { get; set; }
        public List<string[]> TripsRows { get; set; }
        public List<Tombstone> Deleted { get; set; }

        public AlfaRepository()
        {
            Cars = new List<string[]>();
            Fuel = new List<string[]>();
            Repairs = new List<string[]>();
            Utilities = new List<string[]>();
            Categories = new List<string[]>();
            Expenses = new List<string[]>();
            ExpenseCategories = new List<string[]>();
            Settings = ToSettings.ToRows(new ToIntervals());
            CarsMetaRows = new List<string[]>();
            TemplatesRows = new List<string[]>();
            RemindersRows = new List<string[]>();
            NotesRows = new List<string[]>();
            TripsRows = new List<string[]>();
            Deleted = new List<Tombstone>();
        }

        public void CreateNew(string directory)
        {
            Directory.CreateDirectory(directory);
            Cars = new List<string[]> { new string[] { "Alfa Romeo 156" } };
            Fuel = new List<string[]>();
            Repairs = new List<string[]>();
            Utilities = new List<string[]>();
            Categories = new List<string[]>();
            for (int i = 0; i < AlfaCodec.DefaultRepairCategories.Length; i++)
                Categories.Add(new string[] { AlfaCodec.DefaultRepairCategories[i] });
            Expenses = new List<string[]>();
            ExpenseCategories = new List<string[]>();
            for (int i = 0; i < AlfaCodec.DefaultExpenseCategories.Length; i++)
                ExpenseCategories.Add(new string[] { AlfaCodec.DefaultExpenseCategories[i] });
            Settings = ToSettings.ToRows(new ToIntervals());
            CarsMetaRows = new List<string[]>();
            TemplatesRows = new List<string[]>();
            RemindersRows = new List<string[]>();
            NotesRows = new List<string[]>();
            TripsRows = new List<string[]>();
            Deleted = new List<Tombstone>();
            DirectoryPath = directory;
            Save();
        }

        public void LoadDirectory(string directory)
        {
            AlfaCodec.ValidateDirectory(directory);
            Cars = AlfaCodec.ReadRows(Path.Combine(directory, "cars.dat"));
            Fuel = AlfaCodec.ReadRows(Path.Combine(directory, "fuel.dat"));
            Repairs = AlfaCodec.ReadRows(Path.Combine(directory, "repairs.dat"));
            Utilities = AlfaCodec.ReadRows(Path.Combine(directory, "utilities.dat"));
            string categoriesPath = Path.Combine(directory, AlfaCodec.RepairCategoriesFile);
            Categories = File.Exists(categoriesPath)
                ? AlfaCodec.ReadRows(categoriesPath)
                : new List<string[]>();
            string expensesPath = Path.Combine(directory, AlfaCodec.ExpensesFile);
            Expenses = File.Exists(expensesPath)
                ? AlfaCodec.ReadRows(expensesPath)
                : new List<string[]>();
            string expenseCategoriesPath = Path.Combine(directory, AlfaCodec.ExpenseCategoriesFile);
            ExpenseCategories = File.Exists(expenseCategoriesPath)
                ? AlfaCodec.ReadRows(expenseCategoriesPath)
                : new List<string[]>();
            string settingsPath = Path.Combine(directory, AlfaCodec.SettingsFile);
            Settings = File.Exists(settingsPath)
                ? AlfaCodec.ReadRows(settingsPath)
                : ToSettings.ToRows(new ToIntervals());
            string carsMetaPath = Path.Combine(directory, AlfaCodec.CarsMetaFile);
            CarsMetaRows = File.Exists(carsMetaPath)
                ? AlfaCodec.ReadRows(carsMetaPath)
                : new List<string[]>();
            string templatesPath = Path.Combine(directory, AlfaCodec.TemplatesFile);
            TemplatesRows = File.Exists(templatesPath)
                ? AlfaCodec.ReadRows(templatesPath)
                : new List<string[]>();
            string remindersPath = Path.Combine(directory, AlfaCodec.RemindersFile);
            RemindersRows = File.Exists(remindersPath)
                ? AlfaCodec.ReadRows(remindersPath)
                : new List<string[]>();
            string notesPath = Path.Combine(directory, AlfaCodec.NotesFile);
            NotesRows = File.Exists(notesPath)
                ? AlfaCodec.ReadRows(notesPath)
                : new List<string[]>();
            string tripsPath = Path.Combine(directory, AlfaCodec.TripsFile);
            TripsRows = File.Exists(tripsPath)
                ? AlfaCodec.ReadRows(tripsPath)
                : new List<string[]>();
            Deleted = RecordIds.ReadTombstones(directory);
            EnsureCategoriesFromRepairs();
            EnsureExpenseCategoriesFromExpenses();
            DirectoryPath = directory;
        }

        public void LoadZip(string zipPath, string extractDirectory)
        {
            if (Directory.Exists(extractDirectory))
                Directory.Delete(extractDirectory, true);
            Directory.CreateDirectory(extractDirectory);
            ZipFile.ExtractToDirectory(zipPath, extractDirectory);
            LoadDirectory(extractDirectory);
        }

        public void Save()
        {
            if (String.IsNullOrEmpty(DirectoryPath))
                throw new InvalidOperationException("Сначала откройте или создайте папку данных.");
            NormalizeBeforeSave();
            Directory.CreateDirectory(DirectoryPath);
            AlfaCodec.SaveRows(Path.Combine(DirectoryPath, "cars.dat"), Cars);
            AlfaCodec.SaveRows(Path.Combine(DirectoryPath, "fuel.dat"), Fuel);
            AlfaCodec.SaveRows(Path.Combine(DirectoryPath, "repairs.dat"), Repairs);
            AlfaCodec.SaveRows(Path.Combine(DirectoryPath, "utilities.dat"), Utilities);
            AlfaCodec.SaveRows(Path.Combine(DirectoryPath, AlfaCodec.RepairCategoriesFile), Categories);
            AlfaCodec.SaveRows(Path.Combine(DirectoryPath, AlfaCodec.ExpensesFile), Expenses);
            AlfaCodec.SaveRows(Path.Combine(DirectoryPath, AlfaCodec.ExpenseCategoriesFile), ExpenseCategories);
            AlfaCodec.SaveRows(Path.Combine(DirectoryPath, AlfaCodec.SettingsFile), Settings);
            AlfaCodec.SaveRows(Path.Combine(DirectoryPath, AlfaCodec.CarsMetaFile),
                CarsMetaRows ?? new List<string[]>());
            AlfaCodec.SaveRows(Path.Combine(DirectoryPath, AlfaCodec.TemplatesFile),
                TemplatesRows ?? new List<string[]>());
            AlfaCodec.SaveRows(Path.Combine(DirectoryPath, AlfaCodec.RemindersFile),
                RemindersRows ?? new List<string[]>());
            AlfaCodec.SaveRows(Path.Combine(DirectoryPath, AlfaCodec.NotesFile),
                NotesRows ?? new List<string[]>());
            AlfaCodec.SaveRows(Path.Combine(DirectoryPath, AlfaCodec.TripsFile),
                TripsRows ?? new List<string[]>());
            RecordIds.WriteTombstones(DirectoryPath, Deleted ?? new List<Tombstone>());
            AlfaCodec.ValidateDirectory(DirectoryPath);
        }

        private string BuildSnapshotJson()
        {
            string token = ToSettings.GetOrCreateSnapshotDeviceToken(DirectoryPath);
            string created = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture);
            return "{\"schema\":\"alfa156.snapshot\",\"schemaVersion\":1,\"appVersion\":" +
                QuoteSnapshotJson(AppVersion) + ",\"createdUtc\":" + QuoteSnapshotJson(created) +
                ",\"deviceToken\":" + QuoteSnapshotJson(token) + "}";
        }

        private static string QuoteSnapshotJson(string value)
        {
            if (value == null)
                value = "";
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
        }

        /// <summary>
        /// Allocates Safety_yyyyMMdd_HHmmss next to the data folder (does not create it).
        /// </summary>
        public string AllocateSafetyBackupPath()
        {
            string parent = String.IsNullOrEmpty(DirectoryPath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Alfa156")
                : Path.GetDirectoryName(DirectoryPath);
            if (String.IsNullOrEmpty(parent))
                parent = DirectoryPath;
            string baseName = "Safety_" +
                DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string path = Path.Combine(parent, baseName);
            int suffix = 1;
            while (Directory.Exists(path))
            {
                path = Path.Combine(parent, baseName + "_" +
                    suffix.ToString(CultureInfo.InvariantCulture));
                suffix++;
            }
            return path;
        }

        /// <summary>
        /// Copies current .dat (+ Receipts) into Safety_yyyyMMdd_HHmmss. Returns path.
        /// </summary>
        public string CreateSafetyBackup(string allocatedPath)
        {
            if (String.IsNullOrEmpty(DirectoryPath))
                throw new InvalidOperationException("Нет открытой папки данных для копии.");
            if (!File.Exists(Path.Combine(DirectoryPath, "cars.dat")))
                return null;

            string path = allocatedPath;
            if (String.IsNullOrEmpty(path))
                path = AllocateSafetyBackupPath();
            Directory.CreateDirectory(path);
            for (int i = 0; i < AlfaCodec.DataFiles.Length; i++)
            {
                string name = AlfaCodec.DataFiles[i];
                string source = Path.Combine(DirectoryPath, name);
                if (File.Exists(source))
                    File.Copy(source, Path.Combine(path, name), true);
            }
            string receipts = Path.Combine(DirectoryPath, "Receipts");
            if (Directory.Exists(receipts))
            {
                string destReceipts = Path.Combine(path, "Receipts");
                Directory.CreateDirectory(destReceipts);
                string[] files = Directory.GetFiles(receipts);
                for (int i = 0; i < files.Length; i++)
                    File.Copy(files[i], Path.Combine(destReceipts, Path.GetFileName(files[i])), true);
            }
            NoteSuccessfulBackup();
            return path;
        }

        public void SaveZip(string zipPath)
        {
            Save();
            if (File.Exists(zipPath))
                File.Delete(zipPath);
            using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                for (int i = 0; i < AlfaCodec.DataFiles.Length; i++)
                {
                    string fileName = AlfaCodec.DataFiles[i];
                    string path = Path.Combine(DirectoryPath, fileName);
                    if (File.Exists(path))
                        archive.CreateEntryFromFile(path, fileName, CompressionLevel.Optimal);
                }
                ReceiptStore.AddFolderToZip(archive, DirectoryPath);
                string snapshot = BuildSnapshotJson();
                ZipArchiveEntry entry = archive.CreateEntry("snapshot.json", CompressionLevel.Optimal);
                using (StreamWriter writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
                    writer.Write(snapshot);
            }
            NoteSuccessfulBackup();
        }

        /// <summary>Updates last_backup_utc after local backup / ZIP / safety copy.</summary>
        public void NoteSuccessfulBackup()
        {
            if (String.IsNullOrEmpty(DirectoryPath))
                return;
            try
            {
                Settings = ToSettings.MarkBackupDone(Settings);
                AlfaCodec.SaveRows(Path.Combine(DirectoryPath, AlfaCodec.SettingsFile), Settings);
            }
            catch
            {
                // reminder stamp must not fail export
            }
        }

        public void ReplaceAll(
            List<string[]> cars,
            List<string[]> fuel,
            List<string[]> repairs,
            List<string[]> utilities,
            List<string[]> categories)
        {
            ReplaceAll(cars, fuel, repairs, utilities, categories, Expenses, ExpenseCategories, Settings);
        }

        public void ReplaceAll(
            List<string[]> cars,
            List<string[]> fuel,
            List<string[]> repairs,
            List<string[]> utilities,
            List<string[]> categories,
            List<string[]> expenses,
            List<string[]> expenseCategories,
            List<string[]> settings)
        {
            Cars = cars ?? new List<string[]>();
            Fuel = fuel ?? new List<string[]>();
            Repairs = repairs ?? new List<string[]>();
            Utilities = utilities ?? new List<string[]>();
            Categories = categories ?? new List<string[]>();
            Expenses = expenses ?? new List<string[]>();
            ExpenseCategories = expenseCategories ?? new List<string[]>();
            Settings = settings ?? ToSettings.ToRows(new ToIntervals());
            EnsureCategoriesFromRepairs();
            EnsureExpenseCategoriesFromExpenses();
        }

        public void NoteDeleted(string fileName, string id)
        {
            if (Deleted == null)
                Deleted = new List<Tombstone>();
            RecordIds.AddTombstone(Deleted, fileName, id);
        }

        private void EnsureCategoriesFromRepairs()
        {
            Dictionary<string, string> known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Categories.Count; i++)
            {
                if (Categories[i].Length == 0)
                    continue;
                string name = Categories[i][0].Trim();
                if (name.Length == 0)
                    continue;
                if (!known.ContainsKey(name))
                    known[name] = name;
            }

            for (int i = 0; i < Repairs.Count; i++)
            {
                string[] content = RecordIds.StripId(Repairs[i]);
                if (content.Length < 4)
                    continue;
                string category = (content[3] ?? "").Trim();
                if (category.Length == 0 || known.ContainsKey(category))
                    continue;
                known[category] = category;
                Categories.Add(new string[] { category });
            }

            if (Categories.Count == 0)
            {
                for (int i = 0; i < AlfaCodec.DefaultRepairCategories.Length; i++)
                    Categories.Add(new string[] { AlfaCodec.DefaultRepairCategories[i] });
            }
        }

        private void EnsureExpenseCategoriesFromExpenses()
        {
            Dictionary<string, string> known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ExpenseCategories.Count; i++)
            {
                if (ExpenseCategories[i].Length == 0)
                    continue;
                string name = ExpenseCategories[i][0].Trim();
                if (name.Length == 0)
                    continue;
                if (!known.ContainsKey(name))
                    known[name] = name;
            }

            for (int i = 0; i < Expenses.Count; i++)
            {
                string[] content = RecordIds.StripId(Expenses[i]);
                if (content.Length < 3)
                    continue;
                string category = (content[2] ?? "").Trim();
                if (category.Length == 0 || known.ContainsKey(category))
                    continue;
                known[category] = category;
                ExpenseCategories.Add(new string[] { category });
            }

            if (ExpenseCategories.Count == 0)
            {
                for (int i = 0; i < AlfaCodec.DefaultExpenseCategories.Length; i++)
                    ExpenseCategories.Add(new string[] { AlfaCodec.DefaultExpenseCategories[i] });
            }
        }

        private void NormalizeBeforeSave()
        {
            for (int i = 0; i < Cars.Count; i++)
            {
                string id = RecordIds.GetId(Cars[i]);
                string[] content = RecordIds.StripId(Cars[i]);
                content = Resize(content, 1);
                content[0] = (content[0] ?? "").Trim();
                Cars[i] = id != null ? RecordIds.WithId(content, id) : content;
            }
            Cars.RemoveAll(delegate(string[] row)
            {
                string[] content = RecordIds.StripId(row);
                return content.Length == 0 || String.IsNullOrEmpty(content[0]);
            });
            if (Cars.Count == 0)
                throw new InvalidDataException("Нужен хотя бы один автомобиль.");
            RecordIds.EnsureIds(Cars, "cars.dat");

            for (int i = 0; i < Fuel.Count; i++)
                Fuel[i] = NormalizeFuelRow(Fuel[i]);
            RecordIds.EnsureIds(Fuel, "fuel.dat");

            for (int i = 0; i < Repairs.Count; i++)
            {
                string id = RecordIds.GetId(Repairs[i]);
                string[] content = Resize(RecordIds.StripId(Repairs[i]), 9);
                content[0] = AlfaCodec.NormalizeDate(content[0]);
                content[1] = (content[1] ?? "").Trim();
                if (!String.IsNullOrEmpty((content[2] ?? "").Trim()))
                    content[2] = AlfaCodec.FormatNumber(content[2]);
                else
                    content[2] = "";
                content[3] = (content[3] ?? "").Trim();
                content[4] = (content[4] ?? "").Trim();
                content[5] = content[5] ?? "";
                if (!String.IsNullOrEmpty((content[6] ?? "").Trim()))
                    content[6] = AlfaCodec.FormatNumber(content[6]);
                else
                    content[6] = "1";
                content[7] = AlfaCodec.FormatNumber(content[7]);
                content[8] = content[8] ?? "";
                Repairs[i] = id != null ? RecordIds.WithId(content, id) : content;
            }
            RecordIds.EnsureIds(Repairs, "repairs.dat");

            for (int i = 0; i < Utilities.Count; i++)
            {
                string id = RecordIds.GetId(Utilities[i]);
                string[] content = Resize(RecordIds.StripId(Utilities[i]), 8);
                content[0] = AlfaCodec.NormalizeMonth(content[0]);
                for (int c = 1; c < 8; c++)
                {
                    string amount = (content[c] ?? "").Trim();
                    content[c] = amount.Length == 0 ? "" : AlfaCodec.FormatNumber(amount);
                }
                Utilities[i] = id != null ? RecordIds.WithId(content, id) : content;
            }
            RecordIds.EnsureIds(Utilities, "utilities.dat");

            for (int i = 0; i < Categories.Count; i++)
            {
                string id = RecordIds.GetId(Categories[i]);
                string[] content = Resize(RecordIds.StripId(Categories[i]), 1);
                content[0] = (content[0] ?? "").Trim();
                Categories[i] = id != null ? RecordIds.WithId(content, id) : content;
            }
            Categories.RemoveAll(delegate(string[] row)
            {
                string[] content = RecordIds.StripId(row);
                return content.Length == 0 || String.IsNullOrEmpty(content[0]);
            });
            EnsureCategoriesFromRepairs();
            RecordIds.EnsureIds(Categories, AlfaCodec.RepairCategoriesFile);

            for (int i = 0; i < Expenses.Count; i++)
            {
                string id = RecordIds.GetId(Expenses[i]);
                string[] content = Resize(RecordIds.StripId(Expenses[i]), 6);
                content[0] = AlfaCodec.NormalizeDate(content[0]);
                content[1] = (content[1] ?? "").Trim();
                content[2] = (content[2] ?? "").Trim();
                content[3] = (content[3] ?? "").Trim();
                content[4] = AlfaCodec.FormatNumber(content[4]);
                content[5] = content[5] ?? "";
                Expenses[i] = id != null ? RecordIds.WithId(content, id) : content;
            }
            RecordIds.EnsureIds(Expenses, AlfaCodec.ExpensesFile);

            for (int i = 0; i < ExpenseCategories.Count; i++)
            {
                string id = RecordIds.GetId(ExpenseCategories[i]);
                string[] content = Resize(RecordIds.StripId(ExpenseCategories[i]), 1);
                content[0] = (content[0] ?? "").Trim();
                ExpenseCategories[i] = id != null ? RecordIds.WithId(content, id) : content;
            }
            ExpenseCategories.RemoveAll(delegate(string[] row)
            {
                string[] content = RecordIds.StripId(row);
                return content.Length == 0 || String.IsNullOrEmpty(content[0]);
            });
            EnsureExpenseCategoriesFromExpenses();
            RecordIds.EnsureIds(ExpenseCategories, AlfaCodec.ExpenseCategoriesFile);

            if (Settings == null || Settings.Count == 0)
                Settings = ToSettings.ToRows(new ToIntervals());
            else
                Settings = ToSettings.Normalize(Settings);

            if (CarsMetaRows == null)
                CarsMetaRows = new List<string[]>();
            Dictionary<string, string[]> metaByCar =
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < CarsMetaRows.Count; i++)
            {
                CarPassport meta = CarsMeta.FromRow(CarsMetaRows[i]);
                if (meta.CarName.Length == 0)
                    continue;
                metaByCar[meta.CarName] = CarsMeta.ToRow(meta);
            }
            List<string[]> syncedMeta = new List<string[]>();
            for (int i = 0; i < Cars.Count; i++)
            {
                string[] carContent = RecordIds.StripId(Cars[i]);
                string name = carContent.Length > 0 ? (carContent[0] ?? "").Trim() : "";
                if (name.Length == 0)
                    continue;
                string[] row;
                if (metaByCar.TryGetValue(name, out row))
                    syncedMeta.Add(row);
            }
            CarsMetaRows = syncedMeta;
            RecordIds.EnsureIds(CarsMetaRows, AlfaCodec.CarsMetaFile);

            if (TemplatesRows == null)
                TemplatesRows = new List<string[]>();
            List<string[]> validTemplates = new List<string[]>();
            for (int i = 0; i < TemplatesRows.Count; i++)
            {
                if (!RecordTemplates.IsValidRow(TemplatesRows[i]))
                    continue;
                validTemplates.Add(RecordTemplates.ToRow(RecordTemplates.FromRow(TemplatesRows[i])));
            }
            TemplatesRows = validTemplates;
            RecordIds.EnsureIds(TemplatesRows, AlfaCodec.TemplatesFile);

            if (RemindersRows == null)
                RemindersRows = new List<string[]>();
            List<string[]> validReminders = new List<string[]>();
            for (int i = 0; i < RemindersRows.Count; i++)
            {
                if (!DateReminders.IsValidRow(RemindersRows[i]))
                    continue;
                validReminders.Add(DateReminders.ToRow(DateReminders.FromRow(RemindersRows[i])));
            }
            RemindersRows = validReminders;
            RecordIds.EnsureIds(RemindersRows, AlfaCodec.RemindersFile);

            if (NotesRows == null)
                NotesRows = new List<string[]>();
            List<string[]> validNotes = new List<string[]>();
            for (int i = 0; i < NotesRows.Count; i++)
            {
                if (!CarNotes.IsValidRow(NotesRows[i]))
                    continue;
                validNotes.Add(CarNotes.ToRow(CarNotes.FromRow(NotesRows[i])));
            }
            NotesRows = validNotes;
            RecordIds.EnsureIds(NotesRows, AlfaCodec.NotesFile);

            if (TripsRows == null)
                TripsRows = new List<string[]>();
            RecordIds.EnsureIds(TripsRows, AlfaCodec.TripsFile);

            if (Deleted == null)
                Deleted = new List<Tombstone>();
            Deleted = RecordIds.CapTombstones(Deleted);
        }

        private static string[] NormalizeFuelRow(string[] source)
        {
            string id = RecordIds.GetId(source);
            string[] content = RecordIds.StripId(source ?? new string[0]);
            int length = content.Length;
            if (length < 5)
                length = 5;
            // date|car|km|liters|cost|price|comment|fullTank|purpose|station
            if (length > 10)
                length = 10;
            string[] row = Resize(content, length);
            row[0] = AlfaCodec.NormalizeDate(row[0]);
            row[1] = (row[1] ?? "").Trim();
            row[2] = AlfaCodec.FormatNumber(row[2]);
            row[3] = AlfaCodec.FormatNumber(row[3]);
            row[4] = AlfaCodec.FormatNumber(row[4]);
            if (row.Length > 5)
            {
                string price = (row[5] ?? "").Trim();
                if (price.Length == 0)
                {
                    decimal liters;
                    decimal cost;
                    if (AlfaCodec.TryNumber(row[3], out liters) && liters > 0 &&
                        AlfaCodec.TryNumber(row[4], out cost) && cost > 0)
                        row[5] = AlfaCodec.FormatNumber((cost / liters).ToString(
                            System.Globalization.CultureInfo.InvariantCulture));
                    else
                        row[5] = "";
                }
                else
                    row[5] = AlfaCodec.FormatNumber(price);
            }
            if (row.Length > 6)
                row[6] = (row[6] ?? "").Trim();
            if (row.Length > 7)
                row[7] = AlfaCodec.NormalizeFullTankFlag(row[7]);
            if (row.Length > 8)
                row[8] = AlfaCodec.NormalizePurpose(row[8]);
            if (row.Length > 9)
                row[9] = AlfaCodec.NormalizeStation(row[9]);

            while (row.Length > 5)
            {
                string last = row[row.Length - 1] ?? "";
                if (last.Trim().Length > 0)
                    break;
                row = Resize(row, row.Length - 1);
            }
            return id != null ? RecordIds.WithId(row, id) : row;
        }

        private static string[] Resize(string[] source, int length)
        {
            string[] result = new string[length];
            for (int i = 0; i < length; i++)
                result[i] = i < source.Length ? (source[i] ?? "") : "";
            return result;
        }
    }
}
