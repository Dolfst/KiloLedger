using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Alfa156.Android;

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
        string path = Path.Combine(directory, ExpenseDataCodec.SettingsFile);
        if (!File.Exists(path))
            return values;
        List<string[]> rows = ExpenseDataCodec.ReadRows(path);
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

    internal static void Save(string directory, ToIntervals values)
    {
        if (values == null)
            values = new ToIntervals();
        Directory.CreateDirectory(directory);
        List<string[]> rows = new List<string[]>();
        rows.Add(new string[] { OilIntervalKey, Format(values.OilInterval) });
        rows.Add(new string[] { OilWarnKey, Format(values.OilWarn) });
        rows.Add(new string[] { BeltIntervalKey, Format(values.BeltInterval) });
        rows.Add(new string[] { BeltWarnKey, Format(values.BeltWarn) });
        ExpenseDataCodec.SaveRows(Path.Combine(directory, ExpenseDataCodec.SettingsFile), rows);
    }

    internal static void EnsureFile(string directory)
    {
        string path = Path.Combine(directory, ExpenseDataCodec.SettingsFile);
        if (!File.Exists(path))
            Save(directory, new ToIntervals());
    }

    internal static void MergeFiles(string localDirectory, string remoteDirectory, string destinationDirectory,
        bool preferIncoming)
    {
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        LoadInto(map, localDirectory);
        Dictionary<string, string> remote = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        LoadInto(remote, remoteDirectory);
        foreach (KeyValuePair<string, string> pair in remote)
        {
            if (!map.ContainsKey(pair.Key) || preferIncoming)
                map[pair.Key] = pair.Value;
        }
        ApplyDefaults(map);
        List<string[]> rows = new List<string[]>();
        foreach (KeyValuePair<string, string> pair in map)
            rows.Add(new string[] { pair.Key, pair.Value });
        Directory.CreateDirectory(destinationDirectory);
        ExpenseDataCodec.SaveRows(Path.Combine(destinationDirectory, ExpenseDataCodec.SettingsFile), rows);
    }

    private static void LoadInto(Dictionary<string, string> map, string directory)
    {
        string path = Path.Combine(directory, ExpenseDataCodec.SettingsFile);
        if (!File.Exists(path))
            return;
        List<string[]> rows = ExpenseDataCodec.ReadRows(path);
        for (int i = 0; i < rows.Count; i++)
        {
            if (!IsValidRow(rows[i]))
                continue;
            string key = rows[i][0].Trim();
            if (!map.ContainsKey(key))
                map.Add(key, (rows[i][1] ?? "").Trim());
            else
                map[key] = (rows[i][1] ?? "").Trim();
        }
    }

    private static void ApplyDefaults(Dictionary<string, string> map)
    {
        if (!map.ContainsKey(OilIntervalKey)) map[OilIntervalKey] = "10000";
        if (!map.ContainsKey(OilWarnKey)) map[OilWarnKey] = "1000";
        if (!map.ContainsKey(BeltIntervalKey)) map[BeltIntervalKey] = "60000";
        if (!map.ContainsKey(BeltWarnKey)) map[BeltWarnKey] = "5000";
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
