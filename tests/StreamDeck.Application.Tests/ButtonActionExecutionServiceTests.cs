using StreamDeck.Application.Execution;
using StreamDeck.Domain.Entities;

namespace StreamDeck.Application.Tests;

public class ButtonActionExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WithLaunchApplicationAction_DelegatesCorrectPathToLauncher()
    {
        var repository = new InMemoryProfileRepository();
        var profile = Profile.Create("Mi perfil");
        profile.Pages[0].Slots[0].AssignAction(new LaunchApplicationAction("/usr/bin/firefox"));
        await repository.SaveAsync(profile);

        var applicationLauncher = new FakeApplicationLauncher();
        var urlLauncher = new FakeUrlLauncher();
        var service = new ButtonActionExecutionService(repository, applicationLauncher, urlLauncher);

        await service.ExecuteAsync(profile.Id, profile.Pages[0].Id, 1);

        Assert.Equal(1, applicationLauncher.LaunchCallCount);
        Assert.Equal("/usr/bin/firefox", applicationLauncher.LastApplicationPath);
        Assert.Equal(0, urlLauncher.OpenCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithOpenUrlAction_DelegatesCorrectUrlToLauncher()
    {
        var repository = new InMemoryProfileRepository();
        var profile = Profile.Create("Mi perfil");
        profile.Pages[0].Slots[0].AssignAction(new OpenUrlAction("https://example.com/ruta?q=1"));
        await repository.SaveAsync(profile);

        var applicationLauncher = new FakeApplicationLauncher();
        var urlLauncher = new FakeUrlLauncher();
        var service = new ButtonActionExecutionService(repository, applicationLauncher, urlLauncher);

        await service.ExecuteAsync(profile.Id, profile.Pages[0].Id, 1);

        Assert.Equal(1, urlLauncher.OpenCallCount);
        Assert.Equal("https://example.com/ruta?q=1", urlLauncher.LastUrl);
        Assert.Equal(0, applicationLauncher.LaunchCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnconfiguredButton_ThrowsButtonActionNotConfiguredExceptionAndDoesNotLaunch()
    {
        var repository = new InMemoryProfileRepository();
        var profile = Profile.Create("Mi perfil");
        await repository.SaveAsync(profile);

        var applicationLauncher = new FakeApplicationLauncher();
        var urlLauncher = new FakeUrlLauncher();
        var service = new ButtonActionExecutionService(repository, applicationLauncher, urlLauncher);

        await Assert.ThrowsAsync<ButtonActionNotConfiguredException>(
            () => service.ExecuteAsync(profile.Id, profile.Pages[0].Id, 1));

        Assert.Equal(0, applicationLauncher.LaunchCallCount);
        Assert.Equal(0, urlLauncher.OpenCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownProfileId_ThrowsKeyNotFoundExceptionAndDoesNotLaunch()
    {
        var repository = new InMemoryProfileRepository();
        var applicationLauncher = new FakeApplicationLauncher();
        var urlLauncher = new FakeUrlLauncher();
        var service = new ButtonActionExecutionService(repository, applicationLauncher, urlLauncher);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), 1));

        Assert.Equal(0, applicationLauncher.LaunchCallCount);
        Assert.Equal(0, urlLauncher.OpenCallCount);
    }
}
