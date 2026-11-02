namespace StreamDeck.Application.Device;

public enum DeviceConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error,
}

public sealed class DeviceConnectionStateChangedEventArgs : EventArgs
{
    public DeviceConnectionState State { get; }
    public string? ErrorMessage { get; }

    public DeviceConnectionStateChangedEventArgs(DeviceConnectionState state, string? errorMessage = null)
    {
        State = state;
        ErrorMessage = errorMessage;
    }
}

public sealed class DeviceButtonPressedEventArgs : EventArgs
{
    public int Position { get; }

    public DeviceButtonPressedEventArgs(int position)
    {
        Position = position;
    }
}

/// <summary>
/// Abstracción de la conexión con el dispositivo físico (ver docs/serial-protocol-v1.md).
/// La implementación concreta (USB serie, y en el futuro quizás otro transporte) vive en
/// StreamDeck.Device, nunca en Application ni en App.
/// </summary>
public interface IDeviceConnection
{
    DeviceConnectionState State { get; }

    event EventHandler<DeviceConnectionStateChangedEventArgs>? StateChanged;
    event EventHandler<DeviceButtonPressedEventArgs>? ButtonPressed;

    IReadOnlyList<string> GetAvailablePortNames();

    Task ConnectAsync(string portName, CancellationToken cancellationToken = default);

    Task DisconnectAsync();
}