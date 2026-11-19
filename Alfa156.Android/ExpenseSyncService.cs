using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Alfa156.Android;

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

internal sealed class SyncPlan
{
    public bool AddIncoming = true;
    public bool RemoveLocalOnly = false;
    public SyncConflictPolicy ConflictPolicy = SyncConflictPolicy.PreferLocal;
    public bool Cancelled;
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

internal delegate SyncConflictChoice SyncConflictResolver(
    string title, string localRow, string incomingRow);

internal delegate SyncPlan SyncPlanResolver(string summary);

internal static class ExpenseSyncService
{
    internal const int Port = 45816;
    private const int MaximumFileSize = 16 * 1024 * 1024;
    private const string ProtocolHeader = "ALFA156SYNC1|";

    private const int HandshakeTimeoutMs = 10000;

    [ThreadStatic]
    private static SyncJournal lastJournal;

    internal static SyncJournal LastJournal
    {
        get { return lastJournal; }
    }

    internal static string Serve(
        TcpListener listener,
        string pairingCode,
        string dataDirectory,
        string stagingRoot,
        SyncPlanResolver resolvePlan,
        SyncConflictResolver resolveConflict,
        Action<TcpClient> clientAccepted)
    {
        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            clientAccepted(client);
            using (client)
            {
                bool abandoned;
                string error = ServeClient(client, pairingCode, dataDirectory, stagingRoot,
                    resolvePlan, resolveConflict, out abandoned);
                if (!abandoned)
                    return error;
            }
        }
    }

    private static string ServeClient(
        TcpClient client,
        string pairingCode,
        string dataDirectory,
        string stagingRoot,
        SyncPlanResolver resolvePlan,
        SyncConflictResolver resolveConflict,
        out bool abandoned)
    {
        abandoned = false;
        using (NetworkStream stream = client.GetStream())
        {
            stream.ReadTimeout = HandshakeTimeoutMs;
            stream.WriteTimeout = HandshakeTimeoutMs;
            string incomingDirectory = Path.Combine(stagingRoot, "incoming_" + Guid.NewGuid().ToString("N"));
            string mergedDirectory = Path.Combine(stagingRoot, "merged_" + Guid.NewGuid().ToString("N"));
            bool finalResponseStarted = false;
            try
            {
                string handshake;
                try
                {
                    handshake = ReadLine(stream);
                }
                catch (Exception)
                {
                    abandoned = true;
                    return null;
                }
                stream.ReadTimeout = 300000;
                stream.WriteTimeout = 300000;
                if (!String.Equals(handshake, ProtocolHeader + pairingCode, StringComparison.Ordinal))
                {
                    WriteResponse(stream, 1, "Неверный код подключения.");
                    return "Неверный код подключения.";
                }

                WriteInt32(stream, 0);
                Directory.CreateDirectory(incomingDirectory);
                ReceiveSnapshot(stream, incomingDirectory);
                ExpenseDataCodec.ValidateDirectory(incomingDirectory);

                SyncPlan plan = resolvePlan(FormatSummary(
                    AnalyzeDirectories(dataDirectory, incomingDirectory),
                    "Android", "КПК"));
                if (plan == null || plan.Cancelled)
                {
                    WriteResponse(stream, 1, "Синхронизация отменена на Android.");
                    return "Синхронизация отменена.";
                }

                MergeAndInstall(dataDirectory, incomingDirectory, mergedDirectory, plan, resolveConflict);
                finalResponseStarted = true;
                WriteInt32(stream, 0);
                SendSnapshot(stream, dataDirectory);
                return null;
            }
            catch (Exception ex)
            {
                if (finalResponseStarted)
                    throw;
                try
                {
                    WriteResponse(stream, 1, ex.Message);
                }
                catch (Exception responseError)
                {
                    throw new IOException(
                        "Синхронизация не завершена: " + ex.Message +
                        ". Не удалось передать сообщение об ошибке: " + responseError.Message);
                }
                return ex.Message;
            }
            finally
            {
                if (Directory.Exists(incomingDirectory))
                    Directory.Delete(incomingDirectory, true);
                if (Directory.Exists(mergedDirectory))
                    Directory.Delete(mergedDirectory, true);
            }
        }
    }

