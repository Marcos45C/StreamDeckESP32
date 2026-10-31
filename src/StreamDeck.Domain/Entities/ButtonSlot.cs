namespace StreamDeck.Domain.Entities;

public sealed class ButtonSlot
{
    public int Position { get; }

    public int Row => (Position - 1) / 3 + 1;

    public int Column => (Position - 1) % 3 + 1;

    internal ButtonSlot(int position)
    {
        Position = position;
    }
}