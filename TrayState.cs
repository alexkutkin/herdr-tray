namespace HerdrTray;

/// <summary>Aggregated state of the tray icon, declared in priority order.</summary>
internal enum TrayState
{
    Disconnected,
    NeedsAttention,
    Working,
    Idle,
}

/// <summary>Agent state, declared in tooltip priority order.</summary>
internal enum AgentState
{
    Blocked,
    Working,
    Idle,
    Unknown,
}

internal readonly record struct AgentInfo(string Name, AgentState State);

internal sealed record HerdrSnapshot(bool Connected, IReadOnlyList<AgentInfo> Agents)
{
    public static readonly HerdrSnapshot Disconnected = new(false, []);

    public TrayState State =>
        !Connected ? TrayState.Disconnected
        : Agents.Any(agent => agent.State == AgentState.Blocked) ? TrayState.NeedsAttention
        : Agents.Any(agent => agent.State == AgentState.Working) ? TrayState.Working
        : TrayState.Idle;
}
