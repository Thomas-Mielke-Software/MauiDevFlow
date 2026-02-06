namespace MauiDevFlow.Blazor;

/// <summary>
/// Provides the JavaScript code needed to enable Chrome DevTools Protocol debugging
/// in a WebView using Chobitsu.
/// 
/// Chobitsu is a JavaScript implementation of the Chrome DevTools Protocol that runs
/// entirely in the browser/WebView. Combined with a WebSocket bridge, it allows
/// remote debugging tools to connect as if it were a native CDP endpoint.
/// 
/// Architecture:
/// ┌─────────────────────────────────────────────────────────────────┐
/// │  MAUI App                                                       │
/// │  ┌─────────────────────────────────────────────────────────┐   │
/// │  │  BlazorWebView                                           │   │
/// │  │  ┌─────────────────────────────────────────────────────┐ │   │
/// │  │  │  Chobitsu (CDP Implementation in JS)                │ │   │
/// │  │  │  ↕ Messages                                         │ │   │
/// │  │  │  WebSocket Client ──────────────────────────────────┼─┼───┼──→ External
/// │  │  └─────────────────────────────────────────────────────┘ │   │    DevTools
/// │  └─────────────────────────────────────────────────────────┘   │
/// └─────────────────────────────────────────────────────────────────┘
/// </summary>
public static class ChobitsuDebugScript
{

    private static string? _cachedChobitsuJs;

    /// <summary>
    /// Loads chobitsu.js from the embedded resource in this assembly.
    /// Used as a fallback for re-injection after Page.reload.
    /// </summary>
    public static string GetEmbeddedChobitsuJs()
    {
        if (_cachedChobitsuJs != null) return _cachedChobitsuJs;

        var assembly = typeof(ChobitsuDebugScript).Assembly;
        using var stream = assembly.GetManifestResourceStream("MauiDevFlow.Blazor.chobitsu.js")
            ?? throw new InvalidOperationException("Embedded chobitsu.js resource not found in MauiDevFlow.Blazor assembly.");
        using var reader = new System.IO.StreamReader(stream);
        _cachedChobitsuJs = reader.ReadToEnd();
        return _cachedChobitsuJs;
    }

    /// <summary>
    /// Gets the JavaScript code to inject into the WebView to enable debugging.
    /// Expects chobitsu.js to already be loaded via a script tag in index.html.
    /// The NuGet .targets file delivers chobitsu.js to wwwroot/js/ at build time.
    /// </summary>
    /// <param name="wsPort">WebSocket port for debug connections</param>
    public static string GetInjectionScript(int wsPort = 9222)
    {
        return ScriptResources.Load("chobitsu-init.js")
            .Replace("%PORT%", wsPort.ToString());
    }

    /// <summary>
    /// Gets JavaScript to handle an incoming CDP message from an external connection.
    /// Call this when the native WebSocket server receives a message.
    /// </summary>
    public static string GetMessageHandlerScript(string jsonMessage)
    {
        // Escape the JSON for embedding in JavaScript
        var escaped = jsonMessage.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "\\r");
        // Return null explicitly to avoid WKWebView serialization issues with undefined/complex return values
        return $"(function() {{ if (typeof chobitsu !== 'undefined') {{ chobitsu.sendRawMessage('{escaped}'); }} return null; }})()";
    }

    /// <summary>
    /// Gets JavaScript to register a new WebSocket connection.
    /// </summary>
    public static string GetConnectionRegistrationScript(string connectionId)
    {
        return ScriptResources.Load("register-connection.js")
            .Replace("%CONNECTION_ID%", connectionId);
    }

    /// <summary>
    /// Gets the full HTML page that can be used to test the debug server.
    /// </summary>
    public static string GetTestPageHtml(int wsPort = 9222)
    {
        return ScriptResources.Load("test-page.html")
            .Replace("%PORT%", wsPort.ToString())
            .Replace("%INJECTION_SCRIPT%", GetInjectionScript(wsPort));
    }
}
