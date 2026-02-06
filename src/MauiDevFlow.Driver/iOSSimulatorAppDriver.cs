namespace MauiDevFlow.Driver;

/// <summary>
/// Driver for iOS Simulator MAUI apps.
/// iOS Simulator shares host network stack, so localhost works directly.
/// </summary>
public class iOSSimulatorAppDriver : AppDriverBase
{
    public override string Platform => "iOSSimulator";

    // iOS simulator shares host network — no special setup needed.
    // Future: could integrate simctl for screenshots, app lifecycle, etc.
}
