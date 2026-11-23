using System;
using System.Windows.Forms;

using KiloLedger.Localization;
namespace CarExpenses
{
    static class Program
    {
        /// <summary>
        /// Главная точка входа для приложения.
        /// </summary>
        [MTAThread]
        static void Main()
        {
            try
            {
                Application.Run(new Form1());
            }
            catch (System.Exception ex)
            {
                MessageBox.Show((L10n.Get("error.start_app_failed") + " ") + ex.Message, L10n.Get("title.error"));
            }
        }
    }
}