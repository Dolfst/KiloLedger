using System;
using System.Globalization;
using System.Text;

namespace Alfa156.Desktop.Data
{
    /// <summary>
    /// Printable / shareable period report for one car (1.34.0).
    /// Totals come from <see cref="PeriodCompare.Build"/>.
    /// </summary>
    internal static class PeriodReport
    {
        internal static string FormatText(
            string car,
            string from,
            string to,
            PeriodCompare.Totals totals,
            string currency,
            Func<decimal, string> money)
        {
            return Format(car, from, to, totals, currency, money, "\r\n", false);
        }

        internal static string FormatHtml(
            string car,
            string from,
            string to,
            PeriodCompare.Totals totals,
            string currency,
            Func<decimal, string> money)
        {
            return Format(car, from, to, totals, currency, money, "\n", true);
        }

        private static string Format(
            string car,
            string from,
            string to,
            PeriodCompare.Totals totals,
            string currency,
            Func<decimal, string> money,
            string nl,
            bool html)
        {
            if (totals == null)
                totals = new PeriodCompare.Totals();
            if (currency == null || currency.Length == 0)
                currency = "RUB";
            if (money == null)
                money = DefaultMoney;

            string title = "Alfa 156 · отчёт за период";
            string range = PeriodCompare.FormatRangeLabel(from, to);
            string costKm = totals.HasCostPerKm
                ? money(totals.CostPerKm) + " " + currency + "/км"
                : "н/д";
            string kmRange = totals.HasCostPerKm
                ? DefaultMoney(totals.MinKm) + "…" + DefaultMoney(totals.MaxKm) +
                  " км (Δ " + DefaultMoney(totals.MaxKm - totals.MinKm) + " км)"
                : "н/д";

            if (!html)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(title);
                sb.Append(nl).Append("Авто: ").Append(car ?? "");
                sb.Append(nl).Append("Период: ").Append(range);
                sb.Append(nl);
                sb.Append(nl).Append("Топливо: ").Append(money(totals.Fuel)).Append(' ').Append(currency);
                sb.Append(nl).Append("Ремонт: ").Append(money(totals.Repair)).Append(' ').Append(currency);
                sb.Append(nl).Append("Прочие: ").Append(money(totals.Expense)).Append(' ').Append(currency);
                sb.Append(nl).Append("Коммунальные: ").Append(money(totals.Utility)).Append(' ').Append(currency);
                sb.Append(nl).Append("Всего (без коммун.): ").Append(money(totals.CostBase)).Append(' ').Append(currency);
                sb.Append(nl).Append("Всего (с коммунальными): ").Append(money(totals.CostBase + totals.Utility)).Append(' ').Append(currency);
                sb.Append(nl).Append("Стоимость/км: ").Append(costKm);
                sb.Append(nl).Append("Пробег: ").Append(kmRange);
                return sb.ToString();
            }

            StringBuilder h = new StringBuilder();
            h.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/>");
            h.Append("<title>").Append(Esc(title)).Append("</title>");
            h.Append("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#233346}");
            h.Append("h1{font-size:18px;margin:0 0 12px}table{border-collapse:collapse;margin-top:12px}");
            h.Append("td{padding:4px 16px 4px 0}td.v{text-align:right;font-variant-numeric:tabular-nums}");
            h.Append(".meta{color:#555;margin:0 0 4px}</style></head><body>");
            h.Append("<h1>").Append(Esc(title)).Append("</h1>");
            h.Append("<p class=\"meta\">Авто: ").Append(Esc(car ?? "")).Append("</p>");
            h.Append("<p class=\"meta\">Период: ").Append(Esc(range)).Append("</p>");
            h.Append("<table>");
            AppendRow(h, "Топливо", money(totals.Fuel) + " " + currency);
            AppendRow(h, "Ремонт", money(totals.Repair) + " " + currency);
            AppendRow(h, "Прочие", money(totals.Expense) + " " + currency);
            AppendRow(h, "Коммунальные", money(totals.Utility) + " " + currency);
            AppendRow(h, "Всего (без коммун.)", money(totals.CostBase) + " " + currency);
            AppendRow(h, "Всего (с коммунальными)", money(totals.CostBase + totals.Utility) + " " + currency);
            AppendRow(h, "Стоимость/км", costKm);
            AppendRow(h, "Пробег", kmRange);
            h.Append("</table></body></html>");
            return h.ToString();
        }

        private static void AppendRow(StringBuilder h, string label, string value)
        {
            h.Append("<tr><td>").Append(Esc(label)).Append("</td><td class=\"v\">")
                .Append(Esc(value)).Append("</td></tr>");
        }

        private static string Esc(string value)
        {
            if (value == null || value.Length == 0)
                return "";
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private static string DefaultMoney(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
