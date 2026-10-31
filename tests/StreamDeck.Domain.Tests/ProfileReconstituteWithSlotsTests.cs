using StreamDeck.Domain.Entities;

namespace StreamDeck.Domain.Tests;

public class ProfileReconstituteWithSlotsTests
{
    [Fact]
    public void Reconstitute_WithSlotPositions_PreservesProfileIdAndName()
    {
        var id = Guid.NewGuid();
        var pages = new List<(Guid Id, string Name, IEnumerable<int> SlotPositions)>
        {
            (Guid.NewGuid(), "Página 1", Enumerable.Range(1, 9)),
        };

        var profile = Profile.Reconstitute(id, "Mi perfil", pages);

        Assert.Equal(id, profile.Id);
        Assert.Equal("Mi perfil", profile.Name);
    }

    [Fact]
    public void Reconstitute_WithSlotPositions_ReconstructsAllPagesWithNineSlots()
    {
        var page1Id = Guid.NewGuid();
        var page2Id = Guid.NewGuid();
        var pages = new List<(Guid Id, string Name, IEnumerable<int> SlotPositions)>
        {
            (page1Id, "Portada", Enumerable.Range(1, 9)),
            (page2Id, "Macros", Enumerable.Range(1, 9)),
        };

        var profile = Profile.Reconstitute(Guid.NewGuid(), "Mi perfil", pages);

        Assert.Equal(2, profile.Pages.Count);
        Assert.All(profile.Pages, p => Assert.Equal(9, p.Slots.Count));
    }

    [Fact]
    public void Reconstitute_WithSlotPositions_PreservesPageIdsAndOrder()
    {
        var page1Id = Guid.NewGuid();
        var page2Id = Guid.NewGuid();
        var pages = new List<(Guid Id, string Name, IEnumerable<int> SlotPositions)>
        {
            (page1Id, "Portada", Enumerable.Range(1, 9)),
            (page2Id, "Macros", Enumerable.Range(1, 9)),
        };

        var profile = Profile.Reconstitute(Guid.NewGuid(), "Mi perfil", pages);

        Assert.Equal(page1Id, profile.Pages[0].Id);
        Assert.Equal("Portada", profile.Pages[0].Name);
        Assert.Equal(page2Id, profile.Pages[1].Id);
        Assert.Equal("Macros", profile.Pages[1].Name);
    }

    [Fact]
    public void Reconstitute_WithSlotPositions_PreservesSlotPositionsPerPage()
    {
        var pageId = Guid.NewGuid();
        var scrambled = new[] { 9, 1, 8, 2, 7, 3, 6, 4, 5 };
        var pages = new List<(Guid Id, string Name, IEnumerable<int> SlotPositions)>
        {
            (pageId, "Portada", scrambled),
        };

        var profile = Profile.Reconstitute(Guid.NewGuid(), "Mi perfil", pages);

        var slots = profile.Pages[0].Slots;
        for (var i = 0; i < 9; i++)
        {
            Assert.Equal(i + 1, slots[i].Position);
        }
    }

    [Fact]
    public void Reconstitute_WithSlotPositions_WithEmptyProfileId_ThrowsArgumentException()
    {
        var pages = new List<(Guid Id, string Name, IEnumerable<int> SlotPositions)>
        {
            (Guid.NewGuid(), "Página 1", Enumerable.Range(1, 9)),
        };

        Assert.Throws<ArgumentException>(() => Profile.Reconstitute(Guid.Empty, "Mi perfil", pages));
    }

    [Fact]
    public void Reconstitute_WithSlotPositions_WithNoPages_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Profile.Reconstitute(
            Guid.NewGuid(),
            "Mi perfil",
            Array.Empty<(Guid, string, IEnumerable<int>)>()));
    }

    [Fact]
    public void Reconstitute_WithSlotPositions_WithDuplicatePageIds_ThrowsArgumentException()
    {
        var duplicateId = Guid.NewGuid();
        var pages = new List<(Guid Id, string Name, IEnumerable<int> SlotPositions)>
        {
            (duplicateId, "Página 1", Enumerable.Range(1, 9)),
            (duplicateId, "Página 2", Enumerable.Range(1, 9)),
        };

        Assert.Throws<ArgumentException>(() => Profile.Reconstitute(Guid.NewGuid(), "Mi perfil", pages));
    }

    [Fact]
    public void Reconstitute_WithSlotPositions_WithInvalidSlotCountOnOnePage_ThrowsArgumentException()
    {
        var pages = new List<(Guid Id, string Name, IEnumerable<int> SlotPositions)>
        {
            (Guid.NewGuid(), "Página con pocos espacios", Enumerable.Range(1, 8)),
        };

        Assert.Throws<ArgumentException>(() => Profile.Reconstitute(Guid.NewGuid(), "Mi perfil", pages));
    }

    [Fact]
    public void Reconstitute_WithSlotPositions_WithDuplicateSlotPositionsOnOnePage_ThrowsArgumentException()
    {
        var invalidPositions = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 8 };
        var pages = new List<(Guid Id, string Name, IEnumerable<int> SlotPositions)>
        {
            (Guid.NewGuid(), "Página con posiciones duplicadas", invalidPositions),
        };

        Assert.Throws<ArgumentException>(() => Profile.Reconstitute(Guid.NewGuid(), "Mi perfil", pages));
    }

    [Fact]
    public void Reconstitute_WithSlotPositions_WithEmptyPageId_ThrowsArgumentException()
    {
        var pages = new List<(Guid Id, string Name, IEnumerable<int> SlotPositions)>
        {
            (Guid.Empty, "Página 1", Enumerable.Range(1, 9)),
        };

        Assert.Throws<ArgumentException>(() => Profile.Reconstitute(Guid.NewGuid(), "Mi perfil", pages));
    }
}