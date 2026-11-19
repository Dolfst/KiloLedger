using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

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
                    "Код валюты: 1–8 символов (латиница/цифры), например BYN, RUB, USD, EUR.");
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyRows(map, existing);
            ApplyDefaults(map);
            map[CurrencyKey] = normalized;
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
