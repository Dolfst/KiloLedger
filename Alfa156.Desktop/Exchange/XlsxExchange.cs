using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Alfa156.Desktop.Data;

namespace Alfa156.Desktop.Exchange
{
    internal static class XlsxExchange
    {
        public static void Export(AlfaRepository repo, string xlsxPath)
        {
            if (File.Exists(xlsxPath))
                File.Delete(xlsxPath);

            using (FileStream stream = File.Create(xlsxPath))
            using (ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(zip, "[Content_Types].xml", ContentTypesXml());
                WriteEntry(zip, "_rels/.rels", RelsXml());
                WriteEntry(zip, "xl/workbook.xml", WorkbookXml());
                WriteEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRelsXml());
                WriteEntry(zip, "xl/styles.xml", StylesXml());
                WriteSheet(zip, "xl/worksheets/sheet1.xml", new string[] { "Автомобиль" }, repo.Cars);
                WriteSheet(zip, "xl/worksheets/sheet2.xml",
                    new string[] { "Дата", "Автомобиль", "Пробег_км", "Литры", "Сумма_BYN", "Цена_за_литр", "Комментарий", "Полный_бак" },
                    repo.Fuel);
                WriteSheet(zip, "xl/worksheets/sheet3.xml",
                    new string[]
                    {
                        "Дата", "Автомобиль", "Пробег_км", "Категория", "Товар",
                        "Описание", "Кол_во", "Сумма_BYN", "Комментарий"
                    }, repo.Repairs);
                WriteSheet(zip, "xl/worksheets/sheet4.xml", UtilityHeaders(), repo.Utilities);
                WriteSheet(zip, "xl/worksheets/sheet5.xml", new string[] { "Категория" }, repo.Categories);
                WriteSheet(zip, "xl/worksheets/sheet6.xml",
                    new string[] { "Дата", "Автомобиль", "Категория", "Название", "Сумма_BYN", "Комментарий" },
                    repo.Expenses);
                WriteSheet(zip, "xl/worksheets/sheet7.xml", new string[] { "Категория" }, repo.ExpenseCategories);
            }
        }

        public static void Import(AlfaRepository repo, string xlsxPath)
        {
            using (FileStream stream = File.OpenRead(xlsxPath))
            using (ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                Dictionary<string, string> sheetMap = ReadSheetMap(zip);
                List<string> names = new List<string>(sheetMap.Keys);
                if (DengiFormat.LooksLikeDengiSheetNames(names))
                {
                    ImportDengi(repo, zip, sheetMap, names);
                    return;
                }

                List<string[]> cars = ReadNamedSheet(zip, sheetMap, new string[] { "Автомобили", "cars", "Sheet1" }, 1);
                List<string[]> fuel = ReadNamedSheet(zip, sheetMap, new string[] { "Заправки", "fuel", "Sheet2" }, 8);
                List<string[]> repairs = ReadNamedSheet(zip, sheetMap, new string[] { "Ремонт", "repairs", "Sheet3" }, 9);
                List<string[]> utilities = ReadNamedSheet(zip, sheetMap, new string[] { "Коммуналка", "utilities", "Sheet4" }, 8);
                List<string[]> categories = ReadNamedSheetOptional(zip, sheetMap,
                    new string[] { "Категории", "repair-categories", "Sheet5" }, 1);
                List<string[]> expenses = ReadNamedSheetOptional(zip, sheetMap,
                    new string[] { "Прочие", "expenses", "Sheet6" }, 6);
                List<string[]> expenseCategories = ReadNamedSheetOptional(zip, sheetMap,
                    new string[] { "Кат. прочих", "expense-categories", "Sheet7" }, 1);
                if (cars.Count == 0)
                    throw new InvalidDataException("В Excel нет данных на листе «Автомобили».");
                if (DengiFormat.LooksLikeMonthList(cars))
                    throw new InvalidDataException("На листе «Автомобили» похоже месяцы. Для файла «Деньги» нужны листы Заправки/Ремонт/Коммунальные/Справочник.");
                repo.ReplaceAll(cars, fuel, repairs, utilities, categories, expenses, expenseCategories,
                    repo.Settings);
            }
        }

