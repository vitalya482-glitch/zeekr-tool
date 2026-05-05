using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ZeekrTool.Models;
using ZeekrTool.Services;

namespace ZeekrTool
{
    // ZEEKR_TOOL_MARKER: MAIN_WINDOW_CODE_BEHIND_REBUILD_V3_APPS_TABLE
    public partial class MainWindow : Window
    {
        private readonly AdbService _adbService = new AdbService();

        private readonly ObservableCollection<AdbDevice> _devices = new ObservableCollection<AdbDevice>();
        private readonly ObservableCollection<AppInfo> _apps = new ObservableCollection<AppInfo>();

        private string _selectedApk = "";
        private string _selectedDeviceId = "";

        public MainWindow()
        {
            InitializeComponent();

            DeviceComboBox.ItemsSource = _devices;

            // Если в MainWindow.xaml уже есть таблица приложений:
            if (AppsDataGrid != null)
                AppsDataGrid.ItemsSource = _apps;

            SetGlobalStatus("● Ожидание", "Устройство не подключено", Brushes.Goldenrod);
            SetOperationStatus("✓ Готово", "Ожидание действий", Brushes.LightGreen, 0, false);

            _ = RefreshDevicesAsync();
        }

        // ZEEKR_TOOL_MARKER: UI_STATUS_HELPERS
        private void SetGlobalStatus(string title, string subtitle, Brush color)
        {
            TopStatusText.Text = title;
            TopStatusText.Foreground = color;
            TopStatusSubText.Text = subtitle;
        }

        private void SetOperationStatus(string title, string subtitle, Brush color, int progress, bool indeterminate)
        {
            OperationStatusText.Text = title;
            OperationStatusText.Foreground = color;
            OperationSubStatusText.Text = subtitle;

            OperationProgressBar.IsIndeterminate = indeterminate;
            OperationProgressBar.Value = progress;
        }

        private void AddHistory(string text)
        {
            OperationHistoryList.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {text}");
        }

        private void Log(string text)
        {
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
            LogBox.ScrollToEnd();
        }

        private string EmptyDash(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        }

        // ZEEKR_TOOL_MARKER: SCREEN_NAVIGATION
        private void ShowHome_Click(object sender, RoutedEventArgs e)
        {
            HomePanel.Visibility = Visibility.Visible;
            AppsPanel.Visibility = Visibility.Collapsed;
        }

        private async void ShowApps_Click(object sender, RoutedEventArgs e)
        {
            HomePanel.Visibility = Visibility.Collapsed;
            AppsPanel.Visibility = Visibility.Visible;

            await LoadAppsToTableAsync();
        }

        // ZEEKR_TOOL_MARKER: DEVICE_REFRESH
        private async void RefreshDevices_Click(object sender, RoutedEventArgs e)
        {
            await RefreshDevicesAsync();
        }

        private async Task RefreshDevicesAsync()
        {
            SetGlobalStatus("● Поиск устройств", "ADB devices...", Brushes.Goldenrod);
            SetOperationStatus("Поиск устройств...", "Обновление списка ADB", Brushes.Goldenrod, 0, true);

            Log("Обновление списка устройств...");

            _devices.Clear();
            _selectedDeviceId = "";

            var devices = await _adbService.GetDevicesAsync();

            foreach (var device in devices)
                _devices.Add(device);

            if (_devices.Count == 0)
            {
                DeviceComboBox.SelectedIndex = -1;
                SetDisconnected("Устройства не найдены");
                SetOperationStatus("× Нет устройств", "Подключи USB или Wi-Fi ADB", Brushes.OrangeRed, 0, false);
                AddHistory("Устройства не найдены");
                return;
            }

            DeviceComboBox.SelectedIndex = 0;

            SetOperationStatus("✓ Список обновлён", $"Найдено устройств: {_devices.Count}", Brushes.LightGreen, 100, false);
            AddHistory($"Найдено устройств: {_devices.Count}");
            Log($"Найдено устройств: {_devices.Count}");
        }

        private void DeviceComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DeviceComboBox.SelectedItem is not AdbDevice device)
                return;

            _selectedDeviceId = device.Id;

