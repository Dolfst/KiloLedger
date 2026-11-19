using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Alfa156.Desktop.Data;

namespace Alfa156.Desktop.Exchange
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

        internal static ImportPreview FromRepository(AlfaRepository repo, string kindLabel, string path)
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
            AlfaRepository temp = new AlfaRepository();
            temp.LoadDirectory(folder);
            ImportPreview preview = FromRepository(temp, "Папка .dat", folder);
            string receipts = Path.Combine(folder, "Receipts");
            if (Directory.Exists(receipts))
                preview.ReceiptCount = Directory.GetFiles(receipts).Length;
            return preview;
        }

        internal static ImportPreview FromZip(string zipPath, string extractDirectory)
        {
            if (Directory.Exists(extractDirectory))
                Directory.Delete(extractDirectory, true);
            Directory.CreateDirectory(extractDirectory);
            ZipFile.ExtractToDirectory(zipPath, extractDirectory);
            // ZIP may contain a single nested folder.
            string dataRoot = ResolveDataRoot(extractDirectory);
            AlfaRepository temp = new AlfaRepository();
            temp.LoadDirectory(dataRoot);
            ImportPreview preview = FromRepository(temp, "ZIP", zipPath);
            string receipts = Path.Combine(dataRoot, "Receipts");
            if (Directory.Exists(receipts))
                preview.ReceiptCount = Directory.GetFiles(receipts).Length;
            preview.SourcePath = zipPath;
            return preview;
        }

        internal static ImportPreview FromCsvFolder(string folder)
        {
            AlfaRepository temp = new AlfaRepository();
            CsvExchange.ImportFolder(temp, folder);
            return FromRepository(temp, "CSV-папка", folder);
        }

        internal static ImportPreview FromExcel(string xlsxPath)
        {
            AlfaRepository temp = new AlfaRepository();
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
            sb.Append("Источник: ").Append(SourceKindLabel ?? "");
            if (!String.IsNullOrEmpty(SourcePath))
                sb.Append("\r\n").Append(SourcePath);
            sb.Append("\r\n\r\nАвтомобили (").Append(CarNames.Count).Append("): ");
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
            sb.Append("\r\n\r\nЗаправки: ").Append(FuelCount);
            sb.Append("\r\nРемонт и ТО: ").Append(RepairCount);
            sb.Append("\r\nПрочие расходы: ").Append(ExpenseCount);
            sb.Append("\r\nКоммунальные: ").Append(UtilityCount);
            sb.Append("\r\nКат. ремонта: ").Append(RepairCategoryCount);
            sb.Append("\r\nКат. прочих: ").Append(ExpenseCategoryCount);
            if (TemplateCount > 0)
                sb.Append("\r\nШаблоны: ").Append(TemplateCount);
            if (ReminderCount > 0)
                sb.Append("\r\nНапоминания: ").Append(ReminderCount);
            if (NoteCount > 0)
                sb.Append("\r\nЗаметки: ").Append(NoteCount);
            if (ReceiptCount > 0)
                sb.Append("\r\nФото чеков: ").Append(ReceiptCount);
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
