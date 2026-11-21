using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using Alfa156.Localization;
namespace Alfa156.Desktop.Data
{
    internal sealed class ToIntervals
    {
        public decimal OilInterval = 10000;
        public decimal OilWarn = 1000;
        public decimal BeltInterval = 60000;
        public decimal BeltWarn = 5000;
    }

    internal static class ToSettings
    {
        internal const string OilIntervalKey = "oil_interval";
        internal const string OilWarnKey = "oil_warn";
        internal const string BeltIntervalKey = "belt_interval";
        internal const string BeltWarnKey = "belt_warn";
        internal const string CurrencyKey = "currency";
        internal const string DefaultCurrency = "BYN";
        internal const string OnboardingSeenKey = "onboarding_seen";
        internal const string BudgetMonthKey = "budget_month";
        internal const string BudgetWarnPctKey = "budget_warn_pct";
        internal const decimal DefaultBudgetWarnPct = 80;
        internal const string BackupRemindDaysKey = "backup_remind_days";
        internal const string LastBackupUtcKey = "last_backup_utc";
        internal const string LastBackupNagDateKey = "last_backup_nag_date";
        internal const int DefaultBackupRemindDays = 7;
        internal const string DefaultCarKey = "default_car";
        internal const string ReminderWarnDaysKey = "reminder_warn_days";
        internal const int DefaultReminderWarnDays = 14;
        internal const string SnapshotDeviceTokenKey = "snapshot_device_token";
        internal const string LanguageKey = "language";
        internal const string DefaultLanguage = "ru";
        private const string BackupUtcFormat = "yyyy-MM-dd HH:mm:ss";
        private const string NagDateFormat = "yyyy-MM-dd";

        internal static bool IsValidRow(string[] row)
        {
            if (row == null || row.Length != 2)
                return false;
            return (row[0] ?? "").Trim().Length > 0;
        }

        internal static ToIntervals Load(string directory)
        {
            return FromRows(LoadRows(directory));
        }

