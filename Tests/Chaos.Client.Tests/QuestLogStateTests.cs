using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class QuestLogStateTests
{
    private static QuestLogEntryInfo Entry(string key, bool canGiveUp = true, params QuestLogProgressInfo[] progress)
        => new()
        {
            Key = key,
            Title = key.ToUpperInvariant(),
            Area = "Mileth",
            GivenBy = "Noahn",
            Text = $"Do {key}.",
            CanGiveUp = canGiveUp,
            Progress = [..progress]
        };

    [Test]
    public async Task Apply_selects_the_first_quest_when_none_is_selected()
    {
        var state = new QuestLogState();

        state.Apply([Entry("a"), Entry("b")]);

        state.SelectedKey.Should().Be("a");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Apply_keeps_the_selected_quest_across_an_update()
    {
        var state = new QuestLogState();
        state.Apply([Entry("a"), Entry("b")]);
        state.Select("b");

        state.Apply([Entry("c"), Entry("b"), Entry("a")]);

        state.SelectedKey.Should().Be("b");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Apply_falls_back_to_the_first_row_when_the_selected_quest_is_gone()
    {
        var state = new QuestLogState();
        state.Apply([Entry("a"), Entry("b")]);
        state.Select("b");

        state.Apply([Entry("c"), Entry("a")]);

        state.SelectedKey.Should().Be("c");

        await Task.CompletedTask;
    }

    [Test]
    public async Task An_empty_list_shows_the_empty_text()
    {
        var state = new QuestLogState();

        state.Apply([]);

        state.SelectedKey.Should().BeNull();
        state.CanGiveUp.Should().BeFalse();
        state.DetailsText().Should().Be("{=iYou have no active quests.");

        await Task.CompletedTask;
    }

    [Test]
    public async Task PressGiveUp_arms_then_returns_the_key()
    {
        var state = new QuestLogState();
        state.Apply([Entry("a")]);

        state.PressGiveUp().Should().BeNull();
        state.ConfirmingKey.Should().Be("a");
        state.DetailsText().Should().Be("{=uClick GiveUp again to abandon A.");

        state.PressGiveUp().Should().Be("a");
        state.ConfirmingKey.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task PressGiveUp_does_nothing_when_the_quest_cannot_be_given_up()
    {
        var state = new QuestLogState();
        state.Apply([Entry("a", false)]);

        state.PressGiveUp().Should().BeNull();
        state.PressGiveUp().Should().BeNull();
        state.ConfirmingKey.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_confirm_ends_after_five_seconds()
    {
        var state = new QuestLogState();
        state.Apply([Entry("a")]);
        state.PressGiveUp();

        state.Update(4.9f);
        state.ConfirmingKey.Should().Be("a");

        state.Update(0.2f);
        state.ConfirmingKey.Should().BeNull();
        state.PressGiveUp().Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Selecting_another_quest_cancels_the_confirm()
    {
        var state = new QuestLogState();
        state.Apply([Entry("a"), Entry("b")]);
        state.PressGiveUp();

        state.Select("b");

        state.ConfirmingKey.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_confirm_ends_when_its_quest_disappears()
    {
        var state = new QuestLogState();
        state.Apply([Entry("a"), Entry("b")]);
        state.PressGiveUp();

        state.Apply([Entry("b")]);

        state.ConfirmingKey.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task DetailsText_shows_title_giver_text_and_progress()
    {
        var state = new QuestLogState();
        state.Apply([Entry("a", true, new QuestLogProgressInfo("Horns", 3, 5), new QuestLogProgressInfo("Runs", 10, 10))]);

        state.DetailsText()
             .Should()
             .Be("{=cA\n{=iNoahn\n\n{=uDo a.\n\n{=uHorns: 3 / 5\n{=qRuns: 10 / 10");

        await Task.CompletedTask;
    }
}
