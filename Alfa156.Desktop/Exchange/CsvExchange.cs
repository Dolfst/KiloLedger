using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Alfa156.Desktop.Data;

namespace Alfa156.Desktop.Exchange
{
    internal static class CsvExchange
    {
        private static readonly Encoding Utf8Bom = new UTF8Encoding(true);

        internal static void ExportFolder(AlfaRepository repo, string folder)
        {
            Directory.CreateDirectory(folder);
            WriteCsv(Path.Combine(folder, "cars.csv"), new string[] { "Автомобиль" }, repo.Cars);
            WriteCsv(Path.Combine(folder, "fuel.csv"),
                new string[] { "Дата", "Автомобиль", "Пробег_км", "Литры", "Сумма_BYN" }, repo.Fuel);
            WriteCsv(Path.Combine(folder, "repairs.csv"),
                new string[]
                {
                    "Дата", "Автомобиль", "Пробег_км", "Категория", "Товар",
                    "Описание", "Кол_во", "Сумма_BYN", "Комментарий"
                }, repo.Repairs);
            WriteCsv(Path.Combine(folder, "utilities.csv"), BuildUtilityHeaders(), repo.Utilities);
            WriteCsv(Path.Combine(folder, "repair-categories.csv"),
                new string[] { "Категория" }, repo.Categories);
        }

        internal static void ImportFolder(AlfaRepository repo, string folder)
        {
            List<string[]> cars = ReadCsv(Path.Combine(folder, "cars.csv"), 1, true);
            List<string[]> fuel = ReadCsv(Path.Combine(folder, "fuel.csv"), 5, true);
            List<string[]> repairs = ReadCsv(Path.Combine(folder, "repairs.csv"), 9, true);
            List<string[]> utilities = ReadCsv(Path.Combine(folder, "utilities.csv"), 8, true);
            string categoriesPath = Path.Combine(folder, "repair-categories.csv");
            List<string[]> categories = File.Exists(categoriesPath)
                ? ReadCsv(categoriesPath, 1, true)
                : new List<string[]>();
            repo.ReplaceAll(cars, fuel, repairs, utilities, categories);
        }

        internal static void ImportGoogleSheetWorkbook(AlfaRepository repo, string spreadsheetUrl)
        {
            string id = ExtractSpreadsheetId(spreadsheetUrl);
            if (id == null)
                throw new InvalidDataException("Не удалось распознать ссылку Google Sheets. Нужна ссылка вида https://docs.google.com/spreadsheets/d/ID/...");

            // gid map: default first sheets by export of known names via CSV of whole export isn't multi-sheet.
            // Strategy: try common gids 0..4 and also accept "export?format=csv" single sheet for fuel-like data.
            // Better: download each sheet by name using /gviz/tq?tqx=out:csv&sheet=Name
            List<string[]> cars = DownloadSheet(id, "Автомобили", "cars", 1);
            List<string[]> fuel = DownloadSheet(id, "Заправки", "fuel", 5);
            List<string[]> repairs = DownloadSheet(id, "Ремонт", "repairs", 9);
            List<string[]> utilities = DownloadSheet(id, "Коммуналка", "utilities", 8);
            List<string[]> categories = DownloadSheetOptional(id, "Категории", "repair-categories", 1);
            if (cars.Count == 0)
                throw new InvalidDataException("В таблице нет листа «Автомобили» (или «cars») с данными. Сделайте таблицу доступной по ссылке.");
            repo.ReplaceAll(cars, fuel, repairs, utilities, categories);
        }

        internal static string ExtractSpreadsheetId(string url)
        {
            if (String.IsNullOrEmpty(url))
                return null;
            Match match = Regex.Match(url, @"/spreadsheets/d/([a-zA-Z0-9-_]+)");
            if (match.Success)
                return match.Groups[1].Value;
            match = Regex.Match(url, @"^[a-zA-Z0-9-_]{20,}$");
            return match.Success ? url.Trim() : null;
        }

        private static List<string[]> DownloadSheet(string id, string ruName, string enName, int columns)
        {
            List<string[]> rows = DownloadSheetOptional(id, ruName, enName, columns);
            return rows;
        }

        private static List<string[]> DownloadSheetOptional(string id, string ruName, string enName, int columns)
        {
            Exception lastError = null;
            string[] names = new string[] { ruName, enName };
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    string url = "https://docs.google.com/spreadsheets/d/" + id +
                        "/gviz/tq?tqx=out:csv&sheet=" + Uri.EscapeDataString(names[i]);
                    string csv = DownloadText(url);
                    if (LooksLikeHtmlError(csv))
                        continue;
                    return ParseCsv(csv, columns, true);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            if (columns == 1 && String.Equals(enName, "repair-categories", StringComparison.OrdinalIgnoreCase))
                return new List<string[]>();

            if (lastError != null)
                throw new InvalidDataException("Не удалось скачать лист «" + ruName + "»: " + lastError.Message, lastError);
            return new List<string[]>();
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
                    writer.WriteLine(JoinCsv(Pad(rows[i], headers.Length)));
            }
        }

        internal static List<string[]> ReadCsv(string path, int columns, bool hasHeader)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Не найден файл: " + path, path);
            string text = File.ReadAllText(path, Encoding.UTF8);
            return ParseCsv(text, columns, hasHeader);
        }

        internal static List<string[]> ParseCsv(string text, int columns, bool hasHeader)
        {
            List<string[]> raw = ParseCsvRecords(text);
            List<string[]> rows = new List<string[]>();
            int start = hasHeader && raw.Count > 0 ? 1 : 0;
            for (int i = start; i < raw.Count; i++)
            {
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
