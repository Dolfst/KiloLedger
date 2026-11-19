using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Alfa156.Desktop.Data;

namespace Alfa156.Desktop
{
    internal sealed class SyncOptionsForm : Form
    {
        private readonly AlfaRepository local;
        private readonly AlfaRepository remote;
        private readonly string safetyPath;
        private readonly TextBox summary = new TextBox();
        private readonly CheckBox addBox = new CheckBox();
        private readonly CheckBox removeBox = new CheckBox();
        private readonly ComboBox conflictBox = new ComboBox();
        public SyncPlan Plan { get; private set; }

        public SyncOptionsForm(AlfaRepository local, AlfaRepository remote, string safetyPath)
        {
            this.local = local;
            this.remote = remote;
            this.safetyPath = safetyPath;
            Plan = new SyncPlan();
            Text = "Предпросмотр синхронизации с Google";
            Width = 680;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(245, 247, 250);
            Font = new Font("Segoe UI", 9F);

            summary.Multiline = true;
            summary.ReadOnly = true;
            summary.ScrollBars = ScrollBars.Vertical;
            summary.Left = 12;
            summary.Top = 12;
            summary.Width = 640;
            summary.Height = 260;

            addBox.Text = "Добавить записи, которые есть только в Google";
            addBox.Checked = true;
            addBox.Left = 12;
            addBox.Top = 286;
            addBox.Width = 640;
            addBox.CheckedChanged += RefreshPreview;

            removeBox.Text = "Удалить локальные записи, которых нет в Google";
            removeBox.Checked = false;
            removeBox.Left = 12;
            removeBox.Top = 316;
            removeBox.Width = 640;
            removeBox.CheckedChanged += RefreshPreview;

            Label conflictLabel = new Label
            {
                Text = "Если одна и та же запись отличается:",
                Left = 12,
                Top = 350,
                Width = 280
            };
            conflictBox.Left = 300;
            conflictBox.Top = 347;
            conflictBox.Width = 350;
            conflictBox.DropDownStyle = ComboBoxStyle.DropDownList;
            conflictBox.Items.AddRange(new object[]
            {
                "Оставить локальную версию",
                "Взять версию из Google",
                "Склеить поля (пустые дополнить)"
            });
            conflictBox.SelectedIndex = 0;
            conflictBox.SelectedIndexChanged += RefreshPreview;

            Button ok = new Button
            {
                Text = "Применить",
                Left = 400,
                Top = 430,
                Width = 140,
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(49, 77, 102),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            Button cancel = new Button
            {
                Text = "Отмена",
                Left = 550,
                Top = 430,
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
            RefreshPreview(null, EventArgs.Empty);
        }

        private void RefreshPreview(object sender, EventArgs e)
        {
            SyncPlan draft = ReadDraftPlan();
            List<SyncTableDiff> diffs = SyncMerge.Analyze(local, remote);
            SyncJournal preview = SyncMerge.BuildPreview(local, remote, draft);
            preview.SafetyPath = safetyPath;
            summary.Text = SyncMerge.FormatSummary(diffs) + "\r\n\r\n" + preview.FormatPreview() +
                "\r\n\r\n(предпросмотр учитывает выбранные опции выше)";
        }

        private SyncPlan ReadDraftPlan()
        {
            SyncPlan draft = new SyncPlan();
            draft.AddFromGoogle = addBox.Checked;
            draft.RemoveMissingInGoogle = removeBox.Checked;
            if (conflictBox.SelectedIndex == 1)
                draft.ConflictPolicy = SyncConflictPolicy.PreferIncoming;
            else if (conflictBox.SelectedIndex == 2)
                draft.ConflictPolicy = SyncConflictPolicy.MergeFields;
            else
                draft.ConflictPolicy = SyncConflictPolicy.PreferLocal;
            return draft;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
                Plan = ReadDraftPlan();
            base.OnFormClosing(e);
        }
    }
}
