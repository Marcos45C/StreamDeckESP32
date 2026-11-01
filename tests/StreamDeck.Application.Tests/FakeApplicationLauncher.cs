using StreamDeck.Application.Execution;

namespace StreamDeck.Application.Tests;

internal sealed class FakeApplicationLauncher : IApplicationLauncher
{
    public int LaunchCallCount { get; private set; }
    public string? LastApplicationPath { get; private set; }

    public void Launch(string applicationPath)
    {
        LaunchCallCount++;
        LastApplicationPath = applicationPath;
    }
}