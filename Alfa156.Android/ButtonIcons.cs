using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using AndroidColor = global::Android.Graphics.Color;

namespace Alfa156.Android;

/// <summary>
/// Пиктограммы кнопок: по силуэту должно быть ясно, что делает действие.
/// </summary>
internal static class ButtonIcons
{
    public const string Import = "import";
    public const string Export = "export";
    public const string Sync = "sync";
    public const string Add = "add";
    public const string Edit = "edit";
    public const string Delete = "delete";
    public const string Fuel = "fuel";
    public const string Repairs = "repairs";
    public const string Utilities = "utilities";
    public const string Summary = "summary";
    public const string Filter = "filter";
    public const string ResetFilter = "reset_filter";
    public const string Categories = "categories";
    public const string Backup = "backup";
    public const string Restore = "restore";

    public static Drawable Create(Context context, string kind, AndroidColor color)
    {
        float density = context.Resources.DisplayMetrics.Density;
        int size = (int)(24 * density + 0.5f);
        Bitmap bitmap = Bitmap.CreateBitmap(size, size, Bitmap.Config.Argb8888);
        Canvas canvas = new Canvas(bitmap);
        Paint paint = new Paint(PaintFlags.AntiAlias);
        paint.Color = color;
        paint.StrokeCap = Paint.Cap.Round;
        paint.StrokeJoin = Paint.Join.Round;
        float s = size;
        AndroidColor detail = ContrastDetail(color);

        switch (kind)
        {
            case Import:
                DrawImport(canvas, paint, s, color);
                break;
            case Export:
                DrawExport(canvas, paint, s, color);
                break;
            case Sync:
                DrawSync(canvas, paint, s, color);
                break;
            case Add:
                DrawAdd(canvas, paint, s, color);
                break;
            case Edit:
                DrawEdit(canvas, paint, s, color);
                break;
            case Delete:
                DrawDelete(canvas, paint, s, color, detail);
                break;
            case Fuel:
                DrawFuel(canvas, paint, s, color, detail);
                break;
            case Repairs:
                DrawRepairs(canvas, paint, s, color);
                break;
            case Utilities:
                DrawUtilities(canvas, paint, s, color, detail);
                break;
            case Summary:
                DrawSummary(canvas, paint, s, color);
                break;
            case Filter:
                DrawFilter(canvas, paint, s, color);
                break;
            case ResetFilter:
                DrawResetFilter(canvas, paint, s, color);
                break;
            case Categories:
                DrawCategories(canvas, paint, s, color);
                break;
            case Backup:
                DrawBackup(canvas, paint, s, color, detail);
                break;
            case Restore:
                DrawRestore(canvas, paint, s, color);
                break;
            default:
                DrawAdd(canvas, paint, s, color);
                break;
        }

        BitmapDrawable drawable = new BitmapDrawable(context.Resources, bitmap);
        drawable.SetBounds(0, 0, size, size);
        return drawable;
    }

    private static AndroidColor ContrastDetail(AndroidColor color)
    {
        // На светлой/белой заливке детали рисуем тёмными, иначе — светлыми.
        int luminance = (color.R * 30 + color.G * 59 + color.B * 11) / 100;
        return luminance > 160
            ? AndroidColor.Rgb(35, 51, 70)
            : AndroidColor.Argb(230, 255, 255, 255);
    }

    private static void DrawImport(Canvas canvas, Paint paint, float s, AndroidColor color)
    {
        paint.Color = color;
        paint.SetStyle(Paint.Style.Stroke);
        paint.StrokeWidth = s * 0.1f;
        Path tray = new Path();
        tray.MoveTo(s * 0.18f, s * 0.55f);
        tray.LineTo(s * 0.18f, s * 0.78f);
        tray.LineTo(s * 0.82f, s * 0.78f);
        tray.LineTo(s * 0.82f, s * 0.55f);
        canvas.DrawPath(tray, paint);

        paint.SetStyle(Paint.Style.Fill);
        Path arrow = new Path();
        arrow.MoveTo(s * 0.5f, s * 0.68f);
        arrow.LineTo(s * 0.32f, s * 0.48f);
        arrow.LineTo(s * 0.42f, s * 0.48f);
        arrow.LineTo(s * 0.42f, s * 0.18f);
        arrow.LineTo(s * 0.58f, s * 0.18f);
        arrow.LineTo(s * 0.58f, s * 0.48f);
        arrow.LineTo(s * 0.68f, s * 0.48f);
        arrow.Close();
        canvas.DrawPath(arrow, paint);
    }