    internal static List<string> GetLocalIpv4Addresses()
    {
        List<string> addresses = new List<string>();
        List<string> labels = new List<string>();
        NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
        for (int i = 0; i < interfaces.Length; i++)
        {
            OperationalStatus status = interfaces[i].OperationalStatus;
            if (status != OperationalStatus.Up && status != OperationalStatus.Unknown)
                continue;
            UnicastIPAddressInformationCollection unicast =
                interfaces[i].GetIPProperties().UnicastAddresses;
            for (int j = 0; j < unicast.Count; j++)
            {
                IPAddress address = unicast[j].Address;
                if (address.AddressFamily != AddressFamily.InterNetwork ||
                    IPAddress.IsLoopback(address))
                    continue;
                string ip = address.ToString();
                if (ip.StartsWith("169.254.") || ip == "0.0.0.0" || addresses.Contains(ip))
                    continue;
                addresses.Add(ip);
                labels.Add(DescribeEndpoint(interfaces[i], ip));
            }
        }

        for (int i = 0; i < addresses.Count; i++)
        {
            for (int j = i + 1; j < addresses.Count; j++)
            {
                if (EndpointRank(labels[i], addresses[i]) <= EndpointRank(labels[j], addresses[j]))
                    continue;
                string ip = addresses[i];
                addresses[i] = addresses[j];
                addresses[j] = ip;
                string label = labels[i];
                labels[i] = labels[j];
                labels[j] = label;
            }
        }

        List<string> lines = new List<string>();
        for (int i = 0; i < addresses.Count; i++)
            lines.Add(addresses[i] + " — " + labels[i]);
        return lines;
    }

