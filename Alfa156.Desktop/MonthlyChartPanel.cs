using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Alfa156.Desktop
{
    /// <summary>
    /// Simple stacked bar chart (GDI+) for last 12 months — no chart library.
    /// </summary>
    internal sealed class MonthlyChartPanel : Panel
    {
        private List<string> months = new List<string>();
        private List<decimal[]> series = new List<decimal[]>();
        private bool compareMode;
        private List<string> compareLabels = new List<string>();
        private List<decimal> compareValues = new List<decimal>();
        private string compareUnit = "";

        private static readonly Color[] Colors =
        {
            Color.FromArgb(180, 40, 40),
            Color.FromArgb(49, 77, 102),
            Color.FromArgb(90, 140, 90),
            Color.FromArgb(140, 120, 80)
        };

        private static readonly string[] Names = { "Топливо", "Ремонт", "Прочие", "Коммун." };

        public MonthlyChartPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            Height = 220;
            MinimumSize = new Size(200, 180);
        }

        public void SetData(IList<string> monthKeys, IList<decimal[]> totals)
        {
            compareMode = false;
            months = new List<string>();
            series = new List<decimal[]>();
            if (monthKeys != null && totals != null)
            {
                int count = Math.Min(monthKeys.Count, totals.Count);
                for (int i = 0; i < count; i++)
                {
                    months.Add(ShortMonth(monthKeys[i]));
                    decimal[] src = totals[i] ?? new decimal[4];
                    decimal[] copy = new decimal[4];
                    for (int c = 0; c < 4; c++)
                        copy[c] = c < src.Length ? src[c] : 0;
                    series.Add(copy);
                }
            }
            Invalidate();
        }

        public void SetCompareData(IList<string> labels, IList<decimal> values, string unit)
        {
            compareMode = true;
            compareLabels = new List<string>();
            compareValues = new List<decimal>();
            compareUnit = unit ?? "";
            if (labels != null && values != null)
            {
                int count = Math.Min(labels.Count, values.Count);
                for (int i = 0; i < count; i++)
                {
                    compareLabels.Add(labels[i] ?? "");
                    compareValues.Add(values[i]);
                }
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int w = Width;
            int h = Height;
            if (w < 40 || h < 40)
                return;

            float padL = 40;
            float padR = 8;
            float padT = 10;
            float legendH = 28;
            float padB = 26 + legendH;
            float left = padL;
            float right = w - padR;
            float top = padT;
            float bottom = h - padB;

            using (Font font = new Font("Segoe UI", 8f))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(35, 51, 70)))
            using (Pen axis = new Pen(Color.FromArgb(35, 51, 70), 1.2f))
            using (Pen grid = new Pen(Color.FromArgb(225, 231, 237)))
            {
                if (compareMode)
                {
                    g.DrawString("Сравнение: стоимость/км" +
                        (compareUnit.Length > 0 ? " (" + compareUnit + ")" : ""),
                        font, textBrush, left, h - legendH + 3);
                    if (compareValues.Count == 0)
                    {
                        g.DrawString("Нет данных для сравнения /км", font, textBrush,
                            w / 2f - 80, (top + bottom) / 2f);
                        return;
                    }
                    decimal maxTotal = 1;
                    for (int i = 0; i < compareValues.Count; i++)
                        if (compareValues[i] > maxTotal)
                            maxTotal = compareValues[i];
                    for (int gi = 0; gi <= 4; gi++)
                    {
                        float y = bottom - (bottom - top) * gi / 4f;
                        g.DrawLine(grid, left, y, right, y);
                        string label = FormatAxis(maxTotal * gi / 4m);
                        SizeF sz = g.MeasureString(label, font);
                        g.DrawString(label, font, textBrush, left - sz.Width - 4, y - sz.Height / 2f);
                    }
                    g.DrawLine(axis, left, top, left, bottom);
                    g.DrawLine(axis, left, bottom, right, bottom);
                    float group = (right - left) / compareValues.Count;
                    float barW = Math.Max(10f, group * 0.55f);
                    for (int i = 0; i < compareValues.Count; i++)
                    {
                        float cx = left + group * (i + 0.5f);
                        float x0 = cx - barW / 2f;
                        float bh = (float)((double)compareValues[i] / (double)maxTotal) * (bottom - top);
                        using (Brush brush = new SolidBrush(Colors[0]))
                            g.FillRectangle(brush, x0, bottom - bh, barW, bh);
                        string m = ShortLabel(compareLabels[i]);
                        SizeF ms = g.MeasureString(m, font);
                        g.DrawString(m, font, textBrush, cx - ms.Width / 2f, bottom + 4);
                    }
                    return;
                }

                float lx = left;
                for (int i = 0; i < 4; i++)
                {
                    using (Brush b = new SolidBrush(Colors[i]))
                        g.FillRectangle(b, lx, h - legendH + 6, 10, 10);
                    g.DrawString(Names[i], font, textBrush, lx + 14, h - legendH + 3);
                    lx += 14 + g.MeasureString(Names[i], font).Width + 12;
                }

                if (series.Count == 0)
                {
                    g.DrawString("Нет данных за 12 месяцев", font, textBrush, w / 2f - 70, (top + bottom) / 2f);
                    return;
                }

                decimal maxStacked = 1;
                for (int i = 0; i < series.Count; i++)
                {
                    decimal sum = series[i][0] + series[i][1] + series[i][2] + series[i][3];
                    if (sum > maxStacked)
                        maxStacked = sum;
                }

                for (int gi = 0; gi <= 4; gi++)
                {
                    float y = bottom - (bottom - top) * gi / 4f;
                    g.DrawLine(grid, left, y, right, y);
                    string label = FormatAxis(maxStacked * gi / 4m);
                    SizeF sz = g.MeasureString(label, font);
                    g.DrawString(label, font, textBrush, left - sz.Width - 4, y - sz.Height / 2f);
                }
                g.DrawLine(axis, left, top, left, bottom);
                g.DrawLine(axis, left, bottom, right, bottom);

                float groupM = (right - left) / series.Count;
                float barWM = Math.Max(6f, groupM * 0.62f);
                for (int i = 0; i < series.Count; i++)
                {
                    float cx = left + groupM * (i + 0.5f);
                    float x0 = cx - barWM / 2f;
                    float y = bottom;
                    for (int c = 0; c < 4; c++)
                    {
                        if (series[i][c] <= 0)
                            continue;
                        float bh = (float)((double)series[i][c] / (double)maxStacked) * (bottom - top);
                        using (Brush brush = new SolidBrush(Colors[c]))
                            g.FillRectangle(brush, x0, y - bh, barWM, bh);
                        y -= bh;
                    }
                    string m = months[i];
                    SizeF ms = g.MeasureString(m, font);
                    g.DrawString(m, font, textBrush, cx - ms.Width / 2f, bottom + 4);
                }
            }
        }

        private static string ShortLabel(string text)
        {
            if (String.IsNullOrEmpty(text))
                return "";
            return text.Length <= 10 ? text : text.Substring(0, 9) + "…";
        }

        private static string ShortMonth(string yyyyMm)
        {
            if (yyyyMm == null || yyyyMm.Length < 7)
                return yyyyMm ?? "";
            switch (yyyyMm.Substring(5, 2))
            {
                case "01": return "янв";
                case "02": return "фев";
                case "03": return "мар";
                case "04": return "апр";
                case "05": return "май";
                case "06": return "июн";
                case "07": return "июл";
                case "08": return "авг";
                case "09": return "сен";
                case "10": return "окт";
                case "11": return "ноя";
                case "12": return "дек";
                default: return yyyyMm.Substring(5, 2);
            }
        }

        private static string FormatAxis(decimal value)
        {
            if (value >= 1000)
                return (value / 1000m).ToString("0.#", CultureInfo.InvariantCulture) + "k";
            return value.ToString("0", CultureInfo.InvariantCulture);
        }
    }
}