    private static void DrawExport(Canvas canvas, Paint paint, float s, AndroidColor color)
    {
        paint.Color = color;
        paint.SetStyle(Paint.Style.Stroke);
        paint.StrokeWidth = s * 0.1f;
        Path tray = new Path();
        tray.MoveTo(s * 0.18f, s * 0.55f);
        tray.LineTo(s * 0.18f, s * 0.78f);
        tray.LineTo(s * 0.82f, s * 0.78f);
        tray.LineTo(s * 0.82f, s * 0.55f);
        canvas.DrawPath(tray, paint);

        paint.SetStyle(Paint.Style.Fill);
        Path arrow = new Path();
        arrow.MoveTo(s * 0.5f, s * 0.16f);
        arrow.LineTo(s * 0.32f, s * 0.36f);
        arrow.LineTo(s * 0.42f, s * 0.36f);
        arrow.LineTo(s * 0.42f, s * 0.66f);
        arrow.LineTo(s * 0.58f, s * 0.66f);
        arrow.LineTo(s * 0.58f, s * 0.36f);
        arrow.LineTo(s * 0.68f, s * 0.36f);
        arrow.Close();
        canvas.DrawPath(arrow, paint);
    }

    private static void DrawSync(Canvas canvas, Paint paint, float s, AndroidColor color)
    {
        paint.Color = color;
        paint.SetStyle(Paint.Style.Stroke);
        paint.StrokeWidth = s * 0.1f;
        RectF oval = new RectF(s * 0.18f, s * 0.18f, s * 0.82f, s * 0.82f);
        canvas.DrawArc(oval, 40, 120, false, paint);
        canvas.DrawArc(oval, 220, 120, false, paint);

        paint.SetStyle(Paint.Style.Fill);
        Path a1 = new Path();
        a1.MoveTo(s * 0.78f, s * 0.28f);
        a1.LineTo(s * 0.62f, s * 0.18f);
        a1.LineTo(s * 0.7f, s * 0.4f);
        a1.Close();
        canvas.DrawPath(a1, paint);

        Path a2 = new Path();
        a2.MoveTo(s * 0.22f, s * 0.72f);
        a2.LineTo(s * 0.38f, s * 0.82f);
        a2.LineTo(s * 0.3f, s * 0.6f);
        a2.Close();
        canvas.DrawPath(a2, paint);
    }

    private static void DrawAdd(Canvas canvas, Paint paint, float s, AndroidColor color)
    {
        paint.Color = color;
        paint.SetStyle(Paint.Style.Stroke);
        paint.StrokeWidth = s * 0.09f;
        canvas.DrawCircle(s * 0.5f, s * 0.5f, s * 0.36f, paint);
        paint.SetStyle(Paint.Style.Fill);
        canvas.DrawRect(s * 0.44f, s * 0.28f, s * 0.56f, s * 0.72f, paint);
        canvas.DrawRect(s * 0.28f, s * 0.44f, s * 0.72f, s * 0.56f, paint);
    }

    private static void DrawEdit(Canvas canvas, Paint paint, float s, AndroidColor color)
    {
        paint.Color = color;
        paint.SetStyle(Paint.Style.Fill);
        Path pencil = new Path();
        pencil.MoveTo(s * 0.22f, s * 0.78f);
        pencil.LineTo(s * 0.28f, s * 0.55f);
        pencil.LineTo(s * 0.72f, s * 0.18f);
        pencil.LineTo(s * 0.82f, s * 0.28f);
        pencil.LineTo(s * 0.38f, s * 0.72f);
        pencil.Close();
        canvas.DrawPath(pencil, paint);
        canvas.DrawRect(s * 0.18f, s * 0.72f, s * 0.34f, s * 0.84f, paint);
    }

