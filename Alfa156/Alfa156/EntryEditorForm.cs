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
        private readonly Label[] labels;
        private readonly CheckBox[] dateEnabled;

        internal EntryEditorForm(string title, EntryField[] fields)
        {
            this.fields = fields;
            inputs = new Control[fields.Length];
            labels = new Label[fields.Length];
            dateEnabled = new CheckBox[fields.Length];
            Text = title;
            ClientSize = new System.Drawing.Size(240, 268);
            AutoScroll = true;
            BackColor = System.Drawing.Color.FromArgb(232, 238, 244);
            MinimizeBox = false;
            WindowState = FormWindowState.Maximized;

            for (int i = 0; i < fields.Length; i++)
                AddField(i);

            Resize += new EventHandler(ResizeInputs);
            MainMenu menu = new MainMenu();
            menu.MenuItems.Add(CreateMenuItem("Сохранить", new EventHandler(Save)));
            menu.MenuItems.Add(CreateMenuItem("Отмена", new EventHandler(Cancel)));
            Menu = menu;
        }

        internal string[] Values
        {
            get
            {
                string[] values = new string[inputs.Length];
                for (int i = 0; i < inputs.Length; i++)
                {
                    CheckBox check = inputs[i] as CheckBox;
                    if (check != null && fields[i].Kind == "check")
                    {
                        values[i] = check.Checked ? "1" : "0";
                        continue;
                    }
                    ComboBox combo = inputs[i] as ComboBox;
                    if (combo != null)
                        values[i] = combo.SelectedItem == null ? "" : combo.SelectedItem.ToString();
                    else
                    {
                        DateTimePicker datePicker = inputs[i] as DateTimePicker;
                        if (datePicker != null)
                        {
                            if (dateEnabled[i] == null || dateEnabled[i].Checked)
                                values[i] = datePicker.Value.ToString(
                                    fields[i].Kind == "month" ? "yyyy-MM" : "yyyy-MM-dd",
                                    CultureInfo.InvariantCulture);
                            else
                                values[i] = "";
                        }
                        else
                            values[i] = inputs[i].Text.Trim();
                    }
                }
                return values;
            }
        }

        private void AddField(int index)
        {
            int top = 8 + index * 72;
            Label label = new Label();
            label.Text = fields[index].Label;
            label.BackColor = BackColor;
            label.ForeColor = System.Drawing.Color.FromArgb(35, 51, 70);
            label.Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold);
            labels[index] = label;
            Controls.Add(label);

            Control input;
            if (fields[index].Kind == "check")
            {
                CheckBox check = new CheckBox();
                check.Text = fields[index].Label;
                check.Checked = fields[index].Value != "0";
                check.BackColor = BackColor;
                check.ForeColor = System.Drawing.Color.FromArgb(35, 51, 70);
                input = check;
                labels[index].Text = "";
            }
            else if (fields[index].Options != null)
            {
                ComboBox combo = new ComboBox();
                combo.DropDownStyle = ComboBoxStyle.DropDownList;
                combo.BackColor = System.Drawing.Color.White;
                combo.ForeColor = System.Drawing.Color.FromArgb(35, 51, 70);
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
            else if (fields[index].Kind == "date" || fields[index].Kind == "month")
            {
                DateTimePicker picker = new DateTimePicker();
                picker.Format = DateTimePickerFormat.Custom;
                picker.CustomFormat = fields[index].Kind == "month" ? "yyyy-MM" : "yyyy-MM-dd";
                DateTime initialValue;
                if (TryReadDate(fields[index].Value, fields[index].Kind == "month", out initialValue))
                    picker.Value = initialValue;
                if (!fields[index].Required)
                {
                    CheckBox enabled = new CheckBox();
                    enabled.Text = "Указать";
                    enabled.Checked = fields[index].Value.Length > 0;
                    enabled.BackColor = BackColor;
                    enabled.ForeColor = System.Drawing.Color.FromArgb(35, 51, 70);
                    dateEnabled[index] = enabled;
                    picker.Enabled = enabled.Checked;
                    enabled.Click += delegate { picker.Enabled = enabled.Checked; };
                    Controls.Add(enabled);
                }
                input = picker;
            }
            else
            {
                TextBox text = new TextBox();
                text.Text = fields[index].Value;
                text.BackColor = System.Drawing.Color.White;
                text.ForeColor = System.Drawing.Color.FromArgb(35, 51, 70);
                input = text;
            }

            inputs[index] = input;
            Controls.Add(input);
            LayoutField(index, top);
        }

        private void ResizeInputs(object sender, EventArgs e)
        {
            for (int i = 0; i < fields.Length; i++)
                LayoutField(i, 8 + i * 72);
        }

        private void LayoutField(int index, int top)
        {
            int width = ClientSize.Width - 20;
            if (width < 40)
                width = 40;
            labels[index].Location = new System.Drawing.Point(10, top);
            labels[index].Size = new System.Drawing.Size(width, 26);
            if (dateEnabled[index] != null)
            {
                int inputWidth = width - 74;
                if (inputWidth < 40)
                    inputWidth = 40;
                dateEnabled[index].Location = new System.Drawing.Point(10, top + 33);
                dateEnabled[index].Size = new System.Drawing.Size(72, 34);
                inputs[index].Location = new System.Drawing.Point(84, top + 33);
                inputs[index].Size = new System.Drawing.Size(inputWidth, 34);
            }
            else
            {
                inputs[index].Location = new System.Drawing.Point(10, top + 33);
                inputs[index].Size = new System.Drawing.Size(width, 34);
            }
        }

        private static bool TryReadDate(string value, bool monthOnly, out DateTime date)
        {
            date = DateTime.Today;
            if (monthOnly)
            {
                if (value.Length != 7 || value[4] != '-' || !HasDigits(value, 0, 4) ||
                    !HasDigits(value, 5, 2))
                    return false;
                int year = ReadDigits(value, 0, 4);
                int month = ReadDigits(value, 5, 2);
                if (year < 1753 || year > 9998 || month < 1 || month > 12)
                    return false;
                date = new DateTime(year, month, 1);
                return true;
            }

            if (value.Length != 10 || value[4] != '-' || value[7] != '-' ||
                !HasDigits(value, 0, 4) || !HasDigits(value, 5, 2) || !HasDigits(value, 8, 2))
                return false;
            int dateYear = ReadDigits(value, 0, 4);
            int dateMonth = ReadDigits(value, 5, 2);
            int day = ReadDigits(value, 8, 2);
            if (dateYear < 1753)
                return false;
            try
            {
                date = new DateTime(dateYear, dateMonth, day);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
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
                    if (!IsValidDate(values[i]))
                    {
                        InvalidValue(i, "Введите дату в формате гггг-мм-дд.");
                        return;
                    }
                }
                else if (fields[i].Kind == "month")
                {
                    if (!IsValidMonth(values[i]))
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

        private static MenuItem CreateMenuItem(string text, EventHandler handler)
        {
            MenuItem item = new MenuItem();
            item.Text = text;
            item.Click += handler;
            return item;
        }

        private static bool IsValidMonth(string value)
        {
            if (value.Length != 7 || value[4] != '-')
                return false;
            if (!HasDigits(value, 0, 4) || !HasDigits(value, 5, 2))
                return false;
            int year = ReadDigits(value, 0, 4);
            int month = ReadDigits(value, 5, 2);
            return year >= 1753 && month >= 1 && month <= 12;
        }

        private static bool IsValidDate(string value)
        {
            if (value.Length != 10 || value[4] != '-' || value[7] != '-')
                return false;
            if (!HasDigits(value, 0, 4) || !HasDigits(value, 5, 2) || !HasDigits(value, 8, 2))
                return false;

            int year = ReadDigits(value, 0, 4);
            int month = ReadDigits(value, 5, 2);
            int day = ReadDigits(value, 8, 2);
            if (year < 1753 || month < 1 || month > 12)
                return false;

            int[] daysInMonth = new int[] { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
            if (month == 2 && (year % 400 == 0 || (year % 4 == 0 && year % 100 != 0)))
                daysInMonth[1] = 29;
            return day >= 1 && day <= daysInMonth[month - 1];
        }

        private static bool HasDigits(string value, int start, int count)
        {
            for (int i = start; i < start + count; i++)
                if (value[i] < '0' || value[i] > '9')
                    return false;
            return true;
        }

        private static int ReadDigits(string value, int start, int count)
        {
            int number = 0;
            for (int i = start; i < start + count; i++)
                number = number * 10 + value[i] - '0';
            return number;
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
