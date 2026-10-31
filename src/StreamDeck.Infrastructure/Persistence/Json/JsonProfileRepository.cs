using System.Text.Json;
using StreamDeck.Application.Profiles;
using StreamDeck.Domain.Entities;

namespace StreamDeck.Infrastructure.Persistence.Json;

public sealed class JsonProfileRepository : IProfileRepository
{
    private const int SchemaVersionWithoutSlots = 1;
    private const int SchemaVersionWithSlots = 2;

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

        if (dto.SchemaVersion != SchemaVersionWithoutSlots && dto.SchemaVersion != SchemaVersionWithSlots)
        {
            throw new InvalidDataException(
                $"El archivo de perfil '{filePath}' usa la versión de esquema {dto.SchemaVersion}, " +
                $"que no es compatible con las versiones soportadas ({SchemaVersionWithoutSlots} o {SchemaVersionWithSlots}).");
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

        if (dto.Pages.Any(p => p is null))
        {
            throw new InvalidDataException(
                $"El archivo de perfil '{filePath}' contiene una página nula dentro de 'pages'.");
        }

        try
        {
            if (dto.SchemaVersion == SchemaVersionWithoutSlots)
            {
                var pages = dto.Pages.Select(p => (p!.Id, p.Name));
                return Profile.Reconstitute(dto.Id, dto.Name, pages);
            }

            // SchemaVersionWithSlots (2)
            foreach (var page in dto.Pages)
            {
                if (page!.SlotPositions is null)
                {
                    throw new InvalidDataException(
                        $"El archivo de perfil '{filePath}' contiene una página ('{page.Id}') sin 'slotPositions', " +
                        $"requerido en la versión de esquema {SchemaVersionWithSlots}.");
                }
            }

            var pagesWithSlots = dto.Pages.Select(p => (p!.Id, p.Name, (IEnumerable<int>)p.SlotPositions!));
            return Profile.Reconstitute(dto.Id, dto.Name, pagesWithSlots);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException(
                $"El archivo de perfil '{filePath}' contiene datos de dominio inválidos: {ex.Message}", ex);
        }
    }
}