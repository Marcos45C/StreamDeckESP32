namespace StreamDeck.Domain.Entities;

public sealed class LaunchApplicationAction : ButtonAction
{
    public string ApplicationPath { get; }

    public LaunchApplicationAction(string applicationPath)
    {
        if (string.IsNullOrWhiteSpace(applicationPath))
        {
            throw new ArgumentException("La ruta de la aplicación no puede estar vacía.", nameof(applicationPath));
        }

        ApplicationPath = applicationPath.Trim();
    }
}