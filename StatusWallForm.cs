using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HerdrTray;

/// <summary>
/// The status wall: one tile per agent, a big coloured circle each, sized to the window
/// so it stays readable from across the room. Closing hides it; polling continues.
/// </summary>
internal sealed class StatusWallForm : Form
{
    private const int Gap = 16;
    private const uint EsContinuous = 0x80000000;
    private const uint EsDisplayRequired = 0x00000002;

    private static readonly Color Background = Color.FromArgb(0x14, 0x14, 0x16);
    private static readonly Color TileBackground = Color.FromArgb(0x24, 0x24, 0x28);
    private static readonly Color TextPrimary = Color.FromArgb(0xE8, 0xE8, 0xE8);
    private static readonly Color AlertInk = Color.FromArgb(0x1A, 0x13, 0x00);
    private static readonly Color Divider = Color.FromArgb(0x3A, 0x3A, 0x40);

    private readonly Icon _appIcon = AppIcon.Load(32);
    private readonly AppSettings _settings;
    private readonly CheckBox _keepAwake;
    private readonly CheckBox _onTop;
    private readonly CheckBox _autostart;
    private readonly FlowLayoutPanel _optionsBar;

    private HerdrSnapshot _snapshot = HerdrSnapshot.Disconnected;
    private bool _revertingAutostart;

