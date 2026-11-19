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

                decimal maxTotal = 1;
                for (int i = 0; i < series.Count; i++)
                {
                    decimal sum = series[i][0] + series[i][1] + series[i][2] + series[i][3];
                    if (sum > maxTotal)
                        maxTotal = sum;
                }

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

                float group = (right - left) / series.Count;
                float barW = Math.Max(6f, group * 0.62f);
                for (int i = 0; i < series.Count; i++)
                {
                    float cx = left + group * (i + 0.5f);
                    float x0 = cx - barW / 2f;
                    float y = bottom;
                    for (int c = 0; c < 4; c++)
                    {
                        if (series[i][c] <= 0)
                            continue;
                        float bh = (float)((double)series[i][c] / (double)maxTotal) * (bottom - top);
                        using (Brush brush = new SolidBrush(Colors[c]))
                            g.FillRectangle(brush, x0, y - bh, barW, bh);
                        y -= bh;
                    }
                    string m = months[i];
                    SizeF ms = g.MeasureString(m, font);
                    g.DrawString(m, font, textBrush, cx - ms.Width / 2f, bottom + 4);
                }
            }
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
