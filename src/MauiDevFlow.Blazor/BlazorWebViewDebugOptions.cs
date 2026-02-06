namespace MauiDevFlow.Blazor;

/// <summary>
/// Configuration options for BlazorWebView debug tools.
/// Enables Chrome DevTools Protocol (CDP) connections to the BlazorWebView.
/// </summary>
public class BlazorWebViewDebugOptions
{
    /// <summary>
    /// The port for the CDP WebSocket server. Default: 9222
    /// This is the standard Chrome DevTools Protocol port.
    /// </summary>
    public int Port { get; set; } = 9222;

    /// <summary>
    /// Whether debug tools are enabled. Default: true
    /// Set to false to disable in production builds.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to enable WKWebView/WebView inspection in Safari/Chrome DevTools.
    /// Default: true. Requires iOS 16.4+ / macOS 13.3+ for Safari.
    /// </summary>
    public bool EnableWebViewInspection { get; set; } = true;

    /// <summary>
    /// Whether to log debug messages. Default: true in DEBUG builds.
    /// </summary>
    public bool EnableLogging { get; set; }
#if DEBUG
        = true;
#else
        = false;
#endif
}
