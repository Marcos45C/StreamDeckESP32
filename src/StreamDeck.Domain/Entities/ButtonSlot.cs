namespace StreamDeck.Domain.Entities;

public sealed class ButtonSlot
{
    public int Position { get; }

    public int Row => (Position - 1) / 3 + 1;

    public int Column => (Position - 1) % 3 + 1;

    public ButtonAction? Action { get; private set; }

    internal ButtonSlot(int position)
    {
        Position = position;
    }

    public void AssignAction(ButtonAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Action = action;
    }

    public void ClearAction()
    {
        Action = null;
    }
}