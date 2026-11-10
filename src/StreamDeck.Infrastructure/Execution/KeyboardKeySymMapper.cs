using StreamDeck.Domain.Entities;

namespace StreamDeck.Infrastructure.Execution;

/// <summary>
/// Converts the application's layout-independent key identifiers to X11 KeySyms,
/// which are the values accepted by the XDG RemoteDesktop NotifyKeyboardKeysym API.
/// </summary>
internal static class KeyboardKeySymMapper
{
    internal static int GetKeySym(KeyboardKey key)
    {
        var numericKey = (int)key;
        if (numericKey >= (int)KeyboardKey.A && numericKey <= (int)KeyboardKey.Z)
        {
            return 'a' + (numericKey - (int)KeyboardKey.A);
        }

        if (numericKey >= (int)KeyboardKey.Digit0 && numericKey <= (int)KeyboardKey.Digit9)
        {
            return '0' + (numericKey - (int)KeyboardKey.Digit0);
        }

        if (numericKey >= (int)KeyboardKey.F1 && numericKey <= (int)KeyboardKey.F12)
        {
            return 0xFFBE + (numericKey - (int)KeyboardKey.F1);
        }

        return key switch
        {
            KeyboardKey.Enter => 0xFF0D,      // XK_Return
            KeyboardKey.Escape => 0xFF1B,     // XK_Escape
            KeyboardKey.Tab => 0xFF09,        // XK_Tab
            KeyboardKey.Space => 0x0020,      // XK_space
            KeyboardKey.Backspace => 0xFF08,   // XK_BackSpace
            KeyboardKey.Delete => 0xFFFF,      // XK_Delete
            KeyboardKey.Insert => 0xFF63,      // XK_Insert
            KeyboardKey.Home => 0xFF50,        // XK_Home
            KeyboardKey.End => 0xFF57,         // XK_End
            KeyboardKey.PageUp => 0xFF55,      // XK_Page_Up
            KeyboardKey.PageDown => 0xFF56,    // XK_Page_Down
            KeyboardKey.ArrowUp => 0xFF52,     // XK_Up
            KeyboardKey.ArrowDown => 0xFF54,   // XK_Down
            KeyboardKey.ArrowLeft => 0xFF51,   // XK_Left
            KeyboardKey.ArrowRight => 0xFF53,  // XK_Right
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "La tecla no tiene un KeySym compatible."),
        };
    }

    internal static int GetModifierKeySym(KeyboardModifier modifier) => modifier switch
    {
        KeyboardModifier.Ctrl => 0xFFE3,  // XK_Control_L
        KeyboardModifier.Alt => 0xFFE9,   // XK_Alt_L
        KeyboardModifier.Shift => 0xFFE1, // XK_Shift_L
        KeyboardModifier.Super => 0xFFEB, // XK_Super_L
        _ => throw new ArgumentOutOfRangeException(nameof(modifier), modifier, "El modificador no tiene un KeySym compatible."),
    };
}
