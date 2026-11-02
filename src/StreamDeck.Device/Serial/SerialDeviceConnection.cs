using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using StreamDeck.Application.Device;

namespace StreamDeck.Device.Serial;

public sealed class SerialDeviceConnection : IDeviceConnection
{
    private const int BaudRate = 115200;
    private const int ReadTimeoutMilliseconds = 200;
    private const int WriteTimeoutMilliseconds = 500;
    private const int MaxMessageContentLength = 64;
    private const int ProtocolVersion = 1;
    private const string ExpectedDeviceModel = "ESP32-WROOM-32";
    private const int ExpectedButtonCount = 9;

    private static readonly TimeSpan InitialHandshakeDelay = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan HandshakeRetryInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly object _connectionSync = new();
    private readonly StringBuilder _lineBuffer = new(MaxMessageContentLength + 1);

    private SerialPort? _serialPort;
    private CancellationTokenSource? _connectCancellation;
    private CancellationTokenSource? _readCancellation;
    private Task? _readTask;
    private TaskCompletionSource<HandshakeOutcome>? _handshakeCompletionSource;
    private bool _lineOverflowing;
    private int _readFailed;
    private int _connectionGeneration;

    public DeviceConnectionState State { get; private set; } = DeviceConnectionState.Disconnected;

    public event EventHandler<DeviceConnectionStateChangedEventArgs>? StateChanged;
    public event EventHandler<DeviceButtonPressedEventArgs>? ButtonPressed;

    public IReadOnlyList<string> GetAvailablePortNames() => SerialPort.GetPortNames();

    public async Task ConnectAsync(string portName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            throw new ArgumentException("Debe indicarse un puerto serie.", nameof(portName));
        }

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (State is DeviceConnectionState.Connecting or DeviceConnectionState.Connected)
            {
                throw new InvalidOperationException("Ya existe un intento de conexión o una conexión establecida.");
            }

            await CloseResourcesAsync().ConfigureAwait(false);

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lock (_connectionSync)
            {
                _connectCancellation = connectCts;
            }

            SetState(DeviceConnectionState.Connecting);

