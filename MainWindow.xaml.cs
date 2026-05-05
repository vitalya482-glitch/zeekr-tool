using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ZeekrTool.Models;
using ZeekrTool.Services;

namespace ZeekrTool
{
    // ZEEKR_TOOL_MARKER: MAIN_WINDOW_CODE_BEHIND_REBUILD_V3_APPS_TABLE
    public partial class MainWindow : Window
    {
        private bool _isExiting = false;
        private readonly AdbService _adbService = new AdbService();
        private readonly ObservableCollection<AdbDevice> _devices = new ObservableCollection<AdbDevice>();
        private readonly ObservableCollection<AppInfo> _apps = new ObservableCollection<AppInfo>();
        private ICollectionView? _appsView;
        private AppInfo? _selectedApp;

        private string _selectedApk = "";
        private string _selectedDeviceId = "";

        public MainWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing;
            DeviceComboBox.ItemsSource = _devices;
            if (AppsDataGrid != null)
            {
                _appsView = CollectionViewSource.GetDefaultView(_apps);
                _appsView.Filter = FilterApps;
                AppsDataGrid.ItemsSource = _appsView;
            }
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

        private bool FilterApps(object item)
        {
            if (item is not AppInfo app)
                return false;

            string query = AppSearchBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(query))
                return true;

            return app.PackageName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || app.ApkPath.Contains(query, StringComparison.OrdinalIgnoreCase)
                || app.Type.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateSelectedApp(AppInfo? app)
        {
            _selectedApp = app;

            if (SelectedAppText == null)
                return;

            SelectedAppText.Text = app == null
                ? "Выбери приложение в таблице и нажми нужное действие"
                : "Выбрано: " + app.PackageName;
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
            UpdateSelectedApp(null);

            var apps = await _adbService.GetUserAppsAsync(_selectedDeviceId);

            foreach (var app in apps)
                _apps.Add(app);

            _appsView?.Refresh();
            if (_apps.Count > 0)
            {
                AppsDataGrid.SelectedIndex = 0;
                AppsDataGrid.Focus();
                UpdateSelectedApp(AppsDataGrid.SelectedItem as AppInfo);
            }

            SetOperationStatus("✓ Приложения загружены", $"Найдено: {_apps.Count}", Brushes.LightGreen, 100, false);
            AddHistory($"Загружено приложений: {_apps.Count}");
            Log($"Загружено приложений: {_apps.Count}");
        }

        private async void RefreshApps_Click(object sender, RoutedEventArgs e)
        {
            await LoadAppsToTableAsync();
        }

        private void AppSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _appsView?.Refresh();
            UpdateSelectedApp(AppsDataGrid?.SelectedItem as AppInfo);
        }

