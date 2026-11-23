using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Alfa156.Desktop.Data;

using Alfa156.Localization;
namespace Alfa156.Desktop.Exchange
{
    internal sealed class GoogleSyncSettings
    {
        public string ScriptUrl { get; set; }
        public string Token { get; set; }
        public string SpreadsheetUrl { get; set; }

        public static string ConfigPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "KiloLedger", "google-sync.json");
            }
        }

        public static GoogleSyncSettings Load()
        {
            string path = ConfigPath;
            GoogleSyncSettings settings = new GoogleSyncSettings();
            if (!File.Exists(path))
                return settings;
            string json = File.ReadAllText(path, Encoding.UTF8);
            settings.ScriptUrl = JsonUtil.GetString(json, "scriptUrl");
            settings.Token = JsonUtil.GetString(json, "token");
            settings.SpreadsheetUrl = JsonUtil.GetString(json, "spreadsheetUrl");
            return settings;
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"scriptUrl\":").Append(JsonUtil.Quote(ScriptUrl ?? "")).Append(",");
            sb.Append("\"token\":").Append(JsonUtil.Quote(Token ?? "")).Append(",");
            sb.Append("\"spreadsheetUrl\":").Append(JsonUtil.Quote(SpreadsheetUrl ?? ""));
            sb.Append("}");
            File.WriteAllText(ConfigPath, sb.ToString(), new UTF8Encoding(false));
        }
    }

    internal static class GoogleSyncClient
    {
        internal static AlfaRepository PullRemote(GoogleSyncSettings settings)
        {
            return Pull(settings);
        }

        /// <summary>
        /// Проверка связи: pull snapshot и краткая сводка по таблицам.
        /// </summary>
        internal static string TestConnection(GoogleSyncSettings settings)
        {
            AlfaRepository remote = Pull(settings);
            return (L10n.Get("msg.connection_ok") + "\r\n") +
                (L10n.Get("label.cars_colon") + " ") + remote.Cars.Count + "\r\n" +
                (L10n.Get("summary.fuel_section_colon") + " ") + remote.Fuel.Count + "\r\n" +
                (L10n.Get("label.repairs_colon") + " ") + remote.Repairs.Count + "\r\n" +
                (L10n.Get("label.expenses_colon") + " ") + remote.Expenses.Count + "\r\n" +
                (L10n.Get("label.utilities_short_colon") + " ") + remote.Utilities.Count + "\r\n" +
                (L10n.Get("label.categories_colon") + " ") + remote.Categories.Count + "\r\n" +
                (L10n.Get("label.expense_cat_colon") + " ") + remote.ExpenseCategories.Count;
        }

        internal static string ApplyAndPush(
            AlfaRepository local,
            AlfaRepository remote,
            GoogleSyncSettings settings,
            SyncPlan plan)
        {
            SyncJournal journal = SyncMerge.ApplyPlan(local, remote, plan);
            CleanCurrencyInPlace(local);
            try
            {
                Push(settings, local);
            }
            catch (Exception pushError)
            {
                if (SyncMerge.LastJournal != null)
                    SyncMerge.LastJournal.RollbackReason =
                        (L10n.Get("error.google_write_failed") + " ") + pushError.Message;
                throw;
            }
            string counts = L10n.Get("msg.google_sync_stats_cars") + local.Cars.Count +
                L10n.Get("sync.comma_fuel_eq") + local.Fuel.Count +
                L10n.Get("sync.comma_repairs_eq") + local.Repairs.Count +
                L10n.Get("sync.comma_expenses_eq") + local.Expenses.Count +
                L10n.Get("sync.comma_util_eq") + local.Utilities.Count +
                (" " + L10n.Get("summary.from_google_repair") + " ") + remote.Repairs.Count +
                (L10n.Get("summary.comma_fuel") + " ") + remote.Fuel.Count +
                (L10n.Get("summary.comma_expenses") + " ") + remote.Expenses.Count + ")";
            if (journal != null)
                return counts + "\r\n\r\n" + journal.FormatSummary() +
                    ("\r\n\r\n" + L10n.Get("google.natural_keys_remark") + " ") +
                    L10n.Get("google.ids_stay_local");
            return counts;
        }

        internal static string SyncBidirectional(AlfaRepository local, GoogleSyncSettings settings)
        {
            AlfaRepository remote = Pull(settings);
            SyncPlan plan = new SyncPlan
            {
                AddFromGoogle = true,
                RemoveMissingInGoogle = false,
                ConflictPolicy = SyncConflictPolicy.PreferLocal
            };
            return ApplyAndPush(local, remote, settings, plan);
        }

        private static void CleanCurrencyInPlace(AlfaRepository repo)
        {
            for (int i = 0; i < repo.Fuel.Count; i++)
            {
                string id = RecordIds.GetId(repo.Fuel[i]);
                string[] content = RecordIds.StripId(repo.Fuel[i]);
                if (content.Length > 4)
                {
                    content[2] = StripMoney(content[2]);
                    content[3] = StripMoney(content[3]);
                    content[4] = StripMoney(content[4]);
                    if (content.Length > 5)
                        content[5] = StripMoney(content[5]);
                }
                repo.Fuel[i] = id != null ? RecordIds.WithId(content, id) : content;
            }
            for (int i = 0; i < repo.Repairs.Count; i++)
            {
                string id = RecordIds.GetId(repo.Repairs[i]);
                string[] content = RecordIds.StripId(repo.Repairs[i]);
                if (content.Length > 7)
                {
                    content[2] = StripMoney(content[2]);
                    content[6] = StripMoney(content[6]);
                    content[7] = StripMoney(content[7]);
                }
                repo.Repairs[i] = id != null ? RecordIds.WithId(content, id) : content;
            }
            for (int i = 0; i < repo.Utilities.Count; i++)
            {
                string id = RecordIds.GetId(repo.Utilities[i]);
                string[] content = RecordIds.StripId(repo.Utilities[i]);
                for (int c = 1; c < content.Length && c < 8; c++)
                    content[c] = StripMoney(content[c]);
                repo.Utilities[i] = id != null ? RecordIds.WithId(content, id) : content;
            }
            for (int i = 0; i < repo.Expenses.Count; i++)
            {
                string id = RecordIds.GetId(repo.Expenses[i]);
                string[] content = RecordIds.StripId(repo.Expenses[i]);
                if (content.Length > 4)
                    content[4] = StripMoney(content[4]);
                repo.Expenses[i] = id != null ? RecordIds.WithId(content, id) : content;
            }
        }

        private static string StripMoney(string text)
        {
            if (String.IsNullOrEmpty(text)) return "";
            string s = text.Trim().Replace(" ", "").Replace("₽", "");
            s = ReplaceIgnoreCase(s, L10n.Get("currency.r_dot"), "");
            s = ReplaceIgnoreCase(s, L10n.Get("currency.rub_dot"), "");
            s = ReplaceIgnoreCase(s, L10n.Get("currency.rub"), "");
            s = ReplaceIgnoreCase(s, "BYN", "");
            s = ReplaceIgnoreCase(s, "RUB", "");
            s = ReplaceIgnoreCase(s, "USD", "");
            s = ReplaceIgnoreCase(s, "EUR", "");
            decimal value;
            if (AlfaCodec.TryNumber(s, out value))
                return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return s;
        }

        private static string ReplaceIgnoreCase(string text, string oldValue, string newValue)
        {
            if (String.IsNullOrEmpty(text) || String.IsNullOrEmpty(oldValue)) return text;
            int index;
            while ((index = text.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase)) >= 0)
                text = text.Substring(0, index) + newValue + text.Substring(index + oldValue.Length);
            return text;
        }

        internal static AlfaRepository Pull(GoogleSyncSettings settings)
        {
            EnsureSettings(settings);
            string url = AppendQuery(settings.ScriptUrl.Trim(),
                "action=pull&token=" + Uri.EscapeDataString(settings.Token ?? ""));
            string json = DownloadText(url);
            return ParseSnapshot(json);
        }

        internal static void Push(GoogleSyncSettings settings, AlfaRepository repo)
        {
            EnsureSettings(settings);
            string body = BuildPushJson(settings.Token, repo);
            string response = PostJson(settings.ScriptUrl.Trim(), body);
            if (JsonUtil.GetBool(response, "ok") != true)
            {
                string error = JsonUtil.GetString(response, "error");
                throw new InvalidDataException((L10n.Get("error.google_rejected_write") + " ") +
                    (String.IsNullOrEmpty(error) ? response : error));
            }
        }

        private static void EnsureSettings(GoogleSyncSettings settings)
        {
            if (settings == null || String.IsNullOrEmpty(settings.ScriptUrl))
                throw new InvalidOperationException(
                    L10n.Get("error.apps_script_url_not_set"));
            if (String.IsNullOrEmpty(settings.Token))
                throw new InvalidOperationException(L10n.Get("error.sync_token_not_set"));
        }

        private static AlfaRepository ParseSnapshot(string json)
        {
            if (JsonUtil.GetBool(json, "ok") == false)
                throw new InvalidDataException(JsonUtil.GetString(json, "error") ?? L10n.Get("title.google_sync_error"));

            string data = JsonUtil.GetObjectJson(json, "data") ?? json;
            AlfaRepository repo = new AlfaRepository();
            repo.ReplaceAll(
                JsonUtil.GetTable(data, "cars"),
                JsonUtil.GetTable(data, "fuel"),
                JsonUtil.GetTable(data, "repairs"),
                JsonUtil.GetTable(data, "utilities"),
                JsonUtil.GetTable(data, "categories"),
                JsonUtil.GetTable(data, "expenses"),
                JsonUtil.GetTable(data, "expenseCategories"),
                null);
            if (repo.Cars.Count == 0)
                throw new InvalidDataException(L10n.Get("error.google_empty_cars"));
            return repo;
        }

        private static string BuildPushJson(string token, AlfaRepository repo)
        {
            // Google/Dengi layout stays key-based; do not push trailing record ids.
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"token\":").Append(JsonUtil.Quote(token ?? "")).Append(",");
            sb.Append("\"cars\":").Append(JsonUtil.Table(StripIds(repo.Cars))).Append(",");
            sb.Append("\"fuel\":").Append(JsonUtil.Table(StripIds(repo.Fuel))).Append(",");
            sb.Append("\"repairs\":").Append(JsonUtil.Table(StripIds(repo.Repairs))).Append(",");
            sb.Append("\"utilities\":").Append(JsonUtil.Table(StripIds(repo.Utilities))).Append(",");
            sb.Append("\"categories\":").Append(JsonUtil.Table(StripIds(repo.Categories))).Append(",");
            sb.Append("\"expenses\":").Append(JsonUtil.Table(StripIds(repo.Expenses))).Append(",");
            sb.Append("\"expenseCategories\":").Append(JsonUtil.Table(StripIds(repo.ExpenseCategories)));
            sb.Append("}");
            return sb.ToString();
        }

        private static List<string[]> StripIds(List<string[]> rows)
        {
            List<string[]> result = new List<string[]>();
            for (int i = 0; i < rows.Count; i++)
                result.Add(RecordIds.StripId(rows[i]));
            return result;
        }

        private static string AppendQuery(string url, string query)
        {
            return url.IndexOf('?') >= 0 ? url + "&" + query : url + "?" + query;
        }

        private static string DownloadText(string url)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent = "KiloLedgerDesktop/1.0";
            request.AllowAutoRedirect = true;
            request.Timeout = 60000;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private static string PostJson(string url, string json)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";
            request.UserAgent = "KiloLedgerDesktop/1.0";
            request.AllowAutoRedirect = true;
            request.Timeout = 120000;
            request.ContentLength = bytes.Length;
            using (Stream stream = request.GetRequestStream())
                stream.Write(bytes, 0, bytes.Length);
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                string text = reader.ReadToEnd();
                if (LooksLikeHtmlError(text))
                    throw new InvalidDataException(
                        (L10n.Get("error.google_html_instead_of_json") + " ") +
                        (L10n.Get("error.google_validation_hint") + " ") +
                        ExtractHtmlHint(text));
                return text;
            }
        }

        private static bool LooksLikeHtmlError(string text)
        {
            if (String.IsNullOrEmpty(text)) return false;
            string t = text.TrimStart();
            return t.StartsWith("<", StringComparison.Ordinal) ||
                t.IndexOf("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                t.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ExtractHtmlHint(string html)
        {
            if (String.IsNullOrEmpty(html)) return "";
            int idx = html.IndexOf("проверк", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) idx = html.IndexOf("validation", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) idx = html.IndexOf("Exception", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return "";
            int start = Math.Max(0, idx - 40);
            int len = Math.Min(180, html.Length - start);
            string snippet = html.Substring(start, len).Replace('\n', ' ').Replace('\r', ' ');
            return (" " + L10n.Get("timeline.excerpt_colon") + " ") + snippet;
        }
    }

    internal static class JsonUtil
    {
        internal static string Quote(string value)
        {
            if (value == null) value = "";
            StringBuilder sb = new StringBuilder();
            sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }

        internal static string Table(List<string[]> rows)
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

        internal static string GetString(string json, string key)
        {
            string pattern = "\"" + key + "\"";
            int index = IndexOfKey(json, pattern);
            if (index < 0) return null;
            int colon = json.IndexOf(':', index + pattern.Length);
            if (colon < 0) return null;
            int i = SkipWs(json, colon + 1);
            if (i >= json.Length) return null;
            if (json[i] == '"')
                return ReadString(json, i);
            int end = i;
            while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != ']')
                end++;
            return json.Substring(i, end - i).Trim().Trim('"');
        }

        internal static bool? GetBool(string json, string key)
        {
            string value = GetString(json, key);
            if (value == null) return null;
            if (String.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) return false;
            return null;
        }

        internal static string GetObjectJson(string json, string key)
        {
            string pattern = "\"" + key + "\"";
            int index = IndexOfKey(json, pattern);
            if (index < 0) return null;
            int colon = json.IndexOf(':', index + pattern.Length);
            int i = SkipWs(json, colon + 1);
            if (i >= json.Length || json[i] != '{') return null;
            int depth = 0;
            for (int p = i; p < json.Length; p++)
            {
                char c = json[p];
                if (c == '"')
                {
                    p++;
                    while (p < json.Length)
                    {
                        if (json[p] == '\\') { p += 2; continue; }
                        if (json[p] == '"') break;
                        p++;
                    }
                    continue;
                }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return json.Substring(i, p - i + 1);
                }
            }
            return null;
        }

        internal static List<string[]> GetTable(string json, string key)
        {
            return ParseTableFallback(json, key);
        }

        private static List<string[]> ParseTableFallback(string json, string key)
        {
            List<string[]> rows = new List<string[]>();
            string pattern = "\"" + key + "\"";
            int index = IndexOfKey(json, pattern);
            if (index < 0) return rows;
            int colon = json.IndexOf(':', index + pattern.Length);
            int i = SkipWs(json, colon + 1);
            if (i >= json.Length || json[i] != '[') return rows;
            int end = FindMatchingBracket(json, i);
            if (end < 0) return rows;
            string body = json.Substring(i + 1, end - i - 1);
            int p = 0;
            while (p < body.Length)
            {
                p = SkipWs(body, p);
                if (p >= body.Length) break;
                if (body[p] == ',') { p++; continue; }
                if (body[p] != '[') break;
                int rowEnd = FindMatchingBracket(body, p);
                if (rowEnd < 0) break;
                string rowText = body.Substring(p + 1, rowEnd - p - 1);
                rows.Add(ParseRow(rowText));
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
                i = SkipWs(rowText, i);
                if (i >= rowText.Length) break;
                if (rowText[i] == ',') { i++; continue; }
                if (rowText[i] == '"')
                {
                    string value;
                    i = ReadStringAt(rowText, i, out value);
                    fields.Add(value);
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

        private static int FindMatchingBracket(string text, int openIndex)
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
                if (c == '[') depth++;
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0) return p;
                }
            }
            return -1;
        }

        private static int IndexOfKey(string json, string pattern)
        {
            int index = 0;
            while (true)
            {
                index = json.IndexOf(pattern, index, StringComparison.Ordinal);
                if (index < 0) return -1;
                bool leftOk = index == 0 || json[index - 1] == '{' || json[index - 1] == ',' ||
                    Char.IsWhiteSpace(json[index - 1]);
                if (leftOk) return index;
                index += pattern.Length;
            }
        }

        private static int SkipWs(string text, int index)
        {
            while (index < text.Length && Char.IsWhiteSpace(text[index])) index++;
            return index;
        }

        private static string ReadString(string json, int quoteIndex)
        {
            string value;
            ReadStringAt(json, quoteIndex, out value);
            return value;
        }

        private static int ReadStringAt(string json, int quoteIndex, out string value)
        {
            StringBuilder sb = new StringBuilder();
            int i = quoteIndex + 1;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char n = json[i + 1];
                    if (n == 'n') sb.Append('\n');
                    else if (n == 'r') sb.Append('\r');
                    else if (n == 't') sb.Append('\t');
                    else sb.Append(n);
                    i += 2;
                    continue;
                }
                if (c == '"')
                {
                    value = sb.ToString();
                    return i + 1;
                }
                sb.Append(c);
                i++;
            }
            value = sb.ToString();
            return i;
        }
    }
}
