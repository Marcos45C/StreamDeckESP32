using StreamDeck.Application.Execution;

namespace StreamDeck.Application.Tests;

internal sealed class FakeUrlLauncher : IUrlLauncher
{
    public int OpenCallCount { get; private set; }
    public string? LastUrl { get; private set; }

    public void Open(string url)
    {
        OpenCallCount++;
        LastUrl = url;
    }
}
