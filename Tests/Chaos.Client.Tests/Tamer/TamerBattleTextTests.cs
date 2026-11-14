using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TamerBattleTextTests
{
    [Test]
    public void The_broken_message_matches_the_spec()
    {
        TamerBattleText.BrokenMessage.Should().Be("The fight could not be shown.");
    }

    [Test]
    public void Result_titles_follow_the_viewer_side()
    {
        TamerBattleText.ResultTitle(TamerBattleViewerSide.Attacker, TamerBattleSide.Attacker, "Attacker", "Defender").Should().Be("Victory");
        TamerBattleText.ResultTitle(TamerBattleViewerSide.Attacker, TamerBattleSide.Defender, "Attacker", "Defender").Should().Be("Defeat");
        TamerBattleText.ResultTitle(TamerBattleViewerSide.Defender, TamerBattleSide.Defender, "Attacker", "Defender").Should().Be("Victory");
        TamerBattleText.ResultTitle(TamerBattleViewerSide.Defender, TamerBattleSide.Attacker, "Attacker", "Defender").Should().Be("Defeat");
        TamerBattleText.ResultTitle(TamerBattleViewerSide.None, TamerBattleSide.Attacker, "Attacker", "Defender").Should().Be("Attacker wins");
        TamerBattleText.ResultTitle(TamerBattleViewerSide.None, TamerBattleSide.Defender, "Attacker", "Defender").Should().Be("Defender wins");
    }

    [Test]
    public void Result_titles_fall_back_to_side_names_when_squad_names_are_blank()
    {
        TamerBattleText.ResultTitle(TamerBattleViewerSide.None, TamerBattleSide.Attacker, "", "").Should().Be("Attacker wins");
        TamerBattleText.ResultTitle(TamerBattleViewerSide.None, TamerBattleSide.Defender, "", "").Should().Be("Defender wins");
    }

    [Test]
    public void Result_reasons_follow_the_end_reason()
    {
        TamerBattleText.ResultReason(TamerBattleEndReason.WipeOut).Should().Be("All foes down");
        TamerBattleText.ResultReason(TamerBattleEndReason.Time).Should().Be("Time: decided on remaining health");
    }

    [Test]
    public void Clock_text_is_seconds_with_one_decimal()
    {
        TamerBattleText.ClockText(0).Should().Be("0.0");
        TamerBattleText.ClockText(10).Should().Be("1.0");
        TamerBattleText.ClockText(123).Should().Be("12.3");
        TamerBattleText.ClockText(600).Should().Be("60.0");
        TamerBattleText.ClockText(-5).Should().Be("0.0");
    }

    [Test]
    public void Versus_text_names_both_squads()
    {
        TamerBattleText.VersusText("Attacker", "Defender").Should().Be("Attacker vs Defender");
        TamerBattleText.VersusText("", "").Should().Be("Attacker vs Defender");
    }

    [Test]
    public void Rising_text_is_empty_until_the_rise_starts()
    {
        TamerBattleText.RisingText(100).Should().BeEmpty();
        TamerBattleText.RisingText(110).Should().Be("Damage rising +10%");
        TamerBattleText.RisingText(250).Should().Be("Damage rising +150%");
    }

    [Test]
    public void Status_tags_are_short_and_stats_carry_a_sign()
    {
        TamerBattleText.StatusTag("Stun").Should().Be("Stun");
        TamerBattleText.StatusTag("Burn").Should().Be("Burn");
        TamerBattleText.StatusTag("Taunt").Should().Be("Taunt");
        TamerBattleText.StatusTag("Shield").Should().Be("Shield");
        TamerBattleText.StatusTag("AttackUp").Should().Be("+Atk");
        TamerBattleText.StatusTag("AttackDown").Should().Be("-Atk");
        TamerBattleText.StatusTag("ArmorUp").Should().Be("+Arm");
        TamerBattleText.StatusTag("ArmorDown").Should().Be("-Arm");
        TamerBattleText.StatusTag("SpeedUp").Should().Be("+Spd");
        TamerBattleText.StatusTag("SpeedDown").Should().Be("-Spd");
        TamerBattleText.StatusTag("oddkey").Should().Be("oddkey");
        TamerBattleText.StatusTag("").Should().BeEmpty();
        TamerBattleText.StatusTag(null).Should().BeEmpty();
    }

    [Test]
    public void Display_names_come_from_the_table_and_fall_back_to_the_key()
    {
        var names = new List<TamerBattleNameEntry> { new("rake", "Rake"), new("THORNS", "Thorns") };

        TamerBattleText.DisplayName("rake", names).Should().Be("Rake");
        TamerBattleText.DisplayName("thorns", names).Should().Be("Thorns");
        TamerBattleText.DisplayName("mystery", names).Should().Be("mystery");
        TamerBattleText.DisplayName("", names).Should().BeEmpty();
    }

    [Test]
    public void Log_lines_are_single_lines_with_the_names_placed()
    {
        TamerBattleText.AbilityLine("Alpha", "Rake").Should().Be("Alpha uses Rake");
        TamerBattleText.TraitLine("Alpha", "Thorns").Should().Be("Alpha: Thorns");
        TamerBattleText.SetBonusLine("Alpha", "Fire bond").Should().Be("Alpha: Fire bond");
        TamerBattleText.DownLine("Alpha").Should().Be("Alpha falls");
    }

    [Test]
    public void Level_health_speed_and_element_text()
    {
        TamerBattleText.LevelText(7).Should().Be("Lv 7");
        TamerBattleText.HealthText(70, 100).Should().Be("70 / 100");
        TamerBattleText.SpeedText(2).Should().Be("2x");
        TamerBattleText.ElementName(TamerWireElement.Fire).Should().Be("Fire");
        TamerBattleText.ElementName(TamerWireElement.Water).Should().Be("Water");
        TamerBattleText.ElementName(TamerWireElement.Wind).Should().Be("Wind");
        TamerBattleText.ElementName(TamerWireElement.Earth).Should().Be("Earth");
    }
}
