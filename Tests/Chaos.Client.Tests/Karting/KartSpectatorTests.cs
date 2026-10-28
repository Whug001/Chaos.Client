#region
using Chaos.Client.Systems.Karting;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests.Karting;

public class KartSpectatorTests
{
    [Test]
    public void No_candidates_means_no_target()
    {
        var spectator = new KartSpectator();

        spectator.Resolve([], 2).Should().BeNull();
        spectator.Cycle([], 2, 1).Should().BeNull();
    }

    [Test]
    public void A_finished_racer_starts_on_their_own_kart()
        => new KartSpectator().Resolve([4, 2, 7], 2).Should().Be((byte)2);

    [Test]
    public void An_onlooker_starts_on_the_leader()
        => new KartSpectator().Resolve([4, 2, 7], null).Should().Be((byte)4);

    [Test]
    public void The_target_is_kept_while_it_is_a_candidate_and_replaced_when_it_is_not()
    {
        var spectator = new KartSpectator();
        spectator.Resolve([4, 2, 7], null);
        spectator.Cycle([4, 2, 7], null, 1).Should().Be((byte)2);

        spectator.Resolve([2, 4, 7], null).Should().Be((byte)2);
        spectator.Resolve([4, 7], null).Should().Be((byte)4);
    }

    [Test]
    public void Cycling_wraps_at_both_ends()
    {
        var spectator = new KartSpectator();
        spectator.Resolve([4, 2, 7], null);

        spectator.Cycle([4, 2, 7], null, -1).Should().Be((byte)7);
        spectator.Cycle([4, 2, 7], null, 1).Should().Be((byte)4);
        spectator.Target.Should().Be((byte)4);
    }
}
