using Chaos.Client.Controls.World.Popups.Learn;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

/// <summary>
///     The learn-ability popup's view model: the title, the Replaces line, and the requirement rows in the order the popup
///     lists them, built from the server's <see cref="LearnAbilityDisplayArgs" />.
/// </summary>
public sealed class LearnAbilityViewTests
{
    private static LearnAbilityDisplayArgs Args()
        => new()
        {
            IsSkill = false,
            Name = "Arcane Missile",
            Sprite = 12,
            Level = 40,
            Description = "{=aA volley of bolts.",
            Replaces = ["Arcane Bolt"],
            Gold = 15000,
            GoldMet = true,
            AbilityLevel = 50,
            AbilityLevelMet = false,
            Stats =
            [
                new LearnStatRequirement(Stat.INT, 25, true),
                new LearnStatRequirement(Stat.WIS, 20, false)
            ],
            Items = [new LearnItemRequirement("Exquisite Linen", 3, false)],
            Skills = [new LearnAbilityRequirement("Meditate", 0, true)],
            Spells = [new LearnAbilityRequirement("Arcane Bolt", 20, true)],
            LearnOption = 0,
            GuildFundsOption = 1,
            CancelOption = 2
        };

    [Test]
    public void From_FullArgs_RowsInOrder()
    {
        var view = LearnAbilityView.From(Args());

        view.Title.Should().Be("Arcane Missile  Lv 40");
        view.Description.Should().Be("{=aA volley of bolts.");
        view.ShowGuildFunds.Should().BeTrue();

        view.Rows
            .Should()
            .Equal(
                new LearnAbilityRow("Gold", "15,000", true),
                new LearnAbilityRow("Ability Level", "50", false),
                new LearnAbilityRow("INT", "25", true),
                new LearnAbilityRow("WIS", "20", false),
                new LearnAbilityRow("Exquisite Linen", "x3", false),
                new LearnAbilityRow("Meditate", "", true),
                new LearnAbilityRow("Arcane Bolt", "Lv 20", true));
    }

    [Test]
    public void From_NoGuildOption_HidesGuildButton()
    {
        var args = Args();
        args.GuildFundsOption = LearnAbilityDisplayArgs.NoOption;

        LearnAbilityView.From(args).ShowGuildFunds.Should().BeFalse();
    }

    [Test]
    public void From_ZeroGold_NoGoldRow()
    {
        var args = Args();
        args.Gold = 0;

        LearnAbilityView.From(args).Rows.Select(r => r.Label).Should().NotContain("Gold");
    }

    [Test]
    public void From_ZeroAbilityLevel_NoAbilityLevelRow()
    {
        var args = Args();
        args.AbilityLevel = 0;

        LearnAbilityView.From(args).Rows.Select(r => r.Label).Should().NotContain("Ability Level");
    }

    [Test]
    public void From_UndefinedStat_ShowsItsNumber()
    {
        var args = Args();
        args.Stats = [new LearnStatRequirement((Stat)64, 10, false)];

        LearnAbilityView.From(args).Rows.Should().Contain(new LearnAbilityRow("64", "10", false));
    }

    [Test]
    public void From_NoReplaces_NullReplacesLine()
    {
        var args = Args();
        args.Replaces = [];

        LearnAbilityView.From(args).ReplacesLine.Should().BeNull();
    }

    [Test]
    public void From_TwoReplaces_JoinedWithComma()
    {
        var args = Args();
        args.Replaces = ["Arcane Bolt", "Arcane Spark"];

        LearnAbilityView.From(args).ReplacesLine.Should().Be("Replaces: Arcane Bolt, Arcane Spark");
    }

    [Test]
    public void Press_EachButton_SelectsItsOptionAndCloses()
    {
        foreach (var (button, expected) in new[] { (LearnAbilityButton.Learn, 0), (LearnAbilityButton.GuildFunds, 1), (LearnAbilityButton.Cancel, 2) })
        {
            var state = new LearnAbilityPopupState();
            state.Open(Args());

            state.Press(button).Should().Be(expected);
            state.IsOpen.Should().BeFalse();
        }
    }

    [Test]
    public void Press_GuildFundsNotOffered_SelectsNothing()
    {
        var args = Args();
        args.GuildFundsOption = LearnAbilityDisplayArgs.NoOption;
        var state = new LearnAbilityPopupState();
        state.Open(args);

        state.Offers(LearnAbilityButton.GuildFunds).Should().BeFalse();
        state.Press(LearnAbilityButton.GuildFunds).Should().BeNull();
    }

    [Test]
    public void Close_NewDialogArrived_LaterPressSelectsNothing()
    {
        //the server answered Learn with "Come back when..." (a new dialog): the popup closes, and a late click on it
        //must not answer the new dialog with the old option index
        var state = new LearnAbilityPopupState();
        state.Open(Args());

        state.Close().Should().BeTrue();
        state.IsOpen.Should().BeFalse();
        state.Press(LearnAbilityButton.Learn).Should().BeNull();
        state.Close().Should().BeFalse();
    }
}
