using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Alfa156.Desktop.Data
{
    /// <summary>
    /// Totals for one date range used by «Сравнить периоды» in the summary.
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
            public string TopStationLabel = "";
            public decimal TopStationAmount;
            public decimal PersonalTripKm;
            public decimal WorkTripKm;

            public decimal MonthlyBudget;
            public int BudgetMonths;
            public decimal Liters;
            public decimal ConsumptionKm;
            public decimal ConsumptionLiters;

            public bool HasConsumption
            {
                get { return ConsumptionKm > 0 && ConsumptionLiters > 0; }
            }

            public decimal PeriodBudget
            {
                get { return MonthlyBudget > 0 && BudgetMonths > 0 ? MonthlyBudget * BudgetMonths : 0; }
            }

            public decimal LitersPer100Km
            {
                get { return HasConsumption ? ConsumptionLiters * 100m / ConsumptionKm : 0; }
            }

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

        internal static void AddTripKm(Totals totals, string car, List<string[]> trips,
            string from, string to)
        {
            if (totals == null || trips == null)
                return;
            for (int i = 0; i < trips.Count; i++)
            {
                string[] content = RecordIds.StripId(trips[i]);
                if (content.Length < 7)
                    continue;
                if (!SameCar(content[1], car) || !InDayRange(content[0], from, to))
                    continue;
                decimal startKm;
                decimal endKm;
                if (!AlfaCodec.TryNumber(content[2], out startKm) ||
                    !AlfaCodec.TryNumber(content[3], out endKm) || endKm < startKm)
                    continue;
                if (String.Equals((content[4] ?? "").Trim(), "Работа",
                    StringComparison.OrdinalIgnoreCase))
                    totals.WorkTripKm += endKm - startKm;
                else
                    totals.PersonalTripKm += endKm - startKm;
            }
        }

        internal static void ApplyBudget(Totals totals, decimal monthlyBudget, string from, string to)
        {
            from = NormalizeDay(from);
            to = NormalizeDay(to);
            if (totals == null || monthlyBudget <= 0 || from.Length < 7 || to.Length < 7)
                return;
            int months = (int.Parse(to.Substring(0, 4)) - int.Parse(from.Substring(0, 4))) * 12 +
                int.Parse(to.Substring(5, 2)) - int.Parse(from.Substring(5, 2)) + 1;
            if (months < 1)
                return;
            totals.MonthlyBudget = monthlyBudget;
            totals.BudgetMonths = months;
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
                new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, decimal> expensesByCategory =
                new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, decimal> stations =
                new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            List<decimal[]> fills = new List<decimal[]>();
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
                    decimal liters = NumberOrZero(content[3]);
                    t.Liters += liters;
                    decimal fillKm;
                    if (liters > 0 && AlfaCodec.TryNumber(content[2], out fillKm) && fillKm >= 0)
                        fills.Add(new decimal[] { fillKm, liters, AlfaCodec.IsFullTank(fuels[i]) ? 1 : 0 });
                    string station = content.Length > 9 ? (content[9] ?? "").Trim() : "";
                    if (station.Length > 0)
                        AddCategoryAmount(stations, station, NumberOrZero(content[4]));
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
                    AddCategoryAmount(repairsByCategory, content.Length > 3 ? content[3] : "", NumberOrZero(content[7]));
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
                    AddCategoryAmount(expensesByCategory, content.Length > 2 ? content[2] : "", NumberOrZero(content[4]));
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

            ApplyConsumption(t, fills);
            t.TopRepairLabel = TopCategory(repairsByCategory, out t.TopRepairAmount);
            t.TopExpenseLabel = TopCategory(expensesByCategory, out t.TopExpenseAmount);
            t.TopStationLabel = TopCategory(stations, out t.TopStationAmount);
            return t;
        }

        private static void ApplyConsumption(Totals t, List<decimal[]> fills)
        {
            bool found = false;
            decimal minKm = 0;
            decimal maxKm = 0;
            int fullCount = 0;
            for (int i = 0; i < fills.Count; i++)
            {
                if (fills[i][2] == 0)
                    continue;
                fullCount++;
                if (!found || fills[i][0] < minKm)
                    minKm = fills[i][0];
                if (!found || fills[i][0] > maxKm)
                    maxKm = fills[i][0];
                found = true;
            }
            if (fullCount < 2 || maxKm <= minKm)
                return;
            decimal liters = 0;
            for (int i = 0; i < fills.Count; i++)
            {
                if (fills[i][0] > minKm && fills[i][0] <= maxKm)
                    liters += fills[i][1];
            }
            if (liters > 0)
            {
                t.ConsumptionKm = maxKm - minKm;
                t.ConsumptionLiters = liters;
            }
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
            string currency,
            Func<decimal, string> money)
        {
            const string nl = "\r\n";
            StringBuilder sb = new StringBuilder();
            sb.Append("Сравнение периодов:");
            sb.Append(nl).Append("A: ").Append(labelA);
            sb.Append(nl).Append("B: ").Append(labelB);
            AppendMetric(sb, nl, "Топливо", a.Fuel, b.Fuel, currency, money);
            AppendMetric(sb, nl, "Ремонт", a.Repair, b.Repair, currency, money);
            AppendMetric(sb, nl, "Прочие", a.Expense, b.Expense, currency, money);
            AppendMetric(sb, nl, "Коммунальные", a.Utility, b.Utility, currency, money);
            AppendMetric(sb, nl, "Всего (без коммун.)", a.CostBase, b.CostBase, currency, money);
            sb.Append(nl).Append("Стоимость/км:");
            sb.Append(nl).Append("  A ").Append(FormatCostPerKm(a, currency, money));
            sb.Append(nl).Append("  B ").Append(FormatCostPerKm(b, currency, money));
            if (a.HasCostPerKm && b.HasCostPerKm)
            {
                decimal delta = a.CostPerKm - b.CostPerKm;
                sb.Append(nl).Append("  Δ ").Append(Signed(money, delta)).Append(' ').Append(currency).Append("/км");
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
            string currency,
            Func<decimal, string> money)
        {
            sb.Append(nl).Append(title).Append(':');
            sb.Append(nl).Append("  A ").Append(money(a)).Append(' ').Append(currency);
            sb.Append(nl).Append("  B ").Append(money(b)).Append(' ').Append(currency);
            sb.Append(nl).Append("  Δ ").Append(Signed(money, a - b)).Append(' ').Append(currency);
        }

        private static string FormatCostPerKm(Totals t, string currency, Func<decimal, string> money)
        {
            if (!t.HasCostPerKm)
                return "н/д";
            return money(t.CostPerKm) + " " + currency + "/км";
        }

        private static string Signed(Func<decimal, string> money, decimal delta)
        {
            if (delta > 0)
                return "+" + money(delta);
            return money(delta);
        }

        private static bool SameCar(string rowCar, string car)
        {
            return String.Equals((rowCar ?? "").Trim(), car, StringComparison.OrdinalIgnoreCase);
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
            if (!Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out km) || km < 0)
                return;
            t.MinKm = Math.Min(t.MinKm, km);
            t.MaxKm = Math.Max(t.MaxKm, km);
        }

        private static decimal NumberOrZero(string value)
        {
            decimal n;
            return Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out n) ? n : 0;
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
