#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering.Utility;
using Chaos.Client.Systems;
using Chaos.Client.Utilities;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups.Poker;

/// <summary>
///     The fixed-limit hold-em table window: six seat boxes ringed around a community board, the pot beneath it, a
///     one-line action log, and the five action buttons plus the table controls (Leave / Sit Out / Sit In). Reuses
///     the ornate dialog frame (see <see cref="FramedDialogPanelBase" />) the same way
///     <see cref="Slots.SlotMachineControl" /> and <see cref="Wheel.GildedSpindleControl" /> do, and is mounted on
///     WorldScreen.Root and opened by the server's poker Open display when the player sits at a table's stool.
/// </summary>
/// <remarks>
///     <para>
///         <b>Pure renderer, never a decider.</b> Everything on this panel comes out of
///         <see cref="WorldState.PokerTable" />, which is populated wholesale by the server's Snapshot display.
///         There is no client-side legality rule anywhere in this file: the action buttons are enabled from
///         <see cref="ViewModel.PokerTable.LegalActions" /> and nothing else, because the server re-checks every
///         action it receives. A stale hint costs one refused click; a client rule that disagrees with the server
///         looks like a bug forever.
///     </para>
///     <para>
///         <b>Snapshots are absolute.</b> <see cref="OnSnapshot" /> repaints every child from the current view
///         model, unconditionally. Nothing here accumulates or merges across calls, and in particular no seat's
///         hole cards are ever carried forward from an earlier snapshot -- see <see cref="SeatPanel.Apply" />.
///     </para>
///     <para>
///         <b>Cards are never inferred.</b> <see cref="PokerSeatInfo.HoleCards" /> is populated by the server only
///         for the recipient's own seat, and for every unfolded seat at showdown. An empty list means face-down,
///         and this control draws a card back for it -- it never guesses a rank, never caches one, and never
///         reconstructs a seat's holding from the board or from a previous snapshot. The server's per-recipient
///         filtering is the whole security model of the feature; a client that filled in the blanks would defeat
///         it entirely.
///     </para>
///     <para>
///         <b>The two absent-index cases.</b> <see cref="ViewModel.PokerTable.ButtonIndex" /> and
///         <see cref="ViewModel.PokerTable.ActorIndex" /> are nullable bytes, and <see langword="null" /> genuinely
///         means "there is no button" / "nobody is on the clock" -- between hands, and before the first hand of a
///         session. Neither is ever coalesced to zero here: a null button hides the dealer badge on every seat, and
///         a null actor hides the shot clock and the acting-seat outline on every seat. Clamping either to seat 0
///         would put the badge, the highlight, and a live countdown on a player who has none of them.
///     </para>
///     <para>
///         <b>Why the cards are drawn rather than blitted.</b> <see cref="Slots.ReelControl" /> renders its symbols
///         through the injected <c>CreatureRenderer</c> -- a lookup keyed by creature sprite id. There is no card
///         artwork in the DAT archives (<c>controlFileList.txt</c> lists none) and no card-index-to-sprite mapping
///         anywhere in the protocol, and this control is not handed a <c>CreatureRenderer</c> at all, so that path
///         has nothing to resolve for a playing card. <see cref="CardView" /> therefore composes its two faces from
///         the primitives this control family already uses -- <see cref="ImageUtil" /> pixel fills plus a
///         <see cref="UILabel" /> pair on the shared <see cref="TextRenderer" /> font -- which introduces no second
///         asset path, because it introduces no asset path at all.
///     </para>
/// </remarks>
public sealed class PokerTableControl : FramedDialogPanelBase
{
    //── the PokerAction byte values, mirrored from the server's Chaos.Services.Poker.PokerAction ──
    //That enum lives in the server-only `Chaos` project, which the client does not reference (see the dependency
    //list in CLAUDE.md), which is exactly why ActionRequested carries a raw byte rather than the enum. These are
    //the wire values; they must stay in step with the server's enum, and the server rejects anything else.
    private const byte ACTION_FOLD = 0;
    private const byte ACTION_CHECK = 1;
    private const byte ACTION_CALL = 2;
    private const byte ACTION_BET = 3;
    private const byte ACTION_RAISE = 4;

    private const int SEATS_PER_ROW = 3;

    /// <summary>The number of seat rows this layout draws -- one above the board and one below it.</summary>
    private const int SEAT_ROWS = 2;

    /// <summary>
    ///     The table's fixed seat count -- six, matching the shipped hold-em table's <c>seatCount</c>.
    /// </summary>
    /// <remarks>
    ///     DERIVED from the row geometry rather than written as a literal 6, so the two can never drift apart.
    ///     <see cref="SeatScreenSlot" />'s arithmetic is only correct while the seats fill exactly
    ///     <see cref="SEAT_ROWS" /> rows of <see cref="SEATS_PER_ROW" />; when this was an independent literal, a
    ///     seventh seat would have been placed at column -1 -- silently off the left edge of the panel -- rather
    ///     than reported. Raising the count now means changing the geometry that actually draws it.
    /// </remarks>
    private const int SEAT_COUNT = SEATS_PER_ROW * SEAT_ROWS;

    /// <summary>The community board never exceeds five cards (flop, turn, river).</summary>
    private const int BOARD_SIZE = 5;

    //── canonical panel size ──
    //Both dimensions are DERIVED from the content, never pinned: SlotMachineControl's remarks at its own
    //PANEL_WIDTH explain what pinning a total costs -- a gap tightened later leaves a bare patch behind instead
    //of shrinking the window. Width is one seat row plus its margins; height runs from the title down to the
    //table-control row, plus the ornate frame's own bottom border.
    private const int CONTENT_LEFT = 20;
    private const int CONTENT_RIGHT = 20;

    //FramedDialogPanelBase paints a 47px ornate bottom border over the panel's last 47 rows (rivet strip plus the
    //Close button), so content has to end above it. Budgeted by name rather than eyeballed.
    private const int FRAME_BOTTOM_BORDER = 47;

    //centered horizontally but pinned this far from the top, matching Slots/Wheel.
    private const int TOP_MARGIN = 15;

    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    //── seat box ──
    private const int SEAT_WIDTH = 128;
    private const int SEAT_HEIGHT = 56;
    private const int SEAT_GAP = 10;
    private const int SEAT_ROW_WIDTH = (SEAT_WIDTH * SEATS_PER_ROW) + (SEAT_GAP * (SEATS_PER_ROW - 1));

    private const int PANEL_WIDTH = CONTENT_LEFT + SEAT_ROW_WIDTH + CONTENT_RIGHT;

