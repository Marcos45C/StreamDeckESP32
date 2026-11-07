namespace StreamDeck.Application.Execution;

/// <summary>
/// Represents an explicitly authorized desktop session that can provide keyboard input.
/// The application layer deliberately does not expose D-Bus or platform-specific types.
/// </summary>
public interface IKeyboardInputSession : IAsyncDisposable
{
    bool IsAuthorized { get; }

    event EventHandler? AuthorizationLost;

    Task EnsureAuthorizedAsync(CancellationToken cancellationToken = default);
}

public sealed class KeyboardAuthorizationDeniedException : InvalidOperationException
{
    public KeyboardAuthorizationDeniedException(string message) : base(message)
    {
    }
}
