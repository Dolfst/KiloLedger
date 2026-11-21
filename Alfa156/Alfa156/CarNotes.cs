using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using Alfa156.Localization;
namespace CarExpenses
{
    /// <summary>
    /// Free-form dated car notes in notes.dat:
    /// date|car|text|id
    /// CF 3.5 safe. Not expenses — journal only.
    /// </summary>
    internal sealed class CarNote
    {
        public string Date = "";
        public string Car = "";
        public string Text = "";
        public string Id;
    }

    internal static class CarNotes
    {
        internal const string FileName = "notes.dat";
        internal const int MaxTextLength = 500;
        private const string DataDirectory = @"\My Documents\Alfa156";
        private const string DateFormat = "yyyy-MM-dd";

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
            if (content.Length != 3)
                return false;
            if (!IsValidDate(content[0]))
                return false;
            if ((content[1] ?? "").Trim().Length == 0)
                return false;
            if ((content[2] ?? "").Trim().Length == 0)
                return false;
            if (RecordIds.HasId(row) && !RecordIds.LooksLikeId(RecordIds.GetId(row)))
                return false;
            return true;
        }

        internal static bool IsValidDate(string text)
        {
            DateTime ignored;
            return TryParseDate(text, out ignored);
        }

        internal static bool TryParseDate(string text, out DateTime date)
        {
            date = DateTime.MinValue;
            if (text == null)
                return false;
            string raw = text.Trim();
            if (raw.Length != 10 || raw[4] != '-' || raw[7] != '-')
                return false;
            try
            {
                int year = int.Parse(raw.Substring(0, 4), NumberStyles.None, CultureInfo.InvariantCulture);
                int month = int.Parse(raw.Substring(5, 2), NumberStyles.None, CultureInfo.InvariantCulture);
                int day = int.Parse(raw.Substring(8, 2), NumberStyles.None, CultureInfo.InvariantCulture);
                date = new DateTime(year, month, day);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        internal static CarNote FromRow(string[] row)
        {
            CarNote item = new CarNote();
            if (row == null)
                return item;
            string[] content = RecordIds.StripId(row);
            if (content.Length < 3)
                return item;
            item.Date = (content[0] ?? "").Trim();
            item.Car = (content[1] ?? "").Trim();
            item.Text = (content[2] ?? "").Trim();
            item.Id = RecordIds.GetId(row);
            return item;
        }

        internal static string[] ToRow(CarNote item)
        {
            if (item == null)
                item = new CarNote();
            string[] content = new string[]
            {
                (item.Date ?? "").Trim(),
                (item.Car ?? "").Trim(),
                (item.Text ?? "").Trim()
            };
            if (item.Id != null && item.Id.Length > 0 && RecordIds.LooksLikeId(item.Id))
                return RecordIds.WithId(content, item.Id);
            return RecordIds.EnsureId(content, RecordIds.PrefixForFile(FileName));
        }

        internal static List<CarNote> ListAll()
        {
            return ListAllFrom(DataDirectory);
        }

        internal static List<CarNote> ListAllFrom(string directory)
        {
            List<CarNote> result = new List<CarNote>();
            List<string[]> rows = LoadRowsFrom(directory);
            for (int i = 0; i < rows.Count; i++)
            {
                if (!IsValidRow(rows[i]))
                    continue;
                result.Add(FromRow(rows[i]));
            }
            result.Sort(new CarNoteComparer());
            return result;
        }

        internal static List<CarNote> ListForCar(string carName)
        {
            return ListForCarFrom(DataDirectory, carName);
        }

        internal static List<CarNote> ListForCarFrom(string directory, string carName)
        {
            carName = (carName ?? "").Trim();
            List<CarNote> all = ListAllFrom(directory);
            if (carName.Length == 0)
                return all;
            List<CarNote> filtered = new List<CarNote>();
            for (int i = 0; i < all.Count; i++)
                if (String.Compare(all[i].Car, carName, true, CultureInfo.InvariantCulture) == 0)
                    filtered.Add(all[i]);
            return filtered;
        }

        private sealed class CarNoteComparer : IComparer<CarNote>
        {
            public int Compare(CarNote a, CarNote b)
            {
                // Newest first
                int cmp = String.CompareOrdinal(b.Date ?? "", a.Date ?? "");
                if (cmp != 0)
                    return cmp;
                return String.Compare(a.Text ?? "", b.Text ?? "",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        internal static void Upsert(CarNote item)
        {
            if (item == null)
                throw new ArgumentNullException("item");
            item.Date = (item.Date ?? "").Trim();
            item.Car = (item.Car ?? "").Trim();
            item.Text = (item.Text ?? "").Trim();
            if (!IsValidDate(item.Date))
                throw new InvalidOperationException(L10n.Get("error.date_format_upper"));
            if (item.Car.Length == 0)
                throw new InvalidOperationException(L10n.Get("error.select_car"));
            if (item.Text.Length == 0)
                throw new InvalidOperationException(L10n.Get("error.enter_note_text"));
            if (item.Text.Length > MaxTextLength)
                throw new InvalidOperationException(L10n.Get("error.note_text_len"));

            List<string[]> rows = LoadRows();
            bool found = false;
            if (item.Id != null && item.Id.Length > 0 && RecordIds.LooksLikeId(item.Id))
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    if (String.Compare(RecordIds.GetId(rows[i]), item.Id,
                            StringComparison.OrdinalIgnoreCase) != 0)
                        continue;
                    rows[i] = ToRow(item);
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    CarNote existing = FromRow(rows[i]);
                    if (String.CompareOrdinal(existing.Date, item.Date) != 0)
                        continue;
                    if (String.Compare(existing.Car, item.Car, StringComparison.OrdinalIgnoreCase) != 0)
                        continue;
                    if (String.Compare(existing.Text, item.Text, StringComparison.Ordinal) != 0)
                        continue;
                    if (item.Id == null || item.Id.Length == 0)
                        item.Id = existing.Id;
                    rows[i] = ToRow(item);
                    found = true;
                    break;
                }
            }
            if (!found)
                rows.Add(ToRow(item));
            SaveRows(rows);
        }

        internal static void DeleteById(string id)
        {
            id = (id ?? "").Trim();
            if (!RecordIds.LooksLikeId(id))
                return;
            List<string[]> rows = LoadRows();
            List<string[]> kept = new List<string[]>();
            for (int i = 0; i < rows.Count; i++)
            {
                string rowId = RecordIds.GetId(rows[i]);
                if (String.Compare(rowId, id, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    RecordIds.AddTombstone(DataDirectory, FileName, id);
                    continue;
                }
                kept.Add(rows[i]);
            }
            SaveRows(kept);
        }

        internal static string Truncate(string text, int max)
        {
            text = (text ?? "").Trim();
            if (max < 1 || text.Length <= max)
                return text;
            return text.Substring(0, max).Trim() + "…";
        }

        internal static string FormatLine(CarNote item)
        {
            if (item == null)
                return "";
            return item.Date + " · " + Truncate(item.Text, 60) + " (" + item.Car + ")";
        }

        internal static string FormatRecentBlock(string carName, int limit)
        {
            if (limit < 1)
                limit = 3;
            List<CarNote> list = ListForCar(carName);
            StringBuilder sb = new StringBuilder();
            sb.Append(("\r\n\r\n" + L10n.Get("label.notes")));
            if (carName != null && carName.Trim().Length > 0)
            {
                sb.Append(" («");
                sb.Append(carName.Trim());
                sb.Append("»)");
            }
            sb.Append(":");
            if (list.Count == 0)
            {
                sb.Append(("\r\n" + L10n.Get("common.no_lower")));
                return sb.ToString();
            }
            int shown = list.Count < limit ? list.Count : limit;
            for (int i = 0; i < shown; i++)
            {
                sb.Append("\r\n");
                sb.Append(FormatLine(list[i]));
            }
            if (list.Count > shown)
            {
                sb.Append(("\r\n" + L10n.Get("summary.and_more") + " "));
                sb.Append((list.Count - shown).ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        internal static string FormatAllBlock(string carName)
        {
            List<CarNote> list = ListForCar(carName);
            StringBuilder sb = new StringBuilder();
            sb.Append(L10n.Get("label.notes"));
            if (carName != null && carName.Trim().Length > 0)
            {
                sb.Append(" · ");
                sb.Append(carName.Trim());
            }
            sb.Append(":");
            if (list.Count == 0)
            {
                sb.Append(("\r\n" + L10n.Get("summary.none_yet")));
                return sb.ToString();
            }
            for (int i = 0; i < list.Count; i++)
            {
                sb.Append("\r\n");
                sb.Append((i + 1).ToString(CultureInfo.InvariantCulture));
                sb.Append(". ");
                sb.Append(FormatLine(list[i]));
            }
            return sb.ToString();
        }
    }
}
