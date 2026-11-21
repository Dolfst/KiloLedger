using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using Alfa156.Localization;
namespace Alfa156.Android;

/// <summary>
/// Offline data integrity report: orphans, categories, broken fields, receipts, tombstones.
/// </summary>
internal static class DataIntegrity
{
    internal sealed class Finding
    {
        public string Category;
        public string Severity; // error | warn | info
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

        public int ErrorCount
        {
            get { return CountSeverity("error"); }
        }

        public int WarnCount
        {
            get { return CountSeverity("warn"); }
        }

        public int InfoCount
        {
            get { return CountSeverity("info"); }
        }

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

    internal static Report Check(string directory)
    {
        Report report = new Report();
        if (String.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            report.Findings.Add(new Finding(L10n.Get("integrity.cat_data"), "error", L10n.Get("error.data_folder_not_found")));
            return report;
        }

        Dictionary<string, bool> cars = LoadCarSet(directory);
        List<string[]> fuels = SafeLoad(directory, "fuel.dat");
        List<string[]> repairs = SafeLoad(directory, "repairs.dat");
        List<string[]> expenses = SafeLoadOptional(directory, ExpenseDataCodec.ExpensesFile);
        List<string[]> utilities = SafeLoad(directory, "utilities.dat");
        List<string[]> carsRows = SafeLoad(directory, "cars.dat");
        List<string[]> repairCategories = SafeLoad(directory, ExpenseDataCodec.RepairCategoriesFile);
        List<string[]> expenseCategories = SafeLoadOptional(directory, ExpenseDataCodec.ExpenseCategoriesFile);
        List<string[]> carsMeta = SafeLoadOptional(directory, ExpenseDataCodec.CarsMetaFile);
        List<string[]> templates = SafeLoadOptional(directory, ExpenseDataCodec.TemplatesFile);
        List<string[]> reminders = SafeLoadOptional(directory, ExpenseDataCodec.RemindersFile);
        List<string[]> notes = SafeLoadOptional(directory, ExpenseDataCodec.NotesFile);
        List<string> repairCats = ReadCategoryNamesRaw(directory, ExpenseDataCodec.RepairCategoriesFile);
        List<string> expenseCats = ReadCategoryNamesRaw(directory, ExpenseDataCodec.ExpenseCategoriesFile);

        CheckFuel(report, fuels, cars);
        CheckRepairs(report, repairs, cars, repairCats);
        CheckExpenses(report, expenses, cars, expenseCats);
        CheckUtilities(report, utilities);
        CheckCarsMeta(report, carsMeta);
        CheckDuplicateIds(report, fuels, L10n.Get("integrity.target_fuel"));
        CheckDuplicateIds(report, repairs, L10n.Get("integrity.target_repairs"));
        CheckDuplicateIds(report, expenses, L10n.Get("integrity.target_expenses"));
        CheckDuplicateIds(report, utilities, L10n.Get("integrity.target_utilities"));
        CheckDuplicateIds(report, carsRows, L10n.Get("integrity.target_cars"));
        CheckDuplicateIds(report, repairCategories, L10n.Get("integrity.target_repair_cats"));
        CheckDuplicateIds(report, expenseCategories, L10n.Get("integrity.target_expense_cats"));
        CheckDuplicateIds(report, carsMeta, L10n.Get("integrity.target_passports"));
        CheckDuplicateIds(report, templates, L10n.Get("integrity.target_templates"));
        CheckDuplicateIds(report, reminders, L10n.Get("integrity.target_reminders"));
        CheckDuplicateIds(report, notes, L10n.Get("integrity.target_notes"));
        CheckCategoryLists(report, repairCats, expenseCats, repairs, expenses);
        CheckReceipts(report, directory, fuels, repairs, expenses);
        CheckTombstones(report, directory, fuels, repairs, expenses, utilities, cars,
            repairCats, expenseCats);
        if (report.Findings.Count == 0)
            report.Findings.Add(new Finding(L10n.Get("integrity.cat_summary"), "info", L10n.Get("integrity.no_issues")));
        return report;
    }

