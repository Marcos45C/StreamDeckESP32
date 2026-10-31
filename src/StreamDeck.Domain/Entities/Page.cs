namespace StreamDeck.Domain.Entities;

public sealed class Page
{
    public const int SlotCount = 9;

    private readonly List<ButtonSlot> _slots;

    public Guid Id { get; }
    public string Name { get; private set; }
    public IReadOnlyList<ButtonSlot> Slots => _slots.AsReadOnly();

    internal Page(Guid id, string name)
        : this(id, name, CreateDefaultSlots())
    {
    }

    private Page(Guid id, string name, List<ButtonSlot> slots)
    {
        Id = id;
        Name = name;
        _slots = slots;
    }

    internal void Rename(string name)
    {
        Name = name;
    }

    public static Page Reconstitute(Guid id, string name, IEnumerable<int> slotPositions)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("El id de la página no puede estar vacío.", nameof(id));
        }

        var validatedName = NormalizeName(name);
        var slots = BuildValidatedSlots(slotPositions);
        return new Page(id, validatedName, slots);
    }

    private static List<ButtonSlot> CreateDefaultSlots()
    {
        return BuildValidatedSlots(Enumerable.Range(1, SlotCount));
    }

    private static List<ButtonSlot> BuildValidatedSlots(IEnumerable<int> slotPositions)
    {
        ArgumentNullException.ThrowIfNull(slotPositions);

        var positions = slotPositions.ToList();

        if (positions.Count != SlotCount)
        {
            throw new ArgumentException(
                $"Una página debe tener exactamente {SlotCount} espacios de botones (se recibieron {positions.Count}).",
                nameof(slotPositions));
        }

        var seenPositions = new HashSet<int>();

        foreach (var position in positions)
        {
            if (position < 1 || position > SlotCount)
            {
                throw new ArgumentException(
                    $"La posición {position} está fuera del rango válido (1 a {SlotCount}).",
                    nameof(slotPositions));
            }

            if (!seenPositions.Add(position))
            {
                throw new ArgumentException(
                    $"La posición {position} está duplicada.",
                    nameof(slotPositions));
            }
        }

        return Enumerable.Range(1, SlotCount)
            .Select(position => new ButtonSlot(position))
            .ToList();
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre no puede estar vacío.", nameof(name));
        }

        return name.Trim();
    }
}