    private static string DescribeEndpoint(NetworkInterface nic, string ip)
    {
        string name = nic.Name == null ? "" : nic.Name.ToLowerInvariant();
        bool hotspotName = name.IndexOf("softap") >= 0 || name.IndexOf("ap0") >= 0 ||
            name.IndexOf("swlan") >= 0 || name.IndexOf("tether") >= 0 ||
            name.IndexOf("hotspot") >= 0 || name.IndexOf("wlan1") >= 0;
        bool hotspotIp = ip.EndsWith(".1") &&
            (ip.StartsWith("192.168.43.") || ip.StartsWith("192.168.44.") ||
             ip.StartsWith("192.168.49.") || ip.StartsWith("192.168.137.") ||
             ip.StartsWith("192.168.165.") || ip.StartsWith("192.168.75.") ||
             ip.StartsWith("192.168.182.") || ip.StartsWith("192.168.150."));
        if (hotspotName || hotspotIp)
            return "точка доступа ← вводите этот IP на КПК";
        if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
            name.IndexOf("wlan") >= 0 || name.IndexOf("wifi") >= 0)
            return "Wi-Fi";
        if (nic.Name == null || nic.Name.Length == 0)
            return "сеть";
        return nic.Name;
    }

    private static int EndpointRank(string label, string ip)
    {
        if (label == "точка доступа")
            return 0;
        if (label == "Wi-Fi")
            return 1;
        if (ip.StartsWith("192.168."))
            return 2;
        if (ip.StartsWith("10."))
            return 3;
        return 4;
    }

    internal static void MergeFromIncoming(
        string dataDirectory,
        string incomingDirectory,
        string stagingRoot,
        SyncPlan plan,
        SyncConflictResolver resolveConflict)
    {
        string mergedDirectory = Path.Combine(stagingRoot, "merged-" + Guid.NewGuid().ToString("N"));
        MergeAndInstall(dataDirectory, incomingDirectory, mergedDirectory, plan, resolveConflict);
    }

    internal static List<SyncTableDiff> AnalyzeDirectories(string localDirectory, string remoteDirectory)
    {
        List<Tombstone> tombs = RecordIds.UnionTombstones(
            RecordIds.ReadTombstones(localDirectory),
            RecordIds.ReadTombstones(remoteDirectory));

        List<SyncTableDiff> diffs = new List<SyncTableDiff>();
        diffs.Add(AnalyzeTable("Автомобили",
            ExpenseDataCodec.ReadRows(Path.Combine(localDirectory, "cars.dat")),
            ExpenseDataCodec.ReadRows(Path.Combine(remoteDirectory, "cars.dat")),
            new int[] { 0 }, new bool[] { true },
            RecordIds.IdsForType(tombs, RecordIds.TypeCar)));
        diffs.Add(AnalyzeTable("Заправки",
            ExpenseDataCodec.ReadRows(Path.Combine(localDirectory, "fuel.dat")),
            ExpenseDataCodec.ReadRows(Path.Combine(remoteDirectory, "fuel.dat")),
            new int[] { 0, 1, 2 }, new bool[] { false, true, false },
            RecordIds.IdsForType(tombs, RecordIds.TypeFuel)));
        diffs.Add(AnalyzeTable("Ремонт",
            ExpenseDataCodec.ReadRows(Path.Combine(localDirectory, "repairs.dat")),
            ExpenseDataCodec.ReadRows(Path.Combine(remoteDirectory, "repairs.dat")),
            new int[] { 0, 1, 2, 3, 4 }, new bool[] { false, true, false, true, false },
            RecordIds.IdsForType(tombs, RecordIds.TypeRepair)));
        diffs.Add(AnalyzeTable("Коммуналка",
            ExpenseDataCodec.ReadRows(Path.Combine(localDirectory, "utilities.dat")),
            ExpenseDataCodec.ReadRows(Path.Combine(remoteDirectory, "utilities.dat")),
            new int[] { 0 }, new bool[] { false },
            RecordIds.IdsForType(tombs, RecordIds.TypeUtility)));
        string localCat = Path.Combine(localDirectory, ExpenseDataCodec.RepairCategoriesFile);
        string remoteCat = Path.Combine(remoteDirectory, ExpenseDataCodec.RepairCategoriesFile);
        diffs.Add(AnalyzeTable("Категории",
            File.Exists(localCat) ? ExpenseDataCodec.ReadRows(localCat) : new List<string[]>(),
            File.Exists(remoteCat) ? ExpenseDataCodec.ReadRows(remoteCat) : new List<string[]>(),
            new int[] { 0 }, new bool[] { true },
            RecordIds.IdsForType(tombs, RecordIds.TypeCategory)));
        diffs.Add(AnalyzeTable("Прочие расходы",
            ExpenseDataStore.LoadOptional(localDirectory, ExpenseDataCodec.ExpensesFile),
            ExpenseDataStore.LoadOptional(remoteDirectory, ExpenseDataCodec.ExpensesFile),
            new int[] { 0, 1, 2, 3 }, new bool[] { false, true, true, false },
            RecordIds.IdsForType(tombs, RecordIds.TypeExpense)));
        string localExpCat = Path.Combine(localDirectory, ExpenseDataCodec.ExpenseCategoriesFile);
        string remoteExpCat = Path.Combine(remoteDirectory, ExpenseDataCodec.ExpenseCategoriesFile);
        diffs.Add(AnalyzeTable("Кат. прочих",
            File.Exists(localExpCat) ? ExpenseDataCodec.ReadRows(localExpCat) : new List<string[]>(),
            File.Exists(remoteExpCat) ? ExpenseDataCodec.ReadRows(remoteExpCat) : new List<string[]>(),
            new int[] { 0 }, new bool[] { true },
            RecordIds.IdsForType(tombs, RecordIds.TypeExpenseCategory)));
        return diffs;
    }

    internal static string FormatSummary(List<SyncTableDiff> diffs, string localName, string remoteName)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("Сравнение: ").Append(localName).Append(" ↔ ").Append(remoteName).Append('\n');
        for (int i = 0; i < diffs.Count; i++)
        {
            SyncTableDiff d = diffs[i];
            sb.Append('\n').Append(d.Title)
                .Append(": ").Append(localName).Append(' ').Append(d.LocalCount)
                .Append(", ").Append(remoteName).Append(' ').Append(d.RemoteCount)
                .Append("\n  только ").Append(localName).Append(": ").Append(d.OnlyLocal)
                .Append(", только ").Append(remoteName).Append(": ").Append(d.OnlyRemote)
                .Append(", отличаются: ").Append(d.Conflicts);
            if (d.TombstoneHits > 0)
                sb.Append(", удалений: ").Append(d.TombstoneHits);
        }
        return sb.ToString();
    }

    private static SyncTableDiff AnalyzeTable(
        string title, List<string[]> localRows, List<string[]> remoteRows,
        int[] keyColumns, bool[] ignoreCase, HashSet<string> deletedIds)
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
            if (!RecordIds.ContentEqual(localRows[match], remoteRows[i]))
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

    private static void MergeAndInstall(
        string dataDirectory,
        string incomingDirectory,
        string mergedDirectory,
        SyncPlan plan,
        SyncConflictResolver resolveConflict)
    {
        bool hasLocalData = HasAnyDataFile(dataDirectory);
        bool hasCompleteLocalData = HasCompleteData(dataDirectory);
        if (hasLocalData && !hasCompleteLocalData)
            throw new InvalidDataException("На телефоне неполный набор данных. Сначала восстановите копию.");

        Directory.CreateDirectory(mergedDirectory);
        SyncJournal journal = new SyncJournal();
        lastJournal = journal;

        List<Tombstone> mergedTombs = RecordIds.UnionTombstones(
            RecordIds.ReadTombstones(dataDirectory),
            RecordIds.ReadTombstones(incomingDirectory));

        if (!hasCompleteLocalData)
        {
            ExpenseDataStore.CopyDataFiles(incomingDirectory, mergedDirectory);
            ApplyTombstonesToCopied(mergedDirectory, mergedTombs, journal);
        }
        else
        {
            ExpenseDataCodec.ValidateDirectory(dataDirectory);
            ExpenseDataStore.EnsureRepairCategories(dataDirectory);
            ExpenseDataStore.EnsureCompanionFiles(dataDirectory);
            MergeRows("cars.dat", dataDirectory, incomingDirectory, mergedDirectory,
                new int[] { 0 }, new bool[] { true }, plan, resolveConflict, mergedTombs, journal);
            MergeRows(ExpenseDataCodec.RepairCategoriesFile, dataDirectory, incomingDirectory,
                mergedDirectory, new int[] { 0 }, new bool[] { true }, plan, resolveConflict,
                mergedTombs, journal);
            MergeRows("fuel.dat", dataDirectory, incomingDirectory, mergedDirectory,
                new int[] { 0, 1, 2 }, new bool[] { false, true, false }, plan, resolveConflict,
                mergedTombs, journal);
            MergeRows("repairs.dat", dataDirectory, incomingDirectory, mergedDirectory,
                new int[] { 0, 1, 2, 3, 4 },
                new bool[] { false, true, false, true, false }, plan, resolveConflict,
                mergedTombs, journal);
            MergeRows("utilities.dat", dataDirectory, incomingDirectory, mergedDirectory,
                new int[] { 0 }, new bool[] { false }, plan, resolveConflict, mergedTombs, journal);
            MergeRows(ExpenseDataCodec.ExpensesFile, dataDirectory, incomingDirectory, mergedDirectory,
                new int[] { 0, 1, 2, 3 }, new bool[] { false, true, true, false }, plan,
                resolveConflict, mergedTombs, journal);
            MergeRows(ExpenseDataCodec.ExpenseCategoriesFile, dataDirectory, incomingDirectory,
                mergedDirectory, new int[] { 0 }, new bool[] { true }, plan, resolveConflict,
                mergedTombs, journal);
            ToSettings.MergeFiles(dataDirectory, incomingDirectory, mergedDirectory,
                plan.ConflictPolicy == SyncConflictPolicy.PreferIncoming);
        }

        RecordIds.WriteTombstones(mergedDirectory, mergedTombs);
        ExpenseDataStore.EnsureRepairCategories(mergedDirectory);
        ExpenseDataStore.EnsureCompanionFiles(mergedDirectory);
        EnsureIdsInDirectory(mergedDirectory);
        ExpenseDataCodec.ValidateDirectory(mergedDirectory);
        ValidateSnapshotSizes(mergedDirectory);

        string safetyBackup = null;
        if (hasCompleteLocalData)
            safetyBackup = ExpenseDataStore.CreateBackup(dataDirectory);

        try
        {
            ExpenseDataStore.CopyDataFiles(mergedDirectory, dataDirectory);
            ExpenseDataCodec.ValidateDirectory(dataDirectory);
        }
        catch (Exception installError)
        {
            if (safetyBackup == null)
            {
                try
                {
                    for (int i = 0; i < ExpenseDataCodec.DataFiles.Length; i++)
                    {
                        string path = Path.Combine(dataDirectory, ExpenseDataCodec.DataFiles[i]);
                        if (File.Exists(path))
                            File.Delete(path);
                    }
                }
                catch (Exception rollbackError)
                {
                    throw new IOException("Синхронизация не завершена: " + installError.Message +
                        ". Не удалось очистить неполный набор данных: " + rollbackError.Message);
                }
                throw;
            }

            string backupPath = Path.Combine(Path.GetDirectoryName(dataDirectory),
                "Backups", safetyBackup);
            try
            {
                ExpenseDataStore.CopyDataFiles(backupPath, dataDirectory);
            }
            catch (Exception rollbackError)
            {
                throw new IOException("Синхронизация не завершена: " + installError.Message +
                    ". Не удалось вернуть прежние данные: " + rollbackError.Message +
                    ". Копия сохранена в " + backupPath);
            }
            throw;
        }
    }

    private static void ApplyTombstonesToCopied(
        string directory, List<Tombstone> tombs, SyncJournal journal)
    {
        FilterFileByTombstones(directory, "cars.dat", RecordIds.TypeCar, tombs, journal);
        FilterFileByTombstones(directory, "fuel.dat", RecordIds.TypeFuel, tombs, journal);
        FilterFileByTombstones(directory, "repairs.dat", RecordIds.TypeRepair, tombs, journal);
        FilterFileByTombstones(directory, "utilities.dat", RecordIds.TypeUtility, tombs, journal);
        FilterFileByTombstones(directory, ExpenseDataCodec.RepairCategoriesFile,
            RecordIds.TypeCategory, tombs, journal);
    }

    private static void FilterFileByTombstones(
        string directory, string fileName, string entityType,
        List<Tombstone> tombs, SyncJournal journal)
    {
        string path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
            return;
        HashSet<string> deleted = RecordIds.IdsForType(tombs, entityType);
        List<string[]> rows = ExpenseDataCodec.ReadRows(path);
        List<string[]> kept = new List<string[]>();
        for (int i = 0; i < rows.Count; i++)
        {
            string id = RecordIds.GetId(rows[i]);
            if (id != null && deleted.Contains(id))
            {
                journal.Removed++;
                journal.AddExample("удалено " + entityType + " " + id);
                continue;
            }
            kept.Add(RecordIds.EnsureId(rows[i], RecordIds.PrefixForFile(fileName)));
        }
        ExpenseDataCodec.SaveRows(path, kept);
    }

    private static void EnsureIdsInDirectory(string directory)
    {
        EnsureIdsFile(directory, "cars.dat");
        EnsureIdsFile(directory, "fuel.dat");
        EnsureIdsFile(directory, "repairs.dat");
        EnsureIdsFile(directory, "utilities.dat");
        EnsureIdsFile(directory, ExpenseDataCodec.RepairCategoriesFile);
        EnsureIdsFile(directory, ExpenseDataCodec.ExpensesFile);
        EnsureIdsFile(directory, ExpenseDataCodec.ExpenseCategoriesFile);
        RecordIds.EnsureDeletedFile(directory);
        ToSettings.EnsureFile(directory);
    }

    private static void EnsureIdsFile(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
            return;
        List<string[]> rows = ExpenseDataCodec.ReadRows(path);
        RecordIds.EnsureIds(rows, fileName);
        ExpenseDataCodec.SaveRows(path, rows);
    }

    private static void MergeRows(
        string fileName,
        string localDirectory,
        string incomingDirectory,
        string destinationDirectory,
        int[] keyColumns,
        bool[] ignoreCase,
        SyncPlan plan,
        SyncConflictResolver resolveConflict,
        List<Tombstone> tombs,
        SyncJournal journal)
    {
        List<string[]> localRows = ExpenseDataStore.LoadOptional(localDirectory, fileName);
        List<string[]> incoming = ExpenseDataStore.LoadOptional(incomingDirectory, fileName);
        RecordIds.EnsureIds(localRows, fileName);
        RecordIds.EnsureIds(incoming, fileName);

        HashSet<string> deleted = RecordIds.IdsForType(tombs, RecordIds.EntityTypeForFile(fileName));
        List<string[]> merged = new List<string[]>();
        bool[] localMatched = new bool[localRows.Count];
        string title = ConflictTitle(fileName);

        for (int i = 0; i < incoming.Count; i++)
        {
            string[] row = incoming[i];
            string remoteId = RecordIds.GetId(row);
            if (remoteId != null && deleted.Contains(remoteId))
            {
                journal.Removed++;
                journal.AddExample("удалено " + title + " " + remoteId);
                continue;
            }

            int matchIndex = FindIdentity(localRows, row, keyColumns, ignoreCase);
            if (matchIndex < 0)
            {
                if (plan.AddIncoming)
                {
                    merged.Add(row);
                    journal.Added++;
                    journal.AddExample("+ " + title + ": " + FormatRow(RecordIds.StripId(row)));
                }
                continue;
            }

            localMatched[matchIndex] = true;
            string[] local = localRows[matchIndex];
            string localId = RecordIds.GetId(local);
            if (localId != null && deleted.Contains(localId))
            {
                journal.Removed++;
                journal.AddExample("удалено " + title + " " + localId);
                continue;
            }

            if (RecordIds.ContentEqual(local, row))
            {
                merged.Add(RecordIds.PreferId(local, row));
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
                choice = resolveConflict(title, FormatRow(local), FormatRow(row));

            if (choice == SyncConflictChoice.Cancel)
                throw new System.OperationCanceledException("Синхронизация отменена пользователем.");
            if (choice == SyncConflictChoice.KeepIncoming)
                merged.Add(RecordIds.PreferId(row, local));
            else
                merged.Add(RecordIds.PreferId(local, row));
        }

        for (int i = 0; i < localRows.Count; i++)
        {
            if (localMatched[i])
                continue;
            string localId = RecordIds.GetId(localRows[i]);
            if (localId != null && deleted.Contains(localId))
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

        ExpenseDataCodec.SaveRows(Path.Combine(destinationDirectory, fileName), merged);
    }

    private static int FindIdentity(
        List<string[]> rows, string[] candidate, int[] columns, bool[] ignoreCase)
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
            string[] rowContent = RecordIds.StripId(rows[i]);
            if (rowContent.Length <= columns[columns.Length - 1] ||
                candidateContent.Length <= columns[columns.Length - 1])
                continue;
            bool matches = true;
            for (int j = 0; j < columns.Length; j++)
            {
                StringComparison comparison = ignoreCase[j]
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                if (!String.Equals(rowContent[columns[j]], candidateContent[columns[j]], comparison))
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

    private static string ConflictTitle(string fileName)
    {
        if (fileName == "cars.dat")
            return "Автомобиль";
        if (fileName == ExpenseDataCodec.RepairCategoriesFile)
            return "Категория ремонта";
        if (fileName == "fuel.dat")
            return "Заправка";
        if (fileName == "repairs.dat")
            return "Ремонт";
        if (fileName == ExpenseDataCodec.ExpensesFile)
            return "Прочий расход";
        if (fileName == ExpenseDataCodec.ExpenseCategoriesFile)
            return "Категория прочих";
        return "Коммунальные";
    }

    private static string FormatRow(string[] row)
    {
        return String.Join(" | ", RecordIds.StripId(row));
    }

    private static bool HasAnyDataFile(string directory)
    {
        if (!Directory.Exists(directory))
            return false;
        for (int i = 0; i < ExpenseDataCodec.CoreDataFiles.Length; i++)
            if (File.Exists(Path.Combine(directory, ExpenseDataCodec.CoreDataFiles[i])))
                return true;
        return false;
    }

    private static bool HasCompleteData(string directory)
    {
        if (!Directory.Exists(directory))
            return false;
        for (int i = 0; i < ExpenseDataCodec.CoreDataFiles.Length; i++)
            if (!File.Exists(Path.Combine(directory, ExpenseDataCodec.CoreDataFiles[i])))
                return false;
        return true;
    }

    private static void ReceiveSnapshot(NetworkStream stream, string directory)
    {
        for (int i = 0; i < ExpenseDataCodec.DataFiles.Length; i++)
        {
            int length = ReadInt32(stream);
            if (length < 0 || length > MaximumFileSize)
                throw new InvalidDataException("Размер файла синхронизации превышает допустимый.");
            string path = Path.Combine(directory, ExpenseDataCodec.DataFiles[i]);
            using (FileStream output = File.Create(path))
                CopyExactly(stream, output, length);
        }
    }

    private static void SendSnapshot(NetworkStream stream, string directory)
    {
        RecordIds.EnsureDeletedFile(directory);
        ExpenseDataStore.EnsureCompanionFiles(directory);
        ExpenseDataStore.EnsureRepairCategories(directory);
        for (int i = 0; i < ExpenseDataCodec.DataFiles.Length; i++)
        {
            string path = Path.Combine(directory, ExpenseDataCodec.DataFiles[i]);
            byte[] content = File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();
            WriteInt32(stream, content.Length);
            if (content.Length > 0)
                stream.Write(content, 0, content.Length);
        }
        stream.Flush();
    }

    private static void ValidateSnapshotSizes(string directory)
    {
        for (int i = 0; i < ExpenseDataCodec.DataFiles.Length; i++)
        {
            string path = Path.Combine(directory, ExpenseDataCodec.DataFiles[i]);
            if (!File.Exists(path))
            {
                if (ExpenseDataCodec.DataFiles[i] == RecordIds.DeletedFile ||
                    ExpenseDataCodec.DataFiles[i] == ExpenseDataCodec.ExpensesFile ||
                    ExpenseDataCodec.DataFiles[i] == ExpenseDataCodec.ExpenseCategoriesFile ||
                    ExpenseDataCodec.DataFiles[i] == ExpenseDataCodec.SettingsFile)
                    continue;
                throw new InvalidDataException("Нет файла " + ExpenseDataCodec.DataFiles[i]);
            }
            long length = new FileInfo(path).Length;
            if (length > MaximumFileSize)
                throw new InvalidDataException("Файл " + ExpenseDataCodec.DataFiles[i] +
                    " превышает допустимый размер синхронизации.");
        }
    }

    private static string ReadLine(NetworkStream stream)
    {
        StringBuilder line = new StringBuilder();
        while (line.Length < 64)
        {
            int value = stream.ReadByte();
            if (value < 0)
                throw new EndOfStreamException("Соединение прервано до проверки кода.");
            if (value == '\n')
                return line.ToString().TrimEnd('\r');
            line.Append((char)value);
        }
        throw new InvalidDataException("Неверный запрос синхронизации.");
    }

    private static void WriteResponse(NetworkStream stream, int status, string message)
    {
        WriteInt32(stream, status);
        byte[] text = Encoding.UTF8.GetBytes(message ?? "");
        if (text.Length > 4096)
            Array.Resize(ref text, 4096);
        WriteInt32(stream, text.Length);
        stream.Write(text, 0, text.Length);
        stream.Flush();
    }

    private static void CopyExactly(Stream input, Stream output, int length)
    {
        byte[] buffer = new byte[8192];
        int remaining = length;
        while (remaining > 0)
        {
            int count = input.Read(buffer, 0, Math.Min(buffer.Length, remaining));
            if (count == 0)
                throw new EndOfStreamException("Передача файла синхронизации оборвалась.");
            output.Write(buffer, 0, count);
            remaining -= count;
        }
    }

    private static int ReadInt32(Stream stream)
    {
        byte[] bytes = new byte[4];
        ReadExactly(stream, bytes, 0, bytes.Length);
        return bytes[0] | bytes[1] << 8 | bytes[2] << 16 | bytes[3] << 24;
    }

    private static void WriteInt32(Stream stream, int value)
    {
        byte[] bytes =
        {
            (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)
        };
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            int read = stream.Read(buffer, offset, count);
            if (read == 0)
                throw new EndOfStreamException("Соединение синхронизации прервано.");
            offset += read;
            count -= read;
        }
    }
}
