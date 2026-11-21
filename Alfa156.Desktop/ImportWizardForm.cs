using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Alfa156.Desktop.Data;
using Alfa156.Desktop.Exchange;

using Alfa156.Localization;
namespace Alfa156.Desktop
{
    /// <summary>
    /// Мастер «Импорт»: ZIP / папка .dat / CSV / Excel → предпросмотр счётчиков → применение с Safety_.
    /// </summary>
    internal sealed class ImportWizardForm : Form
    {
        internal enum SourceKind
        {
            Zip,
            DatFolder,
            CsvFolder,
            Excel
        }

        private readonly AlfaRepository repository;
        private readonly Action ensurePathForImport;

        private int step;
        private SourceKind kind = SourceKind.Zip;
        private string sourcePath = "";
        private string zipExtractDir;
        private ImportPreview preview;
        private string safetyPath;

        private readonly Label stepLabel;
        private readonly Panel stepHost;
        private readonly Button backButton;
        private readonly Button nextButton;
        private readonly Button cancelButton;

        private RadioButton zipRadio;
        private RadioButton folderRadio;
        private RadioButton csvRadio;
        private RadioButton excelRadio;
        private Label pathLabel;
        private TextBox previewBox;

        /// <summary>Safety copy path created on apply (may be null if there was nothing to back up).</summary>
        internal string SafetyPath
        {
            get { return safetyPath; }
        }

        internal ImportWizardForm(AlfaRepository repo, Action ensurePath)
        {
            repository = repo;
            ensurePathForImport = ensurePath;

            Text = L10n.Get("title.import");
            Width = 700;
            Height = 460;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(245, 247, 250);
            Font = new Font("Segoe UI", 9.5F);

            stepLabel = new Label
            {
                Left = 16,
                Top = 12,
                Width = 650,
                Height = 24,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 51, 70)
            };

            stepHost = new Panel
            {
                Left = 16,
                Top = 44,
                Width = 650,
                Height = 310,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            backButton = new Button
            {
                Text = L10n.Get("button.back"),
                Left = 300,
                Top = 370,
                Width = 100,
                Height = 30
            };
            nextButton = new Button
            {
                Text = L10n.Get("button.next"),
                Left = 410,
                Top = 370,
                Width = 120,
                Height = 30
            };
            cancelButton = new Button
            {
                Text = L10n.Get("button.cancel"),
                Left = 540,
                Top = 370,
                Width = 100,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };

            backButton.Click += delegate { GoTo(step - 1); };
            nextButton.Click += NextClicked;
            CancelButton = cancelButton;
            FormClosed += delegate { CleanupExtract(); };

            Controls.Add(stepLabel);
            Controls.Add(stepHost);
            Controls.Add(backButton);
            Controls.Add(nextButton);
            Controls.Add(cancelButton);

            GoTo(0);
        }

        private void GoTo(int index)
        {
            if (index < 0) index = 0;
            if (index > 2) index = 2;
            step = index;
            stepHost.Controls.Clear();
            backButton.Enabled = step > 0;

            if (step == 0)
                BuildStepChoose();
            else if (step == 1)
                BuildStepPath();
            else
                BuildStepPreview();
        }

        private void BuildStepChoose()
        {
            stepLabel.Text = L10n.Get("wizard.imp_step1_title");
            nextButton.Text = L10n.Get("button.next");

            Label hint = new Label
            {
                Left = 12,
                Top = 12,
                Width = 620,
                Height = 40,
                Text = (L10n.Get("wizard.choose_format_hint") + " ") +
                    L10n.Get("wizard.preview_then_safety_copy")
            };

            zipRadio = MakeRadio(L10n.Get("import.zip_desc"), 60, SourceKind.Zip);
            folderRadio = MakeRadio(L10n.Get("import.dat_folder_label"), 100, SourceKind.DatFolder);
            csvRadio = MakeRadio(L10n.Get("import.csv_folder_desc"), 140, SourceKind.CsvFolder);
            excelRadio = MakeRadio("Excel (.xlsx)", 180, SourceKind.Excel);

            zipRadio.Checked = kind == SourceKind.Zip;
            folderRadio.Checked = kind == SourceKind.DatFolder;
            csvRadio.Checked = kind == SourceKind.CsvFolder;
            excelRadio.Checked = kind == SourceKind.Excel;

            stepHost.Controls.Add(hint);
            stepHost.Controls.Add(zipRadio);
            stepHost.Controls.Add(folderRadio);
            stepHost.Controls.Add(csvRadio);
            stepHost.Controls.Add(excelRadio);
        }

