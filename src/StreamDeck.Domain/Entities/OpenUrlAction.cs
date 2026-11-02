namespace StreamDeck.Domain.Entities;

public sealed class OpenUrlAction : ButtonAction
{
    public string Url { get; }

    public OpenUrlAction(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("La URL no puede estar vacía.", nameof(url));
        }

        var trimmedUrl = url.Trim();

        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("La URL debe ser una dirección absoluta válida.", nameof(url));
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("La URL debe utilizar el esquema http o https.", nameof(url));
        }

        Url = trimmedUrl;
    }
}