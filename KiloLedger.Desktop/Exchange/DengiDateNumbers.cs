using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace KiloLedger.Desktop.Exchange
{
    internal enum DateNumberKind
    {
        NotDate = 0,
        Recovered = 1,
        Ambiguous = 2
    }

    /// <summary>
    /// Google Sheets (русская локаль) иногда записывает число вроде 27.3 как дату
    /// 27.03.2026. В xlsx остаётся серийный номер (46108). Для числового поля
    /// день.месяц восстанавливается в десятичную дробь: 27.03 → 27.3, 05.10 → 5.1.
    /// Если день и месяц не дают одно число без времени, серийный номер не используется.
    /// </summary>
    internal static class DengiDateNumbers
    {
        internal const int MinYear = 1980;
        internal const int MaxYear = 2100;

        private const double MinSerial = 20000;
        private const double MaxSerial = 80000;
        private const double TimeEpsilon = 0.000001;

        private static readonly Regex DayMonthYear = new Regex(
            @"^(\d{1,2})[./-](\d{1,2})[./-](\d{4})(?:[ T](\d{1,2}):(\d{2})(?::(\d{2}))?)?$",
            RegexOptions.CultureInvariant);

        private static readonly Regex IsoDay = new Regex(
            @"^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2})(?::(\d{2}))?(?:\.\d+)?(?:Z|[+-]\d{2}:?\d{2})?)?$",
            RegexOptions.CultureInvariant);

        private static readonly Regex QuotedOrBracket = new Regex(
            "\"[^\"]*\"|\\[[^\\]]*\\]",
            RegexOptions.CultureInvariant);

        internal static bool IsDayMonthFormat(string formatCode)
        {
            if (String.IsNullOrEmpty(formatCode))
                return false;
            string stripped = QuotedOrBracket.Replace(formatCode, "");
            return stripped.IndexOf('d') >= 0 || stripped.IndexOf('D') >= 0;
        }

        internal static DateNumberKind Classify(string text, bool dateFormatted, out string number)
        {
            number = null;
            string raw = (text ?? "").Trim();
            if (raw.Length == 0 || raw[0] == '#')
                return DateNumberKind.NotDate;

            int year;
            int month;
            int day;
            bool hasTime;
            if (TryParseDayMonthYear(raw, out year, out month, out day, out hasTime) ||
                TryParseIso(raw, out year, out month, out day, out hasTime))
                return FinishCalendar(year, month, day, hasTime, out number);

            double serial;
            if (dateFormatted && TrySerial(raw, out serial))
            {
                if (serial > MinSerial && serial < MaxSerial)
                    return FinishSerial(serial, out number);
                return DateNumberKind.NotDate;
            }

            if (dateFormatted)
                return DateNumberKind.Ambiguous;
            return DateNumberKind.NotDate;
        }

        internal static string FormatDayMonth(int day, int month)
        {
            decimal scale = month >= 10 ? 100m : 10m;
            decimal value = day + (month / scale);
            string text = value.ToString(CultureInfo.InvariantCulture);
            int dot = text.IndexOf('.');
            if (dot >= 0)
            {
                text = text.TrimEnd('0');
                if (text.EndsWith(".", StringComparison.Ordinal))
                    text = text.Substring(0, text.Length - 1);
            }
            return text;
        }

        private static DateNumberKind FinishSerial(double serial, out string number)
        {
            number = null;
            double whole = Math.Floor(serial);
            double frac = serial - whole;
            if (frac > TimeEpsilon && (1.0 - frac) > TimeEpsilon)
                return DateNumberKind.Ambiguous;
            DateTime date;
            try
            {
                date = new DateTime(1899, 12, 30).AddDays(whole);
            }
            catch (ArgumentOutOfRangeException)
            {
                return DateNumberKind.Ambiguous;
            }
            return FinishCalendar(date.Year, date.Month, date.Day, false, out number);
        }

        private static DateNumberKind FinishCalendar(int year, int month, int day, bool hasTime, out string number)
        {
            number = null;
            if (hasTime || year < MinYear || year > MaxYear || !IsRealDay(year, month, day))
                return DateNumberKind.Ambiguous;
            number = FormatDayMonth(day, month);
            return DateNumberKind.Recovered;
        }

        private static bool IsRealDay(int year, int month, int day)
        {
            if (month < 1 || month > 12 || day < 1 || day > 31)
                return false;
            try
            {
                DateTime probe = new DateTime(year, month, day);
                return probe.Year == year && probe.Month == month && probe.Day == day;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static bool TryParseDayMonthYear(string raw, out int year, out int month, out int day, out bool hasTime)
        {
            year = 0;
            month = 0;
            day = 0;
            hasTime = false;
            Match match = DayMonthYear.Match(raw);
            if (!match.Success)
                return false;
            day = Int32.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            month = Int32.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            year = Int32.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            hasTime = HasClock(match, 4);
            return true;
        }

        private static bool TryParseIso(string raw, out int year, out int month, out int day, out bool hasTime)
        {
            year = 0;
            month = 0;
            day = 0;
            hasTime = false;
            Match match = IsoDay.Match(raw);
            if (!match.Success)
                return false;
            year = Int32.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            month = Int32.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            day = Int32.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            hasTime = HasClock(match, 4);
            return true;
        }

        private static bool HasClock(Match match, int hourGroup)
        {
            if (!match.Groups[hourGroup].Success)
                return false;
            int hour = Int32.Parse(match.Groups[hourGroup].Value, CultureInfo.InvariantCulture);
            int minute = Int32.Parse(match.Groups[hourGroup + 1].Value, CultureInfo.InvariantCulture);
            int second = 0;
            if (match.Groups[hourGroup + 2].Success)
                second = Int32.Parse(match.Groups[hourGroup + 2].Value, CultureInfo.InvariantCulture);
            return hour != 0 || minute != 0 || second != 0;
        }

        private static bool TrySerial(string raw, out double serial)
        {
            string text = raw.Replace(',', '.');
            return Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out serial);
        }
    }
}