        private RadioButton MakeRadio(string text, int top, SourceKind value)
        {
            RadioButton radio = new RadioButton
            {
                Left = 20,
                Top = top,
                Width = 600,
                Height = 28,
                Text = text,
                Tag = value
            };
            radio.CheckedChanged += delegate
            {
                if (radio.Checked)
                    kind = value;
            };
            return radio;
        }

        private void BuildStepPath()
        {
            stepLabel.Text = L10n.Get("wizard.imp_step2_title");
            nextButton.Text = L10n.Get("button.next");

            string kindText =
                kind == SourceKind.Zip ? "ZIP-файл с резервной копией" :
                kind == SourceKind.DatFolder ? "Папка с файлами cars.dat, fuel.dat, …" :
                kind == SourceKind.CsvFolder ? "Папка с CSV (cars.csv, fuel.csv, …)" :
                L10n.Get("import.excel_file_label");

            Label hint = new Label
            {
                Left = 12,
                Top = 12,
                Width = 620,
                Height = 36,
                Text = kindText
            };

            pathLabel = new Label
            {
                Left = 12,
                Top = 56,
                Width = 620,
                Height = 48,
                ForeColor = Color.FromArgb(35, 51, 70),
                Text = String.IsNullOrEmpty(sourcePath) ? L10n.Get("common.not_selected_yet") : sourcePath
            };

            Button browse = new Button
            {
                Left = 12,
                Top = 112,
                Width = 160,
                Height = 30,
                Text = L10n.Get("button.browse")
            };
            browse.Click += BrowseClicked;

            stepHost.Controls.Add(hint);
            stepHost.Controls.Add(pathLabel);
            stepHost.Controls.Add(browse);

            if (kind == SourceKind.CsvFolder)
            {
                Button templates = new Button
                {
                    Left = 184,
                    Top = 112,
                    Width = 220,
                    Height = 30,
                    Text = L10n.Get("button.download_csv_templates")
                };
                templates.Click += ExportTemplatesClicked;
                Label templateHint = new Label
                {
                    Left = 12,
                    Top = 160,
                    Width = 620,
                    Height = 60,
                    ForeColor = Color.FromArgb(70, 90, 110),
                    Text = (L10n.Get("hint.csv_templates_desc") + " ") +
                        L10n.Get("hint.csv_templates_usage")
                };
                stepHost.Controls.Add(templates);
                stepHost.Controls.Add(templateHint);
            }
        }

        private void BuildStepPreview()
        {
            stepLabel.Text = L10n.Get("wizard.imp_step3_title");
            nextButton.Text = L10n.Get("button.apply");

            previewBox = new TextBox
            {
                Left = 12,
                Top = 12,
                Width = 620,
                Height = 220,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9.5F),
                Text = preview != null ? preview.FormatSummary() : ""
            };

            Label warn = new Label
            {
                Left = 12,
                Top = 244,
                Width = 620,
                Height = 48,
                ForeColor = Color.FromArgb(120, 50, 40),
                Text = (L10n.Get("wizard.apply_replaces_folder") + " ") +
                    L10n.Get("hint.safety_copy_if_cars_exists")
            };

            stepHost.Controls.Add(previewBox);
            stepHost.Controls.Add(warn);
        }

