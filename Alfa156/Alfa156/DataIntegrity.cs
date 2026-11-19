using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CarExpenses
{
    /// <summary>
    /// Offline data integrity report: orphans, categories, broken fields, receipts, tombstones.
    /// </summary>
    internal static class DataIntegrity
    {
        private const string DataDirectory = @"\My Documents\Alfa156";
        private const string RepairCategoriesFile = "repair-categories.dat";
        private const string ExpenseCategoriesFile = "expense-categories.dat";

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

        internal static Report Check()
        {
            Report report = new Report();
            if (!Directory.Exists(DataDirectory))
            {
                report.Findings.Add(new Finding("данные", "error", "Папка данных не найдена."));
                return report;
            }

            Dictionary<string, bool> cars = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            List<string> carNames = DataStore.LoadCarNames();
            for (int i = 0; i < carNames.Count; i++)
            {
                string n = carNames[i] == null ? "" : carNames[i].Trim();
                if (n.Length > 0 && !cars.ContainsKey(n))
                    cars.Add(n, true);
            }

            List<string[]> fuels = DataStore.LoadFuel();
            List<string[]> repairs = DataStore.LoadRepairs();
            List<string[]> expenses = DataStore.LoadExpenses();
            List<string[]> utilities = DataStore.LoadUtilities();
            List<string> repairCats = ReadCategoryNamesRaw(RepairCategoriesFile);
            List<string> expenseCats = ReadCategoryNamesRaw(ExpenseCategoriesFile);

            CheckFuel(report, fuels, cars);
            CheckRepairs(report, repairs, cars, repairCats);
            CheckExpenses(report, expenses, cars, expenseCats);
            CheckUtilities(report, utilities);
            CheckCategoryLists(report, repairCats, expenseCats, repairs, expenses);
            CheckReceipts(report, fuels, repairs, expenses);
            CheckTombstones(report, fuels, repairs, expenses, utilities);
            if (report.Findings.Count == 0)
                report.Findings.Add(new Finding("итог", "info", "Проблем не найдено."));
            return report;
        }

        internal static string Format(Report report)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Проверка данных\r\n");
            sb.Append("ошибок: ").Append(report.ErrorCount)
                .Append(", предупреждений: ").Append(report.WarnCount)
                .Append(", сведений: ").Append(report.InfoCount).Append("\r\n");
            string lastCat = null;
            for (int i = 0; i < report.Findings.Count; i++)
            {
                Finding f = report.Findings[i];
                if (f.Category != lastCat)
                {
                    sb.Append("\r\n[").Append(f.Category).Append("]\r\n");
                    lastCat = f.Category;
                }
                sb.Append("· ").Append(f.Severity).Append(": ").Append(f.Message).Append("\r\n");
            }
            if (report.CanSimpleFix)
                sb.Append("\r\nМожно «Исправить простое»: добавить отсутствующие категории в списки.");
            return sb.ToString();
        }

        internal static int ApplySimpleFixes(Report report)
        {
            if (report == null)
                return 0;
            int added = 0;
            for (int i = 0; i < report.MissingRepairCategories.Count; i++)
            {
                try
                {
                    DataStore.AddRepairCategory(report.MissingRepairCategories[i]);
                    added++;
                }
                catch (InvalidOperationException)
                {
                }
            }
            for (int i = 0; i < report.MissingExpenseCategories.Count; i++)
            {
                try
                {
                    DataStore.AddExpenseCategory(report.MissingExpenseCategories[i]);
                    added++;
                }
                catch (InvalidOperationException)
                {
                }
            }
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
                if (c[1] != null && c[1].Length > 0 && !cars.ContainsKey(c[1]))
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
                if (c[2] != null && c[2].Length > 0)
                    CheckNonNegative(report, label, "пробег", c[2]);
                CheckNonNegative(report, label, "сумма", c[7]);
                if (c[1] != null && c[1].Length > 0 && !cars.ContainsKey(c[1]))
                    report.Findings.Add(new Finding("сироты", "warn",
                        label + ": авто «" + c[1] + "» нет в списке."));
                string cat = c[3] == null ? "" : c[3].Trim();
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
                if (c[1] != null && c[1].Length > 0 && !cars.ContainsKey(c[1]))
                    report.Findings.Add(new Finding("сироты", "warn",
                        label + ": авто «" + c[1] + "» нет в списке."));
                string cat = c[2] == null ? "" : c[2].Trim();
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
                for (int j = 1; j < c.Length && j <= 7; j++)
                {
                    if (c[j] != null && c[j].Length > 0)
                        CheckNonNegative(report, label, "сумма[" + j + "]", c[j]);
                }
            }
        }

        private static void CheckCategoryLists(Report report, List<string> repairCats,
            List<string> expenseCats, List<string[]> repairs, List<string[]> expenses)
        {
            Dictionary<string, bool> usedRepair = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < repairs.Count; i++)
            {
                string[] c = RecordIds.StripId(repairs[i]);
                if (c.Length > 3 && c[3] != null && c[3].Length > 0)
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
                if (c.Length > 2 && c[2] != null && c[2].Length > 0)
                    usedExpense[c[2]] = true;
            }
            for (int i = 0; i < expenseCats.Count; i++)
            {
                if (expenseCats[i].Length > 0 && !usedExpense.ContainsKey(expenseCats[i]))
                    report.Findings.Add(new Finding("категории", "info",
                        "категория прочих «" + expenseCats[i] + "» не используется."));
            }
        }

        private static void CheckReceipts(Report report,
            List<string[]> fuels, List<string[]> repairs, List<string[]> expenses)
        {
            Dictionary<string, bool> ids = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            CollectIds(ids, fuels);
            CollectIds(ids, repairs);
            CollectIds(ids, expenses);
            string folder = Path.Combine(DataDirectory, "Receipts");
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

        private static void CheckTombstones(Report report,
            List<string[]> fuels, List<string[]> repairs, List<string[]> expenses,
            List<string[]> utilities)
        {
            List<Tombstone> tombs = RecordIds.ReadTombstones(DataDirectory);
            if (tombs == null || tombs.Count == 0)
                return;
            Dictionary<string, bool> live = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            CollectIds(live, fuels);
            CollectIds(live, repairs);
            CollectIds(live, expenses);
            CollectIds(live, utilities);
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
            value = value == null ? "" : value.Trim();
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
            value = value == null ? "" : value.Trim();
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
            value = value == null ? "" : value.Trim();
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

        private static List<string> ReadCategoryNamesRaw(string fileName)
        {
            List<string> names = new List<string>();
            try
            {
                List<string[]> rows = DataStore.ReadSyncTable(DataDirectory, fileName);
                for (int i = 0; i < rows.Count; i++)
                {
                    string[] c = RecordIds.StripId(rows[i]);
                    if (c.Length > 0 && c[0] != null && c[0].Trim().Length > 0)
                        names.Add(c[0].Trim());
                }
            }
            catch
            {
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
                if (id != null && id.Length > 0 && !ids.ContainsKey(id))
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
