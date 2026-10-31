using StreamDeck.Domain.Entities;

namespace StreamDeck.Infrastructure.Persistence.Json;

internal sealed class ProfileFileDto
{
    public ProfileFileDto()
    {
    }

    public int SchemaVersion { get; set; }
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<PageDto> Pages { get; set; } = new();

    public static ProfileFileDto FromDomain(Profile profile)
    {
        return new ProfileFileDto
        {
            SchemaVersion = 2,
            Id = profile.Id,
            Name = profile.Name,
            Pages = profile.Pages.Select(p => new PageDto
            {
                Id = p.Id,
                Name = p.Name,
                SlotPositions = p.Slots.Select(s => s.Position).ToList(),
            }).ToList(),
        };
    }
}

internal sealed class PageDto
{
    public PageDto()
    {
    }

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Null en archivos de esquema versión 1 (donde no existía este campo).
    /// Obligatorio y validado al leer esquema versión 2.
    /// </summary>
    public List<int>? SlotPositions { get; set; }
}