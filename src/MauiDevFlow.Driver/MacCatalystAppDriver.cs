namespace MauiDevFlow.Driver;

/// <summary>
/// Driver for Mac Catalyst MAUI apps.
/// Direct localhost connection, no special setup needed.
/// </summary>
public class MacCatalystAppDriver : AppDriverBase
{
    public override string Platform => "MacCatalyst";
}
