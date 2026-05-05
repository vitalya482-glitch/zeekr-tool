using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ZeekrTool.Models;

namespace ZeekrTool.Services
{
    // ZEEKR_TOOL_MARKER: SERVICE_ADB_CORE
    public class AdbService
    {
        private readonly string _adbPath;

        public AdbService()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string bundledAdb = Path.Combine(baseDir, "adb", "adb.exe");
            _adbPath = File.Exists(bundledAdb) ? bundledAdb : "adb";
        }

        public string AdbPath => _adbPath;

        public async Task<CommandResult> RunAsync(string arguments)
        {
            try
            {
                var process = new Process();
                process.StartInfo.FileName = _adbPath;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                return new CommandResult
                {
                    Output = output,
                    Error = error,
                    ExitCode = process.ExitCode
                };
            }
            catch (Exception ex)
            {
                return new CommandResult
                {
                    Output = "",
                    Error = ex.Message,
                    ExitCode = -1
                };
            }
        }

        public async Task<List<AdbDevice>> GetDevicesAsync()
        {
            var result = await RunAsync("devices");
            var devices = new List<AdbDevice>();

            var lines = result.Output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Skip(1);

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 2)
                    continue;

                string id = parts[0].Trim();
                string state = parts[1].Trim();

                var device = new AdbDevice
                {
                    Id = id,
                    State = state,
                    ConnectionType = id.Contains(":") ? "Wi-Fi ADB" : "USB ADB"
                };

                if (state == "device")
                    device.Model = await GetPropAsync(id, "ro.product.model");

