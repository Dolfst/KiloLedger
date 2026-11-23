using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Android.Content;

using Alfa156.Localization;
namespace Alfa156.Android;

internal static class GoogleSyncService
{
    private const string Prefs = "google-sync";

    internal static void SaveSettings(Context context, string scriptUrl, string token)
    {
        context.GetSharedPreferences(Prefs, FileCreationMode.Private)
            .Edit()
            .PutString("scriptUrl", scriptUrl ?? "")
            .PutString("token", token ?? "")
            .Apply();
    }

    internal static void LoadSettings(Context context, out string scriptUrl, out string token)
    {
        var prefs = context.GetSharedPreferences(Prefs, FileCreationMode.Private);
        scriptUrl = prefs.GetString("scriptUrl", "") ?? "";
        token = prefs.GetString("token", "") ?? "";
    }

    internal static async Task<string> SyncAsync(
        Context context,
        string dataDirectory,
        SyncPlanResolver resolvePlan,
        SyncConflictResolver resolveConflict)
    {
        string scriptUrl;
        string token;
        LoadSettings(context, out scriptUrl, out token);
        if (String.IsNullOrWhiteSpace(scriptUrl) || String.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException(L10n.Get("error.specify_url_and_token_first"));

        using HttpClient client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        string pullUrl = AppendQuery(scriptUrl.Trim(), "action=pull&token=" + Uri.EscapeDataString(token));
        string pullJson = await client.GetStringAsync(pullUrl).ConfigureAwait(false);
        if (pullJson.IndexOf("\"ok\":false", StringComparison.OrdinalIgnoreCase) >= 0)
            throw new InvalidDataException(ExtractError(pullJson));

        string staging = Path.Combine(Path.GetTempPath(), "kiloledger-google-" + Guid.NewGuid().ToString("N"));
        string incoming = Path.Combine(staging, "incoming");
        Directory.CreateDirectory(incoming);
        try
        {
            WriteSnapshotFromJson(pullJson, incoming);
            ExpenseDataCodec.ValidateDirectory(incoming);

            SyncPlan defaultPlan = new SyncPlan();
            SyncJournal preview = ExpenseSyncService.BuildPreview(
                dataDirectory, incoming, defaultPlan);
            string safetyPath = ExpenseDataStore.AllocateSafetyBackupPath(dataDirectory);
            preview.SafetyPath = safetyPath;
            SyncPlan plan = resolvePlan(ExpenseSyncService.BuildPreviewText(
                dataDirectory, incoming, "Android", "Google", preview));
            if (plan == null || plan.Cancelled)
                throw new System.OperationCanceledException(L10n.Get("sync.cancelled"));

            ExpenseSyncService.MergeFromIncoming(dataDirectory, incoming, staging, plan,
                resolveConflict, safetyPath);
            ExpenseDataCodec.ValidateDirectory(dataDirectory);

            string pushBody = BuildPushJson(token, dataDirectory);
            using StringContent content = new StringContent(pushBody, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await client.PostAsync(scriptUrl.Trim(), content).ConfigureAwait(false);
            string pushJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode ||
                pushJson.IndexOf("\"ok\":true", StringComparison.OrdinalIgnoreCase) < 0)
            {
                string reason = (L10n.Get("error.google_write_failed") + " ") + ExtractError(pushJson);
                if (ExpenseSyncService.LastJournal != null)
                    ExpenseSyncService.LastJournal.RollbackReason = reason;
                throw new InvalidDataException(reason);
            }

            SyncJournal journal = ExpenseSyncService.LastJournal;
            string message = L10n.Get("msg.google_sync_complete");
            if (journal != null)
                message += "\n\n" + journal.FormatSummary();
            message += ("\n\n" + L10n.Get("google.natural_keys_note"));
            return message;
        }
        finally
        {
            TryDelete(staging);
        }
    }

    private static void WriteSnapshotFromJson(string json, string directory)
    {
        WriteTable(directory, "cars.dat", ExtractTable(json, "cars"), 1);
        WriteFuelTable(directory, ExtractTable(json, "fuel"));
        WriteTable(directory, "repairs.dat", ExtractTable(json, "repairs"), 9);
        WriteTable(directory, "utilities.dat", ExtractTable(json, "utilities"), 8);
        WriteTable(directory, ExpenseDataCodec.RepairCategoriesFile, ExtractTable(json, "categories"), 1);
        WriteTable(directory, ExpenseDataCodec.ExpensesFile, ExtractTable(json, "expenses"), 6);
        WriteTable(directory, ExpenseDataCodec.ExpenseCategoriesFile,
            ExtractTable(json, "expenseCategories"), 1);
    }

    private static void WriteTable(string directory, string fileName, List<string[]> rows, int columns)
    {
        List<string[]> normalized = new List<string[]>();
        for (int i = 0; i < rows.Count; i++)
        {
            string[] row = new string[columns];
            for (int c = 0; c < columns; c++)
                row[c] = c < rows[i].Length ? (rows[i][c] ?? "") : "";
            normalized.Add(row);
        }
        if (fileName == "cars.dat" && normalized.Count == 0)
            normalized.Add(new string[] { L10n.Get("label.default_car_name") });
        ExpenseDataCodec.SaveRows(Path.Combine(directory, fileName), normalized);
    }

    private static void WriteFuelTable(string directory, List<string[]> rows)
    {
        List<string[]> normalized = new List<string[]>();
        for (int i = 0; i < rows.Count; i++)
        {
            int length = rows[i].Length;
            if (length < 5)
                length = 5;
            if (length > 7)
                length = 7;
            while (length > 5)
            {
                string trailing = length - 1 < rows[i].Length ? (rows[i][length - 1] ?? "") : "";
                if (trailing.Trim().Length > 0)
                    break;
                length--;
            }
            string[] row = new string[length];
            for (int c = 0; c < length; c++)
                row[c] = c < rows[i].Length ? (rows[i][c] ?? "") : "";
            normalized.Add(row);
        }
        ExpenseDataCodec.SaveRows(Path.Combine(directory, "fuel.dat"), normalized);
    }

    private static string BuildPushJson(string token, string dataDirectory)
    {
        // Strip trailing record ids so Dengi sheet columns stay unchanged.
        StringBuilder sb = new StringBuilder();
        sb.Append("{\"token\":").Append(Quote(token)).Append(',');
        sb.Append("\"cars\":").Append(TableJson(StripIds(
            ExpenseDataCodec.ReadRows(Path.Combine(dataDirectory, "cars.dat"))))).Append(',');
        sb.Append("\"fuel\":").Append(TableJson(StripIds(
            ExpenseDataCodec.ReadRows(Path.Combine(dataDirectory, "fuel.dat"))))).Append(',');
        sb.Append("\"repairs\":").Append(TableJson(StripIds(
            ExpenseDataCodec.ReadRows(Path.Combine(dataDirectory, "repairs.dat"))))).Append(',');
        sb.Append("\"utilities\":").Append(TableJson(StripIds(
            ExpenseDataCodec.ReadRows(Path.Combine(dataDirectory, "utilities.dat"))))).Append(',');
        string categoriesPath = Path.Combine(dataDirectory, ExpenseDataCodec.RepairCategoriesFile);
        List<string[]> categories = File.Exists(categoriesPath)
            ? ExpenseDataCodec.ReadRows(categoriesPath)
            : new List<string[]>();
        sb.Append("\"categories\":").Append(TableJson(StripIds(categories))).Append(',');
        string expensesPath = Path.Combine(dataDirectory, ExpenseDataCodec.ExpensesFile);
        List<string[]> expenses = File.Exists(expensesPath)
            ? ExpenseDataCodec.ReadRows(expensesPath)
            : new List<string[]>();
        sb.Append("\"expenses\":").Append(TableJson(StripIds(expenses))).Append(',');
        string expenseCategoriesPath = Path.Combine(dataDirectory, ExpenseDataCodec.ExpenseCategoriesFile);
        List<string[]> expenseCategories = File.Exists(expenseCategoriesPath)
            ? ExpenseDataCodec.ReadRows(expenseCategoriesPath)
            : new List<string[]>();
        sb.Append("\"expenseCategories\":").Append(TableJson(StripIds(expenseCategories)));
        sb.Append('}');
        return sb.ToString();
    }

    private static List<string[]> StripIds(List<string[]> rows)
    {
        List<string[]> result = new List<string[]>();
        for (int i = 0; i < rows.Count; i++)
            result.Add(RecordIds.StripId(rows[i]));
        return result;
    }

    private static string TableJson(List<string[]> rows)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('[');
            for (int c = 0; c < rows[i].Length; c++)
            {
                if (c > 0) sb.Append(',');
                sb.Append(Quote(rows[i][c] ?? ""));
            }
            sb.Append(']');
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static List<string[]> ExtractTable(string json, string key)
    {
        // Prefer data.* then root.*
        string data = ExtractObject(json, "data") ?? json;
        return ParseTable(data, key);
    }

    private static List<string[]> ParseTable(string json, string key)
    {
        List<string[]> rows = new List<string[]>();
        string marker = "\"" + key + "\"";
        int index = json.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0) return rows;
        int colon = json.IndexOf(':', index + marker.Length);
        int start = colon + 1;
        while (start < json.Length && Char.IsWhiteSpace(json[start])) start++;
        if (start >= json.Length || json[start] != '[') return rows;
        int end = FindMatching(json, start, '[', ']');
        if (end < 0) return rows;
        string body = json.Substring(start + 1, end - start - 1);
        int p = 0;
        while (p < body.Length)
        {
            while (p < body.Length && (Char.IsWhiteSpace(body[p]) || body[p] == ',')) p++;
            if (p >= body.Length) break;
            if (body[p] != '[') break;
            int rowEnd = FindMatching(body, p, '[', ']');
            if (rowEnd < 0) break;
            rows.Add(ParseRow(body.Substring(p + 1, rowEnd - p - 1)));
            p = rowEnd + 1;
        }
        return rows;
    }

