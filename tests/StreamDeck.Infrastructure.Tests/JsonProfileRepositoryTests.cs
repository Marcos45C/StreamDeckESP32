using StreamDeck.Domain.Entities;
using StreamDeck.Infrastructure.Persistence.Json;
using System.Text.Json;

namespace StreamDeck.Infrastructure.Tests;

public sealed class JsonProfileRepositoryTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly JsonProfileRepository _repository;

    public JsonProfileRepositoryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "StreamDeckESP32Tests", Guid.NewGuid().ToString());
        _repository = new JsonProfileRepository(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    // ---------- Guardado / recuperación básica ----------

    [Fact]
    public async Task SaveAsync_CreatesFileNamedAfterProfileId()
    {
        var profile = Profile.Create("Mi perfil");

        await _repository.SaveAsync(profile);

        Assert.True(File.Exists(Path.Combine(_tempDirectory, $"{profile.Id}.json")));
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_PreservesIdNameAndPageCount()
    {
        var profile = Profile.Create("Mi perfil");
        profile.AddPage("Página 2");

        await _repository.SaveAsync(profile);
        var loaded = await _repository.GetByIdAsync(profile.Id);

        Assert.NotNull(loaded);
        Assert.Equal(profile.Id, loaded!.Id);
        Assert.Equal(profile.Name, loaded.Name);
        Assert.Equal(profile.Pages.Count, loaded.Pages.Count);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_PreservesPageOrder()
    {
        var profile = Profile.Create("Mi perfil");
        var page2 = profile.AddPage("Página 2");
        var page3 = profile.AddPage("Página 3");
        profile.ReorderPage(page3.Id, 0);

        await _repository.SaveAsync(profile);
        var loaded = await _repository.GetByIdAsync(profile.Id);

        Assert.Equal(page3.Id, loaded!.Pages[0].Id);
        Assert.Equal(page2.Id, loaded.Pages[2].Id);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_PreservesPageNames()
    {
        var profile = Profile.Create("Mi perfil");
        profile.RenamePage(profile.Pages[0].Id, "Portada");

        await _repository.SaveAsync(profile);
        var loaded = await _repository.GetByIdAsync(profile.Id);

        Assert.Equal("Portada", loaded!.Pages[0].Name);
    }

    [Fact]
    public async Task SaveAsync_WritesExpectedJsonSchemaUsingDtos()
    {
        var profile = Profile.Create("Mi perfil");
        var page = profile.Pages[0];

        await _repository.SaveAsync(profile);

        var json = await File.ReadAllTextAsync(
            Path.Combine(_tempDirectory, $"{profile.Id}.json"));

        using var document = JsonDocument.Parse(json);

        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(profile.Id, root.GetProperty("id").GetGuid());
        Assert.Equal("Mi perfil", root.GetProperty("name").GetString());

        var pages = root.GetProperty("pages");

        Assert.Equal(1, pages.GetArrayLength());

        var savedPage = pages[0];

        Assert.Equal(page.Id, savedPage.GetProperty("id").GetGuid());
        Assert.Equal("Página 1", savedPage.GetProperty("name").GetString());
    }

    [Fact]
    public async Task SaveAsync_CalledTwiceForSameProfile_UpdatesExistingFile()
    {
        var profile = Profile.Create("Nombre original");
        await _repository.SaveAsync(profile);

        profile.Rename("Nombre actualizado");
        await _repository.SaveAsync(profile);

        var loaded = await _repository.GetByIdAsync(profile.Id);

        Assert.Equal("Nombre actualizado", loaded!.Name);
        Assert.Single(Directory.GetFiles(_tempDirectory, $"{profile.Id}*.json"));
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        Assert.Null(await _repository.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllSavedProfiles()
    {
        var profile1 = Profile.Create("Perfil 1");
        var profile2 = Profile.Create("Perfil 2");
        await _repository.SaveAsync(profile1);
        await _repository.SaveAsync(profile2);

        var all = await _repository.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, p => p.Id == profile1.Id);
        Assert.Contains(all, p => p.Id == profile2.Id);
    }

    [Fact]
    public async Task GetAllAsync_WithEmptyDirectory_ReturnsEmptyCollection()
    {
        Directory.CreateDirectory(_tempDirectory);

        Assert.Empty(await _repository.GetAllAsync());
    }

    [Fact]
    public async Task GetAllAsync_WithNonExistentDirectory_ReturnsEmptyCollection()
    {
        Assert.Empty(await _repository.GetAllAsync());
    }

    [Fact]
    public async Task DeleteAsync_RemovesProfileFile()
    {
        var profile = Profile.Create("Mi perfil");
        await _repository.SaveAsync(profile);

        await _repository.DeleteAsync(profile.Id);

        Assert.Null(await _repository.GetByIdAsync(profile.Id));
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownId_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() => _repository.DeleteAsync(Guid.NewGuid()));

        Assert.Null(exception);
    }

    // ---------- Archivos estructuralmente inválidos ----------
    [Fact]
    public async Task GetByIdAsync_WithEmptyPageIdInContent_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
        "schemaVersion": 1,
        "id": "{{id}}",
        "name": "Perfil",
        "pages": [ { "id": "00000000-0000-0000-0000-000000000000", "name": "Página 1" } ]
        }
        """);

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetByIdAsync(id));
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidPageNameInContent_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
        "schemaVersion": 1,
        "id": "{{id}}",
        "name": "Perfil",
        "pages": [ { "id": "{{Guid.NewGuid()}}", "name": "   " } ]
        }
        """);

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetByIdAsync(id));
    }

    [Fact]
    public async Task GetAllAsync_WithNonGuidJsonFileName_ThrowsInvalidDataException()
    {
        Directory.CreateDirectory(_tempDirectory);
        var validId = Guid.NewGuid();
        var json = $$"""
        {
        "schemaVersion": 1,
        "id": "{{validId}}",
        "name": "Perfil",
        "pages": [ { "id": "{{Guid.NewGuid()}}", "name": "Página 1" } ]
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, "perfil-invalido.json"), json);

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetAllAsync());
    }

    [Fact]
    public async Task GetByIdAsync_WithCorruptJsonFile_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        Directory.CreateDirectory(_tempDirectory);
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, $"{id}.json"), "{ esto no es json válido");

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetByIdAsync(id));
    }

    [Fact]
    public async Task GetByIdAsync_WithUnsupportedSchemaVersion_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
          "schemaVersion": 999,
          "id": "{{id}}",
          "name": "Perfil",
          "pages": [ { "id": "{{Guid.NewGuid()}}", "name": "Página 1" } ]
        }
        """);

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetByIdAsync(id));
    }

    [Fact]
    public async Task GetByIdAsync_WithNullPages_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
          "schemaVersion": 1,
          "id": "{{id}}",
          "name": "Perfil",
          "pages": null
        }
        """);

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetByIdAsync(id));
    }

    [Fact]
    public async Task GetByIdAsync_WithNullPageElement_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
          "schemaVersion": 1,
          "id": "{{id}}",
          "name": "Perfil",
          "pages": [ { "id": "{{Guid.NewGuid()}}", "name": "Página 1" }, null ]
        }
        """);

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetByIdAsync(id));
    }

    [Fact]
    public async Task GetByIdAsync_WithIdMismatchedToFileName_ThrowsInvalidDataException()
    {
        var fileNameId = Guid.NewGuid();
        var differentContentId = Guid.NewGuid();
        await WriteRawJsonAsync(fileNameId, $$"""
        {
          "schemaVersion": 1,
          "id": "{{differentContentId}}",
          "name": "Perfil",
          "pages": [ { "id": "{{Guid.NewGuid()}}", "name": "Página 1" } ]
        }
        """);

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetByIdAsync(fileNameId));
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyProfileNameInContent_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
          "schemaVersion": 1,
          "id": "{{id}}",
          "name": "   ",
          "pages": [ { "id": "{{Guid.NewGuid()}}", "name": "Página 1" } ]
        }
        """);

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetByIdAsync(id));
    }

    [Fact]
    public async Task GetByIdAsync_WithDuplicatePageIdsInContent_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        var duplicatedPageId = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
          "schemaVersion": 1,
          "id": "{{id}}",
          "name": "Perfil",
          "pages": [
            { "id": "{{duplicatedPageId}}", "name": "Página 1" },
            { "id": "{{duplicatedPageId}}", "name": "Página 2" }
          ]
        }
        """);

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetByIdAsync(id));
    }

    [Fact]
    public async Task GetByIdAsync_WithNoPagesInContent_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
          "schemaVersion": 1,
          "id": "{{id}}",
          "name": "Perfil",
          "pages": []
        }
        """);

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetByIdAsync(id));
    }

    private async Task WriteRawJsonAsync(Guid id, string json)
    {
        Directory.CreateDirectory(_tempDirectory);
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, $"{id}.json"), json);
    }
    

    // ---------- Escritura temporal ----------

    [Fact]
    public async Task SaveAsync_DoesNotLeaveTemporaryFileBehindOnSuccess()
    {
        var profile = Profile.Create("Mi perfil");

        await _repository.SaveAsync(profile);

        Assert.Empty(Directory.GetFiles(_tempDirectory, "*.tmp"));
    }

    [Fact]
    public async Task SaveAsync_WhenCancelled_DoesNotLeaveTemporaryFileBehind()
    {
        var profile = Profile.Create("Mi perfil");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _repository.SaveAsync(profile, cts.Token));

        var tempFiles = Directory.Exists(_tempDirectory)
            ? Directory.GetFiles(_tempDirectory, "*.tmp")
            : Array.Empty<string>();
        Assert.Empty(tempFiles);
    }
}