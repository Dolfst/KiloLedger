using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using KiloLedger.Desktop.Data;
using KiloLedger.Localization;

namespace KiloLedger.Desktop.Exchange
{
    /// <summary>
    /// Импорт формата таблицы «Деньги»:
    /// Заправки {авто} | Ремонт {авто} | Коммунальные | Справочник | Итоги
    /// </summary>
    internal static class DengiFormat
    {
        internal struct SheetCell
        {
            internal string Text;
            internal bool DateFormatted;

            internal SheetCell(string text, bool dateFormatted)
            {
                Text = text ?? "";
                DateFormatted = dateFormatted;
            }
        }

        internal static readonly List<string> DateNumberWarnings = new List<string>();

        internal static void ResetWarnings()
        {
            DateNumberWarnings.Clear();
        }

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
            {
                string defaultCar = L10n.Get("label.default_car_name");
                cars[defaultCar] = defaultCar;
            }

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
            return MapFuel(CellsFromText(raw), car);
        }

        internal static List<string[]> MapFuel(List<SheetCell[]> raw, string car)
        {
            List<string[]> rows = new List<string[]>();
            if (raw == null)
                return rows;
            for (int i = 0; i < raw.Count; i++)
            {
                SheetCell[] src = raw[i];
                if (src == null || src.Length == 0) continue;
                if (IsHeaderCell(src[0].Text, "дата")) continue;
                string date = NormalizeAnyDate(src[0].Text);
                if (String.IsNullOrEmpty(date)) continue;
                string km = CleanMeasure(CellAt(src, 1));
                string liters = CleanMeasure(CellAt(src, 2));
                string cost = CleanMeasure(CellAt(src, 3));
                // Колонка после «Расход»: цена за литр. Текст и пустая ячейка не удлиняют строку.
                string priceText = src.Length > 5 ? CleanMeasure(CellAt(src, 5)) : "";
                decimal priceNumber;
                string price = KiloLedgerCodec.TryNumber(priceText, out priceNumber)
                    ? priceNumber.ToString(CultureInfo.InvariantCulture)
                    : "";
                if (km.Length == 0 && liters.Length == 0 && cost.Length == 0) continue;
                if (price.Length > 0)
                    rows.Add(new string[] { date, car, km, liters, cost, price });
                else
                    rows.Add(new string[] { date, car, km, liters, cost });
            }
            return rows;
        }

        /// <summary>
        /// Ремонт: Дата | Пробег | Категория | Товар | Наименование | Кол-во | Сумма | Комментарий
        /// </summary>
        internal static List<string[]> MapRepairs(List<string[]> raw, string car)
        {
            return MapRepairs(CellsFromText(raw), car);
        }

        internal static List<string[]> MapRepairs(List<SheetCell[]> raw, string car)
        {
            List<string[]> rows = new List<string[]>();
            if (raw == null)
                return rows;
            for (int i = 0; i < raw.Count; i++)
            {
                SheetCell[] src = raw[i];
                if (src == null || src.Length == 0) continue;
                if (IsHeaderCell(src[0].Text, "дата")) continue;
                string date = NormalizeAnyDate(src[0].Text);
                if (String.IsNullOrEmpty(date)) continue;
                string km = CleanMeasure(CellAt(src, 1));
                string category = src.Length > 2 ? (src[2].Text ?? "").Trim() : "";
                string product = src.Length > 3 ? CleanMultiline(src[3].Text) : "";
                string description = src.Length > 4 ? CleanMultiline(src[4].Text) : "";
                string qty = src.Length > 5 ? CleanMeasure(src[5]) : "1";
                if (qty.Length == 0) qty = "1";
                string sum = src.Length > 6 ? CleanMeasure(src[6]) : "";
                string comment = src.Length > 7 ? CleanMultiline(src[7].Text) : "";
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
            return MapUtilities(CellsFromText(raw));
        }

        internal static List<string[]> MapUtilities(List<SheetCell[]> raw)
        {
            List<string[]> rows = new List<string[]>();
            if (raw == null)
                return rows;
            for (int i = 0; i < raw.Count; i++)
            {
                SheetCell[] src = raw[i];
                if (src == null || src.Length == 0) continue;
                string monthText = (src[0].Text ?? "").Trim();
                if (monthText.Length == 0) continue;
                if (IsHeaderCell(monthText, "дата") || monthText.IndexOf("вид", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (monthText.IndexOf("не определено", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                string month = NormalizeRussianMonth(monthText);
                if (String.IsNullOrEmpty(month))
                    month = KiloLedgerCodec.NormalizeMonth(monthText);
                if (String.IsNullOrEmpty(month) || !Regex.IsMatch(month, @"^\d{4}-\d{2}$"))
                    continue;

                string[] row = new string[8];
                row[0] = month;
                for (int c = 1; c <= 7; c++)
                    row[c] = CleanMeasure(CellAt(src, c));
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
                for (int i = 0; i < KiloLedgerCodec.DefaultRepairCategories.Length; i++)
                    rows.Add(new string[] { KiloLedgerCodec.DefaultRepairCategories[i] });
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

            return KiloLedgerCodec.NormalizeDate(value);
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

        private static List<SheetCell[]> CellsFromText(List<string[]> raw)
        {
            List<SheetCell[]> rows = new List<SheetCell[]>();
            if (raw == null)
                return rows;
            for (int i = 0; i < raw.Count; i++)
            {
                string[] src = raw[i] ?? new string[0];
                SheetCell[] cells = new SheetCell[src.Length];
                for (int c = 0; c < src.Length; c++)
                    cells[c] = new SheetCell(src[c], false);
                rows.Add(cells);
            }
            return rows;
        }

        private static SheetCell CellAt(SheetCell[] src, int index)
        {
            if (src == null || index < 0 || index >= src.Length)
                return new SheetCell("", false);
            return src[index];
        }

        private static string CleanMeasure(SheetCell cell)
        {
            string recovered;
            DateNumberKind kind = DengiDateNumbers.Classify(cell.Text, cell.DateFormatted, out recovered);
            if (kind == DateNumberKind.Recovered)
                return recovered;
            if (kind == DateNumberKind.Ambiguous)
            {
                if (DateNumberWarnings.Count < 30)
                {
                    string shown = (cell.Text ?? "").Trim();
                    if (shown.Length == 0)
                        shown = "?";
                    DateNumberWarnings.Add(L10n.Format("import.date_number_skipped", shown));
                }
                return "";
            }
            return CleanNumber(cell.Text);
        }

        private static string CleanNumber(string text)
        {
            string value = (text ?? "").Trim();
            if (value.Length == 0) return "";
            if (value.StartsWith("#", StringComparison.Ordinal)) return "";
            decimal number;
            if (KiloLedgerCodec.TryNumber(value, out number))
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
