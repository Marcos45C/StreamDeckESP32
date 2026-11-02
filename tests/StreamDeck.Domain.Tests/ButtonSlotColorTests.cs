using StreamDeck.Domain.Entities;

namespace StreamDeck.Domain.Tests;

public class ButtonSlotColorTests
{
    [Fact]
    public void SetColor_WithValidHex_NormalizesAndStoresColor()
    {
        var slot = Profile.Create("Perfil").Pages[0].Slots[0];

        slot.SetColor("  #a1b2c3  ");

        Assert.Equal("#A1B2C3", slot.ColorHex);
    }

    [Theory]
    [InlineData("")]
    [InlineData("blue")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#GG0000")]
    public void SetColor_WithInvalidHex_ThrowsArgumentException(string color)
    {
        var slot = Profile.Create("Perfil").Pages[0].Slots[0];

        Assert.Throws<ArgumentException>(() => slot.SetColor(color));
    }

    [Fact]
    public void ColorAndAction_AreIndependent()
    {
        var slot = Profile.Create("Perfil").Pages[0].Slots[0];
        slot.SetColor("#2F5F91");
        slot.AssignAction(new OpenUrlAction("https://example.com"));

        slot.ClearAction();

        Assert.Null(slot.Action);
        Assert.Equal("#2F5F91", slot.ColorHex);
    }

    [Fact]
    public void ClearColor_RemovesPreviouslyAssignedColor()
    {
        var slot = Profile.Create("Perfil").Pages[0].Slots[0];
        slot.SetColor("#2F5F91");

        slot.ClearColor();

        Assert.Null(slot.ColorHex);
    }
}
