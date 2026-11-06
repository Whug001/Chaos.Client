#region
using Chaos.Client.Collections;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

[NotInParallel]
public class EntityLookTests
{
    [Before(Test)]
    public void Reset() => WorldState.Clear();

    private static EntityLookArgs Look(uint id, byte scale, bool floating)
        => new() { EntityId = id, ScalePercent = scale, Floating = floating };

    [Test]
    public void Non_default_look_is_stored()
    {
        WorldState.ApplyEntityLook(Look(7, 200, false));
        WorldState.EntityLooks[7].ScalePercent.Should().Be(200);
    }

    [Test]
    public void Default_look_removes_entry()
    {
        WorldState.ApplyEntityLook(Look(7, 100, true));
        WorldState.ApplyEntityLook(Look(7, 100, false));
        WorldState.EntityLooks.ContainsKey(7).Should().BeFalse();
    }

    [Test]
    public void Clear_drops_looks()
    {
        WorldState.ApplyEntityLook(Look(7, 200, true));
        WorldState.Clear();
        WorldState.EntityLooks.Should().BeEmpty();
    }

    [Test]
    public void RemoveEntity_drops_look()
    {
        WorldState.ApplyEntityLook(Look(7, 200, true));
        WorldState.RemoveEntity(7);
        WorldState.EntityLooks.ContainsKey(7).Should().BeFalse();
    }

    [Test]
    public void Float_raise_stays_in_range()
    {
        EntityLookMath.FloatRaise(0).Should().BeApproximately(12f, 0.001f);
        EntityLookMath.FloatRaise(2).Should().BeApproximately(12f, 0.001f);

        for (var t = 0.0; t < 4; t += 0.05)
            EntityLookMath.FloatRaise(t).Should().BeInRange(9f, 15f);
    }
}
