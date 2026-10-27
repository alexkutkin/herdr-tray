using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace HerdrTray;

/// <summary>
/// Reads agent state from the Herdr CLI. `herdr agent list` already prints JSON:
/// {"id":"cli:agent:list","result":{"agents":[{"agent":"claude","agent_status":"working",...}]}}
/// Failures of any kind (no herdr, no server, bad output) report a disconnected snapshot.
/// </summary>
internal static class HerdrStatusReader
{
    private const int CallTimeoutMs = 5000;
    private const int MaxNameLength = 24;

    public static async Task<HerdrSnapshot> ReadAsync()
    {
        try
        {
            string? json = await RunAsync().ConfigureAwait(false);
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
            Arguments = "agent list",
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

        bool exited = await Task.Run(() => process.WaitForExit(CallTimeoutMs)).ConfigureAwait(false);
        if (!exited)
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // Already gone.
            }

            return null;
        }

        await stderr.ConfigureAwait(false);
        return process.ExitCode == 0 ? await stdout.ConfigureAwait(false) : null;
    }

    private static HerdrSnapshot Parse(string json)
    {
        var serializer = new DataContractJsonSerializer(typeof(HerdrResponse));
        HerdrResponse? response;
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
        {
            response = (HerdrResponse)serializer.ReadObject(stream);
        }

        // An error response has no "result", e.g. {"error":{"code":"..."},"id":"cli:agent:list"}.
        List<HerdrAgent>? agents = response?.Result?.Agents;
        if (agents is null)
        {
            return HerdrSnapshot.Disconnected;
        }

        var list = new List<AgentInfo>();
        foreach (HerdrAgent agent in agents)
        {
            if (agent != null)
            {
                list.Add(new AgentInfo(NameOf(agent), StateOf(agent)));
            }
        }

        return new HerdrSnapshot(true, list);
    }

    private static string NameOf(HerdrAgent agent)
    {
        string name = FirstNonEmpty(agent.Name, agent.DisplayAgent, agent.Agent) ?? "Agent";
        name = char.ToUpperInvariant(name[0]) + name.Substring(1);
        return name.Length <= MaxNameLength ? name : name.Substring(0, MaxNameLength - 1) + "…";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    // Herdr's `done` means "finished, ready for input", so it is idle for our purposes.
    private static AgentState StateOf(HerdrAgent agent) => agent.AgentStatus switch
    {
        "blocked" => AgentState.Blocked,
        "working" => AgentState.Working,
        "idle" or "done" => AgentState.Idle,
        _ => AgentState.Unknown,
    };

    [DataContract]
    private sealed class HerdrResponse
    {
        [DataMember(Name = "result")]
        public HerdrResult? Result { get; set; }
    }

    [DataContract]
    private sealed class HerdrResult
    {
        [DataMember(Name = "agents")]
        public List<HerdrAgent>? Agents { get; set; }
    }

    [DataContract]
    private sealed class HerdrAgent
    {
        [DataMember(Name = "name")]
        public string? Name { get; set; }

        [DataMember(Name = "display_agent")]
        public string? DisplayAgent { get; set; }

        [DataMember(Name = "agent")]
        public string? Agent { get; set; }

        [DataMember(Name = "agent_status")]
        public string? AgentStatus { get; set; }
    }
}
