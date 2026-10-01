using Chaos.Client.Rendering;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class SpriteGridTests
{
    private static readonly Color Dark = new(36, 34, 58);

    [Test]
    public void ToPixels_MapsEachCharacter()
        => SpriteGrid.ToPixels(["Xo."], Dark)
                      .Should()
                      .Equal(Color.White, new Color(36, 34, 58, 255), Color.Transparent);

    [Test]
    public void ToPixels_LaysRowsOutTopToBottom()
        => SpriteGrid.ToPixels(["X.", ".X"], Dark)
                      .Should()
                      .Equal(Color.White, Color.Transparent, Color.Transparent, Color.White);

    [Test]
    public void ToPixels_NoRows_Throws()
    {
        var act = () => SpriteGrid.ToPixels([], Dark);

        act.Should()
           .Throw<ArgumentException>();
    }

    [Test]
    public void ToPixels_UnevenRows_Throws()
    {
        var act = () => SpriteGrid.ToPixels(["XX", "X"], Dark);

        act.Should()
           .Throw<ArgumentException>();
    }

    [Test]
    public void ToPixels_EmptyRow_Throws()
    {
        var act = () => SpriteGrid.ToPixels([""], Dark);

        act.Should()
           .Throw<ArgumentException>();
    }

    [Test]
    public void ToPixels_UnknownCharacter_Throws()
    {
        var act = () => SpriteGrid.ToPixels(["X#"], Dark);

        act.Should()
           .Throw<ArgumentException>();
    }

    [Test]
    public void BatFrames_AreThreeValidFrames19PixelsWide()
    {
        SpriteGrid.BatFrames
                   .Should()
                   .HaveCount(3);

        foreach (var frame in SpriteGrid.BatFrames)
        {
            frame.Should()
                 .OnlyContain(row => row.Length == 19);

            SpriteGrid.ToPixels(frame, Dark)
                       .Should()
                       .NotBeEmpty();
        }
    }

    [Test]
    public void GhostFrames_AreTwoValidFrames11By13()
    {
        SpriteGrid.GhostFrames
                   .Should()
                   .HaveCount(2);

        foreach (var frame in SpriteGrid.GhostFrames)
        {
            frame.Should()
                 .HaveCount(13)
                 .And
                 .OnlyContain(row => row.Length == 11);

            SpriteGrid.ToPixels(frame, Dark)
                       .Should()
                       .NotBeEmpty();
        }
    }
}
