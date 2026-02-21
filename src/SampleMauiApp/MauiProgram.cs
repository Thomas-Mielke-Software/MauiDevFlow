using Microsoft.Extensions.Logging;
using MauiDevFlow.Agent;
using MauiDevFlow.Blazor;
#if MACOS
using Microsoft.Maui.Platform.MacOS.Hosting;
using Microsoft.Maui.Essentials.MacOS;
#endif

namespace SampleMauiApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
#if MACOS
			.UseMauiAppMacOS<App>()
			.AddMacOSEssentials()
#else
			.UseMauiApp<App>()
#endif
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		// Blazor WebView
		builder.Services.AddMauiBlazorWebView();
#if MACOS
		builder.AddMacOSBlazorWebView();
#endif

		// Shared data
		builder.Services.AddSingleton<TodoService>();

		// Pages (DI-resolved by Shell's DataTemplate)
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<BlazorTodoPage>();

#if DEBUG
		//builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
		builder.AddMauiDevFlowAgent(options => { options.Port = 9223; });
		builder.AddMauiBlazorDevFlowTools();
#endif

		return builder.Build();
	}
}
