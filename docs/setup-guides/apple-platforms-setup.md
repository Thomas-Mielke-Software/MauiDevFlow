# Apple Platforms Setup (Mac Catalyst & iOS)

Both Mac Catalyst and iOS use WKWebView with Safari's Web Inspector protocol. The debugging approach uses Appium with the XCUITest driver.

## Prerequisites

### 1. Install Node.js

Appium requires Node.js:

```bash
brew install node
```

### 2. Install Appium

```bash
npm install -g appium
```

### 3. Install XCUITest Driver

```bash
appium driver install xcuitest
```

### 4. Install ios-webkit-debug-proxy (iOS only)

For iOS device/simulator WebView debugging:

```bash
brew install ios-webkit-debug-proxy
```

### 5. Xcode Command Line Tools

```bash
xcode-select --install
```

## App Configuration

### Enable WKWebView Inspection

In your MAUI app, the BlazorWebView uses WKWebView internally. For iOS 16.4+ and macOS 13.3+, you need to enable inspection:

**Option 1: Via Platform Code (Recommended)**

Create a custom handler in `Platforms/iOS/AppDelegate.cs` or `Platforms/MacCatalyst/AppDelegate.cs`:

```csharp
using Microsoft.Maui.Handlers;
using WebKit;

public partial class AppDelegate
{
    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        // Enable WebView debugging in debug builds
        #if DEBUG
        BlazorWebViewHandler.BlazorWebViewMapper.AppendToMapping("Inspectable", (handler, view) =>
        {
            if (handler.PlatformView is WKWebView webView)
            {
                if (OperatingSystem.IsIOSVersionAtLeast(16, 4) || 
                    OperatingSystem.IsMacCatalystVersionAtLeast(16, 4))
                {
                    webView.SetValueForKey(NSNumber.FromBoolean(true), new NSString("inspectable"));
                }
            }
        });
        #endif

        return base.FinishedLaunching(application, launchOptions);
    }
}
```

**Option 2: Via Safari Develop Menu**

1. Open Safari
2. Safari → Settings → Advanced → Check "Show Develop menu in menu bar"
3. Run your MAUI app
4. Develop → [Device/Simulator Name] → [App Name] → WebView

### iOS Device Settings

On the physical iOS device:
1. Settings → Safari → Advanced
2. Enable **Web Inspector**

## Starting Appium Server

Start Appium before running tests:

```bash
appium
```

By default, Appium runs on `http://localhost:4723`.

## Usage

### Mac Catalyst

```csharp
var options = new WebViewConnectionOptions
{
    Platform = WebViewPlatform.MacCatalyst,
    BundleId = "com.yourcompany.yourapp", // Your app's bundle ID
    TimeoutSeconds = 60
};

using var driver = WebViewDriverFactory.Create(options);
await driver.ConnectAsync();
```

### iOS Simulator

```csharp
var options = new WebViewConnectionOptions
{
    Platform = WebViewPlatform.iOS,
    BundleId = "com.yourcompany.yourapp",
    StartIosWebKitDebugProxy = true, // Auto-start IWDP
    TimeoutSeconds = 60
};

using var driver = WebViewDriverFactory.Create(options);
await driver.ConnectAsync();
```

### iOS Physical Device

```csharp
var options = new WebViewConnectionOptions
{
    Platform = WebViewPlatform.iOS,
    BundleId = "com.yourcompany.yourapp",
    DeviceId = "your-device-udid", // Get from Xcode or `instruments -s devices`
    StartIosWebKitDebugProxy = true,
    TimeoutSeconds = 60
};

using var driver = WebViewDriverFactory.Create(options);
await driver.ConnectAsync();
```

## Troubleshooting

### "WebView context not found"

- Ensure `WKWebView.isInspectable = true` is set in your app
- Verify Safari Web Inspector is enabled on the device
- Check that Appium server is running
- Wait for the app to fully load the WebView content

### "Failed to connect to Appium"

- Verify Appium server is running: `appium`
- Check the Appium server URL (default: `http://localhost:4723`)
- Ensure XCUITest driver is installed: `appium driver list`

### ios-webkit-debug-proxy Issues

- Start manually: `ios_webkit_debug_proxy -c <UDID>:27753 -d`
- Check if another process is using port 27753

### Finding Device UDID

```bash
# List connected devices
xcrun xctrace list devices

# Or use instruments
instruments -s devices

# Or in Xcode: Window → Devices and Simulators
```

## Context Switching

The AppleWebDriver automatically switches from `NATIVE_APP` to `WEBVIEW_xxx` context. You can also get available contexts:

```csharp
var appleDriver = driver as AppleWebDriver;
var contexts = appleDriver?.GetAvailableContexts();
// Returns: ["NATIVE_APP", "WEBVIEW_1234"]
```

## References

- [Appium Documentation](https://appium.io/docs/en/latest/)
- [XCUITest Driver](https://github.com/appium/appium-xcuitest-driver)
- [ios-webkit-debug-proxy](https://github.com/nickreese/ios-webkit-debug-proxy)
- [Apple: Testing with WebDriver in Safari](https://developer.apple.com/documentation/webkit/testing-with-webdriver-in-safari)
