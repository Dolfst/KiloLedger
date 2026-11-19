using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Alfa156.Desktop.Data
{
    /// <summary>
    /// Calendar reminders in reminders.dat:
    /// date|car|title|type|id
    /// type = insurance|inspection|custom
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

        internal static bool IsValidType(string type)
        {
            type = NormalizeType(type);
            return type == TypeInsurance || type == TypeInspection || type == TypeCustom;
        }

        internal static string NormalizeType(string type)
        {
            string raw = (type ?? "").Trim().ToLowerInvariant();
            if (raw == TypeInsurance || raw == "страховка" || raw == "osago" || raw == "casco")
                return TypeInsurance;
            if (raw == TypeInspection || raw == "техосмотр" || raw == "то" || raw == "inspection")
                return TypeInspection;
            if (raw == TypeCustom || raw == "своё" || raw == "свое" || raw == "другое" || raw == "custom")
                return TypeCustom;
            return raw;
        }

        internal static string TypeLabelRu(string type)
        {
            type = NormalizeType(type);
            if (type == TypeInsurance) return "Страховка";
            if (type == TypeInspection) return "Техосмотр";
            if (type == TypeCustom) return "Своё";
            return type;
        }

        internal static bool TryParseDate(string text, out DateTime date)
        {
            date = DateTime.MinValue;
            if (text == null)
                return false;
            string trimmed = text.Trim();
            if (trimmed.Length != 10 || trimmed[4] != '-' || trimmed[7] != '-')
                return false;
            try
            {
                int year = int.Parse(trimmed.Substring(0, 4), NumberStyles.None, CultureInfo.InvariantCulture);
                int month = int.Parse(trimmed.Substring(5, 2), NumberStyles.None, CultureInfo.InvariantCulture);
                int day = int.Parse(trimmed.Substring(8, 2), NumberStyles.None, CultureInfo.InvariantCulture);
                date = new DateTime(year, month, day);
                return true;
            }
            catch (FormatException) { return false; }
            catch (OverflowException) { return false; }
            catch (ArgumentOutOfRangeException) { return false; }
        }

        internal static string FormatDate(DateTime date)
        {
            return date.ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        internal static bool IsValidRow(string[] row)
        {
            if (row == null)
                return false;
            string[] content = RecordIds.StripId(row);
            if (content.Length != 4)
                return false;
            DateTime dt;
            if (!TryParseDate(content[0], out dt))
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

        internal static List<DateReminder> ListAll(List<string[]> rows)
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

        internal static List<DateReminder> ListDue(List<string[]> rows, int warnDays, DateTime today)
        {
            DateTime day = today.Date;
            List<DateReminder> all = ListAll(rows);
            List<DateReminder> due = new List<DateReminder>();
            for (int i = 0; i < all.Count; i++)
            {
                DateTime when;
                if (!TryParseDate(all[i].Date, out when))
                    continue;
                int daysLeft = (when.Date - day).Days;
                if (daysLeft <= warnDays)
                    due.Add(all[i]);
            }
            return due;
        }

        internal static string FormatLine(DateReminder item, DateTime today)
        {
            if (item == null)
                return "";
            DateTime when;
            string whenText = item.Date;
            string status = "";
            if (TryParseDate(item.Date, out when))
            {
                whenText = FormatDate(when);
                int days = (when.Date - today.Date).Days;
                if (days < 0)
                    status = " · просрочено " + (-days) + " дн.";
                else if (days == 0)
                    status = " · сегодня";
                else
                    status = " · через " + days + " дн.";
            }
            return whenText + " · " + item.Car + " · " + TypeLabelRu(item.Type) +
                " · " + item.Title + status;
        }

        internal static string FormatUpcomingBlock(List<string[]> rows, int warnDays, string newline)
        {
            if (warnDays < 0)
                warnDays = 0;
            List<DateReminder> due = ListDue(rows, warnDays, DateTime.Today);
            StringBuilder sb = new StringBuilder();
            sb.Append(newline).Append(newline).Append("Напоминания (≤").Append(warnDays).Append(" дн.):");
            if (due.Count == 0)
            {
                sb.Append(newline).Append("нет ближайших");
                return sb.ToString();
            }
            for (int i = 0; i < due.Count; i++)
                sb.Append(newline).Append(FormatLine(due[i], DateTime.Today));
            return sb.ToString();
        }

        private static int CompareByDate(DateReminder a, DateReminder b)
        {
            int cmp = String.CompareOrdinal(a.Date ?? "", b.Date ?? "");
            if (cmp != 0)
                return cmp;
            cmp = String.Compare(a.Car ?? "", b.Car ?? "", StringComparison.OrdinalIgnoreCase);
            if (cmp != 0)
                return cmp;
            return String.Compare(a.Title ?? "", b.Title ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }
}
