using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using StreamDeck.Application.Device;

namespace StreamDeck.App.Converters;

public sealed class DeviceConnectionStateColorConverter : IValueConverter
{
    public static readonly DeviceConnectionStateColorConverter Instance = new();

    private static readonly IBrush DisconnectedBrush = new SolidColorBrush(Color.Parse("#6B6B70"));
    private static readonly IBrush ConnectingBrush = new SolidColorBrush(Color.Parse("#D1A84B"));
    private static readonly IBrush ConnectedBrush = new SolidColorBrush(Color.Parse("#4CAF78"));
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#E88A8A"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            DeviceConnectionState.Connecting => ConnectingBrush,
            DeviceConnectionState.Connected => ConnectedBrush,
            DeviceConnectionState.Error => ErrorBrush,
            _ => DisconnectedBrush,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}