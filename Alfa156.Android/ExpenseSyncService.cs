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

internal delegate SyncConflictChoice SyncConflictResolver(
    string title, string localRow, string incomingRow);

internal static class ExpenseSyncService
{
    internal const int Port = 45816;
    private const int MaximumFileSize = 16 * 1024 * 1024;
    private const string ProtocolHeader = "ALFA156SYNC1|";

    internal static string Serve(
        TcpListener listener,
        string pairingCode,
        string dataDirectory,
        string stagingRoot,
        SyncConflictResolver resolveConflict,
        Action<TcpClient> clientAccepted)
    {
        using (TcpClient client = listener.AcceptTcpClient())
        {
            clientAccepted(client);
            return ServeClient(client, pairingCode, dataDirectory, stagingRoot, resolveConflict);
        }
    }

    private static string ServeClient(
        TcpClient client,
        string pairingCode,
        string dataDirectory,
        string stagingRoot,
        SyncConflictResolver resolveConflict)
    {
        using (NetworkStream stream = client.GetStream())
        {
            stream.ReadTimeout = 300000;
            stream.WriteTimeout = 300000;
            string incomingDirectory = Path.Combine(stagingRoot, "incoming_" + Guid.NewGuid().ToString("N"));
            string mergedDirectory = Path.Combine(stagingRoot, "merged_" + Guid.NewGuid().ToString("N"));
            bool finalResponseStarted = false;
            try
            {
                string handshake = ReadLine(stream);
                if (!String.Equals(handshake, ProtocolHeader + pairingCode, StringComparison.Ordinal))
                {
                    WriteResponse(stream, 1, "Неверный код подключения.");
                    return "Неверный код подключения.";
                }

                WriteInt32(stream, 0);
                Directory.CreateDirectory(incomingDirectory);
                ReceiveSnapshot(stream, incomingDirectory);
                ExpenseDataCodec.ValidateDirectory(incomingDirectory);
                MergeAndInstall(dataDirectory, incomingDirectory, mergedDirectory, resolveConflict);
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
        NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
        for (int i = 0; i < interfaces.Length; i++)
        {
            if (interfaces[i].OperationalStatus != OperationalStatus.Up)
                continue;
            UnicastIPAddressInformationCollection unicast =
                interfaces[i].GetIPProperties().UnicastAddresses;
            for (int j = 0; j < unicast.Count; j++)
            {
                IPAddress address = unicast[j].Address;
                if (address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address) && !addresses.Contains(address.ToString()))
                    addresses.Add(address.ToString());
            }
        }
        return addresses;
    }

    private static void MergeAndInstall(
        string dataDirectory,
        string incomingDirectory,
        string mergedDirectory,
        SyncConflictResolver resolveConflict)
    {
        bool hasLocalData = HasAnyDataFile(dataDirectory);
        bool hasCompleteLocalData = HasCompleteData(dataDirectory);
        if (hasLocalData && !hasCompleteLocalData)
            throw new InvalidDataException("На телефоне неполный набор данных. Сначала восстановите копию.");

        Directory.CreateDirectory(mergedDirectory);
        if (!hasCompleteLocalData)
        {
            ExpenseDataStore.CopyDataFiles(incomingDirectory, mergedDirectory);
        }
        else
        {
            ExpenseDataCodec.ValidateDirectory(dataDirectory);
            ExpenseDataStore.EnsureRepairCategories(dataDirectory);
            MergeRows("cars.dat", dataDirectory, incomingDirectory, mergedDirectory,
                new int[] { 0 }, new bool[] { true }, resolveConflict);
            MergeRows(ExpenseDataCodec.RepairCategoriesFile, dataDirectory, incomingDirectory,
                mergedDirectory, new int[] { 0 }, new bool[] { true }, resolveConflict);
            MergeRows("fuel.dat", dataDirectory, incomingDirectory, mergedDirectory,
                new int[] { 0, 1, 2 }, new bool[] { false, true, false }, resolveConflict);
            MergeRows("repairs.dat", dataDirectory, incomingDirectory, mergedDirectory,
                new int[] { 0, 1, 2, 3, 4 },
                new bool[] { false, true, false, true, false }, resolveConflict);
            MergeRows("utilities.dat", dataDirectory, incomingDirectory, mergedDirectory,
                new int[] { 0 }, new bool[] { false }, resolveConflict);
        }

        ExpenseDataStore.EnsureRepairCategories(mergedDirectory);
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

    private static void MergeRows(
        string fileName,
        string localDirectory,
        string incomingDirectory,
        string destinationDirectory,
        int[] keyColumns,
        bool[] ignoreCase,
        SyncConflictResolver resolveConflict)
    {
        List<string[]> merged = ExpenseDataCodec.ReadRows(Path.Combine(localDirectory, fileName));
        List<string[]> incoming = ExpenseDataCodec.ReadRows(Path.Combine(incomingDirectory, fileName));
        for (int i = 0; i < incoming.Count; i++)
        {
            string[] row = incoming[i];
            int matchIndex = FindIdentity(merged, row, keyColumns, ignoreCase);
            if (matchIndex < 0)
            {
                merged.Add(row);
                continue;
            }
            if (RowsEqual(merged[matchIndex], row))
                continue;

            SyncConflictChoice choice = resolveConflict(
                ConflictTitle(fileName), FormatRow(merged[matchIndex]), FormatRow(row));
            if (choice == SyncConflictChoice.Cancel)
                throw new OperationCanceledException("Синхронизация отменена пользователем.");
            if (choice == SyncConflictChoice.KeepIncoming)
                merged[matchIndex] = row;
        }
        ExpenseDataCodec.SaveRows(Path.Combine(destinationDirectory, fileName), merged);
    }

    private static int FindIdentity(
        List<string[]> rows, string[] candidate, int[] columns, bool[] ignoreCase)
    {
        for (int i = 0; i < rows.Count; i++)
        {
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

    private static string ConflictTitle(string fileName)
    {
        if (fileName == "cars.dat")
            return "Автомобиль";
        if (fileName == ExpenseDataCodec.RepairCategoriesFile)
            return "Категория ремонта";
        if (fileName == "fuel.dat")
            return "Заправка с той же датой и пробегом";
        if (fileName == "repairs.dat")
            return "Ремонт с теми же датой, авто и товаром";
        return "Коммунальные данные за тот же месяц";
    }

    private static string FormatRow(string[] row)
    {
        return String.Join(" | ", row);
    }

    private static bool HasAnyDataFile(string directory)
    {
        if (!Directory.Exists(directory))
            return false;
        for (int i = 0; i < ExpenseDataCodec.DataFiles.Length; i++)
            if (File.Exists(Path.Combine(directory, ExpenseDataCodec.DataFiles[i])))
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
        for (int i = 0; i < ExpenseDataCodec.DataFiles.Length; i++)
        {
            byte[] content = File.ReadAllBytes(
                Path.Combine(directory, ExpenseDataCodec.DataFiles[i]));
            WriteInt32(stream, content.Length);
            stream.Write(content, 0, content.Length);
        }
        stream.Flush();
    }

    private static void ValidateSnapshotSizes(string directory)
    {
        for (int i = 0; i < ExpenseDataCodec.DataFiles.Length; i++)
        {
            long length = new FileInfo(Path.Combine(directory, ExpenseDataCodec.DataFiles[i])).Length;
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
