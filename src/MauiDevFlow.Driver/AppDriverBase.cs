namespace MauiDevFlow.Driver;

/// <summary>
/// Base driver that delegates most operations to the AgentClient.
/// Platform-specific drivers override platform-dependent methods.
/// </summary>
public abstract class AppDriverBase : IAppDriver
{
    protected AgentClient? Client { get; private set; }

    public abstract string Platform { get; }

    public virtual async Task ConnectAsync(string host = "localhost", int port = 9223)
    {
        await SetupPlatformAsync(host, port);
        Client = new AgentClient(host, port);

        var status = await Client.GetStatusAsync();
        if (status == null)
            throw new InvalidOperationException($"Could not connect to MauiDevFlow Agent at {host}:{port}");
    }

    /// <summary>
    /// Platform-specific setup (e.g., adb reverse for Android).
    /// </summary>
    protected virtual Task SetupPlatformAsync(string host, int port) => Task.CompletedTask;

    public Task<AgentStatus?> GetStatusAsync()
        => EnsureClient().GetStatusAsync();

    public Task<List<ElementInfo>> GetTreeAsync(int maxDepth = 0)
        => EnsureClient().GetTreeAsync(maxDepth);

    public Task<List<ElementInfo>> QueryAsync(string? type = null, string? automationId = null, string? text = null)
        => EnsureClient().QueryAsync(type, automationId, text);

    public Task<bool> TapAsync(string elementId)
        => EnsureClient().TapAsync(elementId);

    public Task<bool> FillAsync(string elementId, string text)
        => EnsureClient().FillAsync(elementId, text);

    public Task<bool> ClearAsync(string elementId)
        => EnsureClient().ClearAsync(elementId);

    public Task<byte[]?> ScreenshotAsync()
        => EnsureClient().ScreenshotAsync();

    public virtual Task BackAsync()
        => Task.CompletedTask;

    public virtual Task PressKeyAsync(string key)
        => Task.CompletedTask;

    protected AgentClient EnsureClient()
        => Client ?? throw new InvalidOperationException("Not connected. Call ConnectAsync first.");

    public virtual void Dispose()
    {
        Client?.Dispose();
    }
}
