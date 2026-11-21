using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CarExpenses
{
    /// <summary>
    /// Totals for one date range used by «Сравнить периоды» in the summary.
    /// KPK: this-month vs previous-month only (no custom ranges in UI).
    /// </summary>
    internal static class PeriodCompare
    {
        internal sealed class Totals
        {
            public decimal Fuel;
            public decimal Repair;
            public decimal Expense;
            public decimal Utility;
            public decimal MinKm = Decimal.MaxValue;
            public decimal MaxKm;
            public string TopRepairLabel = "";
            public decimal TopRepairAmount;
            public string TopExpenseLabel = "";
            public decimal TopExpenseAmount;

            public decimal CostBase
            {
                get { return Fuel + Repair + Expense; }
            }

            public bool HasCostPerKm
            {
                get { return MinKm != Decimal.MaxValue && MaxKm > MinKm; }
            }

            public decimal CostPerKm
            {
                get { return HasCostPerKm ? CostBase / (MaxKm - MinKm) : 0; }
            }
        }

        internal static void ThisVsPreviousMonth(
            out string aFrom, out string aTo, out string bFrom, out string bTo)
        {
            DateTime today = DateTime.Today;
            DateTime thisStart = new DateTime(today.Year, today.Month, 1);
            DateTime prevStart = thisStart.AddMonths(-1);
            DateTime thisEnd = thisStart.AddMonths(1).AddDays(-1);
            DateTime prevEnd = thisStart.AddDays(-1);
            aFrom = FormatDay(thisStart);
            aTo = FormatDay(thisEnd);
            bFrom = FormatDay(prevStart);
            bTo = FormatDay(prevEnd);
        }

        internal static string FormatDay(DateTime day)
        {
            return day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        internal static string FormatRangeLabel(string from, string to)
        {
            return (from ?? "") + "…" + (to ?? "");
        }

        internal static Totals Build(
            string car,
            List<string[]> fuels,
            List<string[]> repairs,
            List<string[]> expenses,
            List<string[]> utilities,
            string from,
            string to)
        {
            Totals t = new Totals();
            car = (car ?? "").Trim();
            from = NormalizeDay(from);
            to = NormalizeDay(to);
            Dictionary<string, decimal> repairsByCategory =
                new Dictionary<string, decimal>();
            Dictionary<string, decimal> expensesByCategory =
                new Dictionary<string, decimal>();

            if (fuels != null)
            {
                for (int i = 0; i < fuels.Count; i++)
                {
                    string[] content = RecordIds.StripId(fuels[i]);
                    if (content.Length < 5)
                        continue;
                    if (!SameCar(content[1], car) || !InDayRange(content[0], from, to))
                        continue;
                    t.Fuel += NumberOrZero(content[4]);
                    AddKm(t, content[2]);
                }
            }

            if (repairs != null)
            {
                for (int i = 0; i < repairs.Count; i++)
                {
                    string[] content = RecordIds.StripId(repairs[i]);
                    if (content.Length < 8)
                        continue;
                    if (!SameCar(content[1], car) || !InDayRange(content[0], from, to))
                        continue;
                    t.Repair += NumberOrZero(content[7]);
                    AddKm(t, content[2]);
                    AddCategoryAmount(repairsByCategory,
                        content.Length > 3 ? content[3] : "", NumberOrZero(content[7]));
                }
            }

            if (expenses != null)
            {
                for (int i = 0; i < expenses.Count; i++)
                {
                    string[] content = RecordIds.StripId(expenses[i]);
                    if (content.Length < 5)
                        continue;
                    if (!SameCar(content[1], car) || !InDayRange(content[0], from, to))
                        continue;
                    t.Expense += NumberOrZero(content[4]);
                    AddCategoryAmount(expensesByCategory,
                        content.Length > 2 ? content[2] : "", NumberOrZero(content[4]));
                }
            }

            if (utilities != null)
            {
                for (int i = 0; i < utilities.Count; i++)
                {
                    string[] row = utilities[i];
                    if (row == null || row.Length == 0)
                        continue;
                    if (!InMonthRange(row[0], from, to))
                        continue;
                    t.Utility += UtilitySum(row);
                }
            }

            t.TopRepairLabel = TopCategory(repairsByCategory, out t.TopRepairAmount);
            t.TopExpenseLabel = TopCategory(expensesByCategory, out t.TopExpenseAmount);
            return t;
        }

        private static void AddCategoryAmount(Dictionary<string, decimal> totals, string label, decimal amount)
        {
            label = (label ?? "").Trim();
            if (label.Length == 0)
                label = "Без категории";
            if (!totals.ContainsKey(label))
                totals[label] = 0;
            totals[label] += amount;
        }

        private static string TopCategory(Dictionary<string, decimal> totals, out decimal amount)
        {
            string label = "";
            amount = 0;
            foreach (KeyValuePair<string, decimal> item in totals)
            {
                if (item.Value > amount)
                {
                    label = item.Key;
                    amount = item.Value;
                }
            }
            return label;
        }

        internal static string FormatComparison(
            Totals a,
            Totals b,
            string labelA,
            string labelB,
            string currency)
        {
            const string nl = "\r\n";
            StringBuilder sb = new StringBuilder();
            sb.Append("Сравнение периодов:");
            sb.Append(nl).Append("A: ").Append(labelA);
            sb.Append(nl).Append("B: ").Append(labelB);
            AppendMetric(sb, nl, "Топливо", a.Fuel, b.Fuel, currency);
            AppendMetric(sb, nl, "Ремонт", a.Repair, b.Repair, currency);
            AppendMetric(sb, nl, "Прочие", a.Expense, b.Expense, currency);
            AppendMetric(sb, nl, "Коммунальные", a.Utility, b.Utility, currency);
            AppendMetric(sb, nl, "Всего (без коммун.)", a.CostBase, b.CostBase, currency);
            sb.Append(nl).Append("Стоимость/км:");
            sb.Append(nl).Append("  A ").Append(FormatCostPerKm(a, currency));
            sb.Append(nl).Append("  B ").Append(FormatCostPerKm(b, currency));
            if (a.HasCostPerKm && b.HasCostPerKm)
            {
                decimal delta = a.CostPerKm - b.CostPerKm;
                sb.Append(nl).Append("  Δ ").Append(Signed(delta)).Append(' ').Append(currency).Append("/км");
            }
            else
                sb.Append(nl).Append("  Δ н/д");
            return sb.ToString();
        }

        private static void AppendMetric(
            StringBuilder sb,
            string nl,
            string title,
            decimal a,
            decimal b,
            string currency)
        {
            sb.Append(nl).Append(title).Append(':');
            sb.Append(nl).Append("  A ").Append(Money(a)).Append(' ').Append(currency);
            sb.Append(nl).Append("  B ").Append(Money(b)).Append(' ').Append(currency);
            sb.Append(nl).Append("  Δ ").Append(Signed(a - b)).Append(' ').Append(currency);
        }

        private static string FormatCostPerKm(Totals t, string currency)
        {
            if (!t.HasCostPerKm)
                return "н/д";
            return Money(t.CostPerKm) + " " + currency + "/км";
        }

        private static string Signed(decimal delta)
        {
            if (delta > 0)
                return "+" + Money(delta);
            return Money(delta);
        }

        private static string Money(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static bool SameCar(string rowCar, string car)
        {
            return String.Compare((rowCar ?? "").Trim(), car, StringComparison.OrdinalIgnoreCase) == 0;
        }

        private static string NormalizeDay(string value)
        {
            if (value == null)
                return "";
            value = value.Trim();
            if (value.Length >= 10)
                return value.Substring(0, 10);
            if (value.Length == 7 && value[4] == '-')
                return value + "-01";
            return value;
        }

        private static bool InDayRange(string date, string from, string to)
        {
            if (date == null || date.Length < 10)
                return false;
            string d = date.Substring(0, 10);
            if (from.Length > 0 && String.CompareOrdinal(d, from) < 0)
                return false;
            if (to.Length > 0 && String.CompareOrdinal(d, to) > 0)
                return false;
            return true;
        }

        private static bool InMonthRange(string monthOrDate, string from, string to)
        {
            if (monthOrDate == null || monthOrDate.Length < 7)
                return false;
            string m = monthOrDate.Substring(0, 7);
            string fromM = from.Length >= 7 ? from.Substring(0, 7) : "";
            string toM = to.Length >= 7 ? to.Substring(0, 7) : "";
            if (fromM.Length > 0 && String.CompareOrdinal(m, fromM) < 0)
                return false;
            if (toM.Length > 0 && String.CompareOrdinal(m, toM) > 0)
                return false;
            return true;
        }

        private static void AddKm(Totals t, string value)
        {
            decimal km;
            if (!DataStore.TryNumber(value, out km) || km < 0)
                return;
            // CF 3.5: Math.Min/Max have no decimal overloads.
            if (km < t.MinKm)
                t.MinKm = km;
            if (km > t.MaxKm)
                t.MaxKm = km;
        }

        private static decimal NumberOrZero(string value)
        {
            return DataStore.NumberOrZero(value);
        }

        private static decimal UtilitySum(string[] row)
        {
            decimal total = 0;
            for (int i = 1; i < row.Length && i < 8; i++)
                total += NumberOrZero(row[i]);
            return total;
        }
    }
}
