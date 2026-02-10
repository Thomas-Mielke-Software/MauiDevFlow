namespace SampleMauiApp;

public partial class DialogTestPage : ContentPage
{
    public DialogTestPage()
    {
        InitializeComponent();
    }

    private void SetStatus(string action)
    {
        StatusLabel.Text = $"last action: {action}";
    }

    // --- Permission Requests ---

    private async void OnRequestLocation(object? sender, EventArgs e)
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            SetStatus($"location: {status}");
        }
        catch (Exception ex)
        {
            SetStatus($"location error: {ex.Message}");
        }
    }

    private async void OnRequestCamera(object? sender, EventArgs e)
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.Camera>();
            SetStatus($"camera: {status}");
        }
        catch (Exception ex)
        {
            SetStatus($"camera error: {ex.Message}");
        }
    }

    private async void OnRequestPhotos(object? sender, EventArgs e)
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.Photos>();
            SetStatus($"photos: {status}");
        }
        catch (Exception ex)
        {
            SetStatus($"photos error: {ex.Message}");
        }
    }

    private async void OnRequestContacts(object? sender, EventArgs e)
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.ContactsRead>();
            SetStatus($"contacts: {status}");
        }
        catch (Exception ex)
        {
            SetStatus($"contacts error: {ex.Message}");
        }
    }

    private async void OnRequestMicrophone(object? sender, EventArgs e)
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.Microphone>();
            SetStatus($"microphone: {status}");
        }
        catch (Exception ex)
        {
            SetStatus($"microphone error: {ex.Message}");
        }
    }

    private async void OnRequestNotifications(object? sender, EventArgs e)
    {
#if IOS
        try
        {
            var center = UserNotifications.UNUserNotificationCenter.Current;
            var (granted, error) = await center.RequestAuthorizationAsync(
                UserNotifications.UNAuthorizationOptions.Alert
                | UserNotifications.UNAuthorizationOptions.Sound
                | UserNotifications.UNAuthorizationOptions.Badge);
            SetStatus($"notifications: {(granted ? "Granted" : "Denied")}{(error is not null ? $" ({error.LocalizedDescription})" : "")}");
        }
        catch (Exception ex)
        {
            SetStatus($"notifications error: {ex.Message}");
        }
#else
        SetStatus("notifications: not supported on this platform");
        await Task.CompletedTask;
#endif
    }

    // --- App Alerts ---

    private async void OnAlertOkOnly(object? sender, EventArgs e)
    {
        await DisplayAlertAsync("Test Alert", "This is an OK-only alert.", "OK");
        SetStatus("alert: OK dismissed");
    }

    private async void OnAlertOkCancel(object? sender, EventArgs e)
    {
        var result = await DisplayAlertAsync("Confirm Action", "Do you want to proceed?", "OK", "Cancel");
        SetStatus($"alert: {(result ? "OK" : "Cancel")}");
    }

    private async void OnAlertCustomButtons(object? sender, EventArgs e)
    {
        var result = await DisplayAlertAsync("Delete Item", "Are you sure you want to delete this?", "Delete", "Keep");
        SetStatus($"alert: {(result ? "Delete" : "Keep")}");
    }

    private async void OnActionSheet(object? sender, EventArgs e)
    {
        var result = await DisplayActionSheetAsync("Choose an option", "Cancel", "Destructive", "Option 1", "Option 2", "Option 3");
        SetStatus($"action sheet: {result ?? "null"}");
    }

    private async void OnPromptInput(object? sender, EventArgs e)
    {
        var result = await DisplayPromptAsync("Enter Value", "Type something:", "OK", "Cancel", placeholder: "your text here");
        SetStatus($"prompt: {result ?? "cancelled"}");
    }
}
