using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CarExpenses
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
        private const string DataDirectory = @"\My Documents\Alfa156";

        internal static void EnsureFile()
        {
            EnsureFileIn(DataDirectory);
        }

        internal static void EnsureFileIn(string directory)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, FileName);
            if (!File.Exists(path))
                DataStore.WriteSyncTable(directory, FileName, new List<string[]>());
        }

        internal static List<string[]> LoadRows()
        {
            return LoadRowsFrom(DataDirectory);
        }

        internal static List<string[]> LoadRowsFrom(string directory)
        {
            return DataStore.ReadSyncTable(directory, FileName);
        }

        internal static void SaveRows(List<string[]> rows)
        {
            SaveRowsTo(DataDirectory, rows);
        }

        internal static void SaveRowsTo(string directory, List<string[]> rows)
        {
            DataStore.WriteSyncTable(directory, FileName, rows ?? new List<string[]>());
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
            if (meta.Id != null && meta.Id.Length > 0 && RecordIds.LooksLikeId(meta.Id))
                return RecordIds.WithId(content, meta.Id);
            return RecordIds.EnsureId(content, RecordIds.PrefixForFile(FileName));
        }

        internal static CarPassport Get(string carName)
        {
            carName = (carName ?? "").Trim();
            List<string[]> rows = LoadRows();
            for (int i = 0; i < rows.Count; i++)
            {
                CarPassport meta = FromRow(rows[i]);
                if (String.Equals(meta.CarName, carName, StringComparison.OrdinalIgnoreCase))
                    return meta;
            }
            return new CarPassport { CarName = carName };
        }

        internal static void Upsert(CarPassport meta)
        {
            if (meta == null)
                throw new ArgumentNullException("meta");
            meta.CarName = (meta.CarName ?? "").Trim();
            if (meta.CarName.Length == 0)
                throw new InvalidOperationException("Введите название автомобиля.");

            List<string[]> rows = LoadRows();
            bool found = false;
            for (int i = 0; i < rows.Count; i++)
            {
                CarPassport existing = FromRow(rows[i]);
                if (!String.Equals(existing.CarName, meta.CarName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (meta.Id == null || meta.Id.Length == 0)
                    meta.Id = existing.Id;
                rows[i] = ToRow(meta);
                found = true;
                break;
            }
            if (!found)
                rows.Add(ToRow(meta));
            SaveRows(rows);
        }

        internal static void RenameCar(string oldName, string newName)
        {
            oldName = (oldName ?? "").Trim();
            newName = (newName ?? "").Trim();
            if (oldName.Length == 0 || newName.Length == 0)
                return;
            List<string[]> rows = LoadRows();
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
                SaveRows(rows);
        }

        internal static void DeleteCar(string carName)
        {
            carName = (carName ?? "").Trim();
            List<string[]> rows = LoadRows();
            List<string[]> kept = new List<string[]>();
            for (int i = 0; i < rows.Count; i++)
            {
                CarPassport meta = FromRow(rows[i]);
                if (String.Equals(meta.CarName, carName, StringComparison.OrdinalIgnoreCase))
                {
                    if (meta.Id != null && meta.Id.Length > 0)
                        RecordIds.AddTombstone(DataDirectory, FileName, meta.Id);
                    continue;
                }
                kept.Add(rows[i]);
            }
            SaveRows(kept);
        }

        internal static string HeaderSuffix(string carName)
        {
            CarPassport meta = Get(carName);
            StringBuilder sb = new StringBuilder();
            if (meta.Year.Length > 0)
                sb.Append(" · ").Append(meta.Year);
            if (meta.Plate.Length > 0)
                sb.Append(" · ").Append(meta.Plate);
            return sb.ToString();
        }

        internal static string TankNearHint(string carName, string litersText)
        {
            CarPassport meta = Get(carName);
            decimal tank;
            decimal liters;
            if (!DataStore.TryNumber(meta.TankLiters, out tank) || tank <= 0)
                return null;
            if (!DataStore.TryNumber(litersText, out liters) || liters <= 0)
                return null;
            decimal delta = liters > tank ? liters - tank : tank - liters;
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
            if (!DataStore.TryNumber(value, out number))
                return value;
            return number.ToString("0.##", CultureInfo.InvariantCulture);
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
        internal static List<TimelineEntry> Build(string carName)
        {
            carName = (carName ?? "").Trim();
            List<TimelineEntry> list = new List<TimelineEntry>();
            List<string[]> fuel = DataStore.LoadFuel();
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

            List<string[]> repairs = DataStore.LoadRepairs();
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

            List<string[]> expenses = DataStore.LoadExpenses();
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

            List<CarNote> notes = CarNotes.ListForCar(carName);
            for (int i = 0; i < notes.Count; i++)
            {
                TimelineEntry entry = new TimelineEntry();
                entry.Date = notes[i].Date ?? "";
                entry.TypeKey = "note";
                entry.TypeLabel = "Заметка";
                entry.Text = CarNotes.Truncate(notes[i].Text, 80);
                entry.Sum = 0;
                entry.SortKey = entry.Date + "|3|" + i.ToString("0000", CultureInfo.InvariantCulture);
                list.Add(entry);
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
            if (entry.TypeKey == "note")
                return entry.Date + "  " + entry.TypeLabel + "  " + entry.Text;
            return entry.Date + "  " + entry.TypeLabel + "  " + entry.Text + "  " +
                entry.Sum.ToString("0.##", CultureInfo.InvariantCulture) + " BYN";
        }

        internal static string FormatList(string carName)
        {
            List<TimelineEntry> list = Build(carName);
            if (list.Count == 0)
                return "Нет записей топлива, ремонта, прочих расходов и заметок для «" + carName + "».";
            StringBuilder sb = new StringBuilder();
            sb.Append("Лента · ").Append(carName).Append(CarsMeta.HeaderSuffix(carName));
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
            if (DataStore.TryNumber(text, out value))
                return value;
            return 0;
        }
    }
}
