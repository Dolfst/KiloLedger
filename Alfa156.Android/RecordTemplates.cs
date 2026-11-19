using System;
using System.Collections.Generic;
using System.IO;

namespace Alfa156.Android;

/// <summary>
/// Named quick-add templates in templates.dat:
/// type|templateName|payloadFields…|id
/// Payload = entity columns without date and without trailing id.
/// fuel: car|km|liters|cost|price|comment|fullTank|purpose
/// repair: car|km|category|product|name|qty|sum|comment
/// expense: car|category|name|sum|comment
/// </summary>
internal sealed class RecordTemplate
{
    public string Type = "";
    public string Name = "";
    public string[] Payload = new string[0];
    public string Id;
}

internal static class RecordTemplates
{
    internal const string FileName = "templates.dat";
    internal const string TypeFuel = "fuel";
    internal const string TypeRepair = "repair";
    internal const string TypeExpense = "expense";

    internal static void EnsureFile(string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
            ExpenseDataCodec.SaveRows(path, new List<string[]>());
    }

    internal static List<string[]> LoadRows(string directory)
    {
        string path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
            return new List<string[]>();
        return ExpenseDataCodec.ReadRows(path);
    }

    internal static void SaveRows(string directory, List<string[]> rows)
    {
        Directory.CreateDirectory(directory);
        ExpenseDataCodec.SaveRows(Path.Combine(directory, FileName), rows ?? new List<string[]>());
    }

    internal static bool IsValidRow(string[] row)
    {
        if (row == null)
            return false;
        string[] content = RecordIds.StripId(row);
        if (content.Length < 3)
            return false;
        string type = NormalizeType(content[0]);
        if (type == null)
            return false;
        if ((content[1] ?? "").Trim().Length == 0)
            return false;
        int payloadLen = content.Length - 2;
        int expected = ExpectedPayloadLength(type);
        if (payloadLen < 1 || payloadLen > expected)
            return false;
        if (RecordIds.HasId(row) && !RecordIds.LooksLikeId(RecordIds.GetId(row)))
            return false;
        return true;
    }

    internal static string NormalizeType(string type)
    {
        string t = (type ?? "").Trim().ToLowerInvariant();
        if (t == TypeFuel || t == TypeRepair || t == TypeExpense)
            return t;
        return null;
    }

    internal static int ExpectedPayloadLength(string type)
    {
        if (type == TypeFuel) return 8;
        if (type == TypeRepair) return 8;
        if (type == TypeExpense) return 5;
        return 0;
    }

    internal static string TypeLabel(string type)
    {
        if (type == TypeFuel) return "Заправка";
        if (type == TypeRepair) return "Ремонт";
        if (type == TypeExpense) return "Прочие";
        return type ?? "";
    }

    internal static RecordTemplate FromRow(string[] row)
    {
        RecordTemplate item = new RecordTemplate();
        if (row == null)
            return item;
        string[] content = RecordIds.StripId(row);
        item.Type = NormalizeType(content.Length > 0 ? content[0] : "") ?? "";
        item.Name = content.Length > 1 ? (content[1] ?? "").Trim() : "";
        int expected = ExpectedPayloadLength(item.Type);
        item.Payload = new string[expected];
        for (int i = 0; i < expected; i++)
            item.Payload[i] = content.Length > 2 + i ? (content[2 + i] ?? "") : "";
        item.Id = RecordIds.GetId(row);
        return item;
    }

    internal static string[] ToRow(RecordTemplate item)
    {
        if (item == null)
            item = new RecordTemplate();
        string type = NormalizeType(item.Type);
        if (type == null)
            throw new InvalidOperationException("Тип шаблона: fuel, repair или expense.");
        string name = (item.Name ?? "").Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("Введите название шаблона.");
        if (name.IndexOf('\r') >= 0 || name.IndexOf('\n') >= 0)
            throw new InvalidOperationException("Название шаблона без перевода строки.");
        int expected = ExpectedPayloadLength(type);
        string[] payload = item.Payload ?? new string[0];
        string[] content = new string[2 + expected];
        content[0] = type;
        content[1] = name;
        for (int i = 0; i < expected; i++)
            content[2 + i] = i < payload.Length ? (payload[i] ?? "") : "";
        if (!String.IsNullOrEmpty(item.Id) && RecordIds.LooksLikeId(item.Id))
            return RecordIds.WithId(content, item.Id);
        return RecordIds.EnsureId(content, RecordIds.PrefixForFile(FileName));
    }

