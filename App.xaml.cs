using System;
using System.IO;
using System.Windows;

namespace ZeekrTool
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            base.OnStartup(e);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ZeekrTool");

                Directory.CreateDirectory(dir);

                string logPath = Path.Combine(dir, "crash.log");

                File.AppendAllText(
                    logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.ExceptionObject}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
            }
        }
    }
}