            try
            {
                connectCts.Token.ThrowIfCancellationRequested();

                _serialPort = new SerialPort(portName, BaudRate, Parity.None, 8, StopBits.One)
                {
                    Handshake = Handshake.None,
                    DtrEnable = false,
                    RtsEnable = false,
                    ReadTimeout = ReadTimeoutMilliseconds,
                    WriteTimeout = WriteTimeoutMilliseconds,
                    NewLine = "\n",
                };

                _serialPort.Open();
                Volatile.Write(ref _readFailed, 0);
                var generation = Interlocked.Increment(ref _connectionGeneration);
                _handshakeCompletionSource = new TaskCompletionSource<HandshakeOutcome>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                _readCancellation = new CancellationTokenSource();
                var readToken = _readCancellation.Token;
                _readTask = Task.Run(() => ReadLoop(readToken, generation));

                var outcome = await PerformHandshakeAsync(connectCts.Token).ConfigureAwait(false);

                if (Volatile.Read(ref _readFailed) != 0)
                {
                    outcome = HandshakeOutcome.TransportLost;
                }

                switch (outcome)
                {
                    case HandshakeOutcome.Ready:
                        connectCts.Token.ThrowIfCancellationRequested();
                        SetState(DeviceConnectionState.Connected);
                        break;

                    case HandshakeOutcome.UnsupportedVersion:
                        await CloseResourcesAsync().ConfigureAwait(false);
                        SetState(DeviceConnectionState.Error,
                            "El dispositivo respondió que la versión del protocolo no es compatible.");
                        break;

                    case HandshakeOutcome.TimedOut:
                        await CloseResourcesAsync().ConfigureAwait(false);
                        SetState(DeviceConnectionState.Error,
                            "El dispositivo no respondió dentro de los 5 segundos del handshake.");
                        break;

                    case HandshakeOutcome.TransportLost:
                        await CloseResourcesAsync().ConfigureAwait(false);
                        SetState(DeviceConnectionState.Error,
                            "Se perdió la comunicación con el dispositivo durante el handshake.");
                        break;
                }
            }
            catch (OperationCanceledException) when (connectCts.IsCancellationRequested)
            {
                await CloseResourcesAsync().ConfigureAwait(false);
                SetState(DeviceConnectionState.Disconnected);

                // Una cancelación externa se propaga al llamador. Una cancelación solicitada
                // por DisconnectAsync se considera una desconexión normal.
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                await CloseResourcesAsync().ConfigureAwait(false);
                SetState(DeviceConnectionState.Error, ex.Message);
            }
            finally
            {
                _handshakeCompletionSource = null;
                lock (_connectionSync)
                {
                    if (ReferenceEquals(_connectCancellation, connectCts))
                    {
                        _connectCancellation = null;
                    }
                }
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        // Cancelar antes de esperar el gate permite interrumpir un ConnectAsync que está
        // esperando el handshake mientras conserva el gate de ciclo de vida.
        lock (_connectionSync)
        {
            try
            {
                _connectCancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // ConnectAsync terminó justo antes de la solicitud de cancelación.
            }
        }

        await _lifecycleGate.WaitAsync().ConfigureAwait(false);

        try
        {
            await CloseResourcesAsync().ConfigureAwait(false);
            SetState(DeviceConnectionState.Disconnected);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    // ---------- Handshake ----------

    private enum HandshakeOutcome
    {
        Ready,
        UnsupportedVersion,
        TimedOut,
        TransportLost,
    }

    private async Task<HandshakeOutcome> PerformHandshakeAsync(CancellationToken cancellationToken)
    {
        var completion = _handshakeCompletionSource
            ?? throw new InvalidOperationException("No se inicializó el handshake.");

        await Task.Delay(InitialHandshakeDelay, cancellationToken).ConfigureAwait(false);

        // La ventana de cinco segundos comienza con el primer HELLO, según el protocolo.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(HandshakeTimeout);

        try
        {
            if (completion.Task.IsCompleted)
            {
                return await completion.Task.ConfigureAwait(false);
            }

            SendHello();

            while (true)
            {
                var delayTask = Task.Delay(HandshakeRetryInterval, timeoutCts.Token);
                var completed = await Task.WhenAny(completion.Task, delayTask).ConfigureAwait(false);

                if (completed == completion.Task)
                {
                    return await completion.Task.ConfigureAwait(false);
                }

                // Task.WhenAny no propaga la cancelación del Task.Delay. Esperarlo aquí
                // asegura que el timeout o la cancelación externa detengan los reintentos.
                await delayTask.ConfigureAwait(false);
                SendHello();
            }
        }
        catch (OperationCanceledException) when (
            timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return HandshakeOutcome.TimedOut;
        }
    }

    private void SendHello()
    {
        var port = _serialPort ?? throw new InvalidOperationException("El puerto serie no está disponible.");
        port.Write("HELLO 1\n");
    }

    // ---------- Lectura en segundo plano ----------

    private void ReadLoop(CancellationToken cancellationToken, int generation)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int value;

                try
                {
                    var port = _serialPort;
                    if (port is null)
                    {
                        return;
                    }

                    value = port.ReadByte();
                }
                catch (TimeoutException)
                {
                    continue;
                }

                if (value >= 0)
                {
                    ProcessIncomingByte((byte)value);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            if (cancellationToken.IsCancellationRequested ||
                generation != Volatile.Read(ref _connectionGeneration))
            {
                return;
            }

            Volatile.Write(ref _readFailed, 1);
            var message = $"Se perdió la conexión con el dispositivo: {ex.Message}";

            if (State == DeviceConnectionState.Connecting)
            {
                _handshakeCompletionSource?.TrySetResult(HandshakeOutcome.TransportLost);
            }
            else if (State == DeviceConnectionState.Connected)
            {
                SetState(DeviceConnectionState.Disconnected, message);
                _ = CleanupAfterUnexpectedDisconnectAsync(generation);
            }
        }
    }

    private async Task CleanupAfterUnexpectedDisconnectAsync(int generation)
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            // Una reconexión puede haber comenzado mientras esta limpieza esperaba el gate.
            // Nunca permitir que la limpieza de una conexión anterior cierre la nueva.
            if (generation == Volatile.Read(ref _connectionGeneration))
            {
                await CloseResourcesAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    // ---------- Framing: LF/CRLF y límite de 64 bytes ----------

    private void ProcessIncomingByte(byte value)
    {
        var character = (char)value;

        if (character == '\n')
        {
            if (!_lineOverflowing)
            {
                var content = _lineBuffer.ToString();
                if (content.Length > 0 && content[^1] == '\r')
                {
                    content = content[..^1];
                }

                if (content.Length <= MaxMessageContentLength)
                {
                    HandleReceivedLine(content);
                }
            }

            _lineBuffer.Clear();
            _lineOverflowing = false;
            return;
        }

        if (_lineOverflowing)
        {
            return;
        }

        // Se permite un byte extra únicamente para el CR final de CRLF.
        if (_lineBuffer.Length >= MaxMessageContentLength + 1)
        {
            _lineOverflowing = true;
            return;
        }

        _lineBuffer.Append(character);
    }

    // ---------- Procesamiento de mensajes ----------

    private void HandleReceivedLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        var parts = line.Split(' ', StringSplitOptions.None);

        switch (parts[0])
        {
            case "READY":
                if (IsValidReadyLine(parts))
                {
                    _handshakeCompletionSource?.TrySetResult(HandshakeOutcome.Ready);
                }
                break;

            case "ERROR":
                if (parts.Length == 2 && parts[1] == "UNSUPPORTED_VERSION")
                {
                    _handshakeCompletionSource?.TrySetResult(HandshakeOutcome.UnsupportedVersion);
                }
                break;

            case "BUTTON":
                HandleButtonLine(parts);
                break;

            default:
                break;
        }
    }

    private static bool IsValidReadyLine(string[] parts)
    {
        return parts.Length == 4
            && parts[0] == "READY"
            && parts[1] == ProtocolVersion.ToString(CultureInfo.InvariantCulture)
            && parts[2] == ExpectedDeviceModel
            && parts[3] == ExpectedButtonCount.ToString(CultureInfo.InvariantCulture);
    }

    private void HandleButtonLine(string[] parts)
    {
        if (State != DeviceConnectionState.Connected)
        {
            return;
        }

        if (parts.Length != 2
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var position)
            || position < 1
            || position > ExpectedButtonCount)
        {
            return;
        }

        ButtonPressed?.Invoke(this, new DeviceButtonPressedEventArgs(position));
    }

