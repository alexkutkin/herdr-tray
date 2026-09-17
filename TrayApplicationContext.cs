using System.Text;

namespace HerdrTray;

/// <summary>The whole application: one tray icon, a 2 second poll, and Exit.</summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int PollIntervalMs = 2000;
    private const int MaxTooltipLength = 127; // NotifyIcon.Text limit
    private const string Header = "Herdr";

    private readonly Dictionary<TrayState, Icon> _icons = TrayIcons.Create();
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;

    private TrayState? _shownState;
    private string _shownTooltip = string.Empty;
    private bool _polling;
    private bool _exiting;

    public TrayApplicationContext()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Exit", null, (_, _) => Exit());

        _notifyIcon = new NotifyIcon { ContextMenuStrip = menu };
        Show(HerdrSnapshot.Disconnected);
        _notifyIcon.Visible = true;

        _timer = new System.Windows.Forms.Timer { Interval = PollIntervalMs };
        _timer.Tick += async (_, _) => await PollAsync();
        _timer.Start();

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

    private void Show(HerdrSnapshot snapshot)
    {
        TrayState state = snapshot.State;
        if (_shownState != state)
        {
            _shownState = state;
            _notifyIcon.Icon = _icons[state];
        }

        string tooltip = BuildTooltip(snapshot);
        if (tooltip != _shownTooltip)
        {
            _shownTooltip = tooltip;
            _notifyIcon.Text = tooltip;
        }
    }

    /// <summary>Who is doing what, trimmed to fit the tooltip, attention first.</summary>
    private static string BuildTooltip(HerdrSnapshot snapshot)
    {
        switch (snapshot.State)
        {
            case TrayState.Disconnected:
                return Compose(["Disconnected"], hidden: 0);
            case TrayState.Idle:
                return Compose(["Idle"], hidden: 0);
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

        return Compose([], lines.Count);
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

    private static string Label(AgentState state) => state switch
    {
        AgentState.Blocked => "needs input",
        AgentState.Working => "working",
        AgentState.Idle => "idle",
        _ => "unknown",
    };

    private void Exit()
    {
        _exiting = true;
        _timer.Stop();
        _timer.Dispose();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        foreach (Icon icon in _icons.Values)
        {
            icon.Dispose();
        }

        ExitThread();
    }
}
