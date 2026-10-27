using Chaos.Client.Systems;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class SmoothWalkOffsetTests
{
    [Test]
    [Arguments(28f, 14f)]
    [Arguments(-28f, 14f)]
    [Arguments(28f, -14f)]
    [Arguments(-28f, -14f)]
    public void StartsAtTheFullOffsetAndEndsAtZero(float startX, float startY)
    {
        var start = new Vector2(startX, startY);

        AnimationSystem.GetSmoothWalkOffset(start, 0f, true).Should().Be(start);
        AnimationSystem.GetSmoothWalkOffset(start, 1f, true).Should().Be(Vector2.Zero);
    }

    [Test]
    [Arguments(28f, 14f)]
    [Arguments(-28f, 14f)]
    [Arguments(28f, -14f)]
    [Arguments(-28f, -14f)]
    public void NeverMovesBackward(float startX, float startY)
    {
        var start = new Vector2(startX, startY);
        var previous = start;

        for (var i = 1; i <= 1000; i++)
        {
            var offset = AnimationSystem.GetSmoothWalkOffset(start, i / 1000f, true);

            Math.Abs(offset.X).Should().BeLessThanOrEqualTo(Math.Abs(previous.X));
            Math.Abs(offset.Y).Should().BeLessThanOrEqualTo(Math.Abs(previous.Y));
            previous = offset;
        }
    }

    [Test]
    [Arguments(28f, 14f)]
    [Arguments(-28f, -14f)]
    public void SnappedDiagonalsStayOnTheTwoToOneLine(float startX, float startY)
    {
        var start = new Vector2(startX, startY);

        for (var i = 0; i <= 1000; i++)
        {
            var offset = AnimationSystem.GetSmoothWalkOffset(start, i / 1000f, true);
            var onLine = offset.X * startY / startX;

            Math.Abs(offset.Y - onLine).Should().BeLessThanOrEqualTo(1f);
        }
    }

    [Test]
    public void ASidewaysStepHasTwentyEightMoves()
    {
        var start = new Vector2(28, 14);
        var xs = new HashSet<float>();

        for (var i = 0; i <= 1000; i++)
            xs.Add(AnimationSystem.GetSmoothWalkOffset(start, i / 1000f, true).X);

        xs.Should().HaveCount(29);
    }

    [Test]
    [Arguments(0f)]
    [Arguments(0.25f)]
    [Arguments(0.6f)]
    [Arguments(1f)]
    public void UnsnappedIsTheExactRemainder(float progress)
    {
        var start = new Vector2(-28, 14);

        AnimationSystem.GetSmoothWalkOffset(start, progress, false).Should().Be(start * (1f - progress));
    }

    [Test]
    public void ZeroStartOffsetReturnsZero()
        => AnimationSystem.GetSmoothWalkOffset(Vector2.Zero, 0.5f, true).Should().Be(Vector2.Zero);
}
