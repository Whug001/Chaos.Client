using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class GuildCloakDesignStoreTests
{
    [Test]
    public void Id_zero_is_never_a_design()
    {
        var store = new GuildCloakDesignStore();
        store.Set(0, GuildCloakDesign.CreateDefault());

        store.TryGet(0, out _).Should().BeFalse();
    }

    [Test]
    public void A_server_design_is_asked_for_at_most_once_every_ten_seconds()
    {
        var store = new GuildCloakDesignStore();

        store.ShouldRequest(7, 1_000).Should().BeTrue();
        store.ShouldRequest(7, 5_000).Should().BeFalse();
        store.ShouldRequest(7, 11_000).Should().BeTrue();
        store.ShouldRequest(0, 1_000).Should().BeFalse();
        store.ShouldRequest(-3, 1_000).Should().BeFalse();

        store.Set(7, GuildCloakDesign.CreateDefault());

        store.ShouldRequest(7, 50_000).Should().BeFalse();
        store.TryGet(7, out var design).Should().BeTrue();
        design!.Colors.Should().HaveCount(1);
    }

    [Test]
    public void SetLocal_gives_a_new_negative_id_and_drops_the_one_it_replaces()
    {
        var store = new GuildCloakDesignStore();

        var first = store.SetLocal(0, GuildCloakDesign.CreateDefault());
        var second = store.SetLocal(first, GuildCloakDesign.CreateDefault());

        first.Should().Be(-1);
        second.Should().Be(-2);
        store.TryGet(first, out _).Should().BeFalse();
        store.TryGet(second, out _).Should().BeTrue();
    }


    [Test]
    public void Clear_forgets_designs_and_allows_a_new_request_at_once()
    {
        var store = new GuildCloakDesignStore();
        store.Set(7, GuildCloakDesign.CreateDefault());
        store.ShouldRequest(9, 1_000).Should().BeTrue();

        store.Clear();

        store.TryGet(7, out _).Should().BeFalse();
        store.ShouldRequest(9, 1_500).Should().BeTrue();
    }
}
