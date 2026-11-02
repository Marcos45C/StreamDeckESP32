using StreamDeck.Domain.Entities;
using System.Text.Json.Serialization;
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
        SchemaVersion = 4,
        Id = profile.Id,
        Name = profile.Name,
        Pages = profile.Pages.Select(p => new PageDto
        {
            Id = p.Id,
            Name = p.Name,
            SlotPositions = p.Slots.Select(s => s.Position).ToList(),
            Actions = p.Slots
                .Where(s => s.Action is not null)
                .Select(ToActionDto)
                .ToList(),
        }).ToList(),
    };
}

private static ActionDto ToActionDto(ButtonSlot slot)
{
    return slot.Action switch
    {
        LaunchApplicationAction launch => new ActionDto
        {
            Position = slot.Position,
            Type = "launchApplication",
            ApplicationPath = launch.ApplicationPath,
        },
        OpenUrlAction openUrl => new ActionDto
        {
            Position = slot.Position,
            Type = "openUrl",
            Url = openUrl.Url,
        },
        _ => throw new NotSupportedException(
            $"El tipo de acción '{slot.Action?.GetType().Name}' en la posición {slot.Position} " +
            "no está soportado por la persistencia."),
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
    public List<int>? SlotPositions { get; set; }
    /// <summary>
    /// Null en archivos de esquema versión 1 (donde no existía este campo).
    /// Obligatorio y validado al leer esquema versión 2.
    /// </summary>
    public List<ActionDto>? Actions { get; set; }
}
// Se agrega, nueva clase en el mismo archivo:
internal sealed class ActionDto
{
    public ActionDto()
    {
    }

    public int Position { get; set; }
    public string Type { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ApplicationPath { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; set; }
}