using System;
using System.IO;
using System.Windows;

namespace ZeekrTool
{
    // ZEEKR_TOOL_MARKER: APP_ENTRY_POINT
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                WriteCrashLog(args.ExceptionObject as Exception);

            DispatcherUnhandledException += (_, args) =>
            {
                WriteCrashLog(args.Exception);
                MessageBox.Show(
                    "Произошла ошибка запуска. Подробности сохранены в crash.log",
                    "Zeekr Tool",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            base.OnStartup(e);
        }

        private static void WriteCrashLog(Exception? ex)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ZeekrTool");
                Directory.CreateDirectory(dir);

                string path = Path.Combine(dir, "crash.log");
                File.AppendAllText(
                    path,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}

");
            }
            catch
            {
                // Не даём логированию уронить приложение.
            }
        }
    }
}