    internal static List<RecordTemplate> LoadAll(string directory)
    {
        List<string[]> rows = LoadRows(directory);
        List<RecordTemplate> list = new List<RecordTemplate>();
        for (int i = 0; i < rows.Count; i++)
        {
            if (!IsValidRow(rows[i]))
                continue;
            list.Add(FromRow(rows[i]));
        }
        return list;
    }

    internal static List<RecordTemplate> LoadByType(string directory, string type)
    {
        type = NormalizeType(type);
        List<RecordTemplate> all = LoadAll(directory);
        List<RecordTemplate> filtered = new List<RecordTemplate>();
        for (int i = 0; i < all.Count; i++)
            if (all[i].Type == type)
                filtered.Add(all[i]);
        return filtered;
    }

    internal static void Upsert(string directory, RecordTemplate item)
    {
        string[] row = ToRow(item);
        List<string[]> rows = LoadRows(directory);
        string id = RecordIds.GetId(row);
        bool replaced = false;
        for (int i = 0; i < rows.Count; i++)
        {
            string existingId = RecordIds.GetId(rows[i]);
            if (id != null && existingId != null &&
                String.Equals(existingId, id, StringComparison.OrdinalIgnoreCase))
            {
                rows[i] = row;
                replaced = true;
                break;
            }
        }
        if (!replaced)
            rows.Add(row);
        SaveRows(directory, rows);
    }

    internal static void Delete(string directory, string id)
    {
        if (String.IsNullOrEmpty(id))
            return;
        List<string[]> rows = LoadRows(directory);
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            string existing = RecordIds.GetId(rows[i]);
            if (existing != null && String.Equals(existing, id, StringComparison.OrdinalIgnoreCase))
            {
                rows.RemoveAt(i);
                RecordIds.AddTombstone(directory, FileName, id);
                break;
            }
        }
        SaveRows(directory, rows);
    }

    /// <summary>Payload from a full entity row (with optional date+id).</summary>
    internal static string[] PayloadFromEntity(string type, string[] entityRow)
    {
        type = NormalizeType(type);
        if (type == null || entityRow == null)
            return new string[0];
        string[] content = RecordIds.StripId(entityRow);
        int expected = ExpectedPayloadLength(type);
        string[] payload = new string[expected];
        // Entity layout starts with date at [0]; payload starts at [1].
        for (int i = 0; i < expected; i++)
            payload[i] = content.Length > 1 + i ? (content[1 + i] ?? "") : "";
        return payload;
    }

    /// <summary>Build entity content fields (without id): date + payload.</summary>
    internal static string[] EntityFromTemplate(RecordTemplate template, string date)
    {
        if (template == null)
            throw new InvalidOperationException("Шаблон не выбран.");
        string type = NormalizeType(template.Type);
        if (type == null)
            throw new InvalidOperationException("Неверный тип шаблона.");
        int expected = ExpectedPayloadLength(type);
        string[] payload = template.Payload ?? new string[0];
        string[] values = new string[1 + expected];
        values[0] = (date ?? "").Trim();
        for (int i = 0; i < expected; i++)
            values[1 + i] = i < payload.Length ? (payload[i] ?? "") : "";
        return values;
    }

    internal static string DisplayLine(RecordTemplate item)
    {
        if (item == null)
            return "";
        string hint = "";
        if (item.Payload != null && item.Payload.Length > 0)
            hint = (item.Payload[0] ?? "").Trim();
        if (hint.Length > 0)
            return TypeLabel(item.Type) + ": " + item.Name + " · " + hint;
        return TypeLabel(item.Type) + ": " + item.Name;
    }

    internal static void RenameCarInTemplates(string directory, string oldName, string newName)
    {
        oldName = (oldName ?? "").Trim();
        newName = (newName ?? "").Trim();
        if (oldName.Length == 0 || newName.Length == 0)
            return;
        List<string[]> rows = LoadRows(directory);
        bool changed = false;
        for (int i = 0; i < rows.Count; i++)
        {
            if (!IsValidRow(rows[i]))
                continue;
            RecordTemplate item = FromRow(rows[i]);
            if (item.Payload == null || item.Payload.Length == 0)
                continue;
            if (String.Equals(item.Payload[0], oldName, StringComparison.OrdinalIgnoreCase))
            {
                item.Payload[0] = newName;
                rows[i] = ToRow(item);
                changed = true;
            }
        }
        if (changed)
            SaveRows(directory, rows);
    }
}
