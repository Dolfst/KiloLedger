using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Alfa156.Localization;
namespace CarExpenses
{
    /// <summary>
    /// Groups rows that share a natural key or identical content but have different ids.
    /// Same id is never treated as a duplicate of itself.
    /// </summary>
    internal static class DuplicateFinder
    {
        internal sealed class Group
        {
            public string FileName;
            public string Title;
            public string Reason;
            public List<int> Indexes = new List<int>();
            public List<string[]> Rows = new List<string[]>();
        }

        internal static List<Group> FindAll()
        {
            List<Group> result = new List<Group>();
            Collect(result, "fuel.dat", L10n.Get("tab.fuel"),
                DataStore.LoadFuel(), new int[] { 0, 1, 2 }, new bool[] { false, true, false });
            Collect(result, "repairs.dat", L10n.Get("tab.repairs"),
                DataStore.LoadRepairs(), new int[] { 0, 1, 2, 3, 4 },
                new bool[] { false, true, false, true, false });
            Collect(result, "expenses.dat", L10n.Get("tab.expenses"),
                DataStore.LoadExpenses(), new int[] { 0, 1, 2, 3 },
                new bool[] { false, true, true, false });
            Collect(result, "utilities.dat", L10n.Get("tab.utilities"),
                DataStore.LoadUtilities(), new int[] { 0 }, new bool[] { false });
            return result;
        }

        private static void Collect(
            List<Group> result,
            string fileName,
            string title,
            List<string[]> rows,
            int[] keyColumns,
            bool[] ignoreCase)
        {
            if (rows == null || rows.Count < 2)
                return;
            RecordIds.EnsureIds(rows, fileName);

            Dictionary<string, List<int>> byKey = new Dictionary<string, List<int>>();
            for (int i = 0; i < rows.Count; i++)
            {
                string key = NaturalKey(rows[i], keyColumns, ignoreCase);
                if (key == null)
                    continue;
                List<int> list;
                if (!byKey.TryGetValue(key, out list))
                {
                    list = new List<int>();
                    byKey[key] = list;
                }
                list.Add(i);
            }
            foreach (KeyValuePair<string, List<int>> pair in byKey)
                MaybeAdd(result, fileName, title, L10n.Get("dup.same_key"), rows, pair.Value);

            Dictionary<string, List<int>> byContent = new Dictionary<string, List<int>>();
            for (int i = 0; i < rows.Count; i++)
            {
                string key = ContentKey(rows[i]);
                List<int> list;
                if (!byContent.TryGetValue(key, out list))
                {
                    list = new List<int>();
                    byContent[key] = list;
                }
                list.Add(i);
            }
            foreach (KeyValuePair<string, List<int>> pair in byContent)
                MaybeAdd(result, fileName, title, L10n.Get("dup.same_content"), rows, pair.Value);
        }

        private static void MaybeAdd(
            List<Group> result,
            string fileName,
            string title,
            string reason,
            List<string[]> rows,
            List<int> indexes)
        {
            if (indexes == null || indexes.Count < 2)
                return;
            Dictionary<string, bool> ids = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < indexes.Count; i++)
            {
                string id = RecordIds.GetId(rows[indexes[i]]);
                if (id == null || id.Length == 0)
                    id = "#idx:" + indexes[i].ToString();
                if (!ids.ContainsKey(id))
                    ids[id] = true;
            }
            if (ids.Count < 2)
                return;

            // Skip content groups that are exact duplicates of an already reported key group.
            if (reason == "одинаковое содержание" && AlreadyReported(result, fileName, indexes))
                return;

            Group group = new Group();
            group.FileName = fileName;
            group.Title = title;
            group.Reason = reason;
            for (int i = 0; i < indexes.Count; i++)
            {
                group.Indexes.Add(indexes[i]);
                group.Rows.Add(rows[indexes[i]]);
            }
            result.Add(group);
        }

        private static bool AlreadyReported(List<Group> result, string fileName, List<int> indexes)
        {
            for (int g = 0; g < result.Count; g++)
            {
                if (result[g].FileName != fileName)
                    continue;
                if (result[g].Indexes.Count != indexes.Count)
                    continue;
                bool same = true;
                for (int i = 0; i < indexes.Count; i++)
                {
                    if (result[g].Indexes[i] != indexes[i])
                    {
                        same = false;
                        break;
                    }
                }
                if (same)
                    return true;
            }
            return false;
        }

        private static string NaturalKey(string[] row, int[] columns, bool[] ignoreCase)
        {
            string[] content = RecordIds.StripId(row);
            if (content.Length <= columns[columns.Length - 1])
                return null;
            StringBuilder sb = new StringBuilder();
            for (int j = 0; j < columns.Length; j++)
            {
                if (j > 0)
                    sb.Append('\u001f');
                string value = content[columns[j]] == null ? "" : content[columns[j]];
                if (ignoreCase[j])
                    value = value.ToUpper(CultureInfo.InvariantCulture);
                sb.Append(value);
            }
            return sb.ToString();
        }

        private static string ContentKey(string[] row)
        {
            string[] content = RecordIds.StripId(row);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < content.Length; i++)
            {
                if (i > 0)
                    sb.Append('\u001f');
                sb.Append(content[i] == null ? "" : content[i]);
            }
            return sb.ToString();
        }

        internal static string FormatRow(string[] row)
        {
            string[] content = RecordIds.StripId(row);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < content.Length && i < 6; i++)
            {
                if (i > 0)
                    sb.Append(" | ");
                sb.Append(content[i] == null ? "" : content[i]);
            }
            string id = RecordIds.GetId(row);
            if (id != null)
            {
                sb.Append(" [");
                sb.Append(id);
                sb.Append("]");
            }
            return sb.ToString();
        }
    }
}
