using Avalonia.Controls;
using StreamDeck.App.ViewModels;
using StreamDeck.Application.Device;

namespace StreamDeck.App.Views;

public partial class MainWindow : Window
{
    private bool _isClosingForReal;
    private bool _isClosingAfterDisconnect;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closing += OnClosing;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.RequestProfileNameAsync = ShowProfileNameDialogAsync;
            viewModel.RequestPageNameAsync = ShowPageNameDialogAsync;
            viewModel.RequestConfirmationAsync = ShowConfirmationDialogAsync;
            viewModel.RequestApplicationPathAsync = ShowApplicationPathDialogAsync;
            viewModel.RequestUrlAsync = ShowOpenUrlDialogAsync;
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isClosingForReal)
        {
            return;
        }

        if (DataContext is MainViewModel viewModel &&
            (viewModel.DeviceState == DeviceConnectionState.Connected ||
             viewModel.DeviceState == DeviceConnectionState.Connecting))
        {
            e.Cancel = true;

            if (!_isClosingAfterDisconnect)
            {
                _isClosingAfterDisconnect = true;
                _ = CloseAfterDisconnectingDeviceAsync(viewModel);
            }
        }
    }

    private async Task CloseAfterDisconnectingDeviceAsync(MainViewModel viewModel)
    {
        await viewModel.DisconnectDeviceForShutdownAsync();
        _isClosingForReal = true;
        Close();
    }

    private async Task<string?> ShowProfileNameDialogAsync(string? currentName)
    {
        var dialog = new ProfileNameDialog(currentName, "Nombre del perfil");
        return await dialog.ShowDialog<string?>(this);
    }

    private async Task<string?> ShowPageNameDialogAsync(string? currentName)
    {
        var dialog = new ProfileNameDialog(currentName, "Nombre de la página");
        return await dialog.ShowDialog<string?>(this);
    }

    private async Task<bool> ShowConfirmationDialogAsync(string message)
    {
        var dialog = new ConfirmationDialog(message);
        var result = await dialog.ShowDialog<bool?>(this);
        return result == true;
    }

    private async Task<string?> ShowApplicationPathDialogAsync(string? currentPath)
    {
        var dialog = new ApplicationPathDialog(currentPath);
        return await dialog.ShowDialog<string?>(this);
    }

    private async Task<string?> ShowOpenUrlDialogAsync(string? currentUrl)
    {
        var dialog = new OpenUrlDialog(currentUrl);
        return await dialog.ShowDialog<string?>(this);
    }
}