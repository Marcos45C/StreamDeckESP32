using StreamDeck.Application.Execution;
using StreamDeck.Domain.Entities;

namespace StreamDeck.Application.Tests;

public class ButtonActionExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WithConfiguredAction_DelegatesCorrectPathToLauncher()
    {
        var repository = new InMemoryProfileRepository();
        var profile = Profile.Create("Mi perfil");
        profile.Pages[0].Slots[0].AssignAction(new LaunchApplicationAction("/usr/bin/firefox"));
        await repository.SaveAsync(profile);
        var launcher = new FakeApplicationLauncher();
        var service = new ButtonActionExecutionService(repository, launcher);

        await service.ExecuteAsync(profile.Id, profile.Pages[0].Id, 1);

        Assert.Equal(1, launcher.LaunchCallCount);
        Assert.Equal("/usr/bin/firefox", launcher.LastApplicationPath);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnconfiguredButton_ThrowsInvalidOperationExceptionAndDoesNotLaunch()
    {
        var repository = new InMemoryProfileRepository();
        var profile = Profile.Create("Mi perfil");
        await repository.SaveAsync(profile);
        var launcher = new FakeApplicationLauncher();
        var service = new ButtonActionExecutionService(repository, launcher);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteAsync(profile.Id, profile.Pages[0].Id, 1));

        Assert.Equal(0, launcher.LaunchCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownProfileId_ThrowsKeyNotFoundExceptionAndDoesNotLaunch()
    {
        var repository = new InMemoryProfileRepository();
        var launcher = new FakeApplicationLauncher();
        var service = new ButtonActionExecutionService(repository, launcher);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), 1));

        Assert.Equal(0, launcher.LaunchCallCount);
    }
}