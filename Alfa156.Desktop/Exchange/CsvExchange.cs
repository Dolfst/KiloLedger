using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Alfa156.Desktop.Data;

using Alfa156.Localization;
namespace Alfa156.Desktop.Exchange
{
    internal static class CsvExchange
    {
        private static readonly Encoding Utf8Bom = new UTF8Encoding(true);

        internal static void ExportFolder(AlfaRepository repo, string folder)
        {
            Directory.CreateDirectory(folder);
            string money = "Сумма_" + ToSettings.GetCurrency(repo.Settings);
            WriteCsv(Path.Combine(folder, "cars.csv"), new string[] { "Автомобиль" }, repo.Cars);
            WriteCsv(Path.Combine(folder, "fuel.csv"),
                FuelHeaders(money),
                repo.Fuel);
            WriteCsv(Path.Combine(folder, "repairs.csv"),
                RepairHeaders(money), repo.Repairs);
            WriteCsv(Path.Combine(folder, "utilities.csv"), BuildUtilityHeaders(), repo.Utilities);
            WriteCsv(Path.Combine(folder, "repair-categories.csv"),
                new string[] { "Категория" }, repo.Categories);
            WriteCsv(Path.Combine(folder, "expenses.csv"),
                ExpenseHeaders(money),
                repo.Expenses);
            WriteCsv(Path.Combine(folder, "expense-categories.csv"),
                new string[] { "Категория" }, repo.ExpenseCategories);
        }

        /// <summary>
        /// Empty CSV templates (headers only) for fuel / repair / expenses — fill offline, then import.
        /// </summary>
        internal static void ExportTemplates(string folder)
        {
            Directory.CreateDirectory(folder);
            string money = "Сумма_BYN";
            WriteCsv(Path.Combine(folder, "fuel.csv"), FuelHeaders(money), new List<string[]>());
            WriteCsv(Path.Combine(folder, "repairs.csv"), RepairHeaders(money), new List<string[]>());
            WriteCsv(Path.Combine(folder, "expenses.csv"), ExpenseHeaders(money), new List<string[]>());
            WriteCsv(Path.Combine(folder, "cars.csv"), new string[] { "Автомобиль" }, new List<string[]>());
        }

        private static string[] FuelHeaders(string money)
        {
            return new string[]
            {
                "Дата", "Автомобиль", "Пробег_км", "Литры", money, "Цена_за_литр",
                "Комментарий", "Полный_бак", "Цель", "АЗС"
            };
        }

        private static string[] RepairHeaders(string money)
        {
            return new string[]
            {
                "Дата", "Автомобиль", "Пробег_км", "Категория", "Товар",
                "Описание", "Кол_во", money, "Комментарий"
            };
        }

        private static string[] ExpenseHeaders(string money)
        {
            return new string[]
            {
                "Дата", "Автомобиль", "Категория", "Название", money, "Комментарий"
            };
        }

        internal static void ImportFolder(AlfaRepository repo, string folder)
        {
            List<string[]> cars = ReadCsv(Path.Combine(folder, "cars.csv"), 1, true);
            List<string[]> fuel = ReadCsvFlexible(Path.Combine(folder, "fuel.csv"), 7, 10);
            List<string[]> repairs = ReadCsv(Path.Combine(folder, "repairs.csv"), 9, true);
            List<string[]> utilities = ReadCsv(Path.Combine(folder, "utilities.csv"), 8, true);
            string categoriesPath = Path.Combine(folder, "repair-categories.csv");
            List<string[]> categories = File.Exists(categoriesPath)
                ? ReadCsv(categoriesPath, 1, true)
                : new List<string[]>();
            string expensesPath = Path.Combine(folder, "expenses.csv");
            List<string[]> expenses = File.Exists(expensesPath)
                ? ReadCsv(expensesPath, 6, true)
                : new List<string[]>();
            string expenseCategoriesPath = Path.Combine(folder, "expense-categories.csv");
            List<string[]> expenseCategories = File.Exists(expenseCategoriesPath)
                ? ReadCsv(expenseCategoriesPath, 1, true)
                : new List<string[]>();
            repo.ReplaceAll(cars, fuel, repairs, utilities, categories, expenses, expenseCategories,
                repo.Settings);
        }

