using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CarExpenses
{
    /// <summary>
    /// Named quick-add templates in templates.dat:
    /// type|templateName|payloadFields…|id
    /// Payload = entity columns without date and without trailing id.
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
        private const string DataDirectory = @"\My Documents\Alfa156";

        internal static void EnsureFile()
        {
            EnsureFileIn(DataDirectory);
        }

        internal static void EnsureFileIn(string directory)
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, FileName);
            if (!File.Exists(path))
                SaveRowsIn(directory, new List<string[]>());
        }

        internal static List<string[]> LoadRows()
        {
            return LoadRowsFrom(DataDirectory);
        }

        internal static List<string[]> LoadRowsFrom(string directory)
        {
            string path = Path.Combine(directory, FileName);
            if (!File.Exists(path))
                return new List<string[]>();
            return DataStore.ReadSyncTable(directory, FileName);
        }

        internal static void SaveRows(List<string[]> rows)
        {
            SaveRowsIn(DataDirectory, rows);
        }

        internal static void SaveRowsIn(string directory, List<string[]> rows)
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            DataStore.WriteSyncTable(directory, FileName, rows ?? new List<string[]>());
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
            if (content[1] == null || content[1].Trim().Length == 0)
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
            if (type == null)
                return null;
            string t = type.Trim().ToLower(System.Globalization.CultureInfo.InvariantCulture);
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
            return type == null ? "" : type;
        }

        internal static RecordTemplate FromRow(string[] row)
        {
            RecordTemplate item = new RecordTemplate();
            if (row == null)
                return item;
            string[] content = RecordIds.StripId(row);
            item.Type = NormalizeType(content.Length > 0 ? content[0] : "") ?? "";
            item.Name = content.Length > 1 && content[1] != null ? content[1].Trim() : "";
            int expected = ExpectedPayloadLength(item.Type);
            item.Payload = new string[expected];
            for (int i = 0; i < expected; i++)
                item.Payload[i] = content.Length > 2 + i && content[2 + i] != null
                    ? content[2 + i] : "";
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
            string name = item.Name == null ? "" : item.Name.Trim();
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
                content[2 + i] = i < payload.Length && payload[i] != null ? payload[i] : "";
            if (item.Id != null && item.Id.Length > 0 && RecordIds.LooksLikeId(item.Id))
                return RecordIds.WithId(content, item.Id);
            return RecordIds.EnsureId(content, RecordIds.PrefixForFile(FileName));
        }

        internal static List<RecordTemplate> LoadAll()
        {
            List<string[]> rows = LoadRows();
            List<RecordTemplate> list = new List<RecordTemplate>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (!IsValidRow(rows[i]))
                    continue;
                list.Add(FromRow(rows[i]));
            }
            return list;
        }

        internal static List<RecordTemplate> LoadByType(string type)
        {
            type = NormalizeType(type);
            List<RecordTemplate> all = LoadAll();
            List<RecordTemplate> filtered = new List<RecordTemplate>();
            for (int i = 0; i < all.Count; i++)
                if (all[i].Type == type)
                    filtered.Add(all[i]);
            return filtered;
        }

        internal static void Upsert(RecordTemplate item)
        {
            string[] row = ToRow(item);
            List<string[]> rows = LoadRows();
            string id = RecordIds.GetId(row);
            bool replaced = false;
            for (int i = 0; i < rows.Count; i++)
            {
                string existingId = RecordIds.GetId(rows[i]);
                if (id != null && existingId != null &&
                    String.Compare(existingId, id, true, System.Globalization.CultureInfo.InvariantCulture) == 0)
                {
                    rows[i] = row;
                    replaced = true;
                    break;
                }
            }
            if (!replaced)
                rows.Add(row);
            SaveRows(rows);
        }

        internal static void Delete(string id)
        {
            if (id == null || id.Length == 0)
                return;
            List<string[]> rows = LoadRows();
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                string existing = RecordIds.GetId(rows[i]);
                if (existing != null &&
                    String.Compare(existing, id, true, System.Globalization.CultureInfo.InvariantCulture) == 0)
                {
                    rows.RemoveAt(i);
                    RecordIds.AddTombstone(DataDirectory, FileName, id);
                    break;
                }
            }
            SaveRows(rows);
        }

        internal static string[] PayloadFromEntity(string type, string[] entityRow)
        {
            type = NormalizeType(type);
            if (type == null || entityRow == null)
                return new string[0];
            string[] content = RecordIds.StripId(entityRow);
            int expected = ExpectedPayloadLength(type);
            string[] payload = new string[expected];
            for (int i = 0; i < expected; i++)
                payload[i] = content.Length > 1 + i && content[1 + i] != null ? content[1 + i] : "";
            return payload;
        }

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
            values[0] = date == null ? "" : date.Trim();
            for (int i = 0; i < expected; i++)
                values[1 + i] = i < payload.Length && payload[i] != null ? payload[i] : "";
            return values;
        }

        internal static string DisplayLine(RecordTemplate item)
        {
            if (item == null)
                return "";
            string hint = "";
            if (item.Payload != null && item.Payload.Length > 0 && item.Payload[0] != null)
                hint = item.Payload[0].Trim();
            StringBuilder sb = new StringBuilder();
            sb.Append(TypeLabel(item.Type));
            sb.Append(": ");
            sb.Append(item.Name);
            if (hint.Length > 0)
            {
                sb.Append(" · ");
                sb.Append(hint);
            }
            return sb.ToString();
        }

        internal static void RenameCar(string oldName, string newName)
        {
            oldName = oldName == null ? "" : oldName.Trim();
            newName = newName == null ? "" : newName.Trim();
            if (oldName.Length == 0 || newName.Length == 0)
                return;
            List<string[]> rows = LoadRows();
            bool changed = false;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!IsValidRow(rows[i]))
                    continue;
                RecordTemplate item = FromRow(rows[i]);
                if (item.Payload == null || item.Payload.Length == 0)
                    continue;
                if (String.Compare(item.Payload[0], oldName, true,
                        System.Globalization.CultureInfo.InvariantCulture) == 0)
                {
                    item.Payload[0] = newName;
                    rows[i] = ToRow(item);
                    changed = true;
                }
            }
            if (changed)
                SaveRows(rows);
        }
    }
}
