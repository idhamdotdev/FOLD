using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FOLD;

static class Program
{
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appId);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int SW_RESTORE = 9;

    private static void RegisterAppUserModelID()
    {
        try
        {
            string appId = "FOLD by @idham.dev";
            string exePath = System.Environment.ProcessPath ?? "";
            if (string.IsNullOrEmpty(exePath)) return;

            using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\AppUserModelId\{appId}"))
            {
                if (key != null)
                {
                    key.SetValue("DisplayName", "FOLD");
                    key.SetValue("IconUri", exePath);
                }
            }
        }
        catch { }
    }

    private static void TryShowExistingInstance()
    {
        try
        {
            var currentProc = Process.GetCurrentProcess();
            foreach (var proc in Process.GetProcessesByName(currentProc.ProcessName))
            {
                if (proc.Id != currentProc.Id)
                {
                    IntPtr handle = proc.MainWindowHandle;
                    if (handle != IntPtr.Zero)
                    {
                        ShowWindow(handle, SW_RESTORE);
                        SetForegroundWindow(handle);
                        return;
                    }
                }
            }
        }
        catch { }
    }

    [STAThread]
    static void Main()
    {
        // Enforce single instance check using a global Mutex
        using (var mutex = new System.Threading.Mutex(true, "FOLD_by_idham_dev_Mutex", out bool createdNew))
        {
            if (!createdNew)
            {
                TryShowExistingInstance();
                return;
            }

            RegisterAppUserModelID();

            try
            {
                SetCurrentProcessExplicitAppUserModelID("FOLD by @idham.dev");
            }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            using var trayApp = new TrayApp();
            trayApp.Run();
        }
    }
}


