using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class MirrorMathTests
{
    private static readonly MirrorSegmentInfo North = new() { Id = "n", X = 10, Y = 5, Side = MirrorSide.North, Length = 3 };
    private static readonly MirrorSegmentInfo West = new() { Id = "w", X = 20, Y = 10, Side = MirrorSide.West, Length = 2 };

    [Test]
    public async Task Glass_pixels_match_the_shared_geometry()
    {
        //north column 3: base row 14
        MirrorGeometry.IsGlassPixel(MirrorSide.North, 3, 14 - 6).Should().BeTrue();
        MirrorGeometry.IsGlassPixel(MirrorSide.North, 3, 14 - 5).Should().BeFalse();
        MirrorGeometry.IsGlassPixel(MirrorSide.North, 2, 14 - 10).Should().BeFalse();
        //north column 24: base row 25
        MirrorGeometry.IsGlassPixel(MirrorSide.North, 24, 25 - 69).Should().BeTrue();
        MirrorGeometry.IsGlassPixel(MirrorSide.North, 24, 25 - 70).Should().BeFalse();
        MirrorGeometry.IsGlassPixel(MirrorSide.North, 25, 25 - 30).Should().BeFalse();
        //west column 3: base row 25; west column 24: base row 14
        MirrorGeometry.IsGlassPixel(MirrorSide.West, 3, 25 - 6).Should().BeTrue();
        MirrorGeometry.IsGlassPixel(MirrorSide.West, 24, 14 - 69).Should().BeTrue();
        MirrorGeometry.IsGlassPixel(MirrorSide.West, 24, 14 - 5).Should().BeFalse();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Reflections_flip_across_the_glass()
    {
        MirrorMath.ReflectPoint(North, new Vector2(11, 6)).Should().Be(new Vector2(11, 5));
        MirrorMath.ReflectPoint(North, new Vector2(11.5f, 8.25f)).Should().Be(new Vector2(11.5f, 2.75f));
        MirrorMath.ReflectPoint(West, new Vector2(23, 11)).Should().Be(new Vector2(18, 11));

        MirrorMath.ReflectFacing(MirrorSide.North, Direction.Up).Should().Be(Direction.Down);
        MirrorMath.ReflectFacing(MirrorSide.North, Direction.Left).Should().Be(Direction.Left);
        MirrorMath.ReflectFacing(MirrorSide.West, Direction.Right).Should().Be(Direction.Left);
        MirrorMath.ReflectFacing(MirrorSide.West, Direction.Down).Should().Be(Direction.Down);

        MirrorMath.FacingOutOf(MirrorSide.North).Should().Be(Direction.Down);
        MirrorMath.FacingOutOf(MirrorSide.West).Should().Be(Direction.Right);
        await Task.CompletedTask;
    }

    [Test]
    public async Task World_offsets_and_tile_centres()
    {
        MirrorMath.OffsetToTiles(new Vector2(28, 14)).Should().Be(new Vector2(1, 0));
        MirrorMath.OffsetToTiles(new Vector2(-28, 14)).Should().Be(new Vector2(0, 1));
        MirrorMath.OffsetToTiles(Vector2.Zero).Should().Be(Vector2.Zero);

        //Camera.TileToWorld(3, 2, 10) = ((10 - 1 + 3 - 2) * 28, (3 + 2) * 14) = (280, 70); plus half a tile
        MirrorMath.TileCenterWorld(new Vector2(3, 2), 10).Should().Be(new Vector2(308, 84));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Endless_window_and_funhouse_numbers()
    {
        MirrorMath.EndlessCopy(MirrorSide.North, new Vector2(11, 4), 2).Should().Be(new Vector2(11, 4 - 1.8f));
        MirrorMath.EndlessCopy(MirrorSide.West, new Vector2(18, 11), 1).Should().Be(new Vector2(18 - 0.9f, 11));
        MirrorMath.EndlessScale(0).Should().Be(1f);
        MirrorMath.EndlessScale(2).Should().BeApproximately(0.7396f, 0.0001f);
        MirrorMath.EndlessAlpha(1).Should().BeApproximately(0.372f, 0.0001f);
        MirrorMath.EndlessFacing(1, Direction.Up, Direction.Down).Should().Be(Direction.Up);
        MirrorMath.EndlessFacing(2, Direction.Up, Direction.Down).Should().Be(Direction.Down);

        // (15, 7) is one tile east and two south of partner (14, 5), so it shows the same offset from self (10, 5)
        var self = new MirrorSegmentInfo { Id = "a", X = 10, Y = 5, Side = MirrorSide.North, Length = 2 };
        var partner = new MirrorSegmentInfo { Id = "b", X = 14, Y = 5, Side = MirrorSide.North, Length = 2 };
        MirrorMath.WindowPoint(self, partner, new Vector2(15, 7)).Should().Be(new Vector2(11, 7));

        var westSelf = new MirrorSegmentInfo { Id = "c", X = 4, Y = 8, Side = MirrorSide.West, Length = 3 };
        var westPartner = new MirrorSegmentInfo { Id = "d", X = 4, Y = 14, Side = MirrorSide.West, Length = 3 };
        MirrorMath.WindowPoint(westSelf, westPartner, new Vector2(6, 16)).Should().Be(new Vector2(6, 10));

        MirrorMath.FunhouseScale(MirrorFunhouse.Tall).Should().Be((0.75f, 1.5f));
        MirrorMath.FunhouseScale(MirrorFunhouse.Wide).Should().Be((1.35f, 0.7f));
        MirrorMath.FunhouseScale(MirrorFunhouse.Wave).Should().Be((1f, 1f));
        MirrorMath.RippleOffset(0, 0).Should().Be(0f);
        Math.Abs(MirrorMath.RippleOffset(1.3, 40)).Should().BeLessThanOrEqualTo(3f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Haunted_slips_last_three_seconds_eight_to_twenty_apart()
    {
        const double START = 1_790_000_000;
        const double STEP = 0.05;
        var slips = new List<(double Start, double End, HauntedSlip Kind)>();
        double? openStart = null;
        var openKind = HauntedSlip.None;
        var seenCalm = false; //a slip already running at START would be measured short, so skip it

        for (var t = START; t < START + 2000; t += STEP)
        {
            (var kind, _) = MirrorMath.HauntedSlipAt("hau-n-12-30", t);

            if (kind == HauntedSlip.None)
                seenCalm = true;

            if ((kind != HauntedSlip.None) && openStart is null && seenCalm)
            {
                openStart = t;
                openKind = kind;
            } else if ((kind == HauntedSlip.None) && openStart is not null)
            {
                slips.Add((openStart.Value, t, openKind));
                openStart = null;
            }
        }

        slips.Count.Should().BeGreaterThan(80);

        foreach (var slip in slips)
            (slip.End - slip.Start).Should().BeApproximately(3, STEP * 2);

        for (var i = 1; i < slips.Count; i++)
            (slips[i].Start - slips[i - 1].End).Should().BeInRange(8 - STEP * 2, 20 + STEP * 2);

        slips.Select(s => s.Kind).Distinct().OrderBy(k => k).Should().BeEquivalentTo(new[] { HauntedSlip.Lag, HauntedSlip.Stare, HauntedSlip.Ghost });

        MirrorMath.HauntedSlipAt("hau-n-12-30", START + 123.4).Should().Be(MirrorMath.HauntedSlipAt("hau-n-12-30", START + 123.4));

        Enumerable.Range(0, 200)
                  .Count(i => MirrorMath.HauntedSlipAt("a", START + i).Kind != MirrorMath.HauntedSlipAt("b", START + i).Kind)
                  .Should()
                  .BeGreaterThan(0);

        MirrorMath.GhostAlpha(0).Should().BeApproximately(0.35f, 0.001f);
        MirrorMath.GhostAlpha(0.25).Should().BeApproximately(0.5f, 0.001f);
        MirrorMath.GhostAlpha(0.75).Should().BeApproximately(0.2f, 0.001f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Scare_slots_are_about_one_in_six_and_the_face_lasts_four_frames()
    {
        const double START = 1_790_000_000;

        MirrorMath.IsScareSlot("hau-n-12-30", START + 3).Should().Be(MirrorMath.IsScareSlot("hau-n-12-30", START + 3));
        MirrorMath.HauntedSlot("hau-n-12-30", START + 3).Should().Be(MirrorMath.HauntedSlot("hau-n-12-30", START + 3.5));

        var scares = Enumerable.Range(0, 600)
                               .Count(i => MirrorMath.IsScareSlot("hau-n-12-30", START + i * MirrorMath.HAUNTED_SLOT_SECONDS + 1));

        scares.Should().BeInRange(60, 140);

        Enumerable.Range(0, 4000)
                  .Select(i => START + i / 10.0)
                  .Any(t => MirrorMath.InScareWindow("hau-n-12-30", t))
                  .Should()
                  .BeTrue();

        MirrorMath.ScareFrame(0).Should().Be(0);
        MirrorMath.ScareFrame(0.1).Should().Be(1);
        MirrorMath.ScareFrame(0.31).Should().Be(3);
        MirrorMath.ScareFrame(0.4).Should().Be(-1);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Double_timeline()
    {
        MirrorMath.DoubleAt(0.3).Should().Be((DoublePhase.Climbing, 0.5f, 0.225f));
        MirrorMath.DoubleAt(3.6).Should().Be((DoublePhase.Following, 0.5f, 0.45f));
        MirrorMath.DoubleAt(7.1).Phase.Should().Be(DoublePhase.Fading);
        MirrorMath.DoubleAt(7.1).Alpha.Should().BeApproximately(0.225f, 0.001f);
        MirrorMath.DoubleAt(7.65).Phase.Should().Be(DoublePhase.Done);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Mirror_cache_ids()
    {
        var a = MirrorMath.MirrorCacheId(42, Direction.Up, false);
        var b = MirrorMath.MirrorCacheId(42, Direction.Down, false);
        var c = MirrorMath.MirrorCacheId(42, Direction.Up, true);

        (a & 0x8000_0000u).Should().NotBe(0u);
        new[] { a, b, c }.Distinct().Should().HaveCount(3);
        MirrorMath.MirrorCacheId(43, Direction.Up, false).Should().NotBe(a);
        await Task.CompletedTask;
    }

    [Test]
    public async Task A_face_is_on_screen_when_its_canvas_overlaps_the_viewport()
    {
        //the face canvas is 28 x 89 from its top-left origin
        MirrorMath.FaceOnScreen(new Vector2(100, 100), 640, 480).Should().BeTrue();
        MirrorMath.FaceOnScreen(new Vector2(-27, -88), 640, 480).Should().BeTrue();
        MirrorMath.FaceOnScreen(new Vector2(639, 479), 640, 480).Should().BeTrue();

        MirrorMath.FaceOnScreen(new Vector2(-28, 100), 640, 480).Should().BeFalse();
        MirrorMath.FaceOnScreen(new Vector2(100, -89), 640, 480).Should().BeFalse();
        MirrorMath.FaceOnScreen(new Vector2(640, 100), 640, 480).Should().BeFalse();
        MirrorMath.FaceOnScreen(new Vector2(100, 480), 640, 480).Should().BeFalse();
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_local_player_shows_only_in_the_mirrors_they_are_at()
    {
        //the maze's rows: mirror walls at y = 3 and y = 6, corridors at y = 4-5 and 7-8
        var upper = new MirrorSegmentInfo { Id = "u", X = 10, Y = 3, Side = MirrorSide.North, Length = 4 };
        var lower = new MirrorSegmentInfo { Id = "l", X = 10, Y = 6, Side = MirrorSide.North, Length = 4 };
        var window = new MirrorSegmentInfo { Id = "win", X = 10, Y = 6, Side = MirrorSide.North, Length = 4, Style = MirrorStyle.Window };
        var west = new MirrorSegmentInfo { Id = "w", X = 3, Y = 10, Side = MirrorSide.West, Length = 4 };
        bool IsWall(int x, int y) => (y == 6) || (x == 6);

        //y = 7 is in front of both runs, but the y = 6 wall hides the upper one
        MirrorMath.IsInFront(upper, 11, 7, MirrorMath.REFLECT_DEPTH, MirrorMath.REFLECT_MARGIN).Should().BeTrue();
        MirrorMath.ShowsLocalPlayer(upper, 11, 7, IsWall).Should().BeFalse();
        MirrorMath.ShowsLocalPlayer(lower, 11, 7, IsWall).Should().BeTrue();
        MirrorMath.ShowsLocalPlayer(upper, 11, 5, IsWall).Should().BeTrue();

        //west runs look along x the same way
        MirrorMath.ShowsLocalPlayer(west, 5, 11, IsWall).Should().BeTrue();
        MirrorMath.ShowsLocalPlayer(west, 7, 11, IsWall).Should().BeFalse();

        //a window shows the far side, never you, even standing right at it
        MirrorMath.ShowsLocalPlayer(window, 11, 7, IsWall).Should().BeFalse();
        await Task.CompletedTask;
    }
}
