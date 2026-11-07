using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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
    private IKeyboardInputSession? _keyboardInputSession;

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

            if (OperatingSystem.IsLinux())
            {
                _keyboardInputSession = new WaylandKeyboardInputSession();
                _keyboardInputSession.AuthorizationLost += OnKeyboardAuthorizationLost;
            }

            _mainWindow = new MainWindow
            {
                DataContext = new MainViewModel(profileService, buttonActionExecutionService, deviceConnection),
                ShutdownAsync = DisposeKeyboardInputSessionAsync,
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
        // El cierre de MainWindow desconecta el ESP32 y libera la sesión Wayland.
        _mainWindow?.Close();
    }

    private async void OnAuthorizeKeyboardClicked(object? sender, EventArgs e)
    {
        RestoreMainWindow();

        if (_mainWindow?.DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (!OperatingSystem.IsLinux())
        {
            viewModel.KeyboardAuthorizationStatusText =
                "La autorización de atajos está disponible únicamente en Linux Wayland en esta etapa.";
            return;
        }

        if (_keyboardInputSession is null)
        {
            viewModel.KeyboardAuthorizationStatusText =
                "No se pudo inicializar el servicio de autorización de teclado.";
            return;
        }

        viewModel.KeyboardAuthorizationStatusText =
            "Esperando la autorización de teclado de KDE…";

        try
        {
            await _keyboardInputSession.EnsureAuthorizedAsync();
            viewModel.KeyboardAuthorizationStatusText =
                "Teclado autorizado. La sesión permanecerá activa mientras StreamDeck ESP32 esté abierto.";
        }
        catch (KeyboardAuthorizationDeniedException ex)
        {
            viewModel.KeyboardAuthorizationStatusText = ex.Message;
        }
        catch (OperationCanceledException)
        {
            viewModel.KeyboardAuthorizationStatusText =
                "La solicitud de autorización fue interrumpida.";
        }
        catch (Exception ex)
        {
            viewModel.KeyboardAuthorizationStatusText =
                $"No se pudo autorizar el teclado: {ex.Message}";
        }
    }

    private void OnKeyboardAuthorizationLost(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_mainWindow?.DataContext is MainViewModel viewModel)
            {
                viewModel.KeyboardAuthorizationStatusText =
                    "La sesión de teclado fue cerrada por KDE. Podés volver a autorizarla desde el icono de la bandeja.";
            }
        });
    }

    private async Task DisposeKeyboardInputSessionAsync()
    {
        if (_keyboardInputSession is null)
        {
            return;
        }

        _keyboardInputSession.AuthorizationLost -= OnKeyboardAuthorizationLost;
        await _keyboardInputSession.DisposeAsync();
        _keyboardInputSession = null;
    }

    private void RestoreMainWindow()
    {
        _mainWindow?.RestoreFromTray();
    }
}