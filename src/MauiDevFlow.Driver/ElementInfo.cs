using System.Text.Json.Serialization;

namespace MauiDevFlow.Driver;

/// <summary>
/// Element info returned from the Agent API (deserialization model).
/// </summary>
public class ElementInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("fullType")]
    public string FullType { get; set; } = string.Empty;

    [JsonPropertyName("automationId")]
    public string? AutomationId { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("isFocused")]
    public bool IsFocused { get; set; }

    [JsonPropertyName("opacity")]
    public double Opacity { get; set; }

    [JsonPropertyName("bounds")]
    public BoundsInfo? Bounds { get; set; }

    [JsonPropertyName("children")]
    public List<ElementInfo>? Children { get; set; }
}

public class BoundsInfo
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("width")]
    public double Width { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }
}
