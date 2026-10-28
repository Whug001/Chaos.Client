using Chaos.Client.Systems.KeyBinds;
using FluentAssertions;

namespace Chaos.Client.Tests.Karting;

public class KartActionNameTests
{
    [Test]
    public void Kart_actions_have_friendly_names()
    {
        GameActions.NameOf(GameAction.KartAccelerate).Should().Be("Accelerate");
        GameActions.NameOf(GameAction.KartBrake).Should().Be("Brake");
        GameActions.NameOf(GameAction.KartSteerLeft).Should().Be("Steer Left");
        GameActions.NameOf(GameAction.KartSteerRight).Should().Be("Steer Right");
        GameActions.NameOf(GameAction.KartDrift).Should().Be("Drift");
        GameActions.NameOf(GameAction.KartUseItem).Should().Be("Use Item");
    }
}
