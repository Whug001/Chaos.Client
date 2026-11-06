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
    public void Reset()
    {
        WorldState.Clear();
        WorldState.PlayerEntityId = 0;
    }

    private static EntityLookArgs Look(uint id, byte scale, bool floating)
        => new() { EntityId = id, ScalePercent = scale, Floating = floating };

    private static void AddEntity(uint id)
        => WorldState.AddOrUpdateAisling(new DisplayAislingArgs { Id = id, Name = "Test" });

    [Test]
    public void Non_default_look_is_stored()
    {
        AddEntity(7);
        WorldState.ApplyEntityLook(Look(7, 200, false));
        WorldState.EntityLooks[7].ScalePercent.Should().Be(200);
    }

    [Test]
    public void Default_look_removes_entry()
    {
        AddEntity(7);
        WorldState.ApplyEntityLook(Look(7, 100, true));
        WorldState.ApplyEntityLook(Look(7, 100, false));
        WorldState.EntityLooks.ContainsKey(7).Should().BeFalse();
    }

    [Test]
    public void Clear_drops_looks()
    {
        AddEntity(7);
        WorldState.ApplyEntityLook(Look(7, 200, true));
        WorldState.Clear();
        WorldState.EntityLooks.Should().BeEmpty();
    }

    [Test]
    public void RemoveEntity_drops_look()
    {
        AddEntity(7);
        WorldState.ApplyEntityLook(Look(7, 200, true));
        WorldState.RemoveEntity(7);
        WorldState.EntityLooks.ContainsKey(7).Should().BeFalse();
    }

    [Test]
    public void Look_for_unknown_id_is_not_stored()
    {
        WorldState.ApplyEntityLook(Look(9, 200, false));
        WorldState.EntityLooks.ContainsKey(9).Should().BeFalse();
    }

    [Test]
    public void Look_for_known_id_is_stored()
    {
        AddEntity(11);
        WorldState.ApplyEntityLook(Look(11, 200, true));
        WorldState.EntityLooks[11].ScalePercent.Should().Be(200);
    }

    [Test]
    public void Look_for_local_player_counts_as_known()
    {
        WorldState.PlayerEntityId = 13;
        WorldState.ApplyEntityLook(Look(13, 200, false));
        WorldState.EntityLooks.ContainsKey(13).Should().BeTrue();
    }

    [Test]
    public void Default_look_for_unknown_id_stays_absent()
    {
        WorldState.ApplyEntityLook(Look(9, 100, false));
        WorldState.EntityLooks.ContainsKey(9).Should().BeFalse();
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
