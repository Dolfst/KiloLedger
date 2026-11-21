using System;
using System.Globalization;
using System.Text;

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
            string title = "Alfa 156 · отчёт за период";
            string range = PeriodCompare.FormatRangeLabel(from, to);
            string costKm = totals.HasCostPerKm
                ? Money(totals.CostPerKm) + " " + currency + "/км"
                : "н/д";
            string kmRange = totals.HasCostPerKm
                ? Money(totals.MinKm) + "…" + Money(totals.MaxKm) +
                  " км (Δ " + Money(totals.MaxKm - totals.MinKm) + " км)"
                : "н/д";

            StringBuilder sb = new StringBuilder();
            sb.Append(title);
            sb.Append(nl).Append("Авто: ").Append(car ?? "");
            sb.Append(nl).Append("Период: ").Append(range);
            sb.Append(nl);
            sb.Append(nl).Append("Топливо: ").Append(Money(totals.Fuel)).Append(' ').Append(currency);
            sb.Append(nl).Append("Ремонт: ").Append(Money(totals.Repair)).Append(' ').Append(currency);
            sb.Append(nl).Append("Прочие: ").Append(Money(totals.Expense)).Append(' ').Append(currency);
            sb.Append(nl).Append("Коммунальные: ").Append(Money(totals.Utility)).Append(' ').Append(currency);
            sb.Append(nl).Append("Всего (без коммун.): ").Append(Money(totals.CostBase)).Append(' ').Append(currency);
            sb.Append(nl).Append("Всего (с коммунальными): ").Append(Money(totals.CostBase + totals.Utility)).Append(' ').Append(currency);
            sb.Append(nl).Append("Стоимость/км: ").Append(costKm);
            sb.Append(nl).Append("Пробег: ").Append(kmRange);
            return sb.ToString();
        }

        private static string Money(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
