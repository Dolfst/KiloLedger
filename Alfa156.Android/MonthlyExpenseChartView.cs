using System;
using System.Collections.Generic;
using System.Globalization;
using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;
using AndroidColor = global::Android.Graphics.Color;

namespace Alfa156.Android;

/// <summary>
/// Lightweight stacked bar chart for the last 12 months (Canvas only, no chart library).
/// Categories: топливо, ремонт, прочие, коммунальные.
/// </summary>
internal sealed class MonthlyExpenseChartView : View
{
    private readonly List<string> monthLabels = new List<string>();
    private readonly List<decimal[]> series = new List<decimal[]>();
    private readonly Paint barPaint = new Paint(PaintFlags.AntiAlias);
    private readonly Paint axisPaint = new Paint(PaintFlags.AntiAlias);
    private readonly Paint labelPaint = new Paint(PaintFlags.AntiAlias);
    private readonly Paint legendPaint = new Paint(PaintFlags.AntiAlias);
    private readonly Paint gridPaint = new Paint(PaintFlags.AntiAlias);

    private static readonly AndroidColor[] CategoryColors =
    {
        AndroidColor.Rgb(180, 40, 40),   // топливо — brand red
        AndroidColor.Rgb(49, 77, 102),   // ремонт — accent
        AndroidColor.Rgb(90, 140, 90),   // прочие
        AndroidColor.Rgb(140, 120, 80)   // коммунальные
    };

    private static readonly string[] CategoryNames =
    {
        "Топливо", "Ремонт", "Прочие", "Коммун."
    };

    public MonthlyExpenseChartView(Context context) : base(context)
    {
        axisPaint.Color = AndroidColor.Rgb(35, 51, 70);
        axisPaint.StrokeWidth = Dp(1.5f);
        axisPaint.SetStyle(Paint.Style.Stroke);

        labelPaint.Color = AndroidColor.Rgb(35, 51, 70);
        labelPaint.TextSize = Sp(10);
        labelPaint.TextAlign = Paint.Align.Center;

        legendPaint.Color = AndroidColor.Rgb(35, 51, 70);
        legendPaint.TextSize = Sp(11);
        legendPaint.TextAlign = Paint.Align.Left;

        gridPaint.Color = AndroidColor.Rgb(225, 231, 237);
        gridPaint.StrokeWidth = Dp(1);
        gridPaint.SetStyle(Paint.Style.Stroke);

        SetBackgroundColor(AndroidColor.White);
        SetMinimumHeight(DpInt(220));
    }

    public void SetData(IList<string> months, IList<decimal[]> monthlyTotals)
    {
        monthLabels.Clear();
        series.Clear();
        if (months != null && monthlyTotals != null)
        {
            int count = Math.Min(months.Count, monthlyTotals.Count);
            for (int i = 0; i < count; i++)
            {
                monthLabels.Add(ShortMonth(months[i]));
                decimal[] src = monthlyTotals[i] ?? new decimal[4];
                decimal[] copy = new decimal[4];
                for (int c = 0; c < 4; c++)
                    copy[c] = c < src.Length ? src[c] : 0;
                series.Add(copy);
            }
        }
        Invalidate();
    }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        int width = MeasureSpec.GetSize(widthMeasureSpec);
        if (width <= 0)
            width = DpInt(320);
        int height = Math.Max(DpInt(220), MeasureSpec.GetSize(heightMeasureSpec));
        if (MeasureSpec.GetMode(heightMeasureSpec) == MeasureSpecMode.Unspecified)
            height = DpInt(260);
        SetMeasuredDimension(width, height);
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        int w = Width;
        int h = Height;
        if (w <= 0 || h <= 0)
            return;

        float padL = Dp(36);
        float padR = Dp(10);
        float padT = Dp(12);
        float legendH = Dp(36);
        float padB = Dp(28) + legendH;
        float chartLeft = padL;
        float chartRight = w - padR;
        float chartTop = padT;
        float chartBottom = h - padB;

        DrawLegend(canvas, padL, h - legendH + Dp(4), w - padL - padR);

