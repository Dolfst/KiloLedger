using System;
using System.Collections.Generic;
using System.IO;
using AndroidBitmap = global::Android.Graphics.Bitmap;
using AndroidBitmapFactory = global::Android.Graphics.BitmapFactory;

using Alfa156.Localization;
namespace Alfa156.Android;

/// <summary>
/// Optional receipt JPEGs beside .dat files: Data/Receipts/{recordId}.jpg.
/// Does not alter the text .dat format.
/// </summary>
internal static class ReceiptStore
{
    internal const string FolderName = "Receipts";
    private const int MaxEdgePx = 1280;
    private const int JpegQuality = 72;

    internal static string GetFolder(string dataDirectory)
    {
        return Path.Combine(dataDirectory, FolderName);
    }

    internal static string PathForId(string dataDirectory, string recordId)
    {
        if (String.IsNullOrEmpty(recordId) || !RecordIds.LooksLikeId(recordId))
            throw new ArgumentException(L10n.Get("error.invalid_receipt_id"), "recordId");
        return Path.Combine(GetFolder(dataDirectory), recordId + ".jpg");
    }

    internal static bool Exists(string dataDirectory, string recordId)
    {
        if (String.IsNullOrEmpty(recordId) || !RecordIds.LooksLikeId(recordId))
            return false;
        return File.Exists(PathForId(dataDirectory, recordId));
    }

    internal static void EnsureFolder(string dataDirectory)
    {
        Directory.CreateDirectory(GetFolder(dataDirectory));
    }

    internal static void Delete(string dataDirectory, string recordId)
    {
        if (!Exists(dataDirectory, recordId))
            return;
        File.Delete(PathForId(dataDirectory, recordId));
    }

    internal static void SaveJpegFromBitmap(string dataDirectory, string recordId, AndroidBitmap source)
    {
        if (source == null)
            throw new ArgumentNullException("source");
        EnsureFolder(dataDirectory);
        string path = PathForId(dataDirectory, recordId);
        string temp = path + ".tmp";
        using (AndroidBitmap scaled = ScaleDown(source, MaxEdgePx))
        using (FileStream output = File.Create(temp))
        {
            if (!scaled.Compress(AndroidBitmap.CompressFormat.Jpeg, JpegQuality, output))
                throw new IOException(L10n.Get("error.compress_receipt_failed"));
        }
        if (File.Exists(path))
            File.Delete(path);
        File.Move(temp, path);
    }

    internal static void SaveJpegFromStream(string dataDirectory, string recordId, Stream input)
    {
        if (input == null)
            throw new ArgumentNullException("input");
        using (AndroidBitmap decoded = AndroidBitmapFactory.DecodeStream(input))
        {
            if (decoded == null)
                throw new InvalidDataException(L10n.Get("error.read_image_failed"));
            SaveJpegFromBitmap(dataDirectory, recordId, decoded);
        }
    }

    internal static void CopyFolder(string sourceDataDir, string destDataDir)
    {
        string source = GetFolder(sourceDataDir);
        string dest = GetFolder(destDataDir);
        if (!Directory.Exists(source))
            return;
        Directory.CreateDirectory(dest);
        string[] files = Directory.GetFiles(source, "*.jpg");
        for (int i = 0; i < files.Length; i++)
        {
            string name = Path.GetFileName(files[i]);
            File.Copy(files[i], Path.Combine(dest, name), true);
        }
    }

    internal static void AddFolderToZip(System.IO.Compression.ZipArchive archive, string dataDirectory)
    {
        string folder = GetFolder(dataDirectory);
        if (!Directory.Exists(folder))
            return;
        string[] files = Directory.GetFiles(folder, "*.jpg");
        for (int i = 0; i < files.Length; i++)
        {
            string name = Path.GetFileName(files[i]);
            if (!IsSafeReceiptFileName(name))
                continue;
            System.IO.Compression.ZipArchiveEntry entry =
                archive.CreateEntry(FolderName + "/" + name,
                    System.IO.Compression.CompressionLevel.Optimal);
            using (Stream entryStream = entry.Open())
            using (FileStream input = File.OpenRead(files[i]))
                input.CopyTo(entryStream);
        }
    }

