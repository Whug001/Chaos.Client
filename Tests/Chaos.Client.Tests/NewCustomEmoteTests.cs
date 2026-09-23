using Chaos.Client.Rendering.CustomEmotes;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class NewCustomEmoteTests
{
    private static readonly PixelArtEmote[] NewEmotes =
    [
        HeartEyesEmote.Instance,
        HaloEmote.Instance,
        LightbulbEmote.Instance,
        AngerSteamEmote.Instance,
        PartyHatEmote.Instance
    ];

    /// <summary>Every layer an emote shows over its whole run, sampled every 5ms.</summary>
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
        NewEmotes.Select(e => e.BodyAnimation).Should().Equal(20, 2, 3, 4, 5);
        NewEmotes.Select(e => e.PreviewFrame).Should().Equal(1002, 1003, 1004, 1005, 1006);
        NewEmotes.Select(e => e.DurationMs).Should().Equal(1500f, 2000f, 1600f, 1600f, 1800f);

        foreach (var emote in NewEmotes)
            Enum.IsDefined(typeof(BodyAnimation), (byte)emote.BodyAnimation).Should().BeFalse(emote.Name);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Each_emote_is_gone_at_its_end_and_opaque_on_its_icon()
    {
        foreach (var emote in NewEmotes)
        {
            emote.Compose(emote.DurationMs, 0).Should().BeEmpty(emote.Name);

            var icon = emote.Compose(emote.IconTimeMs, 0);
            icon.Should().NotBeEmpty(emote.Name);
            icon.Should().OnlyContain(l => l.Opacity == 1f, emote.Name);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task No_colour_is_near_white_so_the_icon_crop_keeps_it()
    {
        //ImageUtil.FindHeadBounds skips pixels whose R, G and B are all >= 220 (speech-bubble white)
        foreach (var emote in NewEmotes)
        foreach (var art in AllLayers(emote).Select(l => l.Art).Distinct())
        foreach (var color in art.Colors())
            (color.R >= 220 && color.G >= 220 && color.B >= 220)
                .Should()
                .BeFalse($"{art.Key} paints {color}, which the icon crop would drop");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Art_keys_are_unique_across_emotes()
    {
        var byKey = new Dictionary<string, PixelArt>();

        foreach (var art in NewEmotes.SelectMany(AllLayers).Select(l => l.Art).Distinct())
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
        foreach (var emote in NewEmotes)
            for (var t = 0f; t < emote.DurationMs; t += 50f)
            {
                var still = emote.Compose(t, 0);
                var bobbed = emote.Compose(t, 1);

                bobbed.Select(l => l.Y).Should().Equal(still.Select(l => l.Y + 1), $"{emote.Name} at {t}ms");
                bobbed.Select(l => l.X).Should().Equal(still.Select(l => l.X), $"{emote.Name} at {t}ms");
            }

        await Task.CompletedTask;
    }
    #endregion

    #region heart eyes
    [Test]
    public async Task Hearts_pulse_about_four_times_a_second()
    {
        HeartEyesEmote.IsBig(0f).Should().BeTrue();
        HeartEyesEmote.IsBig(HeartEyesEmote.PULSE_MS).Should().BeFalse();
        HeartEyesEmote.IsBig(HeartEyesEmote.PULSE_MS * 2).Should().BeTrue();

        var shrinks = 0;

        for (var t = 1f; t < 1000f; t += 1f)
            if (HeartEyesEmote.IsBig(t - 1f) && !HeartEyesEmote.IsBig(t))
                shrinks++;

        shrinks.Should().Be(4);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Big_and_small_hearts_are_each_centred_on_their_own_eye()
    {
        foreach (var pixels in new[] { HeartEyesEmote.BigPixels, HeartEyesEmote.SmallPixels })
        {
            MeanOpaqueColumn(pixels, 0, 5).Should().BeApproximately(2.0, 0.001);
            MeanOpaqueColumn(pixels, 6, 10).Should().BeApproximately(8.0, 0.001);
        }

        var big = HeartEyesEmote.Instance.Compose(0f, 0).Single();
        var small = HeartEyesEmote.Instance.Compose(HeartEyesEmote.PULSE_MS, 0).Single();

        big.Y.Should().BeLessThan(small.Y);
        await Task.CompletedTask;
    }

    /// <summary>Mean column of the opaque ('.'-free) pixels in <paramref name="fromColumn" />..<paramref name="toColumn" />, inclusive.</summary>
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

    #region halo
    [Test]
    public async Task The_halo_fades_in_then_holds()
    {
        HaloEmote.Opacity(0f).Should().Be(0f);
        HaloEmote.Opacity(HaloEmote.FADE_IN_MS / 2f).Should().BeApproximately(0.5f, 0.001f);
        HaloEmote.Opacity(HaloEmote.FADE_IN_MS).Should().Be(1f);
        HaloEmote.Opacity(HaloEmote.TOTAL_MS - 1f).Should().Be(1f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_halo_bobs_one_row()
    {
        var rows = new HashSet<int>();

        for (var t = 0f; t < HaloEmote.TOTAL_MS; t += 10f)
            rows.Add(HaloEmote.BobRows(t));

        rows.Should().BeEquivalentTo(new[] { 0, -1 });
        HaloEmote.BobRows(0f).Should().Be(0);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_glint_slides_across_once()
    {
        HaloEmote.GlintColumn(HaloEmote.GLINT_START_MS - 1f).Should().Be(-1);
        HaloEmote.GlintColumn(HaloEmote.GLINT_END_MS).Should().Be(-1);

        var visited = new List<int>();

        for (var t = HaloEmote.GLINT_START_MS; t < HaloEmote.GLINT_END_MS; t += 1f)
        {
            var column = HaloEmote.GlintColumn(t);

            if (visited.Count == 0 || visited[^1] != column)
                visited.Add(column);
        }

        visited.Should().Equal(2, 3, 4, 5, 6, 7, 8);
        await Task.CompletedTask;
    }
    #endregion

    #region lightbulb
    [Test]
    public async Task The_bulb_rises_into_place()
    {
        LightbulbEmote.RiseOffset(0f).Should().Be(LightbulbEmote.RISE_ROWS);
        LightbulbEmote.RiseOffset(LightbulbEmote.RISE_MS).Should().Be(0);

        var previous = int.MaxValue;

        for (var t = 0f; t <= LightbulbEmote.RISE_MS; t += 5f)
        {
            var offset = LightbulbEmote.RiseOffset(t);
            offset.Should().BeLessThanOrEqualTo(previous);
            previous = offset;
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_bulb_flickers_on_twice_then_stays_lit()
    {
        var turnOns = 0;

        for (var t = 1f; t < LightbulbEmote.STEADY_MS; t += 1f)
            if (!LightbulbEmote.IsLit(t - 1f) && LightbulbEmote.IsLit(t))
                turnOns++;

        turnOns.Should().Be(2);

        for (var t = LightbulbEmote.STEADY_MS; t < LightbulbEmote.TOTAL_MS; t += 10f)
            LightbulbEmote.IsLit(t).Should().BeTrue();

        LightbulbEmote.IsLit(LightbulbEmote.RISE_MS).Should().BeFalse();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Rays_only_show_once_the_bulb_is_steady()
    {
        LightbulbEmote.Instance.Compose(LightbulbEmote.STEADY_MS - 1f, 0).Should().ContainSingle();
        LightbulbEmote.Instance.Compose(LightbulbEmote.STEADY_MS, 0).Should().HaveCount(2);
        await Task.CompletedTask;
    }
    #endregion

    #region anger steam
    [Test]
    public async Task Steam_puffs_come_in_pairs_and_are_gone_by_the_end()
    {
        AngerSteamEmote.Puffs(0f).Should().HaveCount(2);
        AngerSteamEmote.Puffs(500f).Should().HaveCount(4);
        AngerSteamEmote.Puffs(1500f).Should().BeEmpty();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Each_side_drifts_outward_and_up()
    {
        for (var t = 0f; t < AngerSteamEmote.TOTAL_MS; t += 10f)
            foreach (var puff in AngerSteamEmote.Puffs(t))
            {
                if (puff.IsLeft)
                    puff.CenterX.Should().BeLessThanOrEqualTo(AngerSteamEmote.LEFT_ORIGIN_X);
                else
                    puff.CenterX.Should().BeGreaterThanOrEqualTo(AngerSteamEmote.RIGHT_ORIGIN_X);

                puff.CenterY.Should().BeLessThanOrEqualTo(AngerSteamEmote.ORIGIN_Y);
            }

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_puff_only_grows_and_fades_as_it_ages()
    {
        var previousStage = -1;
        var previousOpacity = float.MaxValue;

        //follow the first left-hand puff through its life
        for (var t = 0f; t < AngerSteamEmote.PUFF_LIFE_MS; t += 5f)
        {
            var puff = AngerSteamEmote.Puffs(t).First(p => p.IsLeft);

            puff.Stage.Should().BeGreaterThanOrEqualTo(previousStage);
            puff.Opacity.Should().BeLessThanOrEqualTo(previousOpacity);
            puff.Opacity.Should().BeInRange(0f, 1f);

            previousStage = puff.Stage;
            previousOpacity = puff.Opacity;
        }

        previousStage.Should().Be(2);
        await Task.CompletedTask;
    }
    #endregion

    #region party hat
    [Test]
    public async Task The_hat_falls_onto_the_head()
    {
        PartyHatEmote.RowOffset(0f).Should().Be(PartyHatEmote.START_ROW_OFFSET);
        PartyHatEmote.RowOffset(PartyHatEmote.DROP_MS).Should().Be(0);

        var previous = int.MinValue;

        for (var t = 0f; t <= PartyHatEmote.DROP_MS; t += 5f)
        {
            var offset = PartyHatEmote.RowOffset(t);
            offset.Should().BeGreaterThanOrEqualTo(previous);
            previous = offset;
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Confetti_bursts_on_landing_and_is_gone_after()
    {
        PartyHatEmote.Instance.Compose(PartyHatEmote.DROP_MS - 1f, 0).Should().ContainSingle();
        PartyHatEmote.Instance.Compose(PartyHatEmote.DROP_MS, 0).Should().HaveCount(1 + 8);

        PartyHatEmote.Instance.Compose(PartyHatEmote.DROP_MS + PartyHatEmote.CONFETTI_MS, 0)
                     .Should()
                     .ContainSingle();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Confetti_goes_up_before_it_comes_down()
    {
        var landing = PartyHatEmote.Instance.Compose(PartyHatEmote.DROP_MS, 0).Skip(1).ToList();
        var early = PartyHatEmote.Instance.Compose(PartyHatEmote.DROP_MS + 60f, 0).Skip(1).ToList();
        var late = PartyHatEmote.Instance.Compose(PartyHatEmote.DROP_MS + PartyHatEmote.CONFETTI_MS - 1f, 0).Skip(1).ToList();

        early.Select(l => l.Y).Average().Should().BeLessThan(landing.Select(l => l.Y).Average());
        late.Select(l => l.Y).Average().Should().BeGreaterThan(early.Select(l => l.Y).Average());
        await Task.CompletedTask;
    }
    #endregion
}
