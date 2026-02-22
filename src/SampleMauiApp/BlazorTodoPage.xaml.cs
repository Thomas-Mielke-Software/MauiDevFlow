#if MACOS
using Microsoft.Maui.Platform.MacOS.Controls;
#endif

namespace SampleMauiApp;

public partial class BlazorTodoPage : ContentPage
{
    public BlazorTodoPage()
    {
        InitializeComponent();
#if MACOS
        var macWebView = new MacOSBlazorWebView
        {
            HostPage = "wwwroot/index.html",
            AutomationId = "BlazorWebView",
        };
        macWebView.RootComponents.Add(new BlazorRootComponent
        {
            Selector = "#app",
            ComponentType = typeof(SampleMauiApp.Components.Routes),
        });
        Content = macWebView;
#endif
    }
}
