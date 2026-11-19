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
        public int TombstoneHits;
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
        private static SyncJournal lastJournal;

        internal static SyncJournal LastJournal
        {
            get { return lastJournal; }
        }

        internal static List<SyncTableDiff> AnalyzeDirectories(string localDirectory, string remoteDirectory)
        {
            List<Tombstone> tombs = RecordIds.UnionTombstones(
                RecordIds.ReadTombstones(localDirectory),
                RecordIds.ReadTombstones(remoteDirectory));

            List<SyncTableDiff> diffs = new List<SyncTableDiff>();
            diffs.Add(AnalyzeTable("Автомобили",
                DataStore.ReadSyncTable(localDirectory, "cars.dat"),
                DataStore.ReadSyncTable(remoteDirectory, "cars.dat"),
                new int[] { 0 }, new bool[] { true },
                RecordIds.IdsForType(tombs, RecordIds.TypeCar)));
            diffs.Add(AnalyzeTable("Заправки",
                DataStore.ReadSyncTable(localDirectory, "fuel.dat"),
                DataStore.ReadSyncTable(remoteDirectory, "fuel.dat"),
                new int[] { 0, 1, 2 }, new bool[] { false, true, false },
                RecordIds.IdsForType(tombs, RecordIds.TypeFuel)));
            diffs.Add(AnalyzeTable("Ремонт",
                DataStore.ReadSyncTable(localDirectory, "repairs.dat"),
                DataStore.ReadSyncTable(remoteDirectory, "repairs.dat"),
                new int[] { 0, 1, 2, 3, 4 },
                new bool[] { false, true, false, true, false },
                RecordIds.IdsForType(tombs, RecordIds.TypeRepair)));
            diffs.Add(AnalyzeTable("Коммуналка",
                DataStore.ReadSyncTable(localDirectory, "utilities.dat"),
                DataStore.ReadSyncTable(remoteDirectory, "utilities.dat"),
                new int[] { 0 }, new bool[] { false },
                RecordIds.IdsForType(tombs, RecordIds.TypeUtility)));
            diffs.Add(AnalyzeTable("Категории",
                DataStore.ReadSyncTable(localDirectory, "repair-categories.dat"),
                DataStore.ReadSyncTable(remoteDirectory, "repair-categories.dat"),
                new int[] { 0 }, new bool[] { true },
                RecordIds.IdsForType(tombs, RecordIds.TypeCategory)));
            diffs.Add(AnalyzeTable("Прочие расходы",
                DataStore.ReadSyncTable(localDirectory, "expenses.dat"),
                DataStore.ReadSyncTable(remoteDirectory, "expenses.dat"),
                new int[] { 0, 1, 2, 3 }, new bool[] { false, true, true, false },
                RecordIds.IdsForType(tombs, RecordIds.TypeExpense)));
            diffs.Add(AnalyzeTable("Кат. прочих",
                DataStore.ReadSyncTable(localDirectory, "expense-categories.dat"),
                DataStore.ReadSyncTable(remoteDirectory, "expense-categories.dat"),
                new int[] { 0 }, new bool[] { true },
                RecordIds.IdsForType(tombs, RecordIds.TypeExpenseCategory)));
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
                if (d.TombstoneHits > 0)
                {
                    sb.Append(", удалений: ");
                    sb.Append(d.TombstoneHits.ToString());
                }
            }
            return sb.ToString();
        }

        internal static SyncJournal ApplyPlan(
            string localDirectory,
            string remoteDirectory,
            string destinationDirectory,
            SyncPlan plan,
            SyncConflictResolver resolveConflict)
        {
            SyncJournal journal = new SyncJournal();
            lastJournal = journal;

            List<Tombstone> tombs = RecordIds.UnionTombstones(
                RecordIds.ReadTombstones(localDirectory),
                RecordIds.ReadTombstones(remoteDirectory));

            MergeFile("cars.dat", localDirectory, remoteDirectory, destinationDirectory,
                new int[] { 0 }, new bool[] { true }, plan, resolveConflict, tombs, journal);
            MergeFile("fuel.dat", localDirectory, remoteDirectory, destinationDirectory,
                new int[] { 0, 1, 2 }, new bool[] { false, true, false }, plan, resolveConflict,
                tombs, journal);
            MergeFile("repairs.dat", localDirectory, remoteDirectory, destinationDirectory,
                new int[] { 0, 1, 2, 3, 4 },
                new bool[] { false, true, false, true, false }, plan, resolveConflict, tombs, journal);
            MergeFile("utilities.dat", localDirectory, remoteDirectory, destinationDirectory,
                new int[] { 0 }, new bool[] { false }, plan, resolveConflict, tombs, journal);
            MergeFile("repair-categories.dat", localDirectory, remoteDirectory, destinationDirectory,
                new int[] { 0 }, new bool[] { true }, plan, resolveConflict, tombs, journal);
            MergeFile("expenses.dat", localDirectory, remoteDirectory, destinationDirectory,
                new int[] { 0, 1, 2, 3 }, new bool[] { false, true, true, false }, plan,
                resolveConflict, tombs, journal);
            MergeFile("expense-categories.dat", localDirectory, remoteDirectory, destinationDirectory,
                new int[] { 0 }, new bool[] { true }, plan, resolveConflict, tombs, journal);
            ToSettings.MergeDirectories(localDirectory, remoteDirectory, destinationDirectory,
                plan.ConflictPolicy == SyncConflictPolicy.PreferIncoming);

            RecordIds.WriteTombstones(destinationDirectory, tombs);
            return journal;
        }

        private static SyncTableDiff AnalyzeTable(
            string title,
            List<string[]> localRows,
            List<string[]> remoteRows,
            int[] keyColumns,
            bool[] ignoreCase,
            Dictionary<string, bool> deletedIds)
        {
            SyncTableDiff diff = new SyncTableDiff();
            diff.Title = title;
            diff.LocalCount = localRows.Count;
            diff.RemoteCount = remoteRows.Count;

            bool[] localMatched = new bool[localRows.Count];
            for (int i = 0; i < remoteRows.Count; i++)
            {
                string[] remote = remoteRows[i];
                string remoteId = RecordIds.GetId(remote);
                if (remoteId != null && deletedIds.ContainsKey(remoteId))
                {
                    diff.TombstoneHits++;
                    continue;
                }
                int match = FindIdentity(localRows, remote, keyColumns, ignoreCase);
                if (match < 0)
                {
                    diff.OnlyRemote++;
                    continue;
                }
                localMatched[match] = true;
                if (!RecordIds.ContentEqual(localRows[match], remote))
                    diff.Conflicts++;
            }
            for (int i = 0; i < localMatched.Length; i++)
            {
                if (localMatched[i])
                    continue;
                string localId = RecordIds.GetId(localRows[i]);
                if (localId != null && deletedIds.ContainsKey(localId))
                {
                    diff.TombstoneHits++;
                    continue;
                }
                diff.OnlyLocal++;
            }
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
            SyncConflictResolver resolveConflict,
            List<Tombstone> tombs,
            SyncJournal journal)
        {
            List<string[]> localRows = DataStore.ReadSyncTable(localDirectory, fileName);
            List<string[]> remoteRows = DataStore.ReadSyncTable(remoteDirectory, fileName);
            RecordIds.EnsureIds(localRows, fileName);
            RecordIds.EnsureIds(remoteRows, fileName);

            Dictionary<string, bool> deleted = RecordIds.IdsForType(
                tombs, RecordIds.EntityTypeForFile(fileName));
            List<string[]> merged = new List<string[]>();
            bool[] localMatched = new bool[localRows.Count];
            string title = ConflictTitle(fileName);

            for (int i = 0; i < remoteRows.Count; i++)
            {
                string[] remote = remoteRows[i];
                string remoteId = RecordIds.GetId(remote);
                if (remoteId != null && deleted.ContainsKey(remoteId))
                {
                    journal.Removed++;
                    journal.AddExample("удалено " + title + " " + remoteId);
                    continue;
                }

                int match = FindIdentity(localRows, remote, keyColumns, ignoreCase);
                if (match < 0)
                {
                    if (plan.AddIncoming)
                    {
                        merged.Add(remote);
                        journal.Added++;
                        journal.AddExample("+ " + title + ": " + FormatRow(RecordIds.StripId(remote)));
                    }
                    continue;
                }

                localMatched[match] = true;
                string[] local = localRows[match];
                string localId = RecordIds.GetId(local);
                if (localId != null && deleted.ContainsKey(localId))
                {
                    journal.Removed++;
                    journal.AddExample("удалено " + title + " " + localId);
                    continue;
                }

                if (RecordIds.ContentEqual(local, remote))
                {
                    merged.Add(RecordIds.PreferId(local, remote));
                    continue;
                }

                journal.Conflicts++;
                journal.AddExample("~ " + title + ": " + FormatRow(RecordIds.StripId(local)));

                SyncConflictChoice choice;
                if (plan.ConflictPolicy == SyncConflictPolicy.PreferIncoming)
                    choice = SyncConflictChoice.KeepIncoming;
                else if (plan.ConflictPolicy == SyncConflictPolicy.PreferLocal)
                    choice = SyncConflictChoice.KeepLocal;
                else
                    choice = resolveConflict(title, FormatRow(local), FormatRow(remote));

                if (choice == SyncConflictChoice.Cancel)
                    throw new InvalidOperationException("Синхронизация отменена пользователем.");
                if (choice == SyncConflictChoice.KeepIncoming)
                    merged.Add(RecordIds.PreferId(remote, local));
                else
                    merged.Add(RecordIds.PreferId(local, remote));
            }

            for (int i = 0; i < localRows.Count; i++)
            {
                if (localMatched[i])
                    continue;
                string localId = RecordIds.GetId(localRows[i]);
                if (localId != null && deleted.ContainsKey(localId))
                {
                    journal.Removed++;
                    journal.AddExample("удалено " + title + " " + localId);
                    continue;
                }
                if (!plan.RemoveLocalOnly)
                {
                    merged.Add(localRows[i]);
                    journal.KeptLocalOnly++;
                }
                else
                {
                    journal.Removed++;
                    journal.AddExample("удалено локальное " + title);
                }
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
            if (fileName == "expenses.dat")
                return "Прочие расходы";
            if (fileName == "expense-categories.dat")
                return "Кат. прочих";
            return fileName;
        }

        private static string FormatRow(string[] row)
        {
            StringBuilder sb = new StringBuilder();
            string[] content = RecordIds.StripId(row);
            for (int i = 0; i < content.Length; i++)
            {
                if (i > 0)
                    sb.Append(" | ");
                sb.Append(content[i] == null ? "" : content[i]);
            }
            return sb.ToString();
        }

        private static int FindIdentity(List<string[]> rows, string[] candidate, int[] columns, bool[] ignoreCase)
        {
            string candidateId = RecordIds.GetId(candidate);
            if (candidateId != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    string id = RecordIds.GetId(rows[i]);
                    if (id != null && String.Compare(id, candidateId, true,
                        System.Globalization.CultureInfo.InvariantCulture) == 0)
                        return i;
                }
            }

            string[] candidateContent = RecordIds.StripId(candidate);
            for (int i = 0; i < rows.Count; i++)
            {
                string[] row = RecordIds.StripId(rows[i]);
                if (row.Length <= columns[columns.Length - 1] ||
                    candidateContent.Length <= columns[columns.Length - 1])
                    continue;
                bool matches = true;
                for (int j = 0; j < columns.Length; j++)
                {
                    if (ignoreCase[j])
                    {
                        if (String.Compare(row[columns[j]], candidateContent[columns[j]], true,
                            System.Globalization.CultureInfo.InvariantCulture) != 0)
                        {
                            matches = false;
                            break;
                        }
                    }
                    else if (row[columns[j]] != candidateContent[columns[j]])
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
    }
}
