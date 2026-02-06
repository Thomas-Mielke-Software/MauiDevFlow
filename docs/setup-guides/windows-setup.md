# Windows Setup

Windows MAUI apps use WebView2 (Microsoft Edge Chromium) which can be debugged via Edge DevTools Protocol. The WindowsWebDriver uses EdgeDriver to connect.

## Prerequisites

### 1. Microsoft Edge WebDriver (EdgeDriver)

Download from: https://developer.microsoft.com/en-us/microsoft-edge/tools/webdriver/

**Important:** The EdgeDriver version must match your installed Edge browser version.

Check your Edge version:
- Open Edge → Settings → About Microsoft Edge

Download the matching driver version and add it to PATH or specify via `WebViewConnectionOptions.DriverPath`.

### 2. WebView2 Runtime

WebView2 Runtime is usually pre-installed on Windows 10/11. If not:
https://developer.microsoft.com/en-us/microsoft-edge/webview2/

## App Configuration

### Enable Remote Debugging

Your MAUI app's WebView2 must be created with remote debugging enabled. This requires platform-specific code.

**In `Platforms/Windows/App.xaml.cs`:**

```csharp
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using Microsoft.Maui.Handlers;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        this.InitializeComponent();

        #if DEBUG
        EnableWebViewDebugging();
        #endif
    }

    private void EnableWebViewDebugging()
    {
        BlazorWebViewHandler.BlazorWebViewMapper.AppendToMapping("WebViewDebugging", async (handler, view) =>
        {
            if (handler.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 webView2)
            {
                // Configure environment with remote debugging port
                var options = new CoreWebView2EnvironmentOptions("--remote-debugging-port=9222");
                var env = await CoreWebView2Environment.CreateAsync(null, null, options);
                
                await webView2.EnsureCoreWebView2Async(env);
            }
        });
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
```

**Alternative: Environment Variable**

You can also set the debugging port via environment variable before starting the app:

```powershell
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=9222"
dotnet run
```

## Usage

### Basic Connection

```csharp
var options = new WebViewConnectionOptions
{
    Platform = WebViewPlatform.Windows,
    RemoteDebuggingPort = 9222, // Default
    TimeoutSeconds = 60
};

using var driver = WebViewDriverFactory.Create(options);
await driver.ConnectAsync();

var title = await driver.GetTitleAsync();
```

### With Custom Driver Path

```csharp
var options = new WebViewConnectionOptions
{
    Platform = WebViewPlatform.Windows,
    RemoteDebuggingPort = 9222,
    DriverPath = @"C:\tools\msedgedriver.exe"
};

using var driver = WebViewDriverFactory.Create(options);
await driver.ConnectAsync();
```

### Verify Accessibility First

```csharp
// Check if WebView2 debugging endpoint is available
var accessible = await WindowsWebDriver.VerifyWebViewAccessibleAsync(9222);
if (accessible)
{
    Console.WriteLine("WebView2 is ready for automation!");
    
    // Get debug target information
    var targets = await WindowsWebDriver.GetDebugTargetsAsync(9222);
    Console.WriteLine(targets);
}
```

## Manual Verification

### Check Debug Endpoint

After starting your MAUI app with debugging enabled:

```powershell
# PowerShell
Invoke-RestMethod http://localhost:9222/json

# Or use curl
curl http://localhost:9222/json
```

This should return JSON with page/target information.

### Edge DevTools

1. Open Microsoft Edge
2. Navigate to `edge://inspect`
3. Your WebView2 app should appear under "Remote Target"
4. Click "inspect" to open DevTools

## Troubleshooting

### "Failed to connect EdgeDriver to WebView2"

- Ensure your MAUI app is running with remote debugging enabled
- Verify the debug endpoint: `curl http://localhost:9222/json`
- Check that no other process is using port 9222

### EdgeDriver Version Mismatch

```
SessionNotCreatedException: Could not start a new session. Response code 500.
```

- Check Edge version: `edge://version`
- Download matching EdgeDriver version
- Specify path: `options.DriverPath = @"path\to\msedgedriver.exe"`

### Port Already in Use

If port 9222 is already in use:

```powershell
# Find process using port
netstat -ano | findstr :9222

# Use a different port in your app configuration
```

Update both app configuration and connection options:
```csharp
options.RemoteDebuggingPort = 9223;
```

### WebView2 Not Creating with Debug Port

Ensure the `CoreWebView2EnvironmentOptions` is applied BEFORE the WebView2 is created:

1. The handler mapping must run early
2. Call `EnsureCoreWebView2Async` with the configured environment
3. Don't let WebView2 auto-initialize before your configuration runs

### "WebView2 Runtime not found"

Install WebView2 Runtime:
- Download from Microsoft: https://go.microsoft.com/fwlink/p/?LinkId=2124703
- Or use the Evergreen Bootstrapper for auto-updates

## Using with Visual Studio

You can also debug WebView2 content directly in Visual Studio:

1. Set a breakpoint in your Blazor code
2. In Debug → Windows → Script Documents, you'll see your web content
3. Use the browser DevTools that opens with F12 inside the app

## Edge DevTools Protocol

For advanced scenarios, you can send CDP commands directly:

```csharp
if (driver.Driver is IDevTools devTools)
{
    var session = devTools.GetDevToolsSession();
    // Send CDP commands
}
```

## References

- [WebView2 Remote Debugging](https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/remote-debugging)
- [Automate WebView2 with WebDriver](https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/webdriver)
- [Edge DevTools Protocol](https://learn.microsoft.com/en-us/microsoft-edge/devtools-protocol-chromium/)
- [EdgeDriver Downloads](https://developer.microsoft.com/en-us/microsoft-edge/tools/webdriver/)
