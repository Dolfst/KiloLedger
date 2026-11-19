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
    internal const string CurrencyKey = "currency";
    internal const string DefaultCurrency = "BYN";
    internal const string OnboardingSeenKey = "onboarding_seen";
    internal const string BudgetMonthKey = "budget_month";
    internal const string BudgetWarnPctKey = "budget_warn_pct";
    internal const decimal DefaultBudgetWarnPct = 80;

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

    internal static string GetCurrency(string directory)
    {
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

    internal static void SetCurrency(string directory, string code)
    {
        string normalized = NormalizeCurrency(code);
        if (normalized == null)
            throw new InvalidOperationException(
                "Код валюты: 1–8 символов (латиница/цифры), например BYN, RUB, USD, EUR.");
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        LoadInto(map, directory);
        ApplyDefaults(map);
        map[CurrencyKey] = normalized;
        WriteMap(directory, map);
    }

    internal static bool IsOnboardingSeen(string directory)
    {
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        LoadInto(map, directory);
        string value;
        return map.TryGetValue(OnboardingSeenKey, out value) && value == "1";
    }

    internal static void SetOnboardingSeen(string directory, bool seen)
    {
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        LoadInto(map, directory);
        ApplyDefaults(map);
        map[OnboardingSeenKey] = seen ? "1" : "0";
        WriteMap(directory, map);
    }

    /// <summary>0 = disabled. Soft monthly budget for fuel+repair+expenses.</summary>
    internal static decimal GetBudgetMonth(string directory)
    {
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        LoadInto(map, directory);
        string raw;
        if (!map.TryGetValue(BudgetMonthKey, out raw))
            return 0;
        decimal value;
        if (!TryDecimal(raw, out value) || value < 0)
            return 0;
        return value;
    }

    internal static decimal GetBudgetWarnPct(string directory)
    {
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        LoadInto(map, directory);
        string raw;
        if (!map.TryGetValue(BudgetWarnPctKey, out raw))
            return DefaultBudgetWarnPct;
        decimal value;
        if (!TryDecimal(raw, out value) || value <= 0 || value > 100)
            return DefaultBudgetWarnPct;
        return value;
    }

    internal static void SetBudget(string directory, decimal monthLimit, decimal warnPct)
    {
        if (monthLimit < 0)
            throw new InvalidOperationException("Бюджет месяца: 0 (выкл.) или положительное число.");
        if (warnPct <= 0 || warnPct > 100)
            throw new InvalidOperationException("Порог предупреждения: от 1 до 100 %.");
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        LoadInto(map, directory);
        ApplyDefaults(map);
        map[BudgetMonthKey] = Format(monthLimit);
        map[BudgetWarnPctKey] = Format(warnPct);
        WriteMap(directory, map);
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

    internal static void Save(string directory, ToIntervals values)
    {
        if (values == null)
            values = new ToIntervals();
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        LoadInto(map, directory);
        map[OilIntervalKey] = Format(values.OilInterval);
        map[OilWarnKey] = Format(values.OilWarn);
        map[BeltIntervalKey] = Format(values.BeltInterval);
        map[BeltWarnKey] = Format(values.BeltWarn);
        ApplyDefaults(map);
        WriteMap(directory, map);
    }

    internal static void EnsureFile(string directory)
    {
        string path = Path.Combine(directory, ExpenseDataCodec.SettingsFile);
        if (!File.Exists(path))
            Save(directory, new ToIntervals());
        else
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            LoadInto(map, directory);
            ApplyDefaults(map);
            WriteMap(directory, map);
        }
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
        WriteMap(destinationDirectory, map);
    }

    private static void WriteMap(string directory, Dictionary<string, string> map)
    {
        List<string[]> rows = new List<string[]>();
        foreach (KeyValuePair<string, string> pair in map)
            rows.Add(new string[] { pair.Key, pair.Value });
        Directory.CreateDirectory(directory);
        ExpenseDataCodec.SaveRows(Path.Combine(directory, ExpenseDataCodec.SettingsFile), rows);
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
            map[key] = (rows[i][1] ?? "").Trim();
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
