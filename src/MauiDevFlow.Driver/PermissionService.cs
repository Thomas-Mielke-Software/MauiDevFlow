namespace MauiDevFlow.Driver;

/// <summary>
/// iOS simulator permission services that can be granted, revoked, or reset via xcrun simctl privacy.
/// </summary>
public enum PermissionService
{
    All,
    Calendar,
    Contacts,
    ContactsLimited,
    Location,
    LocationAlways,
    Photos,
    PhotosAdd,
    MediaLibrary,
    Microphone,
    Motion,
    Reminders,
    Siri,
    Camera
}
