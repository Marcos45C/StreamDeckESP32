using StreamDeck.Domain.Entities;

namespace StreamDeck.Domain.Tests;

public class ButtonSlotNameTests
{
    [Fact]
    public void SetCustomName_TrimsAndStoresName()
    {
        var slot = Profile.Create("Perfil").Pages[0].Slots[0];

        slot.SetCustomName("  Discord  ");

        Assert.Equal("Discord", slot.CustomName);
        Assert.True(slot.HasCustomName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void SetCustomName_WithEmptyName_ThrowsArgumentException(string? name)
    {
        var slot = Profile.Create("Perfil").Pages[0].Slots[0];

        Assert.Throws<ArgumentException>(() => slot.SetCustomName(name!));
    }

    [Fact]
    public void SetCustomName_WithMoreThan24Characters_ThrowsArgumentException()
    {
        var slot = Profile.Create("Perfil").Pages[0].Slots[0];

        Assert.Throws<ArgumentException>(() => slot.SetCustomName("1234567890123456789012345"));
    }

    [Fact]
    public void CustomName_IsIndependentFromColorAndAction()
    {
        var slot = Profile.Create("Perfil").Pages[0].Slots[0];
        slot.SetColor("#2F5F91");
        slot.AssignAction(new OpenUrlAction("https://example.com"));

        slot.SetCustomName("Navegador");
        slot.ClearAction();

        Assert.Equal("Navegador", slot.CustomName);
        Assert.Equal("#2F5F91", slot.ColorHex);
        Assert.Null(slot.Action);
    }

    [Fact]
    public void ClearCustomName_RemovesName()
    {
        var slot = Profile.Create("Perfil").Pages[0].Slots[0];
        slot.SetCustomName("Discord");

        slot.ClearCustomName();

        Assert.Null(slot.CustomName);
        Assert.False(slot.HasCustomName);
    }
}
