using System.Reflection;
using Chaos.Client.Controls.World.Hud.Panel;
using Chaos.Client.Data;
using Chaos.Client.Rendering;
using DALib.Drawing;
using SkiaSharp;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class ChatPanelWrapTests
{
    [Before(Class)]
    public static void SetUpPalette()
    {
        var palette = new Palette(Enumerable.Repeat(SKColors.White, 256));

        typeof(LegendPalette).GetField("Palette", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, palette);
    }

    private static int Width(int chars) => TextRenderer.MeasureCharWidth('a') * chars;

    [Test]
    public async Task Continuation_rows_carry_the_colour_code()
    {
        var rows = ChatPanel.WrapRows("{=q[!lfg] Name: aaaa bbbb cccc dddd eeee", Color.Yellow, 1, null, Width(14));

        rows.Should()
            .HaveCountGreaterThan(2);

        rows[0]
            .Text
            .Should()
            .StartWith("{=q");

        rows.Skip(1)
            .Should()
            .OnlyContain(r => r.Text.StartsWith("{=q"));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Later_colour_code_replaces_the_carried_one()
    {
        var rows = ChatPanel.WrapRows("{=qaaaa bbbb {=bcccc dddd eeee ffff", Color.Yellow, 1, null, Width(10));

        rows[^1]
            .Text
            .Should()
            .StartWith("{=b");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Narrow_width_with_colour_code_terminates()
    {
        var rows = ChatPanel.WrapRows("{=qabcdefghij", Color.Yellow, 1, null, Width(2));

        rows.Should()
            .NotBeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Uncoloured_text_wraps_unchanged()
    {
        var rows = ChatPanel.WrapRows("aaaa bbbb cccc", Color.Yellow, 1, null, Width(9));

        rows.Select(r => r.Text)
            .Should()
            .Equal("aaaa bbbb", "cccc");

        await Task.CompletedTask;
    }
}
