using System.Globalization;
using Avalonia.Data.Converters;
using StreamDeck.Domain.Entities;

namespace StreamDeck.App.Converters;

/// <summary>
/// Convierte el ButtonAction? de un ButtonSlot en el texto secundario que se muestra
/// en la grilla: "Sin configurar" si está vacío, o el nombre del ejecutable si tiene
/// una LaunchApplicationAction asignada.
/// </summary>
public sealed class ButtonActionStatusConverter : IValueConverter
{
    public static readonly ButtonActionStatusConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            LaunchApplicationAction launch => GetDisplayName(launch.ApplicationPath),
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
}