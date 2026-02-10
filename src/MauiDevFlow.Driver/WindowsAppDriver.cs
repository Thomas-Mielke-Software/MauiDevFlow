namespace MauiDevFlow.Driver;

/// <summary>
/// Driver for Windows MAUI apps (WinUI 3).
/// Direct localhost connection — no special port forwarding needed.
/// </summary>
public class WindowsAppDriver : AppDriverBase
{
    public override string Platform => "Windows";
}
