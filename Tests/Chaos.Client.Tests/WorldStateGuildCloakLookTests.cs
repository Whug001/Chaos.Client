using Chaos.Client.Collections;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

/// <summary>
///     <see cref="WorldState.ApplyGuildCloakLook" /> and <see cref="WorldState.GuildCloakLooks" /> are the only guard
///     that a guild cloak look survives later <see cref="WorldState.AddOrUpdateAisling" /> updates, in either arrival
///     order. <see cref="WorldState" /> is static, so tests reset it before and after to avoid cross-test pollution.
/// </summary>
[NotInParallel]
public class WorldStateGuildCloakLookTests
{
    [Before(Test)]
    public void Setup() => WorldState.Clear();

    [After(Test)]
    public void Teardown() => WorldState.Clear();

    private static DisplayAislingArgs Args(uint id, string name = "Test")
        => new()
        {
            Id = id,
            Name = name
        };

    [Test]
    public void A_look_that_arrives_before_the_display_sets_the_new_aislings_appearance()
    {
        WorldState.ApplyGuildCloakLook(5, 42);

        WorldState.AddOrUpdateAisling(Args(5));

        WorldState.GetEntity(5)!.Appearance!.Value.GuildCloakDesignId.Should().Be(42);
    }

    [Test]
    public void A_look_that_arrives_after_the_display_patches_the_existing_aisling()
    {
        WorldState.AddOrUpdateAisling(Args(5));

        WorldState.ApplyGuildCloakLook(5, 42);

        WorldState.GetEntity(5)!.Appearance!.Value.GuildCloakDesignId.Should().Be(42);
    }

    [Test]
    public void A_later_display_update_keeps_the_look_id()
    {
        WorldState.ApplyGuildCloakLook(5, 42);
        WorldState.AddOrUpdateAisling(Args(5));

        WorldState.AddOrUpdateAisling(Args(5, "Renamed"));

        WorldState.GetEntity(5)!.Appearance!.Value.GuildCloakDesignId.Should().Be(42);
    }

    [Test]
    public void RemoveEntity_forgets_the_look()
    {
        WorldState.ApplyGuildCloakLook(5, 42);
        WorldState.AddOrUpdateAisling(Args(5));

        WorldState.RemoveEntity(5);
        WorldState.AddOrUpdateAisling(Args(5));

        WorldState.GetEntity(5)!.Appearance!.Value.GuildCloakDesignId.Should().Be(0);
    }

    [Test]
    public void Clear_forgets_the_look()
    {
        WorldState.ApplyGuildCloakLook(5, 42);

        WorldState.Clear();
        WorldState.AddOrUpdateAisling(Args(5));

        WorldState.GetEntity(5)!.Appearance!.Value.GuildCloakDesignId.Should().Be(0);
    }
}
