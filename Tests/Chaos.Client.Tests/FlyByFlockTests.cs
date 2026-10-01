using Chaos.Client.Rendering;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class FlyByFlockTests
{
    private const float STEP = 0.05f;
    private static readonly Vector2 Viewport = new(640f, 480f);

    private static FlyByFlock NewFlock(int seed) => new(FlyByStyle.Bats, new Random(seed));

    //advances the flock in STEP-sized frames and returns the times (s) at which a new group spawned
    private static List<float> Run(FlyByFlock flock, float seconds, bool spawning = true)
    {
        var spawnTimes = new List<float>();
        var groups = flock.GroupsSpawned;

        for (var t = STEP; t <= seconds + 0.0001f; t += STEP)
        {
            flock.Update(STEP, Viewport, Vector2.Zero, spawning);

            if (flock.GroupsSpawned != groups)
            {
                spawnTimes.Add(t);
                groups = flock.GroupsSpawned;
            }
        }

        return spawnTimes;
    }

    [Test]
    public void NoFlyers_BeforeTheFirstGroupDelay()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var flock = NewFlock(seed);

            Run(flock, FlyByStyle.Bats.FirstGroupMin - 0.1f);

            flock.Flyers
                 .Should()
                 .BeEmpty($"seed {seed}: the first group comes {FlyByStyle.Bats.FirstGroupMin}+ s after switching on");
        }
    }

    [Test]
    public void FirstGroup_IsFourToEightFlyersEnteringFromOffScreen()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var flock = NewFlock(seed);

            while (flock.GroupsSpawned == 0)
                Run(flock, STEP);

            flock.Flyers
                 .Should()
                 .HaveCountGreaterThanOrEqualTo(4)
                 .And
                 .HaveCountLessThanOrEqualTo(8);

            flock.Flyers
                 .Should()
                 .OnlyContain(f => (f.Position.X < 0f) || (f.Position.X > Viewport.X), $"seed {seed}: groups enter from off screen");
        }
    }

    [Test]
    public void Groups_ArriveWithinTheFirstDelayThenTheGap()
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var spawnTimes = Run(NewFlock(seed), 90f);

            spawnTimes.Should()
                      .HaveCountGreaterThanOrEqualTo(4, $"seed {seed}: 90 s holds at least four groups");

            spawnTimes[0]
                .Should()
                .BeInRange(FlyByStyle.Bats.FirstGroupMin - STEP, FlyByStyle.Bats.FirstGroupMax + STEP);

            for (var i = 1; i < spawnTimes.Count; i++)
                (spawnTimes[i] - spawnTimes[i - 1]).Should()
                                                   .BeInRange(
                                                       FlyByStyle.Bats.GapMin - STEP,
                                                       FlyByStyle.Bats.GapMax + STEP,
                                                       $"seed {seed}: gap before group {i}");
        }
    }

    [Test]
    public void SpawningOff_NoNewGroup_AndFlyersLeave()
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var flock = NewFlock(seed);

            while (flock.GroupsSpawned == 0)
                Run(flock, STEP);

            var groups = flock.GroupsSpawned;

            Run(flock, 30f, spawning: false);

            flock.GroupsSpawned
                 .Should()
                 .Be(groups, $"seed {seed}: no group spawns while spawning is off");

            flock.Flyers
                 .Should()
                 .BeEmpty($"seed {seed}: every flyer crosses and is removed within 30 s");
        }
    }

    [Test]
    public void CameraShift_MovesEveryFlyerByTheShift()
    {
        var still = NewFlock(7);
        var panned = NewFlock(7);

        while (still.GroupsSpawned == 0)
        {
            still.Update(STEP, Viewport, Vector2.Zero, true);
            panned.Update(STEP, Viewport, Vector2.Zero, true);
        }

        var shift = new Vector2(10f, -6f);
        still.Update(STEP, Viewport, Vector2.Zero, true);
        panned.Update(STEP, Viewport, shift, true);

        panned.Flyers
              .Should()
              .HaveCount(still.Flyers.Count);

        for (var i = 0; i < still.Flyers.Count; i++)
        {
            (panned.Flyers[i].Position.X - still.Flyers[i].Position.X).Should()
                                                                       .BeApproximately(shift.X, 0.001f);

            (panned.Flyers[i].Position.Y - still.Flyers[i].Position.Y).Should()
                                                                       .BeApproximately(shift.Y, 0.001f);
        }
    }

    [Test]
    public void CameraJumpBiggerThanTheViewport_IsIgnored()
    {
        var still = NewFlock(11);
        var teleported = NewFlock(11);

        while (still.GroupsSpawned == 0)
        {
            still.Update(STEP, Viewport, Vector2.Zero, true);
            teleported.Update(STEP, Viewport, Vector2.Zero, true);
        }

        still.Update(STEP, Viewport, Vector2.Zero, true);
        teleported.Update(STEP, Viewport, new Vector2(5000f, 0f), true);

        teleported.Flyers
                  .Select(f => f.Position)
                  .Should()
                  .Equal(still.Flyers.Select(f => f.Position));
    }

    [Test]
    public void Reset_RemovesEveryFlyer()
    {
        var flock = NewFlock(3);

        while (flock.GroupsSpawned == 0)
            Run(flock, STEP);

        flock.Reset();

        flock.Flyers
             .Should()
             .BeEmpty();
    }

    [Test]
    public void FrameOf_IsAlwaysAValidFrame()
    {
        var flock = NewFlock(5);

        for (var i = 0; i < 400; i++)
        {
            flock.Update(STEP, Viewport, Vector2.Zero, true);

            foreach (var flyer in flock.Flyers)
                flock.FrameOf(flyer)
                     .Should()
                     .BeInRange(0, FlyByStyle.Bats.Frames.Count - 1);
        }
    }

    [Test]
    public void FlyerPushedBackPastItsEntryEdge_IsRemoved()
    {
        var flock = NewFlock(7);

        while (flock.GroupsSpawned == 0)
            Run(flock, STEP);

        var dir = flock.Flyers[0].Direction;

        for (var i = 0; (i < 60) && (flock.Flyers.Count > 0); i++)
            flock.Update(STEP, Viewport, new Vector2(-dir * 200f, 0f), false);

        flock.Flyers.Should().BeEmpty("a flyer pushed back past its entry edge is gone for good");
    }
}
