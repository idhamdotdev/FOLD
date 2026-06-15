using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FOLD;

static class Program
{
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appId);

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

    [STAThread]
    static void Main()
    {
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

