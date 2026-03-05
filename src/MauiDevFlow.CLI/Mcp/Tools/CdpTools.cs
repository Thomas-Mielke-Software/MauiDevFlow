using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;
using MauiDevFlow.CLI.Mcp;

namespace MauiDevFlow.CLI.Mcp.Tools;

[McpServerToolType]
public sealed class CdpTools
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    [McpServerTool(Name = "maui_cdp_evaluate"), Description("Execute JavaScript in a Blazor WebView via Chrome DevTools Protocol. Returns the evaluation result.")]
    public static async Task<string> CdpEvaluate(
        McpAgentSession session,
        [Description("JavaScript expression to evaluate")] string expression,
        [Description("WebView ID or index to target (optional if only one WebView)")] string? webviewId = null,
        [Description("Agent HTTP port (optional if only one agent connected)")] int? agentPort = null)
    {
        var agent = await session.GetAgentClientAsync(agentPort);
        var url = $"{agent.BaseUrl}/api/cdp";
        if (webviewId != null)
            url += $"?webview={Uri.EscapeDataString(webviewId)}";

        var body = JsonSerializer.Serialize(new
        {
            method = "Runtime.evaluate",
            @params = new { expression, returnByValue = true }
        });

        var response = await _http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
        var content = await response.Content.ReadAsStringAsync();

        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(content);
            if (json.TryGetProperty("result", out var result) &&
                result.TryGetProperty("result", out var inner) &&
                inner.TryGetProperty("value", out var value))
            {
                return value.ToString();
            }
            return content;
        }
        catch
        {
            return content;
        }
    }

    [McpServerTool(Name = "maui_cdp_screenshot"), Description("Capture a screenshot of a Blazor WebView via Chrome DevTools Protocol. Returns the image directly.")]
    public static async Task<ContentBlock[]> CdpScreenshot(
        McpAgentSession session,
        [Description("WebView ID or index to target (optional if only one WebView)")] string? webviewId = null,
        [Description("Agent HTTP port (optional if only one agent connected)")] int? agentPort = null)
    {
        var agent = await session.GetAgentClientAsync(agentPort);
        var url = $"{agent.BaseUrl}/api/cdp";
        if (webviewId != null)
            url += $"?webview={Uri.EscapeDataString(webviewId)}";

        var body = JsonSerializer.Serialize(new
        {
            method = "Page.captureScreenshot",
            @params = new { format = "png" }
        });

        var response = await _http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
        var content = await response.Content.ReadAsStringAsync();

        var json = JsonSerializer.Deserialize<JsonElement>(content);
        if (json.TryGetProperty("result", out var result) &&
            result.TryGetProperty("data", out var data))
        {
            var pngBytes = Convert.FromBase64String(data.GetString()!);
            return [
                new TextContentBlock { Text = $"WebView screenshot captured ({pngBytes.Length} bytes)" },
                ImageContentBlock.FromBytes(pngBytes, "image/png")
            ];
        }

        throw new McpException("Failed to capture WebView screenshot. Is a Blazor WebView active?");
    }

    [McpServerTool(Name = "maui_cdp_source"), Description("Get the HTML source of a Blazor WebView.")]
    public static async Task<string> CdpSource(
        McpAgentSession session,
        [Description("WebView ID or index to target (optional if only one WebView)")] string? webviewId = null,
        [Description("Agent HTTP port (optional if only one agent connected)")] int? agentPort = null)
    {
        var agent = await session.GetAgentClientAsync(agentPort);
        var source = await agent.GetCdpSourceAsync(webviewId);
        return string.IsNullOrEmpty(source) ? "No WebView source available." : source;
    }

    [McpServerTool(Name = "maui_cdp_webviews"), Description("List all registered Blazor WebViews in the running app.")]
    public static async Task<string> CdpWebViews(
        McpAgentSession session,
        [Description("Agent HTTP port (optional if only one agent connected)")] int? agentPort = null)
    {
        var agent = await session.GetAgentClientAsync(agentPort);
        var webviews = await agent.GetCdpWebViewsAsync();
        return webviews.ToString();
    }
}
