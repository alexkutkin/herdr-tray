using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HerdrTray;

/// <summary>
/// The tray shortcut menu: Status, Settings, Exit. Standard Windows menu rendering —
/// the same shape and metrics as other notification-area utilities — in the app's dark palette.
/// </summary>
internal static class TrayMenu
{
    private const int DwmWindowCornerPreference = 33;
    private const int DwmRound = 2;

    public static ContextMenuStrip Create(Action showWall, Action showSettings, Action exit)
    {
        bool dark = !SystemInformation.HighContrast;

        // Construct a control first: WinForms switches the process to the DPI awareness
        // declared in App.config here, so metrics must be read after it, not before.
        var menu = new ContextMenuStrip { Font = SystemFonts.MenuFont };

        if (dark)
        {
            menu.BackColor = DarkMenuRenderer.PanelColor;
            menu.ForeColor = DarkMenuRenderer.TextColor;
            menu.Renderer = new DarkMenuRenderer();
            menu.HandleCreated += (_, _) => RoundCorners(menu.Handle);
        }
        else
        {
            menu.Renderer = new ToolStripSystemRenderer();
        }

        // The default command is bold, as in other notification-area utilities.
        var bold = new Font(menu.Font, FontStyle.Bold);
        menu.Disposed += (_, _) => bold.Dispose();

        var status = new ToolStripMenuItem("Status", null, (_, _) => showWall()) { Font = bold };
        var settings = new ToolStripMenuItem("Settings", null, (_, _) => showSettings());
        var quit = new ToolStripMenuItem("Exit", null, (_, _) => exit());

        menu.Items.Add(status);
        menu.Items.Add(settings);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(quit);

        menu.Opened += (_, _) => KeepAboveTaskbar(menu);
        return menu;
    }

    // Notification-area menus open upward and stay clear of the taskbar, unlike the
    // default cursor-anchored placement that can spill under it.
    private static void KeepAboveTaskbar(ContextMenuStrip menu)
    {
        Rectangle area = Screen.FromRectangle(menu.Bounds).WorkingArea;
        int left = Math.Max(area.Left, Math.Min(menu.Left, area.Right - menu.Width));
        int top = Math.Max(area.Top, Math.Min(menu.Top, area.Bottom - menu.Height));
        if (left != menu.Left || top != menu.Top)
        {
            menu.Location = new Point(left, top);
        }
    }

    private static void RoundCorners(IntPtr window)
    {
        int preference = DwmRound;
        DwmSetWindowAttribute(window, DwmWindowCornerPreference, ref preference, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
