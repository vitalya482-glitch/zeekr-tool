using System.Diagnostics;
using System.Windows;

namespace ZeekrTool
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void CheckAdb_Click(object sender, RoutedEventArgs e)
        {
            var process = new Process();
            process.StartInfo.FileName = "adb";
            process.StartInfo.Arguments = "devices";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            LogBox.Text = output;
        }
    }
}