    //── vertical rhythm ──
    private const int TITLE_TOP = 8;
    private const int SEAT_ROW_TOP = TITLE_TOP + TextRenderer.CHAR_HEIGHT + 6;
    private const int BOARD_TOP = SEAT_ROW_TOP + SEAT_HEIGHT + 10;
    private const int POT_TOP = BOARD_TOP + CardView.HEIGHT + 6;
    private const int POT_BOX_HEIGHT = CustomButton.HEIGHT;
    private const int SEAT_ROW2_TOP = POT_TOP + POT_BOX_HEIGHT + 10;
    private const int EVENT_TOP = SEAT_ROW2_TOP + SEAT_HEIGHT + 8;
    private const int ACTION_ROW_TOP = EVENT_TOP + TextRenderer.CHAR_HEIGHT + 6;
    private const int TABLE_ROW_TOP = ACTION_ROW_TOP + CustomButton.HEIGHT + 6;

    private const int PANEL_HEIGHT = TABLE_ROW_TOP + CustomButton.HEIGHT + 4 + FRAME_BOTTOM_BORDER;

    //── button rows ──
    //five action buttons spanning exactly one seat row, so the buttons line up under the seats above them.
    private const int ACTION_BUTTON_COUNT = 5;
    private const int ACTION_BUTTON_GAP = 6;

    private const int ACTION_BUTTON_WIDTH
        = (SEAT_ROW_WIDTH - (ACTION_BUTTON_GAP * (ACTION_BUTTON_COUNT - 1))) / ACTION_BUTTON_COUNT;

    private const int TABLE_BUTTON_COUNT = 3;
    private const int TABLE_BUTTON_WIDTH = 96;
    private const int TABLE_BUTTON_GAP = 8;

    private const int TABLE_ROW_WIDTH
        = (TABLE_BUTTON_WIDTH * TABLE_BUTTON_COUNT) + (TABLE_BUTTON_GAP * (TABLE_BUTTON_COUNT - 1));

    private const int TABLE_ROW_LEFT = CONTENT_LEFT + ((SEAT_ROW_WIDTH - TABLE_ROW_WIDTH) / 2);

    private const int BOARD_CARD_GAP = 3;

    /// <summary>
    ///     How long a rejection message resists being overwritten by the next snapshot's <c>EventText</c> -- long
    ///     enough to read it. Mirrors <see cref="Slots.SlotMachineControl" />'s own result-message hold. This holds
    ///     ONE label's text and nothing else: every other child still repaints from the snapshot, so it is not a
    ///     merge across snapshots.
    /// </summary>
    private const float REJECT_MESSAGE_HOLD_SECONDS = 3f;

    /// <summary>Seconds left on the clock below which the countdown turns red.</summary>
    private const int SHOT_CLOCK_URGENT_SECONDS = 5;

    /// <summary>The soft click the Spindle uses for its pointer ticks -- played once when the action reaches this player.</summary>
    private const int SOUND_YOUR_TURN = 9;

    /// <summary>Fill behind the recessed surfaces (seat boxes, pot readout) -- the same near-black CustomButton uses.</summary>
    private static readonly SKColor RecessedFillColor = new(10, 8, 5, 255);

    /// <summary>Warm gold outline marking the seat on the clock. Border-only, so it never recolors the seat's own text.</summary>
    private static readonly Color ActingSeatColor = new(255, 200, 60, 220);

    private readonly SoundSystem SoundSystem;

    private readonly UILabel TitleLabel;
    private readonly UILabel PotLabel;
    private readonly UILabel EventLabel;

    //indexed by TABLE seat index, not by screen position -- SeatScreenSlot maps one to the other.
    private readonly SeatPanel[] SeatPanels = new SeatPanel[SEAT_COUNT];

    private readonly CardView[] BoardCards = new CardView[BOARD_SIZE];

    private readonly CustomButton FoldButton;
    private readonly CustomButton CheckButton;
    private readonly CustomButton CallButton;
    private readonly CustomButton BetButton;
    private readonly CustomButton RaiseButton;
    private readonly CustomButton LeaveButton;
    private readonly CustomButton SitOutButton;
    private readonly CustomButton SitInButton;

    /// <summary>
    ///     The "leave the hand?" confirmation, shown only by <see cref="RequestDismissal" />. A stock
    ///     <see cref="OkPopupMessageControl" /> with its Cancel button enabled -- the same yes/no dialog
    ///     <c>MarketBuyConfirm</c>, <c>DeleteConfirm</c> and the group-invite prompt already use, rather than a
    ///     bespoke one.
    /// </summary>
    private readonly OkPopupMessageControl ConfirmDialog;

    //scratch, cleared and rebuilt on every snapshot -- never read before it is written, so nothing from the
    //previous snapshot can survive into this one. See OnSnapshot.
    private readonly PokerSeatInfo?[] SeatLookup = new PokerSeatInfo?[SEAT_COUNT];

    //── shot clock ──
    //ClockSeat is the seat the countdown belongs to, and stays null whenever ActorIndex is null: null is the
    //"nobody is on the clock" case and it is carried, not collapsed. ClockRemaining is seeded from the snapshot's
    //SecondsRemaining and ticked down locally between snapshots; the next snapshot re-seeds it outright.
    private byte? ClockSeat;
    private float ClockRemaining;

    //whether the local player was already on the clock at the previous snapshot, so the turn alert fires on the
    //transition rather than on every repaint.
    private bool WasYourTurn;

    private float RejectHoldRemaining;

    /// <summary>
    ///     Raised with a <c>PokerAction</c> byte when the player clicks an enabled action button. WorldScreen wires
    ///     this to <c>ConnectionManager.SendPokerAct</c>.
    /// </summary>
    public event Action<byte>? ActionRequested;

    /// <summary>Raised when the player clicks Leave. Wired to <c>ConnectionManager.SendPokerLeave</c>.</summary>
    public event Action? LeaveRequested;

    /// <summary>Raised when the player clicks Sit Out. Wired to <c>ConnectionManager.SendPokerSitOut</c>.</summary>
    public event Action? SitOutRequested;

    /// <summary>Raised when the player clicks Sit In. Wired to <c>ConnectionManager.SendPokerSitIn</c>.</summary>
    public event Action? SitInRequested;

