using System;
using System.Collections.Generic;

namespace Alfa156.Desktop.Data
{
    internal enum SyncConflictPolicy
    {
        PreferIncoming,
        PreferLocal,
        MergeFields
    }

    internal static class SyncMerge
    {
        internal static void MergeInto(
            AlfaRepository local,
            AlfaRepository incoming,
            SyncConflictPolicy policy)
        {
            local.ReplaceAll(
                MergeList(local.Cars, incoming.Cars, new int[] { 0 }, new bool[] { true }, policy),
                MergeList(local.Fuel, incoming.Fuel, new int[] { 0, 1, 2 }, new bool[] { false, true, false }, policy),
                MergeList(local.Repairs, incoming.Repairs, new int[] { 0, 1, 2, 3, 4 },
                    new bool[] { false, true, false, true, false }, policy),
                MergeList(local.Utilities, incoming.Utilities, new int[] { 0 }, new bool[] { false }, policy),
                MergeList(local.Categories, incoming.Categories, new int[] { 0 }, new bool[] { true }, policy));
        }

        private static List<string[]> MergeList(
            List<string[]> localRows,
            List<string[]> incomingRows,
            int[] keyColumns,
            bool[] ignoreCase,
            SyncConflictPolicy policy)
        {
            List<string[]> merged = new List<string[]>();
            for (int i = 0; i < localRows.Count; i++)
                merged.Add((string[])localRows[i].Clone());

            for (int i = 0; i < incomingRows.Count; i++)
            {
                string[] row = incomingRows[i];
                int match = FindIdentity(merged, row, keyColumns, ignoreCase);
                if (match < 0)
                {
                    merged.Add((string[])row.Clone());
                    continue;
                }
                if (RowsEqual(merged[match], row))
                    continue;

                if (policy == SyncConflictPolicy.PreferIncoming)
                    merged[match] = (string[])row.Clone();
                else if (policy == SyncConflictPolicy.PreferLocal)
                {
                    // leave local
                }
                else
                    merged[match] = MergeFields(merged[match], row);
            }
            return merged;
        }

        private static string[] MergeFields(string[] local, string[] incoming)
        {
            int length = Math.Max(local.Length, incoming.Length);
            string[] result = new string[length];
            for (int i = 0; i < length; i++)
            {
                string left = i < local.Length ? (local[i] ?? "") : "";
                string right = i < incoming.Length ? (incoming[i] ?? "") : "";
                if (left.Trim().Length == 0)
                    result[i] = right;
                else if (right.Trim().Length == 0)
                    result[i] = left;
                else if (String.Equals(left, right, StringComparison.Ordinal))
                    result[i] = left;
                else
                    result[i] = right; // при расхождении берём входящее (Google/другой узел)
            }
            return result;
        }

        private static int FindIdentity(List<string[]> rows, string[] candidate, int[] columns, bool[] ignoreCase)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Length <= columns[columns.Length - 1] || candidate.Length <= columns[columns.Length - 1])
                    continue;
                bool matches = true;
                for (int j = 0; j < columns.Length; j++)
                {
                    StringComparison comparison = ignoreCase[j]
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal;
                    if (!String.Equals(rows[i][columns[j]], candidate[columns[j]], comparison))
                    {
                        matches = false;
                        break;
                    }
                }
                if (matches)
                    return i;
            }
            return -1;
        }

        private static bool RowsEqual(string[] first, string[] second)
        {
            if (first.Length != second.Length)
                return false;
            for (int i = 0; i < first.Length; i++)
                if (!String.Equals(first[i], second[i], StringComparison.Ordinal))
                    return false;
            return true;
        }
    }
}
