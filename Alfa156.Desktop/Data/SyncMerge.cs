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
        public int TombstoneHits;
    }

    internal sealed class SyncPlan
    {
        public bool AddFromGoogle = true;
        public bool RemoveMissingInGoogle = false;
        public SyncConflictPolicy ConflictPolicy = SyncConflictPolicy.PreferLocal;
    }

    internal static class SyncMerge
    {
        private static SyncJournal lastJournal;

        internal static SyncJournal LastJournal
        {
            get { return lastJournal; }
        }

        internal static List<SyncTableDiff> Analyze(AlfaRepository local, AlfaRepository remote)
        {
            List<Tombstone> tombs = RecordIds.UnionTombstones(local.Deleted, remote.Deleted);
            List<SyncTableDiff> diffs = new List<SyncTableDiff>();
            diffs.Add(AnalyzeTable("Автомобили", local.Cars, remote.Cars, new int[] { 0 }, new bool[] { true },
                RecordIds.IdsForType(tombs, RecordIds.TypeCar)));
            diffs.Add(AnalyzeTable("Заправки", local.Fuel, remote.Fuel, new int[] { 0, 1, 2 }, new bool[] { false, true, false },
                RecordIds.IdsForType(tombs, RecordIds.TypeFuel)));
            diffs.Add(AnalyzeTable("Ремонт", local.Repairs, remote.Repairs, new int[] { 0, 1, 2, 3, 4 },
                new bool[] { false, true, false, true, false },
                RecordIds.IdsForType(tombs, RecordIds.TypeRepair)));
            diffs.Add(AnalyzeTable("Коммуналка", local.Utilities, remote.Utilities, new int[] { 0 }, new bool[] { false },
                RecordIds.IdsForType(tombs, RecordIds.TypeUtility)));
            diffs.Add(AnalyzeTable("Категории", local.Categories, remote.Categories, new int[] { 0 }, new bool[] { true },
                RecordIds.IdsForType(tombs, RecordIds.TypeCategory)));
            diffs.Add(AnalyzeTable("Прочие расходы", local.Expenses, remote.Expenses, new int[] { 0, 1, 2, 3 },
                new bool[] { false, true, true, false },
                RecordIds.IdsForType(tombs, RecordIds.TypeExpense)));
            diffs.Add(AnalyzeTable("Кат. прочих", local.ExpenseCategories, remote.ExpenseCategories,
                new int[] { 0 }, new bool[] { true },
                RecordIds.IdsForType(tombs, RecordIds.TypeExpenseCategory)));
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
                    ", отличаются " + d.Conflicts +
                    (d.TombstoneHits > 0 ? ", удалений " + d.TombstoneHits : ""));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Dry-run: SyncJournal for the given plan without mutating local repository.
        /// </summary>
        internal static SyncJournal BuildPreview(AlfaRepository local, AlfaRepository remote, SyncPlan plan)
        {
            if (plan == null)
            {
                plan = new SyncPlan
                {
                    AddFromGoogle = true,
                    RemoveMissingInGoogle = false,
                    ConflictPolicy = SyncConflictPolicy.PreferLocal
                };
            }
            SyncJournal journal = new SyncJournal();
            List<Tombstone> tombs = RecordIds.UnionTombstones(local.Deleted, remote.Deleted);
            ApplyList(local.Cars, remote.Cars, new int[] { 0 }, new bool[] { true }, plan,
                RecordIds.IdsForType(tombs, RecordIds.TypeCar), "cars.dat", journal, false);
            ApplyList(local.Fuel, remote.Fuel, new int[] { 0, 1, 2 }, new bool[] { false, true, false }, plan,
                RecordIds.IdsForType(tombs, RecordIds.TypeFuel), "fuel.dat", journal, false);
            ApplyList(local.Repairs, remote.Repairs, new int[] { 0, 1, 2, 3, 4 },
                new bool[] { false, true, false, true, false }, plan,
                RecordIds.IdsForType(tombs, RecordIds.TypeRepair), "repairs.dat", journal, false);
            ApplyList(local.Utilities, remote.Utilities, new int[] { 0 }, new bool[] { false }, plan,
                RecordIds.IdsForType(tombs, RecordIds.TypeUtility), "utilities.dat", journal, false);
            ApplyList(local.Categories, remote.Categories, new int[] { 0 }, new bool[] { true }, plan,
                RecordIds.IdsForType(tombs, RecordIds.TypeCategory), AlfaCodec.RepairCategoriesFile,
                journal, false);
            ApplyList(local.Expenses, remote.Expenses, new int[] { 0, 1, 2, 3 },
                new bool[] { false, true, true, false }, plan,
                RecordIds.IdsForType(tombs, RecordIds.TypeExpense), AlfaCodec.ExpensesFile, journal, false);
            ApplyList(local.ExpenseCategories, remote.ExpenseCategories, new int[] { 0 }, new bool[] { true },
                plan, RecordIds.IdsForType(tombs, RecordIds.TypeExpenseCategory),
                AlfaCodec.ExpenseCategoriesFile, journal, false);
            return journal;
        }

        internal static SyncJournal ApplyPlan(AlfaRepository local, AlfaRepository remote, SyncPlan plan)
        {
            SyncJournal journal = new SyncJournal();
            lastJournal = journal;
            List<Tombstone> tombs = RecordIds.UnionTombstones(local.Deleted, remote.Deleted);

            local.ReplaceAll(
                ApplyList(local.Cars, remote.Cars, new int[] { 0 }, new bool[] { true }, plan,
                    RecordIds.IdsForType(tombs, RecordIds.TypeCar), "cars.dat", journal, true),
                ApplyList(local.Fuel, remote.Fuel, new int[] { 0, 1, 2 }, new bool[] { false, true, false }, plan,
                    RecordIds.IdsForType(tombs, RecordIds.TypeFuel), "fuel.dat", journal, true),
                ApplyList(local.Repairs, remote.Repairs, new int[] { 0, 1, 2, 3, 4 },
                    new bool[] { false, true, false, true, false }, plan,
                    RecordIds.IdsForType(tombs, RecordIds.TypeRepair), "repairs.dat", journal, true),
                ApplyList(local.Utilities, remote.Utilities, new int[] { 0 }, new bool[] { false }, plan,
                    RecordIds.IdsForType(tombs, RecordIds.TypeUtility), "utilities.dat", journal, true),
                ApplyList(local.Categories, remote.Categories, new int[] { 0 }, new bool[] { true }, plan,
                    RecordIds.IdsForType(tombs, RecordIds.TypeCategory), AlfaCodec.RepairCategoriesFile,
                    journal, true),
                ApplyList(local.Expenses, remote.Expenses, new int[] { 0, 1, 2, 3 },
                    new bool[] { false, true, true, false }, plan,
                    RecordIds.IdsForType(tombs, RecordIds.TypeExpense), AlfaCodec.ExpensesFile, journal, true),
                ApplyList(local.ExpenseCategories, remote.ExpenseCategories, new int[] { 0 }, new bool[] { true },
                    plan, RecordIds.IdsForType(tombs, RecordIds.TypeExpenseCategory),
                    AlfaCodec.ExpenseCategoriesFile, journal, true),
                ToSettings.MergeRows(local.Settings, remote.Settings,
                    plan.ConflictPolicy == SyncConflictPolicy.PreferIncoming));
            local.Deleted = tombs;
            return journal;
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
            bool[] ignoreCase,
            HashSet<string> deletedIds)
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
                string remoteId = RecordIds.GetId(remoteRows[i]);
                if (remoteId != null && deletedIds.Contains(remoteId))
                {
                    diff.TombstoneHits++;
                    continue;
                }
                int match = FindIdentity(localRows, remoteRows[i], keyColumns, ignoreCase);
                if (match < 0)
                {
                    diff.OnlyRemote++;
                    continue;
                }
                localMatched[match] = true;
                if (RecordIds.ContentEqual(localRows[match], remoteRows[i]))
                    diff.Identical++;
                else
                    diff.Conflicts++;
            }
            for (int i = 0; i < localMatched.Length; i++)
            {
                if (localMatched[i])
                    continue;
                string localId = RecordIds.GetId(localRows[i]);
                if (localId != null && deletedIds.Contains(localId))
                {
                    diff.TombstoneHits++;
                    continue;
                }
                diff.OnlyLocal++;
            }
            return diff;
        }

        private static List<string[]> ApplyList(
            List<string[]> localRows,
            List<string[]> remoteRows,
            int[] keyColumns,
            bool[] ignoreCase,
            SyncPlan plan,
            HashSet<string> deletedIds,
            string fileName,
            SyncJournal journal,
            bool writeOutput)
        {
            List<string[]> local = CloneRows(localRows);
            List<string[]> remote = CloneRows(remoteRows);
            RecordIds.EnsureIds(local, fileName);
            RecordIds.EnsureIds(remote, fileName);

            List<string[]> merged = new List<string[]>();
            bool[] localMatched = new bool[local.Count];
            string title = ConflictTitle(fileName);

            for (int i = 0; i < remote.Count; i++)
            {
                string[] remoteRow = remote[i];
                string remoteId = RecordIds.GetId(remoteRow);
                if (remoteId != null && deletedIds.Contains(remoteId))
                {
                    journal.Removed++;
                    journal.AddExample(FormatExample("-", title, remoteRow));
                    continue;
                }

                int match = FindIdentity(local, remoteRow, keyColumns, ignoreCase);
                if (match < 0)
                {
                    if (plan.AddFromGoogle)
                    {
                        if (writeOutput)
                            merged.Add((string[])remoteRow.Clone());
                        journal.Added++;
                        journal.AddExample(FormatExample("+", title, remoteRow));
                    }
                    continue;
                }

                localMatched[match] = true;
                string[] localRow = local[match];
                string localId = RecordIds.GetId(localRow);
                if (localId != null && deletedIds.Contains(localId))
                {
                    journal.Removed++;
                    journal.AddExample(FormatExample("-", title, localRow));
                    continue;
                }

                if (RecordIds.ContentEqual(localRow, remoteRow))
                {
                    if (writeOutput)
                        merged.Add(RecordIds.PreferId(localRow, remoteRow));
                    continue;
                }

                journal.Conflicts++;
                journal.AddExample(FormatExample("~", title, localRow));
                if (!writeOutput)
                {
                    if (plan.ConflictPolicy == SyncConflictPolicy.PreferIncoming)
                        journal.ConflictKeptIncoming++;
                    else if (plan.ConflictPolicy == SyncConflictPolicy.PreferLocal)
                        journal.ConflictKeptLocal++;
                    else
                        journal.ConflictMerged++;
                    continue;
                }

                if (plan.ConflictPolicy == SyncConflictPolicy.PreferIncoming)
                {
                    journal.ConflictKeptIncoming++;
                    journal.AddExample(FormatExample("R", title, remoteRow));
                    merged.Add(RecordIds.PreferId((string[])remoteRow.Clone(), localRow));
                }
                else if (plan.ConflictPolicy == SyncConflictPolicy.PreferLocal)
                {
                    journal.ConflictKeptLocal++;
                    journal.AddExample(FormatExample("L", title, localRow));
                    merged.Add(RecordIds.PreferId((string[])localRow.Clone(), remoteRow));
                }
                else
                {
                    journal.ConflictMerged++;
                    journal.AddExample(FormatExample("M", title, localRow));
                    merged.Add(RecordIds.EnsureId(MergeFields(RecordIds.StripId(localRow),
                        RecordIds.StripId(remoteRow)), RecordIds.PrefixForFile(fileName)));
                }
            }

            for (int i = 0; i < local.Count; i++)
            {
                if (localMatched[i])
                    continue;
                string localId = RecordIds.GetId(local[i]);
                if (localId != null && deletedIds.Contains(localId))
                {
                    journal.Removed++;
                    journal.AddExample(FormatExample("-", title, local[i]));
                    continue;
                }
                if (!plan.RemoveMissingInGoogle)
                {
                    if (writeOutput)
                        merged.Add((string[])local[i].Clone());
                    journal.KeptLocalOnly++;
                }
                else
                {
                    journal.Removed++;
                    journal.AddExample(FormatExample("-", title, local[i]));
                }
            }

            return merged;
        }

        private static string ConflictTitle(string fileName)
        {
            if (fileName == "cars.dat") return "Автомобили";
            if (fileName == "fuel.dat") return "Заправки";
            if (fileName == "repairs.dat") return "Ремонт";
            if (fileName == "utilities.dat") return "Коммуналка";
            if (fileName == AlfaCodec.RepairCategoriesFile) return "Категории";
            if (fileName == AlfaCodec.ExpensesFile) return "Прочие расходы";
            if (fileName == AlfaCodec.ExpenseCategoriesFile) return "Кат. прочих";
            return fileName;
        }

        private static string FormatExample(string mark, string title, string[] row)
        {
            string[] content = RecordIds.StripId(row);
            string car = "";
            string date = "";
            if (title == "Автомобили" || title == "Категории" || title == "Кат. прочих")
            {
                if (content.Length > 0) car = content[0] ?? "";
            }
            else if (title == "Коммуналка")
            {
                if (content.Length > 0) date = content[0] ?? "";
            }
            else
            {
                if (content.Length > 0) date = content[0] ?? "";
                if (content.Length > 1) car = content[1] ?? "";
            }
            return mark + " " + title + " | " + car + " | " + date;
        }

        private static List<string[]> CloneRows(List<string[]> rows)
        {
            List<string[]> copy = new List<string[]>();
            for (int i = 0; i < rows.Count; i++)
                copy.Add((string[])rows[i].Clone());
            return copy;
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
            string candidateId = RecordIds.GetId(candidate);
            if (candidateId != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    string id = RecordIds.GetId(rows[i]);
                    if (id != null && String.Equals(id, candidateId, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }

            string[] candidateContent = RecordIds.StripId(candidate);
            for (int i = 0; i < rows.Count; i++)
            {
                string[] row = RecordIds.StripId(rows[i]);
                if (row.Length <= columns[columns.Length - 1] || candidateContent.Length <= columns[columns.Length - 1])
                    continue;
                bool matches = true;
                for (int j = 0; j < columns.Length; j++)
                {
                    StringComparison comparison = ignoreCase[j]
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal;
                    if (!String.Equals(row[columns[j]], candidateContent[columns[j]], comparison))
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
