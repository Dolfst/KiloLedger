using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using KiloLedger.Localization;
namespace KiloLedger.Desktop.Data
{
    /// <summary>
    /// Summary «Оценка»: average km/day from fuel/repair readings (30–90 days)
    /// and estimated km/days until next oil / belt service.
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
            if (String.IsNullOrEmpty(nl))
                nl = "\r\n";

            decimal avgKmPerDay;
            int spanDays;
            if (!TryAverageKmPerDay(car, fuels, repairs, today, out avgKmPerDay, out spanDays))
                return "";

            StringBuilder sb = new StringBuilder();
            sb.Append(nl).Append(nl).Append(L10n.Get("forecast.title_colon"));
            sb.Append(nl).Append(L10n.Get("forecast.avg_tilde")).Append(Money(avgKmPerDay))
                .Append((" " + L10n.Get("forecast.km_per_day_in") + " "))
                .Append(spanDays.ToString(CultureInfo.InvariantCulture))
                .Append((" " + L10n.Get("unit.days_close")));

            if (lastOilMileage > 0 && oilInterval > 0)
                sb.Append(nl).Append(FormatServiceEstimate(
                    L10n.Get("forecast.until_oil"), currentMileage, lastOilMileage, oilInterval, avgKmPerDay));
            if (lastBeltMileage > 0 && beltInterval > 0)
                sb.Append(nl).Append(FormatServiceEstimate(
                    L10n.Get("forecast.until_belt"), currentMileage, lastBeltMileage, beltInterval, avgKmPerDay));

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
            car = (car ?? "").Trim();
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
                if (!Decimal.TryParse(content[2], NumberStyles.Number,
                        CultureInfo.InvariantCulture, out km) || km < 0)
                    continue;
                readings.Add(new Reading { Day = day, Km = km });
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
                return title + L10n.Get("forecast.overdue_est");
            if (avgKmPerDay <= 0)
                return title + ": ~" + Money(remaining) + (" " + L10n.Get("forecast.km_est"));
            decimal daysExact = remaining / avgKmPerDay;
            int days = (int)(daysExact + 0.5m);
            if (days < 1)
                days = 1;
            return title + ": ~" + Money(remaining) + (" " + L10n.Get("forecast.km_approx") + " ") +
                days.ToString(CultureInfo.InvariantCulture) + (" " + L10n.Get("forecast.days_est"));
        }

        private static bool SameCar(string rowCar, string car)
        {
            return String.Equals((rowCar ?? "").Trim(), car, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseDay(string raw, out DateTime day)
        {
            day = DateTime.MinValue;
            if (raw == null)
                return false;
            string trimmed = raw.Trim();
            if (trimmed.Length < 10)
                return false;
            return DateTime.TryParseExact(trimmed.Substring(0, 10), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
        }

        private static string Money(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
