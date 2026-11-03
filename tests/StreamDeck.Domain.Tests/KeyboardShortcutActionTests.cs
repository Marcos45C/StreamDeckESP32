using StreamDeck.Domain.Entities;

namespace StreamDeck.Domain.Tests;

public sealed class KeyboardShortcutActionTests
{
    [Fact]
    public void Create_WithSingleKeyAndNoModifiers_Succeeds()
    {
        var action = new KeyboardShortcutAction(KeyboardKey.F5);

        Assert.Equal(KeyboardKey.F5, action.Key);
        Assert.Empty(action.Modifiers);
    }

    [Fact]
    public void Create_WithModifiers_PreservesThemInStableOrder()
    {
        var action = new KeyboardShortcutAction(
            KeyboardKey.S,
            new[] { KeyboardModifier.Shift, KeyboardModifier.Ctrl });

        Assert.Equal(KeyboardKey.S, action.Key);
        Assert.Equal(
            new[] { KeyboardModifier.Ctrl, KeyboardModifier.Shift },
            action.Modifiers);
    }

    [Fact]
    public void Create_WithAllSupportedModifiers_Succeeds()
    {
        var action = new KeyboardShortcutAction(
            KeyboardKey.A,
            new[]
            {
                KeyboardModifier.Super,
                KeyboardModifier.Alt,
                KeyboardModifier.Shift,
                KeyboardModifier.Ctrl,
            });

        Assert.Equal(4, action.Modifiers.Count);
    }

    [Fact]
    public void Create_WithUndefinedKey_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new KeyboardShortcutAction((KeyboardKey)999));
    }

    [Fact]
    public void Create_WithUndefinedModifier_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => new KeyboardShortcutAction(
                KeyboardKey.C,
                new[] { (KeyboardModifier)999 }));
    }

    [Fact]
    public void Create_WithDuplicateModifiers_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => new KeyboardShortcutAction(
                KeyboardKey.C,
                new[] { KeyboardModifier.Ctrl, KeyboardModifier.Ctrl }));
    }

    [Fact]
    public void Create_CopiesModifierCollection()
    {
        var modifiers = new List<KeyboardModifier> { KeyboardModifier.Ctrl };
        var action = new KeyboardShortcutAction(KeyboardKey.C, modifiers);

        modifiers.Clear();

        Assert.Equal(new[] { KeyboardModifier.Ctrl }, action.Modifiers);
    }
}
