using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Alfa156.Desktop.Data;
using Alfa156.Desktop.Exchange;

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
            Text = "Мастер настройки Google Sync";
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
                Text = "Назад",
                Left = 280,
                Top = 340,
                Width = 100,
                Height = 30
            };
            nextButton = new Button
            {
                Text = "Далее",
                Left = 390,
                Top = 340,
                Width = 110,
                Height = 30
            };
            cancelButton = new Button
            {
                Text = "Отмена",
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
            stepLabel.Text = "Шаг 1 из 3 — подготовка Apps Script";
            nextButton.Text = "Далее";

            Label body = new Label
            {
                Left = 12,
                Top = 12,
                Width = 600,
                Height = 180,
                Text =
                    "Google Sync связывает ПК и Android с таблицей через Apps Script.\r\n\r\n" +
                    "1. Откройте таблицу «Деньги» в Google Sheets.\r\n" +
                    "2. Расширения → Apps Script — вставьте код из google-sync/Alfa156Sync.gs.\r\n" +
                    "3. Project Settings → Script properties: ключ SYNC_TOKEN (секрет).\r\n" +
                    "4. Deploy → New deployment → Web app (Execute as: Me, Anyone).\r\n" +
                    "5. Скопируйте URL веб-приложения — он понадобится на следующем шаге.\r\n\r\n" +
                    "Подробная инструкция — в README репозитория."
            };

            LinkLabel link = new LinkLabel
            {
                Left = 12,
                Top = 210,
                Width = 600,
                Height = 24,
                Text = "Открыть google-sync/README.md на GitHub"
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
                    MessageBox.Show(this, "Не удалось открыть ссылку: " + ex.Message,
                        "Google Sync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            stepHost.Controls.Add(body);
            stepHost.Controls.Add(link);
        }

        private void BuildStep2()
        {
            stepLabel.Text = "Шаг 2 из 3 — URL и токен";
            nextButton.Text = "Далее";

            Label urlLabel = new Label
            {
                Left = 12,
                Top = 16,
                Width = 600,
                Text = "URL веб-приложения Apps Script"
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
                Text = "SYNC_TOKEN (как в Script properties)"
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
                Text = "Ссылка на таблицу (необязательно, для импорта CSV)"
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
                Text = "Значения сохраняются в %AppData%\\Alfa156\\google-sync.json. " +
                    "Расширенную настройку по-прежнему можно открыть кнопкой «Google: настройка»."
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
            stepLabel.Text = "Шаг 3 из 3 — проверка связи";
            nextButton.Text = "Готово";

            Label body = new Label
            {
                Left = 12,
                Top = 16,
                Width = 600,
                Height = 60,
                Text = "Нажмите «Проверить соединение», чтобы выполнить pull из Apps Script. " +
                    "Если всё настроено, появятся числа строк из таблицы."
            };

            Button testButton = new Button
            {
                Left = 12,
                Top = 90,
                Width = 180,
                Height = 32,
                Text = "Проверить соединение"
            };
            testButton.Click += TestConnectionClicked;

            testResultLabel = new Label
            {
                Left = 12,
                Top = 140,
                Width = 600,
                Height = 100,
                Text = "Результат проверки появится здесь."
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
                MessageBox.Show(this, "Сначала заполните URL и SYNC_TOKEN на шаге 2.",
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
                MessageBox.Show(this, "Укажите URL веб-приложения Apps Script.",
                    "Google Sync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (String.IsNullOrEmpty(token))
            {
                MessageBox.Show(this, "Укажите SYNC_TOKEN.",
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
                MessageBox.Show(this, "Сначала заполните URL и SYNC_TOKEN на шаге 2.",
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
                testResultLabel.Text = "Ошибка: " + ex.Message;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }
}
