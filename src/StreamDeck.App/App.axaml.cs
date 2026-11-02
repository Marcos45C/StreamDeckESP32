using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using StreamDeck.App.ViewModels;
using StreamDeck.App.Views;
using StreamDeck.Application.Device;
using StreamDeck.Application.Execution;
using StreamDeck.Application.Profiles;
using StreamDeck.Device.Serial;
using StreamDeck.Infrastructure.Execution;
using StreamDeck.Infrastructure.Persistence.Json;

namespace StreamDeck.App;

public partial class App : Avalonia.Application
{
    private MainWindow? _mainWindow;
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var profileRepository = new JsonProfileRepository();
            var profileService = new ProfileService(profileRepository);
            var applicationLauncher = new SystemApplicationLauncher();
            var urlLauncher = new SystemUrlLauncher();
            var buttonActionExecutionService = new ButtonActionExecutionService(
                profileRepository, applicationLauncher, urlLauncher);
            var deviceConnection = new SerialDeviceConnection();

            _mainWindow = new MainWindow
            {
                DataContext = new MainViewModel(profileService, buttonActionExecutionService, deviceConnection),
            };

            desktop.MainWindow = _mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnTrayIconClicked(object? sender, EventArgs e)
    {
        RestoreMainWindow();
    }

    private void OnShowMainWindowClicked(object? sender, EventArgs e)
    {
        RestoreMainWindow();
    }

    private void OnExitApplicationClicked(object? sender, EventArgs e)
    {
        // Utiliza el cierre normal de la ventana para que se ejecute
        // la desconexión ordenada del ESP32 cuando corresponda.
        _mainWindow?.Close();
    }

    private void RestoreMainWindow()
    {
        _mainWindow?.RestoreFromTray();
    }
}