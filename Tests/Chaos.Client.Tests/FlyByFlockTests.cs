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

    private static FlyByFlock NewGeese(int seed) => new(FlyByStyle.GooseShadows, new Random(seed));

    //a goose flock just after its first V has spawned
    private static FlyByFlock GeeseWithAVeeUp(int seed)
    {
        var flock = NewGeese(seed);

        while (flock.GroupsSpawned == 0)
            Run(flock, STEP);

        return flock;
    }

    [Test]
    public void Vee_HasOneLeaderAndTwoArms()
    {
        var style = FlyByStyle.GooseShadows;

        //the placement jitter, plus the little each bird bobs in the frame it spawned on
        var tolerance = style.VeeJitter + 0.3f;

        for (var seed = 0; seed < 20; seed++)
        {
            var flock = GeeseWithAVeeUp(seed);
            var direction = flock.Flyers[0].Direction;
            var leader = flock.Flyers.MaxBy(f => f.Position.X * direction)!;
            var arms = new Dictionary<int, List<int>> { [-1] = [], [1] = [] };

            flock.Flyers
                 .Should()
                 .HaveCountGreaterThanOrEqualTo(7)
                 .And
                 .HaveCountLessThanOrEqualTo(11);

            foreach (var bird in flock.Flyers.Where(f => f != leader))
            {
                var behind = (leader.Position.X - bird.Position.X) * direction;
                var aside = bird.Position.Y - leader.Position.Y;
                var k = (int)MathF.Round(behind / style.VeeAlong);

                k.Should()
                 .BeGreaterThanOrEqualTo(1, $"seed {seed}: every other bird is behind the leader");

                behind.Should()
                      .BeApproximately(k * style.VeeAlong, tolerance, $"seed {seed}: bird {k} back along the flight line");

                MathF.Abs(aside)
                     .Should()
                     .BeApproximately(k * style.VeeAcross, tolerance, $"seed {seed}: bird {k} out to the side");

                arms[MathF.Sign(aside)].Add(k);
            }

            foreach (var arm in arms.Values)
                arm.Order()
                   .Should()
                   .Equal(Enumerable.Range(1, arm.Count), $"seed {seed}: an arm is one bird per step, with no gaps")
                   .And
                   .NotBeEmpty();
        }
    }

    [Test]
    public void Vee_EveryBirdLeavesWithinBound()
    {
        var style = FlyByStyle.GooseShadows;

        for (var seed = 0; seed < 20; seed++)
        {
            var flock = GeeseWithAVeeUp(seed);
            var bound = ((Viewport.X + flock.GroupDepth + style.EntryMargin + style.ExitMargin) / style.SpeedMin) + 2f;

            for (var t = 0f; t < bound; t += STEP)
            {
                var before = flock.Flyers.ToList();
                flock.Update(STEP, Viewport, Vector2.Zero, false);

                foreach (var removed in before.Except(flock.Flyers))
                    removed.Position
                           .X
                           .Should()
                           .NotBeInRange(0f, Viewport.X, $"seed {seed}: a bird still on screen stays");
            }

            flock.Flyers
                 .Should()
                 .BeEmpty($"seed {seed}: the whole V, tail included, is gone within {bound:F1} s");
        }
    }

    [Test]
    public void Vee_PushedBackByTheCamera_IsRemoved()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var flock = GeeseWithAVeeUp(seed);
            var direction = flock.Flyers[0].Direction;

            for (var t = 0f; (t < 60f) && (flock.Flyers.Count > 0); t += STEP)
                flock.Update(STEP, Viewport, new Vector2(-direction * 30f, 0f), false);

            flock.Flyers
                 .Should()
                 .BeEmpty($"seed {seed}: a V pushed back past its entry edge is gone for good");
        }
    }

    [Test]
    public void Vee_IsNotRemovedOnTheFrameItSpawns()
    {
        //with a short exit margin, only the V's own depth keeps its tail inside the entry limit
        var style = FlyByStyle.GooseShadows with { ExitMargin = 20f };

        for (var seed = 0; seed < 20; seed++)
        {
            var flock = new FlyByFlock(style, new Random(seed));

            while (flock.GroupsSpawned == 0)
                Run(flock, STEP);

            flock.Flyers
                 .Should()
                 .HaveCountGreaterThanOrEqualTo(7, $"seed {seed}: the tail of a long V starts inside the entry limit");
        }
    }

    [Test]
    public void Vee_ResetClearsFlyers()
    {
        var flock = GeeseWithAVeeUp(3);
        Run(flock, 5f);

        flock.Flyers
             .Should()
             .NotBeEmpty("the V is mid-crossing");

        flock.Reset();

        flock.Flyers
             .Should()
             .BeEmpty();
    }

    [Test]
    public void Scatter_HasNoGroupDepth()
    {
        var flock = NewFlock(3);

        while (flock.GroupsSpawned == 0)
            Run(flock, STEP);

        flock.GroupDepth
             .Should()
             .Be(0f);
    }
}
