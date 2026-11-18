using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Alfa156.Desktop.Data;

namespace Alfa156.Desktop.Exchange
{
    /// <summary>
    /// Импорт формата таблицы «Деньги»:
    /// Заправки Alfa Romeo 156 | Ремонт Alfa Romeo 156 | Коммунальные | Справочник | Итоги
    /// </summary>
    internal static class DengiFormat
    {
        private static readonly string[] MonthNames = new string[]
        {
            "январь", "февраль", "март", "апрель", "май", "июнь",
            "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"
        };

        internal static bool LooksLikeDengiSheetNames(IEnumerable<string> sheetNames)
        {
            bool hasFuel = false, hasRepair = false, hasUtilities = false;
            foreach (string name in sheetNames)
            {
                if (name == null) continue;
                if (name.StartsWith("Заправки", StringComparison.OrdinalIgnoreCase)) hasFuel = true;
                if (name.StartsWith("Ремонт", StringComparison.OrdinalIgnoreCase)) hasRepair = true;
                if (String.Equals(name, "Коммунальные", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(name, "Коммуналка", StringComparison.OrdinalIgnoreCase))
                    hasUtilities = true;
            }
            return hasFuel || hasRepair || hasUtilities;
        }

        internal static List<string[]> BuildCars(IEnumerable<string> sheetNames)
        {
            Dictionary<string, string> cars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in sheetNames)
            {
                if (name == null) continue;
                string car = null;
                if (name.StartsWith("Заправки ", StringComparison.OrdinalIgnoreCase))
                    car = name.Substring("Заправки ".Length).Trim();
                else if (name.StartsWith("Ремонт ", StringComparison.OrdinalIgnoreCase))
                    car = name.Substring("Ремонт ".Length).Trim();
                if (!String.IsNullOrEmpty(car) && !cars.ContainsKey(car))
                    cars[car] = car;
            }
            if (cars.Count == 0)
                cars["Alfa Romeo 156"] = "Alfa Romeo 156";

            List<string[]> rows = new List<string[]>();
            foreach (string car in cars.Values)
                rows.Add(new string[] { car });
            return rows;
        }

        internal static string CarFromSheetName(string sheetName, string fallback)
        {
            if (String.IsNullOrEmpty(sheetName))
                return fallback;
            if (sheetName.StartsWith("Заправки ", StringComparison.OrdinalIgnoreCase))
                return sheetName.Substring("Заправки ".Length).Trim();
            if (sheetName.StartsWith("Ремонт ", StringComparison.OrdinalIgnoreCase))
                return sheetName.Substring("Ремонт ".Length).Trim();
            return fallback;
        }

        /// <summary>
        /// Заправки: Дата | Пробег | Литры | Стоимость | ...
        /// </summary>
        internal static List<string[]> MapFuel(List<string[]> raw, string car)
        {
            List<string[]> rows = new List<string[]>();
            for (int i = 0; i < raw.Count; i++)
            {
                string[] src = raw[i];
                if (src.Length == 0) continue;
                if (IsHeaderCell(src[0], "дата")) continue;
                string date = NormalizeAnyDate(src[0]);
                if (String.IsNullOrEmpty(date)) continue;
                string km = src.Length > 1 ? CleanNumber(src[1]) : "";
                string liters = src.Length > 2 ? CleanNumber(src[2]) : "";
                string cost = src.Length > 3 ? CleanNumber(src[3]) : "";
                if (km.Length == 0 && liters.Length == 0 && cost.Length == 0) continue;
                rows.Add(new string[] { date, car, km, liters, cost });
            }
            return rows;
        }

        /// <summary>
        /// Ремонт: Дата | Пробег | Категория | Товар | Наименование | Кол-во | Сумма | Комментарий
        /// </summary>
        internal static List<string[]> MapRepairs(List<string[]> raw, string car)
        {
            List<string[]> rows = new List<string[]>();
            for (int i = 0; i < raw.Count; i++)
            {
                string[] src = raw[i];
                if (src.Length == 0) continue;
                if (IsHeaderCell(src[0], "дата")) continue;
                string date = NormalizeAnyDate(src[0]);
                if (String.IsNullOrEmpty(date)) continue;
                string km = src.Length > 1 ? CleanNumber(src[1]) : "";
                string category = src.Length > 2 ? (src[2] ?? "").Trim() : "";
                string product = src.Length > 3 ? CleanMultiline(src[3]) : "";
                string description = src.Length > 4 ? CleanMultiline(src[4]) : "";
                string qty = src.Length > 5 ? CleanNumber(src[5]) : "1";
                if (qty.Length == 0) qty = "1";
                string sum = src.Length > 6 ? CleanNumber(src[6]) : "";
                string comment = src.Length > 7 ? CleanMultiline(src[7]) : "";
                if (product.Length == 0 && description.Length == 0 && sum.Length == 0) continue;
                rows.Add(new string[] { date, car, km, category, product, description, qty, sum, comment });
            }
            return rows;
        }

