using System;
using System.Drawing;
using System.Windows.Forms;

namespace CarExpenses
{
    internal sealed class SyncOptionsForm : Form
    {
        private readonly TextBox summaryBox;
        private readonly CheckBox addBox;
        private readonly CheckBox removeBox;
        private readonly ComboBox conflictBox;
        private SyncPlan plan;

        internal SyncOptionsForm(string summary)
        {
            plan = new SyncPlan();
            Text = "Синхронизация";
            ClientSize = new Size(240, 268);
            AutoScroll = true;
            BackColor = Color.FromArgb(232, 238, 244);
            MinimizeBox = false;
            WindowState = FormWindowState.Maximized;

            Label summaryLabel = new Label();
            summaryLabel.Text = "Сравнение данных:";
            summaryLabel.Location = new Point(8, 8);
            summaryLabel.Size = new Size(220, 16);
            Controls.Add(summaryLabel);

            summaryBox = new TextBox();
            summaryBox.Multiline = true;
            summaryBox.ReadOnly = true;
            summaryBox.ScrollBars = ScrollBars.Vertical;
            summaryBox.Location = new Point(8, 28);
            summaryBox.Size = new Size(224, 110);
            summaryBox.Text = summary == null ? "" : summary;
            Controls.Add(summaryBox);

            addBox = new CheckBox();
            addBox.Text = "Добавить только с Android";
            addBox.Checked = true;
            addBox.Location = new Point(8, 146);
            addBox.Size = new Size(224, 20);
            Controls.Add(addBox);

            removeBox = new CheckBox();
            removeBox.Text = "Удалить отсутствующие на Android";
            removeBox.Checked = false;
            removeBox.Location = new Point(8, 168);
            removeBox.Size = new Size(224, 20);
            Controls.Add(removeBox);

            Label conflictLabel = new Label();
            conflictLabel.Text = "Если запись отличается:";
            conflictLabel.Location = new Point(8, 194);
            conflictLabel.Size = new Size(224, 16);
            Controls.Add(conflictLabel);

            conflictBox = new ComboBox();
            conflictBox.DropDownStyle = ComboBoxStyle.DropDownList;
            conflictBox.Location = new Point(8, 214);
            conflictBox.Size = new Size(224, 22);
            conflictBox.Items.Add("Оставить КПК");
            conflictBox.Items.Add("Взять Android");
            conflictBox.Items.Add("Спрашивать каждый раз");
            conflictBox.SelectedIndex = 0;
            Controls.Add(conflictBox);

            MainMenu menu = new MainMenu();
            menu.MenuItems.Add(CreateMenuItem("Синхрон.", new EventHandler(Accept)));
            menu.MenuItems.Add(CreateMenuItem("Отмена", new EventHandler(Cancel)));
            Menu = menu;
        }

        internal SyncPlan Plan
        {
            get { return plan; }
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
