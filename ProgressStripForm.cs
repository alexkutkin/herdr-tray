using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HerdrTray;

/// <summary>
/// A thin always-on-top strip that mirrors the working and blocked agents as coloured
/// segments and hides itself again after a grace period of idleness. Its visibility
/// is a single state machine (StripPhase); dragging and location persistence are built in.
/// </summary>
internal sealed class ProgressStripForm : Form
{
    /// <summary>Strip visibility, in the order the feature passes through it.</summary>
    private enum StripPhase { Disabled, Dormant, Shown, Releasing }

    private const int WsExNoActivate = 0x08000000;
    private const int WmDpiChanged = 0x02E0;
    private const int TimerIntervalMs = 40; // ~25 Hz is enough for the pulse and the fades
    private const double FadeInMs = 300;
    private const double FadeOutMs = 600;
    private const double ReleaseGraceMs = 10000;
    private const double PulsePeriodMs = 1200;
    private const double PulseMinAlpha = 0.55;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmRound = 2;

    // Same palette as the status wall, copied to keep the two files independent.
    private static readonly Color TileBackground = Color.FromArgb(0x24, 0x24, 0x28);
    private static readonly Color TextPrimary = Color.FromArgb(0xE8, 0xE8, 0xE8);
    private static readonly Color Divider = Color.FromArgb(0x3A, 0x3A, 0x40);

    private readonly Action _openWall;
    private readonly Action _turnOff;
    private readonly ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _animation;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private HerdrSnapshot _snapshot = HerdrSnapshot.Disconnected;
    private List<AgentInfo> _agents = new();
    private StripPhase _phase = StripPhase.Dormant;
    private int _segments;
    private bool _blockedPresent;
    private double _fadeFrom;
    private double _fadeStart;
    private double _releaseStart;

    // The DPI the frame is laid out in. The framework refreshes its DeviceDpi cache inside
    // its own WM_DPICHANGED handling, which WndProc swallows, so layout must read this.
    private uint _frameDpi;

    // Drag input state: cursor position on button down, null when no button is held.
    private Point? _dragAnchor;
    private Point _dragOrigin;
    private bool _dragging;

    public ProgressStripForm(Action openWall, Action turnOff)
    {
        _openWall = openWall;
        _turnOff = turnOff;

        // A resize must repaint the whole capsule (segments re-center), and AutoScaleMode.None
        // keeps the framework from re-scaling the already measured frame; OnHandleCreated and
        // OnDpiChangedAfterParent re-assert it at the real monitor scale instead.
        SetStyle(ControlStyles.ResizeRedraw, true);
        AutoScaleMode = AutoScaleMode.None;

        Text = "Herdr Tray Utility";
        // Just a draggable, always-on-top overlay, deliberately NOT a Win32 appbar:
        // no screen-edge space is reserved via SHAppBarMessage.
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false; // WS_EX_TOOLWINDOW: stays out of the taskbar and Alt+Tab
        StartPosition = FormStartPosition.Manual;
        Cursor = Cursors.SizeAll;
        DoubleBuffered = true;
        TopMost = true;

        _menu = CreateMenu();
        ContextMenuStrip = _menu;

        _animation = new System.Windows.Forms.Timer { Interval = TimerIntervalMs };
        _animation.Tick += (_, _) => Animate();

        _frameDpi = (uint)DeviceDpi;
        Reshape(SizeOf(0, _frameDpi));
    }

