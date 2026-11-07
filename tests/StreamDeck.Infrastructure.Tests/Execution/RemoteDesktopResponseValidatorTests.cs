using StreamDeck.Application.Execution;
using StreamDeck.Infrastructure.Execution;
using Tmds.DBus.Protocol;

namespace StreamDeck.Infrastructure.Tests.Execution;

public sealed class RemoteDesktopResponseValidatorTests
{
    [Fact]
    public void EnsureRequestSucceeded_WhenResponseIsSuccess_DoesNotThrow()
    {
        RemoteDesktopResponseValidator.EnsureRequestSucceeded(0, "Start");
    }

    [Fact]
    public void EnsureRequestSucceeded_WhenUserCancelled_ThrowsAuthorizationDenied()
    {
        var exception = Assert.Throws<KeyboardAuthorizationDeniedException>(
            () => RemoteDesktopResponseValidator.EnsureRequestSucceeded(1, "Start"));

        Assert.Contains("cancelada", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureRequestSucceeded_WhenPortalEndsRequestUnexpectedly_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(
            () => RemoteDesktopResponseValidator.EnsureRequestSucceeded(2, "Start"));
    }

    [Fact]
    public void GetRequiredUInt32Value_WhenValueIsUInt32_ReturnsValue()
    {
        var value = RemoteDesktopResponseValidator.GetRequiredUInt32Value((uint)7, "AvailableDeviceTypes");

        Assert.Equal(7u, value);
    }

    [Fact]
    public void GetRequiredUInt32Value_WhenValueHasWrongType_ThrowsInvalidData()
    {
        Assert.Throws<InvalidDataException>(
            () => RemoteDesktopResponseValidator.GetRequiredUInt32Value("7", "AvailableDeviceTypes"));
    }

    [Fact]
    public void GetRequiredSessionHandle_WhenResultContainsSessionObjectPath_ReturnsPath()
    {
        const string expectedPath = "/org/freedesktop/portal/desktop/session/1_42/streamdeck_session_abc";
        var results = new Dictionary<string, VariantValue>
        {
            // The portal returns the session path in a string variant (signature "s").
            ["session_handle"] = expectedPath,
        };

        var actualPath = RemoteDesktopResponseValidator.GetRequiredSessionHandle(results);

        Assert.Equal(expectedPath, actualPath);
    }

    [Fact]
    public void GetRequiredSessionHandle_WhenPathIsOutsidePortalSessionNamespace_ThrowsInvalidData()
    {
        var results = new Dictionary<string, VariantValue>
        {
            ["session_handle"] = "/org/example/session/invalid",
        };

        Assert.Throws<InvalidDataException>(
            () => RemoteDesktopResponseValidator.GetRequiredSessionHandle(results));
    }

    [Fact]
    public void GetRequiredSessionHandle_WhenPathContainsInvalidSegments_ThrowsInvalidData()
    {
        var results = new Dictionary<string, VariantValue>
        {
            ["session_handle"] = "/org/freedesktop/portal/desktop/session/1_42/invalid-token!",
        };

        Assert.Throws<InvalidDataException>(
            () => RemoteDesktopResponseValidator.GetRequiredSessionHandle(results));
    }

    [Fact]
    public void GetRequiredSessionHandle_WhenValueIsNotString_ThrowsInvalidData()
    {
        var results = new Dictionary<string, VariantValue>
        {
            ["session_handle"] = new ObjectPath("/org/freedesktop/portal/desktop/session/1_42/token"),
        };

        Assert.Throws<InvalidDataException>(
            () => RemoteDesktopResponseValidator.GetRequiredSessionHandle(results));
    }

    [Fact]
    public void GetRequiredSessionHandle_WhenResultIsMissing_ThrowsInvalidData()
    {
        Assert.Throws<InvalidDataException>(
            () => RemoteDesktopResponseValidator.GetRequiredSessionHandle(new Dictionary<string, VariantValue>()));
    }

    [Fact]
    public void EnsureKeyboardOnlyGranted_WhenOnlyKeyboardIsGranted_DoesNotThrow()
    {
        var results = new Dictionary<string, VariantValue>
        {
            ["devices"] = (uint)RemoteDesktopResponseValidator.KeyboardDeviceType,
        };

        RemoteDesktopResponseValidator.EnsureKeyboardOnlyGranted(results);
    }

    [Fact]
    public void EnsureKeyboardOnlyGranted_WhenKeyboardIsNotGranted_ThrowsAuthorizationDenied()
    {
        var results = new Dictionary<string, VariantValue>
        {
            ["devices"] = (uint)RemoteDesktopResponseValidator.PointerDeviceType,
        };

        Assert.Throws<KeyboardAuthorizationDeniedException>(
            () => RemoteDesktopResponseValidator.EnsureKeyboardOnlyGranted(results));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void EnsureKeyboardOnlyGranted_WhenUnexpectedDeviceIsGranted_ThrowsInvalidData(uint devices)
    {
        var results = new Dictionary<string, VariantValue>
        {
            ["devices"] = (uint)devices,
        };

        Assert.Throws<InvalidDataException>(
            () => RemoteDesktopResponseValidator.EnsureKeyboardOnlyGranted(results));
    }
}
