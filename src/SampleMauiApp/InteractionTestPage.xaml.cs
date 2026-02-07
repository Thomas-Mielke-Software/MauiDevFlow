using System.Windows.Input;

namespace SampleMauiApp;

public partial class InteractionTestPage : ContentPage
{
    public ICommand TapCommand { get; }

    public InteractionTestPage()
    {
        TapCommand = new Command<string>(param =>
            SetStatus($"Command tap: {param}"));

        BindingContext = this;
        InitializeComponent();
    }

    private void SetStatus(string action)
    {
        StatusLabel.Text = $"last action: {action}";
    }

    private void OnToolbarAction1(object? sender, EventArgs e)
        => SetStatus("toolbar: Action1");

    private void OnToolbarAction2(object? sender, EventArgs e)
        => SetStatus("toolbar: Action2");

    private void OnEventTapGridTapped(object? sender, TappedEventArgs e)
        => SetStatus("event tap: EventTapGrid");

    private void OnImageButtonClicked(object? sender, EventArgs e)
        => SetStatus("image button clicked");

    private void OnPickerChanged(object? sender, EventArgs e)
        => SetStatus($"picker: {TestPicker.SelectedItem}");

    private void OnTestButtonClicked(object? sender, EventArgs e)
        => SetStatus("button: TestButton");

    private void OnCheckBoxChanged(object? sender, CheckedChangedEventArgs e)
        => SetStatus($"checkbox: {e.Value}");

    private void OnSwitchToggled(object? sender, ToggledEventArgs e)
        => SetStatus($"switch: {e.Value}");
}
