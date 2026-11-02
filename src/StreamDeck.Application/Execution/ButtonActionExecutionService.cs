using StreamDeck.Application.Profiles;
using StreamDeck.Domain.Entities;

namespace StreamDeck.Application.Execution;

public sealed class ButtonActionExecutionService
{
    private readonly IProfileRepository _repository;
    private readonly IApplicationLauncher _applicationLauncher;
    private readonly IUrlLauncher _urlLauncher;

    public ButtonActionExecutionService(
        IProfileRepository repository,
        IApplicationLauncher applicationLauncher,
        IUrlLauncher urlLauncher)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(applicationLauncher);
        ArgumentNullException.ThrowIfNull(urlLauncher);

        _repository = repository;
        _applicationLauncher = applicationLauncher;
        _urlLauncher = urlLauncher;
    }

    public async Task ExecuteAsync(
        Guid profileId,
        Guid pageId,
        int position,
        CancellationToken cancellationToken = default)
    {
        var profile = await _repository.GetByIdAsync(profileId, cancellationToken).ConfigureAwait(false);

        if (profile is null)
        {
            throw new KeyNotFoundException($"No existe ningún perfil con el id '{profileId}'.");
        }

        var page = profile.Pages.FirstOrDefault(p => p.Id == pageId)
            ?? throw new KeyNotFoundException(
                $"No existe ninguna página con el id '{pageId}' en el perfil '{profileId}'.");

        var slot = page.Slots.FirstOrDefault(s => s.Position == position)
            ?? throw new KeyNotFoundException(
                $"No existe ningún botón en la posición {position} de la página '{pageId}'.");

        if (slot.Action is null)
        {
            throw new ButtonActionNotConfiguredException(
                $"El botón en la posición {position} no tiene ninguna acción asignada.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        switch (slot.Action)
        {
            case LaunchApplicationAction launch:
                _applicationLauncher.Launch(launch.ApplicationPath);
                break;

            case OpenUrlAction openUrl:
                _urlLauncher.Open(openUrl.Url);
                break;

            default:
                throw new NotSupportedException(
                    $"El tipo de acción '{slot.Action.GetType().Name}' todavía no admite ejecución.");
        }
    }
}

public sealed class ButtonActionNotConfiguredException : InvalidOperationException
{
    public ButtonActionNotConfiguredException(string message) : base(message)
    {
    }
}
