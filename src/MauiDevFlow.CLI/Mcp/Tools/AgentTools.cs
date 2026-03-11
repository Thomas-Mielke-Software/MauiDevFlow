using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using MauiDevFlow.CLI.Mcp;
using MauiDevFlow.CLI.Broker;
using MauiDevFlow.Driver;

namespace MauiDevFlow.CLI.Mcp.Tools;

[McpServerToolType]
public sealed class AgentTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    [McpServerTool(Name = "maui_list_agents"), Description("List all connected MAUI DevFlow agents (running apps). Shows app name, platform, port, and uptime.")]
    public static async Task<string> ListAgents(McpAgentSession session)
    {
        var agents = await session.ListAgentsAsync();
        if (agents == null || agents.Length == 0)
            return "No agents connected. Build and run a MAUI app with MauiDevFlow.Agent configured.";

        var result = agents.Select(a => new
        {
            a.Id,
            a.AppName,
            a.Platform,
            a.Tfm,
            a.Port,
            a.Version,
            uptime = (DateTime.UtcNow - a.ConnectedAt).ToString(@"hh\:mm\:ss")
        });

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "maui_status"), Description("Get detailed status of a connected MAUI DevFlow agent including platform, device type, app name, and version.")]
    public static async Task<string> Status(
        McpAgentSession session,
        [Description("Agent HTTP port (optional if only one agent connected)")] int? agentPort = null,
        [Description("Window index for multi-window apps")] int? window = null)
    {
        var agent = await session.GetAgentClientAsync(agentPort);
        var status = await agent.GetStatusAsync(window);
        if (status == null)
            return "Agent not responding. Is the app running?";

        return JsonSerializer.Serialize(status, JsonOptions);
    }

    [McpServerTool(Name = "maui_wait"), Description("Wait for a MAUI DevFlow agent to connect. Blocks until an agent registers with the broker or timeout is reached.")]
    public static async Task<string> Wait(
        McpAgentSession session,
        [Description("Timeout in seconds (default: 30)")] int timeout = 30,
        [Description("Wait for a specific app name")] string? app = null)
    {
        var brokerPort = await session.GetBrokerPortAsync();
        var deadline = DateTime.UtcNow.AddSeconds(timeout);

        while (DateTime.UtcNow < deadline)
        {
            var agents = await BrokerClient.ListAgentsAsync(brokerPort);
            if (agents != null && agents.Length > 0)
            {
                var match = app != null
                    ? agents.FirstOrDefault(a => a.AppName?.Contains(app, StringComparison.OrdinalIgnoreCase) == true)
                    : agents.FirstOrDefault();

                if (match != null)
                {
                    session.DefaultAgentPort = match.Port;
                    return JsonSerializer.Serialize(new
                    {
                        match.Id,
                        match.AppName,
                        match.Platform,
                        match.Tfm,
                        match.Port,
                        match.Version
                    }, JsonOptions);
                }
            }

            await Task.Delay(500);
        }

        return $"Timeout after {timeout}s — no agent connected" + (app != null ? $" matching '{app}'" : "") + ".";
    }

    [McpServerTool(Name = "maui_select_agent"), Description("Set the default agent for this MCP session. Subsequent tool calls will use this agent automatically without needing agentPort.")]
    public static string SelectAgent(
        McpAgentSession session,
        [Description("Agent HTTP port to use as default")] int agentPort)
    {
        session.DefaultAgentPort = agentPort;
        return $"Default agent set to port {agentPort}. All subsequent commands will use this agent.";
    }
}
