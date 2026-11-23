using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using KiloLedger.Desktop.Data;

using KiloLedger.Localization;
namespace KiloLedger.Desktop
{
    internal sealed class SyncOptionsForm : Form
    {
        private readonly KiloLedgerRepository local;
        private readonly KiloLedgerRepository remote;
        private readonly string safetyPath;
        private readonly TextBox summary = new TextBox();
        private readonly CheckBox addBox = new CheckBox();
        private readonly CheckBox removeBox = new CheckBox();
        private readonly ComboBox conflictBox = new ComboBox();
        public SyncPlan Plan { get; private set; }

        public SyncOptionsForm(KiloLedgerRepository local, KiloLedgerRepository remote, string safetyPath)
        {
            this.local = local;
            this.remote = remote;
            this.safetyPath = safetyPath;
            Plan = new SyncPlan();
            Text = L10n.Get("title.google_sync_preview");
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

            addBox.Text = L10n.Get("sync.add_google_only");
            addBox.Checked = true;
            addBox.Left = 12;
            addBox.Top = 286;
            addBox.Width = 640;
            addBox.CheckedChanged += RefreshPreview;

            removeBox.Text = L10n.Get("sync.delete_local_missing_google");
            removeBox.Checked = false;
            removeBox.Left = 12;
            removeBox.Top = 316;
            removeBox.Width = 640;
            removeBox.CheckedChanged += RefreshPreview;

            Label conflictLabel = new Label
            {
                Text = L10n.Get("sync.if_same_differs"),
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
                L10n.Get("sync.keep_local_version"),
                L10n.Get("sync.take_google_version"),
                L10n.Get("sync.merge_fields_fill_empty")
            });
            conflictBox.SelectedIndex = 0;
            conflictBox.SelectedIndexChanged += RefreshPreview;

            Button ok = new Button
            {
                Text = L10n.Get("button.apply"),
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
                Text = L10n.Get("button.cancel"),
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
                ("\r\n\r\n" + L10n.Get("sync.preview_uses_options"));
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
