using System;
using System.Collections.Generic;
using System.Text;

namespace CarExpenses
{
    internal enum SyncConflictChoice
    {
        KeepLocal,
        KeepIncoming,
        Cancel
    }

    internal enum SyncConflictPolicy
    {
        PreferLocal,
        PreferIncoming,
        AskEach
    }

    internal sealed class SyncTableDiff
    {
        public string Title;
        public int LocalCount;
        public int RemoteCount;
        public int OnlyLocal;
        public int OnlyRemote;
        public int Conflicts;
    }

    internal sealed class SyncPlan
    {
        public bool AddIncoming = true;
        public bool RemoveLocalOnly = false;
        public SyncConflictPolicy ConflictPolicy = SyncConflictPolicy.PreferLocal;
        public bool Cancelled;
    }

    internal delegate SyncPlan SyncPlanResolver(string summary);
    internal delegate SyncConflictChoice SyncConflictResolver(
        string title, string localRow, string incomingRow);

    internal static class SyncMerge
    {
        internal static List<SyncTableDiff> AnalyzeDirectories(string localDirectory, string remoteDirectory)
        {
            List<SyncTableDiff> diffs = new List<SyncTableDiff>();
            diffs.Add(AnalyzeTable("Автомобили",
                DataStore.ReadSyncTable(localDirectory, "cars.dat"),
                DataStore.ReadSyncTable(remoteDirectory, "cars.dat"),
                new int[] { 0 }, new bool[] { true }));
            diffs.Add(AnalyzeTable("Заправки",
                DataStore.ReadSyncTable(localDirectory, "fuel.dat"),
                DataStore.ReadSyncTable(remoteDirectory, "fuel.dat"),
                new int[] { 0, 1, 2 }, new bool[] { false, true, false }));
            diffs.Add(AnalyzeTable("Ремонт",
                DataStore.ReadSyncTable(localDirectory, "repairs.dat"),
                DataStore.ReadSyncTable(remoteDirectory, "repairs.dat"),
                new int[] { 0, 1, 2, 3, 4 },
                new bool[] { false, true, false, true, false }));
            diffs.Add(AnalyzeTable("Коммуналка",
                DataStore.ReadSyncTable(localDirectory, "utilities.dat"),
                DataStore.ReadSyncTable(remoteDirectory, "utilities.dat"),
                new int[] { 0 }, new bool[] { false }));
            diffs.Add(AnalyzeTable("Категории",
                DataStore.ReadSyncTable(localDirectory, "repair-categories.dat"),
                DataStore.ReadSyncTable(remoteDirectory, "repair-categories.dat"),
                new int[] { 0 }, new bool[] { true }));
            return diffs;
        }

        internal static string FormatSummary(List<SyncTableDiff> diffs)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Сравнение: КПК ↔ Android\n");
            for (int i = 0; i < diffs.Count; i++)
            {
                SyncTableDiff d = diffs[i];
                sb.Append("\n");
                sb.Append(d.Title);
                sb.Append(": КПК ");
                sb.Append(d.LocalCount.ToString());
                sb.Append(", Android ");
                sb.Append(d.RemoteCount.ToString());
                sb.Append("\n  только КПК: ");
                sb.Append(d.OnlyLocal.ToString());
                sb.Append(", только Android: ");
                sb.Append(d.OnlyRemote.ToString());
                sb.Append(", отличаются: ");
                sb.Append(d.Conflicts.ToString());
            }
            return sb.ToString();
        }

        internal static void ApplyPlan(
            string localDirectory,
            string remoteDirectory,
            string destinationDirectory,
            SyncPlan plan,
            SyncConflictResolver resolveConflict)
        {
            MergeFile("cars.dat", localDirectory, remoteDirectory, destinationDirectory,
                new int[] { 0 }, new bool[] { true }, plan, resolveConflict);
            MergeFile("fuel.dat", localDirectory, remoteDirectory, destinationDirectory,
                new int[] { 0, 1, 2 }, new bool[] { false, true, false }, plan, resolveConflict);
            MergeFile("repairs.dat", localDirectory, remoteDirectory, destinationDirectory,
                new int[] { 0, 1, 2, 3, 4 },
                new bool[] { false, true, false, true, false }, plan, resolveConflict);
            MergeFile("utilities.dat", localDirectory, remoteDirectory, destinationDirectory,
                new int[] { 0 }, new bool[] { false }, plan, resolveConflict);
            MergeFile("repair-categories.dat", localDirectory, remoteDirectory, destinationDirectory,
                new int[] { 0 }, new bool[] { true }, plan, resolveConflict);
        }