        private static void ImportDengi(AlfaRepository repo, ZipArchive zip, Dictionary<string, string> sheetMap, List<string> names)
        {
            List<string[]> cars = DengiFormat.BuildCars(names);
            string defaultCar = cars[0][0];

            string fuelName = FindSheet(names, "Заправки Alfa Romeo 156", "Заправки");
            string repairName = FindSheet(names, "Ремонт Alfa Romeo 156", "Ремонт");
            string utilitiesName = FindSheet(names, "Коммунальные", "Коммуналка");
            string categoriesName = FindSheet(names, "Справочник", "Категории");

            List<string[]> fuelRaw = fuelName == null ? new List<string[]>() :
                ReadNamedSheetOptional(zip, sheetMap, new string[] { fuelName }, 8);
            List<string[]> repairRaw = repairName == null ? new List<string[]>() :
                ReadNamedSheetOptional(zip, sheetMap, new string[] { repairName }, 10);
            List<string[]> utilitiesRaw = utilitiesName == null ? new List<string[]>() :
                ReadNamedSheetOptional(zip, sheetMap, new string[] { utilitiesName }, 10);
            List<string[]> categoriesRaw = categoriesName == null ? new List<string[]>() :
                ReadNamedSheetOptional(zip, sheetMap, new string[] { categoriesName }, 3);

            // ReadNamedSheetOptional skips header row (starts at Excel row 2). For Dengi header is row 1 — OK.
            // But categories may start at row 1 without header in live sheets; xlsx export has header.
            List<string[]> fuel = DengiFormat.MapFuel(PrependIfNeeded(fuelRaw, false), DengiFormat.CarFromSheetName(fuelName, defaultCar));
            List<string[]> repairs = DengiFormat.MapRepairs(PrependIfNeeded(repairRaw, false), DengiFormat.CarFromSheetName(repairName, defaultCar));
            List<string[]> utilities = DengiFormat.MapUtilities(PrependIfNeeded(utilitiesRaw, false));
            List<string[]> categories = DengiFormat.MapCategories(PrependIfNeeded(categoriesRaw, false));
            repo.ReplaceAll(cars, fuel, repairs, utilities, categories);
        }

