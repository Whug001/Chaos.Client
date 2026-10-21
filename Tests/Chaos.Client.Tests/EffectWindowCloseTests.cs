using System.Reflection;
using Chaos.Client.Controls.Components;
using Chaos.Client.Screens;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

//The server sends Close when the player dies with the window open. Like the mount window,
//Close (and Update) with no window must be ignored: only Open builds the window.
public sealed class EffectWindowCloseTests
{
    private static WorldScreen ScreenWithRoot()
    {
        var screen = new WorldScreen();
        typeof(WorldScreen).GetProperty(nameof(WorldScreen.Root))!.SetValue(screen, new UIPanel());

        return screen;
    }

    private static void Handle(WorldScreen screen, EffectWindowDisplayArgs args)
        => typeof(WorldScreen).GetMethod("HandleEffectWindowDisplay", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(screen, [args]);

    private static object? EffectWindow(WorldScreen screen)
        => typeof(WorldScreen).GetField("EffectWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(screen);

    [Test]
    public async Task Close_without_a_window_creates_nothing()
    {
        var screen = ScreenWithRoot();

        Handle(screen, new EffectWindowDisplayArgs { Type = EffectWindowDisplayType.Close });

        EffectWindow(screen).Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Update_without_a_window_creates_nothing()
    {
        var screen = ScreenWithRoot();

        Handle(
            screen,
            new EffectWindowDisplayArgs
            {
                Type = EffectWindowDisplayType.Update,
                CurrentLogin = 1,
                CurrentDeath = 2
            });

        EffectWindow(screen).Should().BeNull();

        await Task.CompletedTask;
    }
}
