using System;
using System.Collections.Generic;

namespace Alfa156.Desktop.Data
{
    /// <summary>
    /// Quick-add templates in templates.dat:
    /// type|templateName|payloadFields...|id
    /// type = fuel|repair|expense; payload = entity columns without date/id.
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

        internal static bool IsValidType(string type)
        {
            type = NormalizeType(type);
            return type == TypeFuel || type == TypeRepair || type == TypeExpense;
        }

        internal static string NormalizeType(string type)
        {
            return (type ?? "").Trim().ToLowerInvariant();
        }

        internal static bool IsValidRow(string[] row)
        {
            if (row == null)
                return false;
            string[] content = RecordIds.StripId(row);
            if (content.Length < 2)
                return false;
            if (!IsValidType(content[0]))
                return false;
            if ((content[1] ?? "").Trim().Length == 0)
                return false;
            int payloadLen = content.Length - 2;
            string type = NormalizeType(content[0]);
            if (type == TypeFuel && payloadLen > 9)
                return false;
            if (type == TypeRepair && payloadLen > 8)
                return false;
            if (type == TypeExpense && payloadLen > 5)
                return false;
            if (RecordIds.HasId(row) && !RecordIds.LooksLikeId(RecordIds.GetId(row)))
                return false;
            return true;
        }

        internal static RecordTemplate FromRow(string[] row)
        {
            RecordTemplate item = new RecordTemplate();
            if (row == null)
                return item;
            string[] content = RecordIds.StripId(row);
            if (content.Length < 2)
                return item;
            item.Type = NormalizeType(content[0]);
            item.Name = (content[1] ?? "").Trim();
            int payloadLen = content.Length - 2;
            if (payloadLen < 0)
                payloadLen = 0;
            item.Payload = new string[payloadLen];
            for (int i = 0; i < payloadLen; i++)
                item.Payload[i] = content[i + 2] ?? "";
            item.Id = RecordIds.GetId(row);
            return item;
        }

        internal static string[] ToRow(RecordTemplate item)
        {
            if (item == null)
                item = new RecordTemplate();
            string type = NormalizeType(item.Type);
            string name = (item.Name ?? "").Trim();
            string[] payload = item.Payload ?? new string[0];
            string[] content = new string[2 + payload.Length];
            content[0] = type;
            content[1] = name;
            for (int i = 0; i < payload.Length; i++)
                content[i + 2] = payload[i] ?? "";
            if (!String.IsNullOrEmpty(item.Id) && RecordIds.LooksLikeId(item.Id))
                return RecordIds.WithId(content, item.Id);
            return RecordIds.EnsureId(content, RecordIds.PrefixForFile(FileName));
        }

        internal static string[] PayloadFromEntity(string type, string[] entityRow)
        {
            type = NormalizeType(type);
            string[] content = RecordIds.StripId(entityRow);
            if (content == null || content.Length <= 1)
                return new string[0];
            int len = content.Length - 1;
            if (type == TypeFuel && len > 9)
                len = 9;
            if (type == TypeRepair && len > 8)
                len = 8;
            if (type == TypeExpense && len > 5)
                len = 5;
            string[] payload = new string[len];
            for (int i = 0; i < len; i++)
                payload[i] = content[i + 1] ?? "";
            return payload;
        }

        internal static string[] BuildEntityRow(RecordTemplate template, string date, string carOverride)
        {
            if (template == null)
                template = new RecordTemplate();
            string[] payload = template.Payload ?? new string[0];
            string[] entity = new string[1 + payload.Length];
            entity[0] = date ?? "";
            for (int i = 0; i < payload.Length; i++)
                entity[i + 1] = payload[i] ?? "";
            if (!String.IsNullOrEmpty(carOverride) && entity.Length > 1)
                entity[1] = carOverride.Trim();
            return entity;
        }

        internal static List<RecordTemplate> ListByType(List<string[]> rows, string type)
        {
            type = NormalizeType(type);
            List<RecordTemplate> result = new List<RecordTemplate>();
            if (rows == null)
                return result;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!IsValidRow(rows[i]))
                    continue;
                RecordTemplate item = FromRow(rows[i]);
                if (item.Type == type)
                    result.Add(item);
            }
            return result;
        }

        internal static List<string[]> Upsert(List<string[]> rows, RecordTemplate item)
        {
            if (item == null)
                throw new ArgumentNullException("item");
            item.Type = NormalizeType(item.Type);
            item.Name = (item.Name ?? "").Trim();
            if (!IsValidType(item.Type))
                throw new InvalidOperationException("Тип шаблона: fuel, repair или expense.");
            if (item.Name.Length == 0)
                throw new InvalidOperationException("Введите название шаблона.");
            if (item.Name.Length > 64)
                throw new InvalidOperationException("Название шаблона: не более 64 символов.");

            List<string[]> list = rows != null ? new List<string[]>(rows) : new List<string[]>();
            bool found = false;
            for (int i = 0; i < list.Count; i++)
            {
                RecordTemplate existing = FromRow(list[i]);
                if (existing.Type != item.Type)
                    continue;
                if (!String.Equals(existing.Name, item.Name, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (String.IsNullOrEmpty(item.Id))
                    item.Id = existing.Id;
                list[i] = ToRow(item);
                found = true;
                break;
            }
            if (!found)
                list.Add(ToRow(item));
            return list;
        }

        internal static List<string[]> Delete(List<string[]> rows, string type, string name,
            List<Tombstone> deleted)
        {
            type = NormalizeType(type);
            name = (name ?? "").Trim();
            List<string[]> kept = new List<string[]>();
            if (rows == null)
                return kept;
            for (int i = 0; i < rows.Count; i++)
            {
                RecordTemplate item = FromRow(rows[i]);
                if (item.Type == type &&
                    String.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (!String.IsNullOrEmpty(item.Id) && deleted != null)
                    {
                        string stamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ",
                            System.Globalization.CultureInfo.InvariantCulture);
                        deleted.Add(new Tombstone(RecordIds.TypeTemplate, item.Id, stamp));
                    }
                    continue;
                }
                kept.Add(rows[i]);
            }
            return kept;
        }

        internal static string FormatLabel(RecordTemplate item)
        {
            if (item == null)
                return "";
            string typeLabel = item.Type == TypeFuel ? "Топл."
                : item.Type == TypeRepair ? "Ремонт"
                : item.Type == TypeExpense ? "Прочее" : item.Type;
            string hint = "";
            if (item.Payload != null && item.Payload.Length > 0 &&
                !String.IsNullOrEmpty((item.Payload[0] ?? "").Trim()))
                hint = " · " + item.Payload[0].Trim();
            return typeLabel + ": " + item.Name + hint;
        }

        internal static string TypeLabelRu(string type)
        {
            type = NormalizeType(type);
            if (type == TypeFuel) return "Заправка";
            if (type == TypeRepair) return "Ремонт";
            if (type == TypeExpense) return "Прочий расход";
            return type;
        }
    }
}
