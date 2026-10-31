using StreamDeck.Domain.Entities;

namespace StreamDeck.Domain.Tests;

public class ProfileTests
{
    [Fact]
    public void Create_WithValidName_SetsNameAndGeneratesId()
    {
        var profile = Profile.Create("Mi perfil");

        Assert.Equal("Mi perfil", profile.Name);
        Assert.NotEqual(Guid.Empty, profile.Id);
    }

    [Fact]
    public void Create_GeneratesInitialPageNamedPagina1()
    {
        var profile = Profile.Create("Mi perfil");

        Assert.Single(profile.Pages);
        Assert.Equal("Página 1", profile.Pages[0].Name);
        Assert.NotEqual(Guid.Empty, profile.Pages[0].Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        Assert.Throws<ArgumentException>(() => Profile.Create(invalidName!));
    }

    [Fact]
    public void Create_TrimsLeadingAndTrailingSpaces()
    {
        var profile = Profile.Create("   Mi perfil   ");

        Assert.Equal("Mi perfil", profile.Name);
    }

    [Fact]
    public void Rename_WithValidName_UpdatesName()
    {
        var profile = Profile.Create("Original");

        profile.Rename("Nuevo nombre");

        Assert.Equal("Nuevo nombre", profile.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_WithInvalidName_ThrowsArgumentException(string invalidName)
    {
        var profile = Profile.Create("Original");

        Assert.Throws<ArgumentException>(() => profile.Rename(invalidName));
    }

    [Fact]
    public void AddPage_WithValidName_AddsPageToCollection()
    {
        var profile = Profile.Create("Mi perfil");

        var page = profile.AddPage("Página 2");

        Assert.Equal(2, profile.Pages.Count);
        Assert.Contains(profile.Pages, p => p.Id == page.Id && p.Name == "Página 2");
    }

    [Fact]
    public void AddPage_WithInvalidName_ThrowsArgumentException()
    {
        var profile = Profile.Create("Mi perfil");

        Assert.Throws<ArgumentException>(() => profile.AddPage("   "));
    }

    [Fact]
    public void AddPage_AllowsDuplicateNames()
    {
        var profile = Profile.Create("Mi perfil");

        var page1 = profile.AddPage("Página duplicada");
        var page2 = profile.AddPage("Página duplicada");

        Assert.Contains(profile.Pages, p => p.Id == page1.Id && p.Name == "Página duplicada");
        Assert.Contains(profile.Pages, p => p.Id == page2.Id && p.Name == "Página duplicada");
        Assert.NotEqual(page1.Id, page2.Id);
    }

    [Fact]
    public void RenamePage_WithValidName_UpdatesPageName()
    {
        var profile = Profile.Create("Mi perfil");
        var pageId = profile.Pages[0].Id;

        profile.RenamePage(pageId, "Portada");

        Assert.Equal("Portada", profile.Pages[0].Name);
    }

    [Fact]
    public void RenamePage_WithUnknownId_ThrowsKeyNotFoundException()
    {
        var profile = Profile.Create("Mi perfil");

        Assert.Throws<KeyNotFoundException>(() => profile.RenamePage(Guid.NewGuid(), "Portada"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RenamePage_WithInvalidName_ThrowsArgumentException(string invalidName)
    {
        var profile = Profile.Create("Mi perfil");
        var pageId = profile.Pages[0].Id;

        Assert.Throws<ArgumentException>(() => profile.RenamePage(pageId, invalidName));
    }

    [Fact]
    public void RenamePage_TrimsLeadingAndTrailingSpaces()
    {
        var profile = Profile.Create("Mi perfil");
        var pageId = profile.Pages[0].Id;

        profile.RenamePage(pageId, "   Portada   ");

        Assert.Equal("Portada", profile.Pages[0].Name);
    }

    [Fact]
    public void RemovePage_WithMoreThanOnePage_RemovesPage()
    {
        var profile = Profile.Create("Mi perfil");
        var newPage = profile.AddPage("Página 2");

        profile.RemovePage(newPage.Id);

        Assert.Single(profile.Pages);
        Assert.DoesNotContain(profile.Pages, p => p.Id == newPage.Id);
    }

    [Fact]
    public void RemovePage_LastRemainingPage_ThrowsInvalidOperationException()
    {
        var profile = Profile.Create("Mi perfil");
        var onlyPageId = profile.Pages[0].Id;

        Assert.Throws<InvalidOperationException>(() => profile.RemovePage(onlyPageId));
    }

    [Fact]
    public void RemovePage_WithUnknownId_ThrowsKeyNotFoundException()
    {
        var profile = Profile.Create("Mi perfil");
        profile.AddPage("Página 2");

        Assert.Throws<KeyNotFoundException>(() => profile.RemovePage(Guid.NewGuid()));
    }

    [Fact]
    public void RemovePage_UnknownIdWithOnlyOnePage_ThrowsKeyNotFoundException()
    {
        var profile = Profile.Create("Mi perfil");

        Assert.Throws<KeyNotFoundException>(() => profile.RemovePage(Guid.NewGuid()));
    }

    [Fact]
    public void ReorderPage_WithValidIndex_ChangesPageOrder()
    {
        var profile = Profile.Create("Mi perfil");
        var page2 = profile.AddPage("Página 2");
        var page3 = profile.AddPage("Página 3");

        profile.ReorderPage(page3.Id, 0);

        Assert.Equal(page3.Id, profile.Pages[0].Id);
        Assert.Equal(page2.Id, profile.Pages[2].Id);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void ReorderPage_WithIndexOutOfRange_ThrowsArgumentOutOfRangeException(int invalidIndex)
    {
        var profile = Profile.Create("Mi perfil");
        var pageId = profile.Pages[0].Id;

        Assert.Throws<ArgumentOutOfRangeException>(() => profile.ReorderPage(pageId, invalidIndex));
    }

    [Fact]
    public void Pages_IsReadOnlyAndCannotBeModifiedExternally()
    {
        var profile = Profile.Create("Mi perfil");

        Assert.IsAssignableFrom<IReadOnlyList<Page>>(profile.Pages);
        Assert.Throws<NotSupportedException>(() => ((IList<Page>)profile.Pages).Add(profile.Pages[0]));
    }
}