    internal static string Format(Report report)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append((L10n.Get("button.data_integrity") + "\n"));
        sb.Append((L10n.Get("integrity.errors_colon") + " ")).Append(report.ErrorCount)
            .Append((L10n.Get("integrity.comma_warnings") + " ")).Append(report.WarnCount)
            .Append((L10n.Get("integrity.comma_info") + " ")).Append(report.InfoCount).Append('\n');
        string lastCat = null;
        for (int i = 0; i < report.Findings.Count; i++)
        {
            Finding f = report.Findings[i];
            if (f.Category != lastCat)
            {
                sb.Append('\n').Append('[').Append(f.Category).Append("]\n");
                lastCat = f.Category;
            }
            sb.Append("· ").Append(f.Severity).Append(": ").Append(f.Message).Append('\n');
        }
        if (report.CanSimpleFix)
            sb.Append(("\n" + L10n.Get("integrity.can_quick_fix")));
        return sb.ToString();
    }

    /// <summary>Adds missing category names to list files. Returns how many were added.</summary>
    internal static int ApplySimpleFixes(string directory, Report report)
    {
        if (report == null)
            return 0;
        int added = 0;
        for (int i = 0; i < report.MissingRepairCategories.Count; i++)
        {
            try
            {
                ExpenseDataStore.AddRepairCategory(directory, report.MissingRepairCategories[i]);
                added++;
            }
            catch (InvalidOperationException)
            {
                // already present
            }
        }
        for (int i = 0; i < report.MissingExpenseCategories.Count; i++)
        {
            try
            {
                ExpenseDataStore.AddExpenseCategory(directory, report.MissingExpenseCategories[i]);
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
            string label = L10n.Get("integrity.fuel_prefix") + (i + 1);
            if (c.Length < 5)
            {
                report.Findings.Add(new Finding(L10n.Get("integrity.cat_fields"), "error",
                    label + L10n.Get("integrity.too_few_fields")));
                continue;
            }
            CheckDate(report, label, c[0]);
            CheckRequired(report, label, L10n.Get("integrity.field_car"), c[1]);
            CheckRequired(report, label, L10n.Get("integrity.field_odometer"), c[2]);
            CheckRequired(report, label, L10n.Get("integrity.field_liters"), c[3]);
            CheckRequired(report, label, L10n.Get("integrity.field_amount"), c[4]);
            CheckNonNegative(report, label, L10n.Get("integrity.field_odometer"), c[2]);
            CheckNonNegative(report, label, L10n.Get("integrity.field_liters"), c[3]);
            CheckNonNegative(report, label, L10n.Get("integrity.field_amount"), c[4]);
            if (c[1].Length > 0 && !cars.ContainsKey(c[1]))
                report.Findings.Add(new Finding(L10n.Get("integrity.cat_orphans"), "warn",
                    label + L10n.Get("integrity.car_open") + c[1] + L10n.Get("integrity.not_in_list")));
        }
    }

    private static void CheckRepairs(Report report, List<string[]> rows,
        Dictionary<string, bool> cars, List<string> categories)
    {
        Dictionary<string, bool> known = ToSet(categories);
        for (int i = 0; i < rows.Count; i++)
        {
            string[] c = RecordIds.StripId(rows[i]);
            string label = L10n.Get("integrity.repair_prefix") + (i + 1);
            if (c.Length < 8)
            {
                report.Findings.Add(new Finding(L10n.Get("integrity.cat_fields"), "error",
                    label + L10n.Get("integrity.too_few_fields")));
                continue;
            }
            CheckDate(report, label, c[0]);
            CheckRequired(report, label, L10n.Get("integrity.field_car"), c[1]);
            CheckRequired(report, label, L10n.Get("integrity.field_category"), c[3]);
            CheckRequired(report, label, L10n.Get("integrity.field_product"), c[4]);
            CheckRequired(report, label, L10n.Get("integrity.field_amount"), c[7]);
            if (c[2].Length > 0)
                CheckNonNegative(report, label, L10n.Get("integrity.field_odometer"), c[2]);
            CheckNonNegative(report, label, L10n.Get("integrity.field_amount"), c[7]);
            if (c[1].Length > 0 && !cars.ContainsKey(c[1]))
                report.Findings.Add(new Finding(L10n.Get("integrity.cat_orphans"), "warn",
                    label + L10n.Get("integrity.car_open") + c[1] + L10n.Get("integrity.not_in_list")));
            string cat = (c[3] ?? "").Trim();
            if (cat.Length > 0 && !known.ContainsKey(cat))
            {
                report.Findings.Add(new Finding(L10n.Get("integrity.cat_categories"), "warn",
                    label + L10n.Get("integrity.repair_cat_open") + cat + L10n.Get("integrity.missing_in_list_suffix")));
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
            string label = L10n.Get("integrity.expense_prefix") + (i + 1);
            if (c.Length < 5)
            {
                report.Findings.Add(new Finding(L10n.Get("integrity.cat_fields"), "error",
                    label + L10n.Get("integrity.too_few_fields")));
                continue;
            }
            CheckDate(report, label, c[0]);
            CheckRequired(report, label, L10n.Get("integrity.field_car"), c[1]);
            CheckRequired(report, label, L10n.Get("integrity.field_category"), c[2]);
            CheckRequired(report, label, L10n.Get("integrity.field_amount"), c[4]);
            CheckNonNegative(report, label, L10n.Get("integrity.field_amount"), c[4]);
            if (c[1].Length > 0 && !cars.ContainsKey(c[1]))
                report.Findings.Add(new Finding(L10n.Get("integrity.cat_orphans"), "warn",
                    label + L10n.Get("integrity.car_open") + c[1] + L10n.Get("integrity.not_in_list")));
            string cat = (c[2] ?? "").Trim();
            if (cat.Length > 0 && !known.ContainsKey(cat))
            {
                report.Findings.Add(new Finding(L10n.Get("integrity.cat_categories"), "warn",
                    label + L10n.Get("integrity.expense_cat_open") + cat + L10n.Get("integrity.missing_in_list_suffix")));
                if (!ContainsIgnoreCase(report.MissingExpenseCategories, cat))
                    report.MissingExpenseCategories.Add(cat);
            }
        }
    }

    private static void CheckCarsMeta(Report report, List<string[]> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            string[] c = RecordIds.StripId(rows[i]);
            string label = L10n.Get("integrity.passport_prefix") + (i + 1);
            if (c.Length < 1)
            {
                report.Findings.Add(new Finding(L10n.Get("integrity.cat_fields"), "error",
                    label + L10n.Get("integrity.empty_row")));
                continue;
            }
            CheckRequired(report, label, L10n.Get("integrity.field_car"), c[0]);
            if (c.Length > 1 && c[1].Trim().Length > 0)
            {
                int year;
                if (!Int32.TryParse(c[1].Trim(), NumberStyles.None,
                        CultureInfo.InvariantCulture, out year) ||
                    c[1].Trim().Length != 4 || year < 1880 || year > 2100)
                    report.Findings.Add(new Finding(L10n.Get("integrity.cat_fields"), "error",
                        label + L10n.Get("integrity.year_range")));
            }
            if (c.Length > 4 && c[4].Trim().Length > 0)
                CheckNonNegative(report, label, L10n.Get("integrity.field_tank_capacity"), c[4]);
        }
    }

    private static void CheckUtilities(Report report, List<string[]> rows)
    {
        Dictionary<string, bool> months = new Dictionary<string, bool>(StringComparer.Ordinal);
        for (int i = 0; i < rows.Count; i++)
        {
            string[] c = RecordIds.StripId(rows[i]);
            string label = L10n.Get("integrity.utility_prefix") + (i + 1);
            if (c.Length < 1)
            {
                report.Findings.Add(new Finding(L10n.Get("integrity.cat_fields"), "error",
                    label + L10n.Get("integrity.empty_row")));
                continue;
            }
            CheckMonth(report, label, c[0]);
            string month = (c[0] ?? "").Trim();
            if (month.Length > 0)
            {
                if (months.ContainsKey(month))
                    report.Findings.Add(new Finding(L10n.Get("integrity.cat_duplicates"), "error",
                        label + L10n.Get("integrity.month_open") + month + L10n.Get("integrity.duplicate_month_close")));
                else
                    months.Add(month, true);
            }
            bool hasAmount = false;
            for (int j = 1; j < c.Length && j <= 7; j++)
            {
                if (c[j] != null && c[j].Length > 0)
                {
                    hasAmount = true;
                    CheckNonNegative(report, label, L10n.Get("integrity.amount_bracket") + j + "]", c[j]);
                }
            }
            if (!hasAmount)
                report.Findings.Add(new Finding(L10n.Get("integrity.cat_fields"), "error",
                    label + L10n.Get("integrity.no_amount_given")));
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
                report.Findings.Add(new Finding(L10n.Get("integrity.cat_categories"), "info",
                    L10n.Get("integrity.unused_repair_cat_open") + repairCats[i] + L10n.Get("integrity.not_used_suffix")));
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
                report.Findings.Add(new Finding(L10n.Get("integrity.cat_categories"), "info",
                    L10n.Get("integrity.unused_expense_cat_open") + expenseCats[i] + L10n.Get("integrity.not_used_suffix")));
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
        try
        {
            files = Directory.GetFiles(folder, "*.jpg");
        }
        catch
        {
            return;
        }
        for (int i = 0; i < files.Length; i++)
        {
            string name = Path.GetFileNameWithoutExtension(files[i]);
            if (!RecordIds.LooksLikeId(name))
            {
                report.Findings.Add(new Finding(L10n.Get("integrity.cat_receipts"), "warn",
                    (L10n.Get("integrity.bad_receipt_name") + " ") + Path.GetFileName(files[i])));
                continue;
            }
            if (!ids.ContainsKey(name))
            {
                report.Findings.Add(new Finding(L10n.Get("integrity.cat_receipts"), "warn",
                    (L10n.Get("integrity.orphan_receipt_file") + " ") + Path.GetFileName(files[i])));
                continue;
            }
            try
            {
                if (new FileInfo(files[i]).Length <= 0)
                    report.Findings.Add(new Finding(L10n.Get("integrity.cat_receipts"), "warn",
                        (L10n.Get("integrity.empty_receipt_file") + " ") + Path.GetFileName(files[i])));
            }
            catch { }
        }
    }

    private static void CheckDuplicateIds(Report report, List<string[]> rows, string category)
    {
        Dictionary<string, bool> ids = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < rows.Count; i++)
        {
            string id = RecordIds.GetId(rows[i]);
            if (String.IsNullOrEmpty(id))
                continue;
            if (ids.ContainsKey(id))
            {
                report.Findings.Add(new Finding(L10n.Get("integrity.cat_duplicates"), "error",
                    L10n.Get("integrity.duplicate_id_open") + id + (L10n.Get("integrity.in_close") + " ") + category + "."));
                continue;
            }
            ids.Add(id, true);
        }
    }

    private static void CheckTombstones(Report report, string directory,
        List<string[]> fuels, List<string[]> repairs, List<string[]> expenses,
        List<string[]> utilities, Dictionary<string, bool> cars,
        List<string> repairCats, List<string> expenseCats)
    {
        List<Tombstone> tombs = RecordIds.ReadTombstones(directory);
        if (tombs.Count == 0)
            return;
        Dictionary<string, bool> live = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        CollectIds(live, fuels);
        CollectIds(live, repairs);
        CollectIds(live, expenses);
        CollectIds(live, utilities);
        // cars / categories may lack ids — treat as "nothing to match" → info only
        int dangling = 0;
        for (int i = 0; i < tombs.Count; i++)
        {
            if (live.ContainsKey(tombs[i].Id))
                continue;
            dangling++;
        }
        if (dangling > 0)
            report.Findings.Add(new Finding("tombstone", "info",
                (L10n.Get("integrity.dangling_tombstone") + " ") + dangling + (" " + L10n.Get("integrity.tombstone_normal"))));
    }

    private static void CheckDate(Report report, string label, string value)
    {
        value = (value ?? "").Trim();
        if (value.Length == 0)
        {
            report.Findings.Add(new Finding(L10n.Get("integrity.cat_dates"), "error", label + L10n.Get("integrity.empty_date")));
            return;
        }
        DateTime dt;
        if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out dt))
            report.Findings.Add(new Finding(L10n.Get("integrity.cat_dates"), "error",
                label + L10n.Get("integrity.invalid_date_open") + value + L10n.Get("integrity.expected_date_close")));
    }

    private static void CheckMonth(Report report, string label, string value)
    {
        value = (value ?? "").Trim();
        if (value.Length == 0)
        {
            report.Findings.Add(new Finding(L10n.Get("integrity.cat_dates"), "error", label + L10n.Get("integrity.empty_month")));
            return;
        }
        DateTime dt;
        if (!DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out dt) &&
            !DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out dt))
            report.Findings.Add(new Finding(L10n.Get("integrity.cat_dates"), "error",
                label + L10n.Get("integrity.invalid_month_open") + value + "»."));
    }

    private static void CheckRequired(Report report, string label, string field, string value)
    {
        if (value == null || value.Trim().Length == 0)
            report.Findings.Add(new Finding(L10n.Get("integrity.cat_fields"), "error",
                label + L10n.Get("integrity.empty_required_open") + field + "»."));
    }

    private static void CheckNonNegative(Report report, string label, string field, string value)
    {
        value = (value ?? "").Trim();
        if (value.Length == 0)
            return;
        decimal n;
        if (!Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out n))
        {
            report.Findings.Add(new Finding(L10n.Get("integrity.cat_fields"), "error",
                label + ": «" + field + L10n.Get("integrity.not_number_mid") + value + "»)."));
            return;
        }
        if (n < 0)
            report.Findings.Add(new Finding(L10n.Get("integrity.cat_fields"), "error",
                label + L10n.Get("integrity.negative_open") + field + "» (" + value + ")."));
    }

    private static Dictionary<string, bool> LoadCarSet(string directory)
    {
        Dictionary<string, bool> cars = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        try
        {
            List<string> names = ExpenseDataStore.LoadCarNames(directory);
            for (int i = 0; i < names.Count; i++)
            {
                string n = (names[i] ?? "").Trim();
                if (n.Length > 0 && !cars.ContainsKey(n))
                    cars.Add(n, true);
            }
        }
        catch
        {
        }
        return cars;
    }

    private static List<string[]> SafeLoad(string directory, string fileName)
    {
        try
        {
            return ExpenseDataStore.Load(directory, fileName);
        }
        catch
        {
            return new List<string[]>();
        }
    }

    private static List<string[]> SafeLoadOptional(string directory, string fileName)
    {
        try
        {
            return ExpenseDataStore.LoadOptional(directory, fileName);
        }
        catch
        {
            return new List<string[]>();
        }
    }

    private static List<string> ReadCategoryNamesRaw(string directory, string fileName)
    {
        List<string> names = new List<string>();
        string path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
            return names;
        try
        {
            List<string[]> rows = ExpenseDataCodec.ReadRows(path);
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
