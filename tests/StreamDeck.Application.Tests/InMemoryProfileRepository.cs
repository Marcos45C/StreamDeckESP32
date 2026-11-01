using StreamDeck.Application.Profiles;
using StreamDeck.Domain.Entities;

namespace StreamDeck.Application.Tests;

/// <summary>
/// Repositorio en memoria para tests de ProfileService. No toca disco.
/// No comparte referencias de Profile con el llamador: SaveAsync almacena una copia
/// independiente, y GetByIdAsync/GetAllAsync devuelven copias independientes de lo
/// almacenado. Así, un cambio solo queda persistido si SaveAsync se ejecuta de nuevo.
/// </summary>
internal sealed class InMemoryProfileRepository : IProfileRepository
{
    private readonly Dictionary<Guid, Profile> _profiles = new();

    public List<Guid> SavedProfileIds { get; } = new();

    public int SaveCallCount { get; private set; }

    public Task<IReadOnlyList<Profile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<Profile> snapshot = _profiles.Values.Select(Clone).ToList();
        return Task.FromResult(snapshot);
    }

    public Task<Profile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = _profiles.TryGetValue(id, out var stored) ? Clone(stored) : null;
        return Task.FromResult(result);
    }

    public Task SaveAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(profile);

        _profiles[profile.Id] = Clone(profile);
        SavedProfileIds.Add(profile.Id);
        SaveCallCount++;

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _profiles.Remove(id);
        return Task.CompletedTask;
    }

    // Ahora
    private static Profile Clone(Profile profile)
    {
        var pages = profile.Pages.Select(p => (
            p.Id,
            p.Name,
            (IEnumerable<int>)p.Slots.Select(s => s.Position).ToList()));

        var cloned = Profile.Reconstitute(profile.Id, profile.Name, pages);

        foreach (var originalPage in profile.Pages)
        {
            var clonedPage = cloned.Pages.First(p => p.Id == originalPage.Id);

            foreach (var originalSlot in originalPage.Slots)
            {
                if (originalSlot.Action is LaunchApplicationAction launch)
                {
                    var clonedSlot = clonedPage.Slots.First(s => s.Position == originalSlot.Position);
                    clonedSlot.AssignAction(new LaunchApplicationAction(launch.ApplicationPath));
                }
            }
        }

        return cloned;
    }
}