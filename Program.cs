using System;
using System.Threading;
using System.Windows.Forms;

namespace ClaudeUsageBar
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            const string mutexName = "Global\\ClaudeUsageBar_SingleInstance";
            using var mutex = new Mutex(true, mutexName, out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                    "Claude Usage Bar is already running.",
                    "Already Running",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