    /// <summary>Feeds the strip a fresh status snapshot and advances the state machine.</summary>
    public void Apply(HerdrSnapshot snapshot)
    {
        _snapshot = snapshot;

        // Only working or blocked agents get a segment; the wall still shows the full list.
        _agents = snapshot.Agents
            .Where(agent => agent.State is AgentState.Working or AgentState.Blocked)
            .OrderBy(agent => agent.State)
            .ToList();
        _blockedPresent = _agents.Any(agent => agent.State == AgentState.Blocked);

        // The form is sized to the active agent list, so re-layout it whenever that count changes.
        if (_agents.Count != _segments)
        {
            _segments = _agents.Count;
            Reshape(SizeOf(_segments, _frameDpi));
        }

        bool active = HasActivity(snapshot);
        switch (_phase)
        {
            case StripPhase.Dormant or StripPhase.Releasing when active:
                EnterShown();
                break;
            case StripPhase.Shown when !active:
                EnterReleasing();
                break;
        }

        if (Visible)
        {
            Invalidate();
        }
    }

    /// <summary>Moves the strip between its disabled state and the running state machine.</summary>
    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            if (_phase is StripPhase.Shown or StripPhase.Releasing)
            {
                return;
            }

            if (HasActivity(_snapshot))
            {
                EnterShown();
            }
            else
            {
                _phase = StripPhase.Dormant;
            }
        }
        else if (_phase != StripPhase.Disabled)
        {
            _phase = StripPhase.Disabled;
            Hide();
        }
    }

    /// <summary>Puts the strip on its saved position, or the default corner when that is off-screen.</summary>
    public void RestoreLocation()
    {
        AppSettings settings = AppSettings.Load();
        if (settings.StripX is int x && settings.StripY is int y
            && SystemInformation.VirtualScreen.Contains(new Point(x, y)))
        {
            Location = new Point(x, y);
            return;
        }

        Rectangle area = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(area.Right - Width - Dpi(24), area.Bottom - Height - Dpi(24));
    }

    // The net48 equivalent of ShowActivated = false: showing the strip must not steal focus.
    protected override bool ShowWithoutActivation => true;

    // The rounded background needs a matching clipped window region, since Opacity fades
    // turn the layered style off again and per-pixel alpha corners would flicker.
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            // WS_EX_NOACTIVATE: clicking a floating overlay must not steal keyboard focus
            // from whatever the user is working in.
            parameters.ExStyle |= WsExNoActivate;
            return parameters;
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            _animation.Start();
        }
        else
        {
            _animation.Stop();
        }
    }

    // The frame was first measured pre-handle at the process-default DPI; now the real
    // monitor is known, so re-assert the frame at the current DeviceDpi.
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _frameDpi = (uint)DeviceDpi;
        Reshape(SizeOf(_segments, _frameDpi));
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        _frameDpi = (uint)DeviceDpi;
        Reshape(SizeOf(_segments, _frameDpi));
    }

    // The OS-suggested WM_DPICHANGED rectangle assumes legacy 96-DPI scaling and re-applies
    // against the frame we already manage ourselves, inflating an axis (observed 23 -> 47 px
    // height at 120 DPI); the change is handled here instead, so the message never reaches
    // the base class and the suggested bounds are simply ignored.
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmDpiChanged)
        {
            // LOWORD of WParam carries the new DPI of the monitor the window moved to.
            _frameDpi = (uint)(m.WParam.ToInt64() & 0xFFFF);
            Reshape(SizeOf(_segments, _frameDpi));
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _dragAnchor = Cursor.Position;
            _dragOrigin = Location;
            _dragging = false;
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragAnchor is Point anchor)
        {
            Point at = Cursor.Position;
            if (!_dragging && IsDragMove(anchor, at))
            {
                _dragging = true;
            }

            if (_dragging)
            {
                Location = ClampInto(anchor, at);
            }
        }

        base.OnMouseMove(e);
    }

    private bool IsDragMove(Point anchor, Point at) =>
        Math.Abs(at.X - anchor.X) > Dpi(5) || Math.Abs(at.Y - anchor.Y) > Dpi(5);

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _dragAnchor.HasValue)
        {
            EndDrag();
        }

        base.OnMouseUp(e);
    }

    // Ends a press: a drag persists its position, a plain click opens the wall.
    private void EndDrag()
    {
        bool wasDrag = _dragging;
        _dragAnchor = null;
        _dragging = false;
        if (wasDrag)
        {
            SaveLocation();
        }
        else
        {
            _openWall();
        }
    }

    // A press whose capture was stolen (Alt+Tab, screen saver) must not leave a stale drag.
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (_dragAnchor.HasValue)
        {
            EndDrag();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(TileBackground);

        // All geometry here is proportional to the real ClientSize, so any scale the window
        // lands at draws the same capsule; DeviceDpi reads would only freeze a stale value.
        using (var pen = new Pen(Divider))
        using (GraphicsPath shape = RoundedRectangle(
            new RectangleF(0.5f, 0.5f, ClientSize.Width - 1, ClientSize.Height - 1),
            ClientSize.Height * 0.45f - 0.5f))
        {
            graphics.DrawPath(pen, shape);
        }

        DrawSegments(graphics);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animation.Dispose();
            _menu.Dispose();
        }

        base.Dispose(disposing);
    }

    private static bool HasActivity(HerdrSnapshot snapshot) =>
        snapshot.Connected && snapshot.Agents.Any(agent =>
            agent.State is AgentState.Working or AgentState.Blocked);

    // Window frame only: width from the active count, height Dpi(18). Purely a function of
    // dpi. The minimum leaves room for the "0 working"/"offline" label, which scales from
    // the same Dpi(18) height and therefore never outgrows it.
    private static Size SizeOf(int count, uint dpi) =>
        new(
            Math.Max(Dpi(120, dpi), Dpi(6, dpi) * 2 + count * Dpi(14, dpi) + Math.Max(0, count - 1) * Dpi(3, dpi)),
            Dpi(18, dpi));

    private void Reshape(Size size)
    {
        Size = size;

        // A scale change moves the top-left and can leave the right or bottom edge past the
        // working area or under the taskbar; keep the whole capsule on the screen it sits on.
        Rectangle area = Screen.FromControl(this).WorkingArea;
        int left = Math.Max(area.Left, Math.Min(Location.X, area.Right - Width));
        int top = Math.Max(area.Top, Math.Min(Location.Y, area.Bottom - Height));
        Location = new Point(left, top);

        // The region radius matches the paint's proportional capsule radius, from the same
        // real height, so the clipped corners cannot desync from what is drawn inside.
        using GraphicsPath shape = RoundedRectangle(
            new RectangleF(0, 0, size.Width, size.Height),
            size.Height * 0.45f);
        Region old = Region;
        Region = new Region(shape);
        old?.Dispose();
    }

    // Called both when the strip first appears (from opacity 0) and when activity returns
    // mid-fade-out (from wherever the opacity stands), so both rise back to full.
    private void EnterShown()
    {
        _phase = StripPhase.Shown;
        if (!Visible)
        {
            Opacity = 0;
            Show();
        }

        _fadeFrom = Math.Min(Opacity, 1);
        _fadeStart = _clock.ElapsedMilliseconds;
    }

    private void EnterReleasing()
    {
        _phase = StripPhase.Releasing;
        _releaseStart = _clock.ElapsedMilliseconds;
    }

    private void Animate()
    {
        double now = _clock.ElapsedMilliseconds;

        if (_phase == StripPhase.Shown)
        {
            Rise(now);
            Pulse();
        }
        else if (_phase == StripPhase.Releasing)
        {
            double idle = now - _releaseStart;
            if (idle < ReleaseGraceMs)
            {
                Pulse();
                return;
            }

            double progress = (idle - ReleaseGraceMs) / FadeOutMs;
            if (progress >= 1)
            {
                _phase = StripPhase.Dormant;
                Hide();
                return;
            }

            Opacity = 1 - progress;
            Pulse();
        }
    }

    private void Rise(double now)
    {
        double progress = (now - _fadeStart) / FadeInMs;
        if (progress < 1)
        {
            double opacity = _fadeFrom + progress * (1 - _fadeFrom);
            if (Opacity != opacity)
            {
                Opacity = opacity;
            }
        }
        else if (Opacity < 1)
        {
            Opacity = 1;
        }
    }

    // Repaints only when someone is blocked: that is the only animated part of the strip.
    private void Pulse()
    {
        if (_blockedPresent)
        {
            Invalidate();
        }
    }

    private void DrawSegments(Graphics graphics)
    {
        float capsuleHeight = ClientSize.Height;
        if (_agents.Count == 0)
        {
            DrawIdleLabel(graphics, capsuleHeight);
            return;
        }

        float segmentHeight = capsuleHeight * 0.44f;
        float segmentWidth = segmentHeight * 1.75f;
        float gap = segmentHeight * 0.38f;
        float total = _agents.Count * segmentWidth + (_agents.Count - 1) * gap;
        float x = (ClientSize.Width - total) / 2f;
        float y = (capsuleHeight - segmentHeight) / 2f;

        foreach (AgentInfo agent in _agents)
        {
            Color color = TrayIcons.ColorOf(agent.State.ToTrayState());
            double alpha = agent.State == AgentState.Blocked ? PulseAlpha() : 1;
            using (GraphicsPath shape = RoundedRectangle(new RectangleF(x, y, segmentWidth, segmentHeight), segmentHeight / 2f))
            using (var brush = new SolidBrush(Color.FromArgb((int)Math.Round(alpha * byte.MaxValue), color)))
            {
                graphics.FillPath(brush, shape);
            }

            x += segmentWidth + gap;
        }
    }

    // While nobody works the strip keeps the grace countdown visible as a quiet label
    // instead of an empty capsule.
    private void DrawIdleLabel(Graphics graphics, float capsuleHeight)
    {
        string text = _snapshot.Connected ? "0 working" : "offline";
        using var font = new Font("Segoe UI", capsuleHeight * 0.45f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.FromArgb(200, TextPrimary));
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString(text, font, brush, new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), format);
    }

    private double PulseAlpha()
    {
        double phase = (_clock.ElapsedMilliseconds % PulsePeriodMs) / PulsePeriodMs;
        double wave = Math.Sin(2 * Math.PI * phase);
        return PulseMinAlpha + (1 - PulseMinAlpha) * (0.5 + 0.5 * wave);
    }

    // Keeps the whole strip inside the working area of the screen the cursor is on while dragging.
    private Point ClampInto(Point anchor, Point at)
    {
        Rectangle area = Screen.FromPoint(at).WorkingArea;
        int left = _dragOrigin.X + at.X - anchor.X;
        int top = _dragOrigin.Y + at.Y - anchor.Y;
        return new Point(
            Math.Max(area.Left, Math.Min(left, area.Right - Width)),
            Math.Max(area.Top, Math.Min(top, area.Bottom - Height)));
    }

    private void SaveLocation() => AppSettings.SaveStripLocation(Left, Top);

    private ContextMenuStrip CreateMenu()
    {
        bool dark = !SystemInformation.HighContrast;
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

        menu.Items.Add(new ToolStripMenuItem("Open status wall", null, (_, _) => _openWall()));
        menu.Items.Add(new ToolStripMenuItem("Turn off", null, (_, _) => _turnOff()));
        return menu;
    }

    private static void RoundCorners(IntPtr window)
    {
        int preference = DwmRound;
        DwmSetWindowAttribute(window, DwmWindowCornerPreference, ref preference, sizeof(int));
    }

    // Midpoint rounds up so the authored Dpi(18) frame lands on 23 px at 120 DPI;
    // Math.Round's default banker's rounding would park 22.5 at 22.
    private static int Dpi(int value, uint dpi) =>
        (int)Math.Round(value * dpi / 96.0, MidpointRounding.AwayFromZero);

    private int Dpi(int value) => Dpi(value, _frameDpi);

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

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
