using System;
using System.Drawing;
using System.Windows.Forms;

using Alfa156.Localization;
namespace CarExpenses
{
    internal sealed class SyncOptionsForm : Form
    {
        private readonly Label summaryLabel;
        private readonly TextBox summaryBox;
        private readonly CheckBox addBox;
        private readonly CheckBox removeBox;
        private readonly Label conflictLabel;
        private readonly ComboBox conflictBox;
        private SyncPlan plan;

        internal SyncOptionsForm(string summary)
        {
            plan = new SyncPlan();
            Text = L10n.Get("title.preview");
            // Design size for QVGA; Maximize + LayoutControls adapt to any resolution (QVGA/VGA).
            ClientSize = new Size(240, 268);
            AutoScroll = false;
            BackColor = Color.FromArgb(232, 238, 244);
            MinimizeBox = false;
            WindowState = FormWindowState.Maximized;

            summaryLabel = new Label();
            summaryLabel.Text = L10n.Get("sync.merge_preview_colon");
            summaryLabel.BackColor = BackColor;
            summaryLabel.ForeColor = Color.FromArgb(35, 51, 70);
            Controls.Add(summaryLabel);

            summaryBox = new TextBox();
            summaryBox.Multiline = true;
            summaryBox.ReadOnly = true;
            summaryBox.ScrollBars = ScrollBars.Vertical;
            summaryBox.BackColor = Color.White;
            summaryBox.ForeColor = Color.FromArgb(35, 51, 70);
            summaryBox.Text = summary == null ? "" : summary;
            Controls.Add(summaryBox);

            addBox = new CheckBox();
            addBox.Text = L10n.Get("sync.add_android_only");
            addBox.Checked = true;
            addBox.BackColor = BackColor;
            addBox.ForeColor = Color.FromArgb(35, 51, 70);
            Controls.Add(addBox);

            removeBox = new CheckBox();
            removeBox.Text = L10n.Get("sync.delete_missing_android");
            removeBox.Checked = false;
            removeBox.BackColor = BackColor;
            removeBox.ForeColor = Color.FromArgb(35, 51, 70);
            Controls.Add(removeBox);

            conflictLabel = new Label();
            conflictLabel.Text = L10n.Get("sync.if_record_differs");
            conflictLabel.BackColor = BackColor;
            conflictLabel.ForeColor = Color.FromArgb(35, 51, 70);
            Controls.Add(conflictLabel);

            conflictBox = new ComboBox();
            conflictBox.DropDownStyle = ComboBoxStyle.DropDownList;
            conflictBox.BackColor = Color.White;
            conflictBox.ForeColor = Color.FromArgb(35, 51, 70);
            conflictBox.Items.Add(L10n.Get("sync.keep_pda"));
            conflictBox.Items.Add(L10n.Get("sync.take_android"));
            conflictBox.Items.Add(L10n.Get("sync.ask_every_time"));
            conflictBox.SelectedIndex = 0;
            Controls.Add(conflictBox);

            MainMenu menu = new MainMenu();
            menu.MenuItems.Add(CreateMenuItem(L10n.Get("button.apply"), new EventHandler(Accept)));
            menu.MenuItems.Add(CreateMenuItem(L10n.Get("button.cancel"), new EventHandler(Cancel)));
            Menu = menu;

            Resize += new EventHandler(FormResized);
            LayoutControls();
        }

        internal SyncPlan Plan
        {
            get { return plan; }
        }

        private void FormResized(object sender, EventArgs e)
        {
            LayoutControls();
        }

        private void LayoutControls()
        {
            int margin = Scale(8);
            int gap = Scale(6);
            int width = ClientSize.Width - margin * 2;
            if (width < 40)
                width = 40;

            // Compact when SIP / short client height so options stay on screen.
            bool compact = ClientSize.Height < Scale(240);
            float titleSize = ClientSize.Width >= 400 ? (compact ? 10F : 11F) : 9F;
            float bodySize = ClientSize.Width >= 400 ? (compact ? 9F : 10F) : 9F;
            Font titleFont = new Font("Arial", titleSize, FontStyle.Bold);
            Font bodyFont = new Font("Arial", bodySize, FontStyle.Regular);

            summaryLabel.Font = titleFont;
            summaryBox.Font = bodyFont;
            addBox.Font = bodyFont;
            removeBox.Font = bodyFont;
            conflictLabel.Font = bodyFont;
            conflictBox.Font = bodyFont;

            int labelHeight = compact ? Scale(18) : Scale(22);
            int checkHeight = compact ? Scale(24) : Scale(28);
            int comboHeight = compact ? Scale(24) : Scale(28);
            int optionsBlock =
                checkHeight + gap + checkHeight + gap + labelHeight + gap + comboHeight;
            int summaryTop = margin + labelHeight + gap;
            int availableForSummary = ClientSize.Height - summaryTop - optionsBlock - margin * 2;
            int summaryHeight = availableForSummary;
            if (summaryHeight < Scale(48))
                summaryHeight = Scale(48);

            int contentBottom = summaryTop + summaryHeight + margin + optionsBlock;
            AutoScroll = contentBottom > ClientSize.Height;

            summaryLabel.Location = new Point(margin, margin);
            summaryLabel.Size = new Size(width, labelHeight);

            summaryBox.Location = new Point(margin, summaryTop);
            summaryBox.Size = new Size(width, summaryHeight);

            int y = summaryTop + summaryHeight + margin;
            addBox.Location = new Point(margin, y);
            addBox.Size = new Size(width, checkHeight);

            y += checkHeight + gap;
            removeBox.Location = new Point(margin, y);
            removeBox.Size = new Size(width, checkHeight);

            y += checkHeight + gap;
            conflictLabel.Location = new Point(margin, y);
            conflictLabel.Size = new Size(width, labelHeight);

            y += labelHeight + gap;
            conflictBox.Location = new Point(margin, y);
            conflictBox.Size = new Size(width, comboHeight);
        }

        /// <summary>
        /// Scale a QVGA design pixel to the current form width (240 design → device width).
        /// </summary>
        private int Scale(int designPixels)
        {
            int scaled = designPixels * ClientSize.Width / 240;
            if (scaled < 1)
                return 1;
            return scaled;
        }

        private void Accept(object sender, EventArgs e)
        {
            plan.Cancelled = false;
            plan.AddIncoming = addBox.Checked;
            plan.RemoveLocalOnly = removeBox.Checked;
            if (conflictBox.SelectedIndex == 1)
                plan.ConflictPolicy = SyncConflictPolicy.PreferIncoming;
            else if (conflictBox.SelectedIndex == 2)
                plan.ConflictPolicy = SyncConflictPolicy.AskEach;
            else
                plan.ConflictPolicy = SyncConflictPolicy.PreferLocal;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Cancel(object sender, EventArgs e)
        {
            plan.Cancelled = true;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private static MenuItem CreateMenuItem(string text, EventHandler handler)
        {
            MenuItem item = new MenuItem();
            item.Text = text;
            item.Click += handler;
            return item;
        }
    }
}
