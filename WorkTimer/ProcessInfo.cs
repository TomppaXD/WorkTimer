using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WorkTimer
{
    static class ProcessInfo
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint ProcessId);

        public static (string, string) GetActiveWindowTitle()
        {
            IntPtr hwnd = GetForegroundWindow();
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            Process p = Process.GetProcessById((int)pid);

            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            var processName = p.ProcessName;
            var title = "";

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                title = Buff.ToString();
            }
            return (title, processName);
        }
    }
}
