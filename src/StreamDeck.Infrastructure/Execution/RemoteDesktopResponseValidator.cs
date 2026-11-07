using Tmds.DBus.Protocol;
using StreamDeck.Application.Execution;

namespace StreamDeck.Infrastructure.Execution;

internal static class RemoteDesktopResponseValidator
{
    internal const uint SuccessResponse = 0;
    internal const uint UserCancelledResponse = 1;
    internal const uint OtherResponse = 2;

    internal const uint KeyboardDeviceType = 1;
    internal const uint PointerDeviceType = 2;
    internal const uint TouchscreenDeviceType = 4;

    internal static void EnsureRequestSucceeded(uint response, string operation)
    {
        switch (response)
        {
            case SuccessResponse:
                return;
            case UserCancelledResponse:
                throw new KeyboardAuthorizationDeniedException(
                    $"La autorización de teclado fue cancelada durante la operación '{operation}'.");
            case OtherResponse:
                throw new InvalidOperationException(
                    $"El portal de escritorio finalizó la operación '{operation}' sin conceder el permiso.");
            default:
                throw new InvalidDataException(
                    $"El portal de escritorio devolvió el código de respuesta desconocido {response} durante '{operation}'.");
        }
    }

    internal static uint GetRequiredUInt32Value(VariantValue value, string propertyName)
    {
        if (value.Type != VariantValueType.UInt32)
        {
            throw new InvalidDataException(
                $"El portal RemoteDesktop devolvió un tipo inesperado para la propiedad '{propertyName}'.");
        }

        return value.GetUInt32();
    }

    internal static string GetRequiredSessionHandle(Dictionary<string, VariantValue> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        // The portal's public API returns this value as a D-Bus string ("s"),
        // even though the string contains an object path. This is a documented
        // historical API quirk and must not be read as VariantValueType.ObjectPath.
        if (!results.TryGetValue("session_handle", out var value) ||
            value.Type != VariantValueType.String)
        {
            throw new InvalidDataException(
                "El portal no devolvió un 'session_handle' de tipo string al crear la sesión de escritorio.");
        }

        var path = value.GetString();
        const string sessionPrefix = "/org/freedesktop/portal/desktop/session/";
        if (string.IsNullOrWhiteSpace(path) ||
            !path.StartsWith(sessionPrefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "El portal devolvió una ruta de sesión que no pertenece al espacio de nombres RemoteDesktop esperado.");
        }

        var pathSegments = path[sessionPrefix.Length..].Split('/');
        if (pathSegments.Length != 2 || pathSegments.Any(segment =>
                segment.Length == 0 || segment.Any(character =>
                    !char.IsAsciiLetterOrDigit(character) && character != '_')))
        {
            throw new InvalidDataException(
                "El portal devolvió una ruta de sesión con segmentos inválidos.");
        }

        return path;
    }

    internal static void EnsureKeyboardOnlyGranted(Dictionary<string, VariantValue> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (!results.TryGetValue("devices", out var value) ||
            value.Type != VariantValueType.UInt32)
        {
            throw new InvalidDataException(
                "El portal no informó los dispositivos autorizados en la respuesta de Start.");
        }

        var devices = value.GetUInt32();

        if ((devices & KeyboardDeviceType) == 0)
        {
            throw new KeyboardAuthorizationDeniedException(
                "KDE no concedió el permiso de teclado necesario para ejecutar atajos.");
        }

        var unexpectedDevices = devices & ~KeyboardDeviceType;
        if (unexpectedDevices != 0)
        {
            throw new InvalidDataException(
                "El portal informó permisos adicionales de mouse o pantalla táctil. StreamDeckESP32 solo acepta permiso de teclado.");
        }
    }
}
