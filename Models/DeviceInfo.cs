namespace ZeekrTool.Models
{
    // ZEEKR_TOOL_MARKER: MODEL_DEVICE_INFO
    public class DeviceInfo
    {
        public string Id { get; set; } = "";
        public string ConnectionType { get; set; } = "";

        public string Model { get; set; } = "";
        public string Brand { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string Device { get; set; } = "";
        public string Hardware { get; set; } = "";

        public string AndroidVersion { get; set; } = "";
        public string SdkVersion { get; set; } = "";
        public string BuildId { get; set; } = "";

        public string CpuAbi { get; set; } = "";
        public string CpuAbiList { get; set; } = "";

        public string ScreenSize { get; set; } = "";
        public string Density { get; set; } = "";
    }
}
