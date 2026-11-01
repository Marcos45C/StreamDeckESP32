using System.Diagnostics;
using StreamDeck.Application.Execution;

namespace StreamDeck.Infrastructure.Execution;

public sealed class SystemApplicationLauncher : IApplicationLauncher
{
    public void Launch(string applicationPath)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = applicationPath,
        UseShellExecute = false,
    };

    using var process = Process.Start(startInfo);

    if (process is null)
    {
        throw new InvalidOperationException($"No se pudo iniciar la aplicación '{applicationPath}'.");
    }
}
}