    public StatusWallForm(AppSettings settings)
    {
        _settings = settings;

        Text = "Herdr Tray Utility";
        Icon = _appIcon;
        BackColor = Background;
        ForeColor = TextPrimary;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(760, 460);
        MinimumSize = new Size(320, 240);
        DoubleBuffered = true;
        KeyPreview = true;
        TopMost = settings.AlwaysOnTop;

        // Options sit on the wall itself, so they are one click away while the agents are in view.
        _keepAwake = Option("Keep the screen awake", settings.KeepAwake);
        _keepAwake.CheckedChanged += (_, _) =>
        {
            _settings.KeepAwake = _keepAwake.Checked;
            SaveAndApply();
        };

        _onTop = Option("Always on top", settings.AlwaysOnTop);
        _onTop.CheckedChanged += (_, _) =>
        {
            _settings.AlwaysOnTop = _onTop.Checked;
            SaveAndApply();
        };

        _autostart = Option("Start with Windows", Autostart.IsEnabled);
        _autostart.CheckedChanged += (_, _) =>
        {
            if (_revertingAutostart)
            {
                return;
            }

            if (Autostart.TrySetEnabled(_autostart.Checked))
            {
                return;
            }

            _revertingAutostart = true;
            _autostart.Checked = !_autostart.Checked;
            _revertingAutostart = false;
            MessageBox.Show(this, "Could not update the Windows startup setting.", "Herdr Tray Utility",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };

        _optionsBar = OptionsBar();
        Controls.Add(_optionsBar);
        Apply(_snapshot);
    }

    public void Apply(HerdrSnapshot snapshot)
    {
        _snapshot = snapshot;
        if (Visible)
        {
            Invalidate();
        }
    }

    private void ApplySettings()
    {
        TopMost = _settings.AlwaysOnTop;
        UpdateKeepAwake();
    }

    private void SaveAndApply()
    {
        _settings.Save();
        ApplySettings();
    }

    private static ToggleSwitch Option(string text, bool check) => new()
    {
        Text = text,
        Checked = check,
        AutoSize = true,
        BackColor = Background,
        ForeColor = TextPrimary,
        Font = SystemFonts.MenuFont,
        Margin = new Padding(0, 3, 28, 3),
    };

    private FlowLayoutPanel OptionsBar()
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            BackColor = Background,
            Padding = new Padding(16, 10, 16, 10),
        };
        bar.Controls.AddRange(new Control[] { _keepAwake, _onTop, _autostart });
        bar.Paint += (_, e) =>
        {
            using var pen = new Pen(Divider);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };
        return bar;
    }

    // A light title bar over a dark wall looks broken; ignored on Windows versions without it.
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark = 1;
        DwmSetWindowAttribute(Handle, DwmUseImmersiveDarkMode, ref dark, sizeof(int));
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            // The Run entry can change while the wall is hidden; show its real state, don't write it.
            _revertingAutostart = true;
            _autostart.Checked = Autostart.IsEnabled;
            _revertingAutostart = false;
        }

        UpdateKeepAwake();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Hide();
        }

        base.OnKeyDown(e);
    }

    // The cross hides the wall rather than quitting Herdr Tray, so the size survives until restart.
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SetThreadExecutionState(EsContinuous);
            _appIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private void UpdateKeepAwake() =>
        SetThreadExecutionState(Visible && _settings.KeepAwake
            ? EsContinuous | EsDisplayRequired
            : EsContinuous);

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(Background);

        var area = new RectangleF(0, 0, ClientSize.Width, ClientSize.Height - _optionsBar.Height);
        if (!_snapshot.Connected)
        {
            DrawMessage(graphics, area, "Disconnected", TrayIcons.ColorOf(TrayState.Disconnected));
            return;
        }

        if (_snapshot.Agents.Count == 0)
        {
            DrawMessage(graphics, area, "No agents", TrayIcons.ColorOf(TrayState.Idle));
            return;
        }

        // Same order as the tooltip: whoever waits for you comes first.
        List<AgentInfo> agents = _snapshot.Agents.OrderBy(agent => agent.State).ToList();
        DrawTiles(graphics, area, agents);
    }

    private static void DrawTiles(Graphics graphics, RectangleF area, List<AgentInfo> agents)
    {
        int count = agents.Count;
        int columns = Clamp((int)Math.Round(Math.Sqrt(count * area.Width / Math.Max(1f, area.Height))), 1, count);
        int rows = (int)Math.Ceiling(count / (double)columns);
        columns = (int)Math.Ceiling(count / (double)rows);

        float tileWidth = (area.Width - Gap * (columns + 1)) / columns;
        float tileHeight = (area.Height - Gap * (rows + 1)) / rows;

        for (int index = 0; index < count; index++)
        {
            int row = index / columns;
            int column = index % columns;
            int inRow = Math.Min(columns, count - row * columns);

            // Centre a short last row instead of leaving a hole on the right.
            float rowWidth = inRow * tileWidth + (inRow - 1) * Gap;
            float x = area.X + (area.Width - rowWidth) / 2 + column * (tileWidth + Gap);
            float y = area.Y + Gap + row * (tileHeight + Gap);

            DrawTile(graphics, new RectangleF(x, y, tileWidth, tileHeight), agents[index]);
        }
    }

    private static void DrawTile(Graphics graphics, RectangleF rect, AgentInfo agent)
    {
        Color accent = TrayIcons.ColorOf(agent.State.ToTrayState());
        bool alert = agent.State == AgentState.Blocked;
        Color ink = alert ? AlertInk : TextPrimary;

        using (GraphicsPath shape = RoundedRectangle(rect, Math.Min(28f, rect.Height * 0.14f)))
        using (var brush = new SolidBrush(alert ? accent : TileBackground))
        {
            graphics.FillPath(brush, shape);
        }

        float diameter = Math.Min(rect.Width * 0.44f, rect.Height * 0.44f);
        using (var brush = new SolidBrush(alert ? ink : accent))
        {
            graphics.FillEllipse(brush, rect.X + (rect.Width - diameter) / 2, rect.Y + rect.Height * 0.14f, diameter, diameter);
        }

        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        using var nameFont = new Font("Segoe UI", Clamp(rect.Height * 0.11f, 9f, 56f), FontStyle.Bold, GraphicsUnit.Pixel);
        using var stateFont = new Font("Segoe UI", Clamp(rect.Height * 0.085f, 8f, 40f), FontStyle.Regular, GraphicsUnit.Pixel);
        using var nameBrush = new SolidBrush(ink);
        using var stateBrush = new SolidBrush(alert ? ink : accent);

        graphics.DrawString(agent.Name, nameFont, nameBrush,
            new RectangleF(rect.X, rect.Y + rect.Height * 0.62f, rect.Width, rect.Height * 0.16f), format);
        graphics.DrawString(TrayApplicationContext.Label(agent.State), stateFont, stateBrush,
            new RectangleF(rect.X, rect.Y + rect.Height * 0.79f, rect.Width, rect.Height * 0.14f), format);
    }

    private static void DrawMessage(Graphics graphics, RectangleF area, string text, Color color)
    {
        float diameter = Math.Min(area.Width, area.Height) * 0.3f;
        using (var brush = new SolidBrush(color))
        {
            graphics.FillEllipse(brush, area.X + (area.Width - diameter) / 2, area.Y + area.Height * 0.22f, diameter, diameter);
        }

        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var font = new Font("Segoe UI", Clamp(area.Height * 0.09f, 10f, 64f), FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(color);
        graphics.DrawString(text, font, textBrush, new RectangleF(area.X, area.Y + area.Height * 0.62f, area.Width, area.Height * 0.18f), format);
    }

    private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);

    private static float Clamp(float value, float min, float max) => Math.Min(Math.Max(value, min), max);

    private static GraphicsPath RoundedRectangle(RectangleF rect, float radius)
    {
        float diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private const int DwmUseImmersiveDarkMode = 20;

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
