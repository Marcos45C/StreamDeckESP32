using System.Globalization;
using Avalonia.Data.Converters;
using StreamDeck.Domain.Entities;

namespace StreamDeck.App.Converters;

/// <summary>
/// Convierte el ButtonAction? de un ButtonSlot en el texto secundario que se muestra
/// en la grilla: "Sin configurar" si está vacío, el nombre del ejecutable para una
/// LaunchApplicationAction o el host para una OpenUrlAction.
/// </summary>
public sealed class ButtonActionStatusConverter : IValueConverter
{
    public static readonly ButtonActionStatusConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            LaunchApplicationAction launch => GetDisplayName(launch.ApplicationPath),
            OpenUrlAction openUrl => GetUrlDisplayName(openUrl.Url),
            null => "Sin configurar",
            _ => "Configurado",
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static string GetDisplayName(string applicationPath)
    {
        var fileName = Path.GetFileName(applicationPath);
        return string.IsNullOrEmpty(fileName) ? applicationPath : fileName;
    }

    private static string GetUrlDisplayName(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.Host
            : url;
    }
}