#if MACOS
using Microsoft.Maui.Platform.MacOS.Hosting;
using Microsoft.Maui.Hosting;
using AppKit;
using Foundation;

namespace SampleMauiApp;

[Register("MacOSProgram")]
public class MacOSProgram : MacOSMauiApplication
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public static void Main(string[] args)
	{
		NSApplication.Init();
		NSApplication.SharedApplication.Delegate = new MacOSProgram();
		NSApplication.Main(args);
	}
}
#endif
