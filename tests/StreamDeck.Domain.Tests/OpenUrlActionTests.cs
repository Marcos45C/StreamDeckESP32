using StreamDeck.Domain.Entities;

namespace StreamDeck.Domain.Tests;

public class OpenUrlActionTests
{
    [Fact]
    public void Create_WithHttpUrl_Succeeds()
    {
        var action = new OpenUrlAction("http://example.com");

        Assert.Equal("http://example.com", action.Url);
    }

    [Fact]
    public void Create_WithHttpsUrl_Succeeds()
    {
        var action = new OpenUrlAction("https://example.com/path?query=1");

        Assert.Equal("https://example.com/path?query=1", action.Url);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyUrl_ThrowsArgumentException(string? invalidUrl)
    {
        Assert.Throws<ArgumentException>(() => new OpenUrlAction(invalidUrl!));
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("example.com")]
    [InlineData("/relative/path")]
    public void Create_WithInvalidOrRelativeUrl_ThrowsArgumentException(string invalidUrl)
    {
        Assert.Throws<ArgumentException>(() => new OpenUrlAction(invalidUrl));
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("mailto:test@example.com")]
    public void Create_WithNonHttpScheme_ThrowsArgumentException(string invalidUrl)
    {
        Assert.Throws<ArgumentException>(() => new OpenUrlAction(invalidUrl));
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var action = new OpenUrlAction("   https://example.com   ");

        Assert.Equal("https://example.com", action.Url);
    }
}