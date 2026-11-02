using Chaos.Client.Rendering.CustomEmotes;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class EmoteBatchTwoTests
{
    private static readonly PixelArtEmote[] Emotes =
    [
        ZzzCloudEmote.Instance,
        MoneyEyesEmote.Instance,
        CrownEmote.Instance,
        SkullPuffEmote.Instance
    ];

    private static IEnumerable<PixelLayer> AllLayers(PixelArtEmote emote)
    {
        for (var t = 0f; t < emote.DurationMs; t += 5f)
            foreach (var layer in emote.Compose(t, 0))
                yield return layer;
    }

    #region shared
    [Test]
    public async Task Bytes_and_icon_codes_are_the_agreed_ones()
    {
        Emotes.Select(e => e.BodyAnimation).Should().Equal(7, 8, 45, 46);
        Emotes.Select(e => e.PreviewFrame).Should().Equal(1007, 1008, 1009, 1010);
        Emotes.Select(e => e.DurationMs).Should().Equal(2000f, 1500f, 1800f, 2000f);

        foreach (var emote in Emotes)
            Enum.IsDefined(typeof(BodyAnimation), (byte)emote.BodyAnimation).Should().BeFalse(emote.Name);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Each_emote_is_gone_at_its_end_and_opaque_on_its_icon()
    {
        foreach (var emote in Emotes)
        {
            emote.Compose(emote.DurationMs, 0).Should().BeEmpty(emote.Name);
            emote.Compose(-1f, 0).Should().BeEmpty(emote.Name);

            var icon = emote.IconLayers();
            icon.Should().NotBeEmpty(emote.Name);
            icon.Should().OnlyContain(l => l.Opacity == 1f, emote.Name);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task No_colour_is_near_white_so_the_icon_crop_keeps_it()
    {
        foreach (var emote in Emotes)
        foreach (var art in AllLayers(emote).Select(l => l.Art).Distinct())
        foreach (var color in art.Colors())
            (color.R >= 220 && color.G >= 220 && color.B >= 220)
                .Should()
                .BeFalse($"{art.Key} paints {color}, which the icon crop would drop");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Art_keys_are_unique_across_every_custom_emote()
    {
        var byKey = new Dictionary<string, PixelArt>();

        foreach (var emote in CustomEmoteRegistry.All.OfType<PixelArtEmote>())
        foreach (var art in AllLayers(emote).Select(l => l.Art).Distinct())
        {
            if (byKey.TryGetValue(art.Key, out var existing))
                existing.Should().BeSameAs(art, $"key '{art.Key}' names two different pieces of art");

            byKey[art.Key] = art;
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Every_layer_follows_the_head_bob()
    {
        foreach (var emote in Emotes)
            for (var t = 0f; t < emote.DurationMs; t += 10f)
            {
                var still = emote.Compose(t, 0);
                var bobbed = emote.Compose(t, 1);

                bobbed.Select(l => l.Y).Should().Equal(still.Select(l => l.Y + 1), $"{emote.Name} at {t}ms");
                bobbed.Select(l => l.X).Should().Equal(still.Select(l => l.X), $"{emote.Name} at {t}ms");
                bobbed.Select(l => l.Opacity).Should().Equal(still.Select(l => l.Opacity), $"{emote.Name} at {t}ms");
            }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Opacity_never_leaves_zero_to_one()
    {
        foreach (var emote in Emotes)
            AllLayers(emote).Should().OnlyContain(l => l.Opacity >= 0f && l.Opacity <= 1f, emote.Name);

        await Task.CompletedTask;
    }
    #endregion

    #region zzz cloud
    [Test]
    public async Task The_cloud_grows_then_holds_then_fades()
    {
        ZzzCloudEmote.CloudStage(0f).Should().Be(0);
        ZzzCloudEmote.CloudStage(ZzzCloudEmote.GROW_STAGE_MS).Should().Be(1);
        ZzzCloudEmote.CloudStage(ZzzCloudEmote.FULL_MS).Should().Be(2);
        ZzzCloudEmote.CloudStage(ZzzCloudEmote.TOTAL_MS - 1f).Should().Be(2);

        ZzzCloudEmote.CloudOpacity(ZzzCloudEmote.FADE_OUT_MS - 1f).Should().Be(1f);
        ZzzCloudEmote.CloudOpacity(ZzzCloudEmote.FADE_OUT_MS + 150f).Should().BeApproximately(0.5f, 0.001f);
        ZzzCloudEmote.CloudOpacity(ZzzCloudEmote.TOTAL_MS).Should().Be(0f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Three_zs_come_out_one_at_a_time()
    {
        ZzzCloudEmote.Zs(299f).Should().BeEmpty();
        ZzzCloudEmote.Zs(300f).Should().ContainSingle();
        ZzzCloudEmote.Zs(800f).Should().HaveCount(2);
        ZzzCloudEmote.Zs(1950f).Should().BeEmpty();

        var births = 0;

        static bool OneAtOrigin(float t)
            => ZzzCloudEmote.Zs(t).Any(z => (z.CenterX == ZzzCloudEmote.Z_ORIGIN_X) && (z.CenterY == ZzzCloudEmote.Z_ORIGIN_Y));

        for (var t = 1f; t < ZzzCloudEmote.TOTAL_MS; t += 1f)
            if (OneAtOrigin(t) && !OneAtOrigin(t - 1f))
                births++;

        births.Should().Be(3);
        await Task.CompletedTask;
    }

    [Test]
    public async Task A_z_rises_and_drifts_away_from_the_head_then_grows_and_fades()
    {
        var previous = ZzzCloudEmote.Zs(300f).Single();
        var grew = false;

        for (var t = 305f; t < 300f + ZzzCloudEmote.Z_LIFE_MS; t += 5f)
        {
            var z = ZzzCloudEmote.Zs(t).First();

            z.CenterY.Should().BeLessThanOrEqualTo(previous.CenterY);
            z.CenterX.Should().BeGreaterThanOrEqualTo(previous.CenterX);
            z.Opacity.Should().BeLessThanOrEqualTo(previous.Opacity);

            if (previous.IsBig)
                z.IsBig.Should().BeTrue();

            grew |= z.IsBig;
            previous = z;
        }

        grew.Should().BeTrue();
        previous.CenterY.Should().Be(ZzzCloudEmote.Z_ORIGIN_Y - ZzzCloudEmote.Z_RISE_ROWS);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_cloud_stays_clear_of_the_face()
    {
        foreach (var layer in AllLayers(ZzzCloudEmote.Instance))
            (layer.Y + layer.Art.Height - 1).Should().BeLessThan(CustomEmoteGeometry.HEAD_TOP_Y, layer.Art.Key);

        await Task.CompletedTask;
    }
    #endregion

    #region money eyes
    [Test]
    public async Task A_spin_runs_face_side_edge_side()
    {
        var views = new List<int>();

        for (var t = 0f; t < MoneyEyesEmote.SPIN_MS; t += 1f)
        {
            var view = MoneyEyesEmote.View(t);

            if ((views.Count == 0) || (views[^1] != view))
                views.Add(view);
        }

        views.Should().Equal(0, 1, 2, 1);
        MoneyEyesEmote.View(MoneyEyesEmote.SPIN_MS).Should().Be(0);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Every_view_keeps_both_coins_centred_on_their_eyes()
    {
        foreach (var pixels in new[] { MoneyEyesEmote.FacePixels, MoneyEyesEmote.SidePixels, MoneyEyesEmote.EdgePixels })
        {
            MeanOpaqueColumn(pixels, 0, 4).Should().BeApproximately(2.0, 0.25);
            MeanOpaqueColumn(pixels, 6, 10).Should().BeApproximately(8.0, 0.25);
            pixels.Should().OnlyContain(row => row[5] == '.');
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_coins_sit_where_the_heart_eyes_do()
    {
        MoneyEyesEmote.LEFT_X.Should().Be(HeartEyesEmote.LEFT_X);
        MoneyEyesEmote.FacePixels[0].Length.Should().Be(HeartEyesEmote.BigPixels[0].Length);
        await Task.CompletedTask;
    }

    private static double MeanOpaqueColumn(IReadOnlyList<string> rows, int fromColumn, int toColumn)
    {
        var columns = new List<int>();

        foreach (var row in rows)
            for (var x = fromColumn; x <= toColumn; x++)
                if (row[x] != '.')
                    columns.Add(x);

        return columns.Average();
    }
    #endregion

    #region crown
    [Test]
    public async Task The_crown_falls_onto_the_head()
    {
        CrownEmote.RowOffset(0f).Should().Be(CrownEmote.START_ROW_OFFSET);
        CrownEmote.RowOffset(CrownEmote.DROP_MS).Should().Be(0);

        var previous = int.MinValue;

        for (var t = 0f; t <= CrownEmote.DROP_MS; t += 5f)
        {
            var offset = CrownEmote.RowOffset(t);
            offset.Should().BeGreaterThanOrEqualTo(previous);
            previous = offset;
        }

        var landed = CrownEmote.Instance.Compose(CrownEmote.DROP_MS, 0).Single();
        (landed.Y + landed.Art.Height - 1).Should().BeGreaterThanOrEqualTo(CustomEmoteGeometry.HEAD_TOP_Y);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_glint_sweeps_the_band_once_left_to_right()
    {
        CrownEmote.GlintColumn(CrownEmote.GLINT_START_MS - 1f).Should().Be(-1);
        CrownEmote.GlintColumn(CrownEmote.GLINT_END_MS).Should().Be(-1);

        var visited = new List<int>();

        for (var t = CrownEmote.GLINT_START_MS; t < CrownEmote.GLINT_END_MS; t += 1f)
        {
            var column = CrownEmote.GlintColumn(t);

            if ((visited.Count == 0) || (visited[^1] != column))
                visited.Add(column);
        }

        visited.Should().Equal(Enumerable.Range(0, CrownEmote.CrownPixels[0].Length));

        var layers = CrownEmote.Instance.Compose(CrownEmote.GLINT_START_MS, 0);
        layers.Should().HaveCount(2);
        layers[1].Y.Should().Be(layers[0].Y + CrownEmote.BAND_ROW);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_red_gem_is_in_the_middle_of_the_band()
    {
        var band = CrownEmote.CrownPixels[CrownEmote.BAND_ROW];

        band.IndexOf('r').Should().Be(band.Length / 2);
        await Task.CompletedTask;
    }
    #endregion

    #region skull puff
    [Test]
    public async Task Smoke_gathers_upward_into_the_skull()
    {
        var previous = SkullPuffEmote.Gathering(0f);

        previous.Stage.Should().Be(0);
        previous.CenterY.Should().Be(SkullPuffEmote.GATHER_START_Y);

        for (var t = 5f; t < SkullPuffEmote.GATHER_MS; t += 5f)
        {
            var now = SkullPuffEmote.Gathering(t);

            now.Stage.Should().BeGreaterThanOrEqualTo(previous.Stage);
            now.CenterY.Should().BeLessThanOrEqualTo(previous.CenterY);
            previous = now;
        }

        previous.Stage.Should().Be(2);
        SkullPuffEmote.IsSkull(SkullPuffEmote.GATHER_MS - 1f).Should().BeFalse();
        SkullPuffEmote.IsSkull(SkullPuffEmote.GATHER_MS).Should().BeTrue();
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_jaw_chatters_three_times_while_the_skull_shows()
    {
        var openings = 0;

        for (var t = 1f; t < SkullPuffEmote.TOTAL_MS; t += 1f)
            if (!SkullPuffEmote.IsJawOpen(t - 1f) && SkullPuffEmote.IsJawOpen(t))
            {
                SkullPuffEmote.IsSkull(t).Should().BeTrue();
                openings++;
            }

        openings.Should().Be(3);
        SkullPuffEmote.IsJawOpen(SkullPuffEmote.Instance.IconTimeMs).Should().BeFalse();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Open_and_shut_skulls_share_the_same_cranium()
    {
        SkullPuffEmote.SkullOpenPixels.Count.Should().Be(SkullPuffEmote.SkullClosedPixels.Count);
        SkullPuffEmote.SkullOpenPixels.Take(6).Should().Equal(SkullPuffEmote.SkullClosedPixels.Take(6));
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_skull_breaks_into_three_puffs_that_spread_and_fade()
    {
        var atBreak = SkullPuffEmote.Instance.Compose(SkullPuffEmote.BREAK_MS, 0);
        var late = SkullPuffEmote.Instance.Compose(SkullPuffEmote.TOTAL_MS - 1f, 0);

        atBreak.Should().HaveCount(3);
        late.Should().HaveCount(3);

        (late.Max(l => l.X) - late.Min(l => l.X)).Should().BeGreaterThan(atBreak.Max(l => l.X) - atBreak.Min(l => l.X));
        late.Should().OnlyContain(l => l.Opacity < 0.05f);
        late.Select(l => l.Y).Average().Should().BeLessThan(atBreak.Select(l => l.Y).Average());
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_skull_sits_above_the_head_and_centred_on_it()
    {
        var skull = SkullPuffEmote.Instance.Compose(SkullPuffEmote.Instance.IconTimeMs, 0)[1];

        (skull.Y + skull.Art.Height - 1).Should().BeLessThan(CustomEmoteGeometry.HEAD_TOP_Y);
        (skull.X + skull.Art.Width / 2).Should().Be(SkullPuffEmote.CENTER_X);
        await Task.CompletedTask;
    }
    #endregion

    #region potion flask
    [Test]
    public async Task The_flask_rides_on_byte_47_and_ends_when_its_player_walks()
    {
        var flask = PotionFlaskEmote.Instance;

        flask.BodyAnimation.Should().Be(47);
        flask.PreviewFrame.Should().Be(1011);
        flask.DurationMs.Should().Be(3000f);
        flask.EndsOnWalk.Should().BeTrue();
        Emotes.Should().OnlyContain(e => !e.EndsOnWalk);
        Enum.IsDefined(typeof(BodyAnimation), (byte)47).Should().BeFalse();
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_flask_scene_is_gone_at_its_end_and_its_icon_is_the_opaque_flask_alone()
    {
        var flask = PotionFlaskEmote.Instance;

        flask.Compose(PotionFlaskEmote.TOTAL_MS, 0).Should().BeEmpty();
        flask.Compose(-1f, 0).Should().BeEmpty();

        var icon = flask.IconLayers();
        icon.Should().ContainSingle();
        icon[0].Opacity.Should().Be(1f);
        icon[0].X.Should().BeGreaterThan(CustomEmoteGeometry.HEAD_RIGHT_X);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_flask_scene_has_no_near_white_and_stays_inside_zero_to_one_opacity()
    {
        foreach (var layer in AllLayers(PotionFlaskEmote.Instance).Concat(PotionFlaskEmote.Instance.IconLayers()))
        {
            layer.Opacity.Should().BeInRange(0f, 1f, layer.Art.Key);

            foreach (var color in layer.Art.Colors())
                (color.R >= 220 && color.G >= 220 && color.B >= 220).Should().BeFalse($"{layer.Art.Key} paints {color}");
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_scene_stands_on_the_ground_and_ignores_the_head_bob()
    {
        for (var t = 0f; t < PotionFlaskEmote.TOTAL_MS; t += 10f)
        {
            var still = PotionFlaskEmote.Instance.Compose(t, 0);
            var bobbed = PotionFlaskEmote.Instance.Compose(t, 1);

            bobbed.Select(l => (l.X, l.Y)).Should().Equal(still.Select(l => (l.X, l.Y)), $"at {t}ms");
            still.Should().OnlyContain(l => l.Y + l.Art.Height - 1 <= PotionFlaskEmote.GROUND_Y, $"at {t}ms");
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_wall_fades_in_shakes_until_the_burst_then_fades_out()
    {
        PotionFlaskEmote.WallOpacity(0f).Should().Be(0f);
        PotionFlaskEmote.WallOpacity(PotionFlaskEmote.WALL_IN_MS).Should().Be(1f);
        PotionFlaskEmote.WallOpacity(PotionFlaskEmote.FADE_OUT_MS - 1f).Should().Be(1f);
        PotionFlaskEmote.WallOpacity(PotionFlaskEmote.TOTAL_MS).Should().Be(0f);

        PotionFlaskEmote.ShakeColumns(PotionFlaskEmote.CRACK_MS - 1f).Should().Be(0);
        PotionFlaskEmote.ShakeColumns(PotionFlaskEmote.CRACK_MS).Should().Be(1);
        PotionFlaskEmote.ShakeColumns(PotionFlaskEmote.CRACK_MS + PotionFlaskEmote.SHAKE_MS).Should().Be(-1);
        PotionFlaskEmote.ShakeColumns(PotionFlaskEmote.BURST_MS).Should().Be(0);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Cracks_show_only_before_the_burst_and_the_wall_breaks_at_it()
    {
        var keysBefore = PotionFlaskEmote.Instance.Compose(PotionFlaskEmote.BURST_MS - 1f, 0).Select(l => l.Art.Key).ToList();
        var keysAfter = PotionFlaskEmote.Instance.Compose(PotionFlaskEmote.BURST_MS, 0).Select(l => l.Art.Key).ToList();

        keysBefore.Should().Contain("potion-flask:wall").And.Contain("potion-flask:crack-2");
        keysAfter.Should().Contain("potion-flask:wall-broken").And.NotContain(k => k.StartsWith("potion-flask:crack"));
        PotionFlaskEmote.Instance.Compose(PotionFlaskEmote.CRACK_MS - 1f, 0).Should().ContainSingle();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Rubble_flies_up_then_comes_to_rest_on_the_ground()
    {
        PotionFlaskEmote.RubbleChunks(PotionFlaskEmote.BURST_MS - 1f).Should().BeEmpty();

        var start = PotionFlaskEmote.RubbleChunks(PotionFlaskEmote.BURST_MS).ToList();
        var early = PotionFlaskEmote.RubbleChunks(PotionFlaskEmote.BURST_MS + 80f).ToList();
        var settled = PotionFlaskEmote.RubbleChunks(PotionFlaskEmote.FADE_OUT_MS).ToList();
        var later = PotionFlaskEmote.RubbleChunks(PotionFlaskEmote.TOTAL_MS - 1f).ToList();

        early.Select(c => c.Y).Average().Should().BeLessThan(start.Select(c => c.Y).Average());
        settled.Should().OnlyContain(c => c.Y + 1 == PotionFlaskEmote.GROUND_Y);
        later.Should().Equal(settled);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_flask_leaps_out_lands_shouts_with_arms_up_then_backs_into_the_hole()
    {
        PotionFlaskEmote.Flask(PotionFlaskEmote.BURST_MS - 1f).Should().BeNull();

        var burst = PotionFlaskEmote.Flask(PotionFlaskEmote.BURST_MS)!.Value;
        burst.X.Should().Be(PotionFlaskEmote.HOLE_LEFT_X);

        var midLeap = PotionFlaskEmote.Flask((PotionFlaskEmote.BURST_MS + PotionFlaskEmote.LAND_MS) / 2f)!.Value;
        midLeap.Y.Should().Be(PotionFlaskEmote.FlaskRestTopY - PotionFlaskEmote.LEAP_ROWS);

        for (var t = PotionFlaskEmote.LAND_MS; t < PotionFlaskEmote.SHOUT_END_MS; t += 10f)
        {
            var flask = PotionFlaskEmote.Flask(t)!.Value;

            flask.X.Should().Be(PotionFlaskEmote.LAND_LEFT_X);
            flask.ArmsUp.Should().BeTrue();
            flask.Y.Should().BeInRange(PotionFlaskEmote.FlaskRestTopY - 1, PotionFlaskEmote.FlaskRestTopY);
            PotionFlaskEmote.IsShouting(t).Should().BeTrue();
        }

        PotionFlaskEmote.IsShouting(PotionFlaskEmote.SHOUT_END_MS).Should().BeFalse();
        PotionFlaskEmote.Flask(PotionFlaskEmote.BACK_AT_HOLE_MS)!.Value.X.Should().Be(PotionFlaskEmote.HOLE_LEFT_X);
        PotionFlaskEmote.Flask(PotionFlaskEmote.FADE_OUT_MS - 1f)!.Value.Opacity.Should().BeLessThan(0.05f);
        PotionFlaskEmote.Flask(PotionFlaskEmote.FADE_OUT_MS).Should().BeNull();
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_flask_lands_clear_of_the_head_and_left_of_the_wall()
    {
        PotionFlaskEmote.LAND_LEFT_X.Should().BeGreaterThan(CustomEmoteGeometry.HEAD_RIGHT_X);
        PotionFlaskEmote.WALL_LEFT_X.Should().BeGreaterThan(PotionFlaskEmote.LAND_LEFT_X + PotionFlaskEmote.FlaskWidth / 2);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Fizz_only_rises_while_the_flask_shouts_and_never_under_the_speech_bubble()
    {
        PotionFlaskEmote.Fizz(PotionFlaskEmote.LAND_MS - 1f).Should().BeEmpty();
        PotionFlaskEmote.Fizz(PotionFlaskEmote.SHOUT_END_MS + PotionFlaskEmote.FIZZ_LIFE_MS).Should().BeEmpty();

        var neckX = PotionFlaskEmote.LAND_LEFT_X + PotionFlaskEmote.FlaskWidth / 2;

        for (var t = PotionFlaskEmote.LAND_MS; t < PotionFlaskEmote.SHOUT_END_MS; t += 10f)
            foreach (var (dx, dy, opacity) in PotionFlaskEmote.Fizz(t))
            {
                (neckX + dx).Should().BeLessThan(PotionFlaskEmote.BUBBLE_LEFT_X);
                dy.Should().BeInRange(-PotionFlaskEmote.FIZZ_RISE_ROWS, 0);
                opacity.Should().BeInRange(0f, 1f);
            }

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_flask_has_two_poses_of_the_same_size()
    {
        PotionFlaskEmote.FlaskArmsUpPixels.Count.Should().Be(PotionFlaskEmote.FlaskArmsDownPixels.Count);
        PotionFlaskEmote.FlaskArmsUpPixels.Should().OnlyContain(r => r.Length == PotionFlaskEmote.FlaskWidth);
        PotionFlaskEmote.FlaskArmsDownPixels.Should().OnlyContain(r => r.Length == PotionFlaskEmote.FlaskWidth);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_mirrored_bubble_reverses_only_its_text()
    {
        var plain = PotionFlaskEmote.BuildSpeechBubble(PotionFlaskEmote.SHOUT);
        var mirrored = PotionFlaskEmote.BuildSpeechBubble(PotionFlaskEmote.SHOUT, true);

        mirrored.Count.Should().Be(plain.Count);

        for (var row = 0; row < plain.Count; row++)
        {
            if (row is < 2 or > 6)
            {
                mirrored[row].Should().Be(plain[row]);

                continue;
            }

            var text = plain[row][2..^2];

            mirrored[row].Should().Be(plain[row][..2] + new string(text.Reverse().ToArray()) + plain[row][^2..]);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_shout_layer_swaps_to_the_mirrored_bubble_when_flipped()
    {
        var bubble = PotionFlaskEmote.Instance
                                     .Compose(PotionFlaskEmote.LAND_MS, 0)
                                     .Single(l => l.MirroredArt is not null);

        bubble.MirroredArt!.Key.Should().NotBe(bubble.Art.Key);
        bubble.MirroredArt.Width.Should().Be(bubble.Art.Width);
        await Task.CompletedTask;
    }
    #endregion
}
