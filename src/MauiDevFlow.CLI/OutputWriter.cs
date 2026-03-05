using System.Text.Json;
using System.Text.Json.Serialization;

namespace MauiDevFlow.CLI;

/// <summary>
/// Central output abstraction for consistent JSON/human output across all CLI commands.
/// JSON mode: raw data on stdout, structured errors on stderr.
/// Human mode: formatted text on stdout, plain errors on stderr.
/// </summary>
static class OutputWriter
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions s_compactJsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Resolves whether JSON output mode is active.
    /// Priority: --no-json flag > --json flag > MAUIDEVFLOW_OUTPUT env var > TTY auto-detection.
    /// </summary>
    public static bool ResolveJsonMode(bool jsonFlag, bool noJsonFlag)
    {
        if (noJsonFlag) return false;
        if (jsonFlag) return true;

        var envVar = Environment.GetEnvironmentVariable("MAUIDEVFLOW_OUTPUT");
        if (string.Equals(envVar, "json", StringComparison.OrdinalIgnoreCase))
            return true;

        if (Console.IsOutputRedirected)
            return true;

        return false;
    }

    /// <summary>
    /// Write a successful result to stdout.
    /// In JSON mode, serializes the data. In human mode, calls the humanFormatter.
    /// </summary>
    public static void WriteResult<T>(T data, bool json, Action<T>? humanFormatter = null)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(data, s_jsonOptions));
        }
        else if (humanFormatter != null)
        {
            humanFormatter(data);
        }
        else
        {
            Console.WriteLine(JsonSerializer.Serialize(data, s_jsonOptions));
        }
    }

    /// <summary>
    /// Write raw JSON string to stdout (for data already serialized or from HTTP responses).
    /// </summary>
    public static void WriteRawJson(string jsonString)
    {
        Console.WriteLine(jsonString);
    }

    /// <summary>
    /// Write a JsonElement to stdout with indentation.
    /// </summary>
    public static void WriteJsonElement(JsonElement element, bool json)
    {
        if (json)
        {
            Console.WriteLine(element.GetRawText());
        }
        else
        {
            Console.WriteLine(JsonSerializer.Serialize(element, s_jsonOptions));
        }
    }

    /// <summary>
    /// Write a simple success action result (for tap, fill, clear, etc.).
    /// </summary>
    public static void WriteActionResult(bool success, string action, string? elementId, bool json, string? humanMessage = null)
    {
        if (json)
        {
            var result = new ActionResult { Success = success, Action = action, ElementId = elementId };
            Console.WriteLine(JsonSerializer.Serialize(result, s_compactJsonOptions));
        }
        else
        {
            Console.WriteLine(humanMessage ?? (success ? $"{action}: {elementId}" : $"Failed to {action.ToLowerInvariant()}: {elementId}"));
        }
    }

    /// <summary>
    /// Write a structured error to stderr and set error state.
    /// In JSON mode, outputs structured error JSON. In human mode, plain text.
    /// </summary>
    public static void WriteError(string message, bool json, string errorType = "RuntimeError",
        bool retryable = false, string[]? suggestions = null)
    {
        if (json)
        {
            var error = new ErrorResult
            {
                Error = message,
                Type = errorType,
                Retryable = retryable,
                Suggestions = suggestions
            };
            Console.Error.WriteLine(JsonSerializer.Serialize(error, s_compactJsonOptions));
        }
        else
        {
            Console.Error.WriteLine($"Error: {message}");
        }
    }

    /// <summary>
    /// Write a single JSONL line (for streaming commands).
    /// </summary>
    public static void WriteJsonLine<T>(T data)
    {
        Console.WriteLine(JsonSerializer.Serialize(data, s_compactJsonOptions));
    }

    /// <summary>
    /// Serialize an object to indented JSON string.
    /// </summary>
    public static string FormatJson<T>(T data)
    {
        return JsonSerializer.Serialize(data, s_jsonOptions);
    }

    // DTOs for structured output

    private class ActionResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("elementId")]
        public string? ElementId { get; set; }
    }

    private class ErrorResult
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("retryable")]
        public bool Retryable { get; set; }

        [JsonPropertyName("suggestions")]
        public string[]? Suggestions { get; set; }
    }
}