    /// <summary>
    ///     Raised whenever the window closes by any path (the Close button, Escape, or a server-pushed Close
    ///     display). Mirrors <see cref="Slots.SlotMachineControl.Closed" /> and
    ///     <see cref="Wheel.GildedSpindleControl.Closed" />, and exists for the same reason: without it the panel
    ///     can be dismissed with nothing telling the server, so <c>ConnectionManager.SendPokerClose</c> would have
    ///     no trigger at all.
    /// </summary>
    /// <remarks>
    ///     <b>Closing this window stands the player up and can cost them gold.</b> <c>PokerTableScript</c> routes
    ///     <c>PokerInteractionType.Close</c> and <c>PokerInteractionType.Leave</c> into the same <c>ReleaseSeat</c>
    ///     call, on purpose -- its own comment calls a seat that keeps being dealt in behind a closed panel "a gold
    ///     trap", where the player goes on paying blinds and timing out with nothing on screen. Mid-hand,
    ///     <c>ReleaseSeat</c> forfeits everything that seat has already committed, and that is the same outcome for
    ///     every way of leaving. Close is still the right interaction type to send -- it names what the player
    ///     actually did, and the server converging the two is its decision, not an accident -- but nothing here
    ///     should be read as saying that closing the panel keeps the seat. It does not, which is precisely why
    ///     <see cref="RequestDismissal" /> asks first when there is gold in the pot.
    /// </remarks>
    public event Action? Closed;

    public PokerTableControl(SoundSystem soundSystem)
        : base("_nsett", false)
    {
        ArgumentNullException.ThrowIfNull(soundSystem);

        SoundSystem = soundSystem;

        Name = "PokerTable";
        Visible = false;
        UsesControlStack = true; //inherited Show/Hide push/pop the InputDispatcher stack

        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        this.CenterOnScreen();
        Y = TOP_MARGIN;

        //borrowed _nsett prefab button, re-skinned as Close -- see SlotMachineControl's remarks on why this
        //feature has no control file of its own
        OkButton = CreateButton("OK");

        if (OkButton is not null)
        {
            OkButton.NormalTexture = UiRenderer.Instance!.GetSpfTexture("_nbtn.spf");
            OkButton.PressedTexture = UiRenderer.Instance!.GetSpfTexture("_nbtn.spf", 1);
            OkButton.HoverTexture = null;
            OkButton.SelectedTexture = null;
            OkButton.DisabledTexture = null;

            //RequestDismissal, not Hide: this is a PLAYER-initiated close, and the player is the only one who ever
            //gets asked to confirm. See Hide's own remarks.
            OkButton.Clicked += RequestDismissal;
            OkButton.X = Width - OkButton.Width - OK_RIGHT_MARGIN;
            OkButton.Y = Height - OkButton.Height - OK_BOTTOM_MARGIN;
        }

        TitleLabel = new UILabel
        {
            X = 0,
            Y = TITLE_TOP,
            Width = PANEL_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(TitleLabel);

        //── the six seat boxes, ringed around the board ──
        for (var seat = 0; seat < SEAT_COUNT; seat++)
        {
            var (column, row) = SeatScreenSlot(seat);

            var panel = new SeatPanel
            {
                X = CONTENT_LEFT + (column * (SEAT_WIDTH + SEAT_GAP)),
                Y = row == 0 ? SEAT_ROW_TOP : SEAT_ROW2_TOP
            };
            SeatPanels[seat] = panel;
            AddChild(panel);
        }

        //── community board: five slots, re-centred as a group on every snapshot so a three-card flop sits in the
        //   middle of the table rather than hard against the left of a five-wide strip. See LayOutBoard. ──
        for (var i = 0; i < BoardCards.Length; i++)
        {
            var card = new CardView
            {
                Y = BOARD_TOP,
                Visible = false
            };
            BoardCards[i] = card;
            AddChild(card);
        }

        //── pot readout: a recessed display spanning the seat row, directly beneath the board ──
        var potBox = new UIPanel
        {
            X = CONTENT_LEFT,
            Y = POT_TOP,
            Width = SEAT_ROW_WIDTH,
            Height = POT_BOX_HEIGHT,
            Background = BuildRecessedPanel(SEAT_ROW_WIDTH, POT_BOX_HEIGHT),
            IsHitTestVisible = false
        };
        AddChild(potBox);

        PotLabel = new UILabel
        {
            X = 8,
            Y = (POT_BOX_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = SEAT_ROW_WIDTH - 16,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.Gold,
            IsHitTestVisible = false,
            Text = "Pot: 0"
        };
        potBox.AddChild(PotLabel);

        //── one-line action log, directly above the action buttons ──
        EventLabel = new UILabel
        {
            X = CONTENT_LEFT,
            Y = EVENT_TOP,
            Width = SEAT_ROW_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(EventLabel);

        //── action buttons: always visible, enabled only per LegalActions (see RefreshActionButtons). Greyed
        //   rather than hidden so the row never reflows under the cursor mid-hand. ──
        FoldButton = CreateActionButton("Fold", 0, ACTION_FOLD);
        CheckButton = CreateActionButton("Check", 1, ACTION_CHECK);
        CallButton = CreateActionButton("Call", 2, ACTION_CALL);
        BetButton = CreateActionButton("Bet", 3, ACTION_BET);
        RaiseButton = CreateActionButton("Raise", 4, ACTION_RAISE);

        //── table controls: leaving and sitting out/in are not betting actions, so they are not gated on
        //   LegalActions -- they sit on their own row and answer to the local seat's own state. ──
        LeaveButton = CreateTableButton("Leave", 0);
        LeaveButton.Clicked += () => LeaveRequested?.Invoke();

        SitOutButton = CreateTableButton("Sit Out", 1);
        SitOutButton.Clicked += () => SitOutRequested?.Invoke();

        SitInButton = CreateTableButton("Sit In", 2);
        SitInButton.Clicked += () => SitInRequested?.Invoke();

        //── the leave-the-hand confirmation ──
        //Owned by this panel and parented to it, rather than living on Root the way MarketBuyConfirm does. That
        //popup is on Root because it must not be clipped inside the Market window; this one comfortably fits
        //inside the poker panel (it is far smaller in both dimensions), and keeping it here is what lets the whole
        //confirmation ship without touching a file outside this folder. Added last, and given a ZIndex above
        //every other child, so it draws over the table rather than under it.
        ConfirmDialog = new OkPopupMessageControl(true)
        {
            Name = "PokerLeaveConfirm",
            ZIndex = 100
        };

        //re-centred within THIS panel: the constructor centred it on the screen, which is the wrong frame of
        //reference now that its coordinates are relative to a parent.
        ConfirmDialog.X = (PANEL_WIDTH - ConfirmDialog.Width) / 2;
        ConfirmDialog.Y = (PANEL_HEIGHT - ConfirmDialog.Height) / 2;

        ConfirmDialog.OnOk += () =>
        {
            ConfirmDialog.Hide();
            Hide();
        };

        ConfirmDialog.OnCancel += () => ConfirmDialog.Hide();
        AddChild(ConfirmDialog);
    }

    /// <summary>
    ///     Maps a table seat index to its (column, row) slot on screen. Seats 0-2 run left to right along the top,
    ///     seats 3-5 run right to left along the bottom, so consecutive seat indices stay physically adjacent all
    ///     the way round the table -- the order the button and the action actually move in. Laying the bottom row
    ///     out left-to-right instead would make the step from seat 2 to seat 3 jump across the whole panel.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="seat" /> is outside 0..<see cref="SEAT_COUNT" />-1. Thrown rather than tolerated: the
    ///     column arithmetic below goes negative past the last seat, so an out-of-range index used to place a seat
    ///     box off the left edge of the panel with nothing said about it. A layout that cannot draw a seat has to
    ///     say so. Server-sent seat indices never reach here -- <see cref="OnSnapshot" /> range-checks and logs
    ///     those instead, because a wider table is the server's news to deliver, not a crash.
    /// </exception>
    private static (int Column, int Row) SeatScreenSlot(int seat)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seat);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(seat, SEAT_COUNT);

        return seat < SEATS_PER_ROW
            ? (seat, 0)
            : (SEATS_PER_ROW - 1 - (seat - SEATS_PER_ROW), 1);
    }

    /// <summary>
    ///     Builds a recessed dark-fill panel bordered by dlgframe.epf's 8-piece border — the same primitive
    ///     <see cref="CustomButton" /> and <see cref="Slots.SlotMachineControl" /> use for their own inset
    ///     surfaces, so every recessed surface in this control family comes from one place.
    /// </summary>
    private static Texture2D BuildRecessedPanel(int width, int height)
    {
        using var frame = DialogFrame.Composite(RecessedFillColor, width, height);

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);

        if (frame is not null)
            surface.Canvas.DrawImage(frame, 0, 0);
        else
            surface.Canvas.Clear(RecessedFillColor); //fallback if dlgframe.epf failed to load

        using var snapshot = surface.Snapshot();

        return TextureConverter.ToTexture2D(snapshot);
    }

