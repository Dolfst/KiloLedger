using System;
using System.Globalization;
using System.Windows.Forms;

namespace CarExpenses
{
    internal sealed class EntryField
    {
        internal string Label;
        internal string Value;
        internal string Kind;
        internal bool Required;
        internal string[] Options;

        internal EntryField(string label, string value, string kind, bool required)
        {
            Label = label;
            Value = value;
            Kind = kind;
            Required = required;
        }

        internal EntryField(string label, string value, string[] options, bool required)
        {
            Label = label;
            Value = value;
            Options = options;
            Kind = "option";
            Required = required;
        }
    }

    internal sealed class EntryEditorForm : Form
    {
        private readonly EntryField[] fields;
        private readonly Control[] inputs;

        internal EntryEditorForm(string title, EntryField[] fields)
        {
            this.fields = fields;
            inputs = new Control[fields.Length];
            Text = title;
            ClientSize = new System.Drawing.Size(240, 268);
            AutoScroll = true;
            MinimizeBox = false;

            for (int i = 0; i < fields.Length; i++)
                AddField(i);

            MainMenu menu = new MainMenu();
            menu.MenuItems.Add(new MenuItem("Сохранить", new EventHandler(Save)));
            menu.MenuItems.Add(new MenuItem("Отмена", new EventHandler(Cancel)));
            Menu = menu;
        }

        internal string[] Values
        {
            get
            {
                string[] values = new string[inputs.Length];
                for (int i = 0; i < inputs.Length; i++)
                {
                    ComboBox combo = inputs[i] as ComboBox;
                    if (combo != null)
                        values[i] = combo.SelectedItem == null ? "" : combo.SelectedItem.ToString();
                    else
                        values[i] = inputs[i].Text.Trim();
                }
                return values;
            }
        }

        private void AddField(int index)
        {
            int top = 4 + index * 44;
            Label label = new Label();
            label.Location = new System.Drawing.Point(4, top);
            label.Size = new System.Drawing.Size(225, 18);
            label.Text = fields[index].Label;
            Controls.Add(label);

            Control input;
            if (fields[index].Options != null)
            {
                ComboBox combo = new ComboBox();
                combo.DropDownStyle = ComboBoxStyle.DropDownList;
                for (int j = 0; j < fields[index].Options.Length; j++)
                {
                    combo.Items.Add(fields[index].Options[j]);
                    if (fields[index].Options[j] == fields[index].Value)
                        combo.SelectedIndex = j;
                }
                if (combo.SelectedIndex < 0 && combo.Items.Count > 0)
                    combo.SelectedIndex = 0;
                input = combo;
            }
            else
            {
                TextBox text = new TextBox();
                text.Text = fields[index].Value;
                input = text;
            }

            input.Location = new System.Drawing.Point(4, top + 18);
            input.Size = new System.Drawing.Size(225, 22);
            inputs[index] = input;
            Controls.Add(input);
        }

        private void Save(object sender, EventArgs e)
        {
            string[] values = Values;
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].Required && values[i].Length == 0)
                {
                    MessageBox.Show(fields[i].Label + " — обязательное поле.", "Проверьте данные");
                    inputs[i].Focus();
                    return;
                }

                if (values[i].Length == 0)
                    continue;

                if (fields[i].Kind == "date")
                {
                    DateTime date;
                    if (!DateTime.TryParseExact(values[i], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out date))
                    {
                        InvalidValue(i, "Введите дату в формате гггг-мм-дд.");
                        return;
                    }
                }
                else if (fields[i].Kind == "month")
                {
                    DateTime month;
                    if (!DateTime.TryParseExact(values[i], "yyyy-MM", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out month))
                    {
                        InvalidValue(i, "Введите месяц в формате гггг-мм.");
                        return;
                    }
                }
                else if (fields[i].Kind == "number")
                {
                    decimal number;
                    if (!DataStore.TryNumber(values[i], out number) || number < 0)
                    {
                        InvalidValue(i, "Введите неотрицательное число.");
                        return;
                    }
                    values[i] = number.ToString(CultureInfo.InvariantCulture);
                }
            }

            DialogResult = DialogResult.OK;
        }

        private void InvalidValue(int index, string message)
        {
            MessageBox.Show(message, "Проверьте данные");
            inputs[index].Focus();
        }

        private void Cancel(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }
    }
}
