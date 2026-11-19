using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Alfa156.Android;

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

    internal static List<CarNote> ListAll(string directory)
    {
        List<CarNote> result = new List<CarNote>();
        List<string[]> rows = LoadRows(directory);
        for (int i = 0; i < rows.Count; i++)
        {
            if (!IsValidRow(rows[i]))
                continue;
            result.Add(FromRow(rows[i]));
        }
        result.Sort(CompareNewestFirst);
        return result;
    }

    internal static List<CarNote> ListForCar(string directory, string carName)
    {
        carName = (carName ?? "").Trim();
        List<CarNote> all = ListAll(directory);
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

    internal static void Upsert(string directory, CarNote item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        item.Date = (item.Date ?? "").Trim();
        item.Car = (item.Car ?? "").Trim();
        item.Text = (item.Text ?? "").Trim();
        if (!IsValidDate(item.Date))
            throw new InvalidOperationException("Дата: ГГГГ-ММ-ДД.");
        if (item.Car.Length == 0)
            throw new InvalidOperationException("Укажите автомобиль.");
        if (item.Text.Length == 0)
            throw new InvalidOperationException("Введите текст заметки.");
        if (item.Text.Length > MaxTextLength)
            item.Text = item.Text.Substring(0, MaxTextLength).Trim();

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

    internal static string FormatRecentBlock(string directory, string carName, int limit, string nl)
    {
        if (nl == null)
            nl = "\n";
        if (limit < 1)
            limit = 3;
        List<CarNote> list = ListForCar(directory, carName);
        StringBuilder sb = new StringBuilder();
        sb.Append(nl).Append(nl).Append("Заметки");
        if (!String.IsNullOrEmpty(carName))
            sb.Append(" («").Append(carName.Trim()).Append("»)");
        sb.Append(":");
        if (list.Count == 0)
        {
            sb.Append(nl).Append("нет");
            return sb.ToString();
        }
        int shown = list.Count < limit ? list.Count : limit;
        for (int i = 0; i < shown; i++)
            sb.Append(nl).Append(FormatLine(list[i]));
        if (list.Count > shown)
            sb.Append(nl).Append("… ещё ").Append(list.Count - shown);
        return sb.ToString();
    }

    internal static string FormatAllBlock(string directory, string carName, string nl)
    {
        if (nl == null)
            nl = "\n";
        List<CarNote> list = ListForCar(directory, carName);
        StringBuilder sb = new StringBuilder();
        sb.Append("Заметки");
        if (!String.IsNullOrEmpty(carName))
            sb.Append(" · ").Append(carName.Trim());
        sb.Append(":");
        if (list.Count == 0)
        {
            sb.Append(nl).Append("пока нет");
            return sb.ToString();
        }
        for (int i = 0; i < list.Count; i++)
            sb.Append(nl).Append((i + 1).ToString(CultureInfo.InvariantCulture))
                .Append(". ").Append(FormatLine(list[i]));
        return sb.ToString();
    }
}
