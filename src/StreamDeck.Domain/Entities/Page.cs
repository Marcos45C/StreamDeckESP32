namespace StreamDeck.Domain.Entities;

public sealed class Page
{
    public Guid Id { get; }
    public string Name { get; private set; }

    internal Page(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    internal void Rename(string name)
    {
        Name = name;
    }
}