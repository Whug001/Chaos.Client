using System.Reflection;
using Chaos.Client.Controls.Components;
using Chaos.Client.Screens;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.TurkeyShoot;

public sealed class TurkeyShootWindowTests
{
    private static WorldScreen ScreenWithRoot()
    {
        var screen = new WorldScreen();
        typeof(WorldScreen).GetProperty(nameof(WorldScreen.Root))!.SetValue(screen, new UIPanel());

        return screen;
    }

    private static void Handle(WorldScreen screen, TurkeyShootDisplayArgs args)
        => typeof(WorldScreen).GetMethod("HandleTurkeyShootDisplay", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(screen, [args]);

    private static object? Window(WorldScreen screen)
        => typeof(WorldScreen).GetField("TurkeyShootWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(screen);

    [Test]
    public async Task Close_without_a_window_creates_nothing()
    {
        var screen = ScreenWithRoot();

        Handle(screen, new TurkeyShootDisplayArgs { Type = TurkeyShootDisplayType.Close, Reason = TurkeyShootCloseReason.Left });

        Window(screen).Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Round_messages_without_a_window_create_nothing()
    {
        var screen = ScreenWithRoot();

        Handle(screen, new TurkeyShootDisplayArgs { Type = TurkeyShootDisplayType.FlockStart, FlockIndex = 0, Seed = 7, HuntersAtStart = 2, StartsInMs = 1000 });
        Handle(screen, new TurkeyShootDisplayArgs { Type = TurkeyShootDisplayType.Results, WinnerSlots = [0], CountsAsWin = true });

        Window(screen).Should().BeNull();

        await Task.CompletedTask;
    }
}
