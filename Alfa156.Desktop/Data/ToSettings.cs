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

        internal static bool IsValidRow(string[] row)
        {
            if (row == null || row.Length != 2)
                return false;
            return (row[0] ?? "").Trim().Length > 0;
        }

        internal static ToIntervals Load(string directory)
        {
            ToIntervals values = new ToIntervals();
            if (String.IsNullOrEmpty(directory))
                return values;
            string path = Path.Combine(directory, AlfaCodec.SettingsFile);
            if (!File.Exists(path))
                return values;
            List<string[]> rows = AlfaCodec.ReadRows(path);
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

        internal static List<string[]> ToRows(ToIntervals values)
        {
            if (values == null)
                values = new ToIntervals();
            List<string[]> rows = new List<string[]>();
            rows.Add(new string[] { OilIntervalKey, Format(values.OilInterval) });
            rows.Add(new string[] { OilWarnKey, Format(values.OilWarn) });
            rows.Add(new string[] { BeltIntervalKey, Format(values.BeltInterval) });
            rows.Add(new string[] { BeltWarnKey, Format(values.BeltWarn) });
            return rows;
        }

        internal static void Save(string directory, ToIntervals values)
        {
            Directory.CreateDirectory(directory);
            AlfaCodec.SaveRows(Path.Combine(directory, AlfaCodec.SettingsFile), ToRows(values));
        }

        internal static void EnsureFile(string directory)
        {
            string path = Path.Combine(directory, AlfaCodec.SettingsFile);
            if (!File.Exists(path))
                Save(directory, new ToIntervals());
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
            if (!map.ContainsKey(OilIntervalKey)) map[OilIntervalKey] = "10000";
            if (!map.ContainsKey(OilWarnKey)) map[OilWarnKey] = "1000";
            if (!map.ContainsKey(BeltIntervalKey)) map[BeltIntervalKey] = "60000";
            if (!map.ContainsKey(BeltWarnKey)) map[BeltWarnKey] = "5000";
            List<string[]> rows = new List<string[]>();
            foreach (KeyValuePair<string, string> pair in map)
                rows.Add(new string[] { pair.Key, pair.Value });
            return rows;
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

        private static string Format(decimal value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
