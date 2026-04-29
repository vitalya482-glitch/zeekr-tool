using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        private string GetAdbPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string adbPath = Path.Combine(baseDir, "adb", "adb.exe");

            if (File.Exists(adbPath))
                return adbPath;

            return "adb";
        }

        private void Log(string text)
        {
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
            LogBox.ScrollToEnd();
        }

        private void CheckAdb_Click(object sender, RoutedEventArgs e)
        {
            DetectDevice();
        }

        private void DetectDevice()
        {
            Log("Проверка ADB...");

            string devicesOutput = RunCommandWithResult(GetAdbPath(), "devices");

            if (string.IsNullOrWhiteSpace(devicesOutput))
            {
                SetDeviceDisconnected("ADB не ответил");
                return;
            }

            Log(devicesOutput.Trim());

            var deviceLine = devicesOutput
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .FirstOrDefault(line => line.Trim().EndsWith("device"));

            if (deviceLine == null)
            {
                SetDeviceDisconnected("Устройство не найдено");
                return;
            }

            string deviceId = deviceLine.Split('\t')[0].Trim();

            string model = RunAdbShell("getprop ro.product.model").Trim();
            string android = RunAdbShell("getprop ro.build.version.release").Trim();
            string abi = RunAdbShell("getprop ro.product.cpu.abilist").Trim();

            if (string.IsNullOrWhiteSpace(model))
                model = "Android device";

            if (string.IsNullOrWhiteSpace(android))
                android = "Не определено";

            if (string.IsNullOrWhiteSpace(abi))
                abi = RunAdbShell("getprop ro.product.cpu.abi").Trim();

            DeviceStatusText.Text = "✓ Устройство подключено";
            DeviceStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;

            DeviceSubStatusText.Text = deviceId;
            DeviceModelText.Text = model;
            AndroidVersionText.Text = "Android " + android;
            CpuAbiText.Text = abi;
            AdbStatusText.Text = "ADB подключен";

            Log("Устройство найдено: " + deviceId);
            Log("Модель: " + model);
            Log("Android: " + android);
            Log("CPU ABI: " + abi);
        }

        private void SetDeviceDisconnected(string reason)
        {
            DeviceStatusText.Text = "× Устройство не подключено";
            DeviceStatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;

            DeviceSubStatusText.Text = reason;
            DeviceModelText.Text = "Не определено";
            AndroidVersionText.Text = "Не определено";
            CpuAbiText.Text = "Не определено";
            AdbStatusText.Text = "Нет подключения";

            Log(reason);
        }

        private string RunAdbShell(string command)
        {
            return RunCommandWithResult(GetAdbPath(), $"shell {command}");
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
            RunCommand(GetAdbPath(), $"install -r \"{selectedApk}\"");
        }

        private void RunCommand(string file, string args)
        {
            string result = RunCommandWithResult(file, args);

            if (!string.IsNullOrWhiteSpace(result))
                Log(result.Trim());
        }

        private string RunCommandWithResult(string file, string args)
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

                if (!string.IsNullOrWhiteSpace(error))
                    return output + Environment.NewLine + "ERROR: " + error;

                return output;
            }
            catch (Exception ex)
            {
                return "Ошибка запуска команды: " + ex.Message;
            }
        }
    }
}
