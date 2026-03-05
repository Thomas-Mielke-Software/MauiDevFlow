using System.Net.Sockets;
using MauiDevFlow.CLI.Broker;
using MauiDevFlow.Driver;

namespace MauiDevFlow.CLI.Mcp;

public class McpAgentSession
{
	public int? DefaultAgentPort { get; set; }
	public string DefaultAgentHost { get; set; } = "localhost";

	public async Task<AgentClient> GetAgentClientAsync(int? agentPort = null)
	{
		var port = agentPort ?? DefaultAgentPort ?? await ResolveAgentPortAsync();
		return new AgentClient(DefaultAgentHost, port);
	}

	public async Task<int> GetBrokerPortAsync()
	{
		var port = BrokerClient.ReadBrokerPortPublic() ?? BrokerServer.DefaultPort;
		if (!IsTcpAlive(port, timeout: 300))
		{
			var started = await BrokerClient.EnsureBrokerRunningAsync();
			if (started.HasValue)
				port = started.Value;
		}
		return port;
	}

	public async Task<AgentRegistration[]?> ListAgentsAsync()
	{
		var brokerPort = await GetBrokerPortAsync();
		return await BrokerClient.ListAgentsAsync(brokerPort);
	}

	private async Task<int> ResolveAgentPortAsync()
	{
		var brokerPort = BrokerClient.ReadBrokerPortPublic() ?? BrokerServer.DefaultPort;
		var brokerAlive = IsTcpAlive(brokerPort, timeout: 300);

		if (!brokerAlive)
		{
			var started = await BrokerClient.EnsureBrokerRunningAsync();
			if (started.HasValue)
			{
				brokerPort = started.Value;
				brokerAlive = true;
			}
		}

		if (brokerAlive)
		{
			// Try project-specific resolution
			var csprojPath = FindCsprojInCurrentDirectory();
			if (csprojPath is not null)
			{
				var resolved = await BrokerClient.ResolveAgentPortAsync(brokerPort, csprojPath);
				if (resolved.HasValue)
					return resolved.Value;
			}

			// Try auto-select (single agent)
			var auto = await BrokerClient.ResolveAgentPortAsync(brokerPort);
			if (auto.HasValue)
				return auto.Value;
		}

		// Fall back to config file or default
		return ReadConfigPort() ?? 9223;
	}

	private static bool IsTcpAlive(int port, int timeout)
	{
		try
		{
			using var tcp = new TcpClient();
			var result = tcp.BeginConnect("localhost", port, null, null);
			var connected = result.AsyncWaitHandle.WaitOne(timeout);
			if (connected && tcp.Connected)
			{
				tcp.EndConnect(result);
				return true;
			}
			return false;
		}
		catch
		{
			return false;
		}
	}

	private static string? FindCsprojInCurrentDirectory()
	{
		var dir = Directory.GetCurrentDirectory();
		var files = Directory.GetFiles(dir, "*.csproj");
		return files.Length > 0 ? files[0] : null;
	}

	private static int? ReadConfigPort()
	{
		var configPath = Path.Combine(Directory.GetCurrentDirectory(), ".mauidevflow");
		if (!File.Exists(configPath)) return null;
		try
		{
			var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configPath));
			if (json.RootElement.TryGetProperty("port", out var portEl) && portEl.TryGetInt32(out var p))
				return p;
		}
		catch { }
		return null;
	}
}