        internal static void ImportGoogleSheetWorkbook(AlfaRepository repo, string spreadsheetUrl)
        {
            string id = ExtractSpreadsheetId(spreadsheetUrl);
            if (id == null)
                throw new InvalidDataException(L10n.Get("error.invalid_google_sheet_url"));

            // Формат «Деньги»: Заправки Alfa Romeo 156 / Ремонт ... / Коммунальные / Справочник
            string fuelSheet;
            List<string[]> fuelRaw = DownloadFirstMatchingSheet(id, out fuelSheet,
                new string[] { "Заправки Alfa Romeo 156", "Заправки", "fuel" }, 8, false);
            string repairSheet;
            List<string[]> repairRaw = DownloadFirstMatchingSheet(id, out repairSheet,
                new string[] { "Ремонт Alfa Romeo 156", "Ремонт", "repairs" }, 10, false);
            string utilitiesSheet;
            List<string[]> utilitiesRaw = DownloadFirstMatchingSheet(id, out utilitiesSheet,
                new string[] { "Коммунальные", "Коммуналка", "utilities" }, 10, false);
            string categoriesSheet;
            List<string[]> categoriesRaw = DownloadFirstMatchingSheet(id, out categoriesSheet,
                new string[] { "Справочник", "Категории", "repair-categories" }, 3, false);

            bool dengi = fuelRaw.Count > 0 || repairRaw.Count > 0 || utilitiesRaw.Count > 0;
            if (dengi)
            {
                List<string> knownSheets = new List<string>();
                if (fuelSheet != null) knownSheets.Add(fuelSheet);
                if (repairSheet != null) knownSheets.Add(repairSheet);
                if (utilitiesSheet != null) knownSheets.Add(utilitiesSheet);
                if (categoriesSheet != null) knownSheets.Add(categoriesSheet);

                List<string[]> cars = DengiFormat.BuildCars(knownSheets);
                string car = cars[0][0];
                List<string[]> fuel = DengiFormat.MapFuel(fuelRaw, DengiFormat.CarFromSheetName(fuelSheet, car));
                List<string[]> repairs = DengiFormat.MapRepairs(repairRaw, DengiFormat.CarFromSheetName(repairSheet, car));
                List<string[]> utilities = DengiFormat.MapUtilities(utilitiesRaw);
                List<string[]> categories = DengiFormat.MapCategories(categoriesRaw);
                repo.ReplaceAll(cars, fuel, repairs, utilities, categories);
                return;
            }

            // Канонический формат Alfa156 Desktop
            List<string[]> canonicalCars = DownloadSheetOptional(id, new string[] { "Автомобили", "cars" }, 1, true);
            if (DengiFormat.LooksLikeMonthList(canonicalCars))
                throw new InvalidDataException(
                    "Похоже, импортировался лист с месяцами вместо автомобилей. Для таблицы «Деньги» нужны листы «Заправки …», «Ремонт …», «Коммунальные», «Справочник» и доступ по ссылке.");

            List<string[]> canonicalFuel = DownloadSheetOptional(id, new string[] { "Заправки", "fuel" }, 8, true);
            List<string[]> canonicalRepairs = DownloadSheetOptional(id, new string[] { "Ремонт", "repairs" }, 9, true);
            List<string[]> canonicalUtilities = DownloadSheetOptional(id, new string[] { "Коммуналка", "utilities" }, 8, true);
            List<string[]> canonicalCategories = DownloadSheetOptional(id, new string[] { "Категории", "repair-categories" }, 1, true);
            if (canonicalCars.Count == 0)
                throw new InvalidDataException(L10n.Get("error.google_no_data_found"));
            repo.ReplaceAll(canonicalCars, canonicalFuel, canonicalRepairs, canonicalUtilities, canonicalCategories);
        }

        internal static string ExtractSpreadsheetId(string url)
        {
            if (String.IsNullOrEmpty(url))
                return null;
            string text = url.Trim();
            Match match = Regex.Match(text, @"/spreadsheets/d/([a-zA-Z0-9-_]+)");
            if (match.Success)
                return match.Groups[1].Value;
            match = Regex.Match(text, @"^([a-zA-Z0-9-_]{20,})(?:/edit|/view|/?\?|$)");
            if (match.Success)
                return match.Groups[1].Value;
            match = Regex.Match(text, @"([a-zA-Z0-9-_]{30,})");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static List<string[]> DownloadFirstMatchingSheet(
            string id, out string matchedName, string[] names, int columns, bool hasHeader)
        {
            matchedName = null;
            Exception lastError = null;
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    string url = "https://docs.google.com/spreadsheets/d/" + id +
                        "/gviz/tq?tqx=out:csv&sheet=" + Uri.EscapeDataString(names[i]);
                    string csv = DownloadText(url);
                    if (LooksLikeHtmlError(csv) || LooksLikeGvizError(csv))
                        continue;
                    matchedName = names[i];
                    return ParseCsv(csv, columns, hasHeader);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }
            if (lastError != null && names.Length == 1)
                throw new InvalidDataException(L10n.Get("error.download_sheet_failed_open") + names[0] + "»: " + lastError.Message, lastError);
            return new List<string[]>();
        }

        private static List<string[]> DownloadSheetOptional(string id, string[] names, int columns, bool hasHeader)
        {
            string matched;
            return DownloadFirstMatchingSheet(id, out matched, names, columns, hasHeader);
        }

        private static string DownloadText(string url)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent = "Alfa156Desktop/1.0";
            request.AllowAutoRedirect = true;
            request.Timeout = 30000;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private static bool LooksLikeHtmlError(string text)
        {
            if (String.IsNullOrEmpty(text))
                return true;
            string sample = text.TrimStart().ToLowerInvariant();
            return sample.StartsWith("<!doctype") || sample.StartsWith("<html") || sample.Contains("sign in");
        }

