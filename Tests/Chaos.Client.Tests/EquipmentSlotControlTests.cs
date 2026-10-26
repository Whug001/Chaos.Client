#region
using Chaos.Client.Definitions;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public sealed class EquipmentSlotControlTests
{
    [Test]
    public void WeaponSkinSlot_HasTheWeapon2Control()
        => Constants.EquipmentSlotsByControlName.Should().Contain(("WEAPON2", EquipmentSlot.WeaponSkin));

    [Test]
    public void NoControlOrSlot_IsListedTwice()
    {
        Constants.EquipmentSlotsByControlName.Select(e => e.ControlName).Should().OnlyHaveUniqueItems();
        Constants.EquipmentSlotsByControlName.Select(e => e.Slot).Should().OnlyHaveUniqueItems();
    }
}
