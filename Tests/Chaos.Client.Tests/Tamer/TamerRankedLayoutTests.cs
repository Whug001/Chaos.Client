using Chaos.Client.ViewModel;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TamerRankedLayoutTests
{
    private const int CHAR = 6;

    private static void ShouldNotOverlap(IReadOnlyList<TamerBox> boxes)
    {
        for (var i = 0; i < boxes.Count; i++)
            for (var j = i + 1; j < boxes.Count; j++)
                boxes[i].Intersects(boxes[j]).Should().BeFalse($"{boxes[i]} and {boxes[j]} must not overlap");
    }

    [Test]
    public void Boxes_that_only_touch_do_not_intersect()
    {
        var a = new TamerBox("a", 0, 0, 10, 10);

        a.Intersects(new TamerBox("b", 10, 0, 10, 10)).Should().BeFalse();
        a.Intersects(new TamerBox("c", 0, 10, 10, 10)).Should().BeFalse();
        a.Intersects(new TamerBox("d", 9, 9, 10, 10)).Should().BeTrue();
        a.IsInside(new TamerBox("e", 0, 0, 10, 10)).Should().BeTrue();
        a.IsInside(new TamerBox("f", 1, 0, 10, 10)).Should().BeFalse();
        a.Offset(3, 4).Should().Be(new TamerBox("a", 3, 4, 10, 10));
    }

    [Test]
    public void The_window_is_620_by_440_with_a_580_by_312_content_area_at_20_52()
    {
        TamerStableLayout.Panel.Should().Be(new TamerBox("Window", 0, 0, 620, 440));
        TamerStableLayout.Content.Should().Be(new TamerBox("Content", 20, 52, 580, 312));
        TamerRankedLayout.Area.Should().Be(new TamerBox("Ranked tab", 0, 0, 580, 312));
    }

    [Test]
    public void The_top_line_matches_the_spec_table()
    {
        var line = TamerStableLayout.TopLine();

        line.Select(box => (box.X, box.Width)).Should().Equal((20, 70), (94, 70), (168, 70), (244, 161), (410, 80), (495, 105));
        line.Take(3).Should().OnlyContain(box => (box.Y == 24) && (box.Height == 22));
        line.Skip(3).Should().OnlyContain(box => (box.Y == 29) && (box.Height == 12));
    }

    [Test]
    public void Nothing_on_the_top_line_overlaps_or_leaves_its_strip()
    {
        var line = TamerStableLayout.TopLine();

        ShouldNotOverlap(line);
        line.Should().OnlyContain(box => box.IsInside(TamerStableLayout.TopStrip));
        TamerStableLayout.TopStrip.IsInside(TamerStableLayout.Panel).Should().BeTrue();
        TamerStableLayout.TopStrip.Intersects(TamerStableLayout.Content).Should().BeFalse();
        TamerStableLayout.Content.IsInside(TamerStableLayout.Panel).Should().BeTrue();
    }

    [Test]
    public void The_top_line_texts_fit()
    {
        //captions are centred in their buttons; keep a character clear each side
        foreach (var caption in new[] { TamerStableText.CreaturesTab, TamerStableText.SquadsTab, TamerStableText.RankedTab })
            ((caption.Length + 2) * CHAR).Should().BeLessThanOrEqualTo(TamerStableLayout.TAB_WIDTH);

        TamerStableText.RankedTab.Should().Be("Ranked");
        TamerDrawableText.IsPrintableAscii(TamerStableText.RankedTab).Should().BeTrue();

        //a label's text area is its width less 1px of padding each side
        (TamerStableText.EssenceLine(999_999_999_999_999_999).Length * CHAR).Should().BeLessThanOrEqualTo(TamerStableLayout.ESSENCE_WIDTH - 2);
        (TamerStableText.FeedLine(999_999).Length * CHAR).Should().BeLessThanOrEqualTo(TamerStableLayout.FEED_WIDTH - 2);
        (TamerStableText.CountLine(60, 60).Length * CHAR).Should().BeLessThanOrEqualTo(TamerStableLayout.COUNT_WIDTH - 2);
    }

    [Test]
    public void Nothing_in_the_ranked_tab_overlaps()
    {
        var controls = TamerRankedLayout.Controls();

        controls.Should().HaveCount(7 + 5 + 1 + 3 + 1 + 10 + 3 + (3 * 8) + 5);
        ShouldNotOverlap(controls);
    }

    [Test]
    public void Nothing_in_the_ranked_tab_leaves_its_column()
    {
        foreach (var box in TamerRankedLayout.Controls())
        {
            box.IsInside(TamerRankedLayout.Area).Should().BeTrue($"{box} must stay in the tab");

            (box.IsInside(TamerRankedLayout.LeftColumn) || box.IsInside(TamerRankedLayout.RightColumn))
                .Should()
                .BeTrue($"{box} must stay in one column");
        }

        TamerRankedLayout.LeftColumn.Intersects(TamerRankedLayout.RightColumn).Should().BeFalse();
        TamerRankedLayout.LeftColumn.IsInside(TamerRankedLayout.Area).Should().BeTrue();
        TamerRankedLayout.RightColumn.IsInside(TamerRankedLayout.Area).Should().BeTrue();
        TamerRankedLayout.RightColumn.Right.Should().Be(580);
    }

    [Test]
    public void Offer_rows_do_not_overlap_and_their_parts_stay_inside_the_row()
    {
        var rows = Enumerable.Range(0, TamerRankedLayout.OFFER_COUNT).Select(TamerRankedLayout.OfferRow).ToList();
        var parts = TamerRankedLayout.OfferRowParts();

        ShouldNotOverlap(rows);
        rows.Select(row => row.Y).Should().Equal(26, 72, 118);
        rows.Should().OnlyContain(row => row.IsInside(TamerRankedLayout.RightColumn));
        ShouldNotOverlap(parts);
        parts.Should().OnlyContain(part => part.IsInside(TamerRankedLayout.OfferRowArea));
    }

    [Test]
    public void A_line_that_replaces_controls_overlaps_only_what_it_replaces()
    {
        var controls = TamerRankedLayout.Controls();

        foreach (var (box, replaces) in TamerRankedLayout.Replacements())
        {
            box.IsInside(TamerRankedLayout.Area).Should().BeTrue($"{box} must stay in the tab");

            //the reading line stands alone: everything else is hidden while it shows
            if (replaces.Length == 0)
                continue;

            foreach (var control in controls.Where(control => !control.Name.StartsWith(replaces, StringComparison.Ordinal)))
                box.Intersects(control).Should().BeFalse($"{box} must not overlap {control}");
        }
    }

    [Test]
    public void The_regions_match_the_spec_table()
    {
        TamerRankedLayout.Step.Should().Be(new TamerBox("Tier and step", 0, 0, 186, 12));
        TamerRankedLayout.Bar.Should().Be(new TamerBox("Points bar", 0, 16, 186, 10));
        (TamerRankedLayout.Points.Y, TamerRankedLayout.Rating.Y, TamerRankedLayout.Rating.Right).Should().Be((28, 28, 186));
        TamerRankedLayout.Best.Y.Should().Be(44);
        TamerRankedLayout.Season.Y.Should().Be(58);
        TamerRankedLayout.YourDefence.Y.Should().Be(78);
        TamerRankedLayout.DefencePortrait(0).Should().Be(new TamerBox("Defence portrait 1", 0, 92, 34, 34));
        TamerRankedLayout.DefencePortrait(4).Right.Should().Be(186);
        TamerRankedLayout.SetFrom.Should().Be(new TamerBox("Set from squad", 0, 135, 96, 12));
        TamerRankedLayout.SquadButton(0).Should().Be(new TamerBox("Squad button 1", 100, 130, 26, 22));
        TamerRankedLayout.SquadButton(2).Right.Should().Be(184);
        TamerRankedLayout.TopTitle.Y.Should().Be(156);
        TamerRankedLayout.TopRow(0).Y.Should().Be(170);
        TamerRankedLayout.TopRow(9).Bottom.Should().BeLessThanOrEqualTo(310);
        TamerRankedLayout.Opponents.Should().Be(new TamerBox("Opponents", 196, 5, 90, 12));
        TamerRankedLayout.Attacks.Should().Be(new TamerBox("Attacks left", 300, 5, 200, 12));
        TamerRankedLayout.Refresh.Should().Be(new TamerBox("Refresh button", 510, 0, 70, 22));
        TamerRankedLayout.OfferRow(0).Should().Be(new TamerBox("Offer row 1", 196, 26, 384, 44));
        TamerRankedLayout.OfferPortrait(0).Offset(196, 26).Should().Be(new TamerBox("portrait 1", 282, 28, 40, 40));
        TamerRankedLayout.OfferPortrait(4).Offset(196, 26).Right.Should().Be(490);
        TamerRankedLayout.OfferRowParts()[^1].Offset(196, 26).Should().Be(new TamerBox("Attack button", 506, 37, 70, 22));
        TamerRankedLayout.LogTitle.Should().Be(new TamerBox("Defence log", 196, 168, 384, 12));
        TamerRankedLayout.LogList.Should().Be(new TamerBox("Log list", 196, 182, 384, 96));
        TamerRankedLayout.LOG_ROW_WIDTH.Should().Be(TamerRankedText.LOG_ROW_WIDTH);
        TamerRankedLayout.Watch.Should().Be(new TamerBox("Watch button", 196, 284, 80, 22));
        TamerRankedLayout.Revenge.Should().Be(new TamerBox("Revenge button", 282, 284, 80, 22));
        TamerRankedLayout.Hint.Should().Be(new TamerBox("Revenge hint", 368, 289, 212, 12));
    }
}
