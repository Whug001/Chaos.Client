using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using FluentAssertions;

namespace Chaos.Client.Tests;

/// <summary>
///     The stable window sends through one wrapper. While the battle window covers it, a click that was captured
///     before the battle opened (Watch pressed twice, a Yes held down) must not reach the server.
/// </summary>
public sealed class TamerStableSendGuardTests
{
    private static readonly TamerActionType[] Sent = Enum.GetValues<TamerActionType>().Where(type => type != TamerActionType.Close).ToArray();

    [Test]
    public void Every_stable_window_request_goes_while_the_battle_window_is_hidden()
    {
        foreach (var type in Enum.GetValues<TamerActionType>())
            TamerStableSendGuard.Allows(new TamerActionArgs { Type = type }, false).Should().BeTrue($"{type} with no battle window up");
    }

    [Test]
    public void Every_request_but_close_is_refused_under_the_battle_window()
    {
        Sent.Should().Contain([TamerActionType.RankedWatch, TamerActionType.RankedAttack, TamerActionType.RankedRevenge,
            TamerActionType.RankedSetDefence, TamerActionType.RankedRefresh, TamerActionType.RankedInfo]);

        foreach (var type in Sent)
            TamerStableSendGuard.Allows(new TamerActionArgs { Type = type }, true).Should().BeFalse($"{type} under the battle window");
    }

    [Test]
    public void Close_always_goes_so_the_server_never_keeps_a_window_the_client_hid()
    {
        TamerStableSendGuard.Allows(TamerStableState.CloseAction(), true).Should().BeTrue();
        TamerStableSendGuard.Allows(TamerStableState.CloseAction(), false).Should().BeTrue();
    }

    [Test]
    public void Nothing_is_sent_for_no_request()
    {
        TamerStableSendGuard.Allows(null, false).Should().BeFalse();
        TamerStableSendGuard.Allows(null, true).Should().BeFalse();
    }

    [Test]
    public void A_watch_clicked_twice_sends_once_when_the_battle_opens_between()
    {
        var sent = new List<TamerActionArgs>();
        var battleVisible = false;

        void Send(TamerActionArgs? action)
        {
            if (TamerStableSendGuard.Allows(action, battleVisible))
                sent.Add(action!);
        }

        var watch = TamerRankedState.WatchAction(Guid.NewGuid());

        Send(watch);
        battleVisible = true;
        Send(watch);

        sent.Should().ContainSingle().Which.Type.Should().Be(TamerActionType.RankedWatch);
    }
}
