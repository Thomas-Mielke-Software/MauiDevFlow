using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using MauiDevFlow.CLI.Mcp;

namespace MauiDevFlow.CLI.Mcp.Tools;

[McpServerToolType]
public sealed class PropertyTools
{
	private static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromSeconds(10) };

	[McpServerTool(Name = "maui_get_property"), Description("Get the value of a property on a UI element (e.g., Text, IsVisible, BackgroundColor, SelectedIndex).")]
	public static async Task<string> GetProperty(
		McpAgentSession session,
		[Description("Element ID from the visual tree")] string elementId,
		[Description("Property name (e.g., 'Text', 'IsVisible', 'BackgroundColor')")] string property,
		[Description("Agent HTTP port (optional if only one agent connected)")] int? agentPort = null)
	{
		var agent = await session.GetAgentClientAsync(agentPort);
		var value = await agent.GetPropertyAsync(elementId, property);
		return value ?? $"Property '{property}' not found on element '{elementId}'.";
	}

	[McpServerTool(Name = "maui_set_property"), Description("Set a property value on a UI element at runtime (e.g., Text, IsVisible, BackgroundColor, SelectedIndex).")]
	public static async Task<string> SetProperty(
		McpAgentSession session,
		[Description("Element ID from the visual tree")] string elementId,
		[Description("Property name (e.g., 'Text', 'IsVisible', 'BackgroundColor')")] string property,
		[Description("New value for the property")] string value,
		[Description("Agent HTTP port (optional if only one agent connected)")] int? agentPort = null)
	{
		var agent = await session.GetAgentClientAsync(agentPort);
		var json = JsonSerializer.Serialize(new { elementId, property, value });
		var content = new StringContent(json, Encoding.UTF8, "application/json");
		try
		{
			var response = await s_http.PostAsync($"{agent.BaseUrl}/api/action/set-property", content);
			var body = await response.Content.ReadAsStringAsync();
			var result = JsonSerializer.Deserialize<JsonElement>(body);
			if (result.TryGetProperty("success", out var s) && s.GetBoolean())
				return $"Set '{property}' = '{value}' on element '{elementId}'.";
			var error = result.TryGetProperty("error", out var e) ? e.GetString() : "Unknown error";
			return $"Failed to set property: {error}";
		}
		catch (Exception ex)
		{
			return $"Failed to set property: {ex.Message}";
		}
	}
}