        private static string FindSheet(List<string> names, params string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                for (int j = 0; j < names.Count; j++)
                {
                    if (String.Equals(names[j], candidates[i], StringComparison.OrdinalIgnoreCase))
                        return names[j];
                    if (candidates[i].StartsWith("Заправки", StringComparison.OrdinalIgnoreCase) &&
                        names[j].StartsWith("Заправки", StringComparison.OrdinalIgnoreCase))
                        return names[j];
                    if (candidates[i].StartsWith("Ремонт", StringComparison.OrdinalIgnoreCase) &&
                        names[j].StartsWith("Ремонт", StringComparison.OrdinalIgnoreCase) &&
                        !names[j].StartsWith("Ремонтн", StringComparison.OrdinalIgnoreCase))
                        return names[j];
                }
            }
            return null;
        }

        private static List<string[]> PrependIfNeeded(List<string[]> rows, bool unused)
        {
            return rows ?? new List<string[]>();
        }

        private static string[] UtilityHeaders()
        {
            string[] headers = new string[8];
            headers[0] = "Месяц";
            for (int i = 0; i < AlfaCodec.UtilityLabels.Length; i++)
                headers[i + 1] = AlfaCodec.UtilityLabels[i];
            return headers;
        }

        private static void WriteSheet(ZipArchive zip, string entryName, string[] headers, List<string[]> rows)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<sheetData>");
            AppendRow(sb, 1, headers, true);
            for (int i = 0; i < rows.Count; i++)
                AppendRow(sb, i + 2, Pad(RecordIds.StripId(rows[i]), headers.Length), false);
            sb.Append("</sheetData></worksheet>");
            WriteEntry(zip, entryName, sb.ToString());
        }

        private static void AppendRow(StringBuilder sb, int rowIndex, string[] values, bool header)
        {
            sb.Append("<row r=\"").Append(rowIndex.ToString(CultureInfo.InvariantCulture)).Append("\">");
            for (int i = 0; i < values.Length; i++)
            {
                string cellRef = ColumnName(i + 1) + rowIndex.ToString(CultureInfo.InvariantCulture);
                string text = values[i] ?? "";
                sb.Append("<c r=\"").Append(cellRef).Append("\" t=\"inlineStr\"");
                if (header)
                    sb.Append(" s=\"1\"");
                sb.Append("><is><t xml:space=\"preserve\">")
                    .Append(XmlEscape(text))
                    .Append("</t></is></c>");
            }
            sb.Append("</row>");
        }

        private static Dictionary<string, string> ReadSheetMap(ZipArchive zip)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ZipArchiveEntry workbook = zip.GetEntry("xl/workbook.xml");
            ZipArchiveEntry rels = zip.GetEntry("xl/_rels/workbook.xml.rels");
            if (workbook == null || rels == null)
                throw new InvalidDataException("Некорректный xlsx: нет workbook.");

            Dictionary<string, string> relTargets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (Stream stream = rels.Open())
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(stream);
                XmlNodeList nodes = doc.GetElementsByTagName("Relationship");
                for (int i = 0; i < nodes.Count; i++)
                {
                    XmlAttribute id = nodes[i].Attributes["Id"];
                    XmlAttribute target = nodes[i].Attributes["Target"];
                    if (id != null && target != null)
                        relTargets[id.Value] = target.Value.Replace('\\', '/');
                }
            }

            using (Stream stream = workbook.Open())
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(stream);
                XmlNodeList sheets = doc.GetElementsByTagName("sheet");
                for (int i = 0; i < sheets.Count; i++)
                {
                    XmlAttribute name = sheets[i].Attributes["name"];
                    XmlAttribute rid = sheets[i].Attributes["r:id"];
                    if (rid == null)
                        rid = sheets[i].Attributes["id"];
                    if (name == null || rid == null || !relTargets.ContainsKey(rid.Value))
                        continue;
                    string target = relTargets[rid.Value];
                    if (!target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) &&
                        !target.StartsWith("/xl/", StringComparison.OrdinalIgnoreCase))
                        target = "xl/" + target.TrimStart('/');
                    target = target.TrimStart('/');
                    map[name.Value] = target;
                }
            }
            return map;
        }

        private static List<string[]> ReadNamedSheet(ZipArchive zip, Dictionary<string, string> map, string[] names, int columns)
        {
            List<string[]> rows = ReadNamedSheetOptional(zip, map, names, columns);
            return rows;
        }

        private static List<string[]> ReadNamedSheetOptional(ZipArchive zip, Dictionary<string, string> map, string[] names, int columns)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string entryName;
                if (!map.TryGetValue(names[i], out entryName))
                    continue;
                ZipArchiveEntry entry = zip.GetEntry(entryName) ?? zip.GetEntry(entryName.Replace("xl/", ""));
                if (entry == null)
                {
                    foreach (ZipArchiveEntry candidate in zip.Entries)
                    {
                        if (String.Equals(candidate.FullName.Replace('\\', '/'), entryName, StringComparison.OrdinalIgnoreCase))
                        {
                            entry = candidate;
                            break;
                        }
                    }
                }
                if (entry == null)
                    continue;
                return ReadSheetRows(entry, columns);
            }
            return new List<string[]>();
        }

        private static List<string[]> ReadSheetRows(ZipArchiveEntry entry, int columns)
        {
            List<string[]> shared = null;
            // shared strings may be needed; load from parent archive
            ZipArchive archive = entry.Archive;
            ZipArchiveEntry sharedEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (sharedEntry != null)
                shared = ReadSharedStrings(sharedEntry);

            Dictionary<string, string> cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int maxRow = 0;
            int maxCol = 0;
            using (Stream stream = entry.Open())
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(stream);
                XmlNodeList cellNodes = doc.GetElementsByTagName("c");
                for (int i = 0; i < cellNodes.Count; i++)
                {
                    XmlElement cell = (XmlElement)cellNodes[i];
                    string reference = cell.GetAttribute("r");
                    if (String.IsNullOrEmpty(reference))
                        continue;
                    int row;
                    int col;
                    ParseCellRef(reference, out col, out row);
                    if (row > maxRow) maxRow = row;
                    if (col > maxCol) maxCol = col;
                    string type = cell.GetAttribute("t");
                    string value = ExtractCellValue(cell, type, shared);
                    cells[reference] = value;
                }
            }

            List<string[]> rows = new List<string[]>();
            if (maxRow < 2)
                return rows;

            int width = Math.Max(columns, maxCol);
            for (int r = 2; r <= maxRow; r++)
            {
                string[] row = new string[columns];
                bool empty = true;
                for (int c = 1; c <= columns; c++)
                {
                    string key = ColumnName(c) + r.ToString(CultureInfo.InvariantCulture);
                    string value;
                    if (!cells.TryGetValue(key, out value))
                        value = "";
                    row[c - 1] = value;
                    if (!String.IsNullOrEmpty(value.Trim()))
                        empty = false;
                }
                if (!empty)
                    rows.Add(row);
            }
            return rows;
        }

        private static List<string[]> ReadSharedStrings(ZipArchiveEntry entry)
        {
            List<string[]> list = new List<string[]>();
            using (Stream stream = entry.Open())
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(stream);
                XmlNodeList items = doc.GetElementsByTagName("si");
                for (int i = 0; i < items.Count; i++)
                {
                    StringBuilder text = new StringBuilder();
                    XmlNodeList texts = ((XmlElement)items[i]).GetElementsByTagName("t");
                    for (int t = 0; t < texts.Count; t++)
                        text.Append(texts[t].InnerText);
                    list.Add(new string[] { text.ToString() });
                }
            }
            return list;
        }

        private static string ExtractCellValue(XmlElement cell, string type, List<string[]> shared)
        {
            if (String.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                XmlNodeList texts = cell.GetElementsByTagName("t");
                if (texts.Count > 0)
                    return texts[0].InnerText ?? "";
                return "";
            }

            XmlNodeList values = cell.GetElementsByTagName("v");
            string raw = values.Count > 0 ? (values[0].InnerText ?? "") : "";
            if (String.Equals(type, "s", StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (Int32.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out index) &&
                    shared != null && index >= 0 && index < shared.Count)
                    return shared[index][0];
            }
            return raw;
        }

        private static void ParseCellRef(string reference, out int col, out int row)
        {
            int i = 0;
            col = 0;
            while (i < reference.Length && Char.IsLetter(reference[i]))
            {
                col = col * 26 + (Char.ToUpperInvariant(reference[i]) - 'A' + 1);
                i++;
            }
            row = 0;
            Int32.TryParse(reference.Substring(i), NumberStyles.Integer, CultureInfo.InvariantCulture, out row);
        }

        private static string ColumnName(int index)
        {
            string name = "";
            while (index > 0)
            {
                int rem = (index - 1) % 26;
                name = (char)('A' + rem) + name;
                index = (index - 1) / 26;
            }
            return name;
        }

        private static string[] Pad(string[] source, int length)
        {
            string[] result = new string[length];
            for (int i = 0; i < length; i++)
                result[i] = i < source.Length ? (source[i] ?? "") : "";
            return result;
        }

        private static string XmlEscape(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private static void WriteEntry(ZipArchive zip, string name, string content)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (Stream stream = entry.Open())
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(content);
        }

        private static string ContentTypesXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet3.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet4.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet5.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet6.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet7.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "</Types>";
        }

        private static string RelsXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "</Relationships>";
        }

        private static string WorkbookXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                "<sheets>" +
                "<sheet name=\"Автомобили\" sheetId=\"1\" r:id=\"rId1\"/>" +
                "<sheet name=\"Заправки\" sheetId=\"2\" r:id=\"rId2\"/>" +
                "<sheet name=\"Ремонт\" sheetId=\"3\" r:id=\"rId3\"/>" +
                "<sheet name=\"Коммуналка\" sheetId=\"4\" r:id=\"rId4\"/>" +
                "<sheet name=\"Категории\" sheetId=\"5\" r:id=\"rId5\"/>" +
                "<sheet name=\"Прочие\" sheetId=\"6\" r:id=\"rId6\"/>" +
                "<sheet name=\"Кат. прочих\" sheetId=\"7\" r:id=\"rId7\"/>" +
                "</sheets></workbook>";
        }

        private static string WorkbookRelsXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/>" +
                "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet3.xml\"/>" +
                "<Relationship Id=\"rId4\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet4.xml\"/>" +
                "<Relationship Id=\"rId5\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet5.xml\"/>" +
                "<Relationship Id=\"rId6\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet6.xml\"/>" +
                "<Relationship Id=\"rId7\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet7.xml\"/>" +
                "<Relationship Id=\"rId8\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                "</Relationships>";
        }

        private static string StylesXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<fonts count=\"2\">" +
                "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
                "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
                "</fonts>" +
                "<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
                "<borders count=\"1\"><border/></borders>" +
                "<cellStyleXfs count=\"1\"><xf/></cellStyleXfs>" +
                "<cellXfs count=\"2\">" +
                "<xf/>" +
                "<xf fontId=\"1\" applyFont=\"1\"/>" +
                "</cellXfs>" +
                "</styleSheet>";
        }
    }
}
