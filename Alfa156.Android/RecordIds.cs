using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Alfa156.Android;

internal static class RecordIds
{
    internal const int TombstoneCap = 5000;
    internal const string DeletedFile = "deleted.dat";

    internal const string TypeCar = "car";
    internal const string TypeFuel = "fuel";
    internal const string TypeRepair = "repair";
    internal const string TypeUtility = "utility";
    internal const string TypeCategory = "category";
    internal const string TypeExpense = "expense";
    internal const string TypeExpenseCategory = "expense-category";
    internal const string TypeCarMeta = "car-meta";
    internal const string TypeTemplate = "template";
    internal const string TypeReminder = "reminder";

    internal static string PrefixForFile(string fileName)
    {
        if (fileName == "cars.dat") return "c_";
        if (fileName == "fuel.dat") return "f_";
        if (fileName == "repairs.dat") return "r_";
        if (fileName == "utilities.dat") return "u_";
        if (fileName == ExpenseDataCodec.RepairCategoriesFile) return "k_";
        if (fileName == ExpenseDataCodec.ExpensesFile) return "e_";
        if (fileName == ExpenseDataCodec.ExpenseCategoriesFile) return "p_";
        if (fileName == ExpenseDataCodec.CarsMetaFile) return "m_";
        if (fileName == ExpenseDataCodec.TemplatesFile) return "t_";
        if (fileName == ExpenseDataCodec.RemindersFile) return "n_";
        return "x_";
    }

    internal static string EntityTypeForFile(string fileName)
    {
        if (fileName == "cars.dat") return TypeCar;
        if (fileName == "fuel.dat") return TypeFuel;
        if (fileName == "repairs.dat") return TypeRepair;
        if (fileName == "utilities.dat") return TypeUtility;
        if (fileName == ExpenseDataCodec.RepairCategoriesFile) return TypeCategory;
        if (fileName == ExpenseDataCodec.ExpensesFile) return TypeExpense;
        if (fileName == ExpenseDataCodec.ExpenseCategoriesFile) return TypeExpenseCategory;
        if (fileName == ExpenseDataCodec.CarsMetaFile) return TypeCarMeta;
        if (fileName == ExpenseDataCodec.TemplatesFile) return TypeTemplate;
        if (fileName == ExpenseDataCodec.RemindersFile) return TypeReminder;
        return "unknown";
    }

