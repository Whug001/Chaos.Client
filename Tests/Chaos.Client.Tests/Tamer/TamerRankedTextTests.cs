using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TamerRankedTextTests
{
    private const string LongName = "Abcdefghijkl";
    private const int CHAR = 6;

    private static TamerRankedLogView Entry(TamerRankedLogFlags flags, sbyte change = -7, ushort age = 120, string attacker = "Aoife")
        => new(Guid.NewGuid(), attacker, flags, change, age);

    //a label's text area is its width less 1px of padding each side
    private static void ShouldFit(string text, int labelWidth)
        => (text.Length * CHAR).Should().BeLessThanOrEqualTo(labelWidth - 2, $"\"{text}\" must fit a {labelWidth} wide label");

    //a wrapped label shows the whole text only when fitting it changes nothing but the line breaks
    private static void ShouldWrapInto(string text, int labelWidth, int lines)
    {
        var fitted = TamerStableText.FitLines(text, labelWidth - 2, lines);

        fitted.Replace('\n', ' ').Should().Be(text, $"\"{text}\" must fit {lines} lines of a {labelWidth} wide label");
        fitted.Split('\n').Length.Should().BeLessThanOrEqualTo(lines);
    }

    [Test]
    public void Fixed_ranked_strings_match_the_spec()
    {
        TamerRankedText.Reading.Should().Be("Reading the ladder...");
        TamerRankedText.NoDefence.Should().Be("Save a squad of five as your defence to enter the ladder.");
        TamerRankedText.NoOpponents.Should().Be("No opponents yet. Check back later.");
        TamerRankedText.DefenceGone.Should().Be("Your defence uses a creature that no longer exists. Save it again.");
        TamerRankedText.Opponents.Should().Be("Opponents");
        TamerRankedText.Refresh.Should().Be("Refresh");
        TamerRankedText.Attack.Should().Be("Attack");
        TamerRankedText.YourDefence.Should().Be("Your defence");
        TamerRankedText.SetFromSquad.Should().Be("Set from squad:");
        TamerRankedText.TopTen.Should().Be("Top 10");
        TamerRankedText.DefenceLog.Should().Be("Defence log");
        TamerRankedText.Watch.Should().Be("Watch");
        TamerRankedText.Revenge.Should().Be("Revenge");
        TamerRankedText.RevengeHint.Should().Be("Revenge does not use an attack.");
        TamerRankedText.Won.Should().Be("won");
        TamerRankedText.Lost.Should().Be("lost");
        TamerRankedText.TagRevenge.Should().Be("revenge");
        TamerRankedText.TagNoRank.Should().Be("no rank");
        TamerRankedText.TagRevenged.Should().Be("revenged");
        TamerRankedText.RefreshQuestion.Should().Be("Refresh the opponents? You can do this once a day.");
        TamerRankedText.Unavailable.Should().Be("The ladder is unavailable.");
    }

    [Test]
    public void The_age_is_minutes_under_an_hour()
    {
        TamerRankedText.Age(0).Should().Be("0m");
        TamerRankedText.Age(5).Should().Be("5m");
        TamerRankedText.Age(59).Should().Be("59m");
    }

    [Test]
    public void The_age_is_hours_from_an_hour_to_a_day()
    {
        TamerRankedText.Age(60).Should().Be("1h");
        TamerRankedText.Age(119).Should().Be("1h");
        TamerRankedText.Age(120).Should().Be("2h");
        TamerRankedText.Age(1439).Should().Be("23h");
    }

    [Test]
    public void The_age_is_days_from_a_day_up()
    {
        TamerRankedText.Age(1440).Should().Be("1d");
        TamerRankedText.Age(4320).Should().Be("3d");
        TamerRankedText.Age(ushort.MaxValue).Should().Be("45d");
        TamerRankedText.Age(-5).Should().Be("0m");
    }

    [Test]
    public void The_season_line_counts_days()
    {
        TamerRankedText.SeasonLine(2, 23 * 86400).Should().Be("Season 2: 23 days left");
        TamerRankedText.SeasonLine(2, (23 * 86400) + 86399).Should().Be("Season 2: 23 days left");
        TamerRankedText.SeasonLine(2, 86400).Should().Be("Season 2: 1 day left");
    }

    [Test]
    public void The_season_line_counts_hours_under_a_day()
    {
        TamerRankedText.SeasonLine(2, 86399).Should().Be("Season 2: 23 hours left");
        TamerRankedText.SeasonLine(2, 2 * 3600).Should().Be("Season 2: 2 hours left");
        TamerRankedText.SeasonLine(2, 3600).Should().Be("Season 2: 1 hour left");
    }

    [Test]
    public void The_season_line_says_so_under_an_hour()
    {
        TamerRankedText.SeasonLine(2, 3599).Should().Be("Season 2: under 1 hour");
        TamerRankedText.SeasonLine(2, 0).Should().Be("Season 2: under 1 hour");
    }

    [Test]
    public void Step_names_run_from_copper_3_to_mythril_1()
    {
        TamerRankedText.StepName(0).Should().Be("Copper 3");
        TamerRankedText.StepName(2).Should().Be("Copper 1");
        TamerRankedText.StepName(3).Should().Be("Bronze 3");
        TamerRankedText.StepName(7).Should().Be("Silver 2");
        TamerRankedText.StepName(9).Should().Be("Gold 3");
        TamerRankedText.StepName(14).Should().Be("Mythril 1");
        TamerRankedText.StepName(200).Should().Be("Mythril 1");
    }

    [Test]
    public void The_standing_lines_match_the_spec()
    {
        TamerRankedText.PointsLine(40).Should().Be("40/100");
        TamerRankedText.RatingLine(1042).Should().Be("Rating 1042");
        TamerRankedText.BestLine(9).Should().Be("Best: Gold 3");
        TamerRankedText.AttacksLine(3, 5).Should().Be("Attacks left: 3 of 5");
    }

    [Test]
    public void The_points_fraction_is_clamped_to_the_bar()
    {
        TamerRankedText.PointsFraction(0).Should().Be(0);
        TamerRankedText.PointsFraction(40).Should().BeApproximately(0.4, 0.0001);
        TamerRankedText.PointsFraction(100).Should().Be(1);
        TamerRankedText.PointsFraction(250).Should().Be(1);
    }

    [Test]
    public void The_set_defence_question_names_the_squad_as_the_player_counts_it()
    {
        TamerRankedText.SetDefenceQuestion(1).Should().Be("Save squad 2 as your ranked defence?");
        TamerRankedText.SetDefenceQuestion(0).Should().Be("Save squad 1 as your ranked defence?");
    }

    [Test]
    public void The_attack_question_names_the_opponent_and_the_attacks_left()
    {
        TamerRankedText.AttackQuestion("Aoife", 3).Should().Be("Attack Aoife? This uses 1 of your 3 attacks today.");
        TamerRankedText.AttackQuestion("Bot-Dummy", 1).Should().Be("Attack Bot-Dummy? This uses 1 of your 1 attacks today.");
    }

    [Test]
    public void The_revenge_question_has_a_plain_and_a_warning_form()
    {
        TamerRankedText.RevengeQuestion("Cian", false).Should().Be("Take revenge on Cian? This does not use an attack.");

        TamerRankedText.RevengeQuestion("Cian", true)
                       .Should()
                       .Be("You and Cian have already fought today, so this revenge will change no rank and will be used up. Take it anyway?");
    }

    [Test]
    public void A_log_row_reads_age_attacker_result_and_change()
    {
        TamerRankedText.LogRow(Entry(TamerRankedLogFlags.AttackerWon)).Should().Be("2h   Aoife        won  -7");
        TamerRankedText.LogRow(Entry(TamerRankedLogFlags.None, 10, 300, "Bran")).Should().Be("5h   Bran         lost +10");
        TamerRankedText.LogRow(Entry(TamerRankedLogFlags.AttackerWon, 0, 5)).Should().Be("5m   Aoife        won  0");
    }

    [Test]
    public void A_log_row_for_every_flag_mix()
    {
        //replay kept (8), revenge available (4) and fought today (32) drive the buttons and the question, never the row
        foreach (var raw in Enumerable.Range(0, 128))
        {
            var flags = (TamerRankedLogFlags)raw;
            var row = TamerRankedText.LogRow(Entry(flags));

            var expected = "2h   Aoife        " + (flags.HasFlag(TamerRankedLogFlags.AttackerWon) ? "won  " : "lost ") + "-7";

            var tags = new List<string>();

            if ((raw & 2) != 0)
                tags.Add("revenge");

            if ((raw & 16) != 0)
                tags.Add("no rank");

            if ((raw & 64) != 0)
                tags.Add("revenged");

            var tag = string.Join(", ", tags);

            if (tag.Length > 0)
                expected += "   " + tag;

            row.Should().Be(expected, $"flags {raw}");
        }
    }

    [Test]
    public void Unknown_flag_bits_do_not_change_a_log_row()
    {
        TamerRankedText.LogRow(Entry((TamerRankedLogFlags)(1 | 128))).Should().Be(TamerRankedText.LogRow(Entry(TamerRankedLogFlags.AttackerWon)));
    }

    [Test]
    public void A_revenged_fall_is_tagged_revenged()
    {
        TamerRankedText.LogRow(Entry(TamerRankedLogFlags.AttackerWon | TamerRankedLogFlags.RevengeUsed)).Should().Be("2h   Aoife        won  -7   revenged");
        TamerRankedText.LogTag(TamerRankedLogFlags.RevengeUsed).Should().Be("revenged");
        TamerRankedText.LogTag(TamerRankedLogFlags.Revenge).Should().Be("revenge");
        TamerRankedText.LogTag(TamerRankedLogFlags.AttackerWon | TamerRankedLogFlags.RevengeAvailable).Should().BeEmpty();
    }

    [Test]
    public void The_daily_win_line_says_open_or_paid()
    {
        TamerRankedText.DailyWinLine(true).Should().Be("Daily win: open");
        TamerRankedText.DailyWinLine(false).Should().Be("Daily win: paid");
        ShouldFit(TamerRankedText.DailyWinLine(true), TamerRankedText.DAILY_WIN_WIDTH);
        ShouldFit(TamerRankedText.DailyWinLine(false), TamerRankedText.DAILY_WIN_WIDTH);
    }

    [Test]
    public void Every_refusal_the_server_can_send_fits_the_bottom_message_line()
    {
        //copied from the server at commit 9b8329707 and checked again at 7e546b8e6, where every line below is still
        //sent word for word; the tests may not reference its service classes. TamerLadderHandler.FriendlyLine's
        //shortened lines are not here: they answer Friendly on the orange bar and never reach this label
        string[] refusals =
        [
            //TamerStableHandler: DEAD_TEXT, LEDGER_GONE_TEXT, NO_LEDGER_TEXT, and RefusalText(BadName) for an over-long string
            "You can't use your stable while dead.",
            "You need your Stable Ledger for that.",
            "You need a Stable Ledger to do that.",
            "A name is 1 to 12 letters, with single spaces.",

            //TamerStable.UNLOADED_TEXT, sent by TamerLadderHandler.Run for a frozen stable
            "Your stable could not be loaded. Please tell a GM before binding anything.",

            //TamerLadderStore.Explain and its constants
            "The ladder could not be loaded. Please tell a GM.",
            "That name cannot be used.",
            "A ranked defence needs a squad of exactly five.",
            "You saved a defence a moment ago. Try again shortly.",
            "Save a squad of five as your defence to enter the ladder.",
            "You have used today's refresh.",
            "Nobody by that name is on the ladder.",
            "Fake opponents are switched off on this server.",
            "You have no ranked attacks left today.",
            "That opponent is no longer offered. Look again.",
            "That opponent cannot be fought right now.",
            "That defence has changed. Look again.",
            "You two have already fought today.",
            "That attack is no longer in your log.",
            "There is no revenge to take on that attack.",
            "The ladder changed. Try again.",
            "This replay is no longer kept.",

            //TamerFightService.Explain: the lines with no player name in them (the named ones only answer Friendly,
            //which is always an orange bar line and never reaches this label)
            "I cannot fight today.",
            "We have fought today. Come back tomorrow.",
            "Your active squad is empty.",
            "Catch your breath first.",
            "You cannot test a squad against your own.",
            "That squad does not exist.",
            "This replay is out of date and cannot be shown."
        ];

        refusals.Should().OnlyContain(text => TamerDrawableText.IsPrintableAscii(text));

        foreach (var refusal in refusals)
            ShouldFit(refusal, TamerRankedText.MESSAGE_WIDTH);

        //the longest of them leaves two characters to spare
        refusals.Max(text => text.Length).Should().Be(74);
    }

    [Test]
    public void The_columns_of_a_log_row_line_up()
    {
        var shortRow = TamerRankedText.LogRow(Entry(TamerRankedLogFlags.AttackerWon | TamerRankedLogFlags.Revenge, -7, 5, "Abe"));
        var longRow = TamerRankedText.LogRow(Entry(TamerRankedLogFlags.NoRankChange, -100, ushort.MaxValue, LongName));

        shortRow.IndexOf("Abe", StringComparison.Ordinal).Should().Be(longRow.IndexOf(LongName, StringComparison.Ordinal));
        shortRow.IndexOf("won", StringComparison.Ordinal).Should().Be(longRow.IndexOf("lost", StringComparison.Ordinal));
        shortRow.IndexOf("-7", StringComparison.Ordinal).Should().Be(longRow.IndexOf("-100", StringComparison.Ordinal));
        shortRow.IndexOf("revenge", StringComparison.Ordinal).Should().Be(longRow.IndexOf("no rank", StringComparison.Ordinal));
    }

    [Test]
    public void A_top_row_reads_place_name_and_step()
    {
        TamerRankedText.TopRow(1, new TamerRankedTopView("Aoife", 14, 100)).Should().Be(" 1 Aoife        Mythril 1");
        TamerRankedText.TopRow(10, new TamerRankedTopView(LongName, 10, 20)).Should().Be("10 Abcdefghijkl Gold 2");
    }

    [Test]
    public void A_missing_entry_gives_an_empty_row()
    {
        TamerRankedText.LogRow(null).Should().BeEmpty();
        TamerRankedText.TopRow(1, null).Should().BeEmpty();
    }

    [Test]
    public void Every_fixed_string_fits_its_label()
    {
        ShouldFit(TamerRankedText.Opponents, TamerRankedText.OPPONENTS_WIDTH);
        ShouldFit(TamerRankedText.YourDefence, TamerRankedText.LEFT_WIDTH);
        ShouldFit(TamerRankedText.SetFromSquad, TamerRankedText.SET_FROM_WIDTH);
        ShouldFit(TamerRankedText.TopTen, TamerRankedText.LEFT_WIDTH);
        ShouldFit(TamerRankedText.DefenceLog, TamerRankedText.RIGHT_WIDTH);
        ShouldFit(TamerRankedText.RevengeHint, TamerRankedText.HINT_WIDTH);
        ShouldFit(TamerRankedText.Reading, TamerRankedText.RIGHT_WIDTH);
        ShouldFit(TamerRankedText.NoOpponents, TamerRankedText.RIGHT_WIDTH);
        ShouldFit(TamerRankedText.NoDefence, TamerRankedText.RIGHT_WIDTH);
        ShouldFit(TamerRankedText.Unavailable, TamerRankedText.RIGHT_WIDTH);

        //captions are centred in their buttons with no padding of their own; keep a character clear each side
        foreach (var caption in new[] { TamerRankedText.Refresh, TamerRankedText.Attack })
            ((caption.Length + 2) * CHAR).Should().BeLessThanOrEqualTo(TamerRankedText.ACTION_BUTTON_WIDTH);

        foreach (var caption in new[] { TamerRankedText.Watch, TamerRankedText.Revenge })
            ((caption.Length + 2) * CHAR).Should().BeLessThanOrEqualTo(TamerRankedText.LOG_BUTTON_WIDTH);

        foreach (var squad in new byte[] { 0, 1, 2 })
            ((TamerRankedText.SquadButton(squad).Length + 2) * CHAR).Should().BeLessThanOrEqualTo(TamerRankedText.SQUAD_BUTTON_WIDTH);
    }

    [Test]
    public void The_defence_gone_line_wraps_inside_the_portrait_strip()
    {
        ShouldWrapInto(TamerRankedText.DefenceGone, TamerRankedText.LEFT_WIDTH, TamerRankedText.DEFENCE_GONE_LINES);
    }

    [Test]
    public void The_longest_built_lines_fit_their_labels()
    {
        ShouldFit(TamerRankedText.StepName(14), TamerRankedText.OFFER_NAME_WIDTH);
        ShouldFit(LongName, TamerRankedText.OFFER_NAME_WIDTH);
        ShouldFit(TamerRankedText.StepName(14), TamerRankedText.LEFT_WIDTH);
        ShouldFit(TamerRankedText.PointsLine(100), TamerRankedText.POINTS_WIDTH);
        ShouldFit(TamerRankedText.RatingLine(int.MinValue), TamerRankedText.RATING_WIDTH);
        ShouldFit(TamerRankedText.BestLine(14), TamerRankedText.LEFT_WIDTH);
        ShouldFit(TamerRankedText.SeasonLine(ushort.MaxValue, uint.MaxValue), TamerRankedText.LEFT_WIDTH);
        ShouldFit(TamerRankedText.SeasonLine(ushort.MaxValue, 86399), TamerRankedText.LEFT_WIDTH);
        ShouldFit(TamerRankedText.SeasonLine(ushort.MaxValue, 0), TamerRankedText.LEFT_WIDTH);
        ShouldFit(TamerRankedText.AttacksLine(255, 255), TamerRankedText.ATTACKS_WIDTH);
        ShouldFit(TamerRankedText.TopRow(10, new TamerRankedTopView(LongName, 14, 100)), TamerRankedText.LEFT_WIDTH);

        ShouldFit(
            TamerRankedText.LogRow(Entry((TamerRankedLogFlags)255, sbyte.MinValue, ushort.MaxValue, LongName)),
            TamerRankedText.LOG_ROW_WIDTH);
    }

    [Test]
    public void Every_question_fits_the_choice_box()
    {
        var questions = new[]
        {
            TamerRankedText.RefreshQuestion,
            TamerRankedText.SetDefenceQuestion(2),
            TamerRankedText.AttackQuestion(LongName, 255),
            TamerRankedText.RevengeQuestion(LongName, false),
            TamerRankedText.RevengeQuestion(LongName, true)
        };

        foreach (var question in questions)
            ShouldWrapInto(question, TamerRankedText.QUESTION_WIDTH, TamerRankedText.QUESTION_LINES);
    }

    [Test]
    public void Every_string_the_ranked_tab_draws_is_printable_ascii()
    {
        var texts = new List<string>(TamerDrawableText.Constants(typeof(TamerRankedText)))
        {
            TamerRankedText.Age(0),
            TamerRankedText.Age(59),
            TamerRankedText.Age(600),
            TamerRankedText.Age(ushort.MaxValue),
            TamerRankedText.SeasonLine(3, 5 * 86400),
            TamerRankedText.SeasonLine(3, 86400),
            TamerRankedText.SeasonLine(3, 7200),
            TamerRankedText.SeasonLine(3, 3600),
            TamerRankedText.SeasonLine(3, 10),
            TamerRankedText.PointsLine(40),
            TamerRankedText.RatingLine(-12),
            TamerRankedText.BestLine(9),
            TamerRankedText.AttacksLine(3, 5),
            TamerRankedText.DailyWinLine(true),
            TamerRankedText.DailyWinLine(false),
            TamerRankedText.SetDefenceQuestion(1),
            TamerRankedText.AttackQuestion("Bot-Dummy", 3),
            TamerRankedText.RevengeQuestion("Cian", false),
            TamerRankedText.RevengeQuestion("Cian", true),
            TamerRankedText.TopRow(1, new TamerRankedTopView("Aoife", 14, 100)),
            TamerRankedText.TopRow(10, new TamerRankedTopView(LongName, 0, 0)),
            TamerStableText.FitLines(TamerRankedText.DefenceGone, TamerRankedText.LEFT_WIDTH - 2, TamerRankedText.DEFENCE_GONE_LINES)
        };

        for (var step = 0; step <= 15; step++)
            texts.Add(TamerRankedText.StepName(step));

        foreach (var squad in new byte[] { 0, 1, 2 })
            texts.Add(TamerRankedText.SquadButton(squad));

        foreach (var raw in Enumerable.Range(0, 256))
            texts.Add(TamerRankedText.LogRow(Entry((TamerRankedLogFlags)raw, (sbyte)(raw - 128), (ushort)(raw * 257))));

        texts.Count.Should().BeGreaterThan(280);
        texts.Should().OnlyContain(text => TamerDrawableText.IsPrintableAscii(text));
    }
}