    // ---------- Cierre y limpieza ----------

    private async Task CloseResourcesAsync()
    {
        // Invalida cualquier tarea de lectura/limpieza que pertenezca a la conexión anterior.
        Interlocked.Increment(ref _connectionGeneration);

        var readCancellation = _readCancellation;
        readCancellation?.Cancel();

        var readTask = _readTask;
        if (readTask is not null)
        {
            try
            {
                await readTask.ConfigureAwait(false);
            }
            catch
            {
                // El bucle de lectura gestiona sus errores; aquí solo esperamos su finalización.
            }
        }

        _readTask = null;
        _readCancellation = null;
        readCancellation?.Dispose();

        var port = _serialPort;
        _serialPort = null;

        if (port is not null)
        {
            try
            {
                if (port.IsOpen)
                {
                    port.Close();
                }
            }
            catch
            {
                // Mejor esfuerzo: liberar el objeto aunque el dispositivo haya desaparecido.
            }
            finally
            {
                port.Dispose();
            }
        }

        _handshakeCompletionSource = null;
        _lineBuffer.Clear();
        _lineOverflowing = false;
        Volatile.Write(ref _readFailed, 0);
    }

    private void SetState(DeviceConnectionState state, string? errorMessage = null)
    {
        State = state;
        StateChanged?.Invoke(this, new DeviceConnectionStateChangedEventArgs(state, errorMessage));
    }
}
