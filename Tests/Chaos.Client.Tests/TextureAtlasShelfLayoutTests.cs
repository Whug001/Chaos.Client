using Chaos.Client.Rendering;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class TextureAtlasShelfLayoutTests
{
    private static bool Touches(
        TextureAtlasShelfLayout.ShelfPlacement a,
        TextureAtlasShelfLayout.ShelfPlacement b)
        => (a.X + a.Width >= b.X)
        && (b.X + b.Width >= a.X)
        && (a.Y + a.Height >= b.Y)
        && (b.Y + b.Height >= a.Y);

    [Test]
    public void TwoEntriesOnAShelf_HaveOneEmptyColumnBetweenThem()
    {
        var pages = TextureAtlasShelfLayout.Pack([(20, 16), (20, 16)], 64);

        var page = pages.Should().ContainSingle().Subject;
        var ordered = page.Placements.OrderBy(p => p.X).ToArray();

        ordered.Should().HaveCount(2);
        ordered[0].X.Should().Be(0);
        ordered[0].Y.Should().Be(0);
        ordered[1].Y.Should().Be(0);
        ordered[1].X.Should().Be(ordered[0].X + ordered[0].Width + TextureAtlasShelfLayout.SHELF_GAP);

        //placements keep the exact entry size, so the gap column stays transparent
        foreach (var placement in ordered)
            (placement.Width, placement.Height).Should().Be((20, 16));
    }

    [Test]
    public void TwoShelves_HaveOneEmptyRowBetweenThem()
    {
        var pages = TextureAtlasShelfLayout.Pack([(64, 20), (64, 20)], 64);

        var page = pages.Should().ContainSingle().Subject;
        var ordered = page.Placements.OrderBy(p => p.Y).ToArray();

        ordered.Should().HaveCount(2);
        ordered[0].Should().Be(new TextureAtlasShelfLayout.ShelfPlacement(ordered[0].EntryIndex, 0, 0, 64, 20));
        ordered[1].X.Should().Be(0);
        ordered[1].Y.Should().Be(ordered[0].Y + ordered[0].Height + TextureAtlasShelfLayout.SHELF_GAP);

        //bottom of the second shelf, with no trailing gap counted
        page.Height.Should().Be(ordered[1].Y + ordered[1].Height);
    }

    [Test]
    public void MixedSizes_NoTwoPlacedRectanglesTouch()
    {
        var entries = new (int Width, int Height)[]
        {
            (20, 24),
            (16, 20),
            (12, 18),
            (30, 10),
            (10, 8),
            (24, 8),
            (8, 6),
            (40, 6)
        };

        var pages = TextureAtlasShelfLayout.Pack(entries, 64);

        pages.Should().NotBeEmpty();

        var all = pages.SelectMany(p => p.Placements).ToArray();

        //every entry placed exactly once with its exact size
        all.Should().HaveCount(entries.Length);
        all.Select(p => p.EntryIndex).Should().BeEquivalentTo(Enumerable.Range(0, entries.Length));

        foreach (var placement in all)
            (placement.Width, placement.Height).Should().Be(entries[placement.EntryIndex]);

        //no two rectangles on the same page share even an edge
        foreach (var page in pages)
        {
            var placements = page.Placements.ToArray();

            for (var i = 0; i < placements.Length; i++)
                for (var j = i + 1; j < placements.Length; j++)
                    Touches(placements[i], placements[j])
                        .Should()
                        .BeFalse(
                            $"placements {placements[i]} and {placements[j]} on the same page must have a gap between them");
        }
    }

    [Test]
    public void EntryExactlyFillingPageWidth_StaysOnCurrentShelf()
    {
        var pages = TextureAtlasShelfLayout.Pack([(20, 12), (43, 10)], 64);

        var page = pages.Should().ContainSingle().Subject;
        page.Placements.Should().HaveCount(2);

        var first = page.Placements.Single(p => p.EntryIndex == 0);
        var second = page.Placements.Single(p => p.EntryIndex == 1);

        first.X.Should().Be(0);
        first.Y.Should().Be(0);
        second.Y.Should().Be(first.Y);
        second.X.Should().Be(21);

        //the entry exactly fills the remaining width, including the gap before it
        (second.X + second.Width).Should().Be(64);
        page.Width.Should().Be(64);
    }

    [Test]
    public void PageSize_NeverExceedsMax_IncludingExactFills()
    {
        var scenarios = new[]
        {
            (Entries: new (int Width, int Height)[] { (64, 16) }, MaxPageSize: 64),
            (Entries: new (int Width, int Height)[] { (32, 32) }, MaxPageSize: 32),
            (Entries: new (int Width, int Height)[] { (32, 16), (32, 16) }, MaxPageSize: 32),
            (Entries: new (int Width, int Height)[] { (16, 32), (16, 32) }, MaxPageSize: 32),
            (Entries: new (int Width, int Height)[] { (20, 12), (43, 10) }, MaxPageSize: 64),
            (Entries: new (int Width, int Height)[] { (20, 24), (16, 20), (12, 18), (30, 10), (10, 8) }, MaxPageSize: 64)
        };

        foreach (var (entries, maxPageSize) in scenarios)
        {
            var pages = TextureAtlasShelfLayout.Pack(entries, maxPageSize);

            pages.Should()
                 .NotBeEmpty($"entries {string.Join(", ", entries.Select(e => $"{e.Width}x{e.Height}"))} must produce pages");

            foreach (var page in pages)
            {
                page.Width.Should().BeLessThanOrEqualTo(maxPageSize);
                page.Height.Should().BeLessThanOrEqualTo(maxPageSize);
                page.Width.Should().BePositive();
                page.Height.Should().BePositive();
            }
        }
    }

    [Test]
    public void OversizedEntries_AreSkipped()
    {
        var pages = TextureAtlasShelfLayout.Pack([(600, 10), (10, 600), (20, 20)], 64);

        var page = pages.Should().ContainSingle().Subject;
        var placement = page.Placements.Should().ContainSingle().Subject;

        placement.EntryIndex.Should().Be(2);
        (placement.X, placement.Y, placement.Width, placement.Height).Should().Be((0, 0, 20, 20));
        (page.Width, page.Height).Should().Be((20, 20));
    }

    [Test]
    public void OversizedEntries_AreSkippedAgainstCustomLimit()
    {
        var pages = TextureAtlasShelfLayout.Pack([(30, 10), (10, 10)], 64, maxEntrySize: 16);

        var page = pages.Should().ContainSingle().Subject;
        var placement = page.Placements.Should().ContainSingle().Subject;

        placement.EntryIndex.Should().Be(1);
    }
}
