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
            return File.Exists(adbPath) ? adbPath : "adb";
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
            string connectionType = deviceId.Contains(":5555") ? "Wi-Fi ADB" : "USB ADB";

            string model = GetProp("ro.product.model");
            string brand = GetProp("ro.product.brand");
            string manufacturer = GetProp("ro.product.manufacturer");
            string device = GetProp("ro.product.device");
            string hardware = GetProp("ro.hardware");

            string android = GetProp("ro.build.version.release");
            string sdk = GetProp("ro.build.version.sdk");
            string build = GetProp("ro.build.display.id");

            string abi = GetProp("ro.product.cpu.abi");
            string abiList = GetProp("ro.product.cpu.abilist");

            string screen = RunAdbShell("wm size").Trim().Replace("Physical size:", "").Trim();
            string dpi = RunAdbShell("wm density").Trim().Replace("Physical density:", "").Trim();

            DeviceStatusText.Text = "✓ Устройство подключено";
            DeviceStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;

            DeviceSubStatusText.Text = $"{model} / {connectionType}";
            DeviceIdText.Text = "ID: " + deviceId;
            ConnectionTypeText.Text = "Тип подключения: " + connectionType;

            DeviceModelText.Text = string.IsNullOrWhiteSpace(model) ? device : model;
            BrandText.Text = EmptyDash(brand);
            ManufacturerText.Text = EmptyDash(manufacturer);

            AndroidVersionText.Text = "Android " + EmptyDash(android);
            SdkText.Text = EmptyDash(sdk);
            BuildText.Text = EmptyDash(build);

            CpuAbiText.Text = string.IsNullOrWhiteSpace(abiList) ? EmptyDash(abi) : abiList;
            ScreenText.Text = $"{EmptyDash(screen)} / DPI {EmptyDash(dpi)}";
            AdbStatusText.Text = "ADB подключен";

            Log("Устройство найдено:");
            Log("ID: " + deviceId);
            Log("Тип подключения: " + connectionType);
            Log("Модель: " + model);
            Log("Бренд: " + brand);
            Log("Производитель: " + manufacturer);
            Log("Device: " + device);
            Log("Hardware: " + hardware);
            Log("Android: " + android);
            Log("SDK: " + sdk);
            Log("Build: " + build);
            Log("CPU ABI: " + abi);
            Log("ABI list: " + abiList);
            Log("Экран: " + screen);
            Log("DPI: " + dpi);
        }

        private string EmptyDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        }

        private string GetProp(string prop)
        {
            return RunAdbShell("getprop " + prop).Trim();
        }

        private void SetDeviceDisconnected(string reason)
        {
            DeviceStatusText.Text = "× Устройство не подключено";
            DeviceStatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;

            DeviceSubStatusText.Text = reason;
            DeviceIdText.Text = "ID: —";
            ConnectionTypeText.Text = "Тип подключения: —";

            DeviceModelText.Text = "—";
            BrandText.Text = "—";
            ManufacturerText.Text = "—";

            AndroidVersionText.Text = "—";
            SdkText.Text = "—";
            BuildText.Text = "—";

            CpuAbiText.Text = "—";
            ScreenText.Text = "—";
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
