using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Alfa156.Localization;
namespace Alfa156.Android;

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
                string name = (cars[i] ?? "").Trim();
                if (name.Length == 0 || byName.ContainsKey(name))
                    continue;
                Stats s = new Stats { Name = name };
                byName.Add(name, s);
                list.Add(s);
            }
        }

        if (fuels != null)
        {
            List<string[]> sorted = new List<string[]>(fuels);
            sorted.Sort(CompareFuelForConsumption);
            Dictionary<string, string[]> lastFull =
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < sorted.Count; i++)
            {
                string[] row = sorted[i];
                string[] content = RecordIds.StripId(row);
                if (content.Length < 5)
                    continue;
                string car = (content[1] ?? "").Trim();
                Stats s = Ensure(byName, list, car);
                if (s == null)
                    continue;
                s.FuelTotal += NumberOrZero(content[4]);
                AddKm(s, content[2]);
                if (!ExpenseDataCodec.IsFullTank(row))
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

    internal static string FormatBlock(List<Stats> stats, string currency, Func<decimal, string> money)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(L10n.Get("summary.car_compare_colon"));
        if (stats == null || stats.Count == 0)
        {
            sb.Append(("\n" + L10n.Get("summary.no_cars")));
            return sb.ToString();
        }
        for (int i = 0; i < stats.Count; i++)
        {
            Stats s = stats[i];
            decimal total = s.FuelTotal + s.RepairTotal + s.ExpenseTotal;
            sb.Append("\n").Append(s.Name).Append(':');
            sb.Append(("\n  " + L10n.Get("compare.fuel") + " ")).Append(money(s.FuelTotal)).Append(' ').Append(currency);
            sb.Append(("\n  " + L10n.Get("compare.repairs") + " ")).Append(money(s.RepairTotal)).Append(' ').Append(currency);
            sb.Append(("\n  " + L10n.Get("compare.expenses") + " ")).Append(money(s.ExpenseTotal)).Append(' ').Append(currency);
            sb.Append(("\n  " + L10n.Get("compare.total") + " ")).Append(money(total)).Append(' ').Append(currency);
            sb.Append(("\n  " + L10n.Get("compare.cost_per_km") + " "));
            if (s.MinKm != Decimal.MaxValue && s.MaxKm > s.MinKm)
                sb.Append(money(total / (s.MaxKm - s.MinKm))).Append(' ').Append(currency).Append(L10n.Get("unit.per_km"));
            else
                sb.Append(L10n.Get("common.na"));
            sb.Append(("\n  " + L10n.Get("compare.avg_l100") + " "));
            if (s.FullTankDistance > 0)
                sb.Append((s.FullTankLiters * 100m / s.FullTankDistance)
                    .ToString("0.0", CultureInfo.InvariantCulture));
            else
                sb.Append(L10n.Get("common.na"));
            sb.Append(("\n  " + L10n.Get("integrity.field_odometer") + " ")).Append(money(s.CurrentKm)).Append((" " + L10n.Get("unit.km")));
        }
        return sb.ToString();
    }

    private static Stats Ensure(Dictionary<string, Stats> byName, List<Stats> list, string car)
    {
        car = (car ?? "").Trim();
        if (car.Length == 0)
            return null;
        Stats s;
        if (byName.TryGetValue(car, out s))
            return s;
        s = new Stats { Name = car };
        byName.Add(car, s);
        list.Add(s);
        return s;
    }

    private static void AddKm(Stats s, string value)
    {
        decimal km;
        if (!TryNumber(value, out km) || km < 0)
            return;
        s.CurrentKm = Math.Max(s.CurrentKm, km);
        s.MinKm = Math.Min(s.MinKm, km);
        s.MaxKm = Math.Max(s.MaxKm, km);
    }

    private static int CompareFuelForConsumption(string[] a, string[] b)
    {
        string[] ca = RecordIds.StripId(a);
        string[] cb = RecordIds.StripId(b);
        string carA = ca.Length > 1 ? (ca[1] ?? "") : "";
        string carB = cb.Length > 1 ? (cb[1] ?? "") : "";
        int byCar = String.Compare(carA, carB, StringComparison.OrdinalIgnoreCase);
        if (byCar != 0)
            return byCar;
        string dateA = ca.Length > 0 ? (ca[0] ?? "") : "";
        string dateB = cb.Length > 0 ? (cb[0] ?? "") : "";
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
        return Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number);
    }

    private static decimal NumberOrZero(string value)
    {
        decimal n;
        return TryNumber(value, out n) ? n : 0;
    }
}
