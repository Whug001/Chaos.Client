using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class PartyListDocumentTests
{
    private static PartyListDocument Fresh(PartyListKind kind = PartyListKind.Quip)
    {
        var document = new PartyListDocument();
        document.New(kind);

        return document;
    }

    private static CollegePartyListDraftInfo Draft(PartyListKind kind, int entries)
        => new()
        {
            Kind = kind,
            Title = "Mileth",
            Entries = Enumerable
                .Range(0, entries)
                .Select(i => new CollegePartyEntryInfo
                {
                    Text = $"entry {i}",
                    Answer = $"answer {i}",
                    Decoys = [$"decoy {i}a", $"decoy {i}b"]
                })
                .ToList()
        };

    [Test]
    public void New_gives_one_blank_entry_that_is_clean()
    {
        var document = Fresh();

        document.Kind.Should().Be(PartyListKind.Quip);
        document.Entries.Should().ContainSingle();
        document.Selected.Should().Be(0);
        document.Id.Should().BeEmpty();
        document.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Entry_caps_follow_the_kind()
    {
        Fresh(PartyListKind.Quip).MaxEntries.Should().Be(50);
        Fresh(PartyListKind.Sketch).MaxEntries.Should().Be(50);
        Fresh(PartyListKind.Bluff).MaxEntries.Should().Be(30);
    }

    [Test]
    public void Text_caps_follow_the_kind()
    {
        Fresh(PartyListKind.Quip).MaxTextChars.Should().Be(80);
        Fresh(PartyListKind.Bluff).MaxTextChars.Should().Be(120);
        Fresh(PartyListKind.Sketch).MaxTextChars.Should().Be(30);
    }

    [Test]
    public void Load_then_ToInfo_round_trips()
    {
        var document = new PartyListDocument();
        document.Load("abc", Draft(PartyListKind.Bluff, 3));

        document.Id.Should().Be("abc");
        document.Kind.Should().Be(PartyListKind.Bluff);
        document.IsDirty.Should().BeFalse();

        var info = document.ToInfo();

        info.Kind.Should().Be(PartyListKind.Bluff);
        info.Title.Should().Be("Mileth");
        info.Entries.Select(e => (e.Text, e.Answer)).Should().Equal(Draft(PartyListKind.Bluff, 3).Entries.Select(e => (e.Text, e.Answer)));
        info.Entries[0].Decoys.Should().Equal("decoy 0a", "decoy 0b");
    }

    [Test]
    public void Load_caps_entries_text_answers_and_decoys()
    {
        var document = new PartyListDocument();
        var draft = Draft(PartyListKind.Bluff, 35);
        draft.Entries[0].Text = new string('q', 200);
        draft.Entries[0].Answer = new string('a', 60);
        draft.Entries[0].Decoys = ["1", "2", "3", "4", new string('d', 60)];

        document.Load("a", draft);

        document.Entries.Should().HaveCount(30);
        document.Entries[0].Text.Should().HaveLength(120);
        document.Entries[0].Answer.Should().HaveLength(40);
        document.Entries[0].Decoys.Should().HaveCount(3);
    }

    [Test]
    public void Load_caps_sketch_text_at_30()
    {
        var document = new PartyListDocument();
        var draft = Draft(PartyListKind.Sketch, 1);
        draft.Entries[0].Text = new string('s', 60);

        document.Load("a", draft);

        document.Entries[0].Text.Should().HaveLength(30);
    }

    [Test]
    public void Add_stops_at_the_kind_cap_and_selects_the_new_entry()
    {
        var document = Fresh(PartyListKind.Bluff);

        for (var i = 1; i < 30; i++)
            document.Add().Should().BeTrue();

        document.Add().Should().BeFalse();
        document.Entries.Should().HaveCount(30);
        document.Selected.Should().Be(29);
    }

    [Test]
    public void Quip_adds_stop_at_50()
    {
        var document = Fresh(PartyListKind.Quip);

        for (var i = 1; i < 50; i++)
            document.Add().Should().BeTrue();

        document.Add().Should().BeFalse();
        document.Entries.Should().HaveCount(50);
    }

    [Test]
    public void Remove_keeps_the_selection_in_range()
    {
        var document = new PartyListDocument();
        document.Load("a", Draft(PartyListKind.Quip, 3));
        document.Select(2);

        document.Remove().Should().BeTrue();

        document.Entries.Should().HaveCount(2);
        document.Selected.Should().Be(1);
    }

    [Test]
    public void Remove_of_the_last_entry_leaves_a_blank_one()
    {
        var document = Fresh();
        document.SetText("prompt");

        document.Remove().Should().BeTrue();

        document.Entries.Should().ContainSingle();
        document.Current!.Text.Should().BeEmpty();
    }

    [Test]
    public void Move_swaps_and_follows_the_selection()
    {
        var document = new PartyListDocument();
        document.Load("a", Draft(PartyListKind.Quip, 3));

        document.MoveUp().Should().BeFalse();
        document.MoveDown().Should().BeTrue();

        document.Selected.Should().Be(1);
        document.Entries[1].Text.Should().Be("entry 0");
        document.IsDirty.Should().BeTrue();
    }

    [Test]
    public void Preview_mode_refuses_every_edit_but_selects()
    {
        var document = new PartyListDocument();
        document.Load("a", Draft(PartyListKind.Quip, 3), true);
        document.Select(1);

        document.IsReadOnly.Should().BeTrue();
        document.Selected.Should().Be(1);
        document.Add().Should().BeFalse();
        document.Remove().Should().BeFalse();
        document.MoveUp().Should().BeFalse();
        document.MoveDown().Should().BeFalse();
        document.SetTitle("X").Should().BeFalse();
        document.SetText("text").Should().BeFalse();
        document.SetAnswer("answer").Should().BeFalse();
        document.AddDecoy().Should().BeFalse();
        document.SetDecoy(0, "decoy").Should().BeFalse();
        document.RemoveDecoy(0).Should().BeFalse();
        document.IsDirty.Should().BeFalse();
        document.Entries.Should().HaveCount(3);
    }

    [Test]
    public void Text_edits_cut_at_the_kind_cap_and_mark_dirty()
    {
        var document = Fresh(PartyListKind.Quip);

        document.SetText(new string('q', 100)).Should().BeTrue();

        document.Current!.Text.Should().HaveLength(80);
        document.IsDirty.Should().BeTrue();
    }

    [Test]
    public void Answers_and_decoys_cut_at_40_with_three_decoys()
    {
        var document = Fresh(PartyListKind.Bluff);

        document.SetAnswer(new string('a', 60)).Should().BeTrue();
        document.Current!.Answer.Should().HaveLength(40);

        document.AddDecoy().Should().BeTrue();
        document.AddDecoy().Should().BeTrue();
        document.AddDecoy().Should().BeTrue();
        document.AddDecoy().Should().BeFalse();

        document.SetDecoy(0, new string('d', 60)).Should().BeTrue();
        document.Current.Decoys[0].Should().HaveLength(40);

        document.RemoveDecoy(0).Should().BeTrue();
        document.Current.Decoys.Should().HaveCount(2);
        document.RemoveDecoy(9).Should().BeFalse();
    }

    [Test]
    public void Setting_the_same_text_is_not_a_change()
    {
        var document = Fresh();

        document.SetTitle("").Should().BeFalse();
        document.SetText("").Should().BeFalse();
        document.SetAnswer("").Should().BeFalse();
        document.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Labels_number_the_entries()
    {
        var document = Fresh();
        document.SetText("Who?");
        document.Add();

        document.Label(0).Should().Be("1. Who?");
        document.Label(1).Should().Be("2. (empty)");
    }

    [Test]
    public void New_clears_the_loaded_id_title_and_entries()
    {
        var document = new PartyListDocument();
        document.Load("abc", Draft(PartyListKind.Bluff, 2));

        document.New(PartyListKind.Quip);

        document.Id.Should().BeEmpty();
        document.Title.Should().BeEmpty();
        document.Kind.Should().Be(PartyListKind.Quip);
        document.Entries.Should().ContainSingle();
        document.Current!.Text.Should().BeEmpty();
        document.IsDirty.Should().BeFalse();
    }
}
