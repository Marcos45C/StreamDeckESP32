using StreamDeck.Domain.Entities;
using StreamDeck.Infrastructure.Execution;

namespace StreamDeck.Infrastructure.Tests.Execution;

public sealed class KeyboardShortcutSenderTests
{
    [Fact]
    public async Task SendAsync_WithCtrlShiftC_PressesModifiersThenKeyAndReleasesInReverseOrder()
    {
        var events = new List<(int KeySym, uint State)>();
        var action = new KeyboardShortcutAction(
            KeyboardKey.C,
            [KeyboardModifier.Ctrl, KeyboardModifier.Shift]);

        await KeyboardShortcutSender.SendAsync(
            action,
            (keySym, state) =>
            {
                events.Add((keySym, state));
                return Task.CompletedTask;
            });

        Assert.Equal(
            new[]
            {
                (0xFFE3, KeyboardShortcutSender.Pressed), // Ctrl down
                (0xFFE1, KeyboardShortcutSender.Pressed), // Shift down
                (0x0063, KeyboardShortcutSender.Pressed), // C down
                (0x0063, KeyboardShortcutSender.Released), // C up
                (0xFFE1, KeyboardShortcutSender.Released), // Shift up
                (0xFFE3, KeyboardShortcutSender.Released), // Ctrl up
            },
            events);
    }

    [Fact]
    public async Task SendAsync_WhenMainKeyPressFails_ReleasesMainKeyAndPreviouslyPressedModifiers()
    {
        var events = new List<(int KeySym, uint State)>();
        var action = new KeyboardShortcutAction(KeyboardKey.C, [KeyboardModifier.Ctrl]);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await KeyboardShortcutSender.SendAsync(
                action,
                (keySym, state) =>
                {
                    events.Add((keySym, state));
                    if (keySym == 0x0063 && state == KeyboardShortcutSender.Pressed)
                    {
                        throw new InvalidOperationException("Simulated D-Bus failure.");
                    }

                    return Task.CompletedTask;
                }));

        Assert.Equal(
            new[]
            {
                (0xFFE3, KeyboardShortcutSender.Pressed),
                (0x0063, KeyboardShortcutSender.Pressed),
                (0x0063, KeyboardShortcutSender.Released),
                (0xFFE3, KeyboardShortcutSender.Released),
            },
            events);
    }

    [Fact]
    public async Task SendAsync_WhenCancelledAfterModifierPress_ReleasesModifier()
    {
        using var cancellation = new CancellationTokenSource();
        var events = new List<(int KeySym, uint State)>();
        var action = new KeyboardShortcutAction(KeyboardKey.C, [KeyboardModifier.Ctrl]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await KeyboardShortcutSender.SendAsync(
                action,
                (keySym, state) =>
                {
                    events.Add((keySym, state));
                    if (keySym == 0xFFE3 && state == KeyboardShortcutSender.Pressed)
                    {
                        cancellation.Cancel();
                    }

                    return Task.CompletedTask;
                },
                cancellation.Token));

        Assert.Equal(
            new[]
            {
                (0xFFE3, KeyboardShortcutSender.Pressed),
                (0xFFE3, KeyboardShortcutSender.Released),
            },
            events);
    }

    [Fact]
    public async Task SendAsync_WhenReleaseFails_ReportsAggregateFailureAfterAttemptingAllReleases()
    {
        var events = new List<(int KeySym, uint State)>();
        var action = new KeyboardShortcutAction(
            KeyboardKey.C,
            [KeyboardModifier.Ctrl, KeyboardModifier.Shift]);

        await Assert.ThrowsAsync<AggregateException>(async () =>
            await KeyboardShortcutSender.SendAsync(
                action,
                (keySym, state) =>
                {
                    events.Add((keySym, state));
                    if (state == KeyboardShortcutSender.Released && keySym == 0xFFE1)
                    {
                        throw new InvalidOperationException("Simulated release failure.");
                    }

                    return Task.CompletedTask;
                }));

        Assert.Equal(6, events.Count);
        Assert.Equal((0xFFE3, KeyboardShortcutSender.Released), events[^1]);
    }
}
