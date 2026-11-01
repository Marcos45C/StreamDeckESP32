
using StreamDeck.Application.Profiles;
using StreamDeck.Domain.Entities;
namespace StreamDeck.Application.Tests;

public class ProfileServiceTests
{
    // ---------- Perfiles ----------

    [Fact]
    public async Task GetAllProfilesAsync_ReturnsAllProfiles()
    {
        var repository = new InMemoryProfileRepository();
        var profile1 = Profile.Create("Perfil 1");
        var profile2 = Profile.Create("Perfil 2");
        await repository.SaveAsync(profile1);
        await repository.SaveAsync(profile2);
        var service = new ProfileService(repository);

        var all = await service.GetAllProfilesAsync();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, p => p.Id == profile1.Id);
        Assert.Contains(all, p => p.Id == profile2.Id);
    }

    [Fact]
    public async Task GetProfileByIdAsync_WithExistingId_ReturnsProfile()
    {
        var repository = new InMemoryProfileRepository();
        var profile = Profile.Create("Mi perfil");
        await repository.SaveAsync(profile);
        var service = new ProfileService(repository);

        var result = await service.GetProfileByIdAsync(profile.Id);

        Assert.NotNull(result);
        Assert.Equal(profile.Id, result!.Id);
    }

    [Fact]
    public async Task GetProfileByIdAsync_WithUnknownId_ReturnsNull()
    {
        var service = new ProfileService(new InMemoryProfileRepository());

        var result = await service.GetProfileByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateProfileAsync_CreatesProfileAndCallsSaveExactlyOnce()
    {
        var repository = new InMemoryProfileRepository();
        var service = new ProfileService(repository);
        var saveCountBefore = repository.SaveCallCount;

        var created = await service.CreateProfileAsync("Mi perfil");

        Assert.Equal("Mi perfil", created.Name);
        Assert.Equal(saveCountBefore + 1, repository.SaveCallCount);
        Assert.Contains(created.Id, repository.SavedProfileIds);
    }

    [Fact]
    public async Task CreateProfileAsync_PersistsProfileRetrievableFromFreshRead()
    {
        var repository = new InMemoryProfileRepository();
        var service = new ProfileService(repository);

        var created = await service.CreateProfileAsync("Mi perfil");

        // Lectura fresca e independiente del objeto devuelto por CreateProfileAsync.
        var persisted = await repository.GetByIdAsync(created.Id);

        Assert.NotNull(persisted);
        Assert.NotSame(created, persisted);
        Assert.Equal("Mi perfil", persisted!.Name);
        Assert.Single(persisted.Pages);
        Assert.Equal("Página 1", persisted.Pages[0].Name);
    }

    [Fact]
    public async Task RenameProfileAsync_CallsSaveExactlyOnceAndPersistsNewName()
    {
        var repository = new InMemoryProfileRepository();
        var profile = Profile.Create("Nombre original");
        await repository.SaveAsync(profile);
        var service = new ProfileService(repository);
        var saveCountBefore = repository.SaveCallCount;

        var renamed = await service.RenameProfileAsync(profile.Id, "Nombre nuevo");

        Assert.Equal("Nombre nuevo", renamed.Name);
        Assert.Equal(saveCountBefore + 1, repository.SaveCallCount);

        // Verificación de persistencia real: lectura fresca del repositorio,
        // no el objeto "renamed" devuelto por el servicio.
        var persisted = await repository.GetByIdAsync(profile.Id);
        Assert.Equal("Nombre nuevo", persisted!.Name);
    }

    [Fact]
    public async Task RenameProfileAsync_WithUnknownId_ThrowsKeyNotFoundExceptionAndDoesNotCallSave()
    {
        var repository = new InMemoryProfileRepository();
        var service = new ProfileService(repository);
        var saveCountBefore = repository.SaveCallCount;

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.RenameProfileAsync(Guid.NewGuid(), "Nombre nuevo"));

        Assert.Equal(saveCountBefore, repository.SaveCallCount);
    }

    [Fact]
    public async Task DeleteProfileAsync_RemovesProfile()
    {
        var repository = new InMemoryProfileRepository();
        var profile = Profile.Create("Mi perfil");
        await repository.SaveAsync(profile);
        var service = new ProfileService(repository);

        await service.DeleteProfileAsync(profile.Id);

        Assert.Null(await repository.GetByIdAsync(profile.Id));
    }

    [Fact]
    public async Task DeleteProfileAsync_WithUnknownId_DoesNotThrow()
    {
        var service = new ProfileService(new InMemoryProfileRepository());

        var exception = await Record.ExceptionAsync(() => service.DeleteProfileAsync(Guid.NewGuid()));

        Assert.Null(exception);
    }

    // ---------- Páginas ----------

    [Fact]
    public async Task AddPageAsync_CallsSaveExactlyOnceAndPersistsNewPage()
    {
        var repository = new InMemoryProfileRepository();
        var profile = Profile.Create("Mi perfil");
        await repository.SaveAsync(profile);
        var service = new ProfileService(repository);
        var saveCountBefore = repository.SaveCallCount;

        var newPage = await service.AddPageAsync(profile.Id, "Página 2");

        Assert.Equal("Página 2", newPage.Name);
        Assert.Equal(saveCountBefore + 1, repository.SaveCallCount);

        var persisted = await repository.GetByIdAsync(profile.Id);
        Assert.Equal(2, persisted!.Pages.Count);
        Assert.Contains(persisted.Pages, p => p.Id == newPage.Id);
    }

    [Fact]
    public async Task AddPageAsync_WithUnknownProfileId_ThrowsKeyNotFoundExceptionAndDoesNotCallSave()
    {
        var repository = new InMemoryProfileRepository();
        var service = new ProfileService(repository);
        var saveCountBefore = repository.SaveCallCount;

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.AddPageAsync(Guid.NewGuid(), "Página 2"));

        Assert.Equal(saveCountBefore, repository.SaveCallCount);
    }

    [Fact]
    public async Task RenamePageAsync_CallsSaveExactlyOnceAndPersistsNewName()
    {
        var repository = new InMemoryProfileRepository();
        var profile = Profile.Create("Mi perfil");
        var pageId = profile.Pages[0].Id;
        await repository.SaveAsync(profile);
        var service = new ProfileService(repository);
        var saveCountBefore = repository.SaveCallCount;

        await service.RenamePageAsync(profile.Id, pageId, "Portada");

        Assert.Equal(saveCountBefore + 1, repository.SaveCallCount);

        var persisted = await repository.GetByIdAsync(profile.Id);
        Assert.Equal("Portada", persisted!.Pages[0].Name);
    }

    [Fact]
    public async Task RenamePageAsync_WithUnknownProfileId_ThrowsKeyNotFoundExceptionAndDoesNotCallSave()
    {
        var repository = new InMemoryProfileRepository();
        var service = new ProfileService(repository);
        var saveCountBefore = repository.SaveCallCount;

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.RenamePageAsync(Guid.NewGuid(), Guid.NewGuid(), "Portada"));

        Assert.Equal(saveCountBefore, repository.SaveCallCount);
    }

    [Fact]
    public async Task RemovePageAsync_CallsSaveExactlyOnceAndPersistsRemoval()
    {
        var repository = new InMemoryProfileRepository();
        var profile = Profile.Create("Mi perfil");
        var page2 = profile.AddPage("Página 2");
        await repository.SaveAsync(profile);
        var service = new ProfileService(repository);
        var saveCountBefore = repository.SaveCallCount;

        await service.RemovePageAsync(profile.Id, page2.Id);

        Assert.Equal(saveCountBefore + 1, repository.SaveCallCount);

        var persisted = await repository.GetByIdAsync(profile.Id);
        Assert.Single(persisted!.Pages);
        Assert.DoesNotContain(persisted.Pages, p => p.Id == page2.Id);
    }

    [Fact]
    public async Task RemovePageAsync_WithUnknownProfileId_ThrowsKeyNotFoundExceptionAndDoesNotCallSave()
    {
        var repository = new InMemoryProfileRepository();
        var service = new ProfileService(repository);
        var saveCountBefore = repository.SaveCallCount;

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.RemovePageAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(saveCountBefore, repository.SaveCallCount);
    }

    [Fact]
    public async Task ReorderPageAsync_CallsSaveExactlyOnceAndPersistsNewOrder()
    {
        var repository = new InMemoryProfileRepository();
        var profile = Profile.Create("Mi perfil");
        var page2 = profile.AddPage("Página 2");
        var page3 = profile.AddPage("Página 3");
        await repository.SaveAsync(profile);
        var service = new ProfileService(repository);
        var saveCountBefore = repository.SaveCallCount;

        await service.ReorderPageAsync(profile.Id, page3.Id, 0);

        Assert.Equal(saveCountBefore + 1, repository.SaveCallCount);

        var persisted = await repository.GetByIdAsync(profile.Id);
        Assert.Equal(page3.Id, persisted!.Pages[0].Id);
        Assert.Equal(page2.Id, persisted.Pages[2].Id);
    }

    [Fact]
    public async Task ReorderPageAsync_WithUnknownProfileId_ThrowsKeyNotFoundExceptionAndDoesNotCallSave()
    {
        var repository = new InMemoryProfileRepository();
        var service = new ProfileService(repository);
        var saveCountBefore = repository.SaveCallCount;

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.ReorderPageAsync(Guid.NewGuid(), Guid.NewGuid(), 0));

        Assert.Equal(saveCountBefore, repository.SaveCallCount);
    }

    // ---------- Independencia de copias (sin referencias compartidas) ----------

    [Fact]
    public async Task GetProfileByIdAsync_MutatingReturnedProfile_DoesNotAffectStoredState()
    {
        var repository = new InMemoryProfileRepository();
        var profile = Profile.Create("Nombre original");
        await repository.SaveAsync(profile);
        var service = new ProfileService(repository);

        var fetched = await service.GetProfileByIdAsync(profile.Id);
        fetched!.Rename("Nombre mutado localmente"); // nunca se guarda

        var fetchedAgain = await service.GetProfileByIdAsync(profile.Id);

        Assert.Equal("Nombre original", fetchedAgain!.Name);
        Assert.NotSame(fetched, fetchedAgain);
    }

    [Fact]
    public async Task CreateProfileAsync_MutatingReturnedProfile_DoesNotAffectStoredStateWithoutExplicitSave()
    {
        var repository = new InMemoryProfileRepository();
        var service = new ProfileService(repository);

        var created = await service.CreateProfileAsync("Nombre original");
        created.Rename("Nombre mutado localmente"); // nunca se vuelve a guardar

        var persisted = await repository.GetByIdAsync(created.Id);

        Assert.Equal("Nombre original", persisted!.Name);
    }

    [Fact]
    public async Task GetAllProfilesAsync_ReturnsIndependentCopiesNotSharedWithEachOtherOrWithStorage()
    {
        var repository = new InMemoryProfileRepository();
        var profile = Profile.Create("Mi perfil");
        await repository.SaveAsync(profile);
        var service = new ProfileService(repository);

        var firstRead = await service.GetAllProfilesAsync();
        var secondRead = await service.GetAllProfilesAsync();

        Assert.NotSame(firstRead[0], secondRead[0]);
    }

    // ---------- Propagación de errores del dominio ----------

    [Fact]
    public async Task RemovePageAsync_LastRemainingPage_PropagatesInvalidOperationExceptionAndDoesNotCallSave()
    {
        var repository = new InMemoryProfileRepository();
        var profile = Profile.Create("Mi perfil");
        var onlyPageId = profile.Pages[0].Id;
        await repository.SaveAsync(profile);
        var service = new ProfileService(repository);
        var saveCountBefore = repository.SaveCallCount;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RemovePageAsync(profile.Id, onlyPageId));

        Assert.Equal(saveCountBefore, repository.SaveCallCount);
    }

    [Fact]
    public async Task RenamePageAsync_WithInvalidName_PropagatesArgumentExceptionAndDoesNotCallSave()
    {
        var repository = new InMemoryProfileRepository();
        var profile = Profile.Create("Mi perfil");
        var pageId = profile.Pages[0].Id;
        await repository.SaveAsync(profile);
        var service = new ProfileService(repository);
        var saveCountBefore = repository.SaveCallCount;

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RenamePageAsync(profile.Id, pageId, "   "));

        Assert.Equal(saveCountBefore, repository.SaveCallCount);
    }

    [Fact]
    public async Task ReorderPageAsync_WithIndexOutOfRange_PropagatesArgumentOutOfRangeExceptionAndDoesNotCallSave()
    {
        var repository = new InMemoryProfileRepository();
        var profile = Profile.Create("Mi perfil");
        var pageId = profile.Pages[0].Id;
        await repository.SaveAsync(profile);
        var service = new ProfileService(repository);
        var saveCountBefore = repository.SaveCallCount;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.ReorderPageAsync(profile.Id, pageId, 5));

        Assert.Equal(saveCountBefore, repository.SaveCallCount);
    }

    // ---------- Cancelación ----------

    [Fact]
    public async Task GetAllProfilesAsync_WhenCancelled_PropagatesOperationCanceledException()
    {
        var service = new ProfileService(new InMemoryProfileRepository());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetAllProfilesAsync(cts.Token));
    }

    [Fact]
    public async Task CreateProfileAsync_WhenCancelled_PropagatesOperationCanceledExceptionAndDoesNotPersist()
    {
        var repository = new InMemoryProfileRepository();
        var service = new ProfileService(repository);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CreateProfileAsync("Mi perfil", cts.Token));

        Assert.Equal(0, repository.SaveCallCount);
        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task AddPageAsync_WhenCancelled_PropagatesOperationCanceledExceptionAndDoesNotCallSave()
    {
        var repository = new InMemoryProfileRepository();
        var service = new ProfileService(repository);
        var profile = await service.CreateProfileAsync("Mi perfil");
        var saveCountBefore = repository.SaveCallCount;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.AddPageAsync(profile.Id, "Página 2", cts.Token));

        Assert.Equal(saveCountBefore, repository.SaveCallCount);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ProfileService(null!));
    }
}