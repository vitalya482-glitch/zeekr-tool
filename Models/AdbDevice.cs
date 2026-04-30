namespace ZeekrTool.Models
{
    // ZEEKR_TOOL_MARKER: MODEL_ADB_DEVICE
    public class AdbDevice
    {
        public string Id { get; set; } = "";
        public string State { get; set; } = "";
        public string ConnectionType { get; set; } = "";
        public string Model { get; set; } = "";

        public string DisplayName
        {
            get
            {
                string modelPart = string.IsNullOrWhiteSpace(Model) ? "" : $" ({Model})";
                return $"{Id} | {State} | {ConnectionType}{modelPart}";
            }
        }
    }
}