    /// <summary>
    ///     Builds a transparent-interior border of <paramref name="color" />. Border-only rather than a filled
    ///     tint so the acting-seat marker outlines the box without washing out the text inside it.
    /// </summary>
    private static Texture2D BuildBorder(
        int width,
        int height,
        Color color,
        int thickness)
    {
        var pixels = new Color[width * height]; //defaults to transparent -- only the border below is written

        ImageUtil.FillRect(
            pixels,
            width,
            height,
            0,
            0,
            width,
            thickness,
            color);

        ImageUtil.FillRect(
            pixels,
            width,
            height,
            0,
            height - thickness,
            width,
            thickness,
            color);

        ImageUtil.FillRect(
            pixels,
            width,
            height,
            0,
            0,
            thickness,
            height,
            color);

        ImageUtil.FillRect(
            pixels,
            width,
            height,
            width - thickness,
            0,
            thickness,
            height,
            color);

        var texture = new Texture2D(TextureConverter.Device, width, height);
        texture.SetData(pixels);

        return texture;
    }

    private CustomButton CreateActionButton(string caption, int column, byte action)
    {
        var button = new CustomButton(caption, ACTION_BUTTON_WIDTH)
        {
            X = CONTENT_LEFT + (column * (ACTION_BUTTON_WIDTH + ACTION_BUTTON_GAP)),
            Y = ACTION_ROW_TOP,
            Enabled = false
        };

        //CustomButton.OnClick already refuses to fire while disabled, so a greyed button cannot send.
        button.Clicked += () => ActionRequested?.Invoke(action);
        AddChild(button);

        return button;
    }

    private CustomButton CreateTableButton(string caption, int column)
    {
        var button = new CustomButton(caption, TABLE_BUTTON_WIDTH)
        {
            X = TABLE_ROW_LEFT + (column * (TABLE_BUTTON_WIDTH + TABLE_BUTTON_GAP)),
            Y = TABLE_ROW_TOP
        };
        AddChild(button);

        return button;
    }

    /// <summary>
    ///     Centres <paramref name="revealed" /> board cards as a group across the seat row. Called on every
    ///     snapshot because the board grows mid-hand: a three-card flop laid out in the leftmost three of five
    ///     fixed slots would sit visibly off-centre until the river landed.
    /// </summary>
    private void LayOutBoard(int revealed)
    {
        var count = Math.Clamp(revealed, 0, BOARD_SIZE);

        if (count == 0)
            return;

        var stripWidth = (count * CardView.WIDTH) + ((count - 1) * BOARD_CARD_GAP);
        var left = CONTENT_LEFT + ((SEAT_ROW_WIDTH - stripWidth) / 2);

        for (var i = 0; i < count; i++)
            BoardCards[i].X = left + (i * (CardView.WIDTH + BOARD_CARD_GAP));
    }

