using StreamDeck.Domain.Entities;
using Tmds.DBus.Protocol;
using StreamDeck.Application.Execution;
using StreamDeck.Infrastructure.Wayland.DBus;

namespace StreamDeck.Infrastructure.Execution;

/// <summary>
/// Owns a RemoteDesktop portal session that is authorized for keyboard input only.
/// It manages the permission lifecycle and sends keyboard KeySym press/release events.
/// </summary>
public sealed class WaylandKeyboardInputSession : IKeyboardInputSession
{
    private const string PortalBusName = "org.freedesktop.portal.Desktop";
    private const string PortalObjectPath = "/org/freedesktop/portal/desktop";
    private const uint RequiredPortalVersion = 2;
    private const uint KeyboardDeviceType = 1;
    private const uint PersistWhileApplicationRuns = 1;
    private const string RemoteDesktopInterfaceName = "org.freedesktop.portal.RemoteDesktop";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly object _stateLock = new();

    private DBusConnection? _connection;
    private Task<Exception?>? _connectionDisconnectedTask;
    private DBusService _service;
    private RemoteDesktop? _remoteDesktop;
    private Properties? _properties;
    private Session? _session;
    private string? _sessionHandlePath;
    private IDisposable? _sessionClosedObserver;

    private bool _isAuthorized;
    private bool _sessionWasClosed;
    private bool _isClosingSession;
    private int _disposeStarted;

    public bool IsAuthorized
    {
        get
        {
            lock (_stateLock)
            {
                return _isAuthorized;
            }
        }
    }

    public event EventHandler? AuthorizationLost;

    public async Task EnsureAuthorizedAsync(CancellationToken cancellationToken = default)
    {
        EnsureSupportedEnvironment();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _disposeCancellation.Token);

        await _gate.WaitAsync(linkedCancellation.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);

            if (IsAuthorized)
            {
                return;
            }

            // If the portal closed a previous session, release its local proxy before retrying.
            await CloseCurrentSessionAsync().ConfigureAwait(false);
            await EnsureConnectedAsync().ConfigureAwait(false);

            var portalVersion = await GetRequiredPortalUInt32PropertyAsync("version").ConfigureAwait(false);
            if (portalVersion < RequiredPortalVersion)
            {
                throw new PlatformNotSupportedException(
                    $"El portal RemoteDesktop versión {portalVersion} no admite las opciones de sesión requeridas. Se necesita la versión {RequiredPortalVersion} o superior.");
            }

            var availableDevices = await GetRequiredPortalUInt32PropertyAsync("AvailableDeviceTypes").ConfigureAwait(false);
            if ((availableDevices & KeyboardDeviceType) == 0)
            {
                throw new PlatformNotSupportedException(
                    "El portal RemoteDesktop no informa que el control de teclado esté disponible.");
            }

