namespace StreamDeck.Domain.Entities;

/// <summary>
/// Representa una combinación de modificadores y una tecla principal.
/// No simula la entrada por sí misma: su ejecución corresponde a Infrastructure.
/// </summary>
public sealed class KeyboardShortcutAction : ButtonAction
{
    public IReadOnlyList<KeyboardModifier> Modifiers { get; }

    public KeyboardKey Key { get; }

    public KeyboardShortcutAction(
        KeyboardKey key,
        IEnumerable<KeyboardModifier>? modifiers = null)
    {
        if (!Enum.IsDefined(typeof(KeyboardKey), key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "La tecla seleccionada no está admitida.");
        }

        var modifierList = (modifiers ?? Array.Empty<KeyboardModifier>()).ToArray();

        if (modifierList.Any(modifier => !Enum.IsDefined(typeof(KeyboardModifier), modifier)))
        {
            throw new ArgumentException("La combinación contiene un modificador no admitido.", nameof(modifiers));
        }

        if (modifierList.Distinct().Count() != modifierList.Length)
        {
            throw new ArgumentException("La combinación no puede repetir modificadores.", nameof(modifiers));
        }

        // Un orden estable simplifica la persistencia y las comparaciones.
        Modifiers = Array.AsReadOnly(modifierList.OrderBy(modifier => modifier).ToArray());
        Key = key;
    }
}
