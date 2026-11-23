using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace KiloLedger.Localization
{
    /// <summary>Система локализации для KiloLedger (i18n). Строки встроены как ресурсы exe.</summary>
    public class LocalizationManager
    {
        private static LocalizationManager _instance;
        private Dictionary<string, string> _strings;
        private string _currentLanguage = "ru";

        private LocalizationManager()
        {
            LoadLanguage(_currentLanguage);
        }

        public static LocalizationManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new LocalizationManager();
                return _instance;
            }
        }

        public string CurrentLanguage
        {
            get { return _currentLanguage; }
            set
            {
                string next = (value == null || value.Trim().Length == 0)
                    ? "ru"
                    : value.Trim().ToLower(CultureInfo.InvariantCulture);
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
            if (_strings == null || key == null || key.Length == 0 || !_strings.ContainsKey(key))
            {
                return (defaultValue == null || defaultValue.Length == 0) ? (key ?? "") : defaultValue;
            }
            return _strings[key];
        }

        public string[] GetAvailableLanguages()
        {
            List<string> languages = new List<string>();
            string[] order = new string[] { "ru", "en", "es", "fr", "de" };
            List<string> found = new List<string>();
            string[] names = Assembly.GetExecutingAssembly().GetManifestResourceNames();
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                if (name.StartsWith("strings.") && name.EndsWith(".json") && name.Length > 13)
                {
                    string lang = name.Substring(8, name.Length - 8 - 5).ToLower(CultureInfo.InvariantCulture);
                    if (!found.Contains(lang))
                        found.Add(lang);
                }
            }
            for (int i = 0; i < order.Length; i++)
            {
                if (found.Contains(order[i]))
                    languages.Add(order[i]);
            }
            for (int i = 0; i < found.Count; i++)
            {
                if (!languages.Contains(found[i]))
                    languages.Add(found[i]);
            }
            return languages.Count > 0 ? languages.ToArray() : order;
        }

        private void LoadLanguage(string language)
        {
            _strings = new Dictionary<string, string>();
            LoadResourceIntoMap("strings.ru.json");
            if (language != null && language.Length > 0 && !string.Equals(language, "ru", StringComparison.OrdinalIgnoreCase))
            {
                LoadResourceIntoMap("strings." + language + ".json");
            }
        }

        private void LoadResourceIntoMap(string resourceName)
        {
            string json = ReadResource(resourceName);
            if (json == null)
                return;
            try
            {
                ParseJsonStrings(json);
            }
            catch (Exception)
            {
            }
        }

        private static string ReadResource(string resourceName)
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                        return null;
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void ParseJsonStrings(string json)
        {
            if (json == null || json.Length == 0)
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
                        try
                        {
                            int code = int.Parse(s.Substring(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                            sb.Append((char)code);
                            i += 6;
                            continue;
                        }
                        catch (Exception) { }
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
        public static string Get(string key)
        {
            return LocalizationManager.Instance.GetString(key, "");
        }

        public static string Get(string key, string defaultValue)
        {
            return LocalizationManager.Instance.GetString(key, defaultValue);
        }

        public static string Format(string key, params object[] args)
        {
            string pattern = Get(key);
            if (args == null || args.Length == 0)
                return pattern;
            try { return string.Format(pattern, args); }
            catch (Exception) { return pattern; }
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
