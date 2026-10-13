using Chaos.Client.Networking.Definitions;
using Chaos.Client.Screens;
using FluentAssertions;

namespace Chaos.Client.Tests;

/// <summary>The login screen starts over when its connection drops, but not while a redirect is being followed.</summary>
public class LoginConnectionLossTests
{
    [Test]
    public void A_drop_with_no_redirect_is_a_lost_connection()
        => LobbyLoginScreen.IsLostConnection(ConnectionState.Disconnected, redirecting: false).Should().BeTrue();

    [Test]
    public void A_redirect_passing_through_disconnected_is_not()
    {
        LobbyLoginScreen.IsLostConnection(ConnectionState.Disconnected, redirecting: true).Should().BeFalse();
        LobbyLoginScreen.IsLostConnection(ConnectionState.Connecting, redirecting: false).Should().BeFalse();
        LobbyLoginScreen.IsLostConnection(ConnectionState.Login, redirecting: false).Should().BeFalse();
    }
}
