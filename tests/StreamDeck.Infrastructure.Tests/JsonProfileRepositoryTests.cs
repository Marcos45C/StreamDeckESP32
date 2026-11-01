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
        var json = await File.ReadAllTextAsync(Path.Combine(_tempDirectory, $"{profile.Id}.json"));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(3, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(profile.Id, root.GetProperty("id").GetGuid());
        Assert.Equal("Mi perfil", root.GetProperty("name").GetString());
        
        Assert.Equal(0, actionsArray.GetArrayLength());
        var pagesArray = root.GetProperty("pages");
        Assert.Equal(1, pagesArray.GetArrayLength());

        var pageElement = pagesArray[0];
        var actionsArray = pageElement.GetProperty("actions");
        Assert.Equal(page.Id, pageElement.GetProperty("id").GetGuid());
        Assert.Equal("Página 1", pageElement.GetProperty("name").GetString());

        var slotPositions = pageElement.GetProperty("slotPositions")
            .EnumerateArray()
            .Select(e => e.GetInt32())
            .ToList();

        Assert.Equal(Enumerable.Range(1, 9), slotPositions);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_EachPageHasExactlyPositionsOneToNine()
    {
        var profile = Profile.Create("Mi perfil");

        await _repository.SaveAsync(profile);
        var loaded = await _repository.GetByIdAsync(profile.Id);

        var positions = loaded!.Pages[0].Slots.Select(s => s.Position).ToList();
        Assert.Equal(Enumerable.Range(1, 9), positions);
    }

    [Fact]
    public async Task SaveAsync_WithMultiplePages_EachPageSavesNineSlots()
    {
        var profile = Profile.Create("Mi perfil");
        profile.AddPage("Página 2");
        profile.AddPage("Página 3");

        await _repository.SaveAsync(profile);
        var loaded = await _repository.GetByIdAsync(profile.Id);

        Assert.Equal(3, loaded!.Pages.Count);
        Assert.All(loaded.Pages, p => Assert.Equal(9, p.Slots.Count));
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

    // ---------- Compatibilidad con esquema versión 1 ----------

    [Fact]
    public async Task GetByIdAsync_WithSchemaVersion1File_ReadsSuccessfully()
    {
        var id = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
          "schemaVersion": 1,
          "id": "{{id}}",
          "name": "Perfil antiguo",
          "pages": [ { "id": "{{pageId}}", "name": "Página 1" } ]
        }
        """);

        var loaded = await _repository.GetByIdAsync(id);

        Assert.NotNull(loaded);
        Assert.Equal("Perfil antiguo", loaded!.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithSchemaVersion1File_PagesGetNineDefaultSlots()
    {
        var id = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
          "schemaVersion": 1,
          "id": "{{id}}",
          "name": "Perfil antiguo",
          "pages": [ { "id": "{{pageId}}", "name": "Página 1" } ]
        }
        """);

        var loaded = await _repository.GetByIdAsync(id);

        var positions = loaded!.Pages[0].Slots.Select(s => s.Position).ToList();
        Assert.Equal(Enumerable.Range(1, 9), positions);
    }

    [Fact]
    public async Task GetByIdAsync_WithSchemaVersion1File_PreservesIdsNamesAndOrder()
    {
        var id = Guid.NewGuid();
        var page1Id = Guid.NewGuid();
        var page2Id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
          "schemaVersion": 1,
          "id": "{{id}}",
          "name": "Perfil antiguo",
          "pages": [
            { "id": "{{page1Id}}", "name": "Portada" },
            { "id": "{{page2Id}}", "name": "Macros" }
          ]
        }
        """);

        var loaded = await _repository.GetByIdAsync(id);

        Assert.Equal(2, loaded!.Pages.Count);
        Assert.Equal(page1Id, loaded.Pages[0].Id);
        Assert.Equal("Portada", loaded.Pages[0].Name);
        Assert.Equal(page2Id, loaded.Pages[1].Id);
        Assert.Equal("Macros", loaded.Pages[1].Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithSchemaVersion1File_DoesNotModifyOriginalFile()
    {
        var id = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var originalJson = $$"""
        {
          "schemaVersion": 1,
          "id": "{{id}}",
          "name": "Perfil antiguo",
          "pages": [ { "id": "{{pageId}}", "name": "Página 1" } ]
        }
        """;
        await WriteRawJsonAsync(id, originalJson);
        var filePath = Path.Combine(_tempDirectory, $"{id}.json");
        var contentBefore = await File.ReadAllTextAsync(filePath);

        await _repository.GetByIdAsync(id);

        var contentAfter = await File.ReadAllTextAsync(filePath);
        Assert.Equal(contentBefore, contentAfter);
        Assert.DoesNotContain("slotPositions", contentAfter);
    }

    [Fact]
    public async Task SaveAsync_AfterReadingSchemaVersion1Profile_WritesSchemaVersion3()
    {
        var id = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
          "schemaVersion": 1,
          "id": "{{id}}",
          "name": "Perfil antiguo",
          "pages": [ { "id": "{{pageId}}", "name": "Página 1" } ]
        }
        """);

        var loaded = await _repository.GetByIdAsync(id);
        await _repository.SaveAsync(loaded!);

        var json = await File.ReadAllTextAsync(Path.Combine(_tempDirectory, $"{id}.json"));

        Assert.Contains("\"schemaVersion\": 3", json);
        Assert.Contains("\"slotPositions\"", json);
        Assert.Contains("\"actions\"", json);
    }

    // ---------- Lectura y reconstrucción de esquema versión 2 ----------

    [Fact]
    public async Task GetByIdAsync_WithSchemaVersion2_PreservesProfileIdAndName()
    {
        var profile = Profile.Create("Mi perfil");
        await _repository.SaveAsync(profile);

        var loaded = await _repository.GetByIdAsync(profile.Id);

        Assert.NotNull(loaded);
        Assert.Equal(profile.Id, loaded!.Id);
        Assert.Equal("Mi perfil", loaded.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithSchemaVersion2_PreservesPageIdNameAndOrder()
    {
        var profile = Profile.Create("Mi perfil");
        var page2 = profile.AddPage("Página 2");
        await _repository.SaveAsync(profile);

        var loaded = await _repository.GetByIdAsync(profile.Id);

        Assert.Equal(2, loaded!.Pages.Count);
        Assert.Equal(profile.Pages[0].Id, loaded.Pages[0].Id);
        Assert.Equal("Página 1", loaded.Pages[0].Name);
        Assert.Equal(page2.Id, loaded.Pages[1].Id);
        Assert.Equal("Página 2", loaded.Pages[1].Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithSchemaVersion2_MultiPageProfileReconstructsCorrectly()
    {
        var profile = Profile.Create("Mi perfil");
        profile.AddPage("Página 2");
        profile.AddPage("Página 3");
        await _repository.SaveAsync(profile);

        var loaded = await _repository.GetByIdAsync(profile.Id);

        Assert.Equal(3, loaded!.Pages.Count);
        Assert.All(loaded.Pages, p => Assert.Equal(9, p.Slots.Count));
    }

    // ---------- Archivos estructuralmente inválidos ----------

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

    // ---------- Validaciones específicas de esquema versión 2 (slots) ----------

    [Fact]
    public async Task GetByIdAsync_WithSchemaVersion2AndMissingSlotPositions_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
          "schemaVersion": 2,
          "id": "{{id}}",
          "name": "Perfil",
          "pages": [ { "id": "{{Guid.NewGuid()}}", "name": "Página 1" } ]
        }
        """);

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetByIdAsync(id));
    }

    [Fact]
    public async Task GetByIdAsync_WithSchemaVersion2AndFewerThanNineSlotPositions_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
          "schemaVersion": 2,
          "id": "{{id}}",
          "name": "Perfil",
          "pages": [ { "id": "{{Guid.NewGuid()}}", "name": "Página 1", "slotPositions": [1,2,3,4,5,6,7,8] } ]
        }
        """);

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetByIdAsync(id));
    }

    [Fact]
    public async Task GetByIdAsync_WithSchemaVersion2AndMoreThanNineSlotPositions_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
          "schemaVersion": 2,
          "id": "{{id}}",
          "name": "Perfil",
          "pages": [ { "id": "{{Guid.NewGuid()}}", "name": "Página 1", "slotPositions": [1,2,3,4,5,6,7,8,9,10] } ]
        }
        """);

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetByIdAsync(id));
    }

    [Fact]
    public async Task GetByIdAsync_WithSchemaVersion2AndDuplicateSlotPositions_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
          "schemaVersion": 2,
          "id": "{{id}}",
          "name": "Perfil",
          "pages": [ { "id": "{{Guid.NewGuid()}}", "name": "Página 1", "slotPositions": [1,2,3,4,5,6,7,8,8] } ]
        }
        """);

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetByIdAsync(id));
    }

    [Fact]
    public async Task GetByIdAsync_WithSchemaVersion2AndOutOfRangeSlotPosition_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
          "schemaVersion": 2,
          "id": "{{id}}",
          "name": "Perfil",
          "pages": [ { "id": "{{Guid.NewGuid()}}", "name": "Página 1", "slotPositions": [1,2,3,4,5,6,7,8,10] } ]
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
    [Fact]
    public async Task SaveAsync_WithAssignedAction_WritesSchemaVersion3WithActionDetails()
    {
        var profile = Profile.Create("Mi perfil");
        profile.Pages[0].Slots[0].AssignAction(new LaunchApplicationAction("/usr/bin/firefox"));

        await _repository.SaveAsync(profile);
        var json = await File.ReadAllTextAsync(Path.Combine(_tempDirectory, $"{profile.Id}.json"));

        using var document = JsonDocument.Parse(json);
        var actionElement = document.RootElement.GetProperty("pages")[0].GetProperty("actions")[0];

        Assert.Equal(1, actionElement.GetProperty("position").GetInt32());
        Assert.Equal("launchApplication", actionElement.GetProperty("type").GetString());
        Assert.Equal("/usr/bin/firefox", actionElement.GetProperty("applicationPath").GetString());
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_PreservesAssignedAction()
    {
        var profile = Profile.Create("Mi perfil");
        profile.Pages[0].Slots[4].AssignAction(new LaunchApplicationAction("/usr/bin/code"));

        await _repository.SaveAsync(profile);
        var loaded = await _repository.GetByIdAsync(profile.Id);

        var loadedAction = Assert.IsType<LaunchApplicationAction>(
            loaded!.Pages[0].Slots.First(s => s.Position == 5).Action);
        Assert.Equal("/usr/bin/code", loadedAction.ApplicationPath);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_SlotsWithoutActionRemainNull()
    {
        var profile = Profile.Create("Mi perfil");
        profile.Pages[0].Slots[0].AssignAction(new LaunchApplicationAction("/usr/bin/firefox"));

        await _repository.SaveAsync(profile);
        var loaded = await _repository.GetByIdAsync(profile.Id);

        Assert.NotNull(loaded!.Pages[0].Slots[0].Action);
        Assert.All(loaded.Pages[0].Slots.Skip(1), s => Assert.Null(s.Action));
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_ActionsOnMultiplePagesAreNotMixed()
    {
        var profile = Profile.Create("Mi perfil");
        var page2 = profile.AddPage("Página 2");
        profile.Pages[0].Slots[0].AssignAction(new LaunchApplicationAction("/usr/bin/firefox"));
        page2.Slots[2].AssignAction(new LaunchApplicationAction("/usr/bin/code"));

        await _repository.SaveAsync(profile);
        var loaded = await _repository.GetByIdAsync(profile.Id);

        Assert.IsType<LaunchApplicationAction>(loaded!.Pages[0].Slots[0].Action);
        Assert.All(loaded.Pages[0].Slots.Skip(1), s => Assert.Null(s.Action));

        Assert.IsType<LaunchApplicationAction>(loaded.Pages[1].Slots[2].Action);
        Assert.All(loaded.Pages[1].Slots.Where(s => s.Position != 3), s => Assert.Null(s.Action));
    }

    [Fact]
    public async Task GetByIdAsync_WithSchemaVersion1File_SlotsHaveNoActions()
    {
        var id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
        "schemaVersion": 1,
        "id": "{{id}}",
        "name": "Perfil antiguo",
        "pages": [ { "id": "{{Guid.NewGuid()}}", "name": "Página 1" } ]
        }
        """);

        var loaded = await _repository.GetByIdAsync(id);

        Assert.All(loaded!.Pages[0].Slots, s => Assert.Null(s.Action));
    }

    [Fact]
    public async Task GetByIdAsync_WithSchemaVersion2File_SlotsHaveNoActions()
    {
        var id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
        "schemaVersion": 2,
        "id": "{{id}}",
        "name": "Perfil v2",
        "pages": [ { "id": "{{Guid.NewGuid()}}", "name": "Página 1", "slotPositions": [1,2,3,4,5,6,7,8,9] } ]
        }
        """);

        var loaded = await _repository.GetByIdAsync(id);

        Assert.Equal(9, loaded!.Pages[0].Slots.Count);
        Assert.All(loaded.Pages[0].Slots, s => Assert.Null(s.Action));
    }

    [Fact]
    public async Task GetByIdAsync_WithSchemaVersion3AndMissingActionsProperty_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
        "schemaVersion": 3,
        "id": "{{id}}",
        "name": "Perfil",
        "pages": [ { "id": "{{Guid.NewGuid()}}", "name": "Página 1", "slotPositions": [1,2,3,4,5,6,7,8,9] } ]
        }
        """);

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetByIdAsync(id));
    }

    [Fact]
    public async Task GetByIdAsync_WithSchemaVersion3AndNullActionsProperty_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
        "schemaVersion": 3,
        "id": "{{id}}",
        "name": "Perfil",
        "pages": [ { "id": "{{Guid.NewGuid()}}", "name": "Página 1", "slotPositions": [1,2,3,4,5,6,7,8,9], "actions": null } ]
        }
        """);

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetByIdAsync(id));
    }

    [Theory]
    [InlineData("""[ { "position": 1, "type": "unknownType", "applicationPath": "/usr/bin/x" } ]""")]
    [InlineData("""[ { "position": 1, "type": "launchApplication", "applicationPath": "   " } ]""")]
    [InlineData("""[ { "position": 0, "type": "launchApplication", "applicationPath": "/usr/bin/x" } ]""")]
    [InlineData("""[ { "position": 10, "type": "launchApplication", "applicationPath": "/usr/bin/x" } ]""")]
    [InlineData("""[ { "position": 1, "type": "launchApplication", "applicationPath": "/usr/bin/x" }, { "position": 1, "type": "launchApplication", "applicationPath": "/usr/bin/y" } ]""")]
    public async Task GetByIdAsync_WithSchemaVersion3AndInvalidActionsArray_ThrowsInvalidDataException(string actionsJson)
    {
        var id = Guid.NewGuid();
        await WriteRawJsonAsync(id, $$"""
        {
        "schemaVersion": 3,
        "id": "{{id}}",
        "name": "Perfil",
        "pages": [
            {
            "id": "{{Guid.NewGuid()}}",
            "name": "Página 1",
            "slotPositions": [1,2,3,4,5,6,7,8,9],
            "actions": {{actionsJson}}
            }
        ]
        }
        """);

        await Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetByIdAsync(id));
    }
}