        internal static ToIntervals FromRows(List<string[]> rows)
        {
            ToIntervals values = new ToIntervals();
            if (rows == null)
                return values;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!IsValidRow(rows[i]))
                    continue;
                string key = rows[i][0].Trim();
                decimal number;
                if (!AlfaCodec.TryNumber(rows[i][1], out number) || number <= 0)
                    continue;
                if (key == OilIntervalKey) values.OilInterval = number;
                else if (key == OilWarnKey) values.OilWarn = number;
                else if (key == BeltIntervalKey) values.BeltInterval = number;
                else if (key == BeltWarnKey) values.BeltWarn = number;
            }
            return values;
        }

        internal static string GetCurrency(List<string[]> rows)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, rows);
            string code;
            if (map.TryGetValue(CurrencyKey, out code))
            {
                string normalized = NormalizeCurrency(code);
                if (normalized != null)
                    return normalized;
            }
            return DefaultCurrency;
        }

        internal static string NormalizeCurrency(string code)
        {
            if (code == null)
                return null;
            string trimmed = code.Trim().ToUpperInvariant();
            if (trimmed.Length < 1 || trimmed.Length > 8)
                return null;
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
                    return null;
            }
            return trimmed;
        }

        /// <summary>Default rows with intervals + currency (for new repositories).</summary>
        internal static List<string[]> ToRows(ToIntervals values)
        {
            return ApplyIntervals(null, values);
        }

        /// <summary>Update interval keys while preserving currency and other keys.</summary>
        internal static List<string[]> ApplyIntervals(List<string[]> existing, ToIntervals values)
        {
            if (values == null)
                values = new ToIntervals();
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, existing);
            map[OilIntervalKey] = Format(values.OilInterval);
            map[OilWarnKey] = Format(values.OilWarn);
            map[BeltIntervalKey] = Format(values.BeltInterval);
            map[BeltWarnKey] = Format(values.BeltWarn);
            ApplyDefaults(map);
            return MapToRows(map);
        }

        internal static List<string[]> SetCurrency(List<string[]> existing, string code)
        {
            string normalized = NormalizeCurrency(code);
            if (normalized == null)
                throw new InvalidOperationException(
                    L10n.Get("error.currency_format"));
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, existing);
            ApplyDefaults(map);
            map[CurrencyKey] = normalized;
            return MapToRows(map);
        }

        internal static string GetLanguage(List<string[]> rows)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, rows);
            string lang;
            if (map.TryGetValue(LanguageKey, out lang) && lang != null)
            {
                string trimmed = lang.Trim().ToLowerInvariant();
                if (trimmed.Length > 0)
                    return trimmed;
            }
            return DefaultLanguage;
        }

        internal static List<string[]> SetLanguage(List<string[]> existing, string language)
        {
            if (String.IsNullOrEmpty(language))
                language = DefaultLanguage;
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, existing);
            ApplyDefaults(map);
            map[LanguageKey] = language.Trim().ToLowerInvariant();
            return MapToRows(map);
        }

        internal static bool IsOnboardingSeen(List<string[]> rows)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, rows);
            string value;
            return map.TryGetValue(OnboardingSeenKey, out value) && value == "1";
        }

        internal static List<string[]> SetOnboardingSeen(List<string[]> existing, bool seen)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, existing);
            ApplyDefaults(map);
            map[OnboardingSeenKey] = seen ? "1" : "0";
            return MapToRows(map);
        }

        /// <summary>0 = disabled. Soft monthly budget for fuel+repair+expenses.</summary>
        internal static decimal GetBudgetMonth(List<string[]> rows)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, rows);
            string raw;
            if (!map.TryGetValue(BudgetMonthKey, out raw))
                return 0;
            decimal value;
            if (!AlfaCodec.TryNumber(raw, out value) || value < 0)
                return 0;
            return value;
        }

        internal static decimal GetBudgetWarnPct(List<string[]> rows)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, rows);
            string raw;
            if (!map.TryGetValue(BudgetWarnPctKey, out raw))
                return DefaultBudgetWarnPct;
            decimal value;
            if (!AlfaCodec.TryNumber(raw, out value) || value <= 0 || value > 100)
                return DefaultBudgetWarnPct;
            return value;
        }

        internal static List<string[]> SetBudget(List<string[]> existing, decimal monthLimit, decimal warnPct)
        {
            if (monthLimit < 0)
                throw new InvalidOperationException(L10n.Get("error.budget_invalid"));
            if (warnPct <= 0 || warnPct > 100)
                throw new InvalidOperationException(L10n.Get("error.warn_pct_range"));
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, existing);
            ApplyDefaults(map);
            map[BudgetMonthKey] = Format(monthLimit);
            map[BudgetWarnPctKey] = Format(warnPct);
            return MapToRows(map);
        }

        /// <summary>Days before due date to warn about date reminders. Default 14.</summary>
        internal static int GetReminderWarnDays(List<string[]> rows)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, rows);
            string raw;
            if (!map.TryGetValue(ReminderWarnDaysKey, out raw))
                return DefaultReminderWarnDays;
            decimal value;
            if (!AlfaCodec.TryNumber(raw, out value) || value < 0 || value > 3650 ||
                value != Decimal.Truncate(value))
                return DefaultReminderWarnDays;
            return (int)value;
        }

        internal static List<string[]> SetReminderWarnDays(List<string[]> existing, int days)
        {
            if (days < 0 || days > 3650)
                throw new InvalidOperationException(L10n.Get("error.warn_days_range"));
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, existing);
            ApplyDefaults(map);
            map[ReminderWarnDaysKey] = days.ToString(CultureInfo.InvariantCulture);
            return MapToRows(map);
        }

        /// <summary>Favorite/default car name; empty if unset.</summary>
        internal static string GetDefaultCar(List<string[]> rows)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, rows);
            string name;
            if (!map.TryGetValue(DefaultCarKey, out name) || name == null)
                return "";
            return name.Trim();
        }

        internal static List<string[]> SetDefaultCar(List<string[]> existing, string carName)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, existing);
            ApplyDefaults(map);
            map[DefaultCarKey] = (carName ?? "").Trim();
            return MapToRows(map);
        }

        /// <summary>Prefer default_car when present in cars; else first.</summary>
        internal static string ResolvePreferredCar(List<string[]> cars, List<string[]> settings)
        {
            if (cars == null || cars.Count == 0)
                return "";
            string preferred = GetDefaultCar(settings);
            if (preferred.Length > 0)
            {
                for (int i = 0; i < cars.Count; i++)
                {
                    string[] content = RecordIds.StripId(cars[i]);
                    string name = content.Length > 0 ? (content[0] ?? "").Trim() : "";
                    if (String.Equals(name, preferred, StringComparison.OrdinalIgnoreCase))
                        return name;
                }
            }
            string[] first = RecordIds.StripId(cars[0]);
            return first.Length > 0 ? (first[0] ?? "").Trim() : "";
        }

        /// <summary>0 = off. Soft reminder if days since last backup &gt;= this value.</summary>
        internal static int GetBackupRemindDays(List<string[]> rows)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, rows);
            string raw;
            if (!map.TryGetValue(BackupRemindDaysKey, out raw))
                return DefaultBackupRemindDays;
            decimal value;
            if (!AlfaCodec.TryNumber(raw, out value) || value < 0 || value > 3650 ||
                value != Decimal.Truncate(value))
                return DefaultBackupRemindDays;
            return (int)value;
        }

        internal static List<string[]> SetBackupRemindDays(List<string[]> existing, int days)
        {
            if (days < 0 || days > 3650)
                throw new InvalidOperationException(L10n.Get("error.reminder_interval_range"));
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, existing);
            ApplyDefaults(map);
            map[BackupRemindDaysKey] = days.ToString(CultureInfo.InvariantCulture);
            return MapToRows(map);
        }

        internal static List<string[]> MarkBackupDone(List<string[]> existing)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, existing);
            ApplyDefaults(map);
            map[LastBackupUtcKey] = DateTime.UtcNow.ToString(BackupUtcFormat, CultureInfo.InvariantCulture);
            return MapToRows(map);
        }

        internal static bool ShouldShowBackupReminder(List<string[]> rows)
        {
            int remindDays = GetBackupRemindDays(rows);
            if (remindDays <= 0)
                return false;
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, rows);
            string nag;
            string today = DateTime.Now.ToString(NagDateFormat, CultureInfo.InvariantCulture);
            if (map.TryGetValue(LastBackupNagDateKey, out nag) && nag == today)
                return false;
            string lastRaw;
            int daysSince;
            if (!map.TryGetValue(LastBackupUtcKey, out lastRaw) || String.IsNullOrEmpty(lastRaw))
                daysSince = remindDays;
            else
            {
                DateTime lastUtc;
                if (!DateTime.TryParseExact(lastRaw.Trim(), BackupUtcFormat, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out lastUtc))
                    daysSince = remindDays;
                else
                {
                    double total = (DateTime.UtcNow - lastUtc).TotalDays;
                    daysSince = total < 0 ? 0 : (int)total;
                }
            }
            return daysSince >= remindDays;
        }

        internal static int DaysSinceLastBackup(List<string[]> rows)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, rows);
            string lastRaw;
            if (!map.TryGetValue(LastBackupUtcKey, out lastRaw) || String.IsNullOrEmpty(lastRaw))
                return -1;
            DateTime lastUtc;
            if (!DateTime.TryParseExact(lastRaw.Trim(), BackupUtcFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out lastUtc))
                return -1;
            double total = (DateTime.UtcNow - lastUtc).TotalDays;
            return total < 0 ? 0 : (int)total;
        }

        internal static List<string[]> MarkBackupNagShown(List<string[]> existing)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, existing);
            ApplyDefaults(map);
            map[LastBackupNagDateKey] = DateTime.Now.ToString(NagDateFormat, CultureInfo.InvariantCulture);
            return MapToRows(map);
        }

        /// <summary>Ensure defaults without dropping unknown keys.</summary>
        internal static List<string[]> Normalize(List<string[]> rows)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, rows);
            ApplyDefaults(map);
            return MapToRows(map);
        }

        internal static void Save(string directory, ToIntervals values)
        {
            Directory.CreateDirectory(directory);
            List<string[]> existing = LoadRows(directory);
            AlfaCodec.SaveRows(Path.Combine(directory, AlfaCodec.SettingsFile),
                ApplyIntervals(existing, values));
        }

        internal static string GetOrCreateSnapshotDeviceToken(string directory)
        {
            List<string[]> rows = LoadRows(directory);
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, rows);
            string token;
            if (map.TryGetValue(SnapshotDeviceTokenKey, out token) &&
                token != null && token.Trim().Length >= 16)
                return token.Trim();
            token = Guid.NewGuid().ToString("N");
            map[SnapshotDeviceTokenKey] = token;
            AlfaCodec.SaveRows(Path.Combine(directory, AlfaCodec.SettingsFile),
                Normalize(MapToRows(map)));
            return token;
        }

        internal static void EnsureFile(string directory)
        {
            string path = Path.Combine(directory, AlfaCodec.SettingsFile);
            if (!File.Exists(path))
                Save(directory, new ToIntervals());
            else
                AlfaCodec.SaveRows(path, Normalize(AlfaCodec.ReadRows(path)));
        }

        internal static List<string[]> MergeRows(List<string[]> local, List<string[]> remote, bool preferIncoming)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, local);
            Dictionary<string, string> remoteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(remoteMap, remote);
            foreach (KeyValuePair<string, string> pair in remoteMap)
            {
                if (!map.ContainsKey(pair.Key) || preferIncoming)
                    map[pair.Key] = pair.Value;
            }
            ApplyDefaults(map);
            return MapToRows(map);
        }

        private static List<string[]> LoadRows(string directory)
        {
            if (String.IsNullOrEmpty(directory))
                return new List<string[]>();
            string path = Path.Combine(directory, AlfaCodec.SettingsFile);
            if (!File.Exists(path))
                return new List<string[]>();
            return AlfaCodec.ReadRows(path);
        }

        private static void ApplyRows(Dictionary<string, string> map, List<string[]> rows)
        {
            if (rows == null)
                return;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!IsValidRow(rows[i]))
                    continue;
                map[rows[i][0].Trim()] = (rows[i][1] ?? "").Trim();
            }
        }

        private static void ApplyDefaults(Dictionary<string, string> map)
        {
            if (!map.ContainsKey(OilIntervalKey)) map[OilIntervalKey] = "10000";
            if (!map.ContainsKey(OilWarnKey)) map[OilWarnKey] = "1000";
            if (!map.ContainsKey(BeltIntervalKey)) map[BeltIntervalKey] = "60000";
            if (!map.ContainsKey(BeltWarnKey)) map[BeltWarnKey] = "5000";
            if (!map.ContainsKey(CurrencyKey))
                map[CurrencyKey] = DefaultCurrency;
            else
            {
                string normalized = NormalizeCurrency(map[CurrencyKey]);
                map[CurrencyKey] = normalized != null ? normalized : DefaultCurrency;
            }
            if (!map.ContainsKey(BackupRemindDaysKey))
                map[BackupRemindDaysKey] = DefaultBackupRemindDays.ToString(CultureInfo.InvariantCulture);
            if (!map.ContainsKey(ReminderWarnDaysKey))
                map[ReminderWarnDaysKey] = DefaultReminderWarnDays.ToString(CultureInfo.InvariantCulture);
        }

        private static List<string[]> MapToRows(Dictionary<string, string> map)
        {
            List<string[]> rows = new List<string[]>();
            foreach (KeyValuePair<string, string> pair in map)
                rows.Add(new string[] { pair.Key, pair.Value });
            return rows;
        }

        private static string Format(decimal value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
