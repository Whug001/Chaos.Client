using Chaos.Client.ViewModel;
using Chaos.Client.ViewModel.CommissionBoard;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class CommissionBoardStateTests
{
    private static CommissionBoardRow Row(
        string id,
        CommissionBoardRowState state = CommissionBoardRowState.Open,
        string buyer = "Buyer",
        string namedArtist = "",
        string claimedBy = "",
        bool redoUsed = false,
        uint artistGold = 0,
        bool buyerItemWaiting = false,
        bool artistItemWaiting = false,
        string artistItemOwner = "")
        => new()
        {
            Id = id,
            Buyer = buyer,
            Description = $"Portrait {id}",
            OfferGold = 500,
            NamedArtist = namedArtist,
            State = state,
            ClaimedBy = claimedBy,
            ClaimDeadline = 0,
            RedoUsed = redoUsed,
            PortraitHash = string.Empty,
            PainterName = string.Empty,
            ArtistGoldCollectable = artistGold,
            BuyerItemWaiting = buyerItemWaiting,
            ArtistItemWaiting = artistItemWaiting,
            ArtistItemOwner = artistItemOwner
        };

    private static CommissionBoardDisplayArgs Display(
        IEnumerable<CommissionBoardRow>? open = null,
        IEnumerable<CommissionBoardRow>? myRequests = null,
        IEnumerable<CommissionBoardRow>? myCommissions = null)
        => new()
        {
            Open = open?.ToList() ?? [],
            MyRequests = myRequests?.ToList() ?? [],
            MyCommissions = myCommissions?.ToList() ?? []
        };

    private static CommissionBoardState StateFor(string viewer, CommissionBoardDisplayArgs display)
    {
        var state = new CommissionBoardState();
        state.Apply(display, viewer);

        return state;
    }

    [Test]
    public async Task Named_rows_show_only_for_that_artist()
    {
        var state = StateFor("Artist", Display(open: [Row("r1", namedArtist: "Artist"), Row("r2", namedArtist: "Other")]));

        state.OpenRequests.Select(r => r.Id).Should().Equal("r1");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Unnamed_rows_show_for_everyone()
    {
        var state = StateFor("Anyone", Display(open: [Row("r1")]));

        state.OpenRequests.Select(r => r.Id).Should().Equal("r1");

        await Task.CompletedTask;
    }

    [Test]
    public async Task My_lists_show_every_row_the_server_sent()
    {
        var state = StateFor(
            "Me",
            Display(
                myRequests: [Row("r1", CommissionBoardRowState.Closed), Row("r2", CommissionBoardRowState.Accepted)],
                myCommissions: [Row("r3", CommissionBoardRowState.Closed)]));

        state.MyRequests.Select(r => r.Id).Should().Equal("r1", "r2");
        state.MyCommissions.Select(r => r.Id).Should().Equal("r3");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Refresh_keeps_the_selected_row_by_id()
    {
        var state = StateFor("Buyer", Display(myRequests: [Row("r1", buyer: "Buyer"), Row("r2", buyer: "Buyer")]));
        state.SelectTab(CommissionBoardTab.MyRequests);
        state.Select("r2");

        state.Apply(Display(myRequests: [Row("r2", buyer: "Buyer"), Row("r3", buyer: "Buyer")]), "Buyer");

        state.SelectedTab.Should().Be(CommissionBoardTab.MyRequests);
        state.SelectedId.Should().Be("r2");
        state.SelectedRow!.Id.Should().Be("r2");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Refresh_clears_the_selection_when_the_row_is_gone()
    {
        var state = StateFor("Buyer", Display(myRequests: [Row("r1", buyer: "Buyer")]));
        state.Select("r1");

        state.Apply(Display(myRequests: [Row("r2", buyer: "Buyer")]), "Buyer");

        state.SelectedId.Should().BeNull();
        state.SelectedRow.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Current_rows_follow_the_selected_tab()
    {
        var state = StateFor(
            "Me",
            Display(open: [Row("o")], myRequests: [Row("r", buyer: "Me")], myCommissions: [Row("c", claimedBy: "Me")]));

        state.CurrentRows.Select(r => r.Id).Should().Equal("o");

        state.SelectTab(CommissionBoardTab.MyRequests);
        state.CurrentRows.Select(r => r.Id).Should().Equal("r");

        state.SelectTab(CommissionBoardTab.MyCommissions);
        state.CurrentRows.Select(r => r.Id).Should().Equal("c");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Open_offers_claim_to_others_and_cancel_to_the_buyer()
    {
        var other = StateFor("Other", Display(open: [Row("r1", buyer: "Buyer")]));
        var buyer = StateFor("Buyer", Display(open: [Row("r1", buyer: "Buyer")]));

        other.ActionsFor(other.OpenRequests[0]).Should().Equal(CommissionBoardRowAction.Claim);
        buyer.ActionsFor(buyer.OpenRequests[0]).Should().Equal(CommissionBoardRowAction.Cancel);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Claimed_offers_drop_and_deliver_to_the_claimant_only()
    {
        var row = Row("r1", CommissionBoardRowState.Claimed, buyer: "Buyer", claimedBy: "Artist");
        var artist = StateFor("Artist", Display());
        var buyer = StateFor("Buyer", Display());
        var stranger = StateFor("Stranger", Display());

        artist.ActionsFor(row).Should().Equal(CommissionBoardRowAction.Drop, CommissionBoardRowAction.Deliver);
        buyer.ActionsFor(row).Should().BeEmpty();
        stranger.ActionsFor(row).Should().BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Delivered_offers_review_to_the_buyer_only()
    {
        var row = Row("r1", CommissionBoardRowState.Delivered, buyer: "Buyer", claimedBy: "Artist");
        var buyer = StateFor("Buyer", Display());
        var artist = StateFor("Artist", Display());

        buyer.ActionsFor(row).Should().Equal(CommissionBoardRowAction.Review);
        artist.ActionsFor(row).Should().BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Redo_is_allowed_once()
    {
        var state = StateFor("Buyer", Display());
        var fresh = Row("r1", CommissionBoardRowState.Delivered, buyer: "Buyer", claimedBy: "Artist");
        var used = Row("r2", CommissionBoardRowState.Delivered, buyer: "Buyer", claimedBy: "Artist", redoUsed: true);

        state.CanRedo(fresh).Should().BeTrue();
        state.CanRedo(used).Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Collect_gold_is_offered_to_the_claimant()
    {
        var row = Row("r1", CommissionBoardRowState.Accepted, buyer: "Buyer", claimedBy: "Artist", artistGold: 500);
        var artist = StateFor("Artist", Display());
        var buyer = StateFor("Buyer", Display());

        artist.ActionsFor(row).Should().Equal(CommissionBoardRowAction.CollectGold);
        buyer.ActionsFor(row).Should().BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Collect_item_is_offered_to_the_waiting_side()
    {
        var buyerRow = Row("r1", CommissionBoardRowState.Accepted, buyer: "Buyer", buyerItemWaiting: true);
        var artistRow = Row(
            "r2",
            CommissionBoardRowState.Claimed,
            buyer: "Buyer",
            claimedBy: "Artist",
            artistItemWaiting: true,
            artistItemOwner: "Artist");
        var buyer = StateFor("Buyer", Display());
        var artist = StateFor("Artist", Display());

        buyer.ActionsFor(buyerRow).Should().Equal(CommissionBoardRowAction.CollectItem);
        artist.ActionsFor(buyerRow).Should().BeEmpty();
        artist.ActionsFor(artistRow)
             .Should()
             .Equal(CommissionBoardRowAction.Drop, CommissionBoardRowAction.Deliver, CommissionBoardRowAction.CollectItem);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Closed_rows_offer_collect_item_only()
    {
        var state = StateFor("Buyer", Display());
        var waiting = Row("r1", CommissionBoardRowState.Closed, buyer: "Buyer", buyerItemWaiting: true);
        var quiet = Row("r2", CommissionBoardRowState.Closed, buyer: "Buyer");

        state.ActionsFor(waiting).Should().Equal(CommissionBoardRowAction.CollectItem);
        state.ActionsFor(quiet).Should().BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Closed_rows_offer_no_collect_gold()
    {
        var state = StateFor("Artist", Display());
        var row = Row("r1", CommissionBoardRowState.Closed, buyer: "Buyer", claimedBy: "Artist", artistGold: 500);

        state.ActionsFor(row).Should().BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Blank_descriptions_are_refused()
    {
        CommissionBoardState.ValidatePost(string.Empty, "500", out _).Should().NotBeNull();
        CommissionBoardState.ValidatePost("   ", "500", out _).Should().NotBeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Descriptions_cap_at_200_chars()
    {
        CommissionBoardState.ValidatePost(new string('a', 200), "500", out _).Should().BeNull();
        CommissionBoardState.ValidatePost(new string('a', 201), "500", out _).Should().NotBeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Descriptions_cap_at_255_packet_bytes()
    {
        CommissionBoardState.ValidatePost(new string('가', 127), "500", out _).Should().BeNull();
        CommissionBoardState.ValidatePost(new string('가', 128), "500", out _).Should().NotBeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Gold_must_be_between_1_and_2147483647()
    {
        CommissionBoardState.ValidatePost("A portrait", "0", out _).Should().NotBeNull();
        CommissionBoardState.ValidatePost("A portrait", "2147483648", out _).Should().NotBeNull();
        CommissionBoardState.ValidatePost("A portrait", "lots", out _).Should().NotBeNull();
        CommissionBoardState.ValidatePost("A portrait", string.Empty, out _).Should().NotBeNull();
        CommissionBoardState.ValidatePost("A portrait", "2147483647", out var gold).Should().BeNull();

        gold.Should().Be(2147483647);

        await Task.CompletedTask;
    }

    [Test]
    public async Task TryBuildPost_fills_the_action()
    {
        CommissionBoardState.TryBuildPost("A portrait", "500", " Artist ", out var args, out var error)
                            .Should()
                            .BeTrue();

        error.Should().BeEmpty();
        args.Action.Should().Be(CommissionBoardActionType.Post);
        args.Description.Should().Be("A portrait");
        args.Gold.Should().Be(500);
        args.Artist.Should().Be("Artist");

        await Task.CompletedTask;
    }

    [Test]
    public async Task TryBuildPost_keeps_an_empty_artist()
    {
        CommissionBoardState.TryBuildPost("A portrait", "500", string.Empty, out var args, out _).Should().BeTrue();

        args.Artist.Should().BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Redo_notes_cap_at_200_chars_and_accept_empty()
    {
        CommissionBoardState.ValidateRedoNote(string.Empty).Should().BeNull();
        CommissionBoardState.ValidateRedoNote(new string('a', 200)).Should().BeNull();
        CommissionBoardState.ValidateRedoNote(new string('a', 201)).Should().NotBeNull();
        CommissionBoardState.ValidateRedoNote(new string('가', 128)).Should().NotBeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Packet_bytes_count_korean_as_two()
    {
        CommissionBoardState.PacketByteCount("abc").Should().Be(3);
        CommissionBoardState.PacketByteCount("가").Should().Be(2);

        await Task.CompletedTask;
    }

    [Test]
    public async Task ActionArgs_maps_every_button_action()
    {
        CommissionBoardState.ActionArgs(CommissionBoardRowAction.Claim, "id").Action.Should().Be(CommissionBoardActionType.Claim);
        CommissionBoardState.ActionArgs(CommissionBoardRowAction.Cancel, "id").Action.Should().Be(CommissionBoardActionType.Cancel);
        CommissionBoardState.ActionArgs(CommissionBoardRowAction.Drop, "id").Action.Should().Be(CommissionBoardActionType.Drop);
        CommissionBoardState.ActionArgs(CommissionBoardRowAction.Deliver, "id", 7)
                              .Should()
                              .BeEquivalentTo(
                                  new CommissionBoardActionArgs
                                  {
                                      Action = CommissionBoardActionType.Deliver,
                                      RequestId = "id",
                                      Slot = 7
                                  });
        CommissionBoardState.ActionArgs(CommissionBoardRowAction.Accept, "id").Action.Should().Be(CommissionBoardActionType.Accept);
        CommissionBoardState.ActionArgs(CommissionBoardRowAction.Redo, "id").Action.Should().Be(CommissionBoardActionType.Redo);
        CommissionBoardState.ActionArgs(CommissionBoardRowAction.CollectGold, "id").Action.Should().Be(CommissionBoardActionType.CollectGold);
        CommissionBoardState.ActionArgs(CommissionBoardRowAction.CollectItem, "id").Action.Should().Be(CommissionBoardActionType.CollectItem);

        await Task.CompletedTask;
    }

    [Test]
    public async Task ActionArgs_refuses_the_ui_only_review()
    {
        var act = () => CommissionBoardState.ActionArgs(CommissionBoardRowAction.Review, "id");

        act.Should().Throw<ArgumentOutOfRangeException>();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Deliver_choices_list_only_my_painted_portraits()
    {
        var inventory = new Inventory();
        inventory.SetSlot(1, 100, DisplayColor.Default, "Portrait", false, 1, 0, 0, "hash1", "Me");
        inventory.SetSlot(2, 100, DisplayColor.Default, "Portrait", false, 1, 0, 0, "hash2", "Other");
        inventory.SetSlot(3, 100, DisplayColor.Default, "Sword", false, 1, 0, 0);
        inventory.SetSlot(4, 100, DisplayColor.Default, "Portrait", false, 1, 0, 0, "hash4", "me");

        var choices = CommissionBoardState.DeliverChoices(inventory, "Me");

        choices.Select(c => c.Slot).Should().Equal((byte)1, (byte)4);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Deadline_text_names_the_unit_or_empties()
    {
        CommissionBoardState.DeadlineText(0, DateTimeOffset.UtcNow).Should().BeEmpty();
        CommissionBoardState.DeadlineText((ulong)DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(), DateTimeOffset.UtcNow)
                            .Should()
                            .Be("Expired");
        CommissionBoardState.DeadlineText((ulong)DateTimeOffset.UtcNow.AddDays(3).ToUnixTimeSeconds(), DateTimeOffset.UtcNow)
                            .Should()
                            .EndWith("left");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Switching_tabs_clears_a_selection_from_another_tab()
    {
        var state = StateFor("Me", Display(open: [Row("o")], myRequests: [Row("r", buyer: "Me")]));
        state.Select("o");
        state.SelectedRow!.Id.Should().Be("o");

        state.SelectTab(CommissionBoardTab.MyRequests);

        state.SelectedId.Should().BeNull();
        state.SelectedRow.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Selected_row_looks_only_in_the_current_tab()
    {
        var state = StateFor("Me", Display(open: [Row("o")], myRequests: [Row("r", buyer: "Me")]));
        state.Select("r");

        state.SelectedId.Should().Be("r");
        state.SelectedRow.Should().BeNull();

        state.SelectTab(CommissionBoardTab.MyRequests);

        state.SelectedRow!.Id.Should().Be("r");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Details_text_puts_state_and_redo_note_before_the_description()
    {
        var state = StateFor("Buyer", Display());
        var row = Row("r1", CommissionBoardRowState.Delivered, buyer: "Buyer", claimedBy: "Artist");
        row.RedoNote = "softer light";

        var lines = state.DetailsText(row).Split('\n');

        lines[0].Should().Be("Delivered - waiting review");
        lines[1].Should().Be("Redo note: softer light");
        lines[2].Should().Be("Portrait r1");
        lines[3].Should().Be("Buyer Buyer - 500 gold");

        await Task.CompletedTask;
    }
}