    /// <summary>
    ///     Repaints every child from <see cref="WorldState.PokerTable" />. A snapshot is a complete authoritative
    ///     state, so this is a full repaint from the current view model every time -- there is deliberately no
    ///     diffing, no merging, and no carry-forward of anything from a previous call.
    /// </summary>
    public void OnSnapshot()
    {
        var vm = WorldState.PokerTable;

        TitleLabel.Text = string.IsNullOrWhiteSpace(vm.TableName) ? "Poker" : vm.TableName;
        PotLabel.Text = $"Pot: {vm.Pot:N0}";

        //carried as nullables all the way down to the seat panels. Absent means absent.
        var buttonIndex = vm.ButtonIndex;
        var actorIndex = vm.ActorIndex;

        //── shot clock: re-seeded absolutely, never adjusted ──
        if (actorIndex.HasValue)
        {
            ClockSeat = actorIndex;
            ClockRemaining = Math.Max(0, vm.SecondsRemaining);
        } else
        {
            //nobody is on the clock: no countdown anywhere. Collapsing this to seat 0 would run a live countdown
            //on whoever happens to sit there between hands.
            ClockSeat = null;
            ClockRemaining = 0f;
        }

        //whether a hand is actually running, used ONLY to decide whether an occupied, unfolded seat holding no
        //visible cards is drawn face-down or empty. Both terms are server-sent scalars, and neither says anything
        //about what any card IS -- that is the one thing this control must never decide.
        var handInProgress = actorIndex.HasValue || (vm.Pot > 0);

        Array.Clear(SeatLookup);

        foreach (var seat in vm.Seats)
            if ((uint)seat.SeatIndex < SEAT_COUNT)
                SeatLookup[seat.SeatIndex] = seat;
            else

                //a table wider than this fixed six-box layout has outgrown the panel. Log loudly rather than
                //silently dropping a seat, the same way StakeSelectorControl handles its own overflow.
                System.Diagnostics.Debug.WriteLine(
                    $"PokerTableControl: seat index {seat.SeatIndex} exceeds the {SEAT_COUNT}-seat layout and will not be shown.");

        for (var seat = 0; seat < SEAT_COUNT; seat++)
            SeatPanels[seat]
                .Apply(
                    SeatLookup[seat],
                    buttonIndex.HasValue && (buttonIndex.Value == seat),
                    actorIndex.HasValue && (actorIndex.Value == seat),
                    seat == vm.YourSeatIndex,
                    handInProgress);

        //── community board: exactly what the server revealed, nothing more ──
        var board = vm.Board;
        LayOutBoard(board.Count);

        for (var i = 0; i < BoardCards.Length; i++)
            if (i < board.Count)
                BoardCards[i]
                    .ShowFace(board[i]);
            else
                BoardCards[i]
                    .ShowNothing();

        RefreshActionButtons(vm.LegalActions);
        RefreshTableButtons(vm);

        //the action log yields to a rejection the player has not had time to read yet -- see
        //REJECT_MESSAGE_HOLD_SECONDS. Only this one label is held; everything above repainted regardless.
        if (RejectHoldRemaining <= 0f)
        {
            EventLabel.Text = vm.EventText;
            EventLabel.ForegroundColor = LegendColors.White;
        }

        //── turn alert: fires on the transition into the local player's turn, not on every repaint ──
        var yourTurn = actorIndex.HasValue && (actorIndex.Value == vm.YourSeatIndex);

        if (yourTurn && !WasYourTurn && Visible)
            SoundSystem.PlaySound(SOUND_YOUR_TURN);

        WasYourTurn = yourTurn;
    }

    /// <summary>
    ///     Enables exactly the buttons whose <c>PokerAction</c> byte appears in <paramref name="legalActions" />
    ///     and greys the rest. This is the whole rule: there is no client-side legality check here to disagree with
    ///     the server, which re-validates every action it receives.
    /// </summary>
    private void RefreshActionButtons(IReadOnlyList<byte> legalActions)
    {
        FoldButton.Enabled = legalActions.Contains(ACTION_FOLD);
        CheckButton.Enabled = legalActions.Contains(ACTION_CHECK);
        CallButton.Enabled = legalActions.Contains(ACTION_CALL);
        BetButton.Enabled = legalActions.Contains(ACTION_BET);
        RaiseButton.Enabled = legalActions.Contains(ACTION_RAISE);
    }

    /// <summary>
    ///     Enables Sit Out / Sit In from the local seat's own server-sent state -- exactly one of the pair is ever
    ///     live, and neither is while the player holds no seat at this table. Leave stays available: it is the way
    ///     out, and the server refuses it when it must.
    /// </summary>
    private void RefreshTableButtons(ViewModel.PokerTable vm)
    {
        var yourSeat = (uint)vm.YourSeatIndex < SEAT_COUNT ? SeatLookup[vm.YourSeatIndex] : null;
        var seated = yourSeat is not null && !string.IsNullOrEmpty(yourSeat.Name);

        SitOutButton.Enabled = seated && !yourSeat!.IsSittingOut;
        SitInButton.Enabled = seated && yourSeat!.IsSittingOut;
        LeaveButton.Enabled = true;
    }

    /// <summary>
    ///     Shows a reason-specific message on the action log. The refusal came from the server, which has already
    ///     re-checked the action -- nothing about the local view model changes here, and no button is re-gated: the
    ///     authoritative snapshot that follows is what repaints them.
    /// </summary>
    public void OnRejected(PokerRejectReason reason)
    {
        var text = reason switch
        {
            PokerRejectReason.NotSeated        => "You are not seated at this table.",
            PokerRejectReason.NotYourTurn      => "It is not your turn.",
            PokerRejectReason.IllegalAction    => "You cannot do that right now.",
            PokerRejectReason.InsufficientGold => "You do not have enough gold.",
            PokerRejectReason.HandInProgress   => "Wait for the current hand to finish.",
            _                                  => "That did not work."
        };

        EventLabel.Text = text;
        EventLabel.ForegroundColor = LegendColors.Red;
        RejectHoldRemaining = REJECT_MESSAGE_HOLD_SECONDS;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        var deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (RejectHoldRemaining > 0f)
            RejectHoldRemaining = Math.Max(0f, RejectHoldRemaining - deltaSeconds);

        TickShotClock(deltaSeconds);
    }

    /// <summary>
    ///     Runs the countdown down locally between snapshots, so the clock moves at one second per second instead
    ///     of jumping only when the server happens to send. The next snapshot re-seeds it outright.
    /// </summary>
    private void TickShotClock(float deltaSeconds)
    {
        //null actor: no countdown anywhere, on any seat. This is the branch that keeps a between-hands table from
        //showing a live clock on seat 0.
        if (ClockSeat is not { } clockSeat)
        {
            foreach (var panel in SeatPanels)
                panel.SetClock(null);

            return;
        }

        if (ClockRemaining > 0f)
            ClockRemaining = Math.Max(0f, ClockRemaining - deltaSeconds);

        var whole = (int)Math.Ceiling(ClockRemaining);

        for (var seat = 0; seat < SEAT_COUNT; seat++)
            SeatPanels[seat]
                .SetClock(seat == clockSeat ? whole : null);
    }

    /// <summary>
    ///     Repaints from <see cref="WorldState.PokerTable" /> (already populated by <c>ApplyOpen</c> /
    ///     <c>ApplySnapshot</c>) and shows the panel.
    /// </summary>
    public override void Show()
    {
        RejectHoldRemaining = 0f;

        //cleared so re-opening onto a table where the action is already on this player still plays the alert.
        WasYourTurn = false;

        base.Show();
        OnSnapshot();
    }

