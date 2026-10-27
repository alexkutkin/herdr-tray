using System;
using System.Collections.Generic;
using System.Linq;

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

internal readonly struct AgentInfo
{
    public AgentInfo(string name, AgentState state)
    {
        Name = name;
        State = state;
    }

    public string Name { get; }

    public AgentState State { get; }
}

internal static class AgentStates
{
    public static TrayState ToTrayState(this AgentState state) => state switch
    {
        AgentState.Blocked => TrayState.NeedsAttention,
        AgentState.Working => TrayState.Working,
        _ => TrayState.Idle,
    };
}

internal sealed class HerdrSnapshot
{
    public static readonly HerdrSnapshot Disconnected = new HerdrSnapshot(false, Array.Empty<AgentInfo>());

    public HerdrSnapshot(bool connected, IReadOnlyList<AgentInfo> agents)
    {
        Connected = connected;
        Agents = agents;
    }

    public bool Connected { get; }

    public IReadOnlyList<AgentInfo> Agents { get; }

    public TrayState State =>
        !Connected ? TrayState.Disconnected
        : Agents.Any(agent => agent.State == AgentState.Blocked) ? TrayState.NeedsAttention
        : Agents.Any(agent => agent.State == AgentState.Working) ? TrayState.Working
        : TrayState.Idle;
}
