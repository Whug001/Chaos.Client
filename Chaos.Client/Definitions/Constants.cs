#region
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Definitions;

internal static class Constants
{
    /// <summary>
    ///     Ice blue used for every damage-absorption barrier indicator — the HUD's barrier number and the barrier
    ///     sub-bar above an entity's overhead health bar. Kept clear of the whole yellow-orange-red-green arc on
    ///     purpose: the health bar below the sub-bar paints itself dark green, amber and crimson by HP tier, so a warm
    ///     barrier color collides with one of those tiers at two pixels tall. Blue is the nearest large gap in that hue
    ///     space and stays readable against the map behind the bar's unfilled remainder. Deliberately a raw RGB rather
    ///     than a <c>LegendColors</c> entry: legend colors are populated at startup from the game's color table, so
    ///     their exact value is data-driven and cannot be guaranteed to stay clear of the health bar's tiers.
    /// </summary>
    public static readonly Color BarrierColor = new(120, 200, 255);

    public const int BOARD_ROW_HEIGHT = 18;

    public static readonly (string ControlName, EquipmentSlot Slot)[] EquipmentSlotsByControlName =
    [
        ("WEAPON", EquipmentSlot.Weapon),
        ("ARMOR", EquipmentSlot.Armor),
        ("SHIELD", EquipmentSlot.Shield),
        ("HEAD", EquipmentSlot.Helmet),
        ("EAR", EquipmentSlot.Earrings),
        ("NECK", EquipmentSlot.Necklace),
        ("LHAND", EquipmentSlot.LeftRing),
        ("RHAND", EquipmentSlot.RightRing),
        ("LARM", EquipmentSlot.LeftGaunt),
        ("RARM", EquipmentSlot.RightGaunt),
        ("BELT", EquipmentSlot.Belt),
        ("LEG", EquipmentSlot.Greaves),
        ("FOOT", EquipmentSlot.Boots),
        ("CAPE", EquipmentSlot.Accessory1),
        ("ARMOR2", EquipmentSlot.Overcoat),
        ("HEAD2", EquipmentSlot.OverHelm),
        ("CAPE2", EquipmentSlot.Accessory2),
        ("CAPE3", EquipmentSlot.Accessory3)
    ];
}
