using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HerdrTray;

/// <summary>
/// A check box drawn as a pill switch: label, then a track with a knob. Keeps CheckBox's
/// keyboard, focus and accessibility behaviour and replaces only the glyph, so it matches
/// the hand-drawn status wall instead of dropping a light-theme check box onto it.
/// </summary>
internal sealed class ToggleSwitch : CheckBox
{
    private static readonly Color TrackOff = Color.FromArgb(0x3A, 0x3A, 0x40);
    private static readonly Color TrackOffHover = Color.FromArgb(0x4A, 0x4A, 0x52);
    private static readonly Color TrackOn = Color.FromArgb(0x4D, 0xA3, 0xFF);
    private static readonly Color TrackOnHover = Color.FromArgb(0x6E, 0xB5, 0xFF);
    private static readonly Color Knob = Color.FromArgb(0xFF, 0xFF, 0xFF);

    private bool _hover;

    public ToggleSwitch()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
            true);
        Cursor = Cursors.Hand;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        Size text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.SingleLine);
        return new Size(text.Width + Dpi(10) + Dpi(40), Math.Max(text.Height, Dpi(22)));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        int trackWidth = Dpi(40);
        int trackHeight = Dpi(22);
        var track = new Rectangle(Width - trackWidth, (Height - trackHeight) / 2, trackWidth, trackHeight);

        var textArea = new Rectangle(0, 0, Width - trackWidth - Dpi(10), Height);
        TextRenderer.DrawText(graphics, Text, Font, textArea, ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
            TextFormatFlags.NoPrefix);

        if (Focused && ShowFocusCues)
        {
            Size textBounds = TextRenderer.MeasureText(graphics, Text, Font, Size.Empty, TextFormatFlags.SingleLine);
            ControlPaint.DrawFocusRectangle(graphics, new Rectangle(0, 0, textBounds.Width + 2, Height), ForeColor, BackColor);
        }

        using (GraphicsPath shape = Pill(track, trackHeight / 2f))
        using (var brush = new SolidBrush(TrackColor()))
        {
            graphics.FillPath(brush, shape);
        }

        int inset = Dpi(3);
        int knobSize = trackHeight - (inset * 2);
        int knobX = Checked ? track.Right - inset - knobSize : track.Left + inset;
        using (var brush = new SolidBrush(Knob))
        {
            graphics.FillEllipse(brush, knobX, track.Top + inset, knobSize, knobSize);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        Invalidate();
        base.OnCheckedChanged(e);
    }

    private Color TrackColor() => Checked
        ? (_hover ? TrackOnHover : TrackOn)
        : (_hover ? TrackOffHover : TrackOff);

    private int Dpi(int value) => (int)Math.Round(value * DeviceDpi / 96f);

    private static GraphicsPath Pill(Rectangle rect, float radius)
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
}
