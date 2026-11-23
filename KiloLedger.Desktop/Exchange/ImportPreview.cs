using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using KiloLedger.Desktop.Data;

using KiloLedger.Localization;
namespace KiloLedger.Desktop.Exchange
{
    /// <summary>
    /// Counts and car names for the Import wizard preview (no mutation of the live repo).
    /// </summary>
    internal sealed class ImportPreview
    {
        internal string SourceKindLabel;
        internal string SourcePath;
        internal List<string> CarNames = new List<string>();
        internal int FuelCount;
        internal int RepairCount;
        internal int ExpenseCount;
        internal int UtilityCount;
        internal int RepairCategoryCount;
        internal int ExpenseCategoryCount;
        internal int TemplateCount;
        internal int ReminderCount;
        internal int NoteCount;
        internal int ReceiptCount;

        internal static ImportPreview FromRepository(KiloLedgerRepository repo, string kindLabel, string path)
        {
            ImportPreview preview = new ImportPreview();
            preview.SourceKindLabel = kindLabel;
            preview.SourcePath = path ?? "";
            preview.CarNames = CarNamesFrom(repo.Cars);
            preview.FuelCount = repo.Fuel != null ? repo.Fuel.Count : 0;
            preview.RepairCount = repo.Repairs != null ? repo.Repairs.Count : 0;
            preview.ExpenseCount = repo.Expenses != null ? repo.Expenses.Count : 0;
            preview.UtilityCount = repo.Utilities != null ? repo.Utilities.Count : 0;
            preview.RepairCategoryCount = repo.Categories != null ? repo.Categories.Count : 0;
            preview.ExpenseCategoryCount = repo.ExpenseCategories != null ? repo.ExpenseCategories.Count : 0;
            preview.TemplateCount = repo.TemplatesRows != null ? repo.TemplatesRows.Count : 0;
            preview.ReminderCount = repo.RemindersRows != null ? repo.RemindersRows.Count : 0;
            preview.NoteCount = repo.NotesRows != null ? repo.NotesRows.Count : 0;
            return preview;
        }

        internal static ImportPreview FromDatFolder(string folder)
        {
            KiloLedgerRepository temp = new KiloLedgerRepository();
            temp.LoadDirectory(folder);
            ImportPreview preview = FromRepository(temp, L10n.Get("import.dat_folder_short"), folder);
            string receipts = Path.Combine(folder, "Receipts");
            if (Directory.Exists(receipts))
                preview.ReceiptCount = Directory.GetFiles(receipts).Length;
            return preview;
        }

        internal static ImportPreview FromZip(string zipPath, string extractDirectory)
        {
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                ValidateZipEntries(archive);

            if (Directory.Exists(extractDirectory))
                Directory.Delete(extractDirectory, true);
            Directory.CreateDirectory(extractDirectory);
            ZipFile.ExtractToDirectory(zipPath, extractDirectory);
            // ZIP may contain a single nested folder.
            string dataRoot = ResolveDataRoot(extractDirectory);
            KiloLedgerRepository temp = new KiloLedgerRepository();
            temp.LoadDirectory(dataRoot);
            ImportPreview preview = FromRepository(temp, "ZIP", zipPath);
            string receipts = Path.Combine(dataRoot, "Receipts");
            if (Directory.Exists(receipts))
                preview.ReceiptCount = Directory.GetFiles(receipts).Length;
            preview.SourcePath = zipPath;
            return preview;
        }

        private static void ValidateZipEntries(ZipArchive archive)
        {
            bool? nested = null;
            string nestedRoot = null;
            HashSet<string> files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < archive.Entries.Count; i++)
            {
                string entryName = archive.Entries[i].FullName;
                if (String.Equals(entryName, "snapshot.json", StringComparison.OrdinalIgnoreCase) ||
                    entryName.EndsWith("/snapshot.json", StringComparison.OrdinalIgnoreCase))
                {
                    using (StreamReader reader = new StreamReader(archive.Entries[i].Open(), Encoding.UTF8))
                        ValidateSnapshotJson(reader.ReadToEnd());
                    continue;
                }
                bool isDirectory = entryName.EndsWith("/", StringComparison.Ordinal);
                if (isDirectory)
                    entryName = entryName.Substring(0, entryName.Length - 1);
                if (String.IsNullOrEmpty(entryName) || entryName.IndexOf('\\') >= 0 ||
                    entryName.StartsWith("/", StringComparison.Ordinal))
                    throw new InvalidDataException((L10n.Get("error.invalid_zip_path") + " ") + archive.Entries[i].FullName);

                string[] parts = entryName.Split('/');
                for (int p = 0; p < parts.Length; p++)
                    if (parts[p].Length == 0 || parts[p] == "." || parts[p] == ".." ||
                        parts[p].IndexOf(':') >= 0)
                        throw new InvalidDataException((L10n.Get("error.invalid_zip_path") + " ") + archive.Entries[i].FullName);

                bool hasNestedRoot = !IsDataFile(parts[0]) &&
                    !String.Equals(parts[0], ReceiptStore.FolderName, StringComparison.Ordinal);
                if (nested.HasValue && nested.Value != hasNestedRoot)
                    throw new InvalidDataException(L10n.Get("error.zip_structure"));
                nested = hasNestedRoot;

                string[] relative;
                if (hasNestedRoot)
                {
                    if (nestedRoot == null)
                        nestedRoot = parts[0];
                    else if (!String.Equals(nestedRoot, parts[0], StringComparison.Ordinal))
                        throw new InvalidDataException(L10n.Get("error.zip_multiple_roots"));
                    relative = new string[parts.Length - 1];
                    Array.Copy(parts, 1, relative, 0, relative.Length);
                }
                else
                    relative = parts;

                if (relative.Length == 0)
                {
                    if (!isDirectory)
                        throw new InvalidDataException((L10n.Get("error.invalid_zip_file") + " ") + archive.Entries[i].FullName);
                    continue;
                }

                if (isDirectory)
                {
                    if (relative.Length != 1 ||
                        !String.Equals(relative[0], ReceiptStore.FolderName, StringComparison.Ordinal))
                        throw new InvalidDataException((L10n.Get("error.invalid_zip_folder") + " ") + archive.Entries[i].FullName);
                    continue;
                }

                bool isDataFile = relative.Length == 1 && IsDataFile(relative[0]);
                bool isReceipt = relative.Length == 2 &&
                    String.Equals(relative[0], ReceiptStore.FolderName, StringComparison.Ordinal) &&
                    ReceiptStore.IsSafeReceiptFileName(relative[1]);
                if (!isDataFile && !isReceipt)
                    throw new InvalidDataException((L10n.Get("error.invalid_zip_file") + " ") + archive.Entries[i].FullName);

                string normalizedName = String.Join("/", relative);
                if (!files.Add(normalizedName))
                    throw new InvalidDataException((L10n.Get("error.duplicate_file_in_zip") + " ") + normalizedName);
            }
        }

