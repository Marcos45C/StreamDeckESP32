namespace StreamDeck.Domain.Entities;

/// <summary>
/// Teclas principales admitidas por las acciones de atajo.
/// Los nombres son identificadores estables para la persistencia y no dependen
/// de la distribución física del teclado.
/// </summary>
public enum KeyboardKey
{
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    Digit0, Digit1, Digit2, Digit3, Digit4,
    Digit5, Digit6, Digit7, Digit8, Digit9,

    F1, F2, F3, F4, F5, F6,
    F7, F8, F9, F10, F11, F12,

    Enter,
    Escape,
    Tab,
    Space,
    Backspace,
    Delete,
    Insert,
    Home,
    End,
    PageUp,
    PageDown,
    ArrowUp,
    ArrowDown,
    ArrowLeft,
    ArrowRight,
}
