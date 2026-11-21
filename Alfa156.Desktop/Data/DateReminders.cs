using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Alfa156.Desktop.Data
{
    /// <summary>
    /// Date-based reminders in reminders.dat:
    /// date|car|title|type|id
    /// type = insurance|inspection|custom (also Russian: Страховка/Техосмотр/Своё).
    /// </summary>
    internal sealed class DateReminder
    {
        public string Date = "";
        public string Car = "";
        public string Title = "";
        public string Type = DateReminders.TypeCustom;
        public string Id;
    }

    internal static class DateReminders
    {
        internal const string FileName = "reminders.dat";
        internal const string TypeInsurance = "insurance";
        internal const string TypeInspection = "inspection";
        internal const string TypeCustom = "custom";
        private const string DateFormat = "yyyy-MM-dd";

        internal static string NormalizeType(string type)
        {
            string raw = (type ?? "").Trim();
            if (raw.Length == 0)
                return TypeCustom;
            string lower = raw.ToLowerInvariant();
            if (lower == TypeInsurance || lower == "страховка" || lower == "osago" ||
                lower == "осago" || lower == "осаго")
                return TypeInsurance;
            if (lower == TypeInspection || lower == "техосмотр" || lower == "то" ||
                lower == "inspection")
                return TypeInspection;
            if (lower == TypeCustom || lower == "своё" || lower == "свое" || lower == "другое" ||
                lower == "произвольное" || lower == "custom")
                return TypeCustom;
            return TypeCustom;
        }

        internal static bool IsValidType(string type)
        {
            string n = NormalizeType(type);
            return n == TypeInsurance || n == TypeInspection || n == TypeCustom;
        }

        internal static bool IsValidRow(string[] row)
        {
            if (row == null)
                return false;
            string[] content = RecordIds.StripId(row);
            if (content.Length != 4)
                return false;
            if (!IsValidDate(content[0]))
                return false;
            if ((content[1] ?? "").Trim().Length == 0)
                return false;
            if ((content[2] ?? "").Trim().Length == 0)
                return false;
            if (!IsValidType(content[3]))
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

        internal static DateReminder FromRow(string[] row)
        {
            DateReminder item = new DateReminder();
            if (row == null)
                return item;
            string[] content = RecordIds.StripId(row);
            if (content.Length < 4)
                return item;
            item.Date = (content[0] ?? "").Trim();
            item.Car = (content[1] ?? "").Trim();
            item.Title = (content[2] ?? "").Trim();
            item.Type = NormalizeType(content[3]);
            item.Id = RecordIds.GetId(row);
            return item;
        }

        internal static string[] ToRow(DateReminder item)
        {
            if (item == null)
                item = new DateReminder();
            string[] content = new string[]
            {
                (item.Date ?? "").Trim(),
                (item.Car ?? "").Trim(),
                (item.Title ?? "").Trim(),
                NormalizeType(item.Type)
            };
            if (!String.IsNullOrEmpty(item.Id) && RecordIds.LooksLikeId(item.Id))
                return RecordIds.WithId(content, item.Id);
            return RecordIds.EnsureId(content, RecordIds.PrefixForFile(FileName));
        }

        internal static List<DateReminder> FromRows(List<string[]> rows)
        {
            List<DateReminder> result = new List<DateReminder>();
            if (rows == null)
                return result;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!IsValidRow(rows[i]))
                    continue;
                result.Add(FromRow(rows[i]));
            }
            result.Sort(CompareByDate);
            return result;
        }

        private static int CompareByDate(DateReminder a, DateReminder b)
        {
            int cmp = String.CompareOrdinal(a.Date ?? "", b.Date ?? "");
            if (cmp != 0)
                return cmp;
            return String.Compare(a.Title ?? "", b.Title ?? "", StringComparison.OrdinalIgnoreCase);
        }

        internal static int DaysUntil(DateReminder item, DateTime today)
        {
            DateTime date;
            if (item == null || !TryParseDate(item.Date, out date))
                return Int32.MaxValue;
            return (date.Date - today.Date).Days;
        }

        internal static bool IsDueWithin(DateReminder item, int warnDays, DateTime today)
        {
            if (warnDays < 0)
                return false;
            return DaysUntil(item, today) <= warnDays;
        }

        internal static List<DateReminder> ListDueWithin(List<string[]> rows, int warnDays, DateTime today)
        {
            List<DateReminder> all = FromRows(rows);
            List<DateReminder> due = new List<DateReminder>();
            for (int i = 0; i < all.Count; i++)
                if (IsDueWithin(all[i], warnDays, today))
                    due.Add(all[i]);
            return due;
        }

        internal static string TypeLabelRu(string type)
        {
            type = NormalizeType(type);
            if (type == TypeInsurance) return "Страховка";
            if (type == TypeInspection) return "Техосмотр";
            return "Своё";
        }

        internal static string FormatLine(DateReminder item, DateTime today)
        {
            if (item == null)
                return "";
            int days = DaysUntil(item, today);
            string when;
            if (days < 0)
                when = "просрочено " + (-days) + " дн.";
            else if (days == 0)
                when = "сегодня";
            else
                when = "через " + days + " дн.";
            return item.Date + " · " + TypeLabelRu(item.Type) + " · " + item.Title +
                " (" + item.Car + ") — " + when;
        }

        internal static string FormatUpcomingBlock(List<string[]> rows, int warnDays, DateTime today)
        {
            List<DateReminder> due = ListDueWithin(rows, warnDays, today);
            StringBuilder sb = new StringBuilder();
            sb.Append("\r\n\r\nНапоминания (в ближайшие ").Append(warnDays).Append(" дн.):");
            if (due.Count == 0)
            {
                sb.Append("\r\nнет ближайших");
                return sb.ToString();
            }
            for (int i = 0; i < due.Count; i++)
                sb.Append("\r\n").Append(FormatLine(due[i], today));
            return sb.ToString();
        }

        internal static void Upsert(List<string[]> rows, List<Tombstone> deleted, DateReminder item)
        {
            if (rows == null)
                throw new ArgumentNullException("rows");
            if (item == null)
                throw new ArgumentNullException("item");
            item.Date = (item.Date ?? "").Trim();
            item.Car = (item.Car ?? "").Trim();
            item.Title = (item.Title ?? "").Trim();
            item.Type = NormalizeType(item.Type);
            if (!IsValidDate(item.Date))
                throw new InvalidOperationException("Дата: ГГГГ-ММ-ДД.");
            if (item.Car.Length == 0)
                throw new InvalidOperationException("Укажите автомобиль.");
            if (item.Title.Length == 0)
                throw new InvalidOperationException("Введите название напоминания.");
            if (item.Title.Length > 80)
                throw new InvalidOperationException("Название напоминания: не более 80 символов.");

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
                    DateReminder existing = FromRow(rows[i]);
                    if (!String.Equals(existing.Date, item.Date, StringComparison.Ordinal))
                        continue;
                    if (!String.Equals(existing.Car, item.Car, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!String.Equals(existing.Title, item.Title, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (existing.Type != item.Type)
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
                    deleted.Add(new Tombstone(RecordIds.TypeReminder, id, stamp));
                }
            }
        }
    }
}
