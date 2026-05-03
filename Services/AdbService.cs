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

            // ZEEKR_TOOL_MARKER: ADB_APPS_LIST
            public async Task<List<AppInfo>> GetUserAppsAsync(string deviceId)
            {
                var result = await RunAsync($"-s {deviceId} shell pm list packages -3 -f");
            
                var apps = new List<AppInfo>();
            
                var lines = result.Output
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
                foreach (var line in lines)
                {
                    // format: package:/data/app/.../base.apk=com.package.name
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
            
            string size = await ShellAsync(deviceId, "wm size");
            string density = await ShellAsync(deviceId, "wm density");

            info.ScreenSize = size.Replace("Physical size:", "").Trim();
            info.Density = density.Replace("Physical density:", "").Trim();

            return info;
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
    }
}
