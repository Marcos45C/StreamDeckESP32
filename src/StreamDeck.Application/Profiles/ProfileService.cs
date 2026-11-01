using StreamDeck.Domain.Entities;

namespace StreamDeck.Application.Profiles;

public sealed class ProfileService
{
    private readonly IProfileRepository _repository;

    public ProfileService(IProfileRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public Task<IReadOnlyList<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public Task<Profile?> GetProfileByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public async Task<Profile> CreateProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        var profile = Profile.Create(name);
        await _repository.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        return profile;
    }

    public async Task<Profile> RenameProfileAsync(Guid id, string name, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileOrThrowAsync(id, cancellationToken).ConfigureAwait(false);
        profile.Rename(name);
        await _repository.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        return profile;
    }

    public Task DeleteProfileAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, cancellationToken);

    public async Task<Page> AddPageAsync(Guid profileId, string name, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileOrThrowAsync(profileId, cancellationToken).ConfigureAwait(false);
        var page = profile.AddPage(name);
        await _repository.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        return page;
    }

    public async Task RenamePageAsync(Guid profileId, Guid pageId, string name, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileOrThrowAsync(profileId, cancellationToken).ConfigureAwait(false);
        profile.RenamePage(pageId, name);
        await _repository.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemovePageAsync(Guid profileId, Guid pageId, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileOrThrowAsync(profileId, cancellationToken).ConfigureAwait(false);
        profile.RemovePage(pageId);
        await _repository.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    public async Task ReorderPageAsync(Guid profileId, Guid pageId, int newIndex, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileOrThrowAsync(profileId, cancellationToken).ConfigureAwait(false);
        profile.ReorderPage(pageId, newIndex);
        await _repository.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Profile> GetProfileOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (profile is null)
        {
            throw new KeyNotFoundException($"No existe ningún perfil con el id '{id}'.");
        }

        return profile;
    }
    // Se agregan, junto a GetProfileOrThrowAsync:

    private static Page FindPageOrThrow(Profile profile, Guid pageId)
    {
        return profile.Pages.FirstOrDefault(p => p.Id == pageId)
            ?? throw new KeyNotFoundException($"No existe ninguna página con el id '{pageId}' en el perfil '{profile.Id}'.");
    }

    private static ButtonSlot FindSlotOrThrow(Page page, int position)
    {
        return page.Slots.FirstOrDefault(s => s.Position == position)
            ?? throw new KeyNotFoundException($"No existe ningún botón en la posición {position} de la página '{page.Id}'.");
    }

    public async Task AssignApplicationActionAsync(
        Guid profileId, Guid pageId, int position, string applicationPath, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileOrThrowAsync(profileId, cancellationToken).ConfigureAwait(false);
        var page = FindPageOrThrow(profile, pageId);
        var slot = FindSlotOrThrow(page, position);

        slot.AssignAction(new LaunchApplicationAction(applicationPath));
        await _repository.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearButtonActionAsync(
        Guid profileId, Guid pageId, int position, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileOrThrowAsync(profileId, cancellationToken).ConfigureAwait(false);
        var page = FindPageOrThrow(profile, pageId);
        var slot = FindSlotOrThrow(page, position);

        slot.ClearAction();
        await _repository.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
    }
}