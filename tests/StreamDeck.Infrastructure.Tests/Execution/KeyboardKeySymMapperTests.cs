using StreamDeck.Domain.Entities;
using StreamDeck.Infrastructure.Execution;

namespace StreamDeck.Infrastructure.Tests.Execution;

public sealed class KeyboardKeySymMapperTests
{
    [Theory]
    [InlineData(KeyboardKey.A, 0x61)]
    [InlineData(KeyboardKey.Z, 0x7A)]
    [InlineData(KeyboardKey.Digit0, 0x30)]
    [InlineData(KeyboardKey.Digit9, 0x39)]
    [InlineData(KeyboardKey.F1, 0xFFBE)]
    [InlineData(KeyboardKey.F12, 0xFFC9)]
    [InlineData(KeyboardKey.Enter, 0xFF0D)]
    [InlineData(KeyboardKey.Escape, 0xFF1B)]
    [InlineData(KeyboardKey.Tab, 0xFF09)]
    [InlineData(KeyboardKey.Space, 0x0020)]
    [InlineData(KeyboardKey.Backspace, 0xFF08)]
    [InlineData(KeyboardKey.Delete, 0xFFFF)]
    [InlineData(KeyboardKey.Insert, 0xFF63)]
    [InlineData(KeyboardKey.Home, 0xFF50)]
    [InlineData(KeyboardKey.End, 0xFF57)]
    [InlineData(KeyboardKey.PageUp, 0xFF55)]
    [InlineData(KeyboardKey.PageDown, 0xFF56)]
    [InlineData(KeyboardKey.ArrowUp, 0xFF52)]
    [InlineData(KeyboardKey.ArrowDown, 0xFF54)]
    [InlineData(KeyboardKey.ArrowLeft, 0xFF51)]
    [InlineData(KeyboardKey.ArrowRight, 0xFF53)]
    public void GetKeySym_MapsSupportedKeys(KeyboardKey key, int expected)
    {
        Assert.Equal(expected, KeyboardKeySymMapper.GetKeySym(key));
    }

    [Theory]
    [InlineData(KeyboardModifier.Ctrl, 0xFFE3)]
    [InlineData(KeyboardModifier.Alt, 0xFFE9)]
    [InlineData(KeyboardModifier.Shift, 0xFFE1)]
    [InlineData(KeyboardModifier.Super, 0xFFEB)]
    public void GetModifierKeySym_MapsSupportedModifiers(KeyboardModifier modifier, int expected)
    {
        Assert.Equal(expected, KeyboardKeySymMapper.GetModifierKeySym(modifier));
    }

    [Fact]
    public void GetKeySym_WithUnknownValue_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => KeyboardKeySymMapper.GetKeySym((KeyboardKey)999));
    }

    [Fact]
    public void GetModifierKeySym_WithUnknownValue_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => KeyboardKeySymMapper.GetModifierKeySym((KeyboardModifier)999));
    }
}
