using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CarExpenses
{
    /// <summary>
    /// Per-car totals for the summary «Сравнение автомобилей» block.
    /// </summary>
    internal static class CarCompare
    {
        internal sealed class Stats
        {
            public string Name = "";
            public decimal FuelTotal;
            public decimal RepairTotal;
            public decimal ExpenseTotal;
            public decimal MinKm = Decimal.MaxValue;
            public decimal MaxKm;
            public decimal CurrentKm;
            public decimal FullTankLiters;
            public decimal FullTankDistance;
        }

        internal static List<Stats> Build(
            List<string> cars,
            List<string[]> fuels,
            List<string[]> repairs,
            List<string[]> expenses)
        {
            List<Stats> list = new List<Stats>();
            Dictionary<string, Stats> byName =
                new Dictionary<string, Stats>(StringComparer.OrdinalIgnoreCase);
            if (cars != null)
            {
                for (int i = 0; i < cars.Count; i++)
                {
                    string name = cars[i] == null ? "" : cars[i].Trim();
                    if (name.Length == 0 || byName.ContainsKey(name))
                        continue;
                    Stats s = new Stats();
                    s.Name = name;
                    byName.Add(name, s);
                    list.Add(s);
                }
            }

            if (fuels != null)
            {
                List<string[]> sorted = new List<string[]>(fuels);
                sorted.Sort(new Comparison<string[]>(CompareFuelForConsumption));
                Dictionary<string, string[]> lastFull =
                    new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < sorted.Count; i++)
                {
                    string[] row = sorted[i];
                    string[] content = RecordIds.StripId(row);
                    if (content.Length < 5)
                        continue;
                    string car = content[1] == null ? "" : content[1].Trim();
                    Stats s = Ensure(byName, list, car);
                    if (s == null)
                        continue;
                    s.FuelTotal += NumberOrZero(content[4]);
                    AddKm(s, content[2]);
                    if (!IsFullTank(row))
                        continue;
                    string[] prev;
                    if (lastFull.TryGetValue(car, out prev))
                    {
                        decimal prevKm, curKm, liters;
                        if (TryNumber(prev[2], out prevKm) && TryNumber(content[2], out curKm) &&
                            TryNumber(content[3], out liters) && curKm > prevKm && liters > 0)
                        {
                            s.FullTankLiters += liters;
                            s.FullTankDistance += curKm - prevKm;
                        }
                    }
                    lastFull[car] = content;
                }
            }

            if (repairs != null)
            {
                for (int i = 0; i < repairs.Count; i++)
                {
                    string[] content = RecordIds.StripId(repairs[i]);
                    if (content.Length < 8)
                        continue;
                    Stats s = Ensure(byName, list, content[1]);
                    if (s == null)
                        continue;
                    s.RepairTotal += NumberOrZero(content[7]);
                    AddKm(s, content[2]);
                }
            }

            if (expenses != null)
            {
                for (int i = 0; i < expenses.Count; i++)
                {
                    string[] content = RecordIds.StripId(expenses[i]);
                    if (content.Length < 5)
                        continue;
                    Stats s = Ensure(byName, list, content[1]);
                    if (s == null)
                        continue;
                    s.ExpenseTotal += NumberOrZero(content[4]);
                }
            }

            return list;
        }

        internal static string FormatBlock(List<Stats> stats, string currency)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Сравнение автомобилей:");
            if (stats == null || stats.Count == 0)
            {
                sb.Append("\r\nнет автомобилей");
                return sb.ToString();
            }
            for (int i = 0; i < stats.Count; i++)
            {
                Stats s = stats[i];
                decimal total = s.FuelTotal + s.RepairTotal + s.ExpenseTotal;
                sb.Append("\r\n").Append(s.Name).Append(':');
                sb.Append("\r\n  топливо ").Append(Money(s.FuelTotal)).Append(' ').Append(currency);
                sb.Append("\r\n  ремонт ").Append(Money(s.RepairTotal)).Append(' ').Append(currency);
                sb.Append("\r\n  прочие ").Append(Money(s.ExpenseTotal)).Append(' ').Append(currency);
                sb.Append("\r\n  всего ").Append(Money(total)).Append(' ').Append(currency);
                sb.Append("\r\n  стоимость/км ");
                if (s.MinKm != Decimal.MaxValue && s.MaxKm > s.MinKm)
                    sb.Append(Money(total / (s.MaxKm - s.MinKm))).Append(' ').Append(currency).Append("/км");
                else
                    sb.Append("н/д");
                sb.Append("\r\n  ср. л/100км ");
                if (s.FullTankDistance > 0)
                    sb.Append((s.FullTankLiters * 100m / s.FullTankDistance)
                        .ToString("0.0", CultureInfo.InvariantCulture));
                else
                    sb.Append("н/д");
                sb.Append("\r\n  пробег ").Append(Money(s.CurrentKm)).Append(" км");
            }
            return sb.ToString();
        }

        private static bool IsFullTank(string[] row)
        {
            string[] content = RecordIds.StripId(row);
            if (content == null || content.Length <= 7)
                return true;
            string flag = content[7] == null ? "" : content[7].Trim();
            return flag.Length == 0 || flag == "1";
        }

        private static Stats Ensure(Dictionary<string, Stats> byName, List<Stats> list, string car)
        {
            car = car == null ? "" : car.Trim();
            if (car.Length == 0)
                return null;
            Stats s;
            if (byName.TryGetValue(car, out s))
                return s;
            s = new Stats();
            s.Name = car;
            byName.Add(car, s);
            list.Add(s);
            return s;
        }

        private static void AddKm(Stats s, string value)
        {
            decimal km;
            if (!TryNumber(value, out km) || km < 0)
                return;
            if (km > s.CurrentKm)
                s.CurrentKm = km;
            if (km < s.MinKm)
                s.MinKm = km;
            if (km > s.MaxKm)
                s.MaxKm = km;
        }

        private static int CompareFuelForConsumption(string[] a, string[] b)
        {
            string[] ca = RecordIds.StripId(a);
            string[] cb = RecordIds.StripId(b);
            string carA = ca.Length > 1 ? (ca[1] == null ? "" : ca[1]) : "";
            string carB = cb.Length > 1 ? (cb[1] == null ? "" : cb[1]) : "";
            int byCar = String.Compare(carA, carB, StringComparison.OrdinalIgnoreCase);
            if (byCar != 0)
                return byCar;
            string dateA = ca.Length > 0 ? (ca[0] == null ? "" : ca[0]) : "";
            string dateB = cb.Length > 0 ? (cb[0] == null ? "" : cb[0]) : "";
            int byDate = String.CompareOrdinal(dateA, dateB);
            if (byDate != 0)
                return byDate;
            decimal kmA, kmB;
            TryNumber(ca.Length > 2 ? ca[2] : "", out kmA);
            TryNumber(cb.Length > 2 ? cb[2] : "", out kmB);
            return kmA.CompareTo(kmB);
        }

        private static bool TryNumber(string value, out decimal number)
        {
            return DataStore.TryNumber(value, out number);
        }

        private static decimal NumberOrZero(string value)
        {
            return DataStore.NumberOrZero(value);
        }

        private static string Money(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
