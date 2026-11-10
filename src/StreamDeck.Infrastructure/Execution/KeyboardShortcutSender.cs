using System.Runtime.ExceptionServices;
using StreamDeck.Domain.Entities;

namespace StreamDeck.Infrastructure.Execution;

/// <summary>
/// Sends a chord as ordered KeySym press/release events. Every successfully attempted
/// press is followed by a best-effort release, even when cancellation or D-Bus errors occur.
/// </summary>
internal static class KeyboardShortcutSender
{
    internal const uint Released = 0;
    internal const uint Pressed = 1;

    internal static async Task SendAsync(
        KeyboardShortcutAction action,
        Func<int, uint, Task> notifyKeyboardKeysymAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(notifyKeyboardKeysymAsync);

        var pressedKeysyms = new List<int>(action.Modifiers.Count + 1);
        Exception? sendException = null;

        try
        {
            foreach (var modifier in action.Modifiers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var modifierKeysym = KeyboardKeySymMapper.GetModifierKeySym(modifier);
                // Add before awaiting: if the D-Bus reply is lost after the portal handled
                // the press, the finally path still attempts to release this key.
                pressedKeysyms.Add(modifierKeysym);
                await notifyKeyboardKeysymAsync(modifierKeysym, Pressed).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var keyKeysym = KeyboardKeySymMapper.GetKeySym(action.Key);
            pressedKeysyms.Add(keyKeysym);
            await notifyKeyboardKeysymAsync(keyKeysym, Pressed).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            sendException = ex;
        }

        var releaseExceptions = new List<Exception>();
        for (var index = pressedKeysyms.Count - 1; index >= 0; index--)
        {
            try
            {
                // Releases must not inherit the caller's cancellation token; leaving a
                // modifier logically pressed would affect all subsequent desktop input.
                await notifyKeyboardKeysymAsync(pressedKeysyms[index], Released).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                releaseExceptions.Add(ex);
            }
        }

        if (sendException is not null && releaseExceptions.Count > 0)
        {
            throw new AggregateException(
                "Falló el envío del atajo y también la liberación de una o más teclas.",
                new[] { sendException }.Concat(releaseExceptions));
        }

        if (sendException is not null)
        {
            ExceptionDispatchInfo.Capture(sendException).Throw();
        }

        if (releaseExceptions.Count > 0)
        {
            throw new AggregateException(
                "No se pudieron liberar una o más teclas después de enviar el atajo.",
                releaseExceptions);
        }
    }
}