            await CreateAndAuthorizeKeyboardSessionAsync(linkedCancellation.Token).ConfigureAwait(false);
        }
        catch
        {
            await CloseCurrentSessionAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SendShortcutAsync(
        KeyboardShortcutAction action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureSupportedEnvironment();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);

            bool isAuthorized;
            bool sessionWasClosed;
            lock (_stateLock)
            {
                isAuthorized = _isAuthorized;
                sessionWasClosed = _sessionWasClosed;
            }

            if (!isAuthorized || sessionWasClosed)
            {
                throw new KeyboardAuthorizationDeniedException(
                    "El teclado no está autorizado. Autorizalo desde el menú del icono de StreamDeck ESP32 antes de ejecutar atajos.");
            }

            var remoteDesktop = _remoteDesktop ?? throw new KeyboardAuthorizationDeniedException(
                "La conexión con el portal de KDE ya no está disponible. Volvé a autorizar el teclado desde la bandeja.");
            var sessionPath = _sessionHandlePath ?? throw new KeyboardAuthorizationDeniedException(
                "La sesión de teclado no está disponible. Volvé a autorizarla desde la bandeja.");
            var sessionHandle = new ObjectPath(sessionPath);
            var options = new Dictionary<string, VariantValue>();

            Task NotifyAsync(int keysym, uint state) =>
                remoteDesktop.NotifyKeyboardKeysymAsync(sessionHandle, options, keysym, state);

            await KeyboardShortcutSender.SendAsync(action, NotifyAsync, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void EnsureSupportedEnvironment()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "La autorización de teclado mediante XDG RemoteDesktop está implementada únicamente para Linux en esta etapa.");
        }

        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        if (!string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase))
        {
            throw new PlatformNotSupportedException(
                "La autorización de teclado de esta etapa requiere una sesión Linux Wayland (XDG_SESSION_TYPE=wayland).");
        }
    }

    private async Task EnsureConnectedAsync()
    {
        if (_connection is not null && _remoteDesktop is not null && _properties is not null)
        {
            if (_connectionDisconnectedTask is null || !_connectionDisconnectedTask.IsCompleted)
            {
                return;
            }

            var disconnectedConnection = _connection;
            lock (_stateLock)
            {
                if (ReferenceEquals(_connection, disconnectedConnection))
                {
                    _connection = null;
                    _connectionDisconnectedTask = null;
                    _remoteDesktop = null;
                    _properties = null;
                    _sessionHandlePath = null;
                    _service = default;
                }
            }

            disconnectedConnection.Dispose();
        }

        var sessionBusAddress = DBusAddress.Session;
        if (string.IsNullOrWhiteSpace(sessionBusAddress))
        {
            throw new InvalidOperationException(
                "No se encontró el bus D-Bus de sesión del usuario. No se puede solicitar autorización a KDE.");
        }

        var connection = new DBusConnection(sessionBusAddress);
        try
        {
            await connection.ConnectAsync().ConfigureAwait(false);
            var service = new DBusService(connection, PortalBusName);
            var remoteDesktop = service.CreateRemoteDesktop(PortalObjectPath);
            var properties = service.CreateProperties(PortalObjectPath);
            var disconnectedTask = connection.DisconnectedAsync();

            lock (_stateLock)
            {
                _connection = connection;
                _connectionDisconnectedTask = disconnectedTask;
                _service = service;
                _remoteDesktop = remoteDesktop;
                _properties = properties;
            }

            _ = ObserveConnectionDisconnectionAsync(connection, disconnectedTask);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private async Task ObserveConnectionDisconnectionAsync(
        DBusConnection connection,
        Task<Exception?> disconnectedTask)
    {
        try
        {
            await disconnectedTask.ConfigureAwait(false);
        }
        catch
        {
            // La pérdida de la conexión se informa mediante el estado de autorización.
        }

        if (Volatile.Read(ref _disposeStarted) == 0)
        {
            HandlePortalConnectionClosed(connection);
        }
    }

    private async Task<uint> GetRequiredPortalUInt32PropertyAsync(string propertyName)
    {
        var properties = _properties ?? throw new InvalidOperationException(
            "No está disponible la interfaz de propiedades del portal RemoteDesktop.");

        var value = await properties.GetAsync(RemoteDesktopInterfaceName, propertyName).ConfigureAwait(false);
        return RemoteDesktopResponseValidator.GetRequiredUInt32Value(value, propertyName);
    }

    private async Task CreateAndAuthorizeKeyboardSessionAsync(CancellationToken cancellationToken)
    {
        var sessionToken = CreateToken("session");
        var createRequestToken = CreateToken("create");
        var createOptions = new Dictionary<string, VariantValue>
        {
            ["handle_token"] = VariantValue.String(createRequestToken),
            ["session_handle_token"] = VariantValue.String(sessionToken),
        };

        var remoteDesktop = _remoteDesktop ?? throw new InvalidOperationException(
            "No está disponible la interfaz RemoteDesktop del portal.");

        var createResponse = await InvokePortalRequestAsync(
            createRequestToken,
            () => remoteDesktop.CreateSessionAsync(createOptions),
            cancellationToken).ConfigureAwait(false);

        RemoteDesktopResponseValidator.EnsureRequestSucceeded(createResponse.Response, "CreateSession");
        var sessionPath = RemoteDesktopResponseValidator.GetRequiredSessionHandle(createResponse.Results);

        var session = _service.CreateSession(sessionPath);
        lock (_stateLock)
        {
            _session = session;
            _sessionHandlePath = sessionPath;
            _sessionWasClosed = false;
            _isClosingSession = false;
        }

        _sessionClosedObserver = await session.WatchClosedAsync(
            (Dictionary<string, VariantValue> _) => HandlePortalSessionClosed(session)).ConfigureAwait(false);

        var selectRequestToken = CreateToken("select");
        var selectOptions = new Dictionary<string, VariantValue>
        {
            ["handle_token"] = VariantValue.String(selectRequestToken),
            ["types"] = (uint)KeyboardDeviceType,
            ["persist_mode"] = (uint)PersistWhileApplicationRuns,
        };

        var selectResponse = await InvokePortalRequestAsync(
            selectRequestToken,
            () => remoteDesktop.SelectDevicesAsync(new ObjectPath(sessionPath), selectOptions),
            cancellationToken).ConfigureAwait(false);

        RemoteDesktopResponseValidator.EnsureRequestSucceeded(selectResponse.Response, "SelectDevices");

        var startRequestToken = CreateToken("start");
        var startOptions = new Dictionary<string, VariantValue>
        {
            ["handle_token"] = VariantValue.String(startRequestToken),
        };

        var startResponse = await InvokePortalRequestAsync(
            startRequestToken,
            () => remoteDesktop.StartAsync(new ObjectPath(sessionPath), string.Empty, startOptions),
            cancellationToken).ConfigureAwait(false);

        RemoteDesktopResponseValidator.EnsureRequestSucceeded(startResponse.Response, "Start");
        RemoteDesktopResponseValidator.EnsureKeyboardOnlyGranted(startResponse.Results);

        lock (_stateLock)
        {
            if (_sessionWasClosed)
            {
                throw new InvalidOperationException(
                    "El portal cerró la sesión RemoteDesktop antes de completar la autorización.");
            }

            _isAuthorized = true;
        }
    }

    private async Task<PortalRequestResponse> InvokePortalRequestAsync(
        string handleToken,
        Func<Task<ObjectPath>> invoke,
        CancellationToken cancellationToken)
    {
        var connection = _connection ?? throw new InvalidOperationException(
            "No existe una conexión D-Bus para solicitar autorización al portal.");
        var disconnectedTask = _connectionDisconnectedTask ?? connection.DisconnectedAsync();
        var expectedRequestPath = BuildRequestPath(handleToken);
        var request = _service.CreateRequest(expectedRequestPath);
        var responseSource = new TaskCompletionSource<PortalRequestResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        IDisposable? observer = null;

        void OnResponse((uint Response, Dictionary<string, VariantValue> Results) response)
        {
            responseSource.TrySetResult(
                new PortalRequestResponse(response.Response, response.Results));
        }

        try
        {
            // Subscribe before the method call to avoid racing the portal's Response signal.
            observer = await request.WatchResponseAsync(OnResponse).ConfigureAwait(false);
            var requestHandle = await invoke().ConfigureAwait(false);
            var actualRequestPath = requestHandle.ToString();

            if (!string.Equals(actualRequestPath, expectedRequestPath, StringComparison.Ordinal))
            {
                observer.Dispose();
                request = _service.CreateRequest(actualRequestPath);
                observer = await request.WatchResponseAsync(OnResponse).ConfigureAwait(false);
            }

            var completedTask = await Task.WhenAny(responseSource.Task, disconnectedTask)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            if (completedTask != responseSource.Task)
            {
                var disconnectReason = await disconnectedTask.ConfigureAwait(false);
                throw new InvalidOperationException(
                    "Se perdió la conexión D-Bus mientras KDE procesaba la autorización de teclado.",
                    disconnectReason);
            }

            return await responseSource.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                await request.CloseAsync().ConfigureAwait(false);
            }
            catch
            {
                // Closing a request during app shutdown is best effort.
            }

            throw;
        }
        finally
        {
            observer?.Dispose();
        }
    }

    private string BuildRequestPath(string token)
    {
        var uniqueName = _connection?.UniqueName;
        if (string.IsNullOrWhiteSpace(uniqueName) || !uniqueName.StartsWith(':'))
        {
            throw new InvalidOperationException(
                "D-Bus no proporcionó un nombre único de conexión para preparar la solicitud del portal.");
        }

        var senderPathSegment = uniqueName[1..].Replace('.', '_');
        return $"/org/freedesktop/portal/desktop/request/{senderPathSegment}/{token}";
    }

    private static string CreateToken(string prefix) => $"streamdeck_{prefix}_{Guid.NewGuid():N}";

    private void HandlePortalSessionClosed(Session sourceSession)
    {
        var raiseEvent = false;
        lock (_stateLock)
        {
            if (!ReferenceEquals(_session, sourceSession))
            {
                return;
            }

            _sessionWasClosed = true;
            if (!_isClosingSession && _isAuthorized)
            {
                _isAuthorized = false;
                raiseEvent = true;
            }
        }

        if (raiseEvent)
        {
            AuthorizationLost?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HandlePortalConnectionClosed(DBusConnection sourceConnection)
    {
        var raiseEvent = false;
        lock (_stateLock)
        {
            if (!ReferenceEquals(_connection, sourceConnection))
            {
                return;
            }

            _sessionWasClosed = true;
            if (!_isClosingSession && _isAuthorized)
            {
                _isAuthorized = false;
                raiseEvent = true;
            }
        }

        if (raiseEvent)
        {
            AuthorizationLost?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task CloseCurrentSessionAsync()
    {
        Session? session;
        IDisposable? closedObserver;

        lock (_stateLock)
        {
            session = _session;
            closedObserver = _sessionClosedObserver;
            _session = null;
            _sessionHandlePath = null;
            _sessionClosedObserver = null;
            _isAuthorized = false;
            _isClosingSession = true;
        }

        try
        {
            closedObserver?.Dispose();
        }
        catch
        {
            // La conexión puede haberse perdido antes de liberar el observador.
        }

        if (session is not null)
        {
            try
            {
                await session.CloseAsync().ConfigureAwait(false);
            }
            catch
            {
                // Session.Close is best effort; disposing the D-Bus connection also ends owned sessions.
            }
        }

        lock (_stateLock)
        {
            _sessionWasClosed = false;
            _isClosingSession = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        _disposeCancellation.Cancel();
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await CloseCurrentSessionAsync().ConfigureAwait(false);
            _connection?.Dispose();
            lock (_stateLock)
            {
                _connection = null;
                _connectionDisconnectedTask = null;
                _service = default;
                _remoteDesktop = null;
                _properties = null;
                _sessionHandlePath = null;
            }
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
            _disposeCancellation.Dispose();
        }
    }

    private sealed record PortalRequestResponse(
        uint Response,
        Dictionary<string, VariantValue> Results);
}
