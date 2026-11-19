using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Alfa156.Desktop.Data
{
    internal sealed class AlfaRepository
    {
        public string DirectoryPath { get; private set; }
        public List<string[]> Cars { get; private set; }
        public List<string[]> Fuel { get; private set; }
        public List<string[]> Repairs { get; private set; }
        public List<string[]> Utilities { get; private set; }
        public List<string[]> Categories { get; private set; }

        public AlfaRepository()
        {
            Cars = new List<string[]>();
            Fuel = new List<string[]>();
            Repairs = new List<string[]>();
            Utilities = new List<string[]>();
            Categories = new List<string[]>();
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
            EnsureCategoriesFromRepairs();
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
            AlfaCodec.ValidateDirectory(DirectoryPath);
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
            }
        }

        public void ReplaceAll(
            List<string[]> cars,
            List<string[]> fuel,
            List<string[]> repairs,
            List<string[]> utilities,
            List<string[]> categories)
        {
            Cars = cars ?? new List<string[]>();
            Fuel = fuel ?? new List<string[]>();
            Repairs = repairs ?? new List<string[]>();
            Utilities = utilities ?? new List<string[]>();
            Categories = categories ?? new List<string[]>();
            EnsureCategoriesFromRepairs();
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
                if (Repairs[i].Length < 4)
                    continue;
                string category = (Repairs[i][3] ?? "").Trim();
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

        private void NormalizeBeforeSave()
        {
            for (int i = 0; i < Cars.Count; i++)
            {
                if (Cars[i].Length != 1)
                    Cars[i] = Resize(Cars[i], 1);
                Cars[i][0] = (Cars[i][0] ?? "").Trim();
            }
            Cars.RemoveAll(delegate(string[] row) { return row.Length == 0 || String.IsNullOrEmpty(row[0]); });
            if (Cars.Count == 0)
                throw new InvalidDataException("Нужен хотя бы один автомобиль.");

            for (int i = 0; i < Fuel.Count; i++)
            {
                Fuel[i] = NormalizeFuelRow(Fuel[i]);
            }

            for (int i = 0; i < Repairs.Count; i++)
            {
                Repairs[i] = Resize(Repairs[i], 9);
                Repairs[i][0] = AlfaCodec.NormalizeDate(Repairs[i][0]);
                Repairs[i][1] = (Repairs[i][1] ?? "").Trim();
                if (!String.IsNullOrEmpty((Repairs[i][2] ?? "").Trim()))
                    Repairs[i][2] = AlfaCodec.FormatNumber(Repairs[i][2]);
                else
                    Repairs[i][2] = "";
                Repairs[i][3] = (Repairs[i][3] ?? "").Trim();
                Repairs[i][4] = (Repairs[i][4] ?? "").Trim();
                Repairs[i][5] = Repairs[i][5] ?? "";
                if (!String.IsNullOrEmpty((Repairs[i][6] ?? "").Trim()))
                    Repairs[i][6] = AlfaCodec.FormatNumber(Repairs[i][6]);
                else
                    Repairs[i][6] = "1";
                Repairs[i][7] = AlfaCodec.FormatNumber(Repairs[i][7]);
                Repairs[i][8] = Repairs[i][8] ?? "";
            }

            for (int i = 0; i < Utilities.Count; i++)
            {
                Utilities[i] = Resize(Utilities[i], 8);
                Utilities[i][0] = AlfaCodec.NormalizeMonth(Utilities[i][0]);
                for (int c = 1; c < 8; c++)
                {
                    string amount = (Utilities[i][c] ?? "").Trim();
                    Utilities[i][c] = amount.Length == 0 ? "" : AlfaCodec.FormatNumber(amount);
                }
            }

            for (int i = 0; i < Categories.Count; i++)
            {
                Categories[i] = Resize(Categories[i], 1);
                Categories[i][0] = (Categories[i][0] ?? "").Trim();
            }
            Categories.RemoveAll(delegate(string[] row) { return row.Length == 0 || String.IsNullOrEmpty(row[0]); });
            EnsureCategoriesFromRepairs();
        }

        private static string[] NormalizeFuelRow(string[] source)
        {
            int length = source == null ? 5 : source.Length;
            if (length < 5)
                length = 5;
            if (length > 7)
                length = 7;
            string[] row = Resize(source ?? new string[0], length);
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

            while (row.Length > 5)
            {
                string last = row[row.Length - 1] ?? "";
                if (last.Trim().Length > 0)
                    break;
                row = Resize(row, row.Length - 1);
            }
            return row;
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
