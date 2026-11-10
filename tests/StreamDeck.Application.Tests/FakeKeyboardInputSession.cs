using StreamDeck.Application.Execution;
using StreamDeck.Domain.Entities;

namespace StreamDeck.Application.Tests;

internal sealed class FakeKeyboardInputSession : IKeyboardInputSession
{
    public bool IsAuthorized { get; set; }

    public int SendShortcutCallCount { get; private set; }

    public KeyboardShortcutAction? LastAction { get; private set; }

    public event EventHandler? AuthorizationLost;

    public Task EnsureAuthorizedAsync(CancellationToken cancellationToken = default)
    {
        IsAuthorized = true;
        return Task.CompletedTask;
    }

    public Task SendShortcutAsync(
        KeyboardShortcutAction action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsAuthorized)
        {
            throw new KeyboardAuthorizationDeniedException("Keyboard is not authorized.");
        }

        SendShortcutCallCount++;
        LastAction = action;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsAuthorized = false;
        return ValueTask.CompletedTask;
    }

    public void RaiseAuthorizationLost()
    {
        IsAuthorized = false;
        AuthorizationLost?.Invoke(this, EventArgs.Empty);
    }
}
