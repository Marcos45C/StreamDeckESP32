using System.Text.Json;
using StreamDeck.Application.Profiles;
using StreamDeck.Domain.Entities;

namespace StreamDeck.Infrastructure.Persistence.Json;

public sealed class JsonProfileRepository : IProfileRepository
{
    private const int SchemaVersionWithoutSlots = 1;
    private const int SchemaVersionWithSlots = 2;
    private const int SchemaVersionWithActions = 3;

    private const int SchemaVersionWithMultipleActionTypes = 4;
    private const int SchemaVersionWithButtonColors = 5;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _profilesDirectory;

    public JsonProfileRepository(string? profilesDirectory = null)
    {
        _profilesDirectory = profilesDirectory ?? GetDefaultProfilesDirectory();
    }

    public static string GetDefaultProfilesDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "StreamDeckESP32", "profiles");
    }

    public async Task<IReadOnlyList<Profile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_profilesDirectory))
        {
            return Array.Empty<Profile>();
        }

        var profiles = new List<Profile>();

        foreach (var filePath in Directory.EnumerateFiles(_profilesDirectory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            profiles.Add(await ReadProfileAsync(filePath, cancellationToken).ConfigureAwait(false));
        }

        return profiles;
    }

    public async Task<Profile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(id);

        if (!File.Exists(filePath))
        {
            return null;
        }

        return await ReadProfileAsync(filePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_profilesDirectory);

        var dto = ProfileFileDto.FromDomain(profile);
        var finalPath = GetFilePath(profile.Id);
        var tempPath = finalPath + ".tmp";

        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, dto, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(tempPath, finalPath, overwrite: true);
        }
        catch
        {
            TryDeleteTemporaryFile(tempPath);
            throw;
        }
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var filePath = GetFilePath(id);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    private string GetFilePath(Guid id) => Path.Combine(_profilesDirectory, $"{id}.json");

    private static void TryDeleteTemporaryFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch
        {
            // Limpieza best-effort: si falla el borrado del temporal, no debe
            // reemplazar ni ocultar la excepción original que causó el fallo del guardado.
        }
    }

    private static async Task<Profile> ReadProfileAsync(string filePath, CancellationToken cancellationToken)
    {
        ProfileFileDto? dto;

        try
        {
            await using var stream = File.OpenRead(filePath);
            dto = await JsonSerializer.DeserializeAsync<ProfileFileDto>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"El archivo de perfil '{filePath}' está corrupto o no es un JSON válido.", ex);
        }

        if (dto is null)
        {
            throw new InvalidDataException($"El archivo de perfil '{filePath}' está vacío o es inválido.");
        }

        if (dto.SchemaVersion != SchemaVersionWithoutSlots
            && dto.SchemaVersion != SchemaVersionWithSlots
            && dto.SchemaVersion != SchemaVersionWithActions
            && dto.SchemaVersion != SchemaVersionWithMultipleActionTypes
            && dto.SchemaVersion != SchemaVersionWithButtonColors)
        {
            throw new InvalidDataException(
                $"El archivo de perfil '{filePath}' usa la versión de esquema {dto.SchemaVersion}, " +
                "que no es compatible con las versiones soportadas " +
                $"({SchemaVersionWithoutSlots}, {SchemaVersionWithSlots}, {SchemaVersionWithActions}, " +
                $"{SchemaVersionWithMultipleActionTypes} o {SchemaVersionWithButtonColors}).");
        }

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

        if (!Guid.TryParse(fileNameWithoutExtension, out var expectedId))
        {
            throw new InvalidDataException(
                $"El archivo '{filePath}' no tiene un nombre de archivo válido: se esperaba un GUID, " +
                $"se encontró '{fileNameWithoutExtension}'.");
        }

        if (expectedId != dto.Id)
        {
            throw new InvalidDataException(
                $"El archivo '{filePath}' tiene un id de perfil ('{dto.Id}') distinto al de su nombre de archivo ('{expectedId}').");
        }

        if (dto.Pages is null)
        {
            throw new InvalidDataException(
                $"El archivo de perfil '{filePath}' no contiene información de páginas ('pages' es nulo o está ausente).");
        }

        if (dto.Pages.Any(page => page is null))
        {
            throw new InvalidDataException(
                $"El archivo de perfil '{filePath}' contiene una página nula dentro de 'pages'.");
        }

        try
        {
            Profile profile;

            if (dto.SchemaVersion == SchemaVersionWithoutSlots)
            {
                var pages = dto.Pages.Select(page => (page!.Id, page.Name));
                profile = Profile.Reconstitute(dto.Id, dto.Name, pages);
            }
            else
            {
                foreach (var page in dto.Pages)
                {
                    if (page!.SlotPositions is null)
                    {
                        throw new InvalidDataException(
                            $"El archivo de perfil '{filePath}' contiene una página ('{page.Id}') sin 'slotPositions', " +
                            $"requerido en la versión de esquema {dto.SchemaVersion}.");
                    }
                }

                var pagesWithSlots = dto.Pages.Select(page =>
                    (page!.Id, page.Name, (IEnumerable<int>)page.SlotPositions!));
                profile = Profile.Reconstitute(dto.Id, dto.Name, pagesWithSlots);
            }

            if (dto.SchemaVersion >= SchemaVersionWithActions)
            {
                ApplyActions(profile, dto.Pages, dto.SchemaVersion, filePath);
            }

            if (dto.SchemaVersion >= SchemaVersionWithButtonColors)
            {
                ApplySlotColors(profile, dto.Pages, filePath);
            }

            return profile;
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException(
                $"El archivo de perfil '{filePath}' contiene datos de dominio inválidos: {ex.Message}", ex);
        }
    }

    private static void ApplyActions(Profile profile, List<PageDto> pageDtos, int schemaVersion, string filePath)
    {
        foreach (var pageDto in pageDtos)
        {
            if (pageDto!.Actions is null)
            {
                throw new InvalidDataException(
                    $"El archivo de perfil '{filePath}' contiene una página ('{pageDto.Id}') sin 'actions', " +
                    $"requerido en la versión de esquema {schemaVersion}.");
            }

            if (pageDto.Actions.Any(a => a is null))
            {
                throw new InvalidDataException(
                    $"El archivo de perfil '{filePath}' contiene una acción nula en la página '{pageDto.Id}'.");
            }

            var page = profile.Pages.First(p => p.Id == pageDto.Id);
            var seenPositions = new HashSet<int>();

            foreach (var actionDto in pageDto.Actions)
            {
                if (actionDto!.Position < 1 || actionDto.Position > Page.SlotCount)
                {
                    throw new InvalidDataException(
                        $"El archivo de perfil '{filePath}' contiene una acción con posición inválida " +
                        $"({actionDto.Position}) en la página '{pageDto.Id}'.");
                }

                if (!seenPositions.Add(actionDto.Position))
                {
                    throw new InvalidDataException(
                        $"El archivo de perfil '{filePath}' contiene más de una acción para la posición " +
                        $"{actionDto.Position} en la página '{pageDto.Id}'.");
                }

                var slot = page.Slots.First(s => s.Position == actionDto.Position);
                var action = CreateActionFromDto(actionDto, schemaVersion, pageDto.Id, filePath);
                slot.AssignAction(action);
            }
        }
    }
    private static void ApplySlotColors(Profile profile, List<PageDto> pageDtos, string filePath)
    {
        foreach (var pageDto in pageDtos)
        {
            if (pageDto!.SlotColors is null)
            {
                throw new InvalidDataException(
                    $"El archivo de perfil '{filePath}' contiene una página ('{pageDto.Id}') sin 'slotColors', " +
                    "requerido en la versión de esquema 5.");
            }

            if (pageDto.SlotColors.Any(color => color is null))
            {
                throw new InvalidDataException(
                    $"El archivo de perfil '{filePath}' contiene un color nulo en la página '{pageDto.Id}'.");
            }

            var page = profile.Pages.First(p => p.Id == pageDto.Id);
            var seenPositions = new HashSet<int>();

            foreach (var colorDto in pageDto.SlotColors)
            {
                if (colorDto!.Position < 1 || colorDto.Position > Page.SlotCount)
                {
                    throw new InvalidDataException(
                        $"El archivo de perfil '{filePath}' contiene un color con posición inválida " +
                        $"({colorDto.Position}) en la página '{pageDto.Id}'.");
                }

                if (!seenPositions.Add(colorDto.Position))
                {
                    throw new InvalidDataException(
                        $"El archivo de perfil '{filePath}' contiene más de un color para la posición " +
                        $"{colorDto.Position} en la página '{pageDto.Id}'.");
                }

                if (colorDto.ColorHex is null)
                {
                    throw new InvalidDataException(
                        $"El archivo de perfil '{filePath}' contiene un color sin 'colorHex' " +
                        $"en la posición {colorDto.Position} de la página '{pageDto.Id}'.");
                }

                try
                {
                    page.Slots.First(slot => slot.Position == colorDto.Position).SetColor(colorDto.ColorHex);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidDataException(
                        $"El archivo de perfil '{filePath}' contiene un color inválido en la posición " +
                        $"{colorDto.Position} de la página '{pageDto.Id}': {ex.Message}", ex);
                }
            }
        }
    }

    private static ButtonAction CreateActionFromDto(
        ActionDto actionDto,
        int schemaVersion,
        Guid pageId,
        string filePath)
    {
        switch (actionDto.Type)
        {
            case "launchApplication":
                if (actionDto.ApplicationPath is null)
                {
                    throw new InvalidDataException(
                        $"El archivo de perfil '{filePath}' contiene una acción 'launchApplication' sin 'applicationPath' " +
                        $"en la posición {actionDto.Position} de la página '{pageId}'.");
                }

                if (actionDto.Url is not null)
                {
                    throw new InvalidDataException(
                        $"El archivo de perfil '{filePath}' contiene una acción 'launchApplication' " +
                        $"con una propiedad 'url' incompatible en la posición {actionDto.Position} " +
                        $"de la página '{pageId}'.");
                }

                try
                {
                    return new LaunchApplicationAction(actionDto.ApplicationPath);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidDataException(
                        $"El archivo de perfil '{filePath}' contiene una acción inválida en la posición " +
                        $"{actionDto.Position} de la página '{pageId}': {ex.Message}", ex);
                }

            case "openUrl" when schemaVersion >= SchemaVersionWithMultipleActionTypes:
                if (actionDto.Url is null)
                {
                    throw new InvalidDataException(
                        $"El archivo de perfil '{filePath}' contiene una acción 'openUrl' sin 'url' " +
                        $"en la posición {actionDto.Position} de la página '{pageId}'.");
                }

                if (actionDto.ApplicationPath is not null)
                {
                    throw new InvalidDataException(
                        $"El archivo de perfil '{filePath}' contiene una acción 'openUrl' " +
                        $"con una propiedad 'applicationPath' incompatible en la posición {actionDto.Position} " +
                        $"de la página '{pageId}'.");
                }

                try
                {
                    return new OpenUrlAction(actionDto.Url);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidDataException(
                        $"El archivo de perfil '{filePath}' contiene una acción inválida en la posición " +
                        $"{actionDto.Position} de la página '{pageId}': {ex.Message}", ex);
                }

            default:
                throw new InvalidDataException(
                    $"El archivo de perfil '{filePath}' contiene un tipo de acción desconocido " +
                    $"('{actionDto.Type}') en la posición {actionDto.Position} de la página '{pageId}'.");
        }
    }

}
