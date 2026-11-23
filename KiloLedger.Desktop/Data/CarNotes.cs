using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using KiloLedger.Localization;
namespace KiloLedger.Desktop.Data
{
    /// <summary>
    /// Free-form dated car notes in notes.dat:
    /// date|car|text|id
    /// Not expenses — journal only.
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
        private const string DateFormat = "yyyy-MM-dd";

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
            if (raw.Length != 10)
                return false;
            return DateTime.TryParseExact(raw, DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date);
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
            if (!String.IsNullOrEmpty(item.Id) && RecordIds.LooksLikeId(item.Id))
                return RecordIds.WithId(content, item.Id);
            return RecordIds.EnsureId(content, RecordIds.PrefixForFile(FileName));
        }

        internal static List<CarNote> FromRows(List<string[]> rows)
        {
            List<CarNote> result = new List<CarNote>();
            if (rows == null)
                return result;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!IsValidRow(rows[i]))
                    continue;
                result.Add(FromRow(rows[i]));
            }
            result.Sort(CompareNewestFirst);
            return result;
        }

        internal static List<CarNote> ListForCar(List<string[]> rows, string carName)
        {
            carName = (carName ?? "").Trim();
            List<CarNote> all = FromRows(rows);
            if (carName.Length == 0)
                return all;
            List<CarNote> filtered = new List<CarNote>();
            for (int i = 0; i < all.Count; i++)
                if (String.Equals(all[i].Car, carName, StringComparison.OrdinalIgnoreCase))
                    filtered.Add(all[i]);
            return filtered;
        }

        private static int CompareNewestFirst(CarNote a, CarNote b)
        {
            int cmp = String.CompareOrdinal(b.Date ?? "", a.Date ?? "");
            if (cmp != 0)
                return cmp;
            return String.Compare(a.Text ?? "", b.Text ?? "", StringComparison.OrdinalIgnoreCase);
        }

        internal static void Upsert(List<string[]> rows, List<Tombstone> deleted, CarNote item)
        {
            if (rows == null)
                throw new ArgumentNullException("rows");
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

            bool found = false;
            if (!String.IsNullOrEmpty(item.Id) && RecordIds.LooksLikeId(item.Id))
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    if (!String.Equals(RecordIds.GetId(rows[i]), item.Id, StringComparison.OrdinalIgnoreCase))
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
                    if (!String.Equals(existing.Date, item.Date, StringComparison.Ordinal))
                        continue;
                    if (!String.Equals(existing.Car, item.Car, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!String.Equals(existing.Text, item.Text, StringComparison.Ordinal))
                        continue;
                    if (String.IsNullOrEmpty(item.Id))
                        item.Id = existing.Id;
                    rows[i] = ToRow(item);
                    found = true;
                    break;
                }
            }
            if (!found)
                rows.Add(ToRow(item));
        }

        internal static void DeleteById(List<string[]> rows, List<Tombstone> deleted, string id)
        {
            id = (id ?? "").Trim();
            if (!RecordIds.LooksLikeId(id) || rows == null)
                return;
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                if (!String.Equals(RecordIds.GetId(rows[i]), id, StringComparison.OrdinalIgnoreCase))
                    continue;
                rows.RemoveAt(i);
                if (deleted != null)
                {
                    string stamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ",
                        CultureInfo.InvariantCulture);
                    deleted.Add(new Tombstone(RecordIds.TypeNote, id, stamp));
                }
            }
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

        internal static string FormatRecentBlock(List<string[]> rows, string carName, int limit)
        {
            if (limit < 1)
                limit = 3;
            List<CarNote> list = ListForCar(rows, carName);
            StringBuilder sb = new StringBuilder();
            sb.Append(("\r\n\r\n" + L10n.Get("label.notes")));
            if (!String.IsNullOrEmpty(carName))
                sb.Append(" («").Append(carName.Trim()).Append("»)");
            sb.Append(":");
            if (list.Count == 0)
            {
                sb.Append(("\r\n" + L10n.Get("common.no_lower")));
                return sb.ToString();
            }
            int shown = list.Count < limit ? list.Count : limit;
            for (int i = 0; i < shown; i++)
                sb.Append("\r\n").Append(FormatLine(list[i]));
            if (list.Count > shown)
                sb.Append(("\r\n" + L10n.Get("summary.and_more") + " ")).Append(list.Count - shown);
            return sb.ToString();
        }
    }
}
