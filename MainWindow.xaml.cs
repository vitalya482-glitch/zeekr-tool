using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ZeekrTool
{
    public partial class MainWindow : Window
    {
        private string selectedApk = "";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Log(string text)
        {
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
            LogBox.ScrollToEnd();
        }

        private void CheckAdb_Click(object sender, RoutedEventArgs e)
        {
            Log("Проверка ADB...");
            RunCommand("adb", "devices");
        }

        private void SelectApk_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "APK files (*.apk)|*.apk"
            };

            if (dialog.ShowDialog() == true)
            {
                selectedApk = dialog.FileName;
                SelectedFileText.Text = "Выбран APK: " + selectedApk;
                Log("Выбран файл: " + selectedApk);
            }
        }

        private void InstallApk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedApk))
            {
                Log("Сначала выбери APK-файл.");
                return;
            }

            if (!File.Exists(selectedApk))
            {
                Log("Файл не найден.");
                return;
            }

            Log("Установка APK...");
            RunCommand("adb", $"install -r \"{selectedApk}\"");
        }

        private void RunCommand(string file, string args)
        {
            try
            {
                var process = new Process();
                process.StartInfo.FileName = file;
                process.StartInfo.Arguments = args;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(output))
                    Log(output.Trim());

                if (!string.IsNullOrWhiteSpace(error))
                    Log("ERROR: " + error.Trim());
            }
            catch (Exception ex)
            {
                Log("Ошибка запуска команды: " + ex.Message);
            }
        }
    }
}
