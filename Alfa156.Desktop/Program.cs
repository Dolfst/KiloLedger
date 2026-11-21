using System;
using System.Windows.Forms;
using Alfa156.Localization;

namespace Alfa156.Desktop
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            InitializeLanguage();
            Application.Run(new MainForm());
        }

        private static void InitializeLanguage()
        {
            try
            {
                string lastLanguage = Data.ToSettings.GetLanguage(Data.AlfaRepository.ReadSettings(""));
                if (!string.IsNullOrEmpty(lastLanguage))
                    LocalizationManager.SetLanguage(lastLanguage);
            }
            catch
            {
                LocalizationManager.SetLanguage("ru");
            }
        }
    }
}
