namespace Chaos.Client.Models;

/// <summary>
///     One completed sale shown in the Market Logs tab: the item sold, who bought it, the quantity, the per-unit and
///     gross total price (gross = the seller's earnings), and when it sold in the player's local time.
/// </summary>
public readonly record struct MarketSaleLog(
    string ItemName,
    string BuyerName,
    int Quantity,
    int UnitPrice,
    int TotalPrice,
    DateTime SoldAtLocal);
