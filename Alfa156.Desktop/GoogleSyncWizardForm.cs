using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Alfa156.Desktop.Data;
using Alfa156.Desktop.Exchange;

using Alfa156.Localization;
namespace Alfa156.Desktop
{
    /// <summary>
    /// Пошаговая настройка Google Sync: инструкции → URL/токен → проверка связи.
    /// </summary>
    internal sealed class GoogleSyncWizardForm : Form
    {
        private const string ReadmeUrl =
            "https://github.com/Dolfst/alfa/blob/main/google-sync/README.md";

        private readonly GoogleSyncSettings settings;
        private int step;
        private readonly Label stepLabel;
        private readonly Panel stepHost;
        private readonly Button backButton;
        private readonly Button nextButton;
        private readonly Button cancelButton;

        private TextBox urlBox;
        private TextBox tokenBox;
        private TextBox sheetBox;
        private Label testResultLabel;

        internal GoogleSyncWizardForm(GoogleSyncSettings existing)
        {
            settings = existing ?? new GoogleSyncSettings();
            Text = L10n.Get("title.google_sync_wizard");
            Width = 680;
            Height = 420;
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
                Width = 630,
                Height = 24,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 51, 70)
            };

            stepHost = new Panel
            {
                Left = 16,
                Top = 44,
                Width = 630,
                Height = 280,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            backButton = new Button
            {
                Text = L10n.Get("button.back"),
                Left = 280,
                Top = 340,
                Width = 100,
                Height = 30
            };
            nextButton = new Button
            {
                Text = L10n.Get("button.next"),
                Left = 390,
                Top = 340,
                Width = 110,
                Height = 30
            };
            cancelButton = new Button
            {
                Text = L10n.Get("button.cancel"),
                Left = 510,
                Top = 340,
                Width = 100,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };

            backButton.Click += delegate { GoTo(step - 1); };
            nextButton.Click += NextClicked;
            CancelButton = cancelButton;

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
                BuildStep1();
            else if (step == 1)
                BuildStep2();
            else
                BuildStep3();
        }

        private void BuildStep1()
        {
            stepLabel.Text = L10n.Get("wizard.gs_step1_title");
            nextButton.Text = L10n.Get("button.next");

            Label body = new Label
            {
                Left = 12,
                Top = 12,
                Width = 600,
                Height = 180,
                Text =
                    (L10n.Get("wizard.gs_intro") + "\r\n\r\n") +
                    (L10n.Get("wizard.gs_open_sheet_alt") + "\r\n") +
                    (L10n.Get("wizard.gs_step2_paste") + "\r\n") +
                    (L10n.Get("wizard.gs_step3_token") + "\r\n") +
                    "4. Deploy → New deployment → Web app (Execute as: Me, Anyone).\r\n" +
                    (L10n.Get("wizard.gs_step5_copy_url") + "\r\n\r\n") +
                    L10n.Get("hint.detailed_readme")
            };

            LinkLabel link = new LinkLabel
            {
                Left = 12,
                Top = 210,
                Width = 600,
                Height = 24,
                Text = L10n.Get("button.open_google_sync_readme")
            };
            link.LinkClicked += delegate
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = ReadmeUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, (L10n.Get("error.open_link_failed") + " ") + ex.Message,
                        "Google Sync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            stepHost.Controls.Add(body);
            stepHost.Controls.Add(link);
        }

