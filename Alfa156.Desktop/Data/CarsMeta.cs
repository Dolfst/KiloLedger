using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Alfa156.Desktop.Data
{

    /// <summary>
    /// Passport fields for a car, keyed by car name in cars-meta.dat:
    /// carName|year|plate|vin|tankLiters|note|id
    /// </summary>
    internal sealed class CarPassport
    {
        public string CarName = "";
        public string Year = "";
        public string Plate = "";
        public string Vin = "";
        public string TankLiters = "";
        public string Note = "";
        public string Id;
    }

    internal static class CarsMeta
    {
        internal const string FileName = "cars-meta.dat";

        internal static void EnsureFile(string directory)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, FileName);
            if (!File.Exists(path))
                AlfaCodec.SaveRows(path, new List<string[]>());
        }

        internal static List<string[]> LoadRows(string directory)
        {
            string path = Path.Combine(directory, FileName);
            if (!File.Exists(path))
                return new List<string[]>();
            return AlfaCodec.ReadRows(path);
        }

        internal static void SaveRows(string directory, List<string[]> rows)
        {
            Directory.CreateDirectory(directory);
            AlfaCodec.SaveRows(Path.Combine(directory, FileName), rows ?? new List<string[]>());
        }

        internal static bool IsValidRow(string[] row)
        {
            if (row == null)
                return false;
            string[] content = RecordIds.StripId(row);
            if (content.Length < 1 || content.Length > 6)
                return false;
            if ((content[0] ?? "").Trim().Length == 0)
                return false;
            if (RecordIds.HasId(row) && !RecordIds.LooksLikeId(RecordIds.GetId(row)))
                return false;
            return true;
        }

        internal static CarPassport FromRow(string[] row)
        {
            CarPassport meta = new CarPassport();
            if (row == null)
                return meta;
            string[] content = RecordIds.StripId(row);
            meta.CarName = Field(content, 0);
            meta.Year = Field(content, 1);
            meta.Plate = Field(content, 2);
            meta.Vin = Field(content, 3);
            meta.TankLiters = Field(content, 4);
            meta.Note = Field(content, 5);
            meta.Id = RecordIds.GetId(row);
            return meta;
        }

        internal static string[] ToRow(CarPassport meta)
        {
            if (meta == null)
                meta = new CarPassport();
            string[] content = new string[]
            {
                (meta.CarName ?? "").Trim(),
                (meta.Year ?? "").Trim(),
                (meta.Plate ?? "").Trim(),
                (meta.Vin ?? "").Trim(),
                NormalizeNumber(meta.TankLiters),
                (meta.Note ?? "").Trim()
            };
            if (!String.IsNullOrEmpty(meta.Id) && RecordIds.LooksLikeId(meta.Id))
                return RecordIds.WithId(content, meta.Id);
            return RecordIds.EnsureId(content, RecordIds.PrefixForFile(FileName));
        }

        internal static CarPassport Get(string directory, string carName)
        {
            carName = (carName ?? "").Trim();
            List<string[]> rows = LoadRows(directory);
            for (int i = 0; i < rows.Count; i++)
            {
                CarPassport meta = FromRow(rows[i]);
                if (String.Equals(meta.CarName, carName, StringComparison.OrdinalIgnoreCase))
                    return meta;
            }
            return new CarPassport { CarName = carName };
        }

        internal static void Upsert(string directory, CarPassport meta)
        {
            if (meta == null)
                throw new ArgumentNullException("meta");
            meta.CarName = (meta.CarName ?? "").Trim();
            if (meta.CarName.Length == 0)
                throw new InvalidOperationException("Введите название автомобиля.");

            List<string[]> rows = LoadRows(directory);
            bool found = false;
            for (int i = 0; i < rows.Count; i++)
            {
                CarPassport existing = FromRow(rows[i]);
                if (!String.Equals(existing.CarName, meta.CarName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (String.IsNullOrEmpty(meta.Id))
                    meta.Id = existing.Id;
                rows[i] = ToRow(meta);
                found = true;
                break;
            }
            if (!found)
                rows.Add(ToRow(meta));
            SaveRows(directory, rows);
        }

        internal static void RenameCar(string directory, string oldName, string newName)
        {
            oldName = (oldName ?? "").Trim();
            newName = (newName ?? "").Trim();
            if (oldName.Length == 0 || newName.Length == 0)
                return;
            List<string[]> rows = LoadRows(directory);
            bool changed = false;
            for (int i = 0; i < rows.Count; i++)
            {
                CarPassport meta = FromRow(rows[i]);
                if (!String.Equals(meta.CarName, oldName, StringComparison.OrdinalIgnoreCase))
                    continue;
                meta.CarName = newName;
                rows[i] = ToRow(meta);
                changed = true;
            }
            if (changed)
                SaveRows(directory, rows);
        }

        internal static void DeleteCar(string directory, string carName)
        {
            carName = (carName ?? "").Trim();
            List<string[]> rows = LoadRows(directory);
            List<string[]> kept = new List<string[]>();
            for (int i = 0; i < rows.Count; i++)
            {
                CarPassport meta = FromRow(rows[i]);
                if (String.Equals(meta.CarName, carName, StringComparison.OrdinalIgnoreCase))
                {
                    if (!String.IsNullOrEmpty(meta.Id))
                        RecordIds.AddTombstone(directory, FileName, meta.Id);
                    continue;
                }
                kept.Add(rows[i]);
            }
            SaveRows(directory, kept);
        }

        internal static string HeaderSuffix(string directory, string carName)
        {
            CarPassport meta = Get(directory, carName);
            StringBuilder sb = new StringBuilder();
            if (meta.Year.Length > 0)
                sb.Append(" · ").Append(meta.Year);
            if (meta.Plate.Length > 0)
                sb.Append(" · ").Append(meta.Plate);
            return sb.ToString();
        }

        /// <summary>
        /// Soft hint when fill liters are within ~5% of the passport tank volume.
        /// </summary>
        internal static string TankNearHint(string directory, string carName, string litersText)
        {
            CarPassport meta = Get(directory, carName);
            decimal tank;
            decimal liters;
            if (!TryDecimal(meta.TankLiters, out tank) || tank <= 0)
                return null;
            if (!TryDecimal(litersText, out liters) || liters <= 0)
                return null;
            decimal delta = Math.Abs(liters - tank);
            if (delta > tank * 0.05m && delta > 1m)
                return null;
            return "Литры близки к объёму бака (" +
                tank.ToString("0.##", CultureInfo.InvariantCulture) + " л).";
        }

        private static string Field(string[] content, int index)
        {
            return content != null && index < content.Length ? (content[index] ?? "").Trim() : "";
        }

        private static string NormalizeNumber(string value)
        {
            value = (value ?? "").Trim().Replace(',', '.');
            if (value.Length == 0)
                return "";
            decimal number;
            if (!TryDecimal(value, out number))
                return value;
            return number.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static bool TryDecimal(string text, out decimal value)
        {
            return Decimal.TryParse((text ?? "").Trim().Replace(',', '.'),
                NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }
    }

    internal sealed class TimelineEntry
    {
        public string Date = "";
        public string TypeKey = "";
        public string TypeLabel = "";
        public string Text = "";
        public decimal Sum;
        public string SortKey = "";
    }

    internal static class CarTimeline
    {
        internal static List<TimelineEntry> Build(string directory, string carName)
        {
            return Build(
                AlfaCodec.ReadRows(Path.Combine(directory, "fuel.dat")),
                AlfaCodec.ReadRows(Path.Combine(directory, "repairs.dat")),
                AlfaCodec.ReadRows(Path.Combine(directory, AlfaCodec.ExpensesFile)),
                carName);
        }

        internal static List<TimelineEntry> Build(
            List<string[]> fuel, List<string[]> repairs, List<string[]> expenses, string carName)
        {
            carName = (carName ?? "").Trim();
            List<TimelineEntry> list = new List<TimelineEntry>();
            if (fuel != null)
            {
                for (int i = 0; i < fuel.Count; i++)
                {
                    string[] content = RecordIds.StripId(fuel[i]);
                    if (content.Length < 5)
                        continue;
                    if (!String.Equals(content[1], carName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    TimelineEntry entry = new TimelineEntry();
                    entry.Date = content[0] ?? "";
                    entry.TypeKey = "fuel";
                    entry.TypeLabel = "Топл.";
                    entry.Text = (content[3] ?? "") + " л" +
                        (content.Length > 6 && (content[6] ?? "").Trim().Length > 0
                            ? " · " + content[6].Trim() : "");
                    entry.Sum = NumberOrZero(content[4]);
                    entry.SortKey = entry.Date + "|0|" + i.ToString("0000", CultureInfo.InvariantCulture);
                    list.Add(entry);
                }
            }

            if (repairs != null)
            {
                for (int i = 0; i < repairs.Count; i++)
                {
                    string[] content = RecordIds.StripId(repairs[i]);
                    if (content.Length < 8)
                        continue;
                    if (!String.Equals(content[1], carName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    TimelineEntry entry = new TimelineEntry();
                    entry.Date = content[0] ?? "";
                    entry.TypeKey = "repair";
                    entry.TypeLabel = "Ремонт";
                    string product = content.Length > 4 ? (content[4] ?? "").Trim() : "";
                    string category = content.Length > 3 ? (content[3] ?? "").Trim() : "";
                    entry.Text = product.Length > 0 ? product : category;
                    entry.Sum = NumberOrZero(content[7]);
                    entry.SortKey = entry.Date + "|1|" + i.ToString("0000", CultureInfo.InvariantCulture);
                    list.Add(entry);
                }
            }

            if (expenses != null)
            {
                for (int i = 0; i < expenses.Count; i++)
                {
                    string[] content = RecordIds.StripId(expenses[i]);
                    if (content.Length < 5)
                        continue;
                    if (!String.Equals(content[1], carName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    TimelineEntry entry = new TimelineEntry();
                    entry.Date = content[0] ?? "";
                    entry.TypeKey = "expense";
                    entry.TypeLabel = "Прочее";
                    string name = content.Length > 3 ? (content[3] ?? "").Trim() : "";
                    string category = content.Length > 2 ? (content[2] ?? "").Trim() : "";
                    entry.Text = name.Length > 0 ? name : category;
                    entry.Sum = NumberOrZero(content[4]);
                    entry.SortKey = entry.Date + "|2|" + i.ToString("0000", CultureInfo.InvariantCulture);
                    list.Add(entry);
                }
            }

            list.Sort(delegate(TimelineEntry a, TimelineEntry b)
            {
                return String.CompareOrdinal(b.SortKey, a.SortKey);
            });
            return list;
        }

        internal static string FormatLine(TimelineEntry entry)
        {
            if (entry == null)
                return "";
            return entry.Date + "  " + entry.TypeLabel + "  " + entry.Text + "  " +
                entry.Sum.ToString("0.##", CultureInfo.InvariantCulture) + " BYN";
        }

        internal static string FormatList(string directory, string carName)
        {
            return FormatList(Build(directory, carName), carName, CarsMeta.HeaderSuffix(directory, carName));
        }

        internal static string FormatList(List<TimelineEntry> list, string carName, string headerSuffix)
        {
            if (list == null || list.Count == 0)
                return "Нет записей топлива, ремонта и прочих расходов для «" + carName + "».";
            StringBuilder sb = new StringBuilder();
            sb.Append("Лента · ").Append(carName).Append(headerSuffix ?? "");
            sb.Append("\r\n\r\n");
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0)
                    sb.Append("\r\n");
                sb.Append(FormatLine(list[i]));
            }
            return sb.ToString();
        }

        private static decimal NumberOrZero(string text)
        {
            decimal value;
            if (Decimal.TryParse((text ?? "").Trim().Replace(',', '.'),
                NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                return value;
            return 0;
        }
    }
}
