using System;
using System.Windows.Forms;

namespace CodePlanner
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            var settings = CodePlanner.Core.GeminiSettings.Load();
            CodePlanner.Core.LocalizationService.CurrentLanguage = settings.Language;

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
