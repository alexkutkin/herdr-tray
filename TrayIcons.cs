using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace HerdrTray;

/// <summary>
/// Draws the four tray icons: an "H" in the state colour plus a badge —
/// nothing (idle), a dot (working), "!" (needs attention), "x" (disconnected).
/// Shapes rather than glyphs, so they stay readable at 16 px.
/// </summary>
internal static class TrayIcons
{
    private static readonly Dictionary<TrayState, Color> Palette = new()
    {
        [TrayState.Idle] = Color.FromArgb(0x9E, 0x9E, 0x9E),
        [TrayState.Working] = Color.FromArgb(0x4D, 0xA3, 0xFF),
        [TrayState.NeedsAttention] = Color.FromArgb(0xFF, 0xB0, 0x20),
        [TrayState.Disconnected] = Color.FromArgb(0xFF, 0x4D, 0x4F),
    };

    public static Dictionary<TrayState, Icon> Create() =>
        Create(Math.Max(16, SystemInformation.SmallIconSize.Width));

    public static Dictionary<TrayState, Icon> Create(int size) =>
        Palette.ToDictionary(entry => entry.Key, entry => Draw(entry.Key, entry.Value, size));

    public static Color ColorOf(TrayState state) => Palette[state];

    private static Icon Draw(TrayState state, Color color, int size)
    {
        using var bitmap = new Bitmap(size, size);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (var brush = new SolidBrush(color))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            DrawH(graphics, brush, size);

            switch (state)
            {
                case TrayState.Working:
                    DrawDot(graphics, brush, size);
                    break;
                case TrayState.NeedsAttention:
                    DrawBang(graphics, brush, size);
                    break;
                case TrayState.Disconnected:
                    DrawCross(graphics, color, size);
                    break;
            }
        }

        return ToIcon(bitmap);
    }

    private static void DrawH(Graphics graphics, Brush brush, int size)
    {
        float stroke = Stroke(size);
        float left = MathF.Round(size * 0.09f);
        float right = MathF.Round(size * 0.56f);
        float top = MathF.Round(size * 0.19f);
        float bottom = MathF.Round(size * 0.81f);

        graphics.FillRectangle(brush, left, top, stroke, bottom - top);
        graphics.FillRectangle(brush, right - stroke, top, stroke, bottom - top);
        graphics.FillRectangle(brush, left, (top + bottom - stroke) / 2f, right - left, stroke);
    }

    private static void DrawDot(Graphics graphics, Brush brush, int size)
    {
        float diameter = MathF.Round(size * 0.34f);
        float offset = size - diameter - MathF.Round(size * 0.06f);
        graphics.FillEllipse(brush, offset, offset, diameter, diameter);
    }

    private static void DrawBang(Graphics graphics, Brush brush, int size)
    {
        float stroke = Stroke(size);
        float x = size - stroke - MathF.Round(size * 0.12f);
        float top = MathF.Round(size * 0.25f);
        float barBottom = MathF.Round(size * 0.62f);
        float dotTop = MathF.Round(size * 0.74f);

        graphics.FillRectangle(brush, x, top, stroke, barBottom - top);
        graphics.FillRectangle(brush, x, dotTop, stroke, stroke);
    }

    private static void DrawCross(Graphics graphics, Color color, int size)
    {
        using var pen = new Pen(color, Stroke(size)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float left = size * 0.66f;
        float right = size * 0.94f;
        float top = size * 0.42f;
        float bottom = size * 0.74f;

        graphics.DrawLine(pen, left, top, right, bottom);
        graphics.DrawLine(pen, left, bottom, right, top);
    }

    private static float Stroke(int size) => MathF.Max(2f, MathF.Round(size * 0.13f));

    private static Icon ToIcon(Bitmap bitmap)
    {
        IntPtr handle = bitmap.GetHicon();
        try
        {
            using Icon borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone(); // owns its own handle, so the GDI one can go
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
