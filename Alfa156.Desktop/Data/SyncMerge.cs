using System;
using System.Collections.Generic;
using System.Text;

namespace Alfa156.Desktop.Data
{
    internal enum SyncConflictPolicy
    {
        PreferIncoming,
        PreferLocal,
        MergeFields
    }

    internal sealed class SyncTableDiff
    {
        public string Title;
        public int LocalCount;
        public int RemoteCount;
        public int OnlyLocal;
        public int OnlyRemote;
        public int Conflicts;
        public int Identical;
    }

    internal sealed class SyncPlan
    {
        public bool AddFromGoogle = true;
        public bool RemoveMissingInGoogle = false;
        public SyncConflictPolicy ConflictPolicy = SyncConflictPolicy.PreferLocal;
    }

    internal static class SyncMerge
    {
        internal static List<SyncTableDiff> Analyze(AlfaRepository local, AlfaRepository remote)
        {
            List<SyncTableDiff> diffs = new List<SyncTableDiff>();
            diffs.Add(AnalyzeTable("Автомобили", local.Cars, remote.Cars, new int[] { 0 }, new bool[] { true }));
            diffs.Add(AnalyzeTable("Заправки", local.Fuel, remote.Fuel, new int[] { 0, 1, 2 }, new bool[] { false, true, false }));
            diffs.Add(AnalyzeTable("Ремонт", local.Repairs, remote.Repairs, new int[] { 0, 1, 2, 3, 4 },
                new bool[] { false, true, false, true, false }));
            diffs.Add(AnalyzeTable("Коммуналка", local.Utilities, remote.Utilities, new int[] { 0 }, new bool[] { false }));
            diffs.Add(AnalyzeTable("Категории", local.Categories, remote.Categories, new int[] { 0 }, new bool[] { true }));
            return diffs;
        }

        internal static string FormatSummary(List<SyncTableDiff> diffs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Сравнение локальных данных и Google:");
            sb.AppendLine();
            for (int i = 0; i < diffs.Count; i++)
            {
                SyncTableDiff d = diffs[i];
                sb.AppendLine(d.Title + ": локально " + d.LocalCount + ", в Google " + d.RemoteCount +
                    " | только локально " + d.OnlyLocal +
                    ", только в Google " + d.OnlyRemote +
                    ", отличаются " + d.Conflicts);
            }
            return sb.ToString();
        }

        internal static void ApplyPlan(AlfaRepository local, AlfaRepository remote, SyncPlan plan)
        {
            local.ReplaceAll(
                ApplyList(local.Cars, remote.Cars, new int[] { 0 }, new bool[] { true }, plan),
                ApplyList(local.Fuel, remote.Fuel, new int[] { 0, 1, 2 }, new bool[] { false, true, false }, plan),
                ApplyList(local.Repairs, remote.Repairs, new int[] { 0, 1, 2, 3, 4 },
                    new bool[] { false, true, false, true, false }, plan),
                ApplyList(local.Utilities, remote.Utilities, new int[] { 0 }, new bool[] { false }, plan),
                ApplyList(local.Categories, remote.Categories, new int[] { 0 }, new bool[] { true }, plan));
        }

        internal static void MergeInto(
            AlfaRepository local,
            AlfaRepository incoming,
            SyncConflictPolicy policy)
        {
            SyncPlan plan = new SyncPlan
            {
                AddFromGoogle = true,
                RemoveMissingInGoogle = false,
                ConflictPolicy = policy
            };
            ApplyPlan(local, incoming, plan);
        }

        private static SyncTableDiff AnalyzeTable(
            string title,
            List<string[]> localRows,
            List<string[]> remoteRows,
            int[] keyColumns,
            bool[] ignoreCase)
        {
            SyncTableDiff diff = new SyncTableDiff
            {
                Title = title,
                LocalCount = localRows.Count,
                RemoteCount = remoteRows.Count
            };

            bool[] localMatched = new bool[localRows.Count];
            for (int i = 0; i < remoteRows.Count; i++)
            {
                int match = FindIdentity(localRows, remoteRows[i], keyColumns, ignoreCase);
                if (match < 0)
                {
                    diff.OnlyRemote++;
                    continue;
                }
                localMatched[match] = true;
                if (RowsEqual(localRows[match], remoteRows[i]))
                    diff.Identical++;
                else
                    diff.Conflicts++;
            }
            for (int i = 0; i < localMatched.Length; i++)
                if (!localMatched[i])
                    diff.OnlyLocal++;
            return diff;
        }

        private static List<string[]> ApplyList(
            List<string[]> localRows,
            List<string[]> remoteRows,
            int[] keyColumns,
            bool[] ignoreCase,
            SyncPlan plan)
        {
            List<string[]> merged = new List<string[]>();
            bool[] localMatched = new bool[localRows.Count];

            for (int i = 0; i < remoteRows.Count; i++)
            {
                string[] remote = remoteRows[i];
                int match = FindIdentity(localRows, remote, keyColumns, ignoreCase);
                if (match < 0)
                {
                    if (plan.AddFromGoogle)
                        merged.Add((string[])remote.Clone());
                    continue;
                }

                localMatched[match] = true;
                string[] local = localRows[match];
                if (RowsEqual(local, remote))
                {
                    merged.Add((string[])local.Clone());
                    continue;
                }

                if (plan.ConflictPolicy == SyncConflictPolicy.PreferIncoming)
                    merged.Add((string[])remote.Clone());
                else if (plan.ConflictPolicy == SyncConflictPolicy.PreferLocal)
                    merged.Add((string[])local.Clone());
                else
                    merged.Add(MergeFields(local, remote));
            }

            for (int i = 0; i < localRows.Count; i++)
            {
                if (localMatched[i])
                    continue;
                if (!plan.RemoveMissingInGoogle)
                    merged.Add((string[])localRows[i].Clone());
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
                    result[i] = right;
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
