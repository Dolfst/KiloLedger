using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KiloLedger.Localization
{
    /// <summary>Система локализации для KiloLedger (i18n)</summary>
    public class LocalizationManager
    {
        private static LocalizationManager _instance;
        private Dictionary<string, string> _strings;
        private string _currentLanguage = "ru";
        private string _resourcePath;

        private LocalizationManager(string resourcePath)
        {
            _resourcePath = resourcePath;
            LoadLanguage(_currentLanguage);
        }

        /// <summary>Получить или создать единственный экземпляр LocalizationManager</summary>
        public static LocalizationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    string basePath = AppDomain.CurrentDomain.BaseDirectory;
                    string resourcePath = Path.Combine(basePath, "resources");
                    _instance = new LocalizationManager(resourcePath);
                }
                return _instance;
            }
        }

        /// <summary>Текущий язык (ru, en, es, fr, de)</summary>
        public string CurrentLanguage
        {
            get { return _currentLanguage; }
            set
            {
                string next = string.IsNullOrEmpty(value) ? "ru" : value.Trim().ToLowerInvariant();
                if (_currentLanguage != next)
                {
                    _currentLanguage = next;
                    LoadLanguage(_currentLanguage);
                }
            }
        }

        public static void SetLanguage(string language)
        {
            Instance.CurrentLanguage = language;
        }

        public static string GetCurrentLanguage()
        {
            return Instance.CurrentLanguage;
        }

        public string GetString(string key, string defaultValue)
        {
            if (_strings == null || string.IsNullOrEmpty(key) || !_strings.ContainsKey(key))
            {
                return string.IsNullOrEmpty(defaultValue) ? key : defaultValue;
            }
            return _strings[key];
        }

        public string[] GetAvailableLanguages()
        {
            List<string> languages = new List<string>();
            if (!Directory.Exists(_resourcePath))
                return new string[] { "ru", "en", "es", "fr", "de" };

            string[] order = new string[] { "ru", "en", "es", "fr", "de" };
            HashSet<string> found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in Directory.GetFiles(_resourcePath, "strings.*.json"))
            {
                string filename = Path.GetFileNameWithoutExtension(file);
                if (filename.Length > 8)
                    found.Add(filename.Substring(8).ToLowerInvariant());
            }
            foreach (string code in order)
            {
                if (found.Contains(code))
                {
                    languages.Add(code);
                    found.Remove(code);
                }
            }
            foreach (string extra in found)
                languages.Add(extra);
            return languages.Count > 0 ? languages.ToArray() : order;
        }

        private void LoadLanguage(string language)
        {
            _strings = new Dictionary<string, string>();
            LoadFileIntoMap(Path.Combine(_resourcePath, "strings.ru.json"));
            if (!string.IsNullOrEmpty(language) && !string.Equals(language, "ru", StringComparison.OrdinalIgnoreCase))
            {
                LoadFileIntoMap(Path.Combine(_resourcePath, "strings." + language + ".json"));
            }
        }

        private void LoadFileIntoMap(string filePath)
        {
            if (!File.Exists(filePath))
                return;
            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                ParseJsonStrings(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error loading language file: " + ex.Message);
            }
        }

        private void ParseJsonStrings(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;
            int pos = 0;
            while (pos < json.Length)
            {
                int keyStart = FindNextQuote(json, pos);
                if (keyStart < 0) break;
                string key = ReadJsonString(json, keyStart, out pos);
                if (key == null) break;

                int colonPos = json.IndexOf(':', pos);
                if (colonPos < 0) break;

                int valStart = FindNextQuote(json, colonPos + 1);
                if (valStart < 0) break;
                string val = ReadJsonString(json, valStart, out pos);
                if (val == null) break;

                _strings[key] = val;
            }
        }

        private static int FindNextQuote(string s, int start)
        {
            for (int i = start; i < s.Length; i++)
            {
                if (s[i] == '"') return i;
            }
            return -1;
        }

        private static string ReadJsonString(string s, int quoteStart, out int nextPos)
        {
            StringBuilder sb = new StringBuilder();
            int i = quoteStart + 1;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '"')
                {
                    nextPos = i + 1;
                    return sb.ToString();
                }
                if (c == '\\' && i + 1 < s.Length)
                {
                    char esc = s[i + 1];
                    if (esc == 'n') { sb.Append('\n'); i += 2; continue; }
                    if (esc == 'r') { sb.Append('\r'); i += 2; continue; }
                    if (esc == 't') { sb.Append('\t'); i += 2; continue; }
                    if (esc == '"') { sb.Append('"'); i += 2; continue; }
                    if (esc == '\\') { sb.Append('\\'); i += 2; continue; }
                    if (esc == '/') { sb.Append('/'); i += 2; continue; }
                    if (esc == 'u' && i + 5 < s.Length)
                    {
                        int code;
                        if (int.TryParse(s.Substring(i + 2, 4), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out code))
                        {
                            sb.Append((char)code);
                            i += 6;
                            continue;
                        }
                    }
                    sb.Append(esc);
                    i += 2;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            nextPos = s.Length;
            return null;
        }
    }

    public static class L10n
    {
        public static string Get(string key, string defaultValue = "")
        {
            return LocalizationManager.Instance.GetString(key, defaultValue);
        }

        public static string Format(string key, params object[] args)
        {
            string pattern = Get(key, key);
            if (args == null || args.Length == 0)
                return pattern;
            try { return string.Format(pattern, args); }
            catch { return pattern; }
        }

        public static string GetLanguage()
        {
            return LocalizationManager.Instance.CurrentLanguage;
        }

        public static void SetLanguage(string language)
        {
            LocalizationManager.Instance.CurrentLanguage = language;
        }

        public static string[] GetAvailableLanguages()
        {
            return LocalizationManager.Instance.GetAvailableLanguages();
        }

        public static string GetLanguageDisplayName(string code)
        {
            if (code == "ru") return "Русский";
            if (code == "en") return "English";
            if (code == "es") return "Español";
            if (code == "fr") return "Français";
            if (code == "de") return "Deutsch";
            return code;
        }
    }
}
