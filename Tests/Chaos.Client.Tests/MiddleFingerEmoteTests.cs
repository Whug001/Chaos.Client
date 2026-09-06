using Chaos.Client.Rendering;
using SkiaSharp;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class MiddleFingerEmoteTests
{
    [Test]
    public async Task The_body_animation_is_relayed_by_the_server_and_collides_with_nothing()
    {
        //Chaos-Server WorldServer.OnEmote relays 1..44 untouched and drops everything else
        MiddleFingerEmote.BODY_ANIMATION.Should().BeInRange(1, 44);

        //18, 19 and 20 are the gaps in BodyAnimation; taking a named value would break that emote
        Enum.IsDefined(typeof(BodyAnimation), (byte)MiddleFingerEmote.BODY_ANIMATION)
            .Should()
            .BeFalse();

        MiddleFingerEmote.BODY_ANIMATION.Should()
                         .NotBe(SunglassesEmote.BODY_ANIMATION);

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_wheel_icon_sentinel_collides_with_nothing()
    {
        MiddleFingerEmote.PREVIEW_FRAME.Should()
                         .NotBe(SunglassesEmote.PREVIEW_FRAME);

        //emot01 has 50 frames; both sentinels have to sit past the end of it
        MiddleFingerEmote.PREVIEW_FRAME.Should().BeGreaterThan(50);

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_bubble_crop_stops_before_the_head()
    {
        //the aisling head occupies composite rows 24-38. Drawing past row 23 would paint the emote
        //frame's own face over the character's real one, hiding hair and hats.
        MiddleFingerEmote.BUBBLE_BOTTOM_Y.Should().Be(23);

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_raised_finger_survives_the_fold()
    {
        for (var x = MiddleFingerEmote.KEEP_LEFT_X; x <= MiddleFingerEmote.KEEP_RIGHT_X; x++)
        for (var y = 1; y < MiddleFingerEmote.FOLD_ROW; y++)
            MiddleFingerEmote.ShouldFoldAway(x, y)
                             .Should()
                             .BeFalse($"composite {x},{y} is the middle finger");

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_other_fingers_are_folded_away_above_the_knuckle_line()
    {
        foreach (var x in new[] { MiddleFingerEmote.KEEP_LEFT_X - 1, MiddleFingerEmote.KEEP_RIGHT_X + 1, 60, 70 })
        for (var y = 1; y < MiddleFingerEmote.FOLD_ROW; y++)
            MiddleFingerEmote.ShouldFoldAway(x, y)
                             .Should()
                             .BeTrue($"composite {x},{y} is a finger that should fold");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Nothing_below_the_knuckle_line_is_touched()
    {
        //rows from FOLD_ROW down are the knuckles, thumb and palm — the whole point is to keep them
        for (var x = 54; x <= 76; x++)
        for (var y = MiddleFingerEmote.FOLD_ROW; y <= MiddleFingerEmote.BUBBLE_BOTTOM_Y; y++)
            MiddleFingerEmote.ShouldFoldAway(x, y)
                             .Should()
                             .BeFalse($"composite {x},{y} is below the knuckle line");

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_keep_window_is_a_sane_finger_width()
    {
        var width = MiddleFingerEmote.KEEP_RIGHT_X - MiddleFingerEmote.KEEP_LEFT_X + 1;

        width.Should().BeInRange(3, 5);

        await Task.CompletedTask;
    }

    [Test]
    public async Task It_holds_as_long_as_the_other_gesture_bubbles()
    {
        //Rock On, Peace and Stop are single frames held for the default emote duration
        MiddleFingerEmote.DURATION_MS.Should().Be(1500f);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Flipping_mirrors_the_bubble_about_the_same_pivot_as_the_sprite()
    {
        const int WIDTH = 34;

        var unflipped = MiddleFingerEmote.ResolveLeftX(false, WIDTH);
        var flipped = MiddleFingerEmote.ResolveLeftX(true, WIDTH);

        unflipped.Should()
                 .Be(AislingRenderer.LAYER_OFFSET_PADDING);

        //the flipped left edge is the mirror of the unflipped right edge
        flipped.Should()
               .Be(AislingRenderer.MirrorX(unflipped + WIDTH - 1));

        await Task.CompletedTask;
    }

    #region TryFold
    private const int OFFSET = AislingRenderer.LAYER_OFFSET_PADDING;

    private static readonly SKColor BubbleOutline = new(0xCB, 0xCB, 0xEF);
    private static readonly SKColor BubbleFill = new(0xFF, 0xFF, 0xFF);
    private static readonly SKColor Skin = new(0xCB, 0x83, 0x4B);

    /// <summary>
    ///     A stand-in for the Stop frame: bubble outline on the bubble-only rows, bubble fill behind, and skin across
    ///     the finger rows so the fold has something to erase.
    /// </summary>
    private static SKBitmap BuildFakeFrame(int width = 49, int height = 24)
    {
        var bitmap = new SKBitmap(width, height);

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            bitmap.SetPixel(x, y, BubbleFill);

        //bubble-only rows carry the outline colour, which the fold must learn to leave alone
        foreach (var y in new[] { 1, 21, 22, 23 })
        {
            if (y >= height)
                continue;

            for (var x = 0; x < width; x++)
                bitmap.SetPixel(x, y, BubbleOutline);
        }

        //hand pixels across every column of the finger rows
        for (var y = 2; (y < MiddleFingerEmote.FOLD_ROW) && (y < height); y++)
        for (var x = 0; x < width; x++)
            bitmap.SetPixel(x, y, Skin);

        //the fill sample point has to read as bubble interior
        var fillX = MiddleFingerEmote.FILL_SAMPLE_X - OFFSET;

        if ((fillX < width) && (MiddleFingerEmote.FILL_SAMPLE_Y < height))
            bitmap.SetPixel(fillX, MiddleFingerEmote.FILL_SAMPLE_Y, BubbleFill);

        return bitmap;
    }

    [Test]
    public async Task The_fold_keeps_the_finger_and_erases_the_other_fingers()
    {
        using var bitmap = BuildFakeFrame();

        MiddleFingerEmote.TryFold(bitmap, OFFSET)
                         .Should()
                         .BeTrue();

        for (var y = 2; y < MiddleFingerEmote.FOLD_ROW; y++)
        {
            for (var x = MiddleFingerEmote.KEEP_LEFT_X; x <= MiddleFingerEmote.KEEP_RIGHT_X; x++)
                bitmap.GetPixel(x - OFFSET, y)
                      .Should()
                      .Be(Skin, $"the finger at composite {x},{y} must survive");

            bitmap.GetPixel(MiddleFingerEmote.KEEP_LEFT_X - 1 - OFFSET, y)
                  .Should()
                  .Be(BubbleFill);

            bitmap.GetPixel(MiddleFingerEmote.KEEP_RIGHT_X + 1 - OFFSET, y)
                  .Should()
                  .Be(BubbleFill);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_fold_leaves_the_bubbles_own_outline_alone()
    {
        using var bitmap = BuildFakeFrame();
        MiddleFingerEmote.TryFold(bitmap, OFFSET);

        //row 1 sits inside the fold rectangle but is bubble, not hand. Painting it out would punch a hole in
        //the top of the bubble — the exact bug this guards.
        for (var x = 0; x < bitmap.Width; x++)
            bitmap.GetPixel(x, 1)
                  .Should()
                  .Be(BubbleOutline);

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_fold_leaves_everything_below_the_knuckle_line_alone()
    {
        using var before = BuildFakeFrame();
        using var after = BuildFakeFrame();
        MiddleFingerEmote.TryFold(after, OFFSET);

        for (var y = MiddleFingerEmote.FOLD_ROW; y < before.Height; y++)
        for (var x = 0; x < before.Width; x++)
            after.GetPixel(x, y)
                 .Should()
                 .Be(before.GetPixel(x, y), $"row {y} is below the knuckle line");

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_frame_too_short_to_fold_is_refused_rather_than_mangled()
    {
        using var bitmap = BuildFakeFrame(height: MiddleFingerEmote.FOLD_ROW);

        MiddleFingerEmote.TryFold(bitmap, OFFSET)
                         .Should()
                         .BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_frame_too_narrow_to_hold_the_finger_is_refused()
    {
        using var bitmap = BuildFakeFrame(MiddleFingerEmote.KEEP_RIGHT_X - OFFSET);

        MiddleFingerEmote.TryFold(bitmap, OFFSET)
                         .Should()
                         .BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_frame_whose_fill_sample_is_transparent_is_refused()
    {
        using var bitmap = BuildFakeFrame();
        bitmap.SetPixel(MiddleFingerEmote.FILL_SAMPLE_X - OFFSET, MiddleFingerEmote.FILL_SAMPLE_Y, SKColors.Transparent);

        MiddleFingerEmote.TryFold(bitmap, OFFSET)
                         .Should()
                         .BeFalse();

        await Task.CompletedTask;
    }
    #endregion

    [Test]
    public async Task The_shared_mirror_helper_is_the_one_the_sunglasses_use()
    {
        //one implementation, so the off-by-one that hit the sunglasses cannot come back in a second copy
        for (var x = 0; x < 111; x++)
            SunglassesEmote.MirrorX(x)
                           .Should()
                           .Be(AislingRenderer.MirrorX(x));

        await Task.CompletedTask;
    }
}