        private static bool LooksLikeGvizError(string text)
        {
            string sample = (text ?? "").TrimStart();
            if (sample.StartsWith("/*", StringComparison.Ordinal) ||
                sample.StartsWith("google.visualization", StringComparison.OrdinalIgnoreCase))
                return sample.IndexOf("\"errors\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       sample.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0;
            return false;
        }

        private static string[] BuildUtilityHeaders()
        {
            string[] headers = new string[8];
            headers[0] = "Месяц";
            for (int i = 0; i < AlfaCodec.UtilityLabels.Length; i++)
                headers[i + 1] = AlfaCodec.UtilityLabels[i];
            return headers;
        }

        internal static void WriteCsv(string path, string[] headers, List<string[]> rows)
        {
            using (StreamWriter writer = new StreamWriter(path, false, Utf8Bom))
            {
                writer.WriteLine(JoinCsv(headers));
                for (int i = 0; i < rows.Count; i++)
                    writer.WriteLine(JoinCsv(Pad(RecordIds.StripId(rows[i]), headers.Length)));
            }
        }

        private static List<string[]> ReadCsvFlexible(string path, int minColumns, int maxColumns)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException((L10n.Get("error.file_not_found_colon") + " ") + path, path);
            string text = File.ReadAllText(path, Encoding.UTF8);
            List<string[]> raw = ParseCsvRecords(text);
            List<string[]> rows = new List<string[]>();
            int start = raw.Count > 0 ? 1 : 0;
            for (int i = start; i < raw.Count; i++)
            {
                if (raw[i].Length > maxColumns)
                    throw new InvalidDataException(
                        "Слишком много столбцов в CSV (строка " + (i + 1) + ").");
                int columns = Math.Max(minColumns, raw[i].Length);
                string[] row = Pad(raw[i], columns);
                bool empty = true;
                for (int c = 0; c < row.Length; c++)
                {
                    if (!String.IsNullOrEmpty((row[c] ?? "").Trim()))
                    {
                        empty = false;
                        break;
                    }
                }
                if (!empty)
                    rows.Add(row);
            }
            return rows;
        }

        internal static List<string[]> ReadCsv(string path, int columns, bool hasHeader)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException((L10n.Get("error.file_not_found_colon") + " ") + path, path);
            string text = File.ReadAllText(path, Encoding.UTF8);
            return ParseCsvCore(text, columns, hasHeader, true);
        }

        internal static List<string[]> ParseCsv(string text, int columns, bool hasHeader)
        {
            return ParseCsvCore(text, columns, hasHeader, false);
        }

        private static List<string[]> ParseCsvCore(
            string text, int columns, bool hasHeader, bool rejectExtraColumns)
        {
            List<string[]> raw = ParseCsvRecords(text);
            List<string[]> rows = new List<string[]>();
            int start = hasHeader && raw.Count > 0 ? 1 : 0;
            for (int i = start; i < raw.Count; i++)
            {
                if (rejectExtraColumns && raw[i].Length > columns)
                    throw new InvalidDataException(
                        "Слишком много столбцов CSV (строка " + (i + 1) + ").");
                string[] row = Pad(raw[i], columns);
                bool empty = true;
                for (int c = 0; c < row.Length; c++)
                {
                    if (!String.IsNullOrEmpty((row[c] ?? "").Trim()))
                    {
                        empty = false;
                        break;
                    }
                }
                if (!empty)
                    rows.Add(row);
            }
            return rows;
        }

        private static List<string[]> ParseCsvRecords(string text)
        {
            List<string[]> rows = new List<string[]>();
            List<string> fields = new List<string>();
            StringBuilder field = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                            inQuotes = false;
                    }
                    else
                        field.Append(c);
                }
                else
                {
                    if (c == '"')
                        inQuotes = true;
                    else if (c == ',')
                    {
                        fields.Add(field.ToString());
                        field.Length = 0;
                    }
                    else if (c == '\r')
                    {
                        // ignore
                    }
                    else if (c == '\n')
                    {
                        fields.Add(field.ToString());
                        field.Length = 0;
                        rows.Add(fields.ToArray());
                        fields = new List<string>();
                    }
                    else
                        field.Append(c);
                }
            }
            if (field.Length > 0 || fields.Count > 0)
            {
                fields.Add(field.ToString());
                rows.Add(fields.ToArray());
            }
            if (inQuotes)
                throw new InvalidDataException(L10n.Get("error.csv_unclosed_quote"));
            return rows;
        }

        private static string JoinCsv(string[] values)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(EscapeCsv(values[i] ?? ""));
            }
            return sb.ToString();
        }

        private static string EscapeCsv(string value)
        {
            if (value.IndexOfAny(new char[] { ',', '"', '\r', '\n' }) >= 0)
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        private static string[] Pad(string[] source, int length)
        {
            string[] result = new string[length];
            for (int i = 0; i < length; i++)
                result[i] = i < source.Length ? (source[i] ?? "") : "";
            return result;
        }
    }
}