        private void AppsDataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            UpdateSelectedApp(AppsDataGrid.SelectedItem as AppInfo);
        }

        private async void AppsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (AppsDataGrid.SelectedItem is AppInfo app && HasSelectedDevice())
            {
                SetOperationStatus("Запуск приложения...", app.PackageName, Brushes.Goldenrod, 0, true);
                var result = await _adbService.LaunchAppAsync(_selectedDeviceId, app.PackageName);
                Log(result.FullText);
                SetOperationStatus("✓ Команда выполнена", app.PackageName, Brushes.LightGreen, 100, false);
                AddHistory("Запуск: " + app.PackageName);
            }
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


        private void OpenAdbTerminal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string adbPath = _adbService.AdbPath;
                string adbDirectory = Path.GetDirectoryName(adbPath) ?? AppDomain.CurrentDomain.BaseDirectory;
                string title = "Zeekr Tool ADB Terminal";
                string deviceHint = string.IsNullOrWhiteSpace(_selectedDeviceId) ? "" : $" && echo Selected device: {_selectedDeviceId}";

                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/K title {title} && cd /d \"{adbDirectory}\" && echo ADB: {adbPath}{deviceHint} && adb devices",
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                SetOperationStatus("✓ Терминал открыт", "Окно cmd запущено с ADB", Brushes.LightGreen, 100, false);
                AddHistory("Открыт ADB terminal");
            }
            catch (Exception ex)
            {
                Log("Ошибка открытия ADB terminal: " + ex.Message);
                SetOperationStatus("× Терминал не открыт", ex.Message, Brushes.OrangeRed, 0, false);
            }
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogBox.Clear();
            AddHistory("Лог очищен");
            SetOperationStatus("✓ Лог очищен", "Окно лога пустое", Brushes.LightGreen, 100, false);
        }
                // ZEEKR_TOOL_MARKER: APP_TABLE_ACTIONS
        private AppInfo? GetSelectedApp()
        {
            if (AppsDataGrid.SelectedItem is AppInfo app)
            {
                UpdateSelectedApp(app);
                return app;
            }

            if (_selectedApp != null && _apps.Contains(_selectedApp))
                return _selectedApp;
        
            SetOperationStatus("× Приложение не выбрано", "Выбери приложение в таблице", Brushes.OrangeRed, 0, false);
            Log("Приложение не выбрано.");
            return null;
        }
        
        private async void LaunchSelectedApp_Click(object sender, RoutedEventArgs e)
        {
            if (!HasSelectedDevice())
                return;
        
            var app = GetSelectedApp();
            if (app == null)
                return;
        
            SetOperationStatus("Запуск приложения...", app.PackageName, Brushes.Goldenrod, 0, true);
        
            var result = await _adbService.LaunchAppAsync(_selectedDeviceId, app.PackageName);
        
            Log(result.FullText);
            SetOperationStatus("✓ Команда выполнена", app.PackageName, Brushes.LightGreen, 100, false);
            AddHistory("Запуск: " + app.PackageName);
        }
        
        private async void StopSelectedApp_Click(object sender, RoutedEventArgs e)
        {
            if (!HasSelectedDevice())
                return;
        
            var app = GetSelectedApp();
            if (app == null)
                return;
        
            SetOperationStatus("Остановка приложения...", app.PackageName, Brushes.Goldenrod, 0, true);
        
            var result = await _adbService.StopAppAsync(_selectedDeviceId, app.PackageName);
        
            Log(result.FullText);
            SetOperationStatus("✓ Приложение остановлено", app.PackageName, Brushes.LightGreen, 100, false);
            AddHistory("Остановка: " + app.PackageName);
        }
        
        private async void UninstallSelectedApp_Click(object sender, RoutedEventArgs e)
        {
            if (!HasSelectedDevice())
                return;
        
            var app = GetSelectedApp();
            if (app == null)
                return;
        
            var confirm = MessageBox.Show(
                $"Удалить приложение?\n\n{app.PackageName}",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );
        
            if (confirm != MessageBoxResult.Yes)
                return;
        
            SetOperationStatus("Удаление приложения...", app.PackageName, Brushes.Goldenrod, 0, true);
        
            var result = await _adbService.UninstallAppAsync(_selectedDeviceId, app.PackageName);
        
            Log(result.FullText);
        
            if (result.FullText.ToLower().Contains("success"))
            {
                _apps.Remove(app);
                SetOperationStatus("✓ Приложение удалено", app.PackageName, Brushes.LightGreen, 100, false);
                AddHistory("Удалено: " + app.PackageName);
            }
            else
            {
                SetOperationStatus("× Ошибка удаления", "Смотри лог", Brushes.OrangeRed, 0, false);
                AddHistory("Ошибка удаления: " + app.PackageName);
            }
        }
        
        private async void ClearSelectedAppData_Click(object sender, RoutedEventArgs e)
        {
            if (!HasSelectedDevice())
                return;
        
            var app = GetSelectedApp();
            if (app == null)
                return;
        
            var confirm = MessageBox.Show(
                $"Очистить данные приложения?\n\n{app.PackageName}\n\nЭто удалит настройки, кэш и данные приложения.",
                "Подтверждение очистки данных",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );
        
            if (confirm != MessageBoxResult.Yes)
                return;
        
            SetOperationStatus("Очистка данных...", app.PackageName, Brushes.Goldenrod, 0, true);
        
            var result = await _adbService.ClearAppDataAsync(_selectedDeviceId, app.PackageName);
        
            Log(result.FullText);
        
            if (result.FullText.ToLower().Contains("success"))
            {
                SetOperationStatus("✓ Данные очищены", app.PackageName, Brushes.LightGreen, 100, false);
                            AddHistory("Очищены данные: " + app.PackageName);
                        }
                        else
                        {
                            SetOperationStatus("× Ошибка очистки", "Смотри лог", Brushes.OrangeRed, 0, false);
                            AddHistory("Ошибка очистки: " + app.PackageName);
                        }
                    }
        // методы выхода дальше 2 штуки идут
        // ZEEKR_TOOL_MARKER: APP_EXIT_CLEANUP
        private async void Exit_Click(object sender, RoutedEventArgs e)
        {
            _isExiting = true;
        
            SetOperationStatus("Выход...", "Остановка ADB server", Brushes.Goldenrod, 0, true);
        
            try
            {
                await _adbService.StopServerAsync();
            }
            catch {}
        
            Application.Current.Shutdown();
        }
        // ZEEKR_TOOL_MARKER: WINDOW_CLOSE_CLEANUP
        private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isExiting)
                return;
        
            try
            {
                await _adbService.StopServerAsync();
            }
            catch {}
        }
    }
}
