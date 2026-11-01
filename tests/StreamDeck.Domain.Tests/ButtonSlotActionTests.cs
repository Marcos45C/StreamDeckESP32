using StreamDeck.Domain.Entities;

namespace StreamDeck.Domain.Tests;

public class ButtonSlotActionTests
{
    [Fact]
    public void NewButtonSlot_HasNoAction()
    {
        var profile = Profile.Create("Mi perfil");
        var slot = profile.Pages[0].Slots[0];

        Assert.Null(slot.Action);
    }

    [Fact]
    public void AssignAction_WithLaunchApplicationAction_SetsAction()
    {
        var profile = Profile.Create("Mi perfil");
        var slot = profile.Pages[0].Slots[0];
        var action = new LaunchApplicationAction("/usr/bin/firefox");

        slot.AssignAction(action);

        Assert.Same(action, slot.Action);
    }

    [Fact]
    public void AssignAction_CalledTwice_ReplacesPreviousAction()
    {
        var profile = Profile.Create("Mi perfil");
        var slot = profile.Pages[0].Slots[0];
        slot.AssignAction(new LaunchApplicationAction("/usr/bin/firefox"));

        var secondAction = new LaunchApplicationAction("/usr/bin/code");
        slot.AssignAction(secondAction);

        Assert.Same(secondAction, slot.Action);
    }

    [Fact]
    public void ClearAction_RemovesAssignedAction()
    {
        var profile = Profile.Create("Mi perfil");
        var slot = profile.Pages[0].Slots[0];
        slot.AssignAction(new LaunchApplicationAction("/usr/bin/firefox"));

        slot.ClearAction();

        Assert.Null(slot.Action);
    }

    [Fact]
    public void AssignAction_WithNull_ThrowsArgumentNullException()
    {
        var profile = Profile.Create("Mi perfil");
        var slot = profile.Pages[0].Slots[0];

        Assert.Throws<ArgumentNullException>(() => slot.AssignAction(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void LaunchApplicationAction_WithInvalidPath_ThrowsArgumentException(string? invalidPath)
    {
        Assert.Throws<ArgumentException>(() => new LaunchApplicationAction(invalidPath!));
    }

    [Fact]
    public void LaunchApplicationAction_TrimsApplicationPath()
    {
        var action = new LaunchApplicationAction("   /usr/bin/firefox   ");

        Assert.Equal("/usr/bin/firefox", action.ApplicationPath);
    }
    [Fact]
    public void AssigningActionToOneSlot_DoesNotAffectAnotherSlot()
    {
        var profile = Profile.Create("Mi perfil");
        var slot1 = profile.Pages[0].Slots[0];
        var slot2 = profile.Pages[0].Slots[1];

        slot1.AssignAction(new LaunchApplicationAction("/usr/bin/firefox"));

        Assert.NotNull(slot1.Action);
        Assert.Null(slot2.Action);
    }
}