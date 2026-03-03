#if IOS || MACCATALYST
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Maui.Handlers;
using WebKit;

namespace MauiDevFlow.Blazor;

/// <summary>
/// iOS/Mac Catalyst implementation of the Blazor WebView debug service.
/// Uses WKWebView for JavaScript evaluation.
/// </summary>
public class BlazorWebViewDebugService : BlazorWebViewDebugServiceBase
{
    private WKWebView? _webView;

    public BlazorWebViewDebugService() { }

    protected override bool HasWebView => _webView != null;

    protected override async Task<string?> EvaluateJavaScriptAsync(string script)
    {
        if (_webView == null) return null;
        var result = await _webView.EvaluateJavaScriptAsync(script);
        return result?.ToString();
    }

    protected override void ReloadWebView() => _webView!.Reload();

    protected override void NavigateWebView(string url)
    {
        var request = new Foundation.NSUrlRequest(new Foundation.NSUrl(url));
        _webView!.LoadRequest(request);
    }

    public override void ConfigureHandler()
    {
        Log("[BlazorDevFlow] ConfigureHandler called, appending to BlazorWebViewMapper");

        BlazorWebViewHandler.BlazorWebViewMapper.AppendToMapping("ChobitsuDebug", async (handler, view) =>
        {
            Log("[BlazorDevFlow] ChobitsuDebug mapper callback triggered");

            if (handler.PlatformView is WKWebView wkWebView)
            {
                if (wkWebView == _webView) return;
                _webView = wkWebView;
                Log("[BlazorDevFlow] WKWebView captured successfully");
                await OnWebViewCapturedAsync();
            }
            else
            {
                Log($"[BlazorDevFlow] PlatformView is not WKWebView: {handler.PlatformView?.GetType().Name ?? "null"}");
            }
        });
    }
}
#elif MACOS
using CoreFoundation;
using Foundation;
using Microsoft.Maui.Handlers;
using WebKit;
using MacOSBlazorHandler = Microsoft.Maui.Platform.MacOS.Handlers.BlazorWebViewHandler;

namespace MauiDevFlow.Blazor;

/// <summary>
/// macOS AppKit implementation of the Blazor WebView debug service.
/// Uses WKWebView for JavaScript evaluation via the macOS backend's BlazorWebViewHandler.
/// Overrides main thread dispatch since MainThread.InvokeOnMainThreadAsync is not available
/// on the macOS AppKit backend (uses netstandard reference assembly).
/// </summary>
public class BlazorWebViewDebugService : BlazorWebViewDebugServiceBase
{
    private WKWebView? _webView;

    public BlazorWebViewDebugService() { }

    protected override bool HasWebView => _webView != null;

    protected override async Task<string?> EvaluateJavaScriptAsync(string script)
    {
        if (_webView == null) return null;
        var result = await _webView.EvaluateJavaScriptAsync(script);
        return result?.ToString();
    }

    protected override void ReloadWebView() => _webView!.Reload();

    protected override void NavigateWebView(string url)
    {
        var request = new NSUrlRequest(new NSUrl(url));
        _webView!.LoadRequest(request);
    }

    protected override Task<T> RunOnMainThreadAsync<T>(Func<Task<T>> func)
    {
        if (NSThread.Current.IsMainThread)
            return func();

        var tcs = new TaskCompletionSource<T>();
        DispatchQueue.MainQueue.DispatchAsync(async () =>
        {
            try { tcs.SetResult(await func()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    protected override Task<T> RunOnMainThreadAsync<T>(Func<T> func)
    {
        if (NSThread.Current.IsMainThread)
            return Task.FromResult(func());

        var tcs = new TaskCompletionSource<T>();
        DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            try { tcs.SetResult(func()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    protected override Task RunOnMainThreadAsync(Func<Task> func)
    {
        if (NSThread.Current.IsMainThread)
            return func();

        var tcs = new TaskCompletionSource();
        DispatchQueue.MainQueue.DispatchAsync(async () =>
        {
            try { await func(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    protected override Task RunOnMainThreadAsync(Action action)
    {
        if (NSThread.Current.IsMainThread)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            try { action(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    protected override void PostToMainThread(Action action)
    {
        if (NSThread.Current.IsMainThread)
            action();
        else
            DispatchQueue.MainQueue.DispatchAsync(action);
    }

    public override void ConfigureHandler()
    {
        Log("[BlazorDevFlow] ConfigureHandler called, appending to macOS BlazorWebViewHandler mapper");

        MacOSBlazorHandler.Mapper.AppendToMapping("ChobitsuDebug", async (handler, view) =>
        {
            Log("[BlazorDevFlow] ChobitsuDebug mapper callback triggered");

            if (handler.PlatformView is WKWebView wkWebView)
            {
                if (wkWebView == _webView) return;
                _webView = wkWebView;
                Log("[BlazorDevFlow] WKWebView captured successfully");
                await OnWebViewCapturedAsync();
            }
            else
            {
                Log($"[BlazorDevFlow] PlatformView is not WKWebView: {handler.PlatformView?.GetType().Name ?? "null"}");
            }
        });
    }
}
#endif
