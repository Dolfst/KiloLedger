using System;
using System.Globalization;
using System.Text;

using KiloLedger.Localization;
namespace CarExpenses
{
    /// <summary>
    /// Text period report for one car (1.34.0). CF 3.5-safe formatting only.
    /// Totals come from <see cref="PeriodCompare.Build"/>.
    /// </summary>
    internal static class PeriodReport
    {
        internal static string FormatText(
            string car,
            string from,
            string to,
            PeriodCompare.Totals totals,
            string currency)
        {
            if (totals == null)
                totals = new PeriodCompare.Totals();
            if (currency == null || currency.Length == 0)
                currency = "RUB";

            const string nl = "\r\n";
            string title = L10n.Get("report.header");
            string range = PeriodCompare.FormatRangeLabel(from, to);
            string costKm = totals.HasCostPerKm
                ? Money(totals.CostPerKm) + " " + currency + L10n.Get("unit.per_km")
                : L10n.Get("common.na");
            string kmRange = totals.HasCostPerKm
                ? Money(totals.MinKm) + "…" + Money(totals.MaxKm) +
                  (" " + L10n.Get("report.km_delta_open") + " ") + Money(totals.MaxKm - totals.MinKm) + (" " + L10n.Get("unit.km_close"))
                : L10n.Get("common.na");
            string consumption = totals.HasConsumption
                ? Money(totals.LitersPer100Km) + (" " + L10n.Get("unit.l_per_100km"))
                : L10n.Get("common.na");
            string budget = "";
            if (totals.PeriodBudget > 0)
            {
                decimal spent = totals.CostBase;
                budget = Money(spent) + " / " + Money(totals.PeriodBudget) + " " + currency +
                    " (" + Money(spent * 100m / totals.PeriodBudget) + "%)";
                if (spent >= totals.PeriodBudget)
                    budget += (" " + L10n.Get("report.budget_exceeded"));
            }

            StringBuilder sb = new StringBuilder();
            sb.Append(title);
            sb.Append(nl).Append((L10n.Get("label.car_colon") + " ")).Append(car ?? "");
            sb.Append(nl).Append((L10n.Get("report.period_colon") + " ")).Append(range);
            sb.Append(nl);
            sb.Append(nl).Append((L10n.Get("label.fuel_colon") + " ")).Append(Money(totals.Fuel)).Append(' ').Append(currency);
            sb.Append(nl).Append((L10n.Get("label.repairs_colon") + " ")).Append(Money(totals.Repair)).Append(' ').Append(currency);
            if (totals.TopRepairLabel.Length > 0)
                sb.Append(nl).Append(("  " + L10n.Get("report.top_repairs_colon") + " ")).Append(totals.TopRepairLabel).Append(" — ")
                    .Append(Money(totals.TopRepairAmount)).Append(' ').Append(currency);
            sb.Append(nl).Append((L10n.Get("label.expenses_colon") + " ")).Append(Money(totals.Expense)).Append(' ').Append(currency);
            if (totals.TopExpenseLabel.Length > 0)
                sb.Append(nl).Append(("  " + L10n.Get("report.top_expenses_colon") + " ")).Append(totals.TopExpenseLabel).Append(" — ")
                    .Append(Money(totals.TopExpenseAmount)).Append(' ').Append(currency);
            sb.Append(nl).Append((L10n.Get("label.utilities_colon") + " ")).Append(Money(totals.Utility)).Append(' ').Append(currency);
            sb.Append(nl).Append((L10n.Get("report.total_excl_util_colon") + " ")).Append(Money(totals.CostBase)).Append(' ').Append(currency);
            sb.Append(nl).Append((L10n.Get("report.total_incl_util_colon") + " ")).Append(Money(totals.CostBase + totals.Utility)).Append(' ').Append(currency);
            sb.Append(nl).Append((L10n.Get("report.cost_per_km_colon") + " ")).Append(costKm);
            sb.Append(nl).Append((L10n.Get("label.odometer_colon") + " ")).Append(kmRange);
            sb.Append(nl).Append((L10n.Get("report.fueled_colon") + " ")).Append(Money(totals.Liters)).Append((" " + L10n.Get("unit.liters_short")));
            sb.Append(nl).Append((L10n.Get("report.consumption_colon") + " ")).Append(consumption);
            if (budget.Length > 0)
                sb.Append(nl).Append((L10n.Get("report.budget_colon") + " ")).Append(budget);
            if (totals.TopStationLabel.Length > 0)
                sb.Append(nl).Append((L10n.Get("report.top_station_colon") + " ")).Append(totals.TopStationLabel).Append(" — ")
                    .Append(Money(totals.TopStationAmount)).Append(' ').Append(currency);
            sb.Append(nl).Append((L10n.Get("report.trips_personal_colon") + " ")).Append(Money(totals.PersonalTripKm)).Append((" " + L10n.Get("unit.km")));
            sb.Append(nl).Append((L10n.Get("report.trips_work_colon") + " ")).Append(Money(totals.WorkTripKm)).Append((" " + L10n.Get("unit.km")));
            return sb.ToString();
        }

        /// <summary>
        /// HTML built from <see cref="FormatText"/> so both exports always carry the same data.
        /// </summary>
        internal static string FormatHtml(
            string car,
            string from,
            string to,
            PeriodCompare.Totals totals,
            string currency)
        {
            string[] lines = FormatText(car, from, to, totals, currency)
                .Replace("\r", "").Split('\n');
            string title = lines.Length > 0 ? lines[0] : "";
            StringBuilder h = new StringBuilder();
            h.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/>");
            h.Append("<title>").Append(Esc(title)).Append("</title>");
            h.Append("<style>body{font-family:Arial,sans-serif;margin:16px;color:#233346}");
            h.Append("h1{font-size:18px}td{padding:4px 16px 4px 0}td.v{text-align:right}");
            h.Append("</style></head><body><h1>").Append(Esc(title)).Append("</h1><table>");
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                    continue;
                int split = line.IndexOf(": ");
                if (split < 0)
                    h.Append("<tr><td colspan=\"2\">").Append(Esc(line)).Append("</td></tr>");
                else
                    h.Append("<tr><td>").Append(Esc(line.Substring(0, split)))
                        .Append("</td><td class=\"v\">").Append(Esc(line.Substring(split + 2)))
                        .Append("</td></tr>");
            }
            h.Append("</table></body></html>");
            return h.ToString();
        }

        private static string Esc(string value)
        {
            return value.Replace("&", "&amp;").Replace("<", "&lt;")
                .Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string Money(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
