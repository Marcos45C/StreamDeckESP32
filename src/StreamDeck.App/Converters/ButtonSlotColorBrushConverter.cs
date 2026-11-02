using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace StreamDeck.App.Converters;

public sealed class ButtonSlotColorBrushConverter : IValueConverter
{
    public static readonly ButtonSlotColorBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string colorHex || string.IsNullOrWhiteSpace(colorHex))
        {
            return Brushes.Transparent;
        }

        return new SolidColorBrush(Color.Parse(colorHex));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
