using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CarExpenses
{
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
        internal const string TypeNote = "note";

        internal static string PrefixForFile(string fileName)
        {
            if (fileName == "cars.dat") return "c_";
            if (fileName == "fuel.dat") return "f_";
            if (fileName == "repairs.dat") return "r_";
            if (fileName == "utilities.dat") return "u_";
            if (fileName == "repair-categories.dat") return "k_";
            if (fileName == "expenses.dat") return "e_";
            if (fileName == "expense-categories.dat") return "p_";
            if (fileName == "cars-meta.dat") return "m_";
            if (fileName == "templates.dat") return "t_";
            if (fileName == "reminders.dat") return "n_";
            if (fileName == "notes.dat") return "j_";
            return "x_";
        }

        internal static string EntityTypeForFile(string fileName)
        {
            if (fileName == "cars.dat") return TypeCar;
            if (fileName == "fuel.dat") return TypeFuel;
            if (fileName == "repairs.dat") return TypeRepair;
            if (fileName == "utilities.dat") return TypeUtility;
            if (fileName == "repair-categories.dat") return TypeCategory;
            if (fileName == "expenses.dat") return TypeExpense;
            if (fileName == "expense-categories.dat") return TypeExpenseCategory;
            if (fileName == "cars-meta.dat") return TypeCarMeta;
            if (fileName == "templates.dat") return TypeTemplate;
            if (fileName == "reminders.dat") return TypeReminder;
            if (fileName == "notes.dat") return TypeNote;
            return "unknown";
        }

        internal static bool LooksLikeId(string value)
        {
            if (value == null || value.Length < 10)
                return false;
            char prefix = value[0];
            if ((prefix != 'c' && prefix != 'f' && prefix != 'r' && prefix != 'u' && prefix != 'k' &&
                 prefix != 'e' && prefix != 'p' && prefix != 'm' && prefix != 't' && prefix != 'n' &&
                 prefix != 'j' && prefix != 'x') ||
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
            string hex = Guid.NewGuid().ToString("N");
            return prefix + hex.Substring(0, 8);
        }

        internal static string[] EnsureId(string[] row, string prefix)
        {
            if (HasId(row))
                return row;
            if (row == null)
                row = new string[0];
            return WithId(row, NewId(prefix));
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
            using (StreamReader reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0)
                        continue;
                    string[] parts = SplitSimple(line);
                    if (parts.Length < 2)
                        continue;
                    string type = parts[0] == null ? "" : parts[0].Trim();
                    string id = parts[1] == null ? "" : parts[1].Trim();
                    if (type.Length == 0 || !LooksLikeId(id))
                        continue;
                    string stamp = parts.Length > 2 && parts[2] != null ? parts[2] : "";
                    list.Add(new Tombstone(type, id, stamp));
                }
            }
            return list;
        }

        internal static void WriteTombstones(string directory, List<Tombstone> tombstones)
        {
            Directory.CreateDirectory(directory);
            List<Tombstone> capped = CapTombstones(tombstones);
            using (StreamWriter writer = new StreamWriter(Path.Combine(directory, DeletedFile), false))
            {
                for (int i = 0; i < capped.Count; i++)
                {
                    writer.Write(capped[i].EntityType);
                    writer.Write('|');
                    writer.Write(capped[i].Id);
                    writer.Write('|');
                    writer.Write(capped[i].Timestamp == null ? "" : capped[i].Timestamp);
                    writer.WriteLine();
                }
            }
        }

        internal static List<Tombstone> CapTombstones(List<Tombstone> tombstones)
        {
            if (tombstones == null || tombstones.Count <= TombstoneCap)
                return tombstones == null ? new List<Tombstone>() : tombstones;
            List<Tombstone> copy = new List<Tombstone>(tombstones);
            for (int i = 0; i < copy.Count; i++)
                for (int j = i + 1; j < copy.Count; j++)
                    if (String.CompareOrdinal(copy[i].Timestamp == null ? "" : copy[i].Timestamp,
                        copy[j].Timestamp == null ? "" : copy[j].Timestamp) > 0)
                    {
                        Tombstone tmp = copy[i];
                        copy[i] = copy[j];
                        copy[j] = tmp;
                    }
            int skip = copy.Count - TombstoneCap;
            List<Tombstone> result = new List<Tombstone>();
            for (int i = skip; i < copy.Count; i++)
                result.Add(copy[i]);
            return result;
        }

        internal static List<Tombstone> UnionTombstones(List<Tombstone> left, List<Tombstone> right)
        {
            Dictionary<string, Tombstone> map = new Dictionary<string, Tombstone>(
                StringComparer.OrdinalIgnoreCase);
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
                if (!map.ContainsKey(key))
                {
                    map[key] = t;
                    continue;
                }
                Tombstone existing = map[key];
                string ts = t.Timestamp == null ? "" : t.Timestamp;
                string es = existing.Timestamp == null ? "" : existing.Timestamp;
                if (String.CompareOrdinal(ts, es) > 0)
                    map[key] = t;
            }
        }

        internal static void AddTombstone(string directory, string fileName, string id)
        {
            if (id == null || id.Length == 0 || !LooksLikeId(id))
                return;
            List<Tombstone> list = ReadTombstones(directory);
            string type = EntityTypeForFile(fileName);
            string stamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].EntityType == type &&
                    String.Compare(list[i].Id, id, true, CultureInfo.InvariantCulture) == 0)
                    list.RemoveAt(i);
            list.Add(new Tombstone(type, id, stamp));
            WriteTombstones(directory, list);
        }

        internal static Dictionary<string, bool> IdsForType(List<Tombstone> tombstones, string entityType)
        {
            Dictionary<string, bool> set = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (tombstones == null)
                return set;
            for (int i = 0; i < tombstones.Count; i++)
                if (tombstones[i].EntityType == entityType && !set.ContainsKey(tombstones[i].Id))
                    set.Add(tombstones[i].Id, true);
            return set;
        }

        internal static bool ContentEqual(string[] first, string[] second)
        {
            string[] a = StripId(first);
            string[] b = StripId(second);
            int max = a.Length > b.Length ? a.Length : b.Length;
            for (int i = 0; i < max; i++)
            {
                string left = i < a.Length ? (a[i] != null ? a[i] : "") : "";
                string right = i < b.Length ? (b[i] != null ? b[i] : "") : "";
                if (left != right)
                    return false;
            }
            return true;
        }

        internal static string[] PreferId(string[] preferred, string[] other)
        {
            string id = GetId(preferred);
            if (id == null)
                id = GetId(other);
            if (id == null)
                return preferred;
            if (HasId(preferred) && GetId(preferred) == id)
                return preferred;
            return WithId(StripId(preferred), id);
        }

        private static string[] SplitSimple(string line)
        {
            List<string> fields = new List<string>();
            StringBuilder field = new StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '|')
                {
                    fields.Add(field.ToString());
                    field.Length = 0;
                }
                else
                    field.Append(line[i]);
            }
            fields.Add(field.ToString());
            return fields.ToArray();
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
            if (Examples.Count >= MaxExamples || text == null || text.Length == 0)
                return;
            Examples.Add(text);
        }

        public string FormatSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Итог синхронизации:\r\n");
            sb.Append("  добавлено: ");
            sb.Append(Added.ToString());
            sb.Append("\r\n  удалено (tombstone): ");
            sb.Append(Removed.ToString());
            sb.Append("\r\n  конфликтов: ");
            sb.Append(Conflicts.ToString());
            sb.Append("\r\n  только локально: ");
            sb.Append(KeptLocalOnly.ToString());
            AppendExamples(sb, "\r\n");
            return sb.ToString();
        }

        public string FormatPreview()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Предпросмотр слияния:\r\n");
            sb.Append("  только локально: ");
            sb.Append(KeptLocalOnly.ToString());
            sb.Append("\r\n  только удалённо: ");
            sb.Append(Added.ToString());
            sb.Append("\r\n  обновления/конфликты: ");
            sb.Append(Conflicts.ToString());
            sb.Append("\r\n  удаления (tombstone): ");
            sb.Append(Removed.ToString());
            AppendExamples(sb, "\r\n");
            if (SafetyPath != null && SafetyPath.Length > 0)
            {
                sb.Append("\r\n\r\nПеред применением: ");
                sb.Append(SafetyPath);
            }
            return sb.ToString();
        }

        private void AppendExamples(StringBuilder sb, string nl)
        {
            if (Examples.Count == 0)
                return;
            sb.Append(nl);
            sb.Append(nl);
            sb.Append("Примеры:");
            for (int i = 0; i < Examples.Count; i++)
            {
                sb.Append(nl);
                sb.Append("  • ");
                sb.Append(Examples[i]);
            }
        }
    }
}
