using System.Text.Json.Serialization;
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
            SchemaVersion = 6,
            Id = profile.Id,
            Name = profile.Name,
            Pages = profile.Pages.Select(page => new PageDto
            {
                Id = page.Id,
                Name = page.Name,
                SlotPositions = page.Slots.Select(slot => slot.Position).ToList(),
                Actions = page.Slots
                    .Where(slot => slot.Action is not null)
                    .Select(ToActionDto)
                    .ToList(),
                SlotColors = page.Slots
                    .Where(slot => slot.ColorHex is not null)
                    .Select(slot => new SlotColorDto
                    {
                        Position = slot.Position,
                        ColorHex = slot.ColorHex,
                    })
                    .ToList(),
                SlotNames = page.Slots
                    .Where(slot => slot.CustomName is not null)
                    .Select(slot => new SlotNameDto
                    {
                        Position = slot.Position,
                        Name = slot.CustomName,
                    })
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
    public List<ActionDto>? Actions { get; set; }
    public List<SlotColorDto>? SlotColors { get; set; }
    public List<SlotNameDto>? SlotNames { get; set; }
}

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

internal sealed class SlotColorDto
{
    public SlotColorDto()
    {
    }

    public int Position { get; set; }
    public string? ColorHex { get; set; }
}

internal sealed class SlotNameDto
{
    public SlotNameDto()
    {
    }

    public int Position { get; set; }
    public string? Name { get; set; }
}
