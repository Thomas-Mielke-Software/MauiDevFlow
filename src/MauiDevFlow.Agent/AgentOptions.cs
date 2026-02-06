namespace MauiDevFlow.Agent;

/// <summary>
/// Configuration options for the MauiDevFlow Agent.
/// </summary>
public class AgentOptions
{
    /// <summary>
    /// Port for the HTTP API server. Default: 9223.
    /// </summary>
    public int Port { get; set; } = 9223;

    /// <summary>
    /// Whether the agent is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum tree walk depth. 0 = unlimited. Default: 0.
    /// </summary>
    public int MaxTreeDepth { get; set; } = 0;
}
