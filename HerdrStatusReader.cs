using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace HerdrTray;

/// <summary>
/// Reads agent state from the Herdr CLI. `herdr agent list` already prints JSON:
/// {"id":"cli:agent:list","result":{"agents":[{"agent":"claude","agent_status":"working",...}]}}
/// Failures of any kind (no herdr, no server, bad output) report a disconnected snapshot.
/// </summary>
internal static class HerdrStatusReader
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(5);
    private const int MaxNameLength = 24;

    public static async Task<HerdrSnapshot> ReadAsync()
    {
        try
        {
            string? json = await RunAsync();
            return json is null ? HerdrSnapshot.Disconnected : Parse(json);
        }
        catch
        {
            return HerdrSnapshot.Disconnected;
        }
    }

    private static async Task<string?> RunAsync()
    {
        var startInfo = new ProcessStartInfo("herdr")
        {
            ArgumentList = { "agent", "list" },
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        // Both pipes are drained so the child never blocks on a full buffer.
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(CallTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return null;
        }

        await stderr;
        return process.ExitCode == 0 ? await stdout : null;
    }

    private static HerdrSnapshot Parse(string json)
    {
        using var document = JsonDocument.Parse(json);

        // An error response has no "result", e.g. {"error":{"code":"..."},"id":"cli:agent:list"}.
        if (!document.RootElement.TryGetProperty("result", out JsonElement result) ||
            !result.TryGetProperty("agents", out JsonElement agents) ||
            agents.ValueKind != JsonValueKind.Array)
        {
            return HerdrSnapshot.Disconnected;
        }

        var list = new List<AgentInfo>();
        foreach (JsonElement agent in agents.EnumerateArray())
        {
            list.Add(new AgentInfo(NameOf(agent), StateOf(agent)));
        }

        return new HerdrSnapshot(true, list);
    }

    private static string NameOf(JsonElement agent)
    {
        string? name = Text(agent, "name") ?? Text(agent, "display_agent") ?? Text(agent, "agent");
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Agent";
        }

        name = char.ToUpperInvariant(name[0]) + name[1..];
        return name.Length <= MaxNameLength ? name : name[..(MaxNameLength - 1)] + "…";
    }

    // Herdr's `done` means "finished, ready for input", so it is idle for our purposes.
    private static AgentState StateOf(JsonElement agent) => Text(agent, "agent_status") switch
    {
        "blocked" => AgentState.Blocked,
        "working" => AgentState.Working,
        "idle" or "done" => AgentState.Idle,
        _ => AgentState.Unknown,
    };

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