    internal static void ExtractFromZipEntry(
        System.IO.Compression.ZipArchiveEntry entry, string dataDirectory)
    {
        if (entry == null || String.IsNullOrEmpty(entry.FullName))
            return;
        string normalized = entry.FullName.Replace('\\', '/');
        string fileName = Path.GetFileName(normalized);
        if (!IsSafeReceiptFileName(fileName))
            return;
        bool underReceipts = normalized.StartsWith(FolderName + "/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals(FolderName + "/" + fileName, StringComparison.OrdinalIgnoreCase) ||
            (normalized.IndexOf('/') < 0 && IsSafeReceiptFileName(normalized));
        // Accept Receipts/{id}.jpg or bare {id}.jpg inside archive.
        if (!underReceipts && normalized.IndexOf('/') >= 0 &&
            !normalized.StartsWith(FolderName + "/", StringComparison.OrdinalIgnoreCase))
            return;
        EnsureFolder(dataDirectory);
        string dest = Path.Combine(GetFolder(dataDirectory), fileName);
        using (Stream entryStream = entry.Open())
        using (FileStream output = File.Create(dest))
            entryStream.CopyTo(output);
    }

    internal static List<string> ListReceiptIds(string dataDirectory)
    {
        List<string> ids = new List<string>();
        string folder = GetFolder(dataDirectory);
        if (!Directory.Exists(folder))
            return ids;
        string[] files = Directory.GetFiles(folder, "*.jpg");
        for (int i = 0; i < files.Length; i++)
        {
            string name = Path.GetFileNameWithoutExtension(files[i]);
            if (RecordIds.LooksLikeId(name))
                ids.Add(name);
        }
        return ids;
    }

    /// <summary>Wi‑Fi sync limits (1.15+): skip files larger than 2 MB; at most 50 per session.</summary>
    internal const int MaxSyncFileBytes = 2 * 1024 * 1024;
    internal const int MaxSyncFiles = 50;

    internal static bool IsSafeReceiptFileName(string name)
    {
        if (String.IsNullOrEmpty(name) || name.IndexOf("..") >= 0)
            return false;
        if (!name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            return false;
        string id = Path.GetFileNameWithoutExtension(name);
        return RecordIds.LooksLikeId(id);
    }

    /// <summary>
    /// Paths under Receipts/ eligible for Wi‑Fi: safe name, size ≤ 2 MB, capped at 50.
    /// Prefers files whose id appears in fuel/repairs/expenses; then remaining files.
    /// </summary>
    internal static List<string> ListForSync(string dataDirectory)
    {
        List<string> result = new List<string>();
        string folder = GetFolder(dataDirectory);
        if (!Directory.Exists(folder))
            return result;

        HashSet<string> referenced = CollectReferencedIds(dataDirectory);
        string[] files = Directory.GetFiles(folder, "*.jpg");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        AddSyncCandidates(result, files, referenced, true);
        AddSyncCandidates(result, files, referenced, false);
        return result;
    }

    private static void AddSyncCandidates(
        List<string> result, string[] files, HashSet<string> referenced, bool requireReferenced)
    {
        for (int i = 0; i < files.Length; i++)
        {
            if (result.Count >= MaxSyncFiles)
                return;
            string name = Path.GetFileName(files[i]);
            if (!IsSafeReceiptFileName(name))
                continue;
            string id = Path.GetFileNameWithoutExtension(name);
            bool isRef = referenced.Contains(id);
            if (requireReferenced != isRef)
                continue;
            try
            {
                long length = new FileInfo(files[i]).Length;
                if (length <= 0 || length > MaxSyncFileBytes)
                    continue;
            }
            catch
            {
                continue;
            }
            bool already = false;
            for (int j = 0; j < result.Count; j++)
            {
                if (String.Equals(result[j], files[i], StringComparison.OrdinalIgnoreCase))
                {
                    already = true;
                    break;
                }
            }
            if (!already)
                result.Add(files[i]);
        }
    }

    private static HashSet<string> CollectReferencedIds(string dataDirectory)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddIdsFromFile(ids, dataDirectory, "fuel.dat");
        AddIdsFromFile(ids, dataDirectory, "repairs.dat");
        AddIdsFromFile(ids, dataDirectory, ExpenseDataCodec.ExpensesFile);
        return ids;
    }

    private static void AddIdsFromFile(HashSet<string> ids, string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
            return;
        List<string[]> rows = ExpenseDataCodec.ReadRows(path);
        for (int i = 0; i < rows.Count; i++)
        {
            string id = RecordIds.GetId(rows[i]);
            if (id != null)
                ids.Add(id);
        }
    }

    /// <summary>Copy receipt files from source onto dest without deleting dest-only files.</summary>
    internal static void OverlayFrom(string sourceDataDir, string destDataDir)
    {
        string source = GetFolder(sourceDataDir);
        if (!Directory.Exists(source))
            return;
        EnsureFolder(destDataDir);
        string[] files = Directory.GetFiles(source, "*.jpg");
        for (int i = 0; i < files.Length; i++)
        {
            string name = Path.GetFileName(files[i]);
            if (!IsSafeReceiptFileName(name))
                continue;
            File.Copy(files[i], Path.Combine(GetFolder(destDataDir), name), true);
        }
    }

    private static AndroidBitmap ScaleDown(AndroidBitmap source, int maxEdge)
    {
        int w = source.Width;
        int h = source.Height;
        if (w <= maxEdge && h <= maxEdge)
        {
            AndroidBitmap copy = source.Copy(AndroidBitmap.Config.Argb8888, false);
            return copy ?? AndroidBitmap.CreateBitmap(source);
        }
        float scale = Math.Min((float)maxEdge / w, (float)maxEdge / h);
        int nw = Math.Max(1, (int)(w * scale));
        int nh = Math.Max(1, (int)(h * scale));
        return AndroidBitmap.CreateScaledBitmap(source, nw, nh, true);
    }
}
