#if MACOS
using Microsoft.Maui.Platform.MacOS;
#endif

namespace SampleMauiApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
#if MACOS
        FlyoutBehavior = FlyoutBehavior.Locked;
        MacOSShell.SetUseNativeSidebar(this, true);
#endif
    }
}