        private void NextClicked(object sender, EventArgs e)
        {
            try
            {
                if (step == 0)
                {
                    sourcePath = "";
                    preview = null;
                    CleanupExtract();
                    GoTo(1);
                    return;
                }
                if (step == 1)
                {
                    if (String.IsNullOrEmpty(sourcePath))
                    {
                        MessageBox.Show(this, L10n.Get("error.select_file_or_folder_first"), L10n.Get("title.import"),
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    Cursor = Cursors.WaitCursor;
                    try
                    {
                        preview = BuildPreview();
                    }
                    finally
                    {
                        Cursor = Cursors.Default;
                    }
                    GoTo(2);
                    return;
                }
                // step 2 — apply
                ApplyImport();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, (L10n.Get("label.import_colon") + " ") + ex.Message, L10n.Get("title.import"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BrowseClicked(object sender, EventArgs e)
        {
            if (kind == SourceKind.Zip)
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Filter = L10n.Get("filter.zip_files");
                    dialog.Title = L10n.Get("prompt.zip_with_backup");
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    sourcePath = dialog.FileName;
                }
            }
            else if (kind == SourceKind.Excel)
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Excel (*.xlsx)|*.xlsx";
                    dialog.Title = "Excel Alfa156";
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    sourcePath = dialog.FileName;
                }
            }
            else
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = kind == SourceKind.CsvFolder
                        ? L10n.Get("import.csv_files_desc_short")
                        : L10n.Get("import.dat_files_desc_short");
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    sourcePath = dialog.SelectedPath;
                }
            }
            if (pathLabel != null)
                pathLabel.Text = sourcePath;
        }

        private void ExportTemplatesClicked(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = L10n.Get("prompt.where_save_csv_templates");
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    string folder = Path.Combine(dialog.SelectedPath, "Alfa156-csv-templates");
                    CsvExchange.ExportTemplates(folder);
                    MessageBox.Show(this,
                        (L10n.Get("msg.templates_saved_to") + "\r\n") + folder +
                        ("\r\n\r\n" + L10n.Get("msg.csv_templates_headers_only")),
                        L10n.Get("button.csv_templates"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, L10n.Get("button.csv_templates"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private ImportPreview BuildPreview()
        {
            if (kind == SourceKind.Zip)
            {
                CleanupExtract();
                zipExtractDir = Path.Combine(Path.GetTempPath(),
                    "Alfa156_import_" + Guid.NewGuid().ToString("N"));
                return ImportPreview.FromZip(sourcePath, zipExtractDir);
            }
            if (kind == SourceKind.DatFolder)
                return ImportPreview.FromDatFolder(sourcePath);
            if (kind == SourceKind.CsvFolder)
                return ImportPreview.FromCsvFolder(sourcePath);
            return ImportPreview.FromExcel(sourcePath);
        }

        private void ApplyImport()
        {
            if (ensurePathForImport != null)
                ensurePathForImport();
            if (String.IsNullOrEmpty(repository.DirectoryPath))
                throw new InvalidOperationException(L10n.Get("error.no_data_folder_selected"));

            safetyPath = null;
            if (File.Exists(Path.Combine(repository.DirectoryPath, "cars.dat")))
                safetyPath = repository.CreateSafetyBackup(null);

            if (kind == SourceKind.Zip || kind == SourceKind.DatFolder)
            {
                string dataRoot = kind == SourceKind.Zip
                    ? ImportPreview.ResolveDataRoot(zipExtractDir)
                    : sourcePath;
                AlfaCodec.ValidateDirectory(dataRoot);
                CopyDatTree(dataRoot, repository.DirectoryPath);
                repository.LoadDirectory(repository.DirectoryPath);
            }
            else if (kind == SourceKind.CsvFolder)
            {
                CsvExchange.ImportFolder(repository, sourcePath);
                repository.Save();
            }
            else
            {
                XlsxExchange.Import(repository, sourcePath);
                repository.Save();
            }
        }

        private static void CopyDatTree(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            // Core .dat always present after ValidateDirectory.
            for (int i = 0; i < AlfaCodec.CoreDataFiles.Length; i++)
            {
                string name = AlfaCodec.CoreDataFiles[i];
                File.Copy(Path.Combine(source, name), Path.Combine(destination, name), true);
            }
            // Optional: copy when present in source; otherwise leave local file
            // (Android CopyData uses the same pattern for expenses/meta/templates/…).
            string[] optional = new string[]
            {
                AlfaCodec.RepairCategoriesFile, AlfaCodec.ExpensesFile, AlfaCodec.ExpenseCategoriesFile,
                AlfaCodec.SettingsFile, AlfaCodec.CarsMetaFile, AlfaCodec.TemplatesFile,
                AlfaCodec.RemindersFile, RecordIds.DeletedFile
            };
            for (int i = 0; i < optional.Length; i++)
            {
                string src = Path.Combine(source, optional[i]);
                if (File.Exists(src))
                    File.Copy(src, Path.Combine(destination, optional[i]), true);
            }

            // Replace Receipts/ only when the source snapshot includes that folder.
            string srcReceipts = Path.Combine(source, "Receipts");
            if (Directory.Exists(srcReceipts))
            {
                string dstReceipts = Path.Combine(destination, "Receipts");
                if (Directory.Exists(dstReceipts))
                    Directory.Delete(dstReceipts, true);
                Directory.CreateDirectory(dstReceipts);
                string[] files = Directory.GetFiles(srcReceipts);
                for (int i = 0; i < files.Length; i++)
                    File.Copy(files[i], Path.Combine(dstReceipts, Path.GetFileName(files[i])), true);
            }
        }

        private void CleanupExtract()
        {
            if (!String.IsNullOrEmpty(zipExtractDir) && Directory.Exists(zipExtractDir))
            {
                try { Directory.Delete(zipExtractDir, true); }
                catch { /* temp cleanup best-effort */ }
            }
            zipExtractDir = null;
        }
    }
}
