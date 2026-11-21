using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Alfa156.Localization
{
    /// <summary>Система локализации для Alfa 156 (i18n). Строки встроены как ресурсы exe.</summary>
    public class LocalizationManager
    {
        private static LocalizationManager _instance;
        private Dictionary<string, string> _strings;
        private string _currentLanguage = "ru";

        private LocalizationManager()
        {
            LoadLanguage(_currentLanguage);
        }

        /// <summary>Получить или создать единственный экземпляр LocalizationManager</summary>
        public static LocalizationManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new LocalizationManager();
                return _instance;
            }
        }

        /// <summary>Текущий язык (ru, en, es, fr, de)</summary>
        public string CurrentLanguage
        {
            get { return _currentLanguage; }
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    LoadLanguage(_currentLanguage);
                }
            }
        }

        /// <summary>Удобный статический доступ для смены языка из UI кода</summary>
        public static void SetLanguage(string language)
        {
            Instance.CurrentLanguage = language;
        }

        /// <summary>Удобный статический доступ к текущему языку</summary>
        public static string GetCurrentLanguage()
        {
            return Instance.CurrentLanguage;
        }

        /// <summary>Получить переведенную строку по ключу</summary>
        public string GetString(string key, string defaultValue)
        {
            if (_strings == null || !_strings.ContainsKey(key))
            {
                return defaultValue.Length == 0 ? key : defaultValue;
            }
            return _strings[key];
        }

        /// <summary>Получить все доступные языки (встроенные ресурсы exe)</summary>
        public string[] GetAvailableLanguages()
        {
            List<string> languages = new List<string>();
            string[] names = Assembly.GetExecutingAssembly().GetManifestResourceNames();
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                if (name.StartsWith("strings.") && name.EndsWith(".json"))
                {
                    string lang = name.Substring(8, name.Length - 8 - 5); // убрать "strings." и ".json"
                    languages.Add(lang);
                }
            }
            return languages.ToArray();
        }

        /// <summary>Загрузить языковой пакет из встроенного ресурса exe</summary>
        private void LoadLanguage(string language)
        {
            _strings = new Dictionary<string, string>();

            string json = ReadResource("strings." + language + ".json");
            if (json == null)
                json = ReadResource("strings.ru.json"); // Fallback на русский

            if (json == null)
                return;

            try
            {
                ParseJsonStrings(json);
            }
            catch (Exception)
            {
                // не ломаем запуск приложения при повреждённых переводах
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

        /// <summary>Простой парсер JSON для строк (без внешних зависимостей)</summary>
        private void ParseJsonStrings(string json)
        {
            // Простой парсер для { "key": "value" }
            int pos = 0;
            while (pos < json.Length)
            {
                int keyStart = json.IndexOf("\"", pos);
                if (keyStart < 0) break;

                int keyEnd = json.IndexOf("\"", keyStart + 1);
                if (keyEnd < 0) break;

                string key = json.Substring(keyStart + 1, keyEnd - keyStart - 1);

                int colonPos = json.IndexOf(":", keyEnd);
                if (colonPos < 0) break;

                int valueStart = json.IndexOf("\"", colonPos);
                if (valueStart < 0) break;

                int valueEnd = valueStart + 1;
                bool escaped = false;
                while (valueEnd < json.Length)
                {
                    if (json[valueEnd] == '\\' && !escaped)
                    {
                        escaped = true;
                    }
                    else if (json[valueEnd] == '"' && !escaped)
                    {
                        break;
                    }
                    else
                    {
                        escaped = false;
                    }
                    valueEnd++;
                }

                if (valueEnd < json.Length)
                {
                    string value = json.Substring(valueStart + 1, valueEnd - valueStart - 1);
                    // Обработать экранированные символы
                    value = value.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\\"", "\"").Replace("\\\\", "\\");
                    _strings[key] = value;
                    pos = valueEnd + 1;
                }
                else
                {
                    break;
                }
            }
        }
    }

    /// <summary>Удобные методы доступа к строкам локализации</summary>
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
    }
}