    private static string[] ParseRow(string rowText)
    {
        List<string> fields = new List<string>();
        int i = 0;
        while (i < rowText.Length)
        {
            while (i < rowText.Length && (Char.IsWhiteSpace(rowText[i]) || rowText[i] == ',')) i++;
            if (i >= rowText.Length) break;
            if (rowText[i] == '"')
            {
                i++;
                StringBuilder sb = new StringBuilder();
                while (i < rowText.Length)
                {
                    char c = rowText[i];
                    if (c == '\\' && i + 1 < rowText.Length) { sb.Append(rowText[i + 1]); i += 2; continue; }
                    if (c == '"') { i++; break; }
                    sb.Append(c); i++;
                }
                fields.Add(sb.ToString());
            }
            else
            {
                int start = i;
                while (i < rowText.Length && rowText[i] != ',') i++;
                fields.Add(rowText.Substring(start, i - start).Trim());
            }
        }
        return fields.ToArray();
    }

    private static string ExtractObject(string json, string key)
    {
        string marker = "\"" + key + "\"";
        int index = json.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0) return null;
        int colon = json.IndexOf(':', index + marker.Length);
        int start = colon + 1;
        while (start < json.Length && Char.IsWhiteSpace(json[start])) start++;
        if (start >= json.Length || json[start] != '{') return null;
        int end = FindMatching(json, start, '{', '}');
        return end < 0 ? null : json.Substring(start, end - start + 1);
    }

    private static int FindMatching(string text, int openIndex, char open, char close)
    {
        int depth = 0;
        for (int p = openIndex; p < text.Length; p++)
        {
            char c = text[p];
            if (c == '"')
            {
                p++;
                while (p < text.Length)
                {
                    if (text[p] == '\\') { p += 2; continue; }
                    if (text[p] == '"') break;
                    p++;
                }
                continue;
            }
            if (c == open) depth++;
            else if (c == close)
            {
                depth--;
                if (depth == 0) return p;
            }
        }
        return -1;
    }

    private static string ExtractError(string json)
    {
        string marker = "\"error\"";
        int index = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return json;
        int colon = json.IndexOf(':', index);
        int q = json.IndexOf('"', colon + 1);
        if (q < 0) return json;
        int q2 = json.IndexOf('"', q + 1);
        if (q2 < 0) return json;
        return json.Substring(q + 1, q2 - q - 1);
    }

    private static string AppendQuery(string url, string query)
    {
        return url.IndexOf('?', StringComparison.Ordinal) >= 0 ? url + "&" + query : url + "?" + query;
    }

    private static string Quote(string value)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append('"');
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '"' || c == '\\') sb.Append('\\').Append(c);
            else if (c == '\n') sb.Append("\\n");
            else if (c == '\r') sb.Append("\\r");
            else sb.Append(c);
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
