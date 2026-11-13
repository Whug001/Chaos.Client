using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TamerStableTextTests
{
    [Test]
    public void Fixed_window_strings_match_the_spec()
    {
        TamerStableText.WindowTitle.Should().Be("Stable");
        TamerStableText.CreaturesTab.Should().Be("Creatures");
        TamerStableText.SquadsTab.Should().Be("Squads");
        TamerStableText.Opening.Should().Be("Opening the ledger\u2026");
        TamerStableText.EmptyStable.Should().Be("No creatures yet. Weaken a wild creature and throw a binding orb.");
        TamerStableText.UnknownCreature.Should().Be("Unknown creature");
        TamerStableText.PeakLevel.Should().Be("Peak level");
        TamerStableText.Chosen.Should().Be("Chosen");
        TamerStableText.Choose.Should().Be("Choose");
        TamerStableText.Rename.Should().Be("Rename");
        TamerStableText.Feed.Should().Be("Feed");
        TamerStableText.Release.Should().Be("Release");
        TamerStableText.MakeActive.Should().Be("Make active");
        TamerStableText.Front.Should().Be("Front");
        TamerStableText.Back.Should().Be("Back");
        TamerStableText.NoSetBonus.Should().Be("No set bonus.");
        TamerStableText.Close.Should().Be("Close");
        TamerStableText.Save.Should().Be("Save");
        TamerStableText.Revert.Should().Be("Revert");
        TamerStableText.Remove.Should().Be("Remove");
        TamerStableText.Confirm.Should().Be("Confirm");
        TamerStableText.Cancel.Should().Be("Cancel");
        TamerStableText.Yes.Should().Be("Yes");
        TamerStableText.No.Should().Be("No");
    }

    [Test]
    public void Valid_names_follow_the_house_pet_rule()
    {
        TamerStableText.IsValidName("Ember").Should().BeTrue();
        TamerStableText.IsValidName("A").Should().BeTrue();
        TamerStableText.IsValidName("Abcdefghijkl").Should().BeTrue();
        TamerStableText.IsValidName("Ember Imp").Should().BeTrue();
        TamerStableText.IsValidName("a b c").Should().BeTrue();
    }

    [Test]
    public void Valid_names_are_trimmed_before_they_are_checked()
    {
        TamerStableText.IsValidName("  Ember  ").Should().BeTrue();
        TamerStableText.IsValidName(" Ember Imp ").Should().BeTrue();
    }

    [Test]
    public void Empty_and_null_names_are_invalid()
    {
        TamerStableText.IsValidName(string.Empty).Should().BeFalse();
        TamerStableText.IsValidName("   ").Should().BeFalse();
        TamerStableText.IsValidName(null).Should().BeFalse();
    }

    [Test]
    public void Over_long_names_are_invalid()
    {
        TamerStableText.IsValidName("Abcdefghijklm").Should().BeFalse();
        TamerStableText.IsValidName("Ember Imp Name").Should().BeFalse();
    }

    [Test]
    public void Names_with_double_spaces_are_invalid()
    {
        TamerStableText.IsValidName("Ember  Imp").Should().BeFalse();
        TamerStableText.IsValidName("Ember   Imp").Should().BeFalse();
    }

    [Test]
    public void Names_with_digits_punctuation_or_links_are_invalid()
    {
        TamerStableText.IsValidName("Ember2").Should().BeFalse();
        TamerStableText.IsValidName("Ember-Imp").Should().BeFalse();
        TamerStableText.IsValidName("Ember_Imp").Should().BeFalse();
        TamerStableText.IsValidName("Ember's").Should().BeFalse();
        TamerStableText.IsValidName("Ember!").Should().BeFalse();
    }

    [Test]
    public void Release_question_names_the_creature_and_its_essence()
    {
        TamerStableText.ReleaseQuestion("Ember", 5).Should().Be("Release Ember for 5 essence? This cannot be undone.");
        TamerStableText.ReleaseQuestion("River Imp", 100).Should().Be("Release River Imp for 100 essence? This cannot be undone.");
        TamerStableText.ReleaseQuestion("Ash", 40).Should().Be("Release Ash for 40 essence? This cannot be undone.");
    }

    [Test]
    public void Discard_question_matches_the_spec()
    {
        TamerStableText.DiscardQuestion.Should().Be("Discard changes to this squad?");
    }

    [Test]
    public void Count_line_shows_mine_over_capacity_creatures()
    {
        TamerStableText.CountLine(0, 60).Should().Be("0 / 60 creatures");
        TamerStableText.CountLine(7, 60).Should().Be("7 / 60 creatures");
        TamerStableText.CountLine(60, 60).Should().Be("60 / 60 creatures");
        TamerStableText.CountLine(3, 10).Should().Be("3 / 10 creatures");
    }

    [Test]
    public void Top_line_and_slot_lines_match_the_spec()
    {
        TamerStableText.EssenceLine(1250).Should().Be("Essence 1250");
        TamerStableText.FeedLine(4).Should().Be("Feed 4");
        TamerStableText.SlotsLine(3, 3).Should().Be("3 of 3 slots used");
        TamerStableText.SlotsLine(1, 5).Should().Be("1 of 5 slots used");
        TamerStableText.SlotsLine(0, 3).Should().Be("0 of 3 slots used");
    }

    [Test]
    public void Level_lines_match_the_spec()
    {
        TamerStableText.ListLevel(4).Should().Be("Lv 4");
        TamerStableText.ListLevel(20).Should().Be("Lv 20");
        TamerStableText.DetailLevel(4).Should().Be("Level 4");
        TamerStableText.DetailLevel(20).Should().Be("Level 20");
        TamerStableText.UnlocksAtLevel(5).Should().Be("Unlocks at level 5");
        TamerStableText.FeedPreviewLine(4, 6).Should().Be("Level 4 \u2192 6");
        TamerStableText.FeedPreviewLine(4, 4).Should().Be("Level 4 \u2192 4");
    }

    [Test]
    public void Sort_names_are_newest_level_and_name()
    {
        TamerStableText.SortName(TamerStableSort.Newest).Should().Be("Newest");
        TamerStableText.SortName(TamerStableSort.Level).Should().Be("Level");
        TamerStableText.SortName(TamerStableSort.Name).Should().Be("Name");
        TamerStableText.SortLabel(TamerStableSort.Newest).Should().Be("Sort: Newest");
        TamerStableText.SortLabel(TamerStableSort.Level).Should().Be("Sort: Level");
        TamerStableText.SortLabel(TamerStableSort.Name).Should().Be("Sort: Name");
    }

    [Test]
    public void Element_rarity_and_role_names_match_the_enums()
    {
        TamerStableText.ElementName(TamerWireElement.Fire).Should().Be("Fire");
        TamerStableText.ElementName(TamerWireElement.Water).Should().Be("Water");
        TamerStableText.ElementName(TamerWireElement.Wind).Should().Be("Wind");
        TamerStableText.ElementName(TamerWireElement.Earth).Should().Be("Earth");
        TamerStableText.RarityName(TamerWireRarity.Common).Should().Be("Common");
        TamerStableText.RarityName(TamerWireRarity.Uncommon).Should().Be("Uncommon");
        TamerStableText.RarityName(TamerWireRarity.Rare).Should().Be("Rare");
        TamerStableText.RarityName(TamerWireRarity.Boss).Should().Be("Boss");
        TamerStableText.RoleName(TamerWireRole.Bruiser).Should().Be("Bruiser");
        TamerStableText.RoleName(TamerWireRole.Striker).Should().Be("Striker");
        TamerStableText.RoleName(TamerWireRole.Support).Should().Be("Support");
    }

    [Test]
    public void Element_colour_names_match_the_card_faces()
    {
        TamerStableText.ElementColorName(TamerWireElement.Fire).Should().Be("Red");
        TamerStableText.ElementColorName(TamerWireElement.Water).Should().Be("Blue");
        TamerStableText.ElementColorName(TamerWireElement.Wind).Should().Be("Green");
        TamerStableText.ElementColorName(TamerWireElement.Earth).Should().Be("Brown");
    }

    [Test]
    public void Unknown_elements_fall_back_to_grey_and_unknown()
    {
        TamerStableText.ElementName((TamerWireElement)99).Should().Be("Unknown");
        TamerStableText.ElementColorName((TamerWireElement)99).Should().Be("Gray");
        TamerStableText.RarityName((TamerWireRarity)99).Should().Be("Unknown");
        TamerStableText.RoleName((TamerWireRole)99).Should().Be("Unknown");
    }

    [Test]
    public void Invalid_name_message_names_the_rule()
    {
        TamerStableText.InvalidName.Should().Be("Name must be 1-12 letters, single spaces between words.");
    }

    [Test]
    public void Short_text_is_returned_unchanged_by_fit_lines()
    {
        TamerStableText.FitLines("Hello world", 368, 4).Should().Be("Hello world");
        TamerStableText.FitLines("Line one\nLine two", 368, 4).Should().Be("Line one\nLine two");
    }

    [Test]
    public void A_two_hundred_character_description_wraps_into_four_lines()
    {
        var description = new string('a', 200);

        var fitted = TamerStableText.FitLines(description, 368, 4);
        var lines = fitted.Split('\n');

        lines.Should().HaveCount(4);
        lines.Should().OnlyContain(line => line.Length * 6 <= 368);
        string.Concat(lines).Should().HaveLength(200);
    }

    [Test]
    public void Long_text_is_cut_to_the_line_count_with_dots()
    {
        var text = new string('a', 300);

        var fitted = TamerStableText.FitLines(text, 368, 4);
        var lines = fitted.Split('\n');

        lines.Should().HaveCount(4);
        lines[^1].Should().EndWith("..");
        lines.Should().OnlyContain(line => line.Length * 6 <= 368);
    }

    [Test]
    public void A_single_line_fit_cuts_with_dots()
    {
        var fitted = TamerStableText.FitLines(new string('a', 100), 368, 1);

        fitted.Should().NotContain("\n");
        fitted.Should().EndWith("..");
        (fitted.Length * 6).Should().BeLessThanOrEqualTo(368);
    }

    [Test]
    public void Fit_lines_of_nothing_or_no_room_is_empty()
    {
        TamerStableText.FitLines(null, 368, 4).Should().Be(string.Empty);
        TamerStableText.FitLines(string.Empty, 368, 4).Should().Be(string.Empty);
        TamerStableText.FitLines("Text", 0, 4).Should().Be(string.Empty);
        TamerStableText.FitLines("Text", 368, 0).Should().Be(string.Empty);
    }
}
