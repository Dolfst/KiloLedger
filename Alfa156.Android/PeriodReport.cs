using System;
using System.Globalization;
using System.Text;

using Alfa156.Localization;
namespace Alfa156.Android;

/// <summary>
/// Shareable period report for one car (1.34.0).
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
        Func<decimal, string> money,
        string nl)
    {
        return Format(car, from, to, totals, currency, money, nl, false);
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
        if (nl == null || nl.Length == 0)
            nl = "\n";

        string title = L10n.Get("report.header");
        string range = PeriodCompare.FormatRangeLabel(from, to);
        string costKm = totals.HasCostPerKm
            ? money(totals.CostPerKm) + " " + currency + L10n.Get("unit.per_km")
            : L10n.Get("common.na");
        string kmRange = totals.HasCostPerKm
            ? DefaultMoney(totals.MinKm) + "…" + DefaultMoney(totals.MaxKm) +
              (" " + L10n.Get("report.km_delta_open") + " ") + DefaultMoney(totals.MaxKm - totals.MinKm) + (" " + L10n.Get("unit.km_close"))
            : L10n.Get("common.na");
        string consumption = totals.HasConsumption
            ? DefaultMoney(totals.LitersPer100Km) + (" " + L10n.Get("unit.l_per_100km"))
            : L10n.Get("common.na");
        string budget = "";
        if (totals.PeriodBudget > 0)
        {
            decimal spent = totals.CostBase;
            budget = money(spent) + " / " + money(totals.PeriodBudget) + " " + currency +
                " (" + DefaultMoney(spent * 100m / totals.PeriodBudget) + "%)";
            if (spent >= totals.PeriodBudget)
                budget += (" " + L10n.Get("report.budget_exceeded"));
        }

        if (!html)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(title);
            sb.Append(nl).Append((L10n.Get("label.car_colon") + " ")).Append(car ?? "");
            sb.Append(nl).Append((L10n.Get("report.period_colon") + " ")).Append(range);
            sb.Append(nl);
            sb.Append(nl).Append((L10n.Get("label.fuel_colon") + " ")).Append(money(totals.Fuel)).Append(' ').Append(currency);
            sb.Append(nl).Append((L10n.Get("label.repairs_colon") + " ")).Append(money(totals.Repair)).Append(' ').Append(currency);
            if (totals.TopRepairLabel.Length > 0)
                sb.Append(nl).Append(("  " + L10n.Get("report.top_repairs_colon") + " ")).Append(totals.TopRepairLabel).Append(" — ")
                    .Append(money(totals.TopRepairAmount)).Append(' ').Append(currency);
            sb.Append(nl).Append((L10n.Get("label.expenses_colon") + " ")).Append(money(totals.Expense)).Append(' ').Append(currency);
            if (totals.TopExpenseLabel.Length > 0)
                sb.Append(nl).Append(("  " + L10n.Get("report.top_expenses_colon") + " ")).Append(totals.TopExpenseLabel).Append(" — ")
                    .Append(money(totals.TopExpenseAmount)).Append(' ').Append(currency);
            sb.Append(nl).Append((L10n.Get("label.utilities_colon") + " ")).Append(money(totals.Utility)).Append(' ').Append(currency);
            sb.Append(nl).Append((L10n.Get("report.total_excl_util_colon") + " ")).Append(money(totals.CostBase)).Append(' ').Append(currency);
            sb.Append(nl).Append((L10n.Get("report.total_incl_util_colon") + " ")).Append(money(totals.CostBase + totals.Utility)).Append(' ').Append(currency);
            sb.Append(nl).Append((L10n.Get("report.cost_per_km_colon") + " ")).Append(costKm);
            sb.Append(nl).Append((L10n.Get("label.odometer_colon") + " ")).Append(kmRange);
            sb.Append(nl).Append((L10n.Get("report.fueled_colon") + " ")).Append(DefaultMoney(totals.Liters)).Append((" " + L10n.Get("unit.liters_short")));
            sb.Append(nl).Append((L10n.Get("report.consumption_colon") + " ")).Append(consumption);
            if (budget.Length > 0)
                sb.Append(nl).Append((L10n.Get("report.budget_colon") + " ")).Append(budget);
            if (totals.TopStationLabel.Length > 0)
                sb.Append(nl).Append((L10n.Get("report.top_station_colon") + " ")).Append(totals.TopStationLabel).Append(" — ")
                    .Append(money(totals.TopStationAmount)).Append(' ').Append(currency);
            sb.Append(nl).Append((L10n.Get("report.trips_personal_colon") + " ")).Append(DefaultMoney(totals.PersonalTripKm)).Append((" " + L10n.Get("unit.km")));
            sb.Append(nl).Append((L10n.Get("report.trips_work_colon") + " ")).Append(DefaultMoney(totals.WorkTripKm)).Append((" " + L10n.Get("unit.km")));
            return sb.ToString();
        }

        StringBuilder h = new StringBuilder();
        h.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/>");
        h.Append("<title>").Append(Esc(title)).Append("</title>");
        h.Append("<style>body{font-family:sans-serif;margin:16px;color:#233346}");
        h.Append("h1{font-size:18px;margin:0 0 12px}table{border-collapse:collapse;margin-top:12px}");
        h.Append("td{padding:4px 16px 4px 0}td.v{text-align:right}");
        h.Append(".meta{color:#555;margin:0 0 4px}</style></head><body>");
        h.Append("<h1>").Append(Esc(title)).Append("</h1>");
        h.Append("<p class=\"meta\">Авто: ").Append(Esc(car ?? "")).Append("</p>");
        h.Append("<p class=\"meta\">Период: ").Append(Esc(range)).Append("</p>");
        h.Append("<table>");
        AppendRow(h, L10n.Get("label.fuel"), money(totals.Fuel) + " " + currency);
        AppendRow(h, L10n.Get("tab.repairs"), money(totals.Repair) + " " + currency);
        if (totals.TopRepairLabel.Length > 0)
            AppendRow(h, L10n.Get("report.top_repairs"), totals.TopRepairLabel + " — " +
                money(totals.TopRepairAmount) + " " + currency);
        AppendRow(h, L10n.Get("tab.expenses"), money(totals.Expense) + " " + currency);
        if (totals.TopExpenseLabel.Length > 0)
            AppendRow(h, L10n.Get("report.top_expenses"), totals.TopExpenseLabel + " — " +
                money(totals.TopExpenseAmount) + " " + currency);
        AppendRow(h, L10n.Get("label.utilities_full"), money(totals.Utility) + " " + currency);
        AppendRow(h, L10n.Get("report.total_excl_util"), money(totals.CostBase) + " " + currency);
        AppendRow(h, L10n.Get("report.total_incl_util_label"), money(totals.CostBase + totals.Utility) + " " + currency);
        AppendRow(h, L10n.Get("label.cost_per_km"), costKm);
        AppendRow(h, L10n.Get("label.odometer"), kmRange);
        AppendRow(h, L10n.Get("label.fueled"), DefaultMoney(totals.Liters) + (" " + L10n.Get("unit.liters_short")));
        AppendRow(h, L10n.Get("label.consumption"), consumption);
        if (budget.Length > 0)
            AppendRow(h, L10n.Get("label.budget"), budget);
        if (totals.TopStationLabel.Length > 0)
            AppendRow(h, L10n.Get("report.top_station"), totals.TopStationLabel + " — " +
                money(totals.TopStationAmount) + " " + currency);
        AppendRow(h, L10n.Get("trips.personal"), DefaultMoney(totals.PersonalTripKm) + (" " + L10n.Get("unit.km")));
        AppendRow(h, L10n.Get("trips.work"), DefaultMoney(totals.WorkTripKm) + (" " + L10n.Get("unit.km")));
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
