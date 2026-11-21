using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Alfa156.Desktop.Data
{
    /// <summary>
    /// Offline data integrity report: orphans, categories, broken fields, receipts, tombstones.
    /// </summary>
    internal static class DataIntegrity
    {
        internal sealed class Finding
        {
            public string Category;
            public string Severity;
            public string Message;

            public Finding(string category, string severity, string message)
            {
                Category = category;
                Severity = severity;
                Message = message;
            }
        }

        internal sealed class Report
        {
            public List<Finding> Findings = new List<Finding>();
            public List<string> MissingRepairCategories = new List<string>();
            public List<string> MissingExpenseCategories = new List<string>();

            public int ErrorCount { get { return CountSeverity("error"); } }
            public int WarnCount { get { return CountSeverity("warn"); } }
            public int InfoCount { get { return CountSeverity("info"); } }
            public bool CanSimpleFix
            {
                get
                {
                    return MissingRepairCategories.Count > 0 || MissingExpenseCategories.Count > 0;
                }
            }

            private int CountSeverity(string severity)
            {
                int n = 0;
                for (int i = 0; i < Findings.Count; i++)
                    if (Findings[i].Severity == severity)
                        n++;
                return n;
            }
        }

        internal static Report Check(AlfaRepository repository)
        {
            Report report = new Report();
            if (repository == null || String.IsNullOrEmpty(repository.DirectoryPath))
            {
                report.Findings.Add(new Finding("данные", "error", "Папка данных не открыта."));
                return report;
            }

            Dictionary<string, bool> cars = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < repository.Cars.Count; i++)
            {
                string[] c = RecordIds.StripId(repository.Cars[i]);
                if (c.Length > 0)
                {
                    string name = (c[0] ?? "").Trim();
                    if (name.Length > 0 && !cars.ContainsKey(name))
                        cars.Add(name, true);
                }
            }

            // Prefer raw category files so missing names are visible even if Ensure* already ran in memory.
            List<string> repairCats = ReadCategoryNamesRaw(repository.DirectoryPath, AlfaCodec.RepairCategoriesFile);
            List<string> expenseCats = ReadCategoryNamesRaw(repository.DirectoryPath, AlfaCodec.ExpenseCategoriesFile);
            if (repairCats.Count == 0)
                repairCats = NamesFromRows(repository.Categories);
            if (expenseCats.Count == 0)
                expenseCats = NamesFromRows(repository.ExpenseCategories);

            CheckFuel(report, repository.Fuel, cars);
            CheckRepairs(report, repository.Repairs, cars, repairCats);
            CheckExpenses(report, repository.Expenses, cars, expenseCats);
            CheckUtilities(report, repository.Utilities);
            CheckCategoryLists(report, repairCats, expenseCats, repository.Repairs, repository.Expenses);
            CheckReceipts(report, repository.DirectoryPath, repository.Fuel, repository.Repairs, repository.Expenses);
            CheckTombstones(report, repository);
            if (report.Findings.Count == 0)
                report.Findings.Add(new Finding("итог", "info", "Проблем не найдено."));
            return report;
        }

        internal static string Format(Report report)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Проверка данных");
            sb.Append("ошибок: ").Append(report.ErrorCount)
                .Append(", предупреждений: ").Append(report.WarnCount)
                .Append(", сведений: ").Append(report.InfoCount).AppendLine();
            string lastCat = null;
            for (int i = 0; i < report.Findings.Count; i++)
            {
                Finding f = report.Findings[i];
                if (f.Category != lastCat)
                {
                    sb.AppendLine();
                    sb.Append('[').Append(f.Category).AppendLine("]");
                    lastCat = f.Category;
                }
                sb.Append("· ").Append(f.Severity).Append(": ").AppendLine(f.Message);
            }
            if (report.CanSimpleFix)
                sb.AppendLine().Append("Можно «Исправить простое»: добавить отсутствующие категории в списки.");
            return sb.ToString();
        }

        internal static int ApplySimpleFixes(AlfaRepository repository, Report report)
        {
            if (repository == null || report == null)
                return 0;
            int added = 0;
            Dictionary<string, bool> repairKnown =
                new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < repository.Categories.Count; i++)
            {
                string[] c = RecordIds.StripId(repository.Categories[i]);
                if (c.Length > 0 && c[0].Trim().Length > 0)
                    repairKnown[c[0].Trim()] = true;
            }
            for (int i = 0; i < report.MissingRepairCategories.Count; i++)
            {
                string name = report.MissingRepairCategories[i].Trim();
                if (name.Length == 0 || repairKnown.ContainsKey(name))
                    continue;
                repository.Categories.Add(RecordIds.EnsureId(new string[] { name },
                    RecordIds.PrefixForFile(AlfaCodec.RepairCategoriesFile)));
                repairKnown[name] = true;
                added++;
            }

            Dictionary<string, bool> expenseKnown =
                new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < repository.ExpenseCategories.Count; i++)
            {
                string[] c = RecordIds.StripId(repository.ExpenseCategories[i]);
                if (c.Length > 0 && c[0].Trim().Length > 0)
                    expenseKnown[c[0].Trim()] = true;
            }
            for (int i = 0; i < report.MissingExpenseCategories.Count; i++)
            {
                string name = report.MissingExpenseCategories[i].Trim();
                if (name.Length == 0 || expenseKnown.ContainsKey(name))
                    continue;
                repository.ExpenseCategories.Add(RecordIds.EnsureId(new string[] { name },
                    RecordIds.PrefixForFile(AlfaCodec.ExpenseCategoriesFile)));
                expenseKnown[name] = true;
                added++;
            }
            if (added > 0)
                repository.Save();
            return added;
        }

        private static void CheckFuel(Report report, List<string[]> rows, Dictionary<string, bool> cars)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                string[] c = RecordIds.StripId(rows[i]);
                string label = "заправка #" + (i + 1);
                if (c.Length < 5)
                {
                    report.Findings.Add(new Finding("поля", "error", label + ": слишком мало полей."));
                    continue;
                }
                CheckDate(report, label, c[0]);
                CheckRequired(report, label, "авто", c[1]);
                CheckRequired(report, label, "пробег", c[2]);
                CheckRequired(report, label, "литры", c[3]);
                CheckRequired(report, label, "сумма", c[4]);
                CheckNonNegative(report, label, "пробег", c[2]);
                CheckNonNegative(report, label, "литры", c[3]);
                CheckNonNegative(report, label, "сумма", c[4]);
                if (c[1].Length > 0 && !cars.ContainsKey(c[1]))
                    report.Findings.Add(new Finding("сироты", "warn",
                        label + ": авто «" + c[1] + "» нет в списке."));
            }
        }

        private static void CheckRepairs(Report report, List<string[]> rows,
            Dictionary<string, bool> cars, List<string> categories)
        {
            Dictionary<string, bool> known = ToSet(categories);
            for (int i = 0; i < rows.Count; i++)
            {
                string[] c = RecordIds.StripId(rows[i]);
                string label = "ремонт #" + (i + 1);
                if (c.Length < 8)
                {
                    report.Findings.Add(new Finding("поля", "error", label + ": слишком мало полей."));
                    continue;
                }
                CheckDate(report, label, c[0]);
                CheckRequired(report, label, "авто", c[1]);
                CheckRequired(report, label, "категория", c[3]);
                CheckRequired(report, label, "товар", c[4]);
                CheckRequired(report, label, "сумма", c[7]);
                if (c[2].Length > 0)
                    CheckNonNegative(report, label, "пробег", c[2]);
                CheckNonNegative(report, label, "сумма", c[7]);
                if (c[1].Length > 0 && !cars.ContainsKey(c[1]))
                    report.Findings.Add(new Finding("сироты", "warn",
                        label + ": авто «" + c[1] + "» нет в списке."));
                string cat = (c[3] ?? "").Trim();
                if (cat.Length > 0 && !known.ContainsKey(cat))
                {
                    report.Findings.Add(new Finding("категории", "warn",
                        label + ": категория ремонта «" + cat + "» отсутствует в списке."));
                    if (!ContainsIgnoreCase(report.MissingRepairCategories, cat))
                        report.MissingRepairCategories.Add(cat);
                }
            }
        }

        private static void CheckExpenses(Report report, List<string[]> rows,
            Dictionary<string, bool> cars, List<string> categories)
        {
            Dictionary<string, bool> known = ToSet(categories);
            for (int i = 0; i < rows.Count; i++)
            {
                string[] c = RecordIds.StripId(rows[i]);
                string label = "прочие #" + (i + 1);
                if (c.Length < 5)
                {
                    report.Findings.Add(new Finding("поля", "error", label + ": слишком мало полей."));
                    continue;
                }
                CheckDate(report, label, c[0]);
                CheckRequired(report, label, "авто", c[1]);
                CheckRequired(report, label, "категория", c[2]);
                CheckRequired(report, label, "сумма", c[4]);
                CheckNonNegative(report, label, "сумма", c[4]);
                if (c[1].Length > 0 && !cars.ContainsKey(c[1]))
                    report.Findings.Add(new Finding("сироты", "warn",
                        label + ": авто «" + c[1] + "» нет в списке."));
                string cat = (c[2] ?? "").Trim();
                if (cat.Length > 0 && !known.ContainsKey(cat))
                {
                    report.Findings.Add(new Finding("категории", "warn",
                        label + ": категория прочих «" + cat + "» отсутствует в списке."));
                    if (!ContainsIgnoreCase(report.MissingExpenseCategories, cat))
                        report.MissingExpenseCategories.Add(cat);
                }
            }
        }

        private static void CheckUtilities(Report report, List<string[]> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                string[] c = RecordIds.StripId(rows[i]);
                string label = "коммуналка #" + (i + 1);
                if (c.Length < 1)
                {
                    report.Findings.Add(new Finding("поля", "error", label + ": пустая строка."));
                    continue;
                }
                CheckMonth(report, label, c[0]);
                bool hasAmount = false;
                for (int j = 1; j < c.Length && j <= 7; j++)
                {
                    if (c[j] != null && c[j].Length > 0)
                    {
                        hasAmount = true;
                        CheckNonNegative(report, label, "сумма[" + j + "]", c[j]);
                    }
                }
                if (!hasAmount)
                    report.Findings.Add(new Finding("поля", "error",
                        label + ": не указана ни одна сумма."));
            }
        }

        private static void CheckCategoryLists(Report report, List<string> repairCats,
            List<string> expenseCats, List<string[]> repairs, List<string[]> expenses)
        {
            Dictionary<string, bool> usedRepair = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < repairs.Count; i++)
            {
                string[] c = RecordIds.StripId(repairs[i]);
                if (c.Length > 3 && c[3].Length > 0)
                    usedRepair[c[3]] = true;
            }
            for (int i = 0; i < repairCats.Count; i++)
            {
                if (repairCats[i].Length > 0 && !usedRepair.ContainsKey(repairCats[i]))
                    report.Findings.Add(new Finding("категории", "info",
                        "категория ремонта «" + repairCats[i] + "» не используется."));
            }

            Dictionary<string, bool> usedExpense = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < expenses.Count; i++)
            {
                string[] c = RecordIds.StripId(expenses[i]);
                if (c.Length > 2 && c[2].Length > 0)
                    usedExpense[c[2]] = true;
            }
            for (int i = 0; i < expenseCats.Count; i++)
            {
                if (expenseCats[i].Length > 0 && !usedExpense.ContainsKey(expenseCats[i]))
                    report.Findings.Add(new Finding("категории", "info",
                        "категория прочих «" + expenseCats[i] + "» не используется."));
            }
        }

        private static void CheckReceipts(Report report, string directory,
            List<string[]> fuels, List<string[]> repairs, List<string[]> expenses)
        {
            Dictionary<string, bool> ids = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            CollectIds(ids, fuels);
            CollectIds(ids, repairs);
            CollectIds(ids, expenses);
            string folder = ReceiptStore.GetFolder(directory);
            if (!Directory.Exists(folder))
                return;
            string[] files;
            try { files = Directory.GetFiles(folder, "*.jpg"); }
            catch { return; }
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileNameWithoutExtension(files[i]);
                if (!RecordIds.LooksLikeId(name) || !ids.ContainsKey(name))
                    report.Findings.Add(new Finding("чеки", "info",
                        "файл чека без записи: " + Path.GetFileName(files[i])));
            }
        }

        private static void CheckTombstones(Report report, AlfaRepository repository)
        {
            List<Tombstone> tombs = repository.Deleted ?? RecordIds.ReadTombstones(repository.DirectoryPath);
            if (tombs == null || tombs.Count == 0)
                return;
            Dictionary<string, bool> live = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            CollectIds(live, repository.Fuel);
            CollectIds(live, repository.Repairs);
            CollectIds(live, repository.Expenses);
            CollectIds(live, repository.Utilities);
            int dangling = 0;
            for (int i = 0; i < tombs.Count; i++)
                if (!live.ContainsKey(tombs[i].Id))
                    dangling++;
            if (dangling > 0)
                report.Findings.Add(new Finding("tombstone", "info",
                    "tombstone без живой записи: " + dangling + " (нормально после удалений)."));
        }

        private static void CheckDate(Report report, string label, string value)
        {
            value = (value ?? "").Trim();
            if (value.Length == 0)
            {
                report.Findings.Add(new Finding("даты", "error", label + ": пустая дата."));
                return;
            }
            DateTime dt;
            if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out dt))
                report.Findings.Add(new Finding("даты", "error",
                    label + ": неверная дата «" + value + "» (ожидается yyyy-MM-dd)."));
        }

        private static void CheckMonth(Report report, string label, string value)
        {
            value = (value ?? "").Trim();
            if (value.Length == 0)
            {
                report.Findings.Add(new Finding("даты", "error", label + ": пустой месяц."));
                return;
            }
            DateTime dt;
            if (!DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out dt) &&
                !DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out dt))
                report.Findings.Add(new Finding("даты", "error",
                    label + ": неверный месяц «" + value + "»."));
        }

        private static void CheckRequired(Report report, string label, string field, string value)
        {
            if (value == null || value.Trim().Length == 0)
                report.Findings.Add(new Finding("поля", "error",
                    label + ": пустое обязательное поле «" + field + "»."));
        }

        private static void CheckNonNegative(Report report, string label, string field, string value)
        {
            value = (value ?? "").Trim();
            if (value.Length == 0)
                return;
            decimal n;
            if (!Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out n))
            {
                report.Findings.Add(new Finding("поля", "error",
                    label + ": «" + field + "» не число («" + value + "»)."));
                return;
            }
            if (n < 0)
                report.Findings.Add(new Finding("поля", "error",
                    label + ": отрицательное «" + field + "» (" + value + ")."));
        }

        private static List<string> ReadCategoryNamesRaw(string directory, string fileName)
        {
            List<string> names = new List<string>();
            string path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
                return names;
            try
            {
                List<string[]> rows = AlfaCodec.ReadRows(path);
                for (int i = 0; i < rows.Count; i++)
                {
                    string[] c = RecordIds.StripId(rows[i]);
                    if (c.Length > 0 && c[0].Trim().Length > 0)
                        names.Add(c[0].Trim());
                }
            }
            catch
            {
            }
            return names;
        }

        private static List<string> NamesFromRows(List<string[]> rows)
        {
            List<string> names = new List<string>();
            if (rows == null)
                return names;
            for (int i = 0; i < rows.Count; i++)
            {
                string[] c = RecordIds.StripId(rows[i]);
                if (c.Length > 0 && c[0].Trim().Length > 0)
                    names.Add(c[0].Trim());
            }
            return names;
        }

        private static Dictionary<string, bool> ToSet(List<string> names)
        {
            Dictionary<string, bool> set = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < names.Count; i++)
                if (names[i].Length > 0 && !set.ContainsKey(names[i]))
                    set.Add(names[i], true);
            return set;
        }

        private static void CollectIds(Dictionary<string, bool> ids, List<string[]> rows)
        {
            if (rows == null)
                return;
            for (int i = 0; i < rows.Count; i++)
            {
                string id = RecordIds.GetId(rows[i]);
                if (!String.IsNullOrEmpty(id) && !ids.ContainsKey(id))
                    ids.Add(id, true);
            }
        }

        private static bool ContainsIgnoreCase(List<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
                if (String.Equals(list[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
