using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HerdrTray;

/// <summary>The whole application: one tray icon, a 2 second poll, and Exit.</summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int PollIntervalMs = 2000;
    private const int MaxTooltipLength = 63; // .NET Framework throws above 63, unlike the shell's 127
    private const string Header = "Herdr Tray Utility";

    private readonly Dictionary<TrayState, Icon> _icons;
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;

    private StatusWallForm? _wall;
    private ProgressStripForm? _strip;
    private HerdrSnapshot _snapshot = HerdrSnapshot.Disconnected;
    private TrayState? _shownState = TrayState.Disconnected;
    private bool _polling;
    private bool _exiting;

    public TrayApplicationContext()
    {
        // A moved portable folder keeps autostart working: rewrite the stored path on every launch.
        if (Autostart.IsEnabled)
        {
            Autostart.TrySetEnabled(true);
        }

        _menu = TrayMenu.Create(ShowWall, Exit);
        _icons = TrayIcons.Create();

        // Stay neutral until the first poll: a wrong "Disconnected" would stick in the shell's cache.
        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Icon = _icons[TrayState.Disconnected],
            Text = Header,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowWall();
            }
        };
        _notifyIcon.DoubleClick += (_, _) => ShowWall();
        _notifyIcon.Visible = true;

        _timer = new System.Windows.Forms.Timer { Interval = PollIntervalMs };
        _timer.Tick += async (_, _) => await PollAsync();
        _timer.Start();

        if (_settings.StripEnabled)
        {
            StartStrip();
        }

        _ = PollAsync();
    }

    private async Task PollAsync()
    {
        if (_polling || _exiting)
        {
            return;
        }

        _polling = true;
        try
        {
            HerdrSnapshot snapshot = await HerdrStatusReader.ReadAsync();
            if (!_exiting)
            {
                Show(snapshot);
            }
        }
        finally
        {
            _polling = false;
        }
    }

    /// <summary>Opens the status wall, or brings it back if it is already open.</summary>
    private void ShowWall()
    {
        _wall ??= new StatusWallForm(_settings, SetStripEnabled);
        _wall.Apply(_snapshot);

        if (!_wall.Visible)
        {
            _wall.Show();
            return;
        }

        if (_wall.WindowState == FormWindowState.Minimized)
        {
            _wall.WindowState = FormWindowState.Normal;
        }

        _wall.Activate();
    }

    private void Show(HerdrSnapshot snapshot)
    {
        _snapshot = snapshot;
        _wall?.Apply(snapshot);
        _strip?.Apply(snapshot);

        TrayState state = snapshot.State;
        if (_shownState != state)
        {
            _shownState = state;
            _notifyIcon.Icon = _icons[state];
        }

        string tooltip = BuildTooltip(snapshot);
        if (tooltip != _notifyIcon.Text)
        {
            _notifyIcon.Text = tooltip;
        }
    }

    /// <summary>Who is doing what, trimmed to fit the tooltip, attention first.</summary>
    private static string BuildTooltip(HerdrSnapshot snapshot)
    {
        switch (snapshot.State)
        {
            case TrayState.Disconnected:
                return Compose(new[] { "Disconnected" }, hidden: 0);
            case TrayState.Idle:
                return Compose(new[] { "Idle" }, hidden: 0);
        }

        // OrderBy is stable, so Herdr's own order survives inside each state group.
        List<string> lines = snapshot.Agents
            .OrderBy(agent => agent.State)
            .Select(agent => $"{agent.Name} — {Label(agent.State)}")
            .ToList();

        for (int shown = lines.Count; shown > 0; shown--)
        {
            string tooltip = Compose(lines.Take(shown), lines.Count - shown);
            if (tooltip.Length <= MaxTooltipLength)
            {
                return tooltip;
            }
        }

        return Compose(Array.Empty<string>(), lines.Count);
    }

    private static string Compose(IEnumerable<string> lines, int hidden)
    {
        var text = new StringBuilder(Header);
        foreach (string line in lines)
        {
            text.Append('\n').Append(line);
        }

        if (hidden > 0)
        {
            text.Append('\n').Append('+').Append(hidden).Append(hidden == 1 ? " agent" : " agents");
        }

        return text.ToString();
    }

    internal static string Label(AgentState state) => state switch
    {
        AgentState.Blocked => "needs input",
        AgentState.Working => "working",
        AgentState.Idle => "idle",
        _ => "unknown",
    };

    /// <summary>Turns the progress strip on or off; the single writer of the StripEnabled option.</summary>
    private void SetStripEnabled(bool enabled)
    {
        _settings.StripEnabled = enabled;
        _settings.Save();

        if (enabled)
        {
            StartStrip();
        }
        else
        {
            _strip?.SetEnabled(false);
        }
    }

    private void StartStrip()
    {
        if (_strip is null)
        {
            _strip = new ProgressStripForm(ShowWall, () => SetStripEnabled(false));
            _strip.RestoreLocation();
        }

        _strip.SetEnabled(true);
        _strip.Apply(_snapshot);
    }

    private void Exit()
    {
        _exiting = true;
        _timer.Stop();
        _timer.Dispose();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();

        _wall?.Dispose();
        _strip?.Dispose();

        foreach (Icon icon in _icons.Values)
        {
            icon.Dispose();
        }

        ExitThread();
    }
}