    internal static bool LooksLikeId(string value)
    {
        if (String.IsNullOrEmpty(value) || value.Length < 10)
            return false;
        char prefix = value[0];
        if ((prefix != 'c' && prefix != 'f' && prefix != 'r' && prefix != 'u' && prefix != 'k' &&
             prefix != 'e' && prefix != 'p' && prefix != 'm' && prefix != 't' && prefix != 'n' &&
             prefix != 'x') ||
            value[1] != '_')
            return false;
        for (int i = 2; i < value.Length; i++)
        {
            char c = value[i];
            bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!hex)
                return false;
        }
        return true;
    }

    internal static bool HasId(string[] row)
    {
        return row != null && row.Length > 0 && LooksLikeId(row[row.Length - 1]);
    }

    internal static string GetId(string[] row)
    {
        return HasId(row) ? row[row.Length - 1] : null;
    }

    internal static string[] StripId(string[] row)
    {
        if (!HasId(row))
            return row;
        string[] result = new string[row.Length - 1];
        Array.Copy(row, result, result.Length);
        return result;
    }

    internal static string[] WithId(string[] row, string id)
    {
        string[] content = StripId(row);
        string[] result = new string[content.Length + 1];
        Array.Copy(content, result, content.Length);
        result[content.Length] = id;
        return result;
    }

    internal static string NewId(string prefix)
    {
        return prefix + Guid.NewGuid().ToString("N").Substring(0, 8);
    }

    internal static string[] EnsureId(string[] row, string prefix)
    {
        if (HasId(row))
            return row;
        return WithId(row ?? Array.Empty<string>(), NewId(prefix));
    }

    internal static void EnsureIds(List<string[]> rows, string fileName)
    {
        string prefix = PrefixForFile(fileName);
        for (int i = 0; i < rows.Count; i++)
            rows[i] = EnsureId(rows[i], prefix);
    }

    internal static List<Tombstone> ReadTombstones(string directory)
    {
        List<Tombstone> list = new List<Tombstone>();
        string path = Path.Combine(directory, DeletedFile);
        if (!File.Exists(path))
            return list;
        List<string[]> rows = ExpenseDataCodec.ReadRows(path);
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Length < 2)
                continue;
            string type = (rows[i][0] ?? "").Trim();
            string id = (rows[i][1] ?? "").Trim();
            if (type.Length == 0 || !LooksLikeId(id))
                continue;
            string stamp = rows[i].Length > 2 ? (rows[i][2] ?? "") : "";
            list.Add(new Tombstone(type, id, stamp));
        }
        return list;
    }

    internal static void WriteTombstones(string directory, List<Tombstone> tombstones)
    {
        Directory.CreateDirectory(directory);
        List<Tombstone> capped = CapTombstones(tombstones);
        List<string[]> rows = new List<string[]>();
        for (int i = 0; i < capped.Count; i++)
            rows.Add(new string[] { capped[i].EntityType, capped[i].Id, capped[i].Timestamp ?? "" });
        ExpenseDataCodec.SaveRows(Path.Combine(directory, DeletedFile), rows);
    }

    internal static void EnsureDeletedFile(string directory)
    {
        string path = Path.Combine(directory, DeletedFile);
        if (!File.Exists(path))
            ExpenseDataCodec.SaveRows(path, new List<string[]>());
    }

    internal static List<Tombstone> CapTombstones(List<Tombstone> tombstones)
    {
        if (tombstones == null || tombstones.Count <= TombstoneCap)
            return tombstones ?? new List<Tombstone>();
        // Keep newest by timestamp when present; otherwise keep the last N.
        List<Tombstone> copy = new List<Tombstone>(tombstones);
        copy.Sort(CompareTombstones);
        int skip = copy.Count - TombstoneCap;
        return copy.GetRange(skip, TombstoneCap);
    }

    private static int CompareTombstones(Tombstone a, Tombstone b)
    {
        return String.CompareOrdinal(a.Timestamp ?? "", b.Timestamp ?? "");
    }

    internal static List<Tombstone> UnionTombstones(List<Tombstone> left, List<Tombstone> right)
    {
        Dictionary<string, Tombstone> map = new Dictionary<string, Tombstone>(StringComparer.OrdinalIgnoreCase);
        AddAll(map, left);
        AddAll(map, right);
        List<Tombstone> result = new List<Tombstone>();
        foreach (KeyValuePair<string, Tombstone> pair in map)
            result.Add(pair.Value);
        return CapTombstones(result);
    }

    private static void AddAll(Dictionary<string, Tombstone> map, List<Tombstone> list)
    {
        if (list == null)
            return;
        for (int i = 0; i < list.Count; i++)
        {
            Tombstone t = list[i];
            string key = t.EntityType + "|" + t.Id;
            Tombstone existing;
            if (!map.TryGetValue(key, out existing))
            {
                map[key] = t;
                continue;
            }
            if (String.CompareOrdinal(t.Timestamp ?? "", existing.Timestamp ?? "") > 0)
                map[key] = t;
        }
    }

    internal static void AddTombstone(string directory, string fileName, string id)
    {
        if (String.IsNullOrEmpty(id) || !LooksLikeId(id))
            return;
        List<Tombstone> list = ReadTombstones(directory);
        string type = EntityTypeForFile(fileName);
        string stamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        string keyType = type;
        for (int i = list.Count - 1; i >= 0; i--)
            if (list[i].EntityType == keyType &&
                String.Equals(list[i].Id, id, StringComparison.OrdinalIgnoreCase))
                list.RemoveAt(i);
        list.Add(new Tombstone(type, id, stamp));
        WriteTombstones(directory, list);
    }

    internal static HashSet<string> IdsForType(List<Tombstone> tombstones, string entityType)
    {
        HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (tombstones == null)
            return set;
        for (int i = 0; i < tombstones.Count; i++)
            if (tombstones[i].EntityType == entityType)
                set.Add(tombstones[i].Id);
        return set;
    }

    internal static bool ContentEqual(string[] first, string[] second)
    {
        string[] a = StripId(first);
        string[] b = StripId(second);
        int max = Math.Max(a.Length, b.Length);
        for (int i = 0; i < max; i++)
        {
            string left = i < a.Length ? (a[i] ?? "") : "";
            string right = i < b.Length ? (b[i] ?? "") : "";
            if (!String.Equals(left, right, StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    internal static string[] PreferId(string[] preferred, string[] other)
    {
        string id = GetId(preferred) ?? GetId(other);
        if (id == null)
            return preferred;
        if (HasId(preferred) && GetId(preferred) == id)
            return preferred;
        return WithId(StripId(preferred), id);
    }
}

internal sealed class Tombstone
{
    public string EntityType;
    public string Id;
    public string Timestamp;

    public Tombstone(string entityType, string id, string timestamp)
    {
        EntityType = entityType;
        Id = id;
        Timestamp = timestamp;
    }
}

internal sealed class SyncJournal
{
    public const int MaxExamples = 15;

    public int Added;
    public int Removed;
    public int Conflicts;
    public int KeptLocalOnly;
    public readonly List<string> Examples = new List<string>();
    public string SafetyPath;

    public void AddExample(string text)
    {
        if (Examples.Count >= MaxExamples || String.IsNullOrEmpty(text))
            return;
        Examples.Add(text);
    }

    public string FormatSummary()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("Итог синхронизации:\n");
        sb.Append("  добавлено: ").Append(Added).Append('\n');
        sb.Append("  удалено (tombstone): ").Append(Removed).Append('\n');
        sb.Append("  конфликтов: ").Append(Conflicts).Append('\n');
        sb.Append("  только локально: ").Append(KeptLocalOnly);
        AppendExamples(sb);
        return sb.ToString();
    }

    public string FormatPreview()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("Предпросмотр слияния:\n");
        sb.Append("  только локально: ").Append(KeptLocalOnly).Append('\n');
        sb.Append("  только удалённо: ").Append(Added).Append('\n');
        sb.Append("  обновления/конфликты: ").Append(Conflicts).Append('\n');
        sb.Append("  удаления (tombstone): ").Append(Removed);
        AppendExamples(sb);
        if (!String.IsNullOrEmpty(SafetyPath))
            sb.Append("\n\nПеред применением: ").Append(SafetyPath);
        return sb.ToString();
    }

    private void AppendExamples(StringBuilder sb)
    {
        if (Examples.Count == 0)
            return;
        sb.Append("\n\nПримеры:");
        for (int i = 0; i < Examples.Count; i++)
            sb.Append("\n  • ").Append(Examples[i]);
    }
}
