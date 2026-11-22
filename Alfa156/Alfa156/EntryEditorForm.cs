using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.WindowsCE.Forms;

using Alfa156.Localization;
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

        /// <summary>Editable combo with suggestions (АЗС / бренд).</summary>
        internal static EntryField Suggest(string label, string value, string[] options, bool required)
        {
            EntryField field = new EntryField(label, value, "suggest", required);
            field.Options = options != null ? options : new string[0];
            return field;
        }
    }

    internal sealed class EntryEditorForm : Form
    {
        private readonly EntryField[] fields;
        private readonly Control[] inputs;
        private readonly Label[] labels;
        private readonly CheckBox[] dateEnabled;
        private readonly Panel scrollPanel;
        private readonly Label bottomSpacer;
        private InputPanel inputPanel;
        private int lastFocusedIndex = -1;

        internal EntryEditorForm(string title, EntryField[] fields)
        {
            this.fields = fields;
            inputs = new Control[fields.Length];
            labels = new Label[fields.Length];
            dateEnabled = new CheckBox[fields.Length];
            Text = title;
            ClientSize = new Size(240, 268);
            AutoScroll = false;
            BackColor = Color.FromArgb(232, 238, 244);
            MinimizeBox = false;
            WindowState = FormWindowState.Maximized;

            scrollPanel = new Panel();
            scrollPanel.Location = new Point(0, 0);
            scrollPanel.Size = ClientSize;
            scrollPanel.BackColor = BackColor;
            scrollPanel.AutoScroll = true;
            Controls.Add(scrollPanel);

            for (int i = 0; i < fields.Length; i++)
                AddField(i);

            bottomSpacer = new Label();
            bottomSpacer.Text = "";
            bottomSpacer.BackColor = BackColor;
            bottomSpacer.Location = new Point(0, 8 + fields.Length * 72);
            bottomSpacer.Size = new Size(10, 14);
            scrollPanel.Controls.Add(bottomSpacer);

            try
            {
                inputPanel = new InputPanel();
                inputPanel.EnabledChanged += new EventHandler(SipChanged);
            }
            catch
            {
                inputPanel = null;
            }

            Resize += new EventHandler(ResizeInputs);
            Load += new EventHandler(FormLoaded);
            Activated += new EventHandler(FormActivated);
            MainMenu menu = new MainMenu();
            menu.MenuItems.Add(CreateMenuItem(L10n.Get("button.save"), new EventHandler(Save)));
            menu.MenuItems.Add(CreateMenuItem(L10n.Get("button.cancel"), new EventHandler(Cancel)));
            Menu = menu;

            UpdateLayoutAndScroll(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && inputPanel != null)
            {
                try
                {
                    inputPanel.EnabledChanged -= new EventHandler(SipChanged);
                    inputPanel.Dispose();
                }
                catch
                {
                }
                inputPanel = null;
            }
            base.Dispose(disposing);
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
                    {
                        if (fields[i].Kind == "suggest")
                            values[i] = combo.Text == null ? "" : combo.Text.Trim();
                        else
                            values[i] = combo.SelectedItem == null ? "" : combo.SelectedItem.ToString();
                    }
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
            label.ForeColor = Color.FromArgb(35, 51, 70);
            label.Font = new Font("Arial", 9, FontStyle.Bold);
            labels[index] = label;
            scrollPanel.Controls.Add(label);

            int fieldIndex = index;
            Control input;
            if (fields[index].Kind == "check")
            {
                CheckBox check = new CheckBox();
                check.Text = fields[index].Label;
                check.Checked = fields[index].Value != "0";
                check.BackColor = BackColor;
                check.ForeColor = Color.FromArgb(35, 51, 70);
                input = check;
                labels[index].Text = "";
            }
            else if (fields[index].Kind == "suggest")
            {
                ComboBox combo = new ComboBox();
                combo.DropDownStyle = ComboBoxStyle.DropDown;
                combo.BackColor = Color.White;
                combo.ForeColor = Color.FromArgb(35, 51, 70);
                if (fields[index].Options != null)
                {
                    for (int j = 0; j < fields[index].Options.Length; j++)
                        combo.Items.Add(fields[index].Options[j]);
                }
                combo.Text = fields[index].Value == null ? "" : fields[index].Value;
                input = combo;
            }
            else if (fields[index].Options != null)
            {
                ComboBox combo = new ComboBox();
                combo.DropDownStyle = ComboBoxStyle.DropDownList;
                combo.BackColor = Color.White;
                combo.ForeColor = Color.FromArgb(35, 51, 70);
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
                    enabled.Text = L10n.Get("button.specify");
                    enabled.Checked = fields[index].Value.Length > 0;
                    enabled.BackColor = BackColor;
                    enabled.ForeColor = Color.FromArgb(35, 51, 70);
                    dateEnabled[index] = enabled;
                    picker.Enabled = enabled.Checked;
                    enabled.Click += delegate { picker.Enabled = enabled.Checked; };
                    enabled.GotFocus += delegate { OnFieldFocused(fieldIndex); };
                    scrollPanel.Controls.Add(enabled);
                }
                input = picker;
            }
            else
            {
                TextBox text = new TextBox();
                text.Text = fields[index].Value;
                text.BackColor = Color.White;
                text.ForeColor = Color.FromArgb(35, 51, 70);
                input = text;
            }

            input.GotFocus += delegate { OnFieldFocused(fieldIndex); };
            inputs[index] = input;
            scrollPanel.Controls.Add(input);
            LayoutField(index, top, ClientSize.Width - 28);
        }

        private void FormLoaded(object sender, EventArgs e)
        {
            UpdateLayoutAndScroll(false);
        }

        private void FormActivated(object sender, EventArgs e)
        {
            UpdateLayoutAndScroll(false);
        }

        private void SipChanged(object sender, EventArgs e)
        {
            UpdateLayoutAndScroll(true);
        }

        private void ResizeInputs(object sender, EventArgs e)
        {
            UpdateLayoutAndScroll(false);
        }

        private void UpdateLayoutAndScroll(bool scrollFocusedIntoView)
        {
            int clientW = ClientSize.Width;
            int clientH = ClientSize.Height;
            if (clientW < 40) clientW = 40;
            if (clientH < 40) clientH = 40;

            int visibleHeight = GetVisibleHeightAboveKeyboard(clientH);
            scrollPanel.Location = new Point(0, 0);
            scrollPanel.Size = new Size(clientW, visibleHeight);

            int contentHeight = 8 + fields.Length * 72 + 14;
            bool needsVScroll = contentHeight > visibleHeight;
            int scrollReserve = needsVScroll ? 24 : 20;
            int fieldWidth = clientW - scrollReserve;
            if (fieldWidth < 40)
                fieldWidth = 40;

            int scrollY = -scrollPanel.AutoScrollPosition.Y;
            for (int i = 0; i < fields.Length; i++)
                LayoutField(i, 8 + i * 72 - scrollY, fieldWidth);
            if (bottomSpacer != null)
                bottomSpacer.Location = new Point(0, 8 + fields.Length * 72 - scrollY);

            if (scrollFocusedIntoView)
            {
                int focusIdx = FindFocusedFieldIndex();
                if (focusIdx >= 0)
                    EnsureFieldVisible(focusIdx);
            }
        }

        private int GetVisibleHeightAboveKeyboard(int fullClientHeight)
        {
            int visible = fullClientHeight;
            try
            {
                if (inputPanel != null && inputPanel.Enabled)
                {
                    Point topLeft = PointToScreen(new Point(0, 0));
                    Rectangle sip = inputPanel.Bounds;
                    if (sip.Height > 0 && sip.Width > 0)
                    {
                        int candidate = sip.Top - topLeft.Y;
                        if (candidate >= 48 && candidate < visible)
                            visible = candidate;
                    }
                    Rectangle desktop = inputPanel.VisibleDesktop;
                    if (desktop.Height > 0 && desktop.Width > 0)
                    {
                        int candidate = desktop.Bottom - topLeft.Y;
                        if (candidate >= 48 && candidate < visible)
                            visible = candidate;
                    }
                }
            }
            catch
            {
            }
            return visible;
        }

        private int FindFocusedFieldIndex()
        {
            for (int i = 0; i < inputs.Length; i++)
            {
                if (inputs[i] != null && inputs[i].Focused)
                    return i;
                if (dateEnabled[i] != null && dateEnabled[i].Focused)
                    return i;
            }
            return lastFocusedIndex;
        }

        private void OnFieldFocused(int index)
        {
            lastFocusedIndex = index;
            UpdateLayoutAndScroll(false);
            EnsureFieldVisible(index);
        }

        private void EnsureFieldVisible(int index)
        {
            if (index < 0 || index >= fields.Length)
                return;
            int viewHeight = scrollPanel.Height;
            if (viewHeight <= 0)
                return;

            int fieldTop = 8 + index * 72;
            int fieldBottom = fieldTop + 70;
            int currentTop = -scrollPanel.AutoScrollPosition.Y;
            int currentBottom = currentTop + viewHeight;

            int targetTop = currentTop;
            if (fieldTop < currentTop)
                targetTop = fieldTop - 6;
            else if (fieldBottom > currentBottom)
                targetTop = fieldBottom - viewHeight + 8;

            if (targetTop < 0)
                targetTop = 0;

            if (targetTop != currentTop)
                scrollPanel.AutoScrollPosition = new Point(0, targetTop);
        }

        private void LayoutField(int index, int top, int width)
        {
            labels[index].Location = new Point(10, top);
            labels[index].Size = new Size(width, 26);
            if (dateEnabled[index] != null)
            {
                int inputWidth = width - 74;
                if (inputWidth < 40)
                    inputWidth = 40;
                dateEnabled[index].Location = new Point(10, top + 33);
                dateEnabled[index].Size = new Size(72, 34);
                inputs[index].Location = new Point(84, top + 33);
                inputs[index].Size = new Size(inputWidth, 34);
            }
            else
            {
                inputs[index].Location = new Point(10, top + 33);
                inputs[index].Size = new Size(width, 34);
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
                    MessageBox.Show(fields[i].Label + (" " + L10n.Get("error.required_field_suffix")), L10n.Get("title.check_data"));
                    inputs[i].Focus();
                    return;
                }

                if (values[i].Length == 0)
                    continue;

                if (fields[i].Kind == "date")
                {
                    if (!IsValidDate(values[i]))
                    {
                        InvalidValue(i, L10n.Get("error.enter_date_yyyy_mm_dd"));
                        return;
                    }
                }
                else if (fields[i].Kind == "month")
                {
                    if (!IsValidMonth(values[i]))
                    {
                        InvalidValue(i, L10n.Get("error.enter_month_yyyy_mm"));
                        return;
                    }
                }
                else if (fields[i].Kind == "number")
                {
                    decimal number;
                    if (!DataStore.TryNumber(values[i], out number) || number < 0)
                    {
                        InvalidValue(i, L10n.Get("error.enter_non_negative"));
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
            MessageBox.Show(message, L10n.Get("title.check_data"));
            inputs[index].Focus();
        }

        private void Cancel(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }
    }
}
