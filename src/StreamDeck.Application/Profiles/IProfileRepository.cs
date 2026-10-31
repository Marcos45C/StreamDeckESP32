using StreamDeck.Domain.Entities;

namespace StreamDeck.Application.Profiles;

public interface IProfileRepository
{
    Task<IReadOnlyList<Profile>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Profile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(Profile profile, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}