    private static void DrawDelete(Canvas canvas, Paint paint, float s, AndroidColor color, AndroidColor detail)
    {
        paint.Color = color;
        paint.SetStyle(Paint.Style.Fill);
        canvas.DrawRect(s * 0.3f, s * 0.18f, s * 0.7f, s * 0.28f, paint);
        canvas.DrawRect(s * 0.42f, s * 0.12f, s * 0.58f, s * 0.2f, paint);
        Path bin = new Path();
        bin.MoveTo(s * 0.28f, s * 0.32f);
        bin.LineTo(s * 0.34f, s * 0.86f);
        bin.LineTo(s * 0.66f, s * 0.86f);
        bin.LineTo(s * 0.72f, s * 0.32f);
        bin.Close();
        canvas.DrawPath(bin, paint);
        paint.Color = detail;
        paint.StrokeWidth = s * 0.07f;
        paint.SetStyle(Paint.Style.Stroke);
        canvas.DrawLine(s * 0.42f, s * 0.42f, s * 0.42f, s * 0.74f, paint);
        canvas.DrawLine(s * 0.5f, s * 0.42f, s * 0.5f, s * 0.74f, paint);
        canvas.DrawLine(s * 0.58f, s * 0.42f, s * 0.58f, s * 0.74f, paint);
    }

    private static void DrawFuel(Canvas canvas, Paint paint, float s, AndroidColor color, AndroidColor detail)
    {
        paint.Color = color;
        paint.SetStyle(Paint.Style.Fill);
        canvas.DrawRoundRect(new RectF(s * 0.2f, s * 0.18f, s * 0.58f, s * 0.86f), s * 0.05f, s * 0.05f, paint);
        paint.Color = detail;
        canvas.DrawRect(s * 0.27f, s * 0.26f, s * 0.51f, s * 0.48f, paint);
        paint.Color = color;
        paint.SetStyle(Paint.Style.Stroke);
        paint.StrokeWidth = s * 0.09f;
        Path hose = new Path();
        hose.MoveTo(s * 0.58f, s * 0.34f);
        hose.CubicTo(s * 0.78f, s * 0.34f, s * 0.82f, s * 0.55f, s * 0.7f, s * 0.62f);
        canvas.DrawPath(hose, paint);
        paint.SetStyle(Paint.Style.Fill);
        canvas.DrawRoundRect(new RectF(s * 0.62f, s * 0.58f, s * 0.82f, s * 0.78f), s * 0.04f, s * 0.04f, paint);
    }

    private static void DrawRepairs(Canvas canvas, Paint paint, float s, AndroidColor color)
    {
        paint.Color = color;
        paint.SetStyle(Paint.Style.Stroke);
        paint.StrokeWidth = s * 0.14f;
        canvas.DrawCircle(s * 0.32f, s * 0.32f, s * 0.16f, paint);
        canvas.DrawLine(s * 0.42f, s * 0.42f, s * 0.78f, s * 0.78f, paint);
        paint.SetStyle(Paint.Style.Fill);
        Path jaw = new Path();
        jaw.MoveTo(s * 0.68f, s * 0.68f);
        jaw.LineTo(s * 0.86f, s * 0.62f);
        jaw.LineTo(s * 0.78f, s * 0.86f);
        jaw.Close();
        canvas.DrawPath(jaw, paint);
    }

    private static void DrawUtilities(Canvas canvas, Paint paint, float s, AndroidColor color, AndroidColor detail)
    {
        paint.Color = color;
        paint.SetStyle(Paint.Style.Fill);
        Path house = new Path();
        house.MoveTo(s * 0.5f, s * 0.14f);
        house.LineTo(s * 0.86f, s * 0.46f);
        house.LineTo(s * 0.74f, s * 0.46f);
        house.LineTo(s * 0.74f, s * 0.86f);
        house.LineTo(s * 0.26f, s * 0.86f);
        house.LineTo(s * 0.26f, s * 0.46f);
        house.LineTo(s * 0.14f, s * 0.46f);
        house.Close();
        canvas.DrawPath(house, paint);
        paint.Color = detail;
        canvas.DrawRect(s * 0.42f, s * 0.58f, s * 0.58f, s * 0.86f, paint);
    }

    private static void DrawSummary(Canvas canvas, Paint paint, float s, AndroidColor color)
    {
        paint.Color = color;
        paint.SetStyle(Paint.Style.Fill);
        canvas.DrawRect(s * 0.16f, s * 0.52f, s * 0.34f, s * 0.84f, paint);
        canvas.DrawRect(s * 0.4f, s * 0.28f, s * 0.58f, s * 0.84f, paint);
        canvas.DrawRect(s * 0.64f, s * 0.4f, s * 0.82f, s * 0.84f, paint);
        paint.StrokeWidth = s * 0.08f;
        paint.SetStyle(Paint.Style.Stroke);
        canvas.DrawLine(s * 0.12f, s * 0.86f, s * 0.88f, s * 0.86f, paint);
    }

