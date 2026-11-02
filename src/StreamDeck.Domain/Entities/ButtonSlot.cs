namespace StreamDeck.Domain.Entities;

public sealed class ButtonSlot
{
    public int Position { get; }

    public int Row => (Position - 1) / 3 + 1;

    public int Column => (Position - 1) % 3 + 1;

    public ButtonAction? Action { get; private set; }

    /// <summary>
    /// Color de fondo opcional del botón en la interfaz de escritorio, en formato #RRGGBB.
    /// No representa una salida física del ESP32.
    /// </summary>
    public string? ColorHex { get; private set; }

    internal ButtonSlot(int position)
    {
        Position = position;
    }

    public void AssignAction(ButtonAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Action = action;
    }

    public void ClearAction()
    {
        Action = null;
    }

    public void SetColor(string colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            throw new ArgumentException("El color no puede estar vacío.", nameof(colorHex));
        }

        var normalized = colorHex.Trim().ToUpperInvariant();

        if (normalized.Length != 7 || normalized[0] != '#' || normalized.Skip(1).Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("El color debe tener el formato hexadecimal #RRGGBB.", nameof(colorHex));
        }

        ColorHex = normalized;
    }

    public void ClearColor()
    {
        ColorHex = null;
    }
}