        private static SyncTableDiff AnalyzeTable(
            string title,
            List<string[]> localRows,
            List<string[]> remoteRows,
            int[] keyColumns,
            bool[] ignoreCase)
        {
            SyncTableDiff diff = new SyncTableDiff();
            diff.Title = title;
            diff.LocalCount = localRows.Count;
            diff.RemoteCount = remoteRows.Count;

            bool[] localMatched = new bool[localRows.Count];
            for (int i = 0; i < remoteRows.Count; i++)
            {
                string[] remote = remoteRows[i];
                int match = FindIdentity(localRows, remote, keyColumns, ignoreCase);
                if (match < 0)
                {
                    diff.OnlyRemote++;
                    continue;
                }
                localMatched[match] = true;
                if (!RowsEqual(localRows[match], remote))
                    diff.Conflicts++;
            }
            for (int i = 0; i < localMatched.Length; i++)
                if (!localMatched[i])
                    diff.OnlyLocal++;
            return diff;
        }

        private static void MergeFile(
            string fileName,
            string localDirectory,
            string remoteDirectory,
            string destinationDirectory,
            int[] keyColumns,
            bool[] ignoreCase,
            SyncPlan plan,
            SyncConflictResolver resolveConflict)
        {
            List<string[]> localRows = DataStore.ReadSyncTable(localDirectory, fileName);
            List<string[]> remoteRows = DataStore.ReadSyncTable(remoteDirectory, fileName);
            List<string[]> merged = new List<string[]>();
            bool[] localMatched = new bool[localRows.Count];

            for (int i = 0; i < remoteRows.Count; i++)
            {
                string[] remote = remoteRows[i];
                int match = FindIdentity(localRows, remote, keyColumns, ignoreCase);
                if (match < 0)
                {
                    if (plan.AddIncoming)
                        merged.Add(remote);
                    continue;
                }

                localMatched[match] = true;
                string[] local = localRows[match];
                if (RowsEqual(local, remote))
                {
                    merged.Add(local);
                    continue;
                }

                SyncConflictChoice choice;
                if (plan.ConflictPolicy == SyncConflictPolicy.PreferIncoming)
                    choice = SyncConflictChoice.KeepIncoming;
                else if (plan.ConflictPolicy == SyncConflictPolicy.PreferLocal)
                    choice = SyncConflictChoice.KeepLocal;
                else
                    choice = resolveConflict(ConflictTitle(fileName), FormatRow(local), FormatRow(remote));

                if (choice == SyncConflictChoice.Cancel)
                    throw new InvalidOperationException("Синхронизация отменена пользователем.");
                if (choice == SyncConflictChoice.KeepIncoming)
                    merged.Add(remote);
                else
                    merged.Add(local);
            }

            for (int i = 0; i < localRows.Count; i++)
            {
                if (localMatched[i])
                    continue;
                if (!plan.RemoveLocalOnly)
                    merged.Add(localRows[i]);
            }

            DataStore.WriteSyncTable(destinationDirectory, fileName, merged);
        }

        private static string ConflictTitle(string fileName)
        {
            if (fileName == "cars.dat")
                return "Автомобили";
            if (fileName == "fuel.dat")
                return "Заправки";
            if (fileName == "repairs.dat")
                return "Ремонт";
            if (fileName == "utilities.dat")
                return "Коммуналка";
            if (fileName == "repair-categories.dat")
                return "Категории";
            return fileName;
        }

        private static string FormatRow(string[] row)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < row.Length; i++)
            {
                if (i > 0)
                    sb.Append(" | ");
                sb.Append(row[i] == null ? "" : row[i]);
            }
            return sb.ToString();
        }

        private static int FindIdentity(List<string[]> rows, string[] candidate, int[] columns, bool[] ignoreCase)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                string[] row = rows[i];
                if (row.Length <= columns[columns.Length - 1] ||
                    candidate.Length <= columns[columns.Length - 1])
                    continue;
                bool matches = true;
                for (int j = 0; j < columns.Length; j++)
                {
                    if (ignoreCase[j])
                    {
                        if (String.Compare(row[columns[j]], candidate[columns[j]], true,
                            System.Globalization.CultureInfo.InvariantCulture) != 0)
                        {
                            matches = false;
                            break;
                        }
                    }
                    else if (row[columns[j]] != candidate[columns[j]])
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
                if (first[i] != second[i])
                    return false;
            return true;
        }
    }
}
