using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using StreamDeck.App.ViewModels;
using StreamDeck.Application.Device;
using StreamDeck.Domain.Entities;

namespace StreamDeck.App.Views;

public partial class MainWindow : Window
{
    private bool _isClosingForReal;
    private bool _isClosingAfterDisconnect;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        PropertyChanged += OnWindowPropertyChanged;
        Closing += OnClosing;
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty &&
            WindowState == Avalonia.Controls.WindowState.Minimized)
        {
            // Al minimizar, se quita la ventana de la barra de tareas.
            // El TrayIcon de la aplicación permite volver a mostrarla.
            ShowInTaskbar = false;
        }
    }

    public void RestoreFromTray()
    {
        ShowInTaskbar = true;
        WindowState = Avalonia.Controls.WindowState.Normal;
        Activate();
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
            viewModel.RequestButtonNameAsync = ShowButtonNameDialogAsync;
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

    private async Task<string?> ShowButtonNameDialogAsync(string? currentName)
    {
        var dialog = new ButtonNameDialog(currentName);
        return await dialog.ShowDialog<string?>(this);
    }

    private void OnButtonSlotDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: ButtonSlot slot } && DataContext is MainViewModel viewModel)
        {
            viewModel.SelectedButtonSlot = slot;
            viewModel.RenameButtonSlotCommand.Execute(slot);
            e.Handled = true;
        }
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