                devices.Add(device);
            }

            return devices;
        }

        public async Task<string> GetStateAsync(string deviceId)
        {
            var result = await RunAsync($"-s {deviceId} get-state");
            return result.FullText.Trim();
        }

        public async Task<DeviceInfo> GetDeviceInfoAsync(string deviceId)
        {
            var info = new DeviceInfo
            {
                Id = deviceId,
                ConnectionType = deviceId.Contains(":") ? "Wi-Fi ADB" : "USB ADB",

                Model = await GetPropAsync(deviceId, "ro.product.model"),
                Brand = await GetPropAsync(deviceId, "ro.product.brand"),
                Manufacturer = await GetPropAsync(deviceId, "ro.product.manufacturer"),
                Device = await GetPropAsync(deviceId, "ro.product.device"),
                Hardware = await GetPropAsync(deviceId, "ro.hardware"),

                AndroidVersion = await GetPropAsync(deviceId, "ro.build.version.release"),
                SdkVersion = await GetPropAsync(deviceId, "ro.build.version.sdk"),
                BuildId = await GetPropAsync(deviceId, "ro.build.display.id"),

                CpuAbi = await GetPropAsync(deviceId, "ro.product.cpu.abi"),
                CpuAbiList = await GetPropAsync(deviceId, "ro.product.cpu.abilist"),
            };

            string size = await ShellAsync(deviceId, "wm size");
            string density = await ShellAsync(deviceId, "wm density");

            info.ScreenSize = size.Replace("Physical size:", "").Trim();
            info.Density = density.Replace("Physical density:", "").Trim();

            return info;
        }

        // ZEEKR_TOOL_MARKER: ADB_APPS_LIST
        public async Task<List<AppInfo>> GetUserAppsAsync(string deviceId)
        {
            var result = await RunAsync($"-s {deviceId} shell pm list packages -3 -f");
            var apps = new List<AppInfo>();

            var lines = result.Output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (!line.StartsWith("package:"))
                    continue;

                string raw = line.Replace("package:", "").Trim();
                string apkPath = "";
                string packageName = "";

                int splitIndex = raw.LastIndexOf('=');

                if (splitIndex > 0)
                {
                    apkPath = raw.Substring(0, splitIndex);
                    packageName = raw.Substring(splitIndex + 1);
                }
                else
                {
                    packageName = raw;
                }

                apps.Add(new AppInfo
                {
                    PackageName = packageName,
                    ApkPath = apkPath,
                    Type = "User"
                });
            }

            return apps.OrderBy(x => x.PackageName).ToList();
        }

        public async Task<string> GetPropAsync(string deviceId, string prop)
        {
            return (await ShellAsync(deviceId, $"getprop {prop}")).Trim();
        }

        public async Task<string> ShellAsync(string deviceId, string command)
        {
            var result = await RunAsync($"-s {deviceId} shell {command}");
            return result.FullText.Trim();
        }

        public async Task<CommandResult> InstallApkAsync(string deviceId, string apkPath)
        {
            return await RunAsync($"-s {deviceId} install -r \"{apkPath}\"");
        }

        public async Task<CommandResult> InstallMultipleApksAsync(string deviceId, IEnumerable<string> apkPaths)
        {
            string files = string.Join(" ", apkPaths.Select(path => $"\"{path}\""));
            return await RunAsync($"-s {deviceId} install-multiple -r {files}");
        }

        public async Task<CommandResult> RebootAsync(string deviceId)
        {
            return await RunAsync($"-s {deviceId} reboot");
        }

        public async Task<CommandResult> RestartServerAsync()
        {
            await RunAsync("kill-server");
            return await RunAsync("start-server");
        }

        public async Task<CommandResult> GetThirdPartyPackagesAsync(string deviceId)
        {
            return await RunAsync($"-s {deviceId} shell pm list packages -3");
        }

        public async Task<List<string>> GetPackageApkPathsAsync(string deviceId, string packageName)
        {
            var result = await RunAsync($"-s {deviceId} shell pm path {packageName}");

            return result.Output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.StartsWith("package:", StringComparison.OrdinalIgnoreCase))
                .Select(line => line.Substring("package:".Length).Trim())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct()
                .ToList();
        }

        public async Task<CommandResult> PullFileAsync(string deviceId, string remotePath, string localPath)
        {
            string? directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            return await RunAsync($"-s {deviceId} pull \"{remotePath}\" \"{localPath}\"");
        }

        public async Task<string> GetPackageVersionInfoAsync(string deviceId, string packageName)
        {
            // grep exists on most Android builds through toybox. If it is unavailable,
            // adb returns the full error text and the caller saves it in metadata.
            return await ShellAsync(deviceId, $"dumpsys package {packageName} | grep -E 'versionName|versionCode|firstInstallTime|lastUpdateTime'");
        }

        public async Task<CommandResult> TryBackupAppDataAsync(string deviceId, string packageName, string localTarPath)
        {
            string? directory = Path.GetDirectoryName(localTarPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            try
            {
                var process = new Process();
                process.StartInfo.FileName = _adbPath;
                process.StartInfo.Arguments = $"-s {deviceId} exec-out run-as {packageName} sh -c \"cd /data/data/{packageName} && tar -cf - .\"";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();

                await using (var output = File.Create(localTarPath))
                {
                    await process.StandardOutput.BaseStream.CopyToAsync(output);
                }

                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                var fileInfo = new FileInfo(localTarPath);
                if (process.ExitCode != 0 || fileInfo.Length == 0)
                {
                    try { File.Delete(localTarPath); } catch { }
                    return new CommandResult
                    {
                        Output = "",
                        Error = string.IsNullOrWhiteSpace(error) ? "Данные приложения недоступны без root/debuggable-доступа." : error,
                        ExitCode = process.ExitCode == 0 ? -1 : process.ExitCode
                    };
                }

                return new CommandResult
                {
                    Output = $"Data backup saved: {localTarPath}",
                    Error = error,
                    ExitCode = process.ExitCode
                };
            }
            catch (Exception ex)
            {
                try { if (File.Exists(localTarPath)) File.Delete(localTarPath); } catch { }
                return new CommandResult
                {
                    Output = "",
                    Error = ex.Message,
                    ExitCode = -1
                };
            }
        }
                    // ZEEKR_TOOL_MARKER: ADB_STOP_SERVER
        public async Task<CommandResult> StopServerAsync()
        {
            return await RunAsync("kill-server");
        }
                // ZEEKR_TOOL_MARKER: ADB_APP_ACTIONS
        public async Task<CommandResult> LaunchAppAsync(string deviceId, string packageName)
        {
            return await RunAsync($"-s {deviceId} shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1");
        }
        
        public async Task<CommandResult> StopAppAsync(string deviceId, string packageName)
        {
            return await RunAsync($"-s {deviceId} shell am force-stop {packageName}");
        }
        
        public async Task<CommandResult> UninstallAppAsync(string deviceId, string packageName)
        {
            return await RunAsync($"-s {deviceId} uninstall {packageName}");
        }
        
        public async Task<CommandResult> ClearAppDataAsync(string deviceId, string packageName)
        {
            return await RunAsync($"-s {deviceId} shell pm clear {packageName}");
        }
    }
}