    private static void DrawFilter(Canvas canvas, Paint paint, float s, AndroidColor color)
    {
        paint.Color = color;
        paint.SetStyle(Paint.Style.Fill);
        Path funnel = new Path();
        funnel.MoveTo(s * 0.14f, s * 0.18f);
        funnel.LineTo(s * 0.86f, s * 0.18f);
        funnel.LineTo(s * 0.58f, s * 0.52f);
        funnel.LineTo(s * 0.58f, s * 0.78f);
        funnel.LineTo(s * 0.42f, s * 0.86f);
        funnel.LineTo(s * 0.42f, s * 0.52f);
        funnel.Close();
        canvas.DrawPath(funnel, paint);
    }

    private static void DrawResetFilter(Canvas canvas, Paint paint, float s, AndroidColor color)
    {
        DrawFilter(canvas, paint, s, color);
        paint.Color = AndroidColor.Rgb(180, 40, 40);
        paint.StrokeWidth = s * 0.12f;
        paint.SetStyle(Paint.Style.Stroke);
        canvas.DrawLine(s * 0.2f, s * 0.78f, s * 0.8f, s * 0.22f, paint);
    }

    private static void DrawCategories(Canvas canvas, Paint paint, float s, AndroidColor color)
    {
        paint.Color = color;
        paint.SetStyle(Paint.Style.Fill);
        canvas.DrawCircle(s * 0.22f, s * 0.28f, s * 0.07f, paint);
        canvas.DrawCircle(s * 0.22f, s * 0.5f, s * 0.07f, paint);
        canvas.DrawCircle(s * 0.22f, s * 0.72f, s * 0.07f, paint);
        canvas.DrawRoundRect(new RectF(s * 0.36f, s * 0.22f, s * 0.86f, s * 0.34f), s * 0.04f, s * 0.04f, paint);
        canvas.DrawRoundRect(new RectF(s * 0.36f, s * 0.44f, s * 0.78f, s * 0.56f), s * 0.04f, s * 0.04f, paint);
        canvas.DrawRoundRect(new RectF(s * 0.36f, s * 0.66f, s * 0.7f, s * 0.78f), s * 0.04f, s * 0.04f, paint);
    }

    private static void DrawBackup(Canvas canvas, Paint paint, float s, AndroidColor color, AndroidColor detail)
    {
        paint.Color = color;
        paint.SetStyle(Paint.Style.Fill);
        Path disk = new Path();
        disk.MoveTo(s * 0.18f, s * 0.18f);
        disk.LineTo(s * 0.72f, s * 0.18f);
        disk.LineTo(s * 0.84f, s * 0.3f);
        disk.LineTo(s * 0.84f, s * 0.84f);
        disk.LineTo(s * 0.18f, s * 0.84f);
        disk.Close();
        canvas.DrawPath(disk, paint);
        paint.Color = detail;
        canvas.DrawRect(s * 0.3f, s * 0.22f, s * 0.62f, s * 0.4f, paint);
        canvas.DrawRect(s * 0.28f, s * 0.52f, s * 0.72f, s * 0.78f, paint);
    }

    private static void DrawRestore(Canvas canvas, Paint paint, float s, AndroidColor color)
    {
        paint.Color = color;
        paint.SetStyle(Paint.Style.Fill);
        canvas.DrawRoundRect(new RectF(s * 0.18f, s * 0.42f, s * 0.7f, s * 0.84f), s * 0.05f, s * 0.05f, paint);
        canvas.DrawRect(s * 0.28f, s * 0.32f, s * 0.6f, s * 0.44f, paint);
        paint.SetStyle(Paint.Style.Stroke);
        paint.StrokeWidth = s * 0.1f;
        RectF arc = new RectF(s * 0.42f, s * 0.14f, s * 0.86f, s * 0.58f);
        canvas.DrawArc(arc, 200, 220, false, paint);
        paint.SetStyle(Paint.Style.Fill);
        Path tip = new Path();
        tip.MoveTo(s * 0.48f, s * 0.14f);
        tip.LineTo(s * 0.38f, s * 0.3f);
        tip.LineTo(s * 0.58f, s * 0.28f);
        tip.Close();
        canvas.DrawPath(tip, paint);
    }
}