        /// <summary>
        /// Коммунальные: месяц текстом | 7 сумм
        /// </summary>
        internal static List<string[]> MapUtilities(List<string[]> raw)
        {
            List<string[]> rows = new List<string[]>();
            for (int i = 0; i < raw.Count; i++)
            {
                string[] src = raw[i];
                if (src.Length == 0) continue;
                string monthText = (src[0] ?? "").Trim();
                if (monthText.Length == 0) continue;
                if (IsHeaderCell(monthText, "дата") || monthText.IndexOf("вид", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (monthText.IndexOf("не определено", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                string month = NormalizeRussianMonth(monthText);
                if (String.IsNullOrEmpty(month))
                    month = AlfaCodec.NormalizeMonth(monthText);
                if (String.IsNullOrEmpty(month) || !Regex.IsMatch(month, @"^\d{4}-\d{2}$"))
                    continue;

                string[] row = new string[8];
                row[0] = month;
                for (int c = 1; c <= 7; c++)
                    row[c] = src.Length > c ? CleanNumber(src[c]) : "";
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>
        /// Справочник: категория [| описание]
        /// </summary>
        internal static List<string[]> MapCategories(List<string[]> raw)
        {
            List<string[]> rows = new List<string[]>();
            Dictionary<string, string> seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < raw.Count; i++)
            {
                string[] src = raw[i];
                if (src.Length == 0) continue;
                string name = (src[0] ?? "").Trim();
                if (name.Length == 0) continue;
                if (IsHeaderCell(name, "категория")) continue;
                if (seen.ContainsKey(name)) continue;
                seen[name] = name;
                rows.Add(new string[] { name });
            }
            if (rows.Count == 0)
            {
                for (int i = 0; i < AlfaCodec.DefaultRepairCategories.Length; i++)
                    rows.Add(new string[] { AlfaCodec.DefaultRepairCategories[i] });
            }
            return rows;
        }

        internal static bool LooksLikeMonthList(List<string[]> rows)
        {
            if (rows == null || rows.Count < 3) return false;
            int months = 0;
            int take = Math.Min(rows.Count, 8);
            for (int i = 0; i < take; i++)
            {
                if (rows[i].Length == 0) continue;
                if (!String.IsNullOrEmpty(NormalizeRussianMonth(rows[i][0])))
                    months++;
            }
            return months >= 3;
        }

        internal static string NormalizeAnyDate(string text)
        {
            string value = (text ?? "").Trim();
            if (value.Length == 0) return "";

            // Excel serial date
            double serial;
            if (Double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out serial)
                && serial > 20000 && serial < 80000)
            {
                DateTime epoch = new DateTime(1899, 12, 30);
                return epoch.AddDays(serial).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return AlfaCodec.NormalizeDate(value);
        }

        internal static string NormalizeRussianMonth(string text)
        {
            string value = (text ?? "").Trim().ToLowerInvariant();
            if (value.Length == 0) return null;
            Match match = Regex.Match(value, @"([а-яё]+)\s+(\d{4})");
            if (!match.Success) return null;
            string monthName = match.Groups[1].Value;
            int year = Int32.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            for (int i = 0; i < MonthNames.Length; i++)
            {
                if (String.Equals(MonthNames[i], monthName, StringComparison.OrdinalIgnoreCase))
                    return year.ToString("0000", CultureInfo.InvariantCulture) + "-" + (i + 1).ToString("00", CultureInfo.InvariantCulture);
            }
            return null;
        }

        private static bool IsHeaderCell(string value, string expected)
        {
            string text = (value ?? "").Trim().ToLowerInvariant();
            return text.StartsWith(expected, StringComparison.Ordinal);
        }

        private static string CleanNumber(string text)
        {
            string value = (text ?? "").Trim();
            if (value.Length == 0) return "";
            if (value.StartsWith("#", StringComparison.Ordinal)) return "";
            decimal number;
            if (AlfaCodec.TryNumber(value, out number))
                return number.ToString(CultureInfo.InvariantCulture);
            return value;
        }

        private static string CleanMultiline(string text)
        {
            if (text == null) return "";
            return text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
        }
    }
}
