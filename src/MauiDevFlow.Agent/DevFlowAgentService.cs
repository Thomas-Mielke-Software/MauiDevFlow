using Microsoft.Maui.Controls;
using MauiDevFlow.Agent.Core;

namespace MauiDevFlow.Agent;

/// <summary>
/// Platform-specific agent service that provides native tap and screenshot
/// implementations for Android, iOS, Mac Catalyst, and Windows.
/// </summary>
public class PlatformAgentService : DevFlowAgentService
{
    public PlatformAgentService(AgentOptions? options = null) : base(options) { }

    protected override VisualTreeWalker CreateTreeWalker() => new PlatformVisualTreeWalker();

    protected override bool TryNativeTap(VisualElement ve)
    {
        try
        {
            var platformView = ve.Handler?.PlatformView;
            if (platformView == null) return false;

#if IOS || MACCATALYST
            if (platformView is UIKit.UIControl control)
            {
                control.SendActionForControlEvents(UIKit.UIControlEvent.TouchUpInside);
                return true;
            }
#elif ANDROID
            if (platformView is Android.Views.View androidView && androidView.Clickable)
            {
                androidView.PerformClick();
                return true;
            }
#endif
        }
        catch { }
        return false;
    }

#if WINDOWS
    protected override async Task<byte[]?> CaptureScreenshotAsync(VisualElement rootElement)
    {
        // MAUI's VisualDiagnostics doesn't capture WebView2 GPU-rendered content on Windows.
        // When a WebView2 is present, use CoreWebView2.CapturePreviewAsync instead.
        try
        {
            var wv2 = FindPlatformWebView2(rootElement);
            if (wv2?.CoreWebView2 != null)
            {
                using var ras = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                await wv2.CoreWebView2.CapturePreviewAsync(
                    Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, ras);
                var reader = new Windows.Storage.Streams.DataReader(ras.GetInputStreamAt(0));
                await reader.LoadAsync((uint)ras.Size);
                var bytes = new byte[ras.Size];
                reader.ReadBytes(bytes);
                return bytes;
            }
        }
        catch { }

        return await base.CaptureScreenshotAsync(rootElement);
    }

    private static Microsoft.UI.Xaml.Controls.WebView2? FindPlatformWebView2(Element element)
    {
        if (element is View view && view.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 wv2)
            return wv2;
        // Shell doesn't expose pages via Content/Children — use CurrentPage
        if (element is Shell shell && shell.CurrentPage != null)
        {
            var found = FindPlatformWebView2(shell.CurrentPage);
            if (found != null) return found;
        }
        if (element is ContentPage page && page.Content != null)
        {
            var found = FindPlatformWebView2(page.Content);
            if (found != null) return found;
        }
        if (element is Layout layout)
        {
            foreach (var child in layout.Children)
            {
                if (child is Element childElement)
                {
                    var found = FindPlatformWebView2(childElement);
                    if (found != null) return found;
                }
            }
        }
        return null;
    }
#endif
}
