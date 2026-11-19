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

        internal static void Save(ToIntervals values)
        {
            SaveToDirectory(DataDirectory, values);
        }

        internal static void SaveToDirectory(string directory, ToIntervals values)
        {
            if (values == null)
                values = new ToIntervals();
            Directory.CreateDirectory(directory);
            using (StreamWriter writer = new StreamWriter(Path.Combine(directory, SettingsFile), false))
            {
                writer.WriteLine(OilIntervalKey + "|" + Format(values.OilInterval));
                writer.WriteLine(OilWarnKey + "|" + Format(values.OilWarn));
                writer.WriteLine(BeltIntervalKey + "|" + Format(values.BeltInterval));
                writer.WriteLine(BeltWarnKey + "|" + Format(values.BeltWarn));
            }
        }

        internal static void EnsureFile()
        {
            string path = Path.Combine(DataDirectory, SettingsFile);
            if (!File.Exists(path))
                Save(new ToIntervals());
        }

        internal static void EnsureFileIn(string directory)
        {
            string path = Path.Combine(directory, SettingsFile);
            if (!File.Exists(path))
                SaveToDirectory(directory, new ToIntervals());
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
            if (!map.ContainsKey(OilIntervalKey)) map[OilIntervalKey] = "10000";
            if (!map.ContainsKey(OilWarnKey)) map[OilWarnKey] = "1000";
            if (!map.ContainsKey(BeltIntervalKey)) map[BeltIntervalKey] = "60000";
            if (!map.ContainsKey(BeltWarnKey)) map[BeltWarnKey] = "5000";

            Directory.CreateDirectory(destinationDirectory);
            using (StreamWriter writer = new StreamWriter(
                Path.Combine(destinationDirectory, SettingsFile), false))
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
            value = 0;
            if (text == null)
                return false;
            string raw = text.Trim().Replace(',', '.');
            return Decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        private static string Format(decimal value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