            SetGlobalStatus("● Устройство выбрано", device.DisplayName, Brushes.Goldenrod);
            Log("Выбрано устройство: " + device.DisplayName);
        }

        // ZEEKR_TOOL_MARKER: DEVICE_CONNECT_AND_INFO
        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            await ConnectSelectedDeviceAsync();
        }

        private async Task ConnectSelectedDeviceAsync()
        {
            if (string.IsNullOrWhiteSpace(_selectedDeviceId))
            {
                await RefreshDevicesAsync();
                return;
            }

            SetGlobalStatus("● Подключение...", _selectedDeviceId, Brushes.Goldenrod);
            SetOperationStatus("Подключение...", "Проверка выбранного устройства", Brushes.Goldenrod, 0, true);

            Log("Проверка выбранного устройства: " + _selectedDeviceId);

            string state = await _adbService.GetStateAsync(_selectedDeviceId);

            if (!state.Contains("device"))
            {
                SetDisconnected("Устройство недоступно: " + state);
                SetOperationStatus("× Ошибка подключения", state, Brushes.OrangeRed, 0, false);
                AddHistory("Ошибка подключения");
                return;
            }

            var info = await _adbService.GetDeviceInfoAsync(_selectedDeviceId);
            ApplyDeviceInfo(info);

            SetGlobalStatus("● Подключено", $"{EmptyDash(info.Model)} / {info.ConnectionType}", Brushes.LightGreen);
            SetOperationStatus("✓ Готово", "Устройство подключено и готово к работе", Brushes.LightGreen, 100, false);
            AddHistory("Подключение к устройству");

            LogDeviceInfo(info);
        }

        private void ApplyDeviceInfo(DeviceInfo info)
        {
            DeviceStatusText.Text = "✓ Подключено";
            DeviceStatusText.Foreground = Brushes.LightGreen;

            DeviceSubStatusText.Text = $"{EmptyDash(info.Model)} / {info.ConnectionType}";
            DeviceIdText.Text = "ID: " + EmptyDash(info.Id);
            ConnectionTypeText.Text = "Тип подключения: " + EmptyDash(info.ConnectionType);

            DeviceModelText.Text = EmptyDash(info.Model);
            BrandText.Text = EmptyDash(info.Brand);
            ManufacturerText.Text = EmptyDash(info.Manufacturer);

            AndroidVersionText.Text = "Android " + EmptyDash(info.AndroidVersion);
            SdkText.Text = EmptyDash(info.SdkVersion);
            BuildText.Text = EmptyDash(info.BuildId);

            CpuAbiText.Text = string.IsNullOrWhiteSpace(info.CpuAbiList) ? EmptyDash(info.CpuAbi) : info.CpuAbiList;
            ScreenText.Text = $"{EmptyDash(info.ScreenSize)} / DPI {EmptyDash(info.Density)}";
            AdbStatusText.Text = "ADB подключен";
        }

        private void LogDeviceInfo(DeviceInfo info)
        {
            Log("Устройство найдено:");
            Log("ID: " + info.Id);
            Log("Тип подключения: " + info.ConnectionType);
            Log("Модель: " + info.Model);
            Log("Бренд: " + info.Brand);
            Log("Производитель: " + info.Manufacturer);
            Log("Device: " + info.Device);
            Log("Hardware: " + info.Hardware);
            Log("Android: " + info.AndroidVersion);
            Log("SDK: " + info.SdkVersion);
            Log("Build: " + info.BuildId);
            Log("CPU ABI: " + info.CpuAbi);
            Log("ABI list: " + info.CpuAbiList);
            Log("Экран: " + info.ScreenSize);
            Log("DPI: " + info.Density);
        }

        private void SetDisconnected(string reason)
        {
            DeviceStatusText.Text = "× Нет подключения";
            DeviceStatusText.Foreground = Brushes.OrangeRed;

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

            SetGlobalStatus("● Нет подключения", reason, Brushes.OrangeRed);
            Log(reason);
        }

        private bool HasSelectedDevice()
        {
            if (!string.IsNullOrWhiteSpace(_selectedDeviceId))
                return true;

            Log("Сначала выбери устройство.");
            SetOperationStatus("× Нет устройства", "Сначала выбери ADB-устройство", Brushes.OrangeRed, 0, false);
            return false;
        }

        // ZEEKR_TOOL_MARKER: APPS_TABLE
        private async Task LoadAppsToTableAsync()
        {
            if (!HasSelectedDevice())
                return;

            SetOperationStatus("Загрузка приложений...", "Получение списка пользовательских приложений", Brushes.Goldenrod, 0, true);

            _apps.Clear();

            var apps = await _adbService.GetUserAppsAsync(_selectedDeviceId);

            foreach (var app in apps)
                _apps.Add(app);

            SetOperationStatus("✓ Приложения загружены", $"Найдено: {_apps.Count}", Brushes.LightGreen, 100, false);
            AddHistory($"Загружено приложений: {_apps.Count}");
            Log($"Загружено приложений: {_apps.Count}");
        }

        private async void RefreshApps_Click(object sender, RoutedEventArgs e)
        {
            await LoadAppsToTableAsync();
        }

        private void AppSearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Поиск подключим следующим шагом через CollectionView.
            // ZEEKR_TOOL_MARKER: APPS_SEARCH_RESERVED
        }

        // ZEEKR_TOOL_MARKER: APK_INSTALL
        private void SelectApk_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "APK files (*.apk)|*.apk"
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedApk = dialog.FileName;
                SelectedFileText.Text = "Выбран APK: " + _selectedApk;
                Log("Выбран APK: " + _selectedApk);
                AddHistory("Выбран APK");
            }
        }

        private async void InstallApk_Click(object sender, RoutedEventArgs e)
        {
            if (!HasSelectedDevice())
                return;

            if (string.IsNullOrWhiteSpace(_selectedApk))
            {
                Log("Сначала выбери APK-файл.");
                SetOperationStatus("× APK не выбран", "Нужно выбрать файл приложения", Brushes.OrangeRed, 0, false);
                return;
            }

            if (!File.Exists(_selectedApk))
            {
                Log("Файл APK не найден.");
                SetOperationStatus("× Файл не найден", "APK отсутствует по выбранному пути", Brushes.OrangeRed, 0, false);
                return;
            }

            SetGlobalStatus("● Установка APK...", Path.GetFileName(_selectedApk), Brushes.Goldenrod);
            SetOperationStatus("Установка APK...", "Передача и установка приложения", Brushes.Goldenrod, 0, true);

            Log("Установка APK на устройство: " + _selectedDeviceId);

            var result = await _adbService.InstallApkAsync(_selectedDeviceId, _selectedApk);

            Log(result.FullText);

            if (result.FullText.ToLower().Contains("success"))
            {
                SetGlobalStatus("● Подключено", "APK установлен", Brushes.LightGreen);
                SetOperationStatus("✓ APK установлен", Path.GetFileName(_selectedApk), Brushes.LightGreen, 100, false);
                AddHistory("APK установлен");

                if (AppsPanel.Visibility == Visibility.Visible)
                    await LoadAppsToTableAsync();
            }
            else
            {
                SetGlobalStatus("● Ошибка", "Ошибка установки APK", Brushes.OrangeRed);
                SetOperationStatus("× Ошибка установки", "Смотри лог действий", Brushes.OrangeRed, 0, false);
                AddHistory("Ошибка установки APK");
            }
        }

        // ZEEKR_TOOL_MARKER: QUICK_COMMANDS
        private async void Reboot_Click(object sender, RoutedEventArgs e)
        {
            if (!HasSelectedDevice())
                return;

            SetOperationStatus("Перезагрузка...", "Команда reboot отправлена", Brushes.Goldenrod, 0, true);
            var result = await _adbService.RebootAsync(_selectedDeviceId);
            Log(result.FullText);
            AddHistory("Перезагрузка устройства");
        }

        private async void RestartAdb_Click(object sender, RoutedEventArgs e)
        {
            SetOperationStatus("Перезапуск ADB...", "kill-server / start-server", Brushes.Goldenrod, 0, true);

            var result = await _adbService.RestartServerAsync();

            Log(result.FullText);
            SetOperationStatus("✓ ADB перезапущен", "Сервер ADB обновлён", Brushes.LightGreen, 100, false);
            AddHistory("ADB перезапущен");

            await RefreshDevicesAsync();
        }

        private async void LoadApps_Click(object sender, RoutedEventArgs e)
        {
            HomePanel.Visibility = Visibility.Collapsed;
            AppsPanel.Visibility = Visibility.Visible;

            await LoadAppsToTableAsync();
        }

        private async void Screenshot_Click(object sender, RoutedEventArgs e)
        {
            if (!HasSelectedDevice())
                return;

            SetOperationStatus("Скриншот...", "Создание снимка экрана", Brushes.Goldenrod, 0, true);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string remotePath = $"/sdcard/zeekrtool_screenshot_{timestamp}.png";
            string localPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"zeekrtool_screenshot_{timestamp}.png"
            );

            await _adbService.ShellAsync(_selectedDeviceId, $"screencap -p {remotePath}");
            var result = await _adbService.RunAsync($"-s {_selectedDeviceId} pull {remotePath} \"{localPath}\"");

            Log(result.FullText);
            SetOperationStatus("✓ Скриншот готов", localPath, Brushes.LightGreen, 100, false);
            AddHistory("Скриншот сохранён");
        }

        private async void OpenActivityLauncher_Click(object sender, RoutedEventArgs e)
        {
            if (!HasSelectedDevice())
                return;

            SetOperationStatus("Запуск Activity Launcher...", "Попытка запуска приложения", Brushes.Goldenrod, 0, true);

            var result = await _adbService.RunAsync(
                $"-s {_selectedDeviceId} shell monkey -p de.szalkowski.activitylauncher -c android.intent.category.LAUNCHER 1"
            );

            Log(result.FullText);
            SetOperationStatus("✓ Команда выполнена", "Проверь экран устройства", Brushes.LightGreen, 100, false);
            AddHistory("Запуск Activity Launcher");
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogBox.Clear();
            AddHistory("Лог очищен");
            SetOperationStatus("✓ Лог очищен", "Окно лога пустое", Brushes.LightGreen, 100, false);
        }
    }
}
