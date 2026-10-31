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
}