        if (series.Count == 0)
        {
            labelPaint.TextAlign = Paint.Align.Center;
            canvas.DrawText("Нет данных за 12 месяцев", w / 2f, (chartTop + chartBottom) / 2f, labelPaint);
            return;
        }

        decimal maxTotal = 0;
        for (int i = 0; i < series.Count; i++)
        {
            decimal sum = series[i][0] + series[i][1] + series[i][2] + series[i][3];
            if (sum > maxTotal)
                maxTotal = sum;
        }
        if (maxTotal <= 0)
            maxTotal = 1;

        // Grid + Y labels
        labelPaint.TextAlign = Paint.Align.Right;
        for (int g = 0; g <= 4; g++)
        {
            float y = chartBottom - (chartBottom - chartTop) * g / 4f;
            canvas.DrawLine(chartLeft, y, chartRight, y, gridPaint);
            decimal value = maxTotal * g / 4m;
            canvas.DrawText(FormatAxis(value), chartLeft - Dp(4), y + Sp(3), labelPaint);
        }

        canvas.DrawLine(chartLeft, chartTop, chartLeft, chartBottom, axisPaint);
        canvas.DrawLine(chartLeft, chartBottom, chartRight, chartBottom, axisPaint);

        float groupWidth = (chartRight - chartLeft) / series.Count;
        float barWidth = Math.Max(Dp(6), groupWidth * 0.62f);

        for (int i = 0; i < series.Count; i++)
        {
            float cx = chartLeft + groupWidth * (i + 0.5f);
            float left = cx - barWidth / 2f;
            float right = cx + barWidth / 2f;
            float y = chartBottom;
            for (int c = 0; c < 4; c++)
            {
                decimal amount = series[i][c];
                if (amount <= 0)
                    continue;
                float height = (float)((double)amount / (double)maxTotal) * (chartBottom - chartTop);
                float top = y - height;
                barPaint.Color = CategoryColors[c];
                barPaint.SetStyle(Paint.Style.Fill);
                canvas.DrawRect(left, top, right, y, barPaint);
                y = top;
            }
            labelPaint.TextAlign = Paint.Align.Center;
            canvas.DrawText(monthLabels[i], cx, chartBottom + Sp(12), labelPaint);
        }
    }

    private void DrawLegend(Canvas canvas, float x, float y, float availableWidth)
    {
        float box = Dp(10);
        float cursor = x;
        for (int i = 0; i < 4; i++)
        {
            barPaint.Color = CategoryColors[i];
            barPaint.SetStyle(Paint.Style.Fill);
            canvas.DrawRect(cursor, y, cursor + box, y + box, barPaint);
            cursor += box + Dp(4);
            legendPaint.TextAlign = Paint.Align.Left;
            canvas.DrawText(CategoryNames[i], cursor, y + box - Dp(1), legendPaint);
            cursor += legendPaint.MeasureText(CategoryNames[i]) + Dp(12);
            if (cursor > x + availableWidth && i < 3)
            {
                // wrap not needed for typical widths; keep single line
            }
        }
    }

    private static string ShortMonth(string yyyyMm)
    {
        if (yyyyMm == null || yyyyMm.Length < 7)
            return yyyyMm ?? "";
        string mm = yyyyMm.Substring(5, 2);
        switch (mm)
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
            default: return mm;
        }
    }

    private static string FormatAxis(decimal value)
    {
        if (value >= 1000)
            return (value / 1000m).ToString("0.#", CultureInfo.InvariantCulture) + "k";
        return value.ToString("0", CultureInfo.InvariantCulture);
    }

    private float Dp(float value)
    {
        return TypedValue.ApplyDimension(ComplexUnitType.Dip, value, Resources.DisplayMetrics);
    }

    private int DpInt(float value)
    {
        return (int)(Dp(value) + 0.5f);
    }

    private float Sp(float value)
    {
        return TypedValue.ApplyDimension(ComplexUnitType.Sp, value, Resources.DisplayMetrics);
    }
}
