using StreamDeck.Domain.Entities;

namespace StreamDeck.Domain.Tests;

public class ProfileReconstituteTests
{
    [Fact]
    public void Reconstitute_PreservesProfileIdAndName()
    {
        var id = Guid.NewGuid();
        var pages = new[] { (Id: Guid.NewGuid(), Name: "Página 1") };

        var profile = Profile.Reconstitute(id, "Mi perfil", pages);

        Assert.Equal(id, profile.Id);
        Assert.Equal("Mi perfil", profile.Name);
    }

    [Fact]
    public void Reconstitute_PreservesPageIdsNamesAndOrder()
    {
        var page1Id = Guid.NewGuid();
        var page2Id = Guid.NewGuid();
        var pages = new[]
        {
            (Id: page1Id, Name: "Portada"),
            (Id: page2Id, Name: "Macros"),
        };

        var profile = Profile.Reconstitute(Guid.NewGuid(), "Mi perfil", pages);

        Assert.Equal(2, profile.Pages.Count);
        Assert.Equal(page1Id, profile.Pages[0].Id);
        Assert.Equal("Portada", profile.Pages[0].Name);
        Assert.Equal(page2Id, profile.Pages[1].Id);
        Assert.Equal("Macros", profile.Pages[1].Name);
    }

    [Fact]
    public void Reconstitute_TrimsProfileAndPageNames()
    {
        var pages = new[] { (Id: Guid.NewGuid(), Name: "   Página 1   ") };

        var profile = Profile.Reconstitute(Guid.NewGuid(), "   Mi perfil   ", pages);

        Assert.Equal("Mi perfil", profile.Name);
        Assert.Equal("Página 1", profile.Pages[0].Name);
    }

    [Fact]
    public void Reconstitute_AllowsDuplicatePageNames()
    {
        var pages = new[]
        {
            (Id: Guid.NewGuid(), Name: "Igual"),
            (Id: Guid.NewGuid(), Name: "Igual"),
        };

        var profile = Profile.Reconstitute(Guid.NewGuid(), "Mi perfil", pages);

        Assert.Equal(2, profile.Pages.Count);
        Assert.All(profile.Pages, p => Assert.Equal("Igual", p.Name));
    }

    [Fact]
    public void Reconstitute_WithEmptyProfileId_ThrowsArgumentException()
    {
        var pages = new[] { (Id: Guid.NewGuid(), Name: "Página 1") };

        Assert.Throws<ArgumentException>(() => Profile.Reconstitute(Guid.Empty, "Mi perfil", pages));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reconstitute_WithInvalidProfileName_ThrowsArgumentException(string invalidName)
    {
        var pages = new[] { (Id: Guid.NewGuid(), Name: "Página 1") };

        Assert.Throws<ArgumentException>(() => Profile.Reconstitute(Guid.NewGuid(), invalidName, pages));
    }

    [Fact]
    public void Reconstitute_WithNoPages_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => Profile.Reconstitute(Guid.NewGuid(), "Mi perfil", Array.Empty<(Guid, string)>()));
    }

    [Fact]
    public void Reconstitute_WithEmptyPageId_ThrowsArgumentException()
    {
        var pages = new[] { (Id: Guid.Empty, Name: "Página 1") };

        Assert.Throws<ArgumentException>(() => Profile.Reconstitute(Guid.NewGuid(), "Mi perfil", pages));
    }

    [Fact]
    public void Reconstitute_WithDuplicatePageIds_ThrowsArgumentException()
    {
        var duplicateId = Guid.NewGuid();
        var pages = new[]
        {
            (Id: duplicateId, Name: "Página 1"),
            (Id: duplicateId, Name: "Página 2"),
        };

        Assert.Throws<ArgumentException>(() => Profile.Reconstitute(Guid.NewGuid(), "Mi perfil", pages));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reconstitute_WithInvalidPageName_ThrowsArgumentException(string invalidName)
    {
        var pages = new[] { (Id: Guid.NewGuid(), Name: invalidName) };

        Assert.Throws<ArgumentException>(() => Profile.Reconstitute(Guid.NewGuid(), "Mi perfil", pages));
    }

    [Fact]
    public void Reconstitute_ReconstitutedProfileCanStillBeModified()
    {
        var pages = new[] { (Id: Guid.NewGuid(), Name: "Página 1") };
        var profile = Profile.Reconstitute(Guid.NewGuid(), "Mi perfil", pages);

        var newPage = profile.AddPage("Página 2");

        Assert.Equal(2, profile.Pages.Count);
        Assert.Contains(profile.Pages, p => p.Id == newPage.Id);
    }
}