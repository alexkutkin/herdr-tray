using System;
using System.Threading;
using System.Windows.Forms;

namespace HerdrTray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Autostart plus a double-click must not produce two tray icons.
        using Mutex mutex = new Mutex(true, @"Local\HerdrTray", out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApplicationContext());
    }
}
