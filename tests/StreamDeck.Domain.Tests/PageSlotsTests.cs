using StreamDeck.Domain.Entities;

namespace StreamDeck.Domain.Tests;

public class PageSlotsTests
{
    [Fact]
    public void Create_NewPage_HasExactlyNineSlots()
    {
        var profile = Profile.Create("Mi perfil");

        Assert.Equal(9, profile.Pages[0].Slots.Count);
    }

    [Fact]
    public void Create_NewPage_SlotsHavePositionsOneToNineInOrder()
    {
        var profile = Profile.Create("Mi perfil");
        var slots = profile.Pages[0].Slots;

        for (var i = 0; i < 9; i++)
        {
            Assert.Equal(i + 1, slots[i].Position);
        }
    }

    [Fact]
    public void Create_NewPage_SlotPositionsAreUnique()
    {
        var profile = Profile.Create("Mi perfil");
        var slots = profile.Pages[0].Slots;

        var distinctPositions = slots.Select(s => s.Position).Distinct().Count();

        Assert.Equal(9, distinctPositions);
    }

    [Fact]
    public void AddPage_NewPage_HasExactlyNineSlots()
    {
        var profile = Profile.Create("Mi perfil");

        var newPage = profile.AddPage("Página 2");

        Assert.Equal(9, newPage.Slots.Count);
    }

    [Fact]
    public void Slot_RowAndColumn_MatchExpectedGrid()
    {
        var profile = Profile.Create("Mi perfil");
        var slots = profile.Pages[0].Slots;

        Assert.Equal((1, 1), (slots[0].Row, slots[0].Column));
        Assert.Equal((1, 2), (slots[1].Row, slots[1].Column));
        Assert.Equal((1, 3), (slots[2].Row, slots[2].Column));
        Assert.Equal((2, 1), (slots[3].Row, slots[3].Column));
        Assert.Equal((2, 2), (slots[4].Row, slots[4].Column));
        Assert.Equal((2, 3), (slots[5].Row, slots[5].Column));
        Assert.Equal((3, 1), (slots[6].Row, slots[6].Column));
        Assert.Equal((3, 2), (slots[7].Row, slots[7].Column));
        Assert.Equal((3, 3), (slots[8].Row, slots[8].Column));
    }

    [Fact]
    public void Reconstitute_WithExactlyNineValidPositions_Succeeds()
    {
        var page = Page.Reconstitute(Guid.NewGuid(), "Página 1", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });

        Assert.Equal(9, page.Slots.Count);
    }

    [Fact]
    public void Reconstitute_PreservesPositionsRegardlessOfInputOrder()
    {
        var scrambled = new[] { 9, 1, 8, 2, 7, 3, 6, 4, 5 };

        var page = Page.Reconstitute(Guid.NewGuid(), "Página 1", scrambled);

        for (var i = 0; i < 9; i++)
        {
            Assert.Equal(i + 1, page.Slots[i].Position);
        }
    }

    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(0)]
    public void Reconstitute_WithWrongSlotCount_ThrowsArgumentException(int count)
    {
        var positions = Enumerable.Range(1, count).ToArray();

        Assert.Throws<ArgumentException>(
            () => Page.Reconstitute(Guid.NewGuid(), "Página 1", positions));
    }

    [Fact]
    public void Reconstitute_WithDuplicatePositions_ThrowsArgumentException()
    {
        var positions = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 8 };

        Assert.Throws<ArgumentException>(
            () => Page.Reconstitute(Guid.NewGuid(), "Página 1", positions));
    }

    [Fact]
    public void Reconstitute_WithOutOfRangePosition_ThrowsArgumentException()
    {
        var positions = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 10 };

        Assert.Throws<ArgumentException>(
            () => Page.Reconstitute(Guid.NewGuid(), "Página 1", positions));
    }

    [Fact]
    public void Reconstitute_WithZeroPosition_ThrowsArgumentException()
    {
        var positions = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };

        Assert.Throws<ArgumentException>(
            () => Page.Reconstitute(Guid.NewGuid(), "Página 1", positions));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reconstitute_WithInvalidName_ThrowsArgumentException(string invalidName)
    {
        Assert.Throws<ArgumentException>(
            () => Page.Reconstitute(Guid.NewGuid(), invalidName, Enumerable.Range(1, 9)));
    }

    [Fact]
    public void Reconstitute_TrimsName()
    {
        var page = Page.Reconstitute(Guid.NewGuid(), "   Página 1   ", Enumerable.Range(1, 9));

        Assert.Equal("Página 1", page.Name);
    }

    [Fact]
    public void Reconstitute_PreservesId()
    {
        var id = Guid.NewGuid();

        var page = Page.Reconstitute(id, "Página 1", Enumerable.Range(1, 9));

        Assert.Equal(id, page.Id);
    }

    [Fact]
    public void Reconstitute_WithNullPositions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => Page.Reconstitute(Guid.NewGuid(), "Página 1", null!));
    }
    [Fact]
    public void Reconstitute_WithEmptyId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => Page.Reconstitute(Guid.Empty, "Página 1", Enumerable.Range(1, 9)));
    }
}