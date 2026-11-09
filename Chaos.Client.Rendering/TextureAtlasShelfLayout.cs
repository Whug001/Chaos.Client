namespace Chaos.Client.Rendering;

/// <summary>
///     Pure shelf-packing layout for <see cref="TextureAtlas" />. Computes where each entry goes without touching the
///     GPU, so the layout can be unit tested. A one-pixel transparent gap separates entries horizontally and shelves
///     vertically, so a sprite edge landing on a screen pixel centre never samples a neighbouring entry.
/// </summary>
public static class TextureAtlasShelfLayout
{
    /// <summary>
    ///     Transparent pixels kept between shelf-packed entries, horizontally and between shelves.
    /// </summary>
    public const int SHELF_GAP = 1;

    /// <summary>
    ///     Entries larger than this in either dimension are skipped during shelf packing.
    /// </summary>
    public const int MAX_SHELF_ENTRY_SIZE = 512;

    /// <summary>
    ///     Packs entries into pages. Entries are sorted by height descending, entries larger than
    ///     <paramref name="maxEntrySize" /> are skipped, and pages flush with the same logic as before: a page holds
    ///     every entry placed before an entry overflows it. Gaps sit only between entries, so an entry that exactly
    ///     fills the remaining width or height of a page still fits, and reported page sizes never include a trailing
    ///     gap.
    /// </summary>
    public static List<ShelfPage> Pack(
        IReadOnlyList<(int Width, int Height)> entries,
        int maxPageSize,
        int maxEntrySize = MAX_SHELF_ENTRY_SIZE)
    {
        //sort indices by height descending for better shelf packing; sorting indices (rather than filtering first)
        //keeps the exact order TextureAtlas.BuildShelf produced
        var order = new List<int>(entries.Count);

        for (var i = 0; i < entries.Count; i++)
            order.Add(i);

        order.Sort((a, b) => entries[b].Height.CompareTo(entries[a].Height));

        var pages = new List<ShelfPage>();
        var placements = new List<ShelfPlacement>();
        var currentX = 0;
        var currentY = 0;
        var shelfHeight = 0;
        var maxWidthUsed = 0;

        void FlushPage()
        {
            if (placements.Count == 0)
                return;

            //page pixels are zeroed on flush, so the trailing gap stays transparent without writing anything
            pages.Add(new ShelfPage(maxWidthUsed, currentY + shelfHeight, [.. placements]));
            placements.Clear();
        }

        foreach (var index in order)
        {
            var (width, height) = entries[index];

            //skip entries too large for atlas packing
            if ((width > maxEntrySize) || (height > maxEntrySize))
                continue;

            //would it start a new shelf? the gap lives between entries, so an entry that exactly fills the
            //remaining width still fits
            var startsNewShelf = (currentX + width) > maxPageSize;
            var shelfTop = currentY;

            if (startsNewShelf)
            {
                shelfTop = currentY + shelfHeight;

                if (shelfHeight > 0)
                    shelfTop += SHELF_GAP;
            }

            //does it fit on the current page? an entry that exactly fills the remaining height still fits
            if ((shelfTop + height) > maxPageSize)
            {
                //flush current page and start a new one
                FlushPage();

                currentX = 0;
                currentY = 0;
                shelfHeight = 0;
                maxWidthUsed = 0;
                shelfTop = 0;
                startsNewShelf = false;
            }

            if (startsNewShelf)
            {
                currentX = 0;
                currentY = shelfTop;
                shelfHeight = 0;
            }

            placements.Add(new ShelfPlacement(index, currentX, currentY, width, height));
            shelfHeight = Math.Max(shelfHeight, height);

            //measure the page before adding the gap, so no trailing gap is counted in the page size
            maxWidthUsed = Math.Max(maxWidthUsed, currentX + width);
            currentX += width + SHELF_GAP;
        }

        //flush remaining entries
        FlushPage();

        return pages;
    }

    /// <summary>
    ///     A placed entry within a shelf page. <see cref="EntryIndex" /> is the index into the input list.
    /// </summary>
    public readonly record struct ShelfPlacement(int EntryIndex, int X, int Y, int Width, int Height);

    /// <summary>
    ///     One flushed atlas page: its pixel size and the entries placed on it.
    /// </summary>
    public sealed record ShelfPage(int Width, int Height, IReadOnlyList<ShelfPlacement> Placements);
}
