namespace ZeekrTool.Models
{
    // ZEEKR_TOOL_MARKER: MODEL_COMMAND_RESULT
    public class CommandResult
    {
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";
        public int ExitCode { get; set; }

        public bool Success => ExitCode == 0 && string.IsNullOrWhiteSpace(Error);

        public string FullText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Error))
                    return Output.Trim();

                return (Output + "\nERROR: " + Error).Trim();
            }
        }
    }
}
