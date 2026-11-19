using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CarExpenses
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
        internal const string SettingsFile = "settings.dat";
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
        private const string BackupUtcFormat = "yyyy-MM-dd HH:mm:ss";
        private const string NagDateFormat = "yyyy-MM-dd";
        private const string DataDirectory = @"\My Documents\Alfa156";

        internal static bool IsValidRow(string[] row)
        {
            if (row == null || row.Length != 2)
                return false;
            return row[0] != null && row[0].Trim().Length > 0;
        }

        internal static ToIntervals Load()
        {
            return LoadFromDirectory(DataDirectory);
        }

        internal static ToIntervals LoadFromDirectory(string directory)
        {
            ToIntervals values = new ToIntervals();
            string path = Path.Combine(directory, SettingsFile);
            if (!File.Exists(path))
                return values;
            List<string[]> rows = ReadRows(path);
            for (int i = 0; i < rows.Count; i++)
            {
                if (!IsValidRow(rows[i]))
                    continue;
                string key = rows[i][0].Trim();
                decimal number;
                if (!TryDecimal(rows[i][1], out number) || number <= 0)
                    continue;
                if (key == OilIntervalKey) values.OilInterval = number;
                else if (key == OilWarnKey) values.OilWarn = number;
                else if (key == BeltIntervalKey) values.BeltInterval = number;
                else if (key == BeltWarnKey) values.BeltWarn = number;
            }
            return values;
        }

        internal static string GetCurrency()
        {
            return GetCurrencyFromDirectory(DataDirectory);
        }

        internal static string GetCurrencyFromDirectory(string directory)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            LoadInto(map, directory);
            string code;
            if (map.TryGetValue(CurrencyKey, out code))
            {
                string normalized = NormalizeCurrency(code);
                if (normalized != null)
                    return normalized;
            }
            return DefaultCurrency;
        }

        internal static void SetCurrency(string code)
        {
            SetCurrencyInDirectory(DataDirectory, code);
        }

        internal static void SetCurrencyInDirectory(string directory, string code)
        {
            string normalized = NormalizeCurrency(code);
            if (normalized == null)
                throw new InvalidOperationException(
                    "Код валюты: 1–8 символов (латиница/цифры), например BYN, RUB, USD, EUR.");
            Dictionary<string, string> map = new Dictionary<string, string>();
            LoadInto(map, directory);
            ApplyDefaults(map);
            map[CurrencyKey] = normalized;
            WriteMap(directory, map);
        }

        internal static bool IsOnboardingSeen()
        {
            return IsOnboardingSeenInDirectory(DataDirectory);
        }

        internal static bool IsOnboardingSeenInDirectory(string directory)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            LoadInto(map, directory);
            string value;
            return map.TryGetValue(OnboardingSeenKey, out value) && value == "1";
        }

        internal static void SetOnboardingSeen(bool seen)
        {
            SetOnboardingSeenInDirectory(DataDirectory, seen);
        }

        internal static void SetOnboardingSeenInDirectory(string directory, bool seen)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            LoadInto(map, directory);
            ApplyDefaults(map);
            map[OnboardingSeenKey] = seen ? "1" : "0";
            WriteMap(directory, map);
        }

        /// <summary>0 = disabled. Soft monthly budget for fuel+repair+expenses.</summary>
        internal static decimal GetBudgetMonth()
        {
            return GetBudgetMonthFromDirectory(DataDirectory);
        }

        internal static decimal GetBudgetMonthFromDirectory(string directory)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            LoadInto(map, directory);
            string raw;
            if (!map.TryGetValue(BudgetMonthKey, out raw))
                return 0;
            decimal value;
            if (!TryDecimal(raw, out value) || value < 0)
                return 0;
            return value;
        }

        internal static decimal GetBudgetWarnPct()
        {
            return GetBudgetWarnPctFromDirectory(DataDirectory);
        }

        internal static decimal GetBudgetWarnPctFromDirectory(string directory)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            LoadInto(map, directory);
            string raw;
            if (!map.TryGetValue(BudgetWarnPctKey, out raw))
                return DefaultBudgetWarnPct;
            decimal value;
            if (!TryDecimal(raw, out value) || value <= 0 || value > 100)
                return DefaultBudgetWarnPct;
            return value;
        }

        internal static void SetBudget(decimal monthLimit, decimal warnPct)
        {
            SetBudgetInDirectory(DataDirectory, monthLimit, warnPct);
        }

        internal static void SetBudgetInDirectory(string directory, decimal monthLimit, decimal warnPct)
        {
            if (monthLimit < 0)
                throw new InvalidOperationException("Бюджет месяца: 0 (выкл.) или положительное число.");
            if (warnPct <= 0 || warnPct > 100)
                throw new InvalidOperationException("Порог предупреждения: от 1 до 100 %.");
            Dictionary<string, string> map = new Dictionary<string, string>();
            LoadInto(map, directory);
            ApplyDefaults(map);
            map[BudgetMonthKey] = Format(monthLimit);
            map[BudgetWarnPctKey] = Format(warnPct);
            WriteMap(directory, map);
        }

        /// <summary>Favorite/default car name; empty if unset.</summary>
        internal static string GetDefaultCar()
        {
            return GetDefaultCarFromDirectory(DataDirectory);
        }

        internal static string GetDefaultCarFromDirectory(string directory)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            LoadInto(map, directory);
            string name;
            if (!map.TryGetValue(DefaultCarKey, out name) || name == null)
                return "";
            return name.Trim();
        }

        internal static void SetDefaultCar(string carName)
        {
            SetDefaultCarInDirectory(DataDirectory, carName);
        }

        internal static void SetDefaultCarInDirectory(string directory, string carName)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            LoadInto(map, directory);
            ApplyDefaults(map);
            map[DefaultCarKey] = carName == null ? "" : carName.Trim();
            WriteMap(directory, map);
        }

        /// <summary>Prefer default_car when present in cars; else current; else first.</summary>
        internal static string ResolvePreferredCar(List<string> cars, string current)
        {
            if (cars == null || cars.Count == 0)
                return current == null ? "" : current;
            string preferred = GetDefaultCar();
            if (preferred.Length > 0)
            {
                for (int i = 0; i < cars.Count; i++)
                    if (String.Compare(cars[i], preferred, true,
                            CultureInfo.InvariantCulture) == 0)
                        return cars[i];
            }
            if (current != null && current.Length > 0)
            {
                for (int i = 0; i < cars.Count; i++)
                    if (String.Compare(cars[i], current, true,
                            CultureInfo.InvariantCulture) == 0)
                        return cars[i];
            }
            return cars[0];
        }


        /// <summary>Days before calendar reminder date to warn (default 14).</summary>
        internal static int GetReminderWarnDays()
        {
            return GetReminderWarnDaysFromDirectory(DataDirectory);
        }

        internal static int GetReminderWarnDaysFromDirectory(string directory)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            LoadInto(map, directory);
            string raw;
            if (!map.TryGetValue(ReminderWarnDaysKey, out raw))
                return DefaultReminderWarnDays;
            decimal value;
            if (!TryDecimal(raw, out value) || value < 0 || value > 3650)
                return DefaultReminderWarnDays;
            return (int)value;
        }

        internal static void SetReminderWarnDays(int days)
        {
            SetReminderWarnDaysInDirectory(DataDirectory, days);
        }

        internal static void SetReminderWarnDaysInDirectory(string directory, int days)
        {
            if (days < 0 || days > 3650)
                throw new InvalidOperationException("Предупреждение напоминаний: 0–3650 дней.");
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            LoadInto(map, directory);
            ApplyDefaults(map);
            map[ReminderWarnDaysKey] = days.ToString(CultureInfo.InvariantCulture);
            WriteMap(directory, map);
        }

        /// <summary>0 = off. Soft reminder if days since last backup &gt;= this value.</summary>
        internal static int GetBackupRemindDays()
        {
            return GetBackupRemindDaysFromDirectory(DataDirectory);
        }

        internal static int GetBackupRemindDaysFromDirectory(string directory)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            LoadInto(map, directory);
            string raw;
            if (!map.TryGetValue(BackupRemindDaysKey, out raw))
                return DefaultBackupRemindDays;
            decimal value;
            if (!TryDecimal(raw, out value) || value < 0 || value > 3650)
                return DefaultBackupRemindDays;
            return (int)value;
        }

        internal static void SetBackupRemindDays(int days)
        {
            SetBackupRemindDaysInDirectory(DataDirectory, days);
        }

        internal static void SetBackupRemindDaysInDirectory(string directory, int days)
        {
            if (days < 0 || days > 3650)
                throw new InvalidOperationException("Интервал напоминания: 0 (выкл.) или 1–3650 дней.");
            Dictionary<string, string> map = new Dictionary<string, string>();
            LoadInto(map, directory);
            ApplyDefaults(map);
            map[BackupRemindDaysKey] = days.ToString(CultureInfo.InvariantCulture);
            WriteMap(directory, map);
        }

        internal static void MarkBackupDone()
        {
            MarkBackupDoneInDirectory(DataDirectory);
        }

        internal static void MarkBackupDoneInDirectory(string directory)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            LoadInto(map, directory);
            ApplyDefaults(map);
            map[LastBackupUtcKey] = DateTime.UtcNow.ToString(BackupUtcFormat, CultureInfo.InvariantCulture);
            WriteMap(directory, map);
        }

        internal static bool ShouldShowBackupReminder()
        {
            return ShouldShowBackupReminderInDirectory(DataDirectory);
        }

        internal static bool ShouldShowBackupReminderInDirectory(string directory)
        {
            int remindDays = GetBackupRemindDaysFromDirectory(directory);
            if (remindDays <= 0)
                return false;
            Dictionary<string, string> map = new Dictionary<string, string>();
            LoadInto(map, directory);
            string nag;
            string today = DateTime.Now.ToString(NagDateFormat, CultureInfo.InvariantCulture);
            if (map.TryGetValue(LastBackupNagDateKey, out nag) && nag == today)
                return false;
            string lastRaw;
            int daysSince;
            if (!map.TryGetValue(LastBackupUtcKey, out lastRaw) || lastRaw == null || lastRaw.Length == 0)
                daysSince = remindDays;
            else
            {
                DateTime lastUtc;
                if (!TryParseBackupUtc(lastRaw, out lastUtc))
                    daysSince = remindDays;
                else
                {
                    double total = (DateTime.UtcNow - lastUtc).TotalDays;
                    daysSince = total < 0 ? 0 : (int)total;
                }
            }
            return daysSince >= remindDays;
        }

        internal static int DaysSinceLastBackup()
        {
            return DaysSinceLastBackupInDirectory(DataDirectory);
        }

        internal static int DaysSinceLastBackupInDirectory(string directory)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            LoadInto(map, directory);
            string lastRaw;
            if (!map.TryGetValue(LastBackupUtcKey, out lastRaw) || lastRaw == null || lastRaw.Length == 0)
                return -1;
            DateTime lastUtc;
            if (!TryParseBackupUtc(lastRaw, out lastUtc))
                return -1;
            double total = (DateTime.UtcNow - lastUtc).TotalDays;
            return total < 0 ? 0 : (int)total;
        }

        internal static void MarkBackupNagShown()
        {
            MarkBackupNagShownInDirectory(DataDirectory);
        }

        internal static void MarkBackupNagShownInDirectory(string directory)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            LoadInto(map, directory);
            ApplyDefaults(map);
            map[LastBackupNagDateKey] = DateTime.Now.ToString(NagDateFormat, CultureInfo.InvariantCulture);
            WriteMap(directory, map);
        }

        /// <summary>CF-safe parse of backup UTC stamp (CF has no DateTime.TryParseExact).</summary>
        private static bool TryParseBackupUtc(string raw, out DateTime utc)
        {
            utc = DateTime.MinValue;
            if (raw == null)
                return false;
            string trimmed = raw.Trim();
            // yyyy-MM-dd HH:mm:ss
            if (trimmed.Length != 19 || trimmed[4] != '-' || trimmed[7] != '-' ||
                trimmed[10] != ' ' || trimmed[13] != ':' || trimmed[16] != ':')
                return false;
            try
            {
                int year = int.Parse(trimmed.Substring(0, 4), NumberStyles.None, CultureInfo.InvariantCulture);
                int month = int.Parse(trimmed.Substring(5, 2), NumberStyles.None, CultureInfo.InvariantCulture);
                int day = int.Parse(trimmed.Substring(8, 2), NumberStyles.None, CultureInfo.InvariantCulture);
                int hour = int.Parse(trimmed.Substring(11, 2), NumberStyles.None, CultureInfo.InvariantCulture);
                int minute = int.Parse(trimmed.Substring(14, 2), NumberStyles.None, CultureInfo.InvariantCulture);
                int second = int.Parse(trimmed.Substring(17, 2), NumberStyles.None, CultureInfo.InvariantCulture);
                utc = new DateTime(year, month, day, hour, minute, second);
                return true;
            }
            catch (FormatException) { return false; }
            catch (OverflowException) { return false; }
            catch (ArgumentOutOfRangeException) { return false; }
        }

        /// <summary>Null if invalid; otherwise trimmed uppercase code.</summary>
        internal static string NormalizeCurrency(string code)
        {
            if (code == null)
                return null;
            string trimmed = code.Trim().ToUpper(CultureInfo.InvariantCulture);
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

        internal static void Save(ToIntervals values)
        {
            SaveToDirectory(DataDirectory, values);
        }

        internal static void SaveToDirectory(string directory, ToIntervals values)
        {
            if (values == null)
                values = new ToIntervals();
            Dictionary<string, string> map = new Dictionary<string, string>();
            LoadInto(map, directory);
            map[OilIntervalKey] = Format(values.OilInterval);
            map[OilWarnKey] = Format(values.OilWarn);
            map[BeltIntervalKey] = Format(values.BeltInterval);
            map[BeltWarnKey] = Format(values.BeltWarn);
            ApplyDefaults(map);
            WriteMap(directory, map);
        }

        internal static void EnsureFile()
        {
            string path = Path.Combine(DataDirectory, SettingsFile);
            if (!File.Exists(path))
                Save(new ToIntervals());
            else
            {
                Dictionary<string, string> map = new Dictionary<string, string>();
                LoadInto(map, DataDirectory);
                ApplyDefaults(map);
                WriteMap(DataDirectory, map);
            }
        }

        internal static void EnsureFileIn(string directory)
        {
            string path = Path.Combine(directory, SettingsFile);
            if (!File.Exists(path))
                SaveToDirectory(directory, new ToIntervals());
            else
            {
                Dictionary<string, string> map = new Dictionary<string, string>();
                LoadInto(map, directory);
                ApplyDefaults(map);
                WriteMap(directory, map);
            }
        }

        internal static void MergeDirectories(string localDirectory, string remoteDirectory,
            string destinationDirectory, bool preferIncoming)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            LoadInto(map, localDirectory);
            Dictionary<string, string> remote = new Dictionary<string, string>();
            LoadInto(remote, remoteDirectory);
            foreach (KeyValuePair<string, string> pair in remote)
            {
                if (!map.ContainsKey(pair.Key) || preferIncoming)
                    map[pair.Key] = pair.Value;
            }
            ApplyDefaults(map);
            WriteMap(destinationDirectory, map);
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

        private static void WriteMap(string directory, Dictionary<string, string> map)
        {
            Directory.CreateDirectory(directory);
            using (StreamWriter writer = new StreamWriter(
                Path.Combine(directory, SettingsFile), false))
            {
                foreach (KeyValuePair<string, string> pair in map)
                    writer.WriteLine(pair.Key + "|" + pair.Value);
            }
        }

        private static void LoadInto(Dictionary<string, string> map, string directory)
        {
            string path = Path.Combine(directory, SettingsFile);
            if (!File.Exists(path))
                return;
            List<string[]> rows = ReadRows(path);
            for (int i = 0; i < rows.Count; i++)
            {
                if (!IsValidRow(rows[i]))
                    continue;
                map[rows[i][0].Trim()] = rows[i][1] == null ? "" : rows[i][1].Trim();
            }
        }

        private static List<string[]> ReadRows(string path)
        {
            List<string[]> rows = new List<string[]>();
            using (StreamReader reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                    if (line.Length > 0)
                        rows.Add(DecodeRow(line));
            }
            return rows;
        }

        private static string[] DecodeRow(string line)
        {
            List<string> fields = new List<string>();
            StringBuilder field = new StringBuilder();
            bool escaped = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (escaped)
                {
                    field.Append(c);
                    escaped = false;
                }
                else if (c == '\\')
                    escaped = true;
                else if (c == '|')
                {
                    fields.Add(field.ToString());
                    field.Length = 0;
                }
                else
                    field.Append(c);
            }
            if (escaped)
                field.Append('\\');
            fields.Add(field.ToString());
            return fields.ToArray();
        }

        private static bool TryDecimal(string text, out decimal value)
        {
            return DataStore.TryNumber(text, out value);
        }

        private static string Format(decimal value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