        private void BuildStep2()
        {
            stepLabel.Text = L10n.Get("wizard.gs_step2_title");
            nextButton.Text = L10n.Get("button.next");

            Label urlLabel = new Label
            {
                Left = 12,
                Top = 16,
                Width = 600,
                Text = L10n.Get("google.script_url_label")
            };
            urlBox = new TextBox
            {
                Left = 12,
                Top = 40,
                Width = 600,
                Text = settings.ScriptUrl ?? ""
            };

            Label tokenLabel = new Label
            {
                Left = 12,
                Top = 80,
                Width = 600,
                Text = L10n.Get("google.sync_token_hint")
            };
            tokenBox = new TextBox
            {
                Left = 12,
                Top = 104,
                Width = 600,
                Text = settings.Token ?? ""
            };

            Label sheetLabel = new Label
            {
                Left = 12,
                Top = 144,
                Width = 600,
                Text = L10n.Get("label.sheet_url_csv_opt")
            };
            sheetBox = new TextBox
            {
                Left = 12,
                Top = 168,
                Width = 600,
                Text = settings.SpreadsheetUrl ?? ""
            };

            Label hint = new Label
            {
                Left = 12,
                Top = 210,
                Width = 600,
                Height = 40,
                ForeColor = Color.FromArgb(70, 90, 110),
                Text = (L10n.Get("hint.google_sync_json_path_2") + " ") +
                    L10n.Get("wizard.advanced_settings_hint")
            };

            stepHost.Controls.Add(urlLabel);
            stepHost.Controls.Add(urlBox);
            stepHost.Controls.Add(tokenLabel);
            stepHost.Controls.Add(tokenBox);
            stepHost.Controls.Add(sheetLabel);
            stepHost.Controls.Add(sheetBox);
            stepHost.Controls.Add(hint);
        }

        private void BuildStep3()
        {
            urlBox = null;
            tokenBox = null;
            sheetBox = null;
            stepLabel.Text = L10n.Get("wizard.gs_step3_title");
            nextButton.Text = L10n.Get("button.done");

            Label body = new Label
            {
                Left = 12,
                Top = 16,
                Width = 600,
                Height = 60,
                Text = (L10n.Get("wizard.click_test_conn") + " ") +
                    L10n.Get("wizard.test_conn_hint")
            };

            Button testButton = new Button
            {
                Left = 12,
                Top = 90,
                Width = 180,
                Height = 32,
                Text = L10n.Get("button.test_connection")
            };
            testButton.Click += TestConnectionClicked;

            testResultLabel = new Label
            {
                Left = 12,
                Top = 140,
                Width = 600,
                Height = 100,
                Text = L10n.Get("wizard.test_result_placeholder")
            };

            stepHost.Controls.Add(body);
            stepHost.Controls.Add(testButton);
            stepHost.Controls.Add(testResultLabel);
        }

        private void NextClicked(object sender, EventArgs e)
        {
            if (step == 0)
            {
                GoTo(1);
                return;
            }
            if (step == 1)
            {
                if (!SaveStep2())
                    return;
                GoTo(2);
                return;
            }
            if (String.IsNullOrEmpty(settings.ScriptUrl) || String.IsNullOrEmpty(settings.Token))
            {
                MessageBox.Show(this, L10n.Get("wizard.fill_url_token_step2"),
                    "Google Sync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                GoTo(1);
                return;
            }
            settings.Save();
            DialogResult = DialogResult.OK;
            Close();
        }

        private bool SaveStep2()
        {
            string url = urlBox == null ? (settings.ScriptUrl ?? "") : urlBox.Text.Trim();
            string token = tokenBox == null ? (settings.Token ?? "") : tokenBox.Text.Trim();
            string sheet = sheetBox == null ? (settings.SpreadsheetUrl ?? "") : sheetBox.Text.Trim();
            if (String.IsNullOrEmpty(url))
            {
                MessageBox.Show(this, L10n.Get("error.specify_apps_script_url"),
                    "Google Sync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (String.IsNullOrEmpty(token))
            {
                MessageBox.Show(this, L10n.Get("error.specify_sync_token"),
                    "Google Sync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            settings.ScriptUrl = url;
            settings.Token = token;
            settings.SpreadsheetUrl = sheet;
            settings.Save();
            return true;
        }

        private void TestConnectionClicked(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(settings.ScriptUrl) || String.IsNullOrEmpty(settings.Token))
            {
                MessageBox.Show(this, L10n.Get("wizard.fill_url_token_step2"),
                    "Google Sync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                GoTo(1);
                return;
            }
            try
            {
                Cursor = Cursors.WaitCursor;
                string message = GoogleSyncClient.TestConnection(settings);
                testResultLabel.ForeColor = Color.FromArgb(20, 110, 60);
                testResultLabel.Text = message;
            }
            catch (Exception ex)
            {
                testResultLabel.ForeColor = Color.FromArgb(150, 30, 30);
                testResultLabel.Text = (L10n.Get("label.error_colon") + " ") + ex.Message;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }
}
