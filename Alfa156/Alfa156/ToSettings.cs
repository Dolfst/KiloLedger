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
