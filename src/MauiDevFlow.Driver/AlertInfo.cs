namespace MauiDevFlow.Driver;

/// <summary>
/// Represents a button found in a system or app alert dialog.
/// </summary>
public record AlertButton(string Label, double X, double Y, double Width, double Height)
{
    public int CenterX => (int)(X + Width / 2);
    public int CenterY => (int)(Y + Height / 2);
}

/// <summary>
/// Information about a detected alert dialog.
/// </summary>
public record AlertInfo(string? Title, IReadOnlyList<AlertButton> Buttons);
