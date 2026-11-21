using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CarExpenses
{
    /// <summary>
    /// Date-based reminders in reminders.dat:
    /// date|car|title|type|id
    /// type = insurance|inspection|custom (also Russian: Страховка/Техосмотр/Своё).
    /// CF 3.5 safe.
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

        internal static string NormalizeType(string type)
        {
            if (type == null)
                return TypeCustom;
            string raw = type.Trim();
            if (raw.Length == 0)
                return TypeCustom;
            string lower = raw.ToLower(CultureInfo.InvariantCulture);
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
            if (item.Id != null && item.Id.Length > 0 && RecordIds.LooksLikeId(item.Id))
                return RecordIds.WithId(content, item.Id);
            return RecordIds.EnsureId(content, RecordIds.PrefixForFile(FileName));
        }

        internal static List<DateReminder> ListAll()
        {
            return ListAllFrom(DataDirectory);
        }

        internal static List<DateReminder> ListAllFrom(string directory)
        {
            List<DateReminder> result = new List<DateReminder>();
            List<string[]> rows = LoadRowsFrom(directory);
            for (int i = 0; i < rows.Count; i++)
            {
                if (!IsValidRow(rows[i]))
                    continue;
                result.Add(FromRow(rows[i]));
            }
            result.Sort(new DateReminderComparer());
            return result;
        }

        private sealed class DateReminderComparer : IComparer<DateReminder>
        {
            public int Compare(DateReminder a, DateReminder b)
            {
                int cmp = String.CompareOrdinal(a.Date ?? "", b.Date ?? "");
                if (cmp != 0)
                    return cmp;
                return String.Compare(a.Title ?? "", b.Title ?? "",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        internal static void Upsert(DateReminder item)
        {
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
                item.Title = item.Title.Substring(0, 80).Trim();

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
                    DateReminder existing = FromRow(rows[i]);
                    if (String.CompareOrdinal(existing.Date, item.Date) != 0)
                        continue;
                    if (String.Compare(existing.Car, item.Car, StringComparison.OrdinalIgnoreCase) != 0)
                        continue;
                    if (String.Compare(existing.Title, item.Title, StringComparison.OrdinalIgnoreCase) != 0)
                        continue;
                    if (existing.Type != item.Type)
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

        internal static List<DateReminder> ListDueWithin(int warnDays, DateTime today)
        {
            List<DateReminder> all = ListAll();
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
                when = "просрочено " + (-days).ToString(CultureInfo.InvariantCulture) + " дн.";
            else if (days == 0)
                when = "сегодня";
            else
                when = "через " + days.ToString(CultureInfo.InvariantCulture) + " дн.";
            return item.Date + " · " + TypeLabelRu(item.Type) + " · " + item.Title +
                " (" + item.Car + ") — " + when;
        }

        internal static string FormatUpcomingBlock(int warnDays, DateTime today)
        {
            List<DateReminder> due = ListDueWithin(warnDays, today);
            StringBuilder sb = new StringBuilder();
            sb.Append("\r\n\r\nНапоминания (в ближайшие ");
            sb.Append(warnDays.ToString(CultureInfo.InvariantCulture));
            sb.Append(" дн.):");
            if (due.Count == 0)
            {
                sb.Append("\r\nнет ближайших");
                return sb.ToString();
            }
            for (int i = 0; i < due.Count; i++)
            {
                sb.Append("\r\n");
                sb.Append(FormatLine(due[i], today));
            }
            return sb.ToString();
        }

        internal static string FormatAllBlock(DateTime today)
        {
            List<DateReminder> all = ListAll();
            StringBuilder sb = new StringBuilder();
            sb.Append("Напоминания:");
            if (all.Count == 0)
            {
                sb.Append("\r\nпока нет");
                return sb.ToString();
            }
            for (int i = 0; i < all.Count; i++)
            {
                sb.Append("\r\n");
                sb.Append((i + 1).ToString(CultureInfo.InvariantCulture));
                sb.Append(". ");
                sb.Append(FormatLine(all[i], today));
            }
            return sb.ToString();
        }
    }
}
