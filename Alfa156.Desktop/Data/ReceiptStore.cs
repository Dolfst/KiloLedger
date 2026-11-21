using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;

namespace Alfa156.Desktop.Data
{
    /// <summary>
    /// Optional receipt JPEGs: {dataFolder}/Receipts/{recordId}.jpg
    /// </summary>
    internal static class ReceiptStore
    {
        internal const string FolderName = "Receipts";
        private const int MaxEdgePx = 1280;
        private const long JpegQuality = 72L;

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

        internal static void Delete(string dataDirectory, string recordId)
        {
            if (!Exists(dataDirectory, recordId))
                return;
            File.Delete(PathForId(dataDirectory, recordId));
        }

        internal static void EnsureFolder(string dataDirectory)
        {
            Directory.CreateDirectory(GetFolder(dataDirectory));
        }

        internal static void SaveFromFile(string dataDirectory, string recordId, string sourcePath)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Файл изображения не найден.", sourcePath);
            EnsureFolder(dataDirectory);
            string dest = PathForId(dataDirectory, recordId);
            string temp = dest + ".tmp";
            using (Image source = Image.FromFile(sourcePath))
            using (Bitmap scaled = ScaleDown(source, MaxEdgePx))
            {
                ImageCodecInfo jpeg = FindJpegCodec();
                EncoderParameters parameters = new EncoderParameters(1);
                parameters.Param[0] = new EncoderParameter(Encoder.Quality, JpegQuality);
                if (jpeg != null)
                    scaled.Save(temp, jpeg, parameters);
                else
                    scaled.Save(temp, ImageFormat.Jpeg);
            }
            if (File.Exists(dest))
                File.Delete(dest);
            File.Move(temp, dest);
        }

        internal static void OpenOrAttach(string dataDirectory, string recordId, IWin32Window owner)
        {
            if (String.IsNullOrEmpty(dataDirectory))
                throw new InvalidOperationException("Сначала откройте папку данных.");
            if (String.IsNullOrEmpty(recordId) || !RecordIds.LooksLikeId(recordId))
                throw new InvalidOperationException("Выберите строку с id записи (сохраните .dat после добавления).");

            if (Exists(dataDirectory, recordId))
            {
                DialogResult choice = MessageBox.Show(owner,
                    "Фото уже есть. Открыть текущее или заменить файлом?",
                    "Фото чека",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);
                if (choice == DialogResult.Cancel)
                    return;
                if (choice == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(PathForId(dataDirectory, recordId));
                    return;
                }
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Фото чека для " + recordId;
                dialog.Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp|Все файлы|*.*";
                if (dialog.ShowDialog(owner) != DialogResult.OK)
                    return;
                SaveFromFile(dataDirectory, recordId, dialog.FileName);
            }
        }

        internal static void AddFolderToZip(ZipArchive archive, string dataDirectory)
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
                archive.CreateEntryFromFile(files[i], FolderName + "/" + name, CompressionLevel.Optimal);
            }
        }

        internal static void ExtractReceiptsFromDirectory(string extractDirectory)
        {
            // LoadZip already extracts the whole archive, including Receipts/.
            // Ensure folder exists even if empty.
            if (Directory.Exists(extractDirectory))
                EnsureFolder(extractDirectory);
        }

        internal static bool IsSafeReceiptFileName(string name)
        {
            if (String.IsNullOrEmpty(name) || name.IndexOf("..") >= 0)
                return false;
            if (!name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                return false;
            return RecordIds.LooksLikeId(Path.GetFileNameWithoutExtension(name));
        }

        private static Bitmap ScaleDown(Image source, int maxEdge)
        {
            int w = source.Width;
            int h = source.Height;
            if (w <= maxEdge && h <= maxEdge)
                return new Bitmap(source);
            float scale = Math.Min((float)maxEdge / w, (float)maxEdge / h);
            int nw = Math.Max(1, (int)(w * scale));
            int nh = Math.Max(1, (int)(h * scale));
            Bitmap result = new Bitmap(nw, nh);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(source, 0, 0, nw, nh);
            }
            return result;
        }

        private static ImageCodecInfo FindJpegCodec()
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            for (int i = 0; i < codecs.Length; i++)
                if (codecs[i].FormatID == ImageFormat.Jpeg.Guid)
                    return codecs[i];
            return null;
        }
    }
}
