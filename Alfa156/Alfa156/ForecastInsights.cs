using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CarExpenses
{
    /// <summary>
    /// Summary «Оценка»: average km/day from fuel/repair readings (30–90 days)
    /// and estimated km/days until next oil / belt service. CF 3.5 safe.
    /// </summary>
    internal static class ForecastInsights
    {
        private const int MinSpanDays = 30;
        private const int MaxLookbackDays = 90;

        private sealed class Reading
        {
            public DateTime Day;
            public decimal Km;
        }

        /// <summary>
        /// Returns a summary block (with leading newlines) or empty if data is insufficient.
        /// </summary>
        internal static string FormatBlock(
            string car,
            List<string[]> fuels,
            List<string[]> repairs,
            decimal currentMileage,
            decimal lastOilMileage,
            decimal lastBeltMileage,
            decimal oilInterval,
            decimal beltInterval,
            DateTime today,
            string nl)
        {
            if (nl == null || nl.Length == 0)
                nl = "\r\n";

            decimal avgKmPerDay;
            int spanDays;
            if (!TryAverageKmPerDay(car, fuels, repairs, today, out avgKmPerDay, out spanDays))
                return "";

            StringBuilder sb = new StringBuilder();
            sb.Append(nl).Append(nl).Append("Оценка пробега:");
            sb.Append(nl).Append("Средний: ~").Append(Money(avgKmPerDay))
                .Append(" км/день (за ")
                .Append(spanDays.ToString(CultureInfo.InvariantCulture))
                .Append(" дн.)");

            if (lastOilMileage > 0 && oilInterval > 0)
                sb.Append(nl).Append(FormatServiceEstimate(
                    "До замены масла", currentMileage, lastOilMileage, oilInterval, avgKmPerDay));
            if (lastBeltMileage > 0 && beltInterval > 0)
                sb.Append(nl).Append(FormatServiceEstimate(
                    "До ремня ГРМ", currentMileage, lastBeltMileage, beltInterval, avgKmPerDay));

            return sb.ToString();
        }

        internal static bool TryAverageKmPerDay(
            string car,
            List<string[]> fuels,
            List<string[]> repairs,
            DateTime today,
            out decimal avgKmPerDay,
            out int spanDays)
        {
            avgKmPerDay = 0;
            spanDays = 0;
            car = car == null ? "" : car.Trim();
            if (car.Length == 0)
                return false;

            DateTime windowStart = today.Date.AddDays(-MaxLookbackDays);
            List<Reading> readings = new List<Reading>();
            Collect(readings, fuels, car, windowStart, today.Date, 5);
            Collect(readings, repairs, car, windowStart, today.Date, 8);
            if (readings.Count < 2)
                return false;

            Reading earliest = readings[0];
            Reading latest = readings[0];
            for (int i = 1; i < readings.Count; i++)
            {
                Reading r = readings[i];
                if (r.Day < earliest.Day ||
                    (r.Day == earliest.Day && r.Km < earliest.Km))
                    earliest = r;
                if (r.Day > latest.Day ||
                    (r.Day == latest.Day && r.Km > latest.Km))
                    latest = r;
            }

            spanDays = (latest.Day - earliest.Day).Days;
            if (spanDays < MinSpanDays || spanDays > MaxLookbackDays)
                return false;

            decimal kmDelta = latest.Km - earliest.Km;
            if (kmDelta <= 0)
                return false;

            avgKmPerDay = kmDelta / spanDays;
            return avgKmPerDay > 0;
        }

        private static void Collect(
            List<Reading> readings,
            List<string[]> rows,
            string car,
            DateTime windowStart,
            DateTime today,
            int minFields)
        {
            if (rows == null)
                return;
            for (int i = 0; i < rows.Count; i++)
            {
                string[] content = RecordIds.StripId(rows[i]);
                if (content == null || content.Length < minFields)
                    continue;
                if (!SameCar(content[1], car))
                    continue;
                DateTime day;
                if (!TryParseDay(content[0], out day))
                    continue;
                if (day < windowStart || day > today)
                    continue;
                decimal km;
                if (!DataStore.TryNumber(content[2], out km) || km < 0)
                    continue;
                Reading r = new Reading();
                r.Day = day;
                r.Km = km;
                readings.Add(r);
            }
        }

        private static string FormatServiceEstimate(
            string title,
            decimal currentMileage,
            decimal lastMileage,
            decimal interval,
            decimal avgKmPerDay)
        {
            decimal remaining = interval - (currentMileage - lastMileage);
            if (remaining <= 0)
                return title + ": срок прошёл (оценка)";
            if (avgKmPerDay <= 0)
                return title + ": ~" + Money(remaining) + " км (оценка)";
            decimal daysExact = remaining / avgKmPerDay;
            int days = (int)(daysExact + 0.5m);
            if (days < 1)
                days = 1;
            return title + ": ~" + Money(remaining) + " км ≈ " +
                days.ToString(CultureInfo.InvariantCulture) + " дн. (оценка)";
        }

        private static bool SameCar(string rowCar, string car)
        {
            return String.Compare((rowCar ?? "").Trim(), car,
                StringComparison.OrdinalIgnoreCase) == 0;
        }

        /// <summary>CF-safe parse of yyyy-MM-dd (optional time suffix ignored).</summary>
        private static bool TryParseDay(string raw, out DateTime day)
        {
            day = DateTime.MinValue;
            if (raw == null)
                return false;
            string trimmed = raw.Trim();
            if (trimmed.Length < 10 || trimmed[4] != '-' || trimmed[7] != '-')
                return false;
            try
            {
                int year = int.Parse(trimmed.Substring(0, 4), NumberStyles.None, CultureInfo.InvariantCulture);
                int month = int.Parse(trimmed.Substring(5, 2), NumberStyles.None, CultureInfo.InvariantCulture);
                int d = int.Parse(trimmed.Substring(8, 2), NumberStyles.None, CultureInfo.InvariantCulture);
                day = new DateTime(year, month, d);
                return true;
            }
            catch (FormatException) { return false; }
            catch (OverflowException) { return false; }
            catch (ArgumentOutOfRangeException) { return false; }
        }

        private static string Money(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
