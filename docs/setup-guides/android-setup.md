# Android Setup

Android WebViews use the Chromium engine and can be debugged via Chrome DevTools Protocol. The AndroidWebDriver uses ChromeDriver with ADB port forwarding.

## Prerequisites

### 1. Android SDK Platform Tools (ADB)

Install via Android Studio or standalone:

```bash
# macOS with Homebrew
brew install android-platform-tools

# Or download from Google:
# https://developer.android.com/studio/releases/platform-tools
```

Verify installation:
```bash
adb version
```

### 2. ChromeDriver

Download ChromeDriver matching your device's Chrome/WebView version:
https://chromedriver.chromium.org/downloads

For newer versions (Chrome 115+):
https://googlechromelabs.github.io/chrome-for-testing/

Place `chromedriver` in your PATH or specify via `WebViewConnectionOptions.DriverPath`.

### 3. USB Debugging on Device

1. Enable Developer Options:
   - Settings → About Phone → Tap "Build Number" 7 times
2. Enable USB Debugging:
   - Settings → Developer Options → USB Debugging = ON
3. Connect device via USB and authorize the connection

Verify:
```bash
adb devices
# Should show your device
```

## App Configuration

### Enable WebView Debugging

In your MAUI app's Android platform code, enable WebView debugging.

**Option 1: In MainActivity.cs**

```csharp
using Android.Webkit;

public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        #if DEBUG
        // Enable WebView debugging
        WebView.SetWebContentsDebuggingEnabled(true);
        #endif
    }
}
```

**Option 2: Via BlazorWebView Handler**

```csharp
using Microsoft.Maui.Handlers;
using Android.Webkit;

// In MauiProgram.cs
builder.ConfigureMauiHandlers(handlers =>
{
    #if DEBUG && ANDROID
    BlazorWebViewHandler.BlazorWebViewMapper.AppendToMapping("WebDebugging", (handler, view) =>
    {
        WebView.SetWebContentsDebuggingEnabled(true);
    });
    #endif
});
```

## Manual Port Forwarding (For Understanding)

The AndroidWebDriver handles this automatically, but understanding the process helps with debugging:

### Step 1: Find WebView Debug Socket

```bash
adb shell cat /proc/net/unix | grep webview_devtools_remote
# Output: ... @webview_devtools_remote_12345
# The number (12345) is the PID
```

### Step 2: Forward Port

```bash
adb forward tcp:9222 localabstract:webview_devtools_remote_12345
```

### Step 3: Verify

```bash
curl http://localhost:9222/json
# Should return JSON with page information
```

You can also open `chrome://inspect` in Chrome browser to see the WebView.

## Usage

### Basic Connection

```csharp
var options = new WebViewConnectionOptions
{
    Platform = WebViewPlatform.Android,
    RemoteDebuggingPort = 9222, // Default
    TimeoutSeconds = 60
};

using var driver = WebViewDriverFactory.Create(options);
await driver.ConnectAsync();

var title = await driver.GetTitleAsync();
```

### With Specific Device

```csharp
var options = new WebViewConnectionOptions
{
    Platform = WebViewPlatform.Android,
    DeviceId = "emulator-5554", // or device serial from `adb devices`
    AndroidPackage = "com.yourcompany.yourapp",
    TimeoutSeconds = 60
};

using var driver = WebViewDriverFactory.Create(options);
await driver.ConnectAsync();
```

### Verify Accessibility First

```csharp
// After manual ADB forwarding, verify WebView is accessible
var accessible = await AndroidWebDriver.VerifyWebViewAccessibleAsync(9222);
if (accessible)
{
    Console.WriteLine("WebView is ready for automation!");
}
```

## Troubleshooting

### "No debuggable WebView found"

- Ensure your app is running and has `WebView.setWebContentsDebuggingEnabled(true)`
- Check the package name filter if specified
- Verify ADB connection: `adb devices`

### "Failed to connect ChromeDriver"

- Ensure ChromeDriver version matches device's Chrome/WebView version
- Check what Chrome version is on device: `adb shell dumpsys package com.google.android.webview | grep versionName`
- Download matching ChromeDriver

### ChromeDriver Version Mismatch

Find device WebView version:
```bash
adb shell dumpsys package com.google.android.webview | grep versionName
```

Download matching ChromeDriver and specify path:
```csharp
options.DriverPath = "/path/to/chromedriver";
```

### Multiple Devices Connected

Specify device serial:
```csharp
options.DeviceId = "emulator-5554";
```

Or via ADB:
```bash
adb -s emulator-5554 forward tcp:9222 localabstract:webview_devtools_remote_12345
```

### ADB Not Found

Ensure ADB is in PATH:
```bash
export PATH=$PATH:~/Library/Android/sdk/platform-tools
```

### Emulator Not Detected

```bash
# List running emulators
adb devices

# Start emulator (if AVD exists)
emulator -avd Pixel_4_API_30
```

## Chrome DevTools Manual Inspection

For manual debugging, you can also use Chrome's built-in inspector:

1. Open Chrome on your computer
2. Navigate to `chrome://inspect`
3. Under "Remote Target", you should see your app's WebView
4. Click "inspect" to open DevTools

## References

- [Chrome DevTools Protocol](https://chromedevtools.github.io/devtools-protocol/)
- [ChromeDriver - Android](https://chromedriver.chromium.org/getting-started/getting-started---android)
- [Debug Android WebViews](https://developer.android.com/develop/ui/views/layout/webapps/debug-chrome-devtools)
- [ADB Documentation](https://developer.android.com/studio/command-line/adb)
