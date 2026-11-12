using System;
using System.Windows.Forms;

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
                MessageBox.Show("Не удалось запустить приложение: " + ex.Message, "Ошибка");
            }
        }
    }
}