#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Models;

/// <summary>
///     A seller-side market listing shown on the Sell tab. Unlike the read-only <c>MarketListing</c> (Results tab) this is
///     mutable client state: <see cref="Quantity" /> shrinks as units are delisted/sold and <see cref="UnitPrice" /> is set
///     after the item is dragged in (null while it is an unpriced draft — not yet buyable). Local/placeholder until the
///     market backend is wired; a real listing carries a server-assigned <see cref="ListingId" />.
/// </summary>
public sealed class MarketSellListing
{
    public required ulong ListingId { get; init; }
    public required ushort Sprite { get; init; }
    public required DisplayColor Color { get; init; }
    public required string Name { get; init; }

    /// <summary>How many units this listing holds; shrinks when the seller delists some or a buyer purchases some.</summary>
    public required int Quantity { get; set; }

    /// <summary>The per-unit asking price, or null while the listing is an unpriced draft (not yet buyable).</summary>
    public int? UnitPrice { get; set; }

    public bool IsPriced => UnitPrice.HasValue;
}
