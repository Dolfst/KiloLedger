using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Alfa156.Desktop.Data;

namespace Alfa156.Desktop
{
    internal sealed class SyncOptionsForm : Form
    {
        private readonly CheckBox addBox = new CheckBox();
        private readonly CheckBox removeBox = new CheckBox();
        private readonly ComboBox conflictBox = new ComboBox();
        public SyncPlan Plan { get; private set; }

        public SyncOptionsForm(List<SyncTableDiff> diffs)
        {
            Plan = new SyncPlan();
            Text = "Синхронизация с Google";
            Width = 640;
            Height = 420;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(245, 247, 250);
            Font = new Font("Segoe UI", 9F);

            TextBox summary = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Left = 12,
                Top = 12,
                Width = 600,
                Height = 180,
                Text = SyncMerge.FormatSummary(diffs)
            };

            addBox.Text = "Добавить записи, которые есть только в Google";
            addBox.Checked = true;
            addBox.Left = 12;
            addBox.Top = 210;
            addBox.Width = 600;

            removeBox.Text = "Удалить локальные записи, которых нет в Google";
            removeBox.Checked = false;
            removeBox.Left = 12;
            removeBox.Top = 240;
            removeBox.Width = 600;

            Label conflictLabel = new Label
            {
                Text = "Если одна и та же запись отличается:",
                Left = 12,
                Top = 275,
                Width = 280
            };
            conflictBox.Left = 300;
            conflictBox.Top = 272;
            conflictBox.Width = 310;
            conflictBox.DropDownStyle = ComboBoxStyle.DropDownList;
            conflictBox.Items.AddRange(new object[]
            {
                "Оставить локальную версию",
                "Взять версию из Google",
                "Склеить поля (пустые дополнить)"
            });
            conflictBox.SelectedIndex = 0;

            Button ok = new Button
            {
                Text = "Синхронизировать",
                Left = 360,
                Top = 330,
                Width = 140,
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(49, 77, 102),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            Button cancel = new Button
            {
                Text = "Отмена",
                Left = 510,
                Top = 330,
                Width = 100,
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(summary);
            Controls.Add(addBox);
            Controls.Add(removeBox);
            Controls.Add(conflictLabel);
            Controls.Add(conflictBox);
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                Plan.AddFromGoogle = addBox.Checked;
                Plan.RemoveMissingInGoogle = removeBox.Checked;
                if (conflictBox.SelectedIndex == 1)
                    Plan.ConflictPolicy = SyncConflictPolicy.PreferIncoming;
                else if (conflictBox.SelectedIndex == 2)
                    Plan.ConflictPolicy = SyncConflictPolicy.MergeFields;
                else
                    Plan.ConflictPolicy = SyncConflictPolicy.PreferLocal;
            }
            base.OnFormClosing(e);
        }
    }
}
