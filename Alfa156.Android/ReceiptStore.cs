using System;
using System.Collections.Generic;
using System.IO;
using Android.Graphics;

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
            throw new ArgumentException("Неверный id записи для фото чека.", "recordId");
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

    internal static void SaveJpegFromBitmap(string dataDirectory, string recordId, Bitmap source)
    {
        if (source == null)
            throw new ArgumentNullException("source");
        EnsureFolder(dataDirectory);
        string path = PathForId(dataDirectory, recordId);
        string temp = path + ".tmp";
        using (Bitmap scaled = ScaleDown(source, MaxEdgePx))
        using (FileStream output = File.Create(temp))
        {
            if (!scaled.Compress(Bitmap.CompressFormat.Jpeg, JpegQuality, output))
                throw new IOException("Не удалось сжать фото чека.");
        }
        if (File.Exists(path))
            File.Delete(path);
        File.Move(temp, path);
    }

    internal static void SaveJpegFromStream(string dataDirectory, string recordId, Stream input)
    {
        if (input == null)
            throw new ArgumentNullException("input");
        using (Bitmap decoded = BitmapFactory.DecodeStream(input))
        {
            if (decoded == null)
                throw new InvalidDataException("Не удалось прочитать изображение.");
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

    internal static bool IsSafeReceiptFileName(string name)
    {
        if (String.IsNullOrEmpty(name) || name.IndexOf("..") >= 0)
            return false;
        if (!name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            return false;
        string id = Path.GetFileNameWithoutExtension(name);
        return RecordIds.LooksLikeId(id);
    }

    private static Bitmap ScaleDown(Bitmap source, int maxEdge)
    {
        int w = source.Width;
        int h = source.Height;
        if (w <= maxEdge && h <= maxEdge)
        {
            Bitmap copy = source.Copy(Bitmap.Config.Argb8888, false);
            return copy ?? Bitmap.CreateBitmap(source);
        }
        float scale = Math.Min((float)maxEdge / w, (float)maxEdge / h);
        int nw = Math.Max(1, (int)(w * scale));
        int nh = Math.Max(1, (int)(h * scale));
        return Bitmap.CreateScaledBitmap(source, nw, nh, true);
    }
}
