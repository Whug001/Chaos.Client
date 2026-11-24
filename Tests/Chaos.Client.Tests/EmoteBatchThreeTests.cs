using Chaos.Client.Rendering.CustomEmotes;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class EmoteBatchThreeTests
{
    private static readonly PixelArtEmote[] Emotes =
    [
        ReverseCardEmote.Instance,
        PressFEmote.Instance,
        PopcornEmote.Instance,
        MicDropEmote.Instance,
        TinyViolinEmote.Instance,
        StonksEmote.Instance,
        SaltyEmote.Instance,
        TumbleweedEmote.Instance,
        RedFlagEmote.Instance,
        FacepalmEmote.Instance
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
        Emotes.Select(e => e.BodyAnimation).Should().Equal(48, 49, 50, 51, 52, 53, 54, 55, 56, 57);
        Emotes.Select(e => e.PreviewFrame).Should().Equal(1012, 1013, 1014, 1015, 1016, 1017, 1018, 1019, 1020, 1021);
        Emotes.Select(e => e.DurationMs).Should().Equal(2000f, 2000f, 2500f, 2400f, 2600f, 2400f, 2400f, 3000f, 2200f, 2200f);

        foreach (var emote in Emotes)
        {
            Enum.IsDefined(typeof(BodyAnimation), (byte)emote.BodyAnimation).Should().BeFalse(emote.Name);
            emote.DurationMs.Should().BeInRange(1500f, 3000f, emote.Name);
        }

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
    public async Task Each_icon_fits_near_the_head()
    {
        foreach (var emote in Emotes)
            foreach (var layer in emote.IconLayers())
            {
                layer.X.Should().BeGreaterThanOrEqualTo(40, $"{emote.Name} {layer.Art.Key}");
                //the reverse card is 11 wide at x62, so its icon reaches one past the old bound
                (layer.X + layer.Art.Width).Should().BeLessThanOrEqualTo(73, $"{emote.Name} {layer.Art.Key}");
                layer.Y.Should().BeGreaterThanOrEqualTo(12, $"{emote.Name} {layer.Art.Key}");
                (layer.Y + layer.Art.Height).Should().BeLessThanOrEqualTo(46, $"{emote.Name} {layer.Art.Key}");
            }

        await Task.CompletedTask;
    }

    [Test]
    public async Task No_colour_is_near_white_so_the_icon_crop_keeps_it()
    {
        foreach (var emote in Emotes)
        foreach (var art in AllLayers(emote).Concat(emote.IconLayers()).Select(l => l.Art).Distinct())
        foreach (var color in art.Colors())
            (color.R >= 220 && color.G >= 220 && color.B >= 220)
                .Should()
                .BeFalse($"{art.Key} paints {color}, which the icon crop would drop");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Art_keys_are_unique_across_the_registry()
    {
        var byKey = new Dictionary<string, PixelArt>();

        foreach (var emote in CustomEmoteRegistry.All.OfType<PixelArtEmote>())
        foreach (var art in AllLayers(emote).Concat(emote.IconLayers()).Select(l => l.Art).Distinct())
        {
            if (byKey.TryGetValue(art.Key, out var existing))
                existing.Should().BeSameAs(art, $"key '{art.Key}' names two different pieces of art");

            byKey[art.Key] = art;
        }

        foreach (var emote in Emotes)
            AllLayers(emote).Select(l => l.Art).Should().NotBeEmpty($"{emote.Name} draws nothing");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Head_emotes_follow_the_head_bob()
    {
        var heads = Emotes.Where(e => !e.EndsOnWalk).ToList();
        heads.Should().NotBeEmpty();

        foreach (var emote in heads)
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
    public async Task Ground_scenes_ignore_the_bob_and_stay_on_it_while_head_emotes_end_on_the_head()
    {
        foreach (var emote in Emotes)
            if (emote.EndsOnWalk)
            {
                for (var t = 0f; t < emote.DurationMs; t += 10f)
                {
                    var still = emote.Compose(t, 0);
                    var bobbed = emote.Compose(t, 1);

                    bobbed.Select(l => (l.X, l.Y)).Should().Equal(still.Select(l => (l.X, l.Y)), $"{emote.Name} at {t}ms");
                    still.Should()
                         .OnlyContain(l => l.Y + l.Art.Height - 1 <= PotionFlaskEmote.GROUND_Y, $"{emote.Name} at {t}ms");
                }
            }
            else
                emote.EndsOnWalk.Should().BeFalse(emote.Name);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Opacity_never_leaves_zero_to_one()
    {
        foreach (var emote in Emotes)
            AllLayers(emote).Concat(emote.IconLayers()).Should().OnlyContain(l => l.Opacity >= 0f && l.Opacity <= 1f, emote.Name);

        await Task.CompletedTask;
    }
    #endregion

    #region reverse card
    [Test]
    public async Task The_card_rises_into_place_beside_the_head()
    {
        ReverseCardEmote.RowOffset(0f).Should().Be(ReverseCardEmote.START_ROW_OFFSET);
        ReverseCardEmote.RowOffset(ReverseCardEmote.RISE_MS).Should().Be(0);

        var previous = int.MaxValue;

        for (var t = 0f; t <= ReverseCardEmote.RISE_MS; t += 5f)
        {
            var offset = ReverseCardEmote.RowOffset(t);
            offset.Should().BeLessThanOrEqualTo(previous);
            previous = offset;
        }

        ReverseCardEmote.REST_LEFT_X.Should().BeGreaterThan(CustomEmoteGeometry.HEAD_RIGHT_X);

        var landed = ReverseCardEmote.Instance.Compose(ReverseCardEmote.RISE_MS, 0).Single();
        landed.X.Should().Be(ReverseCardEmote.REST_LEFT_X);
        landed.Y.Should().Be(ReverseCardEmote.REST_TOP_Y);
        landed.Art.Width.Should().Be(11);
        landed.Art.Height.Should().Be(15);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_card_flips_face_side_edge_side_twice()
    {
        ReverseCardEmote.FlipIndex(ReverseCardEmote.FLIP1_START_MS - 1f).Should().Be(-1);
        ReverseCardEmote.FlipIndex(ReverseCardEmote.FLIP1_START_MS).Should().Be(0);
        ReverseCardEmote.FlipIndex(ReverseCardEmote.FLIP2_START_MS).Should().Be(1);
        ReverseCardEmote.FlipIndex(ReverseCardEmote.FLIP2_START_MS + ReverseCardEmote.FLIP_MS).Should().Be(-1);

        foreach (var start in new[] { ReverseCardEmote.FLIP1_START_MS, ReverseCardEmote.FLIP2_START_MS })
        {
            var views = new List<int>();

            for (var t = start; t < start + ReverseCardEmote.FLIP_MS; t += 1f)
            {
                var view = ReverseCardEmote.View(t);

                if ((views.Count == 0) || (views[^1] != view))
                    views.Add(view);
            }

            views.Should().Equal([0, 1, 2, 1], $"flip starting at {start}ms");
        }

        ReverseCardEmote.View(ReverseCardEmote.RISE_MS).Should().Be(0);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_card_cycles_green_blue_red_across_the_flips()
    {
        ReverseCardEmote.CardColour(0f).Should().Be(0);
        ReverseCardEmote.CardColour(ReverseCardEmote.FLIP1_START_MS).Should().Be(0);
        ReverseCardEmote.CardColour(ReverseCardEmote.FLIP1_START_MS + ReverseCardEmote.FLIP_MS).Should().Be(1);
        ReverseCardEmote.CardColour(ReverseCardEmote.FLIP2_START_MS + ReverseCardEmote.FLIP_MS).Should().Be(2);

        ReverseCardEmote.Instance.Compose(ReverseCardEmote.RISE_MS, 0).Single().Art.Key.Should().Contain("green");
        ReverseCardEmote.Instance.Compose(ReverseCardEmote.FLIP1_START_MS + ReverseCardEmote.FLIP_MS + 1f, 0).Single().Art.Key.Should().Contain("blue");
        ReverseCardEmote.Instance.Compose(ReverseCardEmote.FLIP2_START_MS + ReverseCardEmote.FLIP_MS + 1f, 0).Single().Art.Key.Should().Contain("red");

        ReverseCardEmote.Opacity(ReverseCardEmote.FADE_OUT_MS - 1f).Should().Be(1f);
        ReverseCardEmote.Opacity(ReverseCardEmote.TOTAL_MS).Should().Be(0f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_arrows_need_no_mirroring_because_they_survive_a_half_turn()
    {
        ReverseCardEmote.Rotate180(ReverseCardEmote.FacePixels).Should().Equal(ReverseCardEmote.FacePixels);

        foreach (var layer in AllLayers(ReverseCardEmote.Instance))
            layer.MirroredArt.Should().BeNull(layer.Art.Key);

        await Task.CompletedTask;
    }
    #endregion

    #region press f
    [Test]
    public async Task The_keycap_rests_above_the_head_centred_on_it()
    {
        PressFEmote.LEFT_X.Should().Be(CustomEmoteGeometry.HEAD_LEFT_X);

        var resting = PressFEmote.Instance.Compose(PressFEmote.Instance.IconTimeMs, 0).Single();
        (resting.X + resting.Art.Width / 2).Should().Be(55);
        (resting.Y + resting.Art.Height - 1).Should().BeLessThan(CustomEmoteGeometry.HEAD_TOP_Y);
        resting.Art.Width.Should().Be(11);
        resting.Art.Height.Should().Be(11);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_cap_is_pressed_twice_then_springs_back()
    {
        PressFEmote.IsPressed(PressFEmote.PRESS1_START_MS - 1f).Should().BeFalse();
        PressFEmote.IsPressed(PressFEmote.PRESS1_START_MS).Should().BeTrue();
        PressFEmote.IsPressed(PressFEmote.PRESS1_START_MS + PressFEmote.PRESS_MS).Should().BeFalse();
        PressFEmote.IsPressed(PressFEmote.PRESS2_START_MS).Should().BeTrue();
        PressFEmote.IsPressed(PressFEmote.PRESS2_START_MS + PressFEmote.PRESS_MS).Should().BeFalse();

        string KeyAt(float t) => PressFEmote.Instance.Compose(t, 0).Single().Art.Key;
        KeyAt(PressFEmote.PRESS1_START_MS - 1f).Should().Contain("up");
        KeyAt(PressFEmote.PRESS1_START_MS).Should().Contain("pressed");
        KeyAt(PressFEmote.PRESS2_START_MS + 1f).Should().Contain("pressed");
        KeyAt(PressFEmote.PRESS2_START_MS + PressFEmote.PRESS_MS).Should().Contain("up");
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_cap_fades_while_rising_a_couple_of_rows()
    {
        PressFEmote.RiseRows(PressFEmote.FADE_OUT_MS - 1f).Should().Be(0);
        PressFEmote.RiseRows(PressFEmote.TOTAL_MS - 1f).Should().Be(PressFEmote.FADE_RISE_ROWS);
        PressFEmote.Opacity(PressFEmote.FADE_OUT_MS - 1f).Should().Be(1f);
        PressFEmote.Opacity(PressFEmote.TOTAL_MS).Should().Be(0f);

        var late = PressFEmote.Instance.Compose(PressFEmote.TOTAL_MS - 1f, 0).Single();
        late.Opacity.Should().BeLessThan(0.05f);
        late.Y.Should().Be(PressFEmote.REST_TOP_Y - PressFEmote.FADE_RISE_ROWS);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Both_faces_carry_a_pre_mirrored_F()
    {
        foreach (var t in new[] { 100f, PressFEmote.PRESS1_START_MS + 1f, PressFEmote.PRESS2_START_MS + 1f })
        {
            var layer = PressFEmote.Instance.Compose(t, 0).Single();
            layer.MirroredArt.Should().NotBeNull();
            layer.MirroredArt!.Width.Should().Be(layer.Art.Width);
            layer.MirroredArt.Height.Should().Be(layer.Art.Height);
        }

        PressFEmote.MirrorRows(PressFEmote.UpPixels)
                   .Should()
                   .Equal(PressFEmote.UpPixels.Select(r => new string(r.Reverse().ToArray())));
        PressFEmote.MirrorRows(PressFEmote.MirrorRows(PressFEmote.UpPixels)).Should().Equal(PressFEmote.UpPixels);
        PressFEmote.MirrorRows(PressFEmote.MirrorRows(PressFEmote.PressedPixels)).Should().Equal(PressFEmote.PressedPixels);
        await Task.CompletedTask;
    }
    #endregion

    #region popcorn
    [Test]
    public async Task The_tub_rises_holds_then_lowers_and_fades()
    {
        PopcornEmote.TubOffset(0f).Should().Be(PopcornEmote.START_ROW_OFFSET);
        PopcornEmote.TubOffset(PopcornEmote.RISE_MS).Should().Be(0);
        PopcornEmote.TubOffset(PopcornEmote.FADE_OUT_MS - 1f).Should().Be(0);
        PopcornEmote.TubOffset(PopcornEmote.TOTAL_MS - 1f).Should().Be(PopcornEmote.FADE_SINK_ROWS);

        PopcornEmote.TubOpacity(PopcornEmote.FADE_OUT_MS - 1f).Should().Be(1f);
        PopcornEmote.TubOpacity(PopcornEmote.TOTAL_MS).Should().Be(0f);

        (PopcornEmote.REST_TOP_Y + 3).Should().Be(37);
        await Task.CompletedTask;
    }

    [Test]
    public async Task At_least_five_kernels_pop_out()
    {
        PopcornEmote.KernelStartsMs.Should().HaveCountGreaterThanOrEqualTo(5);

        for (var i = 0; i < PopcornEmote.KernelStartsMs.Count; i++)
        {
            var start = PopcornEmote.KernelStartsMs[i];
            var kernel = PopcornEmote.Kernels(start + 1f).Single(k => k.Index == i);
            kernel.Opacity.Should().Be(1f);
            kernel.CenterX.Should().Be(PopcornEmote.HEAP_CENTER_X);
        }

        PopcornEmote.Kernels(PopcornEmote.KernelStartsMs[0] - 1f).Should().BeEmpty();
        PopcornEmote.Kernels(PopcornEmote.TOTAL_MS - 1f).Should().BeEmpty();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Kernels_arc_up_and_out_alternating_sides()
    {
        for (var i = 0; i < PopcornEmote.KernelStartsMs.Count; i++)
        {
            var start = PopcornEmote.KernelStartsMs[i];
            var side = i % 2 == 0 ? -1 : 1;
            var reach = 0;
            var top = PopcornEmote.HEAP_CENTER_Y;

            for (var t = start + 1f; t < start + PopcornEmote.KERNEL_LIFE_MS; t += 5f)
            {
                var kernel = PopcornEmote.Kernels(t).Single(k => k.Index == i);
                ((kernel.CenterX - PopcornEmote.HEAP_CENTER_X) * side).Should().BeGreaterThanOrEqualTo(0);

                ((kernel.CenterX - PopcornEmote.HEAP_CENTER_X) * side).Should().BeGreaterThanOrEqualTo(reach);
                reach = (kernel.CenterX - PopcornEmote.HEAP_CENTER_X) * side;
                kernel.CenterY.Should().BeLessThanOrEqualTo(top);
                top = kernel.CenterY;
            }

            reach.Should().Be(PopcornEmote.KERNEL_DRIFT_COLUMNS, $"kernel {i} drifts fully outward");
            top.Should().Be(PopcornEmote.HEAP_CENTER_Y - PopcornEmote.KERNEL_RISE_ROWS);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Kernels_fade_at_the_top_of_their_arc()
    {
        for (var i = 0; i < PopcornEmote.KernelStartsMs.Count; i++)
        {
            var start = PopcornEmote.KernelStartsMs[i];
            var last = PopcornEmote.Kernels(start + PopcornEmote.KERNEL_LIFE_MS - 1f).Single(k => k.Index == i);
            last.Opacity.Should().BeLessThan(0.1f);
            last.CenterY.Should().Be(PopcornEmote.HEAP_CENTER_Y - PopcornEmote.KERNEL_RISE_ROWS);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_tub_covers_the_chin_but_leaves_the_eyes_visible()
    {
        var tub = PopcornEmote.Instance.Compose(PopcornEmote.Instance.IconTimeMs, 0).First();
        tub.Y.Should().BeGreaterThan(CustomEmoteGeometry.EYE_TOP_Y);
        (tub.Y + tub.Art.Height - 1).Should().BeGreaterThan(CustomEmoteGeometry.HEAD_BOTTOM_Y);
        (tub.X + tub.Art.Width / 2).Should().BeGreaterThanOrEqualTo(55);

        PopcornEmote.Instance.Compose(PopcornEmote.Instance.IconTimeMs, 0)
                             .Should()
                             .ContainSingle("no kernel has popped at icon time");
        await Task.CompletedTask;
    }
    #endregion

    #region mic drop
    [Test]
    public async Task The_mic_hangs_then_falls_with_accelerating_speed()
    {
        for (var t = 0f; t < MicDropEmote.DROP_START_MS; t += 10f)
            MicDropEmote.MicTopY(t).Should().Be(MicDropEmote.HAND_TOP_Y);

        MicDropEmote.MicTopY(MicDropEmote.DROP_START_MS).Should().Be(MicDropEmote.HAND_TOP_Y);
        MicDropEmote.IsLying(MicDropEmote.LAND_MS - 1f).Should().BeFalse();
        MicDropEmote.IsLying(MicDropEmote.LAND_MS).Should().BeTrue();
        MicDropEmote.MicTopY(MicDropEmote.LAND_MS).Should().Be(MicDropEmote.LYING_REST_TOP_Y);

        var early = MicDropEmote.MicTopY(600f) - MicDropEmote.MicTopY(500f);
        var late = MicDropEmote.MicTopY(800f) - MicDropEmote.MicTopY(700f);
        late.Should().BeGreaterThan(early);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_mic_bounces_once_lying_down_then_rests_on_the_ground()
    {
        var atLand = MicDropEmote.MicTopY(MicDropEmote.LAND_MS);
        var midBounce = MicDropEmote.MicTopY(MicDropEmote.LAND_MS + MicDropEmote.BOUNCE_MS / 2f);
        var atRest = MicDropEmote.MicTopY(MicDropEmote.REST_MS);

        midBounce.Should().Be(MicDropEmote.LYING_REST_TOP_Y - MicDropEmote.BOUNCE_APEX_ROWS);
        midBounce.Should().BeLessThan(atLand);
        midBounce.Should().BeLessThan(atRest);
        atRest.Should().Be(MicDropEmote.LYING_REST_TOP_Y);

        for (var t = MicDropEmote.REST_MS; t < MicDropEmote.FADE_OUT_MS; t += 25f)
            MicDropEmote.MicTopY(t).Should().Be(MicDropEmote.LYING_REST_TOP_Y);

        //the lying piece takes over exactly at the landing, on the same bottom row
        var falling = MicDropEmote.Instance.Compose(MicDropEmote.LAND_MS - 1f, 0).First();
        var lying = MicDropEmote.Instance.Compose(MicDropEmote.LAND_MS, 0).First();
        falling.Art.Key.Should().Contain("upright");
        lying.Art.Key.Should().Contain("lying");
        (falling.Y + falling.Art.Height - 1).Should().Be(lying.Y + lying.Art.Height - 1);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Each_ground_hit_flicks_impact_marks_out_that_fade()
    {
        MicDropEmote.Impacts(MicDropEmote.LAND_MS - 1f).Should().BeEmpty();

        foreach (var hit in new[] { MicDropEmote.LAND_MS, MicDropEmote.REST_MS })
        {
            var marks = MicDropEmote.Impacts(hit).ToList();
            marks.Should().HaveCount(3);
            marks.Count(m => m.IsLine).Should().Be(2);
            marks.Count(m => !m.IsLine).Should().Be(1);
            marks.Should().OnlyContain(m => m.Opacity == 1f);

            var lines = marks.Where(m => m.IsLine).ToList();
            (lines[0].X + 3).Should().BeLessThanOrEqualTo(MicDropEmote.MIC_X);
            lines[1].X.Should().BeGreaterThan(MicDropEmote.MIC_X + MicDropEmote.UPRIGHT_WIDTH - 1);

            var leftEarly = MicDropEmote.Impacts(hit).Where(m => m.IsLine).Min(m => m.X);
            var leftLate = MicDropEmote.Impacts(hit + MicDropEmote.IMPACT_MS - 1f).Where(m => m.IsLine).Min(m => m.X);
            var rightEarly = MicDropEmote.Impacts(hit).Where(m => m.IsLine).Max(m => m.X);
            var rightLate = MicDropEmote.Impacts(hit + MicDropEmote.IMPACT_MS - 1f).Where(m => m.IsLine).Max(m => m.X);
            leftLate.Should().BeLessThan(leftEarly);
            rightLate.Should().BeGreaterThan(rightEarly);

            MicDropEmote.Impacts(hit + MicDropEmote.IMPACT_MS - 1f).Should().OnlyContain(m => m.Opacity < 0.05f);
        }

        //the landing's window ends exactly as the bounce's hit starts, so only the last window ever clears
        MicDropEmote.Impacts(MicDropEmote.REST_MS + MicDropEmote.IMPACT_MS).Should().BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_mic_hangs_beside_the_body_and_never_sinks_below_the_ground()
    {
        MicDropEmote.MIC_X.Should().BeGreaterThan(CustomEmoteGeometry.HEAD_RIGHT_X);
        MicDropEmote.HAND_TOP_Y.Should().BeGreaterThan(CustomEmoteGeometry.HEAD_BOTTOM_Y);

        for (var t = 0f; t < MicDropEmote.TOTAL_MS; t += 10f)
            foreach (var layer in MicDropEmote.Instance.Compose(t, 0))
                (layer.Y + layer.Art.Height - 1).Should().BeLessThanOrEqualTo(PotionFlaskEmote.GROUND_Y, $"at {t}ms");

        MicDropEmote.Instance.EndsOnWalk.Should().BeTrue();

        var icon = MicDropEmote.Instance.IconLayers();
        icon.Should().ContainSingle();
        icon[0].Art.Key.Should().Contain("upright");
        icon[0].Opacity.Should().Be(1f);
        await Task.CompletedTask;
    }
    #endregion

    #region tiny violin
    [Test]
    public async Task The_violin_sits_by_the_cheek_overlapping_it()
    {
        //the body overlaps the cheek so it reads against the head, while the neck and scroll clear it
        TinyViolinEmote.VIOLIN_LEFT_X.Should().BeLessThanOrEqualTo(CustomEmoteGeometry.HEAD_RIGHT_X);
        (TinyViolinEmote.VIOLIN_LEFT_X + 13).Should().BeGreaterThan(CustomEmoteGeometry.HEAD_RIGHT_X);
        TinyViolinEmote.VIOLIN_TOP_Y.Should().BeGreaterThan(CustomEmoteGeometry.EYE_TOP_Y);

        var violin = TinyViolinEmote.Instance.Compose(TinyViolinEmote.Instance.IconTimeMs, 0).First();
        violin.Art.Width.Should().Be(13);
        violin.Art.Height.Should().Be(6);
        (violin.Y + violin.Art.Height - 1).Should().BeLessThanOrEqualTo(CustomEmoteGeometry.HEAD_BOTTOM_Y);

        //a figure-eight body: wide bouts top and bottom, a pinched waist, f-holes and a neck with scroll
        TinyViolinEmote.ViolinPixels[1].Should().Contain("mm");
        TinyViolinEmote.ViolinPixels[2].Should().Contain("f");
        TinyViolinEmote.ViolinPixels[2].Should().Contain("nnnn");
        TinyViolinEmote.ViolinPixels[2].Should().Contain("SS");
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_notes_are_navy_eighth_notes()
    {
        //a 2x2 head at the bottom-left, a 1-pixel stem 4 rows tall, a 2-pixel flag off the top to the right
        TinyViolinEmote.NotePixels.Should().HaveCount(6);
        TinyViolinEmote.NotePixels.Should().OnlyContain(r => r.Length == 4);
        TinyViolinEmote.NotePixels[0].Should().Be(".NNN");
        TinyViolinEmote.NotePixels[1].Should().Be(".N..");
        TinyViolinEmote.NotePixels[4].Should().Be("NN..");
        TinyViolinEmote.NotePixels[5].Should().Be("NN..");
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_bow_saws_three_columns_out_and_back()
    {
        TinyViolinEmote.BowOffset(TinyViolinEmote.PLAY_START_MS - 1f).Should().Be(0);
        TinyViolinEmote.BowOffset(TinyViolinEmote.PLAY_START_MS).Should().Be(0);
        TinyViolinEmote.BowOffset(TinyViolinEmote.PLAY_START_MS + TinyViolinEmote.SAW_MS).Should().Be(TinyViolinEmote.BOW_THROW_COLUMNS);
        TinyViolinEmote.BowOffset(TinyViolinEmote.PLAY_START_MS + 2 * TinyViolinEmote.SAW_MS).Should().Be(0);
        TinyViolinEmote.BowOffset(TinyViolinEmote.PLAY_END_MS).Should().Be(0);
        TinyViolinEmote.BOW_THROW_COLUMNS.Should().Be(3);

        var inner = TinyViolinEmote.Instance.Compose(TinyViolinEmote.PLAY_START_MS + 1f, 0).First(l => l.Art.Key.Contains("bow"));
        var outer = TinyViolinEmote.Instance.Compose(TinyViolinEmote.PLAY_START_MS + TinyViolinEmote.SAW_MS + 1f, 0).First(l => l.Art.Key.Contains("bow"));
        (outer.X - inner.X).Should().Be(3);
        inner.Y.Should().Be(TinyViolinEmote.BOW_TOP_Y);
        TinyViolinEmote.Instance.Compose(TinyViolinEmote.PLAY_END_MS, 0).Should().NotContain(l => l.Art.Key.Contains("bow"));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Three_notes_rise_up_and_out_and_fade()
    {
        TinyViolinEmote.NoteStartsMs.Should().HaveCountGreaterThanOrEqualTo(3);
        TinyViolinEmote.Notes(TinyViolinEmote.NoteStartsMs[0] - 1f).Should().BeEmpty();
        TinyViolinEmote.Notes(TinyViolinEmote.TOTAL_MS - 1f).Should().BeEmpty();

        for (var i = 0; i < TinyViolinEmote.NoteStartsMs.Count; i++)
        {
            var start = TinyViolinEmote.NoteStartsMs[i];
            var first = TinyViolinEmote.Notes(start + 1f).Single(n => n.Index == i);
            first.CenterX.Should().Be(TinyViolinEmote.NOTE_ORIGIN_X);
            first.CenterY.Should().Be(TinyViolinEmote.NOTE_ORIGIN_Y);
            first.Opacity.Should().Be(1f);

            var previous = first;

            for (var t = start + 5f; t < start + TinyViolinEmote.NOTE_LIFE_MS; t += 5f)
            {
                var note = TinyViolinEmote.Notes(t).Single(n => n.Index == i);
                note.CenterY.Should().BeLessThanOrEqualTo(previous.CenterY);
                note.CenterX.Should().BeGreaterThanOrEqualTo(previous.CenterX);
                note.Opacity.Should().BeLessThanOrEqualTo(previous.Opacity);
                previous = note;
            }

            previous.CenterY.Should().Be(TinyViolinEmote.NOTE_ORIGIN_Y - TinyViolinEmote.NOTE_RISE_ROWS);
            previous.CenterX.Should().Be(TinyViolinEmote.NOTE_ORIGIN_X + TinyViolinEmote.NOTE_DRIFT_COLUMNS);
            previous.Opacity.Should().BeLessThan(0.1f);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_violin_fades_in_then_out_at_the_end()
    {
        TinyViolinEmote.ViolinOpacity(0f).Should().Be(0f);
        TinyViolinEmote.ViolinOpacity(TinyViolinEmote.VIOLIN_IN_MS).Should().Be(1f);
        TinyViolinEmote.ViolinOpacity(TinyViolinEmote.FADE_OUT_MS - 1f).Should().Be(1f);
        TinyViolinEmote.ViolinOpacity(TinyViolinEmote.TOTAL_MS).Should().Be(0f);

        var icon = TinyViolinEmote.Instance.IconLayers();
        icon.Should().HaveCount(2);
        icon.Should().OnlyContain(l => l.Opacity == 1f);
        await Task.CompletedTask;
    }
    #endregion

    #region stonks
    [Test]
    public async Task The_chart_reveals_itself_segment_by_segment_arrowhead_last()
    {
        StonksEmote.StageIndex(0f).Should().Be(0);
        StonksEmote.StageIndex(StonksEmote.REVEAL_MS - 1f).Should().Be(4);
        StonksEmote.StageIndex(StonksEmote.REVEAL_MS).Should().Be(4);

        var seen = new List<int>();

        for (var t = 0f; t < StonksEmote.REVEAL_MS; t += 1f)
        {
            var stage = StonksEmote.StageIndex(t);

            if ((seen.Count == 0) || (seen[^1] != stage))
                seen.Add(stage);
        }

        seen.Should().Equal(0, 1, 2, 3, 4);

        var tops = new List<int>();
        var counts = new List<int>();

        for (var s = 0; s < 5; s++)
        {
            var rows = StonksEmote.Instance.Compose(s * StonksEmote.STAGE_MS + 1f, 0).Single().Art.Rows;
            tops.Add(TopRow(rows));
            counts.Add(Painted(rows));
        }

        //the first two stages climb to row 9, the middle ones to row 7, and only the last carries the arrowhead
        tops[0].Should().Be(9);
        tops[1].Should().Be(9);
        tops[2].Should().Be(7);
        tops[3].Should().Be(7);
        tops[4].Should().BeLessThanOrEqualTo(2);

        for (var s = 1; s < 5; s++)
            counts[s].Should().BeGreaterThan(counts[s - 1]);

        await Task.CompletedTask;

        static int TopRow(IReadOnlyList<string> rows)
        {
            for (var y = 0; y < rows.Count; y++)
                if (rows[y].Any(c => c != '.'))
                    return y;

            return -1;
        }

        static int Painted(IReadOnlyList<string> rows) => rows.Sum(r => r.Count(c => c != '.'));
    }

    [Test]
    public async Task The_finished_arrow_pulses_once_then_fades()
    {
        StonksEmote.PulseRows(StonksEmote.PULSE_UP_MS - 1f).Should().Be(0);
        StonksEmote.PulseRows(StonksEmote.PULSE_UP_MS).Should().Be(-1);
        StonksEmote.PulseRows(StonksEmote.PULSE_DOWN_MS).Should().Be(0);
        StonksEmote.PulseRows(StonksEmote.FADE_OUT_MS).Should().Be(0);

        var rest = StonksEmote.Instance.Compose(StonksEmote.PULSE_UP_MS - 1f, 0).Single().Y;
        StonksEmote.Instance.Compose(StonksEmote.PULSE_UP_MS, 0).Single().Y.Should().Be(rest - 1);
        StonksEmote.Instance.Compose(StonksEmote.PULSE_DOWN_MS, 0).Single().Y.Should().Be(rest);

        StonksEmote.Opacity(StonksEmote.FADE_OUT_MS - 1f).Should().Be(1f);
        StonksEmote.Opacity(StonksEmote.TOTAL_MS).Should().Be(0f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_mirrored_stages_read_left_to_right_once_flipped_back()
    {
        for (var t = 0f; t < StonksEmote.FADE_OUT_MS; t += 50f)
        {
            var layer = StonksEmote.Instance.Compose(t, 0).Single();
            layer.MirroredArt.Should().NotBeNull();
            layer.MirroredArt!.Width.Should().Be(layer.Art.Width);
            layer.MirroredArt.Height.Should().Be(layer.Art.Height);
            StonksEmote.MirrorRows(layer.MirroredArt.Rows).Should().Equal(layer.Art.Rows);
        }

        //the tip pixel sits on the right in unflipped coordinates and on the left once pre-mirrored,
        //so it lands on the right on screen in both facings
        var full = StonksEmote.Instance.Compose(1000f, 0).Single();
        TopPixelColumn(full.Art.Rows).Should().BeGreaterThan(StonksEmote.CHART_WIDTH / 2);
        TopPixelColumn(full.MirroredArt!.Rows).Should().BeLessThan(StonksEmote.CHART_WIDTH / 2);

        await Task.CompletedTask;

        static int TopPixelColumn(IReadOnlyList<string> rows)
        {
            var top = rows.Select((r, y) => (r, y)).First(t => t.r.Any(c => c != '.'));

            return top.r.Select((c, x) => (c, x)).First(t => t.c != '.').x;
        }
    }

    [Test]
    public async Task The_chart_rises_from_lower_left_to_upper_right()
    {
        StonksEmote.Spine.First().Should().Be((1, 14));
        StonksEmote.Spine.Last().Should().Be((17, 3));
        StonksEmote.Arrowhead.Should().OnlyContain(p => p.X >= 13);
        StonksEmote.Arrowhead.Min(p => p.Y).Should().Be(2);
        StonksEmote.Arrowhead.Max(p => p.Y).Should().Be(6);
        //the filled head is clearly wider than the two-pixel line
        StonksEmote.Arrowhead.Max(p => p.X).Should().BeGreaterThan(StonksEmote.Arrowhead.Min(p => p.X) + 2);
        StonksEmote.Arrowhead.Min(p => p.Y).Should().BeLessThan(StonksEmote.Spine.Max(p => p.Y));

        for (var i = 1; i < StonksEmote.Spine.Count; i++)
        {
            StonksEmote.Spine[i].X.Should().BeGreaterThanOrEqualTo(StonksEmote.Spine[i - 1].X);
            (StonksEmote.Spine[i].Y - StonksEmote.Spine[i - 1].Y).Should().BeLessThanOrEqualTo(1);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_chart_sits_beside_and_above_the_head()
    {
        StonksEmote.CHART_LEFT_X.Should().BeLessThanOrEqualTo(CustomEmoteGeometry.HEAD_RIGHT_X);
        (StonksEmote.CHART_LEFT_X + StonksEmote.CHART_WIDTH - 1).Should().BeGreaterThan(CustomEmoteGeometry.HEAD_RIGHT_X);
        StonksEmote.CHART_TOP_Y.Should().BeLessThan(CustomEmoteGeometry.HEAD_TOP_Y);

        //the arrowhead clears the head while the tail starts down by it
        (StonksEmote.CHART_LEFT_X + StonksEmote.Arrowhead.Max(p => p.X)).Should().BeGreaterThan(CustomEmoteGeometry.HEAD_RIGHT_X);
        (StonksEmote.CHART_TOP_Y + StonksEmote.Arrowhead.Min(p => p.Y)).Should().BeLessThan(CustomEmoteGeometry.HEAD_TOP_Y);
        (StonksEmote.CHART_TOP_Y + StonksEmote.Spine.Max(p => p.Y)).Should().BeGreaterThanOrEqualTo(CustomEmoteGeometry.HEAD_TOP_Y);
        await Task.CompletedTask;
    }
    #endregion

    #region salty
    [Test]
    public async Task The_shaker_stands_upright_then_tips_to_pour_and_back()
    {
        SaltyEmote.IsTilted(SaltyEmote.TILT_MS - 1f).Should().BeFalse();
        SaltyEmote.IsTilted(SaltyEmote.TILT_MS).Should().BeTrue();
        SaltyEmote.IsTilted(SaltyEmote.UNTILT_MS - 1f).Should().BeTrue();
        SaltyEmote.IsTilted(SaltyEmote.UNTILT_MS).Should().BeFalse();

        string KeyAt(float t) => SaltyEmote.Instance.Compose(t, 0).First().Art.Key;
        KeyAt(0f).Should().Be("salty:shaker");
        KeyAt(SaltyEmote.TILT_MS).Should().Be("salty:shaker-tilted");
        KeyAt(SaltyEmote.UNTILT_MS).Should().Be("salty:shaker");

        //the upright shaker sits above the head, slightly to the aisling's right
        var upright = SaltyEmote.Instance.Compose(0f, 0).First();
        upright.X.Should().Be(SaltyEmote.UPRIGHT_LEFT_X);
        upright.X.Should().BeGreaterThan(CustomEmoteGeometry.HEAD_LEFT_X);
        (upright.Y + upright.Art.Height - 1).Should().BeLessThan(CustomEmoteGeometry.HEAD_TOP_Y);
        upright.Art.Width.Should().Be(7);
        upright.Art.Height.Should().Be(10);

        //a metal cap with holes over a glass body filled with salt in its lower half
        SaltyEmote.UprightPixels[2].Should().Contain("H");
        SaltyEmote.UprightPixels[4].Should().NotContain("w");
        SaltyEmote.UprightPixels[7].Should().Contain("w");

        //the tilted piece leans down-left, its cap toward the head top
        var tilted = SaltyEmote.Instance.Compose(SaltyEmote.TILT_MS, 0).First();
        tilted.X.Should().Be(SaltyEmote.TILTED_LEFT_X);
        tilted.X.Should().BeLessThan(upright.X);
        tilted.Art.Width.Should().Be(7);
        tilted.Art.Height.Should().Be(10);
        tilted.Art.Key.Should().NotBe(upright.Art.Key);

        //the tilted cap carries visible holes and salt sits at the tube's pouring end
        SaltyEmote.TiltedPixels[7].Should().Contain("HH");
        SaltyEmote.TiltedPixels[5].Should().Contain("ww");
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_shaker_jolts_down_and_up_three_times()
    {
        var onsets = 0;

        for (var t = SaltyEmote.SHAKE_START_MS; t < SaltyEmote.UNTILT_MS; t += 1f)
            if ((SaltyEmote.JoltOffset(t - 1f) == 0) && (SaltyEmote.JoltOffset(t) == SaltyEmote.JOLT_ROWS))
                onsets++;

        onsets.Should().Be(SaltyEmote.JOLT_COUNT);

        SaltyEmote.JoltOffset(SaltyEmote.SHAKE_START_MS - 1f).Should().Be(0);
        SaltyEmote.JoltOffset(SaltyEmote.SHAKE_START_MS).Should().Be(SaltyEmote.JOLT_ROWS);
        SaltyEmote.JoltOffset(SaltyEmote.SHAKE_START_MS + SaltyEmote.JOLT_MS / 2f).Should().Be(0);
        SaltyEmote.JoltOffset(SaltyEmote.SHAKE_START_MS + SaltyEmote.JOLT_MS).Should().Be(SaltyEmote.JOLT_ROWS);
        SaltyEmote.JoltOffset(SaltyEmote.UNTILT_MS).Should().Be(0);

        //the jolt moves the pouring piece, never the upright one
        var dipped = SaltyEmote.Instance.Compose(SaltyEmote.SHAKE_START_MS, 0).First();
        var back = SaltyEmote.Instance.Compose(SaltyEmote.SHAKE_START_MS + SaltyEmote.JOLT_MS / 2f, 0).First();
        (dipped.Y - back.Y).Should().Be(SaltyEmote.JOLT_ROWS);
        dipped.Art.Should().BeSameAs(back.Art);

        var upright = SaltyEmote.Instance.Compose(SaltyEmote.UNTILT_MS + 1f, 0).First();
        upright.Y.Should().Be(SaltyEmote.UPRIGHT_TOP_Y);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Each_jolt_scatters_grains_from_the_cap_onto_the_head_top()
    {
        //grains fall for ~250ms while jolts come every 180ms, so neighbouring clusters overlap in flight
        SaltyEmote.GRAIN_LIFE_MS.Should().BeApproximately(250f, 1f);
        SaltyEmote.GRAIN_LIFE_MS.Should().BeGreaterThan(SaltyEmote.JOLT_MS);
        SaltyEmote.GRAINS_PER_JOLT.Should().BeInRange(6, 7);
        SaltyEmote.GrainDx.Should().HaveCount(SaltyEmote.GRAINS_PER_JOLT);
        SaltyEmote.GrainDx.Min().Should().BeLessThanOrEqualTo(-2);
        SaltyEmote.GrainDx.Max().Should().BeGreaterThanOrEqualTo(2);

        for (var jolt = 0; jolt < SaltyEmote.JOLT_COUNT; jolt++)
        {
            var start = SaltyEmote.SHAKE_START_MS + jolt * SaltyEmote.JOLT_MS;
            var grains = SaltyEmote.Grains(start + 1f).Where(g => g.Index == jolt).ToList();
            grains.Should().HaveCount(SaltyEmote.GRAINS_PER_JOLT);

            //fresh grains leave the pour point fully opaque, scattered either side of it...
            grains.Should().OnlyContain(g => g.Opacity == 1f);
            grains.Select(g => g.X).Should().Equal(SaltyEmote.GrainDx.Select(dx => SaltyEmote.POUR_X + dx));

            //...some drawn 2 pixels wide, a few with a mid-grey shadow dot beside them...
            grains.Should().Contain(g => g.IsWide);
            grains.Should().Contain(g => !g.IsWide);
            grains.Should().Contain(g => g.HasShadow);

            //...and land spread across the head top rows, fading as they land
            var last = SaltyEmote.Grains(start + SaltyEmote.GRAIN_LIFE_MS - 1f).Where(g => g.Index == jolt).ToList();
            last.Should().HaveCount(SaltyEmote.GRAINS_PER_JOLT);
            last.Should().OnlyContain(g => g.Y >= CustomEmoteGeometry.HEAD_TOP_Y && g.Y <= CustomEmoteGeometry.HEAD_TOP_Y + 2);
            last.Select(g => g.X).Distinct().Should().HaveCountGreaterThan(1);
            last.Should().OnlyContain(g => g.Opacity < 0.1f);

            SaltyEmote.Grains(start + SaltyEmote.GRAIN_LIFE_MS).Where(g => g.Index == jolt).Should().BeEmpty();
        }

        SaltyEmote.Grains(SaltyEmote.SHAKE_START_MS - 1f).Should().BeEmpty();
        SaltyEmote.Grains(SaltyEmote.TOTAL_MS - 1f).Should().BeEmpty();
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_shaker_tips_back_and_fades_out()
    {
        SaltyEmote.ShakerOpacity(0f).Should().Be(0f);
        SaltyEmote.ShakerOpacity(SaltyEmote.APPEAR_MS).Should().Be(1f);
        SaltyEmote.ShakerOpacity(SaltyEmote.FADE_OUT_MS - 1f).Should().Be(1f);
        SaltyEmote.ShakerOpacity(SaltyEmote.TOTAL_MS).Should().Be(0f);

        var late = SaltyEmote.Instance.Compose(SaltyEmote.TOTAL_MS - 1f, 0).First();
        late.Art.Key.Should().Be("salty:shaker");
        late.Opacity.Should().BeLessThan(0.05f);

        var icon = SaltyEmote.Instance.IconLayers();
        icon.Should().ContainSingle().Which.Art.Key.Should().Be("salty:shaker");
        await Task.CompletedTask;
    }
    #endregion

    #region tumbleweed
    [Test]
    public async Task The_weed_rolls_from_far_left_to_far_right_on_the_ground()
    {
        TumbleweedEmote.WeedX(0f).Should().Be(TumbleweedEmote.START_X);
        TumbleweedEmote.WeedX(TumbleweedEmote.TOTAL_MS).Should().Be(TumbleweedEmote.END_X);

        var previous = int.MinValue;

        for (var t = 0f; t < TumbleweedEmote.TOTAL_MS; t += 10f)
        {
            var x = TumbleweedEmote.WeedX(t);
            x.Should().BeGreaterThanOrEqualTo(previous);
            previous = x;
        }

        (TumbleweedEmote.RestTopY + TumbleweedEmote.WEED_HEIGHT - 1).Should().Be(PotionFlaskEmote.GROUND_Y);

        var weed = TumbleweedEmote.Instance.Compose(TumbleweedEmote.HOP_MS / 2f, 0).First();
        weed.Art.Width.Should().Be(TumbleweedEmote.WEED_WIDTH);
        weed.Art.Height.Should().Be(TumbleweedEmote.WEED_HEIGHT);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_weed_hops_low_four_times()
    {
        TumbleweedEmote.HOP_COUNT.Should().Be(4);
        TumbleweedEmote.HopLift(0f).Should().Be(0);

        foreach (var landing in TumbleweedEmote.LandingTimesMs)
            TumbleweedEmote.HopLift(landing).Should().Be(0);

        TumbleweedEmote.LandingTimesMs.Should().HaveCount(TumbleweedEmote.HOP_COUNT - 1);

        var apexes = 0;
        var wasUp = false;

        for (var t = 0f; t < TumbleweedEmote.TOTAL_MS; t += 5f)
        {
            var up = TumbleweedEmote.HopLift(t) == TumbleweedEmote.HOP_APEX_ROWS;

            if (up && !wasUp)
                apexes++;

            wasUp = up;
        }

        apexes.Should().Be(TumbleweedEmote.HOP_COUNT);
        TumbleweedEmote.HOP_APEX_ROWS.Should().BeInRange(2, 3);

        //mid-hop the weed hangs above the ground, never below it
        for (var t = 0f; t < TumbleweedEmote.TOTAL_MS; t += 10f)
            foreach (var layer in TumbleweedEmote.Instance.Compose(t, 0))
                (layer.Y + layer.Art.Height - 1).Should().BeLessThanOrEqualTo(PotionFlaskEmote.GROUND_Y, $"at {t}ms");
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_weed_cycles_three_rotation_frames_while_it_rolls()
    {
        TumbleweedEmote.FrameIndex(0f).Should().Be(0);
        TumbleweedEmote.FrameIndex(TumbleweedEmote.FRAME_MS).Should().Be(1);
        TumbleweedEmote.FrameIndex(2f * TumbleweedEmote.FRAME_MS).Should().Be(2);
        TumbleweedEmote.FrameIndex(3f * TumbleweedEmote.FRAME_MS).Should().Be(0);

        var seen = new List<int>();

        for (var t = 0f; t < 3f * TumbleweedEmote.FRAME_MS; t += 1f)
        {
            var frame = TumbleweedEmote.FrameIndex(t);

            if ((seen.Count == 0) || (seen[^1] != frame))
                seen.Add(frame);
        }

        seen.Should().Equal(0, 1, 2);

        //the frames visibly differ but share the same tangled tangle
        TumbleweedEmote.Frame1Pixels.Should().NotEqual(TumbleweedEmote.Frame0Pixels);
        TumbleweedEmote.Frame2Pixels.Should().NotEqual(TumbleweedEmote.Frame0Pixels);
        TumbleweedEmote.Frame0Pixels.Should().OnlyContain(r => r.Length == TumbleweedEmote.WEED_WIDTH);
        TumbleweedEmote.Frame1Pixels.Should().OnlyContain(r => r.Length == TumbleweedEmote.WEED_WIDTH);
        TumbleweedEmote.Frame2Pixels.Should().OnlyContain(r => r.Length == TumbleweedEmote.WEED_WIDTH);

        foreach (var frame in new[] { TumbleweedEmote.Frame0Pixels, TumbleweedEmote.Frame1Pixels, TumbleweedEmote.Frame2Pixels })
        {
            //three tones of twig: dark backs, mid middles, pale fronts
            var painted = frame.SelectMany(r => r).Where(c => c != '.').Distinct().OrderBy(c => c).ToList();
            painted.Should().Equal('d', 'm', 'p');

            //holes through the middle show the ground: the centre mixes twigs and gaps
            var middle = frame.Skip(4).Take(5).SelectMany(r => r.Skip(4).Take(5)).ToList();
            middle.Should().Contain('.');
            middle.Should().Contain(c => c == 'd' || c == 'm' || c == 'p');

            //no clean circular outline: some edge rows are empty and the silhouette is broken
            frame.SelectMany(r => r).Should().Contain('.');
            frame.First().Count(c => c != '.').Should().BeLessThanOrEqualTo(2);
            frame.Last().Count(c => c != '.').Should().BeLessThanOrEqualTo(2);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Dust_kicks_up_where_the_weed_lands_from_each_hop()
    {
        TumbleweedEmote.Puffs(0f).Should().BeEmpty();

        foreach (var landing in TumbleweedEmote.LandingTimesMs)
        {
            TumbleweedEmote.Puffs(landing - 1f).Should().BeEmpty();

            var puffs = TumbleweedEmote.Puffs(landing).ToList();
            puffs.Should().HaveCount(2);
            puffs.Should().OnlyContain(p => p.Opacity == 1f);
            (puffs[1].X - puffs[0].X).Should().Be(TumbleweedEmote.WEED_WIDTH - 1);
            puffs.Select(p => p.X).Should().Contain(TumbleweedEmote.WeedX(landing));

            TumbleweedEmote.Puffs(landing + TumbleweedEmote.PUFF_MS).Should().BeEmpty();

            var late = TumbleweedEmote.Puffs(landing + TumbleweedEmote.PUFF_MS - 1f).ToList();
            late.Should().HaveCount(2);
            late.Should().OnlyContain(p => p.Opacity < 0.05f);
            late[0].Y.Should().BeLessThanOrEqualTo(TumbleweedEmote.Puffs(landing).First().Y);
        }

        TumbleweedEmote.Puffs(TumbleweedEmote.TOTAL_MS - 1f).Should().BeEmpty();
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_weed_fades_in_and_out_at_the_ends()
    {
        TumbleweedEmote.Opacity(0f).Should().Be(0f);
        TumbleweedEmote.Opacity(TumbleweedEmote.FADE_IN_MS).Should().Be(1f);
        TumbleweedEmote.Opacity(TumbleweedEmote.FADE_OUT_MS - 1f).Should().Be(1f);
        TumbleweedEmote.Opacity(TumbleweedEmote.TOTAL_MS).Should().Be(0f);

        var late = TumbleweedEmote.Instance.Compose(TumbleweedEmote.TOTAL_MS - 1f, 0).First();
        late.Opacity.Should().BeLessThan(0.05f);

        TumbleweedEmote.Instance.EndsOnWalk.Should().BeTrue();

        var icon = TumbleweedEmote.Instance.IconLayers();
        icon.Should().ContainSingle().Which.Art.Key.Should().Be("tumbleweed:frame-0");
        await Task.CompletedTask;
    }
    #endregion

    #region red flag
    [Test]
    public async Task The_pole_rises_from_behind_the_head_with_an_ease_in()
    {
        RedFlagEmote.RowOffset(0f).Should().Be(RedFlagEmote.START_ROW_OFFSET);
        RedFlagEmote.RowOffset(RedFlagEmote.RISE_MS).Should().Be(0);

        var previous = int.MaxValue;

        for (var t = 0f; t <= RedFlagEmote.RISE_MS; t += 5f)
        {
            var offset = RedFlagEmote.RowOffset(t);
            offset.Should().BeLessThanOrEqualTo(previous);
            previous = offset;
        }

        //the pole stands just right of the head, its base inside the head's top rows
        RedFlagEmote.POLE_LEFT_X.Should().BeGreaterThan(CustomEmoteGeometry.HEAD_RIGHT_X);
        RedFlagEmote.POLE_BASE_Y.Should().BeGreaterThanOrEqualTo(CustomEmoteGeometry.HEAD_TOP_Y);

        var risen = RedFlagEmote.Instance.Compose(RedFlagEmote.RISE_MS, 0).First(l => l.Art.Key == "red-flag:pole");
        risen.X.Should().Be(RedFlagEmote.POLE_LEFT_X);
        risen.Y.Should().Be(RedFlagEmote.POLE_REST_TOP_Y);
        risen.Art.Width.Should().Be(1);
        risen.Art.Height.Should().Be(RedFlagEmote.POLE_HEIGHT);

        //at the start the pole hides behind the head top
        var rising = RedFlagEmote.Instance.Compose(0f, 0).First(l => l.Art.Key == "red-flag:pole");
        rising.Y.Should().Be(RedFlagEmote.POLE_REST_TOP_Y + RedFlagEmote.START_ROW_OFFSET);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_pennant_flies_from_the_pole_top_away_from_the_head()
    {
        var flag = RedFlagEmote.Instance.Compose(RedFlagEmote.RISE_MS, 0).First(l => l.Art.Key.Contains("flag-"));
        var pole = RedFlagEmote.Instance.Compose(RedFlagEmote.RISE_MS, 0).First(l => l.Art.Key == "red-flag:pole");

        flag.X.Should().BeGreaterThan(pole.X);
        flag.Y.Should().Be(pole.Y);
        flag.Art.Width.Should().Be(RedFlagEmote.FLAG_WIDTH);
        flag.Art.Height.Should().Be(RedFlagEmote.FLAG_HEIGHT);

        //the whole pennant clears the head to the right
        flag.X.Should().BeGreaterThan(CustomEmoteGeometry.HEAD_RIGHT_X);
        (flag.Y + flag.Art.Height - 1).Should().BeLessThan(CustomEmoteGeometry.HEAD_BOTTOM_Y);

        //two reds plus the dark shadow fold near the hoist
        RedFlagEmote.Flag0Pixels.SelectMany(r => r).Distinct().OrderBy(c => c).Should().Equal('.', 'R', 'r', 's');
        RedFlagEmote.Flag0Pixels[2][2].Should().Be('s');
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_cloth_ripples_through_three_frames_while_it_holds()
    {
        RedFlagEmote.WaveIndex(0f).Should().Be(0);
        RedFlagEmote.WaveIndex(RedFlagEmote.WAVE_MS).Should().Be(1);
        RedFlagEmote.WaveIndex(2f * RedFlagEmote.WAVE_MS).Should().Be(2);
        RedFlagEmote.WaveIndex(3f * RedFlagEmote.WAVE_MS).Should().Be(0);

        var seen = new List<int>();

        for (var t = 0f; t < 3f * RedFlagEmote.WAVE_MS; t += 1f)
        {
            var frame = RedFlagEmote.WaveIndex(t);

            if ((seen.Count == 0) || (seen[^1] != frame))
                seen.Add(frame);
        }

        seen.Should().Equal(0, 1, 2);

        string KeyAt(float t) => RedFlagEmote.Instance.Compose(t, 0).First(l => l.Art.Key.Contains("flag-")).Art.Key;
        KeyAt(RedFlagEmote.RISE_MS).Should().NotBe(KeyAt(RedFlagEmote.RISE_MS + RedFlagEmote.WAVE_MS));
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_flag_fades_out_at_the_end()
    {
        RedFlagEmote.Opacity(RedFlagEmote.FADE_OUT_MS - 1f).Should().Be(1f);
        RedFlagEmote.Opacity(RedFlagEmote.TOTAL_MS).Should().Be(0f);

        var late = RedFlagEmote.Instance.Compose(RedFlagEmote.TOTAL_MS - 1f, 0);
        late.Should().NotBeEmpty();
        late.Should().OnlyContain(l => l.Opacity < 0.05f);
        late.Select(l => l.Art.Key).Should().Contain("red-flag:pole");
        late.Where(l => l.Art.Key.Contains("flag-")).Should().ContainSingle();
        await Task.CompletedTask;
    }
    #endregion

    #region facepalm
    [Test]
    public async Task The_hand_rises_fast_and_covers_the_eyes()
    {
        FacepalmEmote.RiseOffset(0f).Should().Be(FacepalmEmote.START_ROW_OFFSET);
        FacepalmEmote.RiseOffset(FacepalmEmote.RISE_MS).Should().Be(0);
        //the hand sits centred on the head: just after the smack it holds alone over the eyes
        var hand = FacepalmEmote.Instance.Compose(FacepalmEmote.RISE_MS + FacepalmEmote.SMACK_MS + 1f, 0).Single();
        hand.Art.Key.Should().Be("facepalm:hand");
        hand.Art.Width.Should().Be(9);
        hand.Art.Height.Should().Be(12);
        hand.X.Should().Be(FacepalmEmote.HAND_LEFT_X);
        (hand.X + hand.Art.Width / 2).Should().Be(55);

        //the fingertips reach up over the forehead while the palm sits over the eyes and nose
        hand.Y.Should().BeLessThan(CustomEmoteGeometry.EYE_TOP_Y);
        (hand.Y + hand.Art.Height - 1).Should().BeGreaterThan(CustomEmoteGeometry.EYE_TOP_Y + 1);

        //four fingers with rounded tips at different lengths and darker gap lines between them...
        FacepalmEmote.HandPixels[0].Count(c => c == 'k').Should().Be(1);
        FacepalmEmote.HandPixels[2].Should().Contain("MSMSMS");
        FacepalmEmote.HandPixels[2].Should().StartWith("k");
        FacepalmEmote.HandPixels[2].Should().EndWith("kk");

        //...a thumb stub sticking out on the left, and a 5-wide wrist at the bottom
        FacepalmEmote.HandPixels[6].Should().StartWith(".k");
        FacepalmEmote.HandPixels[9].Should().Be("..kMMMk..");
        FacepalmEmote.HandPixels[11].Should().Be("..kkkkk..");
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_smack_flashes_an_impact_and_dips_the_hand_a_row()
    {
        FacepalmEmote.IsImpact(FacepalmEmote.RISE_MS - 1f).Should().BeFalse();
        FacepalmEmote.IsImpact(FacepalmEmote.RISE_MS).Should().BeTrue();
        FacepalmEmote.IsImpact(FacepalmEmote.RISE_MS + FacepalmEmote.SMACK_MS).Should().BeFalse();

        FacepalmEmote.SmackRows(FacepalmEmote.RISE_MS - 1f).Should().Be(0);
        FacepalmEmote.SmackRows(FacepalmEmote.RISE_MS).Should().Be(1);

        var smack = FacepalmEmote.Instance.Compose(FacepalmEmote.RISE_MS, 0);
        smack.Select(l => l.Art.Key).Should().Contain("facepalm:impact");
        var impact = smack.First(l => l.Art.Key == "facepalm:impact");
        impact.X.Should().Be(FacepalmEmote.IMPACT_LEFT_X);
        impact.Y.Should().Be(FacepalmEmote.IMPACT_TOP_Y);
        impact.Opacity.Should().Be(1f);

        //the hand sits one row lower while the impact flashes, then comes back
        var handAtSmack = smack.First(l => l.Art.Key == "facepalm:hand").Y;
        var handAfter = FacepalmEmote.Instance.Compose(FacepalmEmote.RISE_MS + FacepalmEmote.SMACK_MS + 1f, 0).Single().Y;
        (handAtSmack - handAfter).Should().Be(1);

        FacepalmEmote.Instance.Compose(FacepalmEmote.RISE_MS + FacepalmEmote.SMACK_MS + 1f, 0)
                             .Select(l => l.Art.Key)
                             .Should()
                             .NotContain("facepalm:impact");
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_hand_drags_down_once_in_a_weary_rub()
    {
        FacepalmEmote.RubRows(FacepalmEmote.RUB_START_MS - 1f).Should().Be(0);
        FacepalmEmote.RubRows((FacepalmEmote.RUB_START_MS + FacepalmEmote.RUB_START_MS + FacepalmEmote.RUB_MS) / 2f).Should().Be(0);
        FacepalmEmote.RubRows(FacepalmEmote.RUB_START_MS + FacepalmEmote.RUB_MS).Should().Be(1);

        var before = FacepalmEmote.HandTopY(FacepalmEmote.RUB_START_MS - 1f);
        var after = FacepalmEmote.HandTopY(FacepalmEmote.RUB_START_MS + FacepalmEmote.RUB_MS);
        (after - before).Should().Be(1);
        await Task.CompletedTask;
    }

    [Test]
    public async Task A_sigh_drifts_out_beside_the_chin_and_fades()
    {
        //a bigger soft cloudlet starting beside the chin
        FacepalmEmote.SighPixels.Should().HaveCount(4);
        FacepalmEmote.SighPixels.Should().OnlyContain(r => r.Length == 5);

        FacepalmEmote.Sigh(FacepalmEmote.SIGH_START_MS - 1f).Should().BeNull();

        var first = FacepalmEmote.Sigh(FacepalmEmote.SIGH_START_MS);
        first.Should().NotBeNull();
        first!.Value.X.Should().Be(FacepalmEmote.SIGH_ORIGIN_X);
        first!.Value.Y.Should().Be(FacepalmEmote.SIGH_ORIGIN_Y);
        first!.Value.Opacity.Should().Be(1f);

        //beside the chin: below the eye rows, right of the hand
        FacepalmEmote.SIGH_ORIGIN_Y.Should().BeGreaterThan(CustomEmoteGeometry.EYE_TOP_Y + 1);
        FacepalmEmote.SIGH_ORIGIN_X.Should().BeGreaterThan(FacepalmEmote.HAND_LEFT_X + 9 - 1);

        var last = FacepalmEmote.Sigh(FacepalmEmote.SIGH_START_MS + FacepalmEmote.SIGH_MS - 1f);
        last.Should().NotBeNull();
        (last!.Value.X - first!.Value.X).Should().Be(FacepalmEmote.SIGH_DRIFT_COLUMNS);
        last!.Value.Y.Should().BeLessThanOrEqualTo(first!.Value.Y);
        last!.Value.Opacity.Should().BeLessThan(0.1f);

        FacepalmEmote.Sigh(FacepalmEmote.SIGH_START_MS + FacepalmEmote.SIGH_MS).Should().BeNull();

        //the sigh rides along on the compose while it lives
        FacepalmEmote.Instance.Compose(FacepalmEmote.SIGH_START_MS + 1f, 0).Select(l => l.Art.Key).Should().Contain("facepalm:sigh");
        FacepalmEmote.Instance.Compose(FacepalmEmote.SIGH_START_MS + FacepalmEmote.SIGH_MS + 1f, 0).Select(l => l.Art.Key).Should().NotContain("facepalm:sigh");
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_hand_lowers_and_fades_at_the_end()
    {
        FacepalmEmote.Opacity(FacepalmEmote.FADE_OUT_MS - 1f).Should().Be(1f);
        FacepalmEmote.Opacity(FacepalmEmote.TOTAL_MS).Should().Be(0f);
        FacepalmEmote.LowerRows(FacepalmEmote.FADE_OUT_MS - 1f).Should().Be(0);
        FacepalmEmote.LowerRows(FacepalmEmote.TOTAL_MS - 1f).Should().Be(FacepalmEmote.FADE_SINK_ROWS);

        var late = FacepalmEmote.Instance.Compose(FacepalmEmote.TOTAL_MS - 1f, 0).Single();
        late.Art.Key.Should().Be("facepalm:hand");
        late.Opacity.Should().BeLessThan(0.05f);
        late.Y.Should().Be(FacepalmEmote.HAND_TOP_Y + 1 + FacepalmEmote.FADE_SINK_ROWS);
        await Task.CompletedTask;
    }
    #endregion
}
