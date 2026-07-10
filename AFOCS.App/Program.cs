using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AFOCS.App
{
    static class Program
    {

        [STAThread]
        static void Main()
        {
            RunApp();
        }


        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        static void RunApp()
        {
            var mutex = new Mutex(true, "Dimension.AFOCS.APP", out var ret);
            if (ret)
            {
                try
                {
                    var app = new App();
                    app.InitializeComponent();
                    app.Run();
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
            else
            {
                var current = Process.GetCurrentProcess();
                foreach (var process in Process.GetProcessesByName(current.ProcessName))
                {
                    if (process.Id != current.Id)
                    {
                        IntPtr hWnd = process.MainWindowHandle; // 如果窗口当前在前面，直接置顶即可
                        if (hWnd != IntPtr.Zero)
                        {
                            SetForegroundWindow(hWnd);
                            ShowWindow(hWnd, SW_RESTORE);
                        }
                        else
                        {
                            BroadcastShowWindowMessage();     // 如果窗口被最小化到托盘，发送自定义消息去置顶
                        }
                        break;
                    }
                }
            }
        }
        // 定义Windows消息常量，当前表示双击
        private const int WM_SHOWME = 0x8001;

        // 向所有顶级窗口发送显示消息
        private static void BroadcastShowWindowMessage()
        {
            EnumWindows((hWnd, lParam) =>
            {
                int processId;
                GetWindowThreadProcessId(hWnd, out processId);

                // 检查该窗口是否属于我们的应用程序
                Process process = null;
                try
                {
                    process = Process.GetProcessById(processId);
                    if (process.ProcessName == Process.GetCurrentProcess().ProcessName &&
                        process.Id != Process.GetCurrentProcess().Id)
                    {
                        // 发送自定义消息给应用程序窗口
                        PostMessage(hWnd, WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
                    }
                }
                catch
                {
                    // 忽略可能出现的异常
                }
                finally
                {
                    process?.Dispose();
                }
                return true;
            }, IntPtr.Zero);
        }

        [DllImport("user32.dll", EntryPoint = "SetForegroundWindow", SetLastError = true)]
        internal static extern void SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    }
}
