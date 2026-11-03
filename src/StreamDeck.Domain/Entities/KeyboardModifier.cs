namespace StreamDeck.Domain.Entities;

/// <summary>
/// Modificadores admitidos para una combinación de teclado.
/// Los valores son independientes del sistema operativo; Infrastructure los traduce
/// a los códigos correspondientes de cada entorno.
/// </summary>
public enum KeyboardModifier
{
    Ctrl = 1,
    Alt = 2,
    Shift = 3,
    Super = 4,
}
