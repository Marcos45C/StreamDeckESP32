using StreamDeck.Infrastructure.Execution;

namespace StreamDeck.Infrastructure.Tests.Execution;

public sealed class WaylandKeyboardInputSessionLifecycleTests
{
    [Fact]
    public async Task DisposeAsync_CanBeCalledMoreThanOnce()
    {
        var session = new WaylandKeyboardInputSession();

        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert.False(session.IsAuthorized);
    }
}
