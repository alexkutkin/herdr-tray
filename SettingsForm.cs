using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HerdrTray;

/// <summary>
/// Application options. Every change applies and is saved immediately, like the
/// Windows 11 Settings model, so the only commit button is Close.
/// </summary>
internal sealed class SettingsForm : Form
{
    private static readonly Color Background = Color.FromArgb(0x14, 0x14, 0x16);
    private static readonly Color TextPrimary = Color.FromArgb(0xE8, 0xE8, 0xE8);
    private static readonly Color TextSecondary = Color.FromArgb(0xA8, 0xA8, 0xB0);
    private static readonly Color ButtonBackground = Color.FromArgb(0x24, 0x24, 0x28);
    private static readonly Color ButtonBorder = Color.FromArgb(0x3A, 0x3A, 0x40);

    private readonly AppSettings _settings;
    private readonly Action _changed;
    private readonly Icon _appIcon = AppIcon.Load(32);
    private readonly CheckBox _keepAwake;
    private readonly CheckBox _onTop;
    private readonly CheckBox _autostart;
    private bool _revertingAutostart;

    public SettingsForm(AppSettings settings, Action changed)
    {
        _settings = settings;
        _changed = changed;

        Text = "Herdr Tray Utility Settings";
        Icon = _appIcon;
        BackColor = Background;
        ForeColor = TextPrimary;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 198);
        KeyPreview = true;

        _keepAwake = Option("Keep the screen awake while the wall is open", settings.KeepAwake);
        _keepAwake.CheckedChanged += (_, _) =>
        {
            _settings.KeepAwake = _keepAwake.Checked;
            SaveAndApply();
        };

        _onTop = Option("Keep the wall always on top", settings.AlwaysOnTop);
        _onTop.CheckedChanged += (_, _) =>
        {
            _settings.AlwaysOnTop = _onTop.Checked;
            SaveAndApply();
        };

        _autostart = Option("Start with Windows", Autostart.IsEnabled);
        _autostart.CheckedChanged += (_, _) => ApplyAutostart();

        var close = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = ButtonBackground,
            ForeColor = TextPrimary,
            Size = new Size(92, 32),
            UseVisualStyleBackColor = false,
        };
        close.FlatAppearance.BorderColor = ButtonBorder;
        close.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x2E, 0x2E, 0x34);
        close.FlatAppearance.MouseDownBackColor = Color.FromArgb(0x3A, 0x3A, 0x42);

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Background,
        };
        options.Controls.Add(_keepAwake);
        options.Controls.Add(_onTop);
        options.Controls.Add(_autostart);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            BackColor = Background,
        };
        buttons.Controls.Add(close);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Background,
            Padding = new Padding(24, 22, 24, 16),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(options, 0, 0);
        layout.Controls.Add(buttons, 0, 1);
        Controls.Add(layout);

        AcceptButton = close;
        CancelButton = close;
    }

    private static CheckBox Option(string text, bool check) => new()
    {
        Text = text,
        Checked = check,
        AutoSize = true,
        BackColor = Background,
        ForeColor = TextSecondary,
        Margin = new Padding(0, 0, 0, 16),
    };

    private void SaveAndApply()
    {
        _settings.Save();
        _changed();
    }

    private void ApplyAutostart()
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
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark = 1;
        DwmSetWindowAttribute(Handle, DwmUseImmersiveDarkMode, ref dark, sizeof(int));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _appIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private const int DwmUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
