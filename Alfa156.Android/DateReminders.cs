using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Alfa156.Android;

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

    internal static void EnsureFile(string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
            ExpenseDataCodec.SaveRows(path, new List<string[]>());
    }

    internal static List<string[]> LoadRows(string directory)
    {
        string path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
            return new List<string[]>();
        return ExpenseDataCodec.ReadRows(path);
    }

    internal static void SaveRows(string directory, List<string[]> rows)
    {
        Directory.CreateDirectory(directory);
        ExpenseDataCodec.SaveRows(Path.Combine(directory, FileName), rows ?? new List<string[]>());
    }

    internal static string NormalizeType(string type)
    {
        string raw = (type ?? "").Trim();
        if (raw.Length == 0)
            return TypeCustom;
        string lower = raw.ToLowerInvariant();
        if (lower == TypeInsurance || lower == "страховка" || lower == "osago" ||
            lower == "осago" || lower == "осаго")
            return TypeInsurance;
        if (lower == TypeInspection || lower == "техосмотр" || lower == "то" || lower == "inspection")
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
        if (content.Length < 4 || content.Length > 4)
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

    internal static List<DateReminder> ListAll(string directory)
    {
        List<DateReminder> result = new List<DateReminder>();
        List<string[]> rows = LoadRows(directory);
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

    internal static void Upsert(string directory, DateReminder item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
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

        List<string[]> rows = LoadRows(directory);
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
        SaveRows(directory, rows);
    }

    internal static void DeleteById(string directory, string id)
    {
        id = (id ?? "").Trim();
        if (!RecordIds.LooksLikeId(id))
            return;
        List<string[]> rows = LoadRows(directory);
        List<string[]> kept = new List<string[]>();
        for (int i = 0; i < rows.Count; i++)
        {
            string rowId = RecordIds.GetId(rows[i]);
            if (String.Equals(rowId, id, StringComparison.OrdinalIgnoreCase))
            {
                RecordIds.AddTombstone(directory, FileName, id);
                continue;
            }
            kept.Add(rows[i]);
        }
        SaveRows(directory, kept);
    }

    /// <summary>Days until due date; negative if overdue. Null date → Int32.MaxValue.</summary>
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
        int days = DaysUntil(item, today);
        return days <= warnDays;
    }

    internal static List<DateReminder> ListDueWithin(string directory, int warnDays, DateTime today)
    {
        List<DateReminder> all = ListAll(directory);
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

    internal static string FormatUpcomingBlock(string directory, int warnDays, DateTime today, string nl)
    {
        if (nl == null)
            nl = "\n";
        List<DateReminder> due = ListDueWithin(directory, warnDays, today);
        StringBuilder sb = new StringBuilder();
        sb.Append(nl).Append(nl).Append("Напоминания (в ближайшие ")
            .Append(warnDays).Append(" дн.):");
        if (due.Count == 0)
        {
            sb.Append(nl).Append("нет ближайших");
            return sb.ToString();
        }
        for (int i = 0; i < due.Count; i++)
            sb.Append(nl).Append(FormatLine(due[i], today));
        return sb.ToString();
    }

    internal static string FormatAllBlock(string directory, DateTime today, string nl)
    {
        if (nl == null)
            nl = "\n";
        List<DateReminder> all = ListAll(directory);
        StringBuilder sb = new StringBuilder();
        sb.Append("Напоминания:");
        if (all.Count == 0)
        {
            sb.Append(nl).Append("пока нет");
            return sb.ToString();
        }
        for (int i = 0; i < all.Count; i++)
            sb.Append(nl).Append((i + 1).ToString(CultureInfo.InvariantCulture))
                .Append(". ").Append(FormatLine(all[i], today));
        return sb.ToString();
    }
}
