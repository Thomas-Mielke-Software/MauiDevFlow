using Microsoft.Maui.Hosting;
using Microsoft.Maui.LifecycleEvents;

namespace MauiDevFlow.Blazor;

/// <summary>
/// Extension methods for registering MauiDevFlow Blazor debug tools.
/// </summary>
public static class BlazorDevFlowExtensions
{
    /// <summary>
    /// Adds MauiDevFlow Blazor WebView debugging tools to the MAUI app.
    /// Enables Chrome DevTools Protocol (CDP) access to BlazorWebView content.
    /// Chobitsu.js is embedded in the library — no wwwroot copy required.
    /// </summary>
    public static MauiAppBuilder AddMauiBlazorDevFlowTools(this MauiAppBuilder builder, Action<BlazorWebViewDebugOptions>? configure = null)
    {
        var options = new BlazorWebViewDebugOptions();
        configure?.Invoke(options);

        if (!options.Enabled) return builder;

#if ANDROID
        var service = new BlazorWebViewDebugService(options.Port);
        if (options.EnableLogging)
        {
            service.LogCallback = (msg) => System.Diagnostics.Debug.WriteLine(msg);
        }

        builder.Services.AddSingleton(service);

        service.ConfigureHandler();

        builder.ConfigureLifecycleEvents(lifecycle =>
        {
            lifecycle.AddAndroid(android =>
            {
                android.OnResume(activity =>
                {
                    if (!service.IsRunning)
                    {
                        service.Start();
                        System.Diagnostics.Debug.WriteLine($"[MauiDevFlow] Blazor CDP bridge started on port {options.Port}");
                    }
                });
            });
        });
#elif IOS || MACCATALYST
        var service = new BlazorWebViewDebugService(options.Port);
        if (options.EnableLogging)
        {
            service.LogCallback = (msg) => System.Diagnostics.Debug.WriteLine(msg);
        }

        builder.Services.AddSingleton(service);

        // Configure handler to capture WebView reference
        service.ConfigureHandler();

        builder.ConfigureLifecycleEvents(lifecycle =>
        {
            lifecycle.AddiOS(ios =>
            {
                ios.FinishedLaunching((_, _) =>
                {
                    service.Start();
                    System.Diagnostics.Debug.WriteLine($"[MauiDevFlow] Blazor CDP bridge started on port {options.Port}");
                    return true;
                });
            });
        });
#endif

        return builder;
    }
}