    /// <summary>
    ///     Hides the panel, unconditionally and immediately, and fires <see cref="Closed" /> on any path that
    ///     actually hid a visible window.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Never prompts.</b> The "your bet stays in the pot" confirmation lives in
    ///         <see cref="RequestDismissal" />, which only the player's own Close button and Escape key go through.
    ///         The server calls this method too -- <c>WorldScreen.ServerHandlers</c> answers a Close display with
    ///         <c>Poker.Hide()</c> followed by <c>WorldState.PokerTable.Clear()</c> -- and putting the dialog in
    ///         here would show a player a confirmation for a session the SERVER just ended, and could leave the
    ///         panel standing open against the server's wishes. So the prompt sits strictly above this, never
    ///         inside it.
    ///     </para>
    ///     <para>
    ///         Deliberately does NOT clear <see cref="WorldState.PokerTable" />, which is where this differs from
    ///         <see cref="Slots.SlotMachineControl.Hide" />. It does not need to: the server answers every close by
    ///         pushing its own Close display, and the handler for that clears the view model itself. Clearing here
    ///         as well would only race the dispatcher for the same job, and would wipe the state mid-teardown on
    ///         the very paths where the server is the one closing the panel. <see cref="Show" /> repaints from the
    ///         view model on the way back in, so nothing stale survives a reopen either way.
    ///     </para>
    /// </remarks>
    public override void Hide()
    {
        var wasVisible = Visible;

        //a confirmation still standing when the panel goes away -- the server closing the session out from under
        //an open prompt is the case that matters -- must not be left visible or on the input stack.
        ConfirmDialog.Hide();

        ClockSeat = null;
        ClockRemaining = 0f;
        RejectHoldRemaining = 0f;
        WasYourTurn = false;

        //nulling ClockSeat is not enough on its own: TickShotClock is what actually writes "no clock" to the seat
        //panels, and it does not run while the panel is hidden. Without this the next Show() would paint one frame
        //of the countdown this hide was ending.
        foreach (var panel in SeatPanels)
            panel.SetClock(null);

        base.Hide();

        if (wasVisible)
            Closed?.Invoke();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            //the other player-initiated dismissal, and so the other one that has to ask first.
            RequestDismissal();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>
    ///     The single entry point for a dismissal the PLAYER asked for -- the Close button and the Escape key, and
    ///     nothing else. Confirms first when there is gold at stake, and closes immediately when there is not.
    /// </summary>
    /// <remarks>
    ///     This exists because closing the panel is not the harmless act it looks like: the server routes Close and
    ///     Leave into the same <c>ReleaseSeat</c>, which mid-hand forfeits everything the seat has committed (see
    ///     <see cref="Closed" />). Escape used to hand the pot away on a single keystroke with no warning.
    ///     <para>
    ///         It is deliberately NOT reachable from <see cref="Hide" />. The server calls <see cref="Hide" /> too,
    ///         on its own Close display, and a confirmation dialog raised there would be asking the player to
    ///         approve something that has already happened -- and could hold the panel open against the server.
    ///     </para>
    /// </remarks>
    private void RequestDismissal()
    {
        if (!Visible)
            return;

        //nothing in the pot from this seat: nothing to warn about, so do not make the player click twice to close
        //a window. The prompt only earns its interruption when it is guarding something.
        if (!HasGoldAtRisk())
        {
            Hide();

            return;
        }

        ConfirmDialog.Show("Leave the hand? Your bet stays in the pot.");
    }

    /// <summary>
    ///     Whether closing right now would cost the player gold: a hand is running and this player's own seat has
    ///     chips committed to it that it has not already forfeited.
    /// </summary>
    /// <remarks>
    ///     Every term is read straight from the current snapshot, and all of them are server-sent. A folded seat is
    ///     excluded on purpose -- its committed chips are already gone whether it leaves or stays, so warning about
    ///     them would be warning about a loss the player cannot avoid, which teaches them to click through the
    ///     dialog without reading it.
    /// </remarks>
    private static bool HasGoldAtRisk()
    {
        var vm = WorldState.PokerTable;

        //the same two server-sent scalars the card backs are gated on -- see OnSnapshot.
        if (!vm.ActorIndex.HasValue && (vm.Pot <= 0))
            return false;

        foreach (var seat in vm.Seats)
            if (seat.SeatIndex == vm.YourSeatIndex)
                return !string.IsNullOrEmpty(seat.Name) && !seat.HasFolded && (seat.Committed > 0);

        return false;
    }

    /// <summary>
    ///     One seat box: name, gold, chips committed this hand, its last action, its two hole-card slots, and the
    ///     shot clock when this seat is the one on the clock. Everything it shows comes from the
    ///     <see cref="PokerSeatInfo" /> handed to <see cref="Apply" />, or from the two explicitly nullable table
    ///     indices already resolved by the owning panel -- it holds no state of its own across snapshots beyond the
    ///     last clock value it painted.
    /// </summary>
    private sealed class SeatPanel : UIPanel
    {
        private const int PAD = 4;
        private const int TEXT_WIDTH = 78;
        private const int BADGE_WIDTH = 14;
        private const int NAME_WIDTH = TEXT_WIDTH - BADGE_WIDTH;
        private const int CARD_COLUMN_X = PAD + TEXT_WIDTH;
        private const int CARD_GAP = 2;
        private const int HOLE_CARDS = 2;
        private const int CARD_COLUMN_WIDTH = (CardView.WIDTH * HOLE_CARDS) + CARD_GAP;
        private const int CLOCK_TOP = PAD + CardView.HEIGHT + 4;

        private readonly UILabel NameLabel;
        private readonly UILabel DealerBadge;
        private readonly UILabel GoldLabel;
        private readonly UILabel CommittedLabel;
        private readonly UILabel LastActionLabel;
        private readonly UILabel ClockLabel;
        private readonly UIPanel ActingOutline;
        private readonly CardView[] Cards = new CardView[HOLE_CARDS];

        private bool Occupied;

        //deliberately seeded to a value SetClock can never be handed, so the very first call -- including
        //SetClock(null) -- still writes through to the label.
        private int? RenderedClock = -1;

        public SeatPanel()
        {
            Width = SEAT_WIDTH;
            Height = SEAT_HEIGHT;
            Background = BuildRecessedPanel(SEAT_WIDTH, SEAT_HEIGHT);
            IsHitTestVisible = false;

            NameLabel = AddRow(
                PAD,
                PAD,
                NAME_WIDTH,
                HorizontalAlignment.Left);

            //the dealer button marker. Visible ONLY when the table's ButtonIndex is this seat -- a null
            //ButtonIndex leaves it hidden on every seat, which is exactly what "there is no button" looks like.
            DealerBadge = AddRow(
                PAD + NAME_WIDTH,
                PAD,
                BADGE_WIDTH,
                HorizontalAlignment.Right);
            DealerBadge.ForegroundColor = LegendColors.Gold;
            DealerBadge.Text = "D";
            DealerBadge.Visible = false;

            GoldLabel = AddRow(
                PAD,
                PAD + TextRenderer.CHAR_HEIGHT,
                TEXT_WIDTH,
                HorizontalAlignment.Left);

            CommittedLabel = AddRow(
                PAD,
                PAD + (TextRenderer.CHAR_HEIGHT * 2),
                TEXT_WIDTH,
                HorizontalAlignment.Left);

            LastActionLabel = AddRow(
                PAD,
                PAD + (TextRenderer.CHAR_HEIGHT * 3),
                TEXT_WIDTH,
                HorizontalAlignment.Left);

            for (var i = 0; i < Cards.Length; i++)
            {
                var card = new CardView
                {
                    X = CARD_COLUMN_X + (i * (CardView.WIDTH + CARD_GAP)),
                    Y = PAD,
                    Visible = false
                };
                Cards[i] = card;
                AddChild(card);
            }

            ClockLabel = AddRow(
                CARD_COLUMN_X,
                CLOCK_TOP,
                CARD_COLUMN_WIDTH,
                HorizontalAlignment.Center);
            ClockLabel.Visible = false;

            //added last so it draws over the seat's own contents rather than under them.
            ActingOutline = new UIPanel
            {
                X = 0,
                Y = 0,
                Width = SEAT_WIDTH,
                Height = SEAT_HEIGHT,
                Background = BuildBorder(
                    SEAT_WIDTH,
                    SEAT_HEIGHT,
                    ActingSeatColor,
                    2),
                IsHitTestVisible = false,
                Visible = false
            };
            AddChild(ActingOutline);
        }

        private UILabel AddRow(
            int x,
            int y,
            int width,
            HorizontalAlignment alignment)
        {
            var label = new UILabel
            {
                X = x,
                Y = y,
                Width = width,
                Height = TextRenderer.CHAR_HEIGHT,
                HorizontalAlignment = alignment,
                ForegroundColor = LegendColors.White,
                IsHitTestVisible = false
            };
            AddChild(label);

            return label;
        }

        /// <summary>
        ///     Repaints this seat from one snapshot's worth of state.
        /// </summary>
        /// <param name="info">
        ///     This seat's entry in the snapshot, or <see langword="null" /> when the snapshot carried no entry for
        ///     it at all.
        /// </param>
        /// <param name="isButton">
        ///     Whether the table's <c>ButtonIndex</c> is this seat. Already resolved by the caller from a nullable
        ///     byte -- a null index yields <see langword="false" /> for every seat, never <see langword="true" />
        ///     for seat 0.
        /// </param>
        /// <param name="isActor">
        ///     Whether the table's <c>ActorIndex</c> is this seat, resolved the same way and under the same rule.
        /// </param>
        /// <param name="isYou">Whether this is the recipient's own seat.</param>
        /// <param name="handInProgress">
        ///     Whether a hand is actually running, which is the only thing that decides whether an occupied,
        ///     unfolded seat holding no visible cards is drawn face-down or empty. It never decides what a card is.
        /// </param>
        public void Apply(
            PokerSeatInfo? info,
            bool isButton,
            bool isActor,
            bool isYou,
            bool handInProgress)
        {
            DealerBadge.Visible = isButton;
            ActingOutline.Visible = isActor;

            Occupied = info is not null && !string.IsNullOrEmpty(info.Name);

            if (!Occupied)
            {
                NameLabel.Text = "Empty";
                NameLabel.ForegroundColor = LegendColors.DarkGray;
                GoldLabel.Text = string.Empty;
                CommittedLabel.Text = string.Empty;
                LastActionLabel.Text = string.Empty;
                ClockLabel.Visible = false;
                RenderedClock = -1;

                foreach (var card in Cards)
                    card.ShowNothing();

                return;
            }

            var seat = info!;

            //dimmed the moment a seat is out of the hand, by either route the server reports it. The dimming is
            //the seat's whole "not in this one" signal, so it covers every line at once rather than one label.
            var inactive = seat.HasFolded || seat.IsSittingOut;
            var dimmed = LegendColors.DimGray;

            //the local player's own seat reads in gold so it is findable at a glance -- the panel is otherwise six
            //identical boxes, and nothing said which one was yours.
            NameLabel.Text = seat.Name;

            NameLabel.ForegroundColor = inactive ? dimmed :
                isYou ? LegendColors.Gold : LegendColors.White;

            GoldLabel.Text = $"Gold: {seat.Gold:N0}";
            GoldLabel.ForegroundColor = inactive ? dimmed : LegendColors.White;

            CommittedLabel.Text = $"Bet: {seat.Committed:N0}";
            CommittedLabel.ForegroundColor = inactive ? dimmed : LegendColors.PastelYellow;

            LastActionLabel.Text = seat.IsSittingOut ? "Sitting out" :
                seat.HasFolded ? "Folded" : seat.LastAction;
            LastActionLabel.ForegroundColor = inactive ? dimmed : LegendColors.LightGray;

            ApplyCards(seat, inactive, handInProgress);
        }

        /// <summary>
        ///     Draws exactly what the snapshot carried for this seat, and nothing else.
        /// </summary>
        /// <remarks>
        ///     <see cref="PokerSeatInfo.HoleCards" /> is the entire input: whatever indices it holds are drawn face
        ///     up, and an empty list is drawn face down (or not at all). No card is ever remembered from a previous
        ///     snapshot, derived from the board, or filled in for a seat the server chose not to reveal -- that
        ///     per-recipient filtering is the security model of the whole feature, and a client that reconstructed
        ///     around it would hand every player everyone else's holdings.
        /// </remarks>
        private void ApplyCards(PokerSeatInfo seat, bool inactive, bool handInProgress)
        {
            var cards = seat.HoleCards;

            for (var i = 0; i < Cards.Length; i++)
                if (i < cards.Count)
                    Cards[i]
                        .ShowFace(cards[i]);
                else if (!inactive && handInProgress)

                    //occupied, in the hand, and the server sent nothing for it: face-down is what "nothing sent"
                    //renders as. A folded or sitting-out seat has no cards in front of it, and between hands
                    //nobody does.
                    Cards[i]
                        .ShowBack();
                else
                    Cards[i]
                        .ShowNothing();
        }

        /// <summary>
        ///     Sets the countdown shown on this seat, or hides it when <paramref name="seconds" /> is
        ///     <see langword="null" /> -- which is what every seat is handed whenever the table has no actor. Cheap
        ///     to call every frame: it writes nothing when the whole-second value has not moved.
        /// </summary>
        public void SetClock(int? seconds)
        {
            //an unoccupied seat never shows a clock, whatever the table thinks its actor is.
            var value = Occupied ? seconds : null;

            if (value == RenderedClock)
                return;

            RenderedClock = value;

            if (value is not { } remaining)
            {
                ClockLabel.Visible = false;

                return;
            }

            ClockLabel.Text = $"{remaining}s";
            ClockLabel.ForegroundColor = remaining <= SHOT_CLOCK_URGENT_SECONDS ? LegendColors.Red : LegendColors.White;
            ClockLabel.Visible = true;
        }
    }

    /// <summary>
    ///     One card slot: a face (rank over suit), a back, or nothing at all.
    /// </summary>
    /// <remarks>
    ///     Both faces are composed here rather than loaded, for the reason given in the owning control's remarks --
    ///     there is no card artwork in the archives and no card-index-to-sprite mapping in the protocol, so
    ///     <see cref="Slots.ReelControl" />'s creature-sprite path has nothing to look a playing card up by. The
    ///     rank/suit decode mirrors the server's <c>Card.FromIndex</c> exactly: rank is <c>index / 4 + 2</c>
    ///     (2 through 14) and suit is <c>index % 4</c> (clubs, diamonds, hearts, spades). It is a decode of a byte
    ///     the server sent, never a guess about a byte it did not.
    /// </remarks>
    private sealed class CardView : UIPanel
    {
        public const int WIDTH = 20;
        public const int HEIGHT = 28;

        private static readonly Color FaceColor = new(232, 224, 200, 255);
        private static readonly Color EdgeColor = new(24, 20, 14, 255);
        private static readonly Color BackColor = new(96, 28, 28, 255);
        private static readonly Color BackAccentColor = new(152, 62, 62, 255);
        private static readonly Color BlackSuitInk = new(24, 20, 14, 255);
        private static readonly Color RedSuitInk = new(176, 24, 24, 255);

        //owned per instance rather than shared statically: UIPanel.Dispose disposes Background, and a texture
        //shared across seventeen card slots would be torn down by whichever slot was disposed first.
        private readonly Texture2D FaceTexture;
        private readonly Texture2D BackTexture;
        private readonly UILabel RankLabel;
        private readonly UILabel SuitLabel;

        public CardView()
        {
            Width = WIDTH;
            Height = HEIGHT;
            IsHitTestVisible = false;

            FaceTexture = BuildFace();
            BackTexture = BuildBack();
            Background = BackTexture;

            RankLabel = AddGlyphRow(2);
            SuitLabel = AddGlyphRow(2 + TextRenderer.CHAR_HEIGHT);
        }

        private UILabel AddGlyphRow(int y)
        {
            var label = new UILabel
            {
                X = 0,
                Y = y,
                Width = WIDTH,
                Height = TextRenderer.CHAR_HEIGHT,
                HorizontalAlignment = HorizontalAlignment.Center,
                ForegroundColor = BlackSuitInk,
                IsHitTestVisible = false,
                Visible = false
            };
            AddChild(label);

            return label;
        }

        private static Texture2D BuildFace()
        {
            var pixels = new Color[WIDTH * HEIGHT];

            ImageUtil.FillRect(
                pixels,
                WIDTH,
                HEIGHT,
                0,
                0,
                WIDTH,
                HEIGHT,
                EdgeColor);

            ImageUtil.FillRect(
                pixels,
                WIDTH,
                HEIGHT,
                1,
                1,
                WIDTH - 2,
                HEIGHT - 2,
                FaceColor);

            var texture = new Texture2D(TextureConverter.Device, WIDTH, HEIGHT);
            texture.SetData(pixels);

            return texture;
        }

        private static Texture2D BuildBack()
        {
            var pixels = new Color[WIDTH * HEIGHT];

            ImageUtil.FillRect(
                pixels,
                WIDTH,
                HEIGHT,
                0,
                0,
                WIDTH,
                HEIGHT,
                EdgeColor);

            ImageUtil.FillRect(
                pixels,
                WIDTH,
                HEIGHT,
                1,
                1,
                WIDTH - 2,
                HEIGHT - 2,
                BackColor);

            //a single inset outline -- enough to read as a patterned back rather than a flat coloured tile at this
            //size, without pretending to be artwork.
            ImageUtil.FillRect(
                pixels,
                WIDTH,
                HEIGHT,
                4,
                4,
                WIDTH - 8,
                1,
                BackAccentColor);

            ImageUtil.FillRect(
                pixels,
                WIDTH,
                HEIGHT,
                4,
                HEIGHT - 5,
                WIDTH - 8,
                1,
                BackAccentColor);

            ImageUtil.FillRect(
                pixels,
                WIDTH,
                HEIGHT,
                4,
                4,
                1,
                HEIGHT - 8,
                BackAccentColor);

            ImageUtil.FillRect(
                pixels,
                WIDTH,
                HEIGHT,
                WIDTH - 5,
                4,
                1,
                HEIGHT - 8,
                BackAccentColor);

            var texture = new Texture2D(TextureConverter.Device, WIDTH, HEIGHT);
            texture.SetData(pixels);

            return texture;
        }

        /// <summary>Draws the card the server sent, face up.</summary>
        public void ShowFace(byte cardIndex)
        {
            //outside the 0-51 range the server's Card.FromIndex defines: show a back rather than inventing a card
            //for a byte that does not name one.
            if (cardIndex > 51)
            {
                ShowBack();

                return;
            }

            var rank = (cardIndex / 4) + 2; //2 (Two) through 14 (Ace)
            var suit = cardIndex % 4; //0 clubs, 1 diamonds, 2 hearts, 3 spades

            var rankText = rank switch
            {
                10 => "T",
                11 => "J",
                12 => "Q",
                13 => "K",
                14 => "A",
                _  => rank.ToString()
            };

            //letters rather than the Unicode pips: the Dark Ages bitmap font has no glyphs for the suit symbols.
            var suitText = suit switch
            {
                0 => "C",
                1 => "D",
                2 => "H",
                _ => "S"
            };

            var ink = suit is 1 or 2 ? RedSuitInk : BlackSuitInk;

            Background = FaceTexture;
            RankLabel.Text = rankText;
            RankLabel.ForegroundColor = ink;
            RankLabel.Visible = true;
            SuitLabel.Text = suitText;
            SuitLabel.ForegroundColor = ink;
            SuitLabel.Visible = true;
            Visible = true;
        }

        /// <summary>Draws a card back — the rendering of "the server sent nothing for this slot".</summary>
        public void ShowBack()
        {
            Background = BackTexture;
            RankLabel.Visible = false;
            SuitLabel.Visible = false;
            Visible = true;
        }

        /// <summary>Draws no card at all — this slot holds nothing.</summary>
        public void ShowNothing() => Visible = false;

        public override void Dispose()
        {
            //detached first: the base disposes Background, which is one of the two textures below.
            Background = null;
            FaceTexture.Dispose();
            BackTexture.Dispose();

            base.Dispose();
        }
    }
}