        internal static void ValidateSnapshotJson(string json)
        {
            if (String.IsNullOrEmpty(json))
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

        private static bool IsDataFile(string name)
        {
            for (int i = 0; i < KiloLedgerCodec.DataFiles.Length; i++)
                if (String.Equals(name, KiloLedgerCodec.DataFiles[i], StringComparison.Ordinal))
                    return true;
            return false;
        }

        internal static ImportPreview FromCsvFolder(string folder)
        {
            KiloLedgerRepository temp = new KiloLedgerRepository();
            CsvExchange.ImportFolder(temp, folder);
            return FromRepository(temp, L10n.Get("import.csv_folder_label"), folder);
        }

        internal static ImportPreview FromExcel(string xlsxPath)
        {
            KiloLedgerRepository temp = new KiloLedgerRepository();
            XlsxExchange.Import(temp, xlsxPath);
            return FromRepository(temp, "Excel", xlsxPath);
        }

        /// <summary>
        /// Finds the directory that contains cars.dat (root or one nested level).
        /// </summary>
        internal static string ResolveDataRoot(string extractOrFolder)
        {
            if (File.Exists(Path.Combine(extractOrFolder, "cars.dat")))
                return extractOrFolder;
            string[] dirs = Directory.GetDirectories(extractOrFolder);
            for (int i = 0; i < dirs.Length; i++)
            {
                if (File.Exists(Path.Combine(dirs[i], "cars.dat")))
                    return dirs[i];
            }
            return extractOrFolder;
        }

        internal string FormatSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append((L10n.Get("label.source_colon") + " ")).Append(SourceKindLabel ?? "");
            if (!String.IsNullOrEmpty(SourcePath))
                sb.Append("\r\n").Append(SourcePath);
            sb.Append(("\r\n\r\n" + L10n.Get("import.preview_cars_open"))).Append(CarNames.Count).Append("): ");
            if (CarNames.Count == 0)
                sb.Append("—");
            else
            {
                for (int i = 0; i < CarNames.Count; i++)
                {
                    if (i > 0)
                        sb.Append(", ");
                    sb.Append(CarNames[i]);
                }
            }
            sb.Append(("\r\n\r\n" + L10n.Get("summary.fuel_section_colon") + " ")).Append(FuelCount);
            sb.Append(("\r\n" + L10n.Get("summary.repairs_section_colon") + " ")).Append(RepairCount);
            sb.Append(("\r\n" + L10n.Get("summary.expenses_section_colon") + " ")).Append(ExpenseCount);
            sb.Append(("\r\n" + L10n.Get("label.utilities_colon") + " ")).Append(UtilityCount);
            sb.Append(("\r\n" + L10n.Get("label.repair_cat_colon") + " ")).Append(RepairCategoryCount);
            sb.Append(("\r\n" + L10n.Get("label.expense_cat_colon") + " ")).Append(ExpenseCategoryCount);
            if (TemplateCount > 0)
                sb.Append(("\r\n" + L10n.Get("label.templates_colon") + " ")).Append(TemplateCount);
            if (ReminderCount > 0)
                sb.Append(("\r\n" + L10n.Get("summary.reminders_colon") + " ")).Append(ReminderCount);
            if (NoteCount > 0)
                sb.Append(("\r\n" + L10n.Get("summary.notes_colon") + " ")).Append(NoteCount);
            if (ReceiptCount > 0)
                sb.Append(("\r\n" + L10n.Get("label.receipt_photos_colon") + " ")).Append(ReceiptCount);
            return sb.ToString();
        }

        private static List<string> CarNamesFrom(List<string[]> cars)
        {
            List<string> names = new List<string>();
            if (cars == null)
                return names;
            for (int i = 0; i < cars.Count; i++)
            {
                if (cars[i] != null && cars[i].Length > 0 && !String.IsNullOrEmpty(cars[i][0]))
                    names.Add(cars[i][0]);
            }
            return names;
        }
    }
}
