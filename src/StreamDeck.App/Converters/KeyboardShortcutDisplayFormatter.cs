using StreamDeck.Domain.Entities;

namespace StreamDeck.App.Converters;

public static class KeyboardShortcutDisplayFormatter
{
    public static string Format(KeyboardShortcutAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var parts = action.Modifiers
            .Select(modifier => modifier.ToString())
            .Append(FormatKey(action.Key));

        return string.Join(" + ", parts);
    }

    public static string FormatKey(KeyboardKey key)
    {
        var name = key.ToString();

        if (name.StartsWith("Digit", StringComparison.Ordinal))
        {
            return name[5..];
        }

        return key switch
        {
            KeyboardKey.Space => "Espacio",
            KeyboardKey.Backspace => "Retroceso",
            KeyboardKey.Delete => "Supr",
            KeyboardKey.Insert => "Insert",
            KeyboardKey.Home => "Inicio",
            KeyboardKey.End => "Fin",
            KeyboardKey.PageUp => "RePág",
            KeyboardKey.PageDown => "AvPág",
            KeyboardKey.ArrowUp => "↑",
            KeyboardKey.ArrowDown => "↓",
            KeyboardKey.ArrowLeft => "←",
            KeyboardKey.ArrowRight => "→",
            _ => name,
        };
    }
}
