using System.Diagnostics;
using StreamDeck.Application.Execution;

namespace StreamDeck.Infrastructure.Execution;

public sealed class SystemUrlLauncher : IUrlLauncher
{
    public void Open(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("La URL no puede estar vacía.", nameof(url));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("La URL debe ser una dirección HTTP o HTTPS absoluta.", nameof(url));
        }

        var startInfo = CreateStartInfo(url);
        using var process = Process.Start(startInfo);

        if (process is null)
        {
            throw new InvalidOperationException($"No se pudo abrir la dirección web '{url}'.");
        }
    }

    private static ProcessStartInfo CreateStartInfo(string url)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            };
        }

        if (OperatingSystem.IsLinux())
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "xdg-open",
                UseShellExecute = false,
            };

            // ArgumentList evita interpretar la URL como parte de un comando de shell.
            startInfo.ArgumentList.Add(url);
            return startInfo;
        }

        throw new PlatformNotSupportedException(
            "La apertura de direcciones web está implementada para Windows y Linux.");
    }
}
