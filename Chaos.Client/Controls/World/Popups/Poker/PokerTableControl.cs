#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.World.Hud;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Controls.World.ViewPort;
using Chaos.Client.Extensions;
using Chaos.Client.Models;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Utility;
using Chaos.Client.Systems;
using Chaos.Client.Utilities;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
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

    /// <summary>
    ///     The table's fixed seat count -- six, matching the shipped hold-em table's <c>seatCount</c>.
    /// </summary>
    /// <remarks>
    ///     The seats are placed around an ellipse by <see cref="SeatAnchor" />, which owns one hand-written
    ///     anchor per seat rather than deriving positions from rows and columns. That is deliberate: a ring has
    ///     no arithmetic that stays correct as the count changes, so <see cref="SeatAnchor" /> throws on an index
    ///     it has no anchor for instead of computing a plausible-looking position off the felt. Raising this
    ///     count means adding anchors, and the code will say so rather than silently misplacing a seat.
    /// </remarks>
    private const int SEAT_COUNT = 6;

    /// <summary>The community board never exceeds five cards (flop, turn, river).</summary>
    private const int BOARD_SIZE = 5;

    //-- canonical panel size --
    //Unlike the two-row layout this replaced, the panel total IS pinned here, because a ring is laid out from
    //its centre outward rather than stacked from the top down: the felt's centre, the seat anchors and the
    //board all measure from PANEL_WIDTH/PANEL_HEIGHT, so deriving the total from the content would be circular.
    //Both fit the 640x480 virtual screen with margin to spare.
    private const int PANEL_WIDTH = 624;

    //FramedDialogPanelBase paints a 47px ornate bottom border over the panel's last 47 rows (rivet strip plus the
    //Close button), so content has to end above it. Budgeted by name rather than eyeballed.
    private const int FRAME_BOTTOM_BORDER = 47;

    //centered horizontally but pinned this far from the top, matching Slots/Wheel.
    private const int TOP_MARGIN = 15;

    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    //-- seat plaque --
    //A portrait sits at the left with the name and wager stacked beside it, and the two hole cards sit under
    //both. Sized to the content: the card row is the wider of the two blocks only if the cards grow.
    private const int PORTRAIT_SIZE = 40;
    private const int SEAT_TEXT_GAP = 4;

    //wide enough for "Gold: 52,364,211" -- a purse, not a table stack, so it runs to eight digits and grouping.
    private const int SEAT_TEXT_WIDTH = 100;

    private const int SEAT_PAD = 2;

    /// <summary>The portrait's offset inside its plaque. Named here so the control can centre a bubble on a face.</summary>
    private const int PORTRAIT_X_IN_SEAT = SEAT_PAD;
    private const int SEAT_CARD_GAP = 2;
    private const int SEAT_CARDS_WIDTH = (CardView.WIDTH * 2) + SEAT_CARD_GAP;

    /// <summary>
    ///     Portrait, details and hole cards sit side by side rather than stacked.
    /// </summary>
    /// <remarks>
    ///     Stacking the cards under the details was tried first and does not fit: three text rows reach y=38
    ///     while a <see cref="CardView" /> is 50 tall, so in a plaque short enough to ring the felt the cards
    ///     landed on top of the wager line. Widening and going horizontal is what actually has room -- the
    ///     height then only has to clear the tallest single element rather than the sum of all three.
    /// </remarks>
    private const int SEAT_WIDTH = SEAT_PAD
                                   + PORTRAIT_SIZE
                                   + SEAT_TEXT_GAP
                                   + SEAT_TEXT_WIDTH
                                   + SEAT_TEXT_GAP
                                   + SEAT_CARDS_WIDTH
                                   + SEAT_PAD;

    private const int SEAT_HEIGHT = CardView.HEIGHT + (SEAT_PAD * 2);

    //-- felt --
    private const int TITLE_TOP = 8;
    private const int FELT_TOP = TITLE_TOP + TextRenderer.CHAR_HEIGHT + 6;
    private const int FELT_HEIGHT = 300;
    private const int FELT_BOTTOM = FELT_TOP + FELT_HEIGHT;

    /// <summary>How far the painted felt is inset from the panel edges, leaving the side seats room to sit on its rail.</summary>
    private const int FELT_SIDE_INSET = 96;

    private const int FELT_WIDTH = PANEL_WIDTH - (FELT_SIDE_INSET * 2);
    private const int FELT_CENTER_X = PANEL_WIDTH / 2;
    private const int FELT_CENTER_Y = FELT_TOP + (FELT_HEIGHT / 2);

    /// <summary>Width of the darker rail ringing the felt -- the table edge players' plaques rest on.</summary>
    private const int FELT_RAIL_WIDTH = 7;

    private const int SEAT_EDGE_MARGIN = 6;

    /// <summary>Horizontal gap between the two top seats (and the two bottom seats) either side of the centre line.</summary>
    private const int SEAT_SPREAD = 12;

    //-- board and pot, both centred on the felt --
    /// <summary>
    ///     The emotes the picker offers, in grid order.
    /// </summary>
    /// <remarks>
    ///     <b>Face emotes only</b> -- expressions drawn onto the character's own face. The extended emote set
    ///     (Rock On, Shock, Sweat, Love and the rest) draws a white speech bubble above the head instead, which
    ///     is not wanted here: at portrait size the bubble is most of what you see.
    ///     <para>
    ///         These are frames 0-6 of the emote sheet -- a contiguous run of single-frame expressions, which is
    ///         the original face set. Everything from frame 11 up is the extended bubble set; Sweat is frame 21
    ///         and was the one that showed the problem.
    ///     </para>
    ///     <para>
    ///         <c>Snore</c> (frames 7-8) and <c>Mouth</c> (9-10) sit between the two blocks and are excluded
    ///         because their artwork has not been checked -- their frame counts suggest animation rather than a
    ///         still expression. <c>BlowKiss</c> and <c>Wave</c> are excluded for a different reason entirely:
    ///         they are body animations, not overlays, and never showed up here at all.
    ///     </para>
    /// </remarks>
    private static readonly (BodyAnimation Animation, string Caption)[] Emotes =
    [
        (BodyAnimation.Smile, "Smile"),
        (BodyAnimation.Wink, "Wink"),
        (BodyAnimation.Frown, "Frown"),
        (BodyAnimation.Cry, "Cry"),
        (BodyAnimation.Surprise, "Surprise"),
        (BodyAnimation.Tongue, "Tongue"),
        (BodyAnimation.Pleasant, "Pleasant")
    ];

    private const int EMOTE_COLUMNS = 3;
    private const int EMOTE_BUTTON_WIDTH = 88;
    private const int EMOTE_BUTTON_GAP = 5;
    private const int EMOTE_PICKER_PAD = 6;

    private static readonly int EmoteRows = (Emotes.Length + EMOTE_COLUMNS - 1) / EMOTE_COLUMNS;

    private static readonly int EmotePickerWidth
        = (EMOTE_PICKER_PAD * 2) + (EMOTE_BUTTON_WIDTH * EMOTE_COLUMNS) + (EMOTE_BUTTON_GAP * (EMOTE_COLUMNS - 1));

    private static readonly int EmotePickerHeight
        = (EMOTE_PICKER_PAD * 2) + (CustomButton.HEIGHT * EmoteRows) + (EMOTE_BUTTON_GAP * (EmoteRows - 1));

    private const int CHAT_PROMPT_WIDTH = 300;
    private const int CHAT_PROMPT_PAD = 6;
    private const int CHAT_SEND_WIDTH = 60;

    /// <summary>Gap between a seat's plaque and the speech bubble pointing at it.</summary>
    private const int BUBBLE_GAP = 3;

    private const int BOARD_CARD_GAP = 4;

    private const int BOARD_ROW_WIDTH = (CardView.WIDTH * BOARD_SIZE) + (BOARD_CARD_GAP * (BOARD_SIZE - 1));
    private const int BOARD_TOP = FELT_CENTER_Y - CardView.HEIGHT - 8;
    private const int POT_TOP = BOARD_TOP + CardView.HEIGHT + 10;

    //narrow enough to pass between the left and right plaques, which reach further inward than they used to.
    private const int POT_BOX_WIDTH = 160;
    private const int POT_BOX_HEIGHT = CustomButton.HEIGHT;
    private const int POT_BOX_LEFT = FELT_CENTER_X - (POT_BOX_WIDTH / 2);

    //the action log sits ON the felt beneath the pot, where a dealer's call would be heard, rather than in the
    //strip below: the ring layout spends that strip on buttons and there is no room left under the table.
    private const int EVENT_TOP = POT_TOP + POT_BOX_HEIGHT + 6;
    //narrow enough to pass BETWEEN the two side seats rather than under them: at 300 it clears both, and the
    //felt is wider than the gap those seats leave.
    private const int EVENT_WIDTH = 300;
    private const int EVENT_LEFT = FELT_CENTER_X - (EVENT_WIDTH / 2);

    /// <summary>The "why" line sits directly under the event text, inside the felt, clear of the board above it.</summary>
    private const int WIN_REASON_TOP = EVENT_TOP + TextRenderer.CHAR_HEIGHT + 2;

    //-- button rows, below the felt --
    private const int ACTION_ROW_TOP = FELT_BOTTOM + 8;
    private const int ACTION_BUTTON_COUNT = 5;
    private const int ACTION_BUTTON_GAP = 6;
    private const int ACTION_ROW_WIDTH = 400;

    private const int ACTION_BUTTON_WIDTH
        = (ACTION_ROW_WIDTH - (ACTION_BUTTON_GAP * (ACTION_BUTTON_COUNT - 1))) / ACTION_BUTTON_COUNT;

    private const int ACTION_ROW_LEFT
        = (PANEL_WIDTH - ((ACTION_BUTTON_WIDTH * ACTION_BUTTON_COUNT) + (ACTION_BUTTON_GAP * (ACTION_BUTTON_COUNT - 1)))) / 2;

    private const int TABLE_ROW_TOP = ACTION_ROW_TOP + CustomButton.HEIGHT + 6;
    private const int TABLE_BUTTON_COUNT = 5;
    private const int TABLE_BUTTON_WIDTH = 96;
    private const int TABLE_BUTTON_GAP = 8;

    private const int TABLE_ROW_WIDTH
        = (TABLE_BUTTON_WIDTH * TABLE_BUTTON_COUNT) + (TABLE_BUTTON_GAP * (TABLE_BUTTON_COUNT - 1));

    private const int TABLE_ROW_LEFT = (PANEL_WIDTH - TABLE_ROW_WIDTH) / 2;

    private const int PANEL_HEIGHT = TABLE_ROW_TOP + CustomButton.HEIGHT + 4 + FRAME_BOTTOM_BORDER;

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

    /// <summary>The felt itself -- a muted table green, dark enough that cream cards and white text sit on it legibly.</summary>
    private static readonly SKColor FeltSurfaceColor = new(24, 78, 52, 255);

    /// <summary>The rail ringing the felt: the table's wooden edge, where the seat plaques rest.</summary>
    private static readonly SKColor FeltRailColor = new(48, 32, 18, 255);

    /// <summary>Hairline inside the rail, so the two greens read as a surface with an edge rather than a flat disc.</summary>
    private static readonly SKColor FeltSheenColor = new(46, 112, 78, 255);

    /// <summary>Warm gold outline marking the seat on the clock. Border-only, so it never recolors the seat's own text.</summary>
    private static readonly Color ActingSeatColor = new(255, 200, 60, 220);

    /// <summary>
    ///     Names for <c>PokerTableDisplayArgs.WinningHand</c>, indexed by the wire byte. Index 0 is the
    ///     no-showdown case. Kept client-side so the server sends one byte rather than a string per client.
    /// </summary>
    private static readonly string[] WinningHandNames =
    [
        "everyone else folded",
        "High Card",
        "One Pair",
        "Two Pair",
        "Three of a Kind",
        "Straight",
        "Flush",
        "Full House",
        "Four of a Kind",
        "Straight Flush"
    ];

    private readonly SoundSystem SoundSystem;

    private readonly UILabel TitleLabel;
    private readonly UILabel PotLabel;
    private readonly UILabel EventLabel;
    private readonly UILabel WinReasonLabel;

    //indexed by TABLE seat index, not by ring position -- SeatAnchor maps one to the other.
    private readonly UIPanel FeltPanel;

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
    private readonly CustomButton EmoteButton;
    private readonly UIPanel EmotePicker;
    private readonly CustomButton ChatButton;
    private readonly ChatPromptPanel ChatPrompt;

    //live speech bubbles, one entry per seat that is currently saying something
    private readonly List<ChatBubble> Bubbles = [];

    //in-flight action animations. Both are pure presentation: they are spawned by comparing one snapshot to the
    //last and never feed anything back into what is drawn from server state.
    private readonly List<ChipSlide> ChipSlides = [];
    private readonly List<ActionFlash> ActionFlashes = [];

    //what the previous snapshot said, so a change can be told from a repeat
    private readonly int[] PreviousCommitted = new int[SEAT_COUNT];
    private readonly string?[] PreviousAction = new string?[SEAT_COUNT];

    //false until the first snapshot after a Show has seeded the above. Without it, opening the panel onto a hand
    //already in progress would replay every wager and action that had happened before the player sat down.
    private bool AnimationsPrimed;

    //makes each animation's control name unique; RemoveChild matches on name
    private int AnimationSequence;

    /// <summary>The gold piece a <see cref="ChipSlide" /> carries. Shared, and never disposed -- one small texture for the process.</summary>
    private static readonly Texture2D CoinTexture = BuildCoin(10);

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

    /// <summary>
    ///     Raised with the chosen emote when the player picks one. Wired to <c>ConnectionManager.SendEmote</c>.
    /// </summary>
    /// <remarks>
    ///     Carries the <see cref="BodyAnimation" /> itself rather than a raw byte, unlike
    ///     <see cref="ActionRequested" />: that one is a poker action whose wire values this control defines, while
    ///     this is an existing world enum the send method already takes.
    /// </remarks>
    public event Action<BodyAnimation>? EmoteRequested;

    /// <summary>
    ///     Raised with what the player typed. Wired to <c>ConnectionManager.SendPublicMessage</c>.
    /// </summary>
    /// <remarks>
    ///     Ordinary public speech, not a table-only channel: it reaches the room the same way talking normally
    ///     does, and the bubble this panel draws is the same one the world would have drawn had the panel not
    ///     been covering it.
    /// </remarks>
    public event Action<string>? ChatRequested;

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

    /// <param name="soundSystem">Plays the your-turn cue.</param>
    /// <param name="aislingRenderer">
    ///     Draws the seated players' portraits. Appearance is not carried by the poker protocol at all -- it is
    ///     read from <c>WorldState</c>, where every player standing at the table already is.
    /// </param>
    /// <param name="creatureRenderer">Draws the monsters standing in for the court cards.</param>
    public PokerTableControl(SoundSystem soundSystem, AislingRenderer aislingRenderer, CreatureRenderer creatureRenderer)
        : base("_nsett", false)
    {
        ArgumentNullException.ThrowIfNull(soundSystem);
        ArgumentNullException.ThrowIfNull(aislingRenderer);
        ArgumentNullException.ThrowIfNull(creatureRenderer);

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
        //RequestDismissal, not Hide: this is a PLAYER-initiated close, and the player is the only one who ever
        //gets asked to confirm. See Hide's own remarks.
        OkButton = CreateCloseButton(RequestDismissal, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

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

        //── the felt, added before the seats so it draws beneath them: the plaques sit ON the rail ──
        FeltPanel = new UIPanel
        {
            X = FELT_SIDE_INSET,
            Y = FELT_TOP,
            Width = FELT_WIDTH,
            Height = FELT_HEIGHT,
            Background = BuildFelt(FELT_WIDTH, FELT_HEIGHT, FELT_RAIL_WIDTH),
            IsHitTestVisible = false
        };
        AddChild(FeltPanel);

        //── the six seat plaques, ringed clockwise around the felt ──
        for (var seat = 0; seat < SEAT_COUNT; seat++)
        {
            var (x, y) = SeatAnchor(seat);

            var panel = new SeatPanel(aislingRenderer, creatureRenderer)
            {
                X = x,
                Y = y
            };
            SeatPanels[seat] = panel;
            AddChild(panel);
        }

        //── community board: five slots, re-centred as a group on every snapshot so a three-card flop sits in the
        //   middle of the table rather than hard against the left of a five-wide strip. See LayOutBoard. ──
        for (var i = 0; i < BoardCards.Length; i++)
        {
            var card = new CardView(creatureRenderer)
            {
                Y = BOARD_TOP,
                Visible = false
            };
            BoardCards[i] = card;
            AddChild(card);
        }

        //── pot readout: a recessed plate centred on the felt, directly beneath the board ──
        var potBox = new UIPanel
        {
            X = POT_BOX_LEFT,
            Y = POT_TOP,
            Width = POT_BOX_WIDTH,
            Height = POT_BOX_HEIGHT,
            Background = BuildRecessedPanel(POT_BOX_WIDTH, POT_BOX_HEIGHT),
            IsHitTestVisible = false
        };
        AddChild(potBox);

        PotLabel = new UILabel
        {
            X = 8,
            Y = (POT_BOX_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = POT_BOX_WIDTH - 16,
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
            X = EVENT_LEFT,
            Y = EVENT_TOP,
            Width = EVENT_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(EventLabel);

        //the second line of the result banner: why the pot went where it did. Painted only while the server
        //reports winners, which is exactly the settle pause, so it needs no timer of its own.
        WinReasonLabel = new UILabel
        {
            X = EVENT_LEFT,
            Y = WIN_REASON_TOP,
            Width = EVENT_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.Gold,
            IsHitTestVisible = false,
            Visible = false
        };
        AddChild(WinReasonLabel);

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

        EmoteButton = CreateTableButton("Emote", 3);

        //── the emote picker, hidden until asked for ──
        EmotePicker = new UIPanel
        {
            X = FELT_CENTER_X - (EmotePickerWidth / 2),
            Y = FELT_CENTER_Y - (EmotePickerHeight / 2),
            Width = EmotePickerWidth,
            Height = EmotePickerHeight,
            Background = BuildRecessedPanel(EmotePickerWidth, EmotePickerHeight),
            Visible = false,

            //above the table but below the leave confirmation, which must never be covered
            ZIndex = 50
        };
        AddChild(EmotePicker);

        for (var i = 0; i < Emotes.Length; i++)
        {
            var (animation, caption) = Emotes[i];

            var button = new CustomButton(caption, EMOTE_BUTTON_WIDTH)
            {
                X = EMOTE_PICKER_PAD + ((i % EMOTE_COLUMNS) * (EMOTE_BUTTON_WIDTH + EMOTE_BUTTON_GAP)),
                Y = EMOTE_PICKER_PAD + ((i / EMOTE_COLUMNS) * (CustomButton.HEIGHT + EMOTE_BUTTON_GAP))
            };

            button.Clicked += () =>
            {
                EmoteRequested?.Invoke(animation);
                EmotePicker.Visible = false;
            };

            EmotePicker.AddChild(button);
        }

        //wired after the picker exists rather than beside the button: the handler captures it, and the field is
        //still null at the point the button is created.
        EmoteButton.Clicked += () =>
        {
            if (EmotePicker.Visible)
            {
                EmotePicker.Visible = false;

                return;
            }

            //only one of the two overlays at a time -- the mirror of ChatButton's handler below. Both sit at the
            //same ZIndex over the same middle of the felt, and the prompt, added later, would draw over the
            //picker and take the clicks meant for it.
            ChatPrompt.Close();
            EmotePicker.Visible = true;
        };

        ChatButton = CreateTableButton("Chat", 4);

        //── the say-something prompt ──
        ChatPrompt = new ChatPromptPanel(CHAT_PROMPT_WIDTH, CHAT_SEND_WIDTH, CHAT_PROMPT_PAD)
        {
            X = FELT_CENTER_X - (CHAT_PROMPT_WIDTH / 2),
            Y = FELT_CENTER_Y - 16,
            Visible = false,
            ZIndex = 50
        };
        ChatPrompt.Submitted += text => ChatRequested?.Invoke(text);
        AddChild(ChatPrompt);

        ChatButton.Clicked += () =>
        {
            if (ChatPrompt.Visible)
            {
                ChatPrompt.Close();

                return;
            }

            //only one of the two overlays at a time -- they occupy the same middle of the felt
            EmotePicker.Visible = false;
            //the same budget the HUD's own say box uses: what fits in "Name: message" on one 67-character line.
            //Anything past it is not rejected by the server, it is silently cut off for everyone who hears it,
            //and the budget depends on this player's name so it is read at open time rather than fixed up front.
            ChatPrompt.Open(ChatInputControl.PublicMessageMaxLength());
        };

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
    /// <summary>
    ///     Where seat <paramref name="seat" />'s plaque sits, as the top-left corner of a
    ///     <see cref="SEAT_WIDTH" />x<see cref="SEAT_HEIGHT" /> box in panel space.
    /// </summary>
    /// <remarks>
    ///     Seats run <b>clockwise from the left</b> -- left, top-left, top-right, right, bottom-right,
    ///     bottom-left -- which preserves the ordering the two-row layout had before it: that one ran left to
    ///     right along the top and then back right to left along the bottom, which is the same ring walked the
    ///     same way. Table seat 0 is still drawn at the same end of the table it always was, so a player who
    ///     knew where their seat appeared does not find it moved.
    ///     <para>
    ///         The anchors are written out rather than computed from an angle. A trigonometric ring would place
    ///         seats on the ellipse's true perimeter, which is wrong here: the plaques are rectangles that must
    ///         sit tangent to the rail without overlapping each other or running off the panel, and the four
    ///         corner positions need more inward bias than a circle gives them. Six hand-placed anchors are
    ///         honest about that; a formula would have to be fought with fudge factors.
    ///     </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     If <paramref name="seat" /> has no anchor. Deliberately louder than a computed fallback: a seat with
    ///     no place on the ring must be reported, not drawn somewhere plausible-looking off the felt.
    /// </exception>
    private static (int X, int Y) SeatAnchor(int seat)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seat);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(seat, SEAT_COUNT);

        const int LEFT_X = SEAT_EDGE_MARGIN;
        const int RIGHT_X = PANEL_WIDTH - SEAT_WIDTH - SEAT_EDGE_MARGIN;
        const int SIDE_Y = FELT_CENTER_Y - (SEAT_HEIGHT / 2);
        const int TOP_Y = FELT_TOP;
        const int BOTTOM_Y = FELT_BOTTOM - SEAT_HEIGHT;
        const int INNER_LEFT_X = FELT_CENTER_X - SEAT_SPREAD - SEAT_WIDTH;
        const int INNER_RIGHT_X = FELT_CENTER_X + SEAT_SPREAD;

        return seat switch
        {
            0 => (LEFT_X, SIDE_Y),
            1 => (INNER_LEFT_X, TOP_Y),
            2 => (INNER_RIGHT_X, TOP_Y),
            3 => (RIGHT_X, SIDE_Y),
            4 => (INNER_RIGHT_X, BOTTOM_Y),
            5 => (INNER_LEFT_X, BOTTOM_Y),
            _ => throw new ArgumentOutOfRangeException(nameof(seat), seat, "No ring anchor for this seat")
        };
    }

    /// <summary>
    ///     Paints the table: an antialiased felt ellipse ringed by a darker rail, on a transparent background so
    ///     the ornate frame's own backdrop still shows in the corners the table does not reach.
    /// </summary>
    /// <remarks>
    ///     Skia rather than a hand-rasterised ellipse, for the same reason
    ///     <see cref="BuildRecessedPanel" /> uses it: a coverage-tested ellipse of this size has visibly stepped
    ///     edges, and the antialiasing is what makes it read as a table rather than as a polygon.
    /// </remarks>
    /// <summary>Builds the gold piece: a filled disc with a darker rim, so it reads as a coin at ten pixels.</summary>
    private static Texture2D BuildCoin(int size)
    {
        var info = new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);

        surface.Canvas.Clear(SKColors.Transparent);

        var centre = size / 2f;

        using (var rim = new SKPaint())
        {
            rim.IsAntialias = true;
            rim.Style = SKPaintStyle.Fill;
            rim.Color = new SKColor(122, 86, 12, 255);
            surface.Canvas.DrawCircle(centre, centre, centre, rim);
        }

        using (var face = new SKPaint())
        {
            face.IsAntialias = true;
            face.Style = SKPaintStyle.Fill;
            face.Color = new SKColor(232, 184, 48, 255);
            surface.Canvas.DrawCircle(centre, centre, centre - 1.2f, face);
        }

        using var snapshot = surface.Snapshot();

        return TextureConverter.ToTexture2D(snapshot);
    }

    private static Texture2D BuildFelt(int width, int height, int railWidth)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);

        surface.Canvas.Clear(SKColors.Transparent);

        var full = new SKRect(0, 0, width, height);

        using (var rail = new SKPaint())
        {
            rail.IsAntialias = true;
            rail.Style = SKPaintStyle.Fill;
            rail.Color = FeltRailColor;
            surface.Canvas.DrawOval(full, rail);
        }

        var inner = new SKRect(
            railWidth,
            railWidth,
            width - railWidth,
            height - railWidth);

        using (var felt = new SKPaint())
        {
            felt.IsAntialias = true;
            felt.Style = SKPaintStyle.Fill;
            felt.Color = FeltSurfaceColor;
            surface.Canvas.DrawOval(inner, felt);
        }

        //a hairline highlight just inside the rail: without it the two greens meet flat and the table reads as a
        //painted disc rather than a surface with an edge
        using (var sheen = new SKPaint())
        {
            sheen.IsAntialias = true;
            sheen.Style = SKPaintStyle.Stroke;
            sheen.StrokeWidth = 1;
            sheen.Color = FeltSheenColor;
            surface.Canvas.DrawOval(inner, sheen);
        }

        using var snapshot = surface.Snapshot();

        return TextureConverter.ToTexture2D(snapshot);
    }

    /// <summary>
    ///     Builds a recessed dark-fill panel bordered by dlgframe.epf's 8-piece border — the same primitive
    ///     <see cref="CustomButton" /> and <see cref="Slots.SlotMachineControl" /> use for their own inset
    ///     surfaces, so every recessed surface in this control family comes from one place.
    /// </summary>
    private static Texture2D BuildRecessedPanel(int width, int height)
        => DialogFrame.BuildRecessedTexture(RecessedFillColor, width, height);

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
            X = ACTION_ROW_LEFT + (column * (ACTION_BUTTON_WIDTH + ACTION_BUTTON_GAP)),
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
        var left = FELT_CENTER_X - (stripWidth / 2);

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

        DetectActionAnimations();

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

        //── result banner, line two: present exactly when the server reports winners ──
        if (vm.WinnerSeats.Count > 0)
        {
            WinReasonLabel.Text = vm.WinningHand < WinningHandNames.Length ? WinningHandNames[vm.WinningHand] : "a winning hand";
            WinReasonLabel.Visible = true;
        } else
            WinReasonLabel.Visible = false;

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
        //a rejection can arrive for a panel that is already gone: the Closed echo of a server-pushed Close is
        //answered NotSeated (see WorldScreen.Wiring). There is nothing to show it on and nothing to hold.
        if (!Visible)
            return;

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

    /// <summary>
    ///     Shows <paramref name="message" /> over the seat held by <paramref name="entityId" />, if that player is
    ///     at this table.
    /// </summary>
    /// <remarks>
    ///     Called from the same handler that adds the world's own bubble, so a seated player's speech appears in
    ///     both places and neither can drift from the other. Anyone who is not seated here is ignored -- their
    ///     bubble is drawn on the floor behind this panel, where it belongs.
    /// </remarks>
    public void ShowChatBubble(uint entityId, string message, bool isShout)
    {
        if (!Visible || string.IsNullOrWhiteSpace(message))
            return;

        var seat = SeatOfEntity(entityId);

        if (seat < 0)
            return;

        //one bubble per seat: a player who talks twice replaces their own bubble rather than stacking two on top
        //of each other over the same head
        RemoveBubblesFor(entityId);

        var bubble = ChatBubble.Create(entityId, message, isShout, BubbleName(entityId));
        var (seatX, seatY) = SeatAnchor(seat);

        //the top row speaks downward and everyone else upward, so a bubble always opens onto the felt instead of
        //off the top of the panel
        var below = seatY <= FELT_TOP;

        bubble.TailOnTop = below;
        bubble.Y = below ? seatY + SEAT_HEIGHT + BUBBLE_GAP : seatY - bubble.Height - BUBBLE_GAP;

        //centred over the portrait rather than the plaque: the portrait is the face it belongs to
        var wanted = seatX + PORTRAIT_X_IN_SEAT + (PORTRAIT_SIZE / 2) - (bubble.Width / 2);
        bubble.X = Math.Clamp(wanted, 2, Math.Max(2, PANEL_WIDTH - bubble.Width - 2));

        //above the felt and the plaques, below the pickers and the confirmation
        bubble.ZIndex = 40;

        Bubbles.Add(bubble);
        AddChild(bubble);
    }

    /// <summary>
    ///     Spawns the action animations by diffing this snapshot against the last.
    /// </summary>
    /// <remarks>
    ///     Gold reaching the pot is detected as a seat's committed total going UP, which covers calls, bets,
    ///     raises and both blinds without needing the server to say which of those it was. A total that goes DOWN
    ///     is the hand settling and the seat resetting to zero -- nothing travels for that, because nothing was
    ///     wagered.
    /// </remarks>
    private void DetectActionAnimations()
    {
        var potX = FELT_CENTER_X;
        var potY = POT_TOP + (POT_BOX_HEIGHT / 2);

        for (var seat = 0; seat < SEAT_COUNT; seat++)
        {
            var info = SeatLookup[seat];
            var committed = info?.Committed ?? 0;
            var action = info?.LastAction;

            if (AnimationsPrimed)
            {
                var (seatX, seatY) = SeatAnchor(seat);
                var originX = seatX + (SEAT_WIDTH / 2);
                var originY = seatY + (SEAT_HEIGHT / 2);

                if (committed > PreviousCommitted[seat])
                    SpawnChipSlide(originX, originY, potX, potY);

                if (!string.IsNullOrEmpty(action) && !string.Equals(action, PreviousAction[seat], StringComparison.Ordinal))
                    SpawnActionFlash(action, originX, originY, potX, potY);
            }

            PreviousCommitted[seat] = committed;
            PreviousAction[seat] = action;
        }

        AnimationsPrimed = true;
    }

    private void SpawnChipSlide(
        int fromX,
        int fromY,
        int toX,
        int toY)
    {
        var slide = new ChipSlide(
            $"PokerChip{AnimationSequence++}",
            fromX,
            fromY,
            toX,
            toY)
        {
            //over the felt and the plaques, under the pickers and the confirmation
            ZIndex = 45
        };

        ChipSlides.Add(slide);
        AddChild(slide);
    }

    private void SpawnActionFlash(
        string action,
        int originX,
        int originY,
        int towardX,
        int towardY)
    {
        var tint = action switch
        {
            "Fold"  => LegendColors.DimGray,
            "Check" => LegendColors.White,
            "Call"  => LegendColors.PastelYellow,
            _       => LegendColors.Gold
        };

        var flash = new ActionFlash(
            $"PokerFlash{AnimationSequence++}",
            action,
            tint,
            originX - (TextRenderer.MeasureWidth(action) / 2),
            originY,
            towardX,
            towardY)
        {
            ZIndex = 46
        };

        ActionFlashes.Add(flash);
        AddChild(flash);
    }

    private void ClearAnimations()
    {
        foreach (var slide in ChipSlides)
            RemoveChild(slide.Name);

        foreach (var flash in ActionFlashes)
            RemoveChild(flash.Name);

        ChipSlides.Clear();
        ActionFlashes.Clear();

        Array.Clear(PreviousCommitted);
        Array.Clear(PreviousAction);
        AnimationsPrimed = false;
    }

    private int SeatOfEntity(uint entityId)
    {
        for (var seat = 0; seat < SeatPanels.Length; seat++)
            if (SeatPanels[seat].SubjectId == entityId)
                return seat;

        return -1;
    }

    //RemoveChild takes a name and disposes what it finds, so every bubble is named after the player it belongs
    //to. One name per seated player also means a second bubble for the same player cannot exist.
    private static string BubbleName(uint entityId) => $"PokerBubble{entityId}";

    private void RemoveBubblesFor(uint entityId)
    {
        RemoveChild(BubbleName(entityId));
        Bubbles.RemoveAll(bubble => bubble.EntityId == entityId);
    }

    private void ClearBubbles()
    {
        foreach (var bubble in Bubbles)
            RemoveChild(bubble.Name);

        Bubbles.Clear();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        var deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (RejectHoldRemaining > 0f)
            RejectHoldRemaining = Math.Max(0f, RejectHoldRemaining - deltaSeconds);

        TickShotClock(deltaSeconds);

        //emote frames advance every frame in WorldScreen.Update, far more often than a snapshot arrives, so the
        //portraits are polled here rather than repainted from server state.
        foreach (var panel in SeatPanels)
            panel.TickEmote();

        //each of these ages itself in its own Update; this only retires the ones that have finished
        for (var i = Bubbles.Count - 1; i >= 0; i--)
        {
            if (!Bubbles[i].IsExpired)
                continue;

            RemoveChild(Bubbles[i].Name);
            Bubbles.RemoveAt(i);
        }

        for (var i = ChipSlides.Count - 1; i >= 0; i--)
        {
            if (!ChipSlides[i].IsExpired)
                continue;

            RemoveChild(ChipSlides[i].Name);
            ChipSlides.RemoveAt(i);
        }

        for (var i = ActionFlashes.Count - 1; i >= 0; i--)
        {
            if (!ActionFlashes[i].IsExpired)
                continue;

            RemoveChild(ActionFlashes[i].Name);
            ActionFlashes.RemoveAt(i);
        }
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

        //that repaint primed the animation baseline from whatever the view model held -- on a fresh open, an
        //empty roster. Un-prime so the first snapshot the SERVER sends after opening seeds it instead; otherwise
        //every seat's last action from the previous hand flashes the moment the player sits down.
        AnimationsPrimed = false;
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
        //already hidden: nothing below has anything to tear down, and base.Hide() is not a no-op on a hidden
        //control -- InputDispatcher.RemoveControl falls through to re-focusing whatever is now on top, which
        //would take the keyboard away from a textbox the player has since clicked into. The server's Close
        //display arrives after every player-initiated close and lands exactly here.
        if (!Visible)
            return;

        //a confirmation still standing when the panel goes away -- the server closing the session out from under
        //an open prompt is the case that matters -- must not be left visible or on the input stack.
        ConfirmDialog.Hide();
        EmotePicker.Visible = false;
        ChatPrompt.Close();
        ClearBubbles();
        ClearAnimations();
        WinReasonLabel.Visible = false;

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
        Closed?.Invoke();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            //an open picker is what Escape dismisses first: closing the whole table out from under someone who
            //only meant to back out of the emote list would forfeit their gold to the pot.
            if (ChatPrompt.Visible)
            {
                ChatPrompt.Close();
                e.Handled = true;

                return;
            }

            if (EmotePicker.Visible)
            {
                EmotePicker.Visible = false;
                e.Handled = true;

                return;
            }

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
        private const int PAD = SEAT_PAD;
        private const int HOLE_CARDS = 2;

        //three columns: portrait, details, hole cards. Nothing is stacked on top of anything else, which is the
        //whole point -- see SEAT_WIDTH's remarks for what stacking cost.
        private const int PORTRAIT_X = PAD;
        private const int PORTRAIT_Y = (SEAT_HEIGHT - PORTRAIT_SIZE) / 2;

        private const int TEXT_X = PORTRAIT_X + PORTRAIT_SIZE + SEAT_TEXT_GAP;
        private const int TEXT_WIDTH = SEAT_TEXT_WIDTH;

        private const int BADGE_WIDTH = 14;
        private const int CLOCK_WIDTH = 20;
        private const int NAME_WIDTH = TEXT_WIDTH - BADGE_WIDTH - CLOCK_WIDTH;

        //four rows -- name, gold, wager, last action -- filling the plaque's height beside the cards.
        private const int ROW_TOP = 3;
        private const int NAME_TOP = ROW_TOP;
        private const int GOLD_TOP = ROW_TOP + TextRenderer.CHAR_HEIGHT;
        private const int COMMITTED_TOP = ROW_TOP + (TextRenderer.CHAR_HEIGHT * 2);
        private const int LAST_ACTION_TOP = ROW_TOP + (TextRenderer.CHAR_HEIGHT * 3);

        private const int CARD_COLUMN_X = TEXT_X + TEXT_WIDTH + SEAT_TEXT_GAP;
        private const int CARD_GAP = SEAT_CARD_GAP;
        private const int CARD_COLUMN_WIDTH = SEAT_CARDS_WIDTH;
        private const int CARD_ROW_TOP = PAD;

        private readonly UILabel NameLabel;
        private readonly UILabel DealerBadge;
        private readonly UILabel GoldLabel;
        private readonly UILabel CommittedLabel;
        private readonly UILabel LastActionLabel;
        private readonly UILabel ClockLabel;
        private readonly UIPanel ActingOutline;
        private readonly CardView[] Cards = new CardView[HOLE_CARDS];
        private readonly PortraitView Portrait;
        private readonly CreatureRenderer CreatureRenderer;

        private bool Occupied;

        //deliberately seeded to a value SetClock can never be handed, so the very first call -- including
        //SetClock(null) -- still writes through to the label.
        private int? RenderedClock = -1;

        public SeatPanel(AislingRenderer aislingRenderer, CreatureRenderer creatureRenderer)
        {
            Width = SEAT_WIDTH;
            Height = SEAT_HEIGHT;
            Background = BuildRecessedPanel(SEAT_WIDTH, SEAT_HEIGHT);
            IsHitTestVisible = false;
            CreatureRenderer = creatureRenderer;

            Portrait = new PortraitView(aislingRenderer)
            {
                X = PORTRAIT_X,
                Y = PORTRAIT_Y
            };
            AddChild(Portrait);

            NameLabel = AddRow(
                TEXT_X,
                NAME_TOP,
                NAME_WIDTH,
                HorizontalAlignment.Left);

            //the dealer button marker. Visible ONLY when the table's ButtonIndex is this seat -- a null
            //ButtonIndex leaves it hidden on every seat, which is exactly what "there is no button" looks like.
            DealerBadge = AddRow(
                TEXT_X + NAME_WIDTH,
                NAME_TOP,
                BADGE_WIDTH,
                HorizontalAlignment.Right);
            DealerBadge.ForegroundColor = LegendColors.Gold;
            DealerBadge.Text = "D";
            DealerBadge.Visible = false;

            GoldLabel = AddRow(
                TEXT_X,
                GOLD_TOP,
                TEXT_WIDTH,
                HorizontalAlignment.Left);

            CommittedLabel = AddRow(
                TEXT_X,
                COMMITTED_TOP,
                TEXT_WIDTH,
                HorizontalAlignment.Left);

            LastActionLabel = AddRow(
                TEXT_X,
                LAST_ACTION_TOP,
                TEXT_WIDTH,
                HorizontalAlignment.Left);

            for (var i = 0; i < Cards.Length; i++)
            {
                var card = new CardView(creatureRenderer)
                {
                    X = CARD_COLUMN_X + (i * (CardView.WIDTH + CARD_GAP)),
                    Y = CARD_ROW_TOP,
                    Visible = false
                };
                Cards[i] = card;
                AddChild(card);
            }

            //shares the name row with the dealer badge: the plaque has four rows and all four are spoken for.
            ClockLabel = AddRow(
                TEXT_X + NAME_WIDTH + BADGE_WIDTH,
                NAME_TOP,
                CLOCK_WIDTH,
                HorizontalAlignment.Right);
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

        /// <summary>Advances this seat's portrait emote. Driven from the control's own per-frame Update.</summary>
        public void TickEmote() => Portrait.Tick();

        /// <summary>The world id of the player sitting here, or null when the seat is empty or they are out of view.</summary>
        public uint? SubjectId => Portrait.SubjectId;

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
                //an empty plaque has no portrait and no columns to line up with, so the word sits in the middle
                //of the whole box rather than in the name slot beside a face that is not there
                NameLabel.X = 0;
                NameLabel.Y = (SEAT_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2;
                NameLabel.Width = SEAT_WIDTH;
                NameLabel.HorizontalAlignment = HorizontalAlignment.Center;

                NameLabel.Text = "Empty";
                NameLabel.ForegroundColor = LegendColors.DarkGray;
                GoldLabel.Text = string.Empty;
                CommittedLabel.Text = string.Empty;
                LastActionLabel.Text = string.Empty;
                ClockLabel.Visible = false;
                RenderedClock = -1;
                Portrait.ShowPlayer(0);

                foreach (var card in Cards)
                    card.ShowNothing();

                return;
            }

            var seat = info!;

            //back to the name slot beside the portrait: the empty branch above moves this label to the centre of
            //the plaque, and a seat that fills up has to put it back
            NameLabel.X = TEXT_X;
            NameLabel.Y = NAME_TOP;
            NameLabel.Width = NAME_WIDTH;
            NameLabel.HorizontalAlignment = HorizontalAlignment.Left;

            Portrait.ShowPlayer(seat.EntityId);

            //dimmed the moment a seat is out of the hand, by either route the server reports it. The dimming is
            //the seat's whole "not in this one" signal, so it covers every line at once rather than one label.
            var inactive = seat.HasFolded || seat.IsSittingOut;
            var dimmed = LegendColors.DimGray;

            //the local player's own seat reads in gold so it is findable at a glance -- the panel is otherwise six
            //identical boxes, and nothing said which one was yours.
            NameLabel.Text = seat.Name;

            NameLabel.ForegroundColor = inactive ? dimmed :
                isYou ? LegendColors.Gold : LegendColors.White;

            //only your own purse is on the wire for you -- the server sends zero for every other seat -- so a
            //placeholder rather than "Gold: 0", which would read as a player who is broke instead of a number
            //that was deliberately withheld. Plain ASCII: the bitmap font's coverage above 126 is not assured.
            GoldLabel.Text = isYou ? $"Gold: {seat.Gold:N0}" : "-";
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
        public const int WIDTH = 32;
        public const int HEIGHT = 50;

        /// <summary>Rank glyphs are drawn at double size; the bitmap font stays crisp at integer scales.</summary>
        private const int RANK_SCALE = 2;

        private const int CORNER_INSET = 3;

        /// <summary>Edge of the square the suit pip is drawn into, in the card's top-right corner.</summary>
        private const int PIP_SIZE = 14;

        /// <summary>
        ///     Edge of the large pip filling a number card's body.
        /// </summary>
        /// <remarks>
        ///     The corner pip alone is not enough to tell a spade from a club at this size -- both are a dark blob
        ///     with a stem once they are 14 pixels across. A number card has nothing else in its body (the court
        ///     cards' monster art goes there), so the suit is drawn again, large, where there is room for the
        ///     shape to actually read.
        /// </remarks>
        private const int BODY_PIP_SIZE = 20;

        /// <summary>Resolution the pip paths are rasterised at before being scaled down to <see cref="PIP_SIZE" />.</summary>
        private const int PIP_SOURCE_SIZE = 32;

        /// <summary>
        ///     The four suit pips, indexed by the server's suit ordering: clubs, diamonds, hearts, spades.
        /// </summary>
        /// <remarks>
        ///     Real shapes rather than the letters C/D/H/S this drew first -- the Dark Ages bitmap font has no
        ///     glyphs for the pips, so they are rasterised from Skia paths instead of typed.
        ///     <para>
        ///         Shared across every card and deliberately never disposed. They are four small textures held for
        ///         the life of the process, and the alternative -- one set per <see cref="CardView" /> -- would
        ///         build sixty-eight of them for seventeen card slots. Nothing assigns them to
        ///         <see cref="UIPanel.Background" />, which is the one field the base class disposes, so the
        ///         hazard the per-instance face and back textures exist to avoid does not apply here.
        ///     </para>
        /// </remarks>
        private static readonly Texture2D[] SuitPips = BuildSuitPips();

        /// <summary>The art window the face-card monster is fitted into, below the rank strip.</summary>
        private const int ART_TOP = CORNER_INSET + (TextRenderer.CHAR_HEIGHT * RANK_SCALE);

        private const int ART_INSET = 2;

        /// <summary>Which monster stands in for each court card. Number cards carry no art.</summary>
        private const int SPRITE_JACK = 1263; //Red Mantis
        private const int SPRITE_QUEEN = 1264; //Dark Mantis
        private const int SPRITE_KING = 1265; //Blue Mantis
        private const int SPRITE_ACE = 1266; //Kobold -- the slot machine's jackpot symbol, for the highest card

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

        /// <summary>Resolves the court-card monsters. Null on the board's cards only if none was supplied.</summary>
        private readonly CreatureRenderer? CreatureRenderer;

        //face-up state, drawn by Draw rather than held in child labels: the rank is scaled, and UILabel has no
        //scale. Everything here is set only by ShowFace.
        private string RankText = string.Empty;
        private int Suit;
        private Color Ink = BlackSuitInk;
        private int ArtSpriteId;
        private bool FaceUp;

        public CardView(CreatureRenderer? creatureRenderer = null)
        {
            Width = WIDTH;
            Height = HEIGHT;
            IsHitTestVisible = false;
            CreatureRenderer = creatureRenderer;

            FaceTexture = BuildFace();
            BackTexture = BuildBack();
            Background = BackTexture;
        }

        private static Texture2D[] BuildSuitPips()
            => [BuildPip(DrawClub), BuildPip(DrawDiamond), BuildPip(DrawHeart), BuildPip(DrawSpade)];

        private static Texture2D BuildPip(Action<SKCanvas, SKPaint> draw)
        {
            var info = new SKImageInfo(PIP_SOURCE_SIZE, PIP_SOURCE_SIZE, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);

            surface.Canvas.Clear(SKColors.Transparent);

            using (var paint = new SKPaint())
            {
                paint.IsAntialias = true;
                paint.Style = SKPaintStyle.Fill;

                //white so the draw-time tint decides the ink colour
                paint.Color = SKColors.White;

                draw(surface.Canvas, paint);
            }

            using var snapshot = surface.Snapshot();

            return TextureConverter.ToTexture2D(snapshot);
        }

        private static void DrawDiamond(SKCanvas canvas, SKPaint paint)
        {
            using var path = new SKPath();
            path.MoveTo(16, 1);
            path.LineTo(29, 16);
            path.LineTo(16, 31);
            path.LineTo(3, 16);
            path.Close();
            canvas.DrawPath(path, paint);
        }

        private static void DrawHeart(SKCanvas canvas, SKPaint paint)
        {
            using var path = new SKPath();
            path.MoveTo(16, 30);
            path.CubicTo(2, 19, 2, 7, 9, 4);
            path.CubicTo(13, 2, 16, 5, 16, 9);
            path.CubicTo(16, 5, 19, 2, 23, 4);
            path.CubicTo(30, 7, 30, 19, 16, 30);
            path.Close();
            canvas.DrawPath(path, paint);
        }

        private static void DrawSpade(SKCanvas canvas, SKPaint paint)
        {
            using var path = new SKPath();

            //a sharp apex and wide low lobes: the point at the top is the only thing separating a spade from a
            //club once both are a dark blob on a stem, so it is drawn tall and narrow rather than rounded.
            path.MoveTo(16, 2);
            path.CubicTo(26, 12, 31, 18, 26, 23);
            path.CubicTo(22, 26, 18, 24, 16, 21);
            path.CubicTo(14, 24, 10, 26, 6, 23);
            path.CubicTo(1, 18, 6, 12, 16, 2);
            path.Close();
            canvas.DrawPath(path, paint);

            //a flared foot, wider than the club's, so the two silhouettes differ below as well as above
            using var stem = new SKPath();
            stem.MoveTo(16, 19);
            stem.CubicTo(18, 26, 20, 28, 23, 31);
            stem.LineTo(9, 31);
            stem.CubicTo(12, 28, 14, 26, 16, 19);
            stem.Close();
            canvas.DrawPath(stem, paint);
        }

        private static void DrawClub(SKCanvas canvas, SKPaint paint)
        {
            canvas.DrawCircle(16, 9, 7, paint);
            canvas.DrawCircle(8, 20, 7, paint);
            canvas.DrawCircle(24, 20, 7, paint);

            using var stem = new SKPath();
            stem.MoveTo(16, 18);
            stem.LineTo(20, 31);
            stem.LineTo(12, 31);
            stem.Close();
            canvas.DrawPath(stem, paint);
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

            Background = FaceTexture;
            RankText = rankText;
            Suit = suit;
            Ink = suit is 1 or 2 ? RedSuitInk : BlackSuitInk;

            ArtSpriteId = rank switch
            {
                11 => SPRITE_JACK,
                12 => SPRITE_QUEEN,
                13 => SPRITE_KING,
                14 => SPRITE_ACE,
                _  => 0
            };

            FaceUp = true;
            Visible = true;
        }

        /// <summary>Draws a card back — the rendering of "the server sent nothing for this slot".</summary>
        public void ShowBack()
        {
            Background = BackTexture;
            FaceUp = false;
            Visible = true;
        }

        /// <inheritdoc />
        /// <remarks>
        ///     The rank is drawn here rather than held in a <see cref="UILabel" /> because it is drawn at
        ///     <see cref="RANK_SCALE" /> and UILabel has no scale. The court monster is drawn under the glyphs so
        ///     the rank stays readable over it.
        /// </remarks>
        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);

            if (!Visible || !FaceUp)
                return;

            if (ArtSpriteId != 0)
                DrawCourtArt(spriteBatch);
            else
                spriteBatch.Draw(
                    SuitPips[Suit],
                    new Rectangle(
                        ScreenX + ((WIDTH - BODY_PIP_SIZE) / 2),
                        ScreenY + ART_TOP,
                        BODY_PIP_SIZE,
                        BODY_PIP_SIZE),
                    Ink);

            TextRenderer.DrawText(
                spriteBatch,
                new Vector2(ScreenX + CORNER_INSET, ScreenY + CORNER_INSET),
                RankText,
                Ink,
                false,
                1f,
                false,
                RANK_SCALE);

            //the pip is drawn white and tinted here, so one texture per suit serves both ink colours
            var pip = SuitPips[Suit];

            spriteBatch.Draw(
                pip,
                new Rectangle(
                    ScreenX + WIDTH - 2 - PIP_SIZE,
                    ScreenY + CORNER_INSET + 1,
                    PIP_SIZE,
                    PIP_SIZE),
                Ink);
        }

        /// <summary>
        ///     Fits the court card's monster into the art window, scaled down to fit rather than cropped.
        /// </summary>
        /// <remarks>
        ///     A missing renderer, sprite or frame simply leaves the card without art -- a court card still reads
        ///     correctly from its rank and suit, so there is nothing here worth failing a draw over.
        /// </remarks>
        private void DrawCourtArt(SpriteBatch spriteBatch)
        {
            if (CreatureRenderer is null)
                return;

            var (frameIndex, flip) = CreatureRenderer.GetAnimInfo(ArtSpriteId) is { } info
                ? AnimationSystem.GetCreatureIdleFrame(in info, Direction.Down)
                : (0, false);

            if (CreatureRenderer.GetFrame(ArtSpriteId, frameIndex) is not { } frame)
                return;

            var texture = frame.Texture;

            if (texture is null)
                return;

            var dest = new Rectangle(
                ScreenX + ART_INSET,
                ScreenY + ART_TOP,
                WIDTH - (ART_INSET * 2),
                HEIGHT - ART_TOP - ART_INSET);

            var visible = Rectangle.Intersect(dest, ClipRect);

            if (visible is not { Width: > 0, Height: > 0 })
                return;

            Texture2D actual;
            Rectangle source;

            if (texture is CachedTexture2D { AtlasRegion: { } region })
            {
                actual = region.Atlas;
                source = region.SourceRect;
            } else
            {
                actual = texture;
                source = new Rectangle(0, 0, texture.Width, texture.Height);
            }

            spriteBatch.Draw(
                actual,
                visible,
                source,
                Color.White,
                0f,
                Vector2.Zero,
                flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                0f);
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

    /// <summary>
    ///     A seated player's head-and-shoulders portrait, cropped out of their full front-facing idle figure.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The appearance does not come from the poker protocol.</b> <c>PokerSeatEntry</c> carries a name and
    ///         nothing about how anyone looks, and it was not extended to: every player seated at this table is
    ///         standing on a tile beside the merchant, two squares away at most, so they are already tracked in
    ///         <see cref="WorldState" /> with a full <see cref="AislingAppearance" />. Reading it from there costs
    ///         no packet and cannot disagree with what is drawn on the floor.
    ///     </para>
    ///     <para>
    ///         Followed by entity id -- the one <c>PokerSeatEntry.EntityId</c> carries, which is the same id the
    ///         floor tracks the player under -- and re-resolved every frame, because the entity behind an id is not
    ///         stable: a same-map refresh rebuilds the whole list, and a player can morph or change clothes while
    ///         seated. A lookup by id is a dictionary read, cheap enough to do six times a frame where a name scan
    ///         was not. A player who is momentarily not in view keeps their last figure; one morphed into a
    ///         creature form (which clears <see cref="WorldEntity.Appearance" />) gets no portrait -- the plaque
    ///         still names them, so nothing is lost but the picture.
    ///     </para>
    /// </remarks>
    private sealed class PortraitView : UIPanel
    {
        /// <summary>The front-facing idle pose, matching what <c>AvatarCapture</c> uses for the launcher card.</summary>
        private const int FRONT_IDLE_FRAME = 5;

        private const string IDLE_ANIM = "04";

        /// <summary>How much of the figure's width to keep, centred on the canvas's own centre line.</summary>
        /// <remarks>
        ///     Kept close to the head's own width. A wider window is not a bigger portrait -- it is the same head
        ///     with more empty canvas around it, scaled down to fit the same box, which is what made the first
        ///     attempt look tiny.
        /// </remarks>
        private const int CROP_WIDTH = 30;

        /// <summary>How far down from the top of the head to keep -- head and shoulders, not the body.</summary>
        private const int CROP_HEIGHT = 30;

        private readonly AislingRenderer Renderer;

        private Texture2D? Figure;
        private AislingAppearance? RenderedAppearance;
        private int FaceTop;

        //the world entity this portrait is following, so the per-frame emote check is a field read rather than a
        //scan of every visible entity six times a frame
        private WorldEntity? Subject;

        //the id this portrait follows, kept apart from Subject so a seat keeps its identity through the moments
        //its entity is not in the list -- that is what lets a chat bubble still find the seat then
        private uint SubjectEntityId;
        private int RenderedEmoteFrame = -1;

        /// <summary>The world id of whoever this portrait is following, used to match incoming speech to a seat.</summary>
        public uint? SubjectId => SubjectEntityId == 0 ? null : SubjectEntityId;

        public PortraitView(AislingRenderer renderer)
        {
            ArgumentNullException.ThrowIfNull(renderer);

            Renderer = renderer;
            Width = PORTRAIT_SIZE;
            Height = PORTRAIT_SIZE;
            IsHitTestVisible = false;
        }

        /// <summary>
        ///     Points this portrait at <paramref name="name" />, re-rendering only when the player or their
        ///     appearance actually changed.
        /// </summary>
        /// <remarks>
        ///     <see cref="AislingRenderer.Render" /> composites a fresh texture on every call -- the renderer's own
        ///     cache is keyed by entity id and serves world drawing, not this -- so calling it once per snapshot
        ///     per seat would allocate and leak a texture several times a second. The appearance is compared as
        ///     well as the name so a player who re-dyes or re-equips still refreshes.
        /// </remarks>
        /// <summary>Follows the player with world entity id <paramref name="entityId" />, or nobody for 0.</summary>
        public void ShowPlayer(uint entityId)
        {
            if (entityId != SubjectEntityId)
            {
                SubjectEntityId = entityId;
                Subject = null;
                RenderedAppearance = null;
                RenderedEmoteFrame = -1;

                //a new subject must never be shown wearing the old one's face, even for the frames before it
                //resolves
                Render();
            }

            SyncSubject();
        }

        /// <summary>
        ///     Re-resolves the followed entity and repaints if how they look has changed.
        /// </summary>
        private void SyncSubject()
        {
            if (SubjectEntityId == 0)
                return;

            var current = WorldState.GetEntity(SubjectEntityId);

            //absent for a moment -- the entity list being rebuilt underneath us -- keep following the id and keep
            //the last figure: the plaque still names them, and a blank for one refresh is worse than a stale face
            if (current is null)
            {
                Subject = null;

                return;
            }

            Subject = current;

            if (Nullable.Equals(current.Appearance, RenderedAppearance))
                return;

            RenderedAppearance = current.Appearance;
            MeasureFace();
            Render();
        }

        /// <summary>
        ///     Finds the top of the head, from a render with no emote on it.
        /// </summary>
        /// <remarks>
        ///     Measured separately, and only when the player or their appearance changes, because an emote is
        ///     drawn ABOVE the head: with one playing it becomes the topmost content, and anchoring on "first row
        ///     with pixels" would frame the emote instead of the face. That is exactly what it did -- a bubble
        ///     emote replaced the portrait for as long as it lasted.
        ///     <para>
        ///         The picker now offers only face emotes, which draw on the face and add nothing above the
        ///         hairline, so in practice the two measurements agree. This stays anyway: it costs one render per
        ///         appearance change and it is the only thing standing between a future bubble emote and that bug
        ///         coming back.
        ///     </para>
        /// </remarks>
        private void MeasureFace()
        {
            FaceTop = 0;

            if (RenderedAppearance is not { } value)
                return;

            using var plain = Renderer.Render(
                in value,
                FRONT_IDLE_FRAME,
                out _,
                out _,
                IDLE_ANIM,
                false,
                true);

            if (plain is not null)
                FaceTop = FindContentTop(plain);
        }

        /// <summary>
        ///     Follows the seated player's emote, re-rendering only on the frames it actually changes.
        /// </summary>
        /// <remarks>
        ///     The emote plays over the character's head in the world, which nobody at this table can see -- the
        ///     panel covers it. Drawing it on the portrait is what makes the feature visible to the people it is
        ///     for. Gated on an actual frame change because each render composites a fresh texture; an emote is a
        ///     second or two of animation, so this is a short burst rather than steady churn.
        /// </remarks>
        public void Tick()
        {
            SyncSubject();

            var frame = Subject?.ActiveEmoteFrame ?? -1;

            if (frame == RenderedEmoteFrame)
                return;

            RenderedEmoteFrame = frame;
            Render();
        }

        private void Render()
        {
            Figure?.Dispose();
            Figure = null;

            if (RenderedAppearance is not { } value)
                return;

            Figure = Renderer.Render(
                in value,
                FRONT_IDLE_FRAME,
                out _,
                out var topPadding,
                IDLE_ANIM,
                false,
                true,
                RenderedEmoteFrame);

            //Neither out-parameter locates the top of the head. topPadding is extra canvas added above the
            //standard body for tall headwear (height minus COMPOSITE_HEIGHT), and cropping from it started below
            //the hairline; cropping from zero instead left the head sitting in the bottom of the frame, because
            //a composite is mostly empty above the figure. So measure it: find the first row that actually has
            //pixels in it. Exact for every body, hat and hairstyle, and it runs only when the portrait changes.
            //the head's position comes from MeasureFace, which looks at a figure with no emote on it; measuring
            //this one would anchor to whatever the emote drew above the hairline
            _ = topPadding;
        }

        /// <summary>
        ///     The first row of <paramref name="texture" /> holding any pixel worth seeing.
        /// </summary>
        /// <remarks>
        ///     Reads the composite back once per portrait change, which is rare -- a seat has to gain a player or
        ///     that player has to change how they look. Cheaper than it sounds and exact, which the two offsets
        ///     the renderer hands out are not: neither of them marks the top of the head.
        /// </remarks>
        private static int FindContentTop(Texture2D texture)
        {
            using var scope = new PixelBufferScope(texture);

            var pixels = scope.AsSpan();

            for (var y = 0; y < scope.Height; y++)
            {
                var row = y * scope.Width;

                for (var x = 0; x < scope.Width; x++)
                    //a threshold rather than zero: the composite's edges are antialiased, and a stray one-alpha
                    //pixel above the hairline would anchor the crop to nothing.
                    if (pixels[row + x].A > 16)
                        return y;
            }

            return 0;
        }

        /// <inheritdoc />
        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);

            if (!Visible || (Figure is null))
                return;

            var top = Math.Clamp(FaceTop, 0, Math.Max(0, Figure.Height - 1));

            var source = new Rectangle(
                Math.Max(0, AislingRenderer.CANVAS_CENTER_X - (CROP_WIDTH / 2)),
                top,
                Math.Min(CROP_WIDTH, Figure.Width),
                Math.Min(CROP_HEIGHT, Figure.Height - top));

            if (source is { Width: <= 0 } or { Height: <= 0 })
                return;

            var dest = new Rectangle(ScreenX, ScreenY, Width, Height);
            var visible = Rectangle.Intersect(dest, ClipRect);

            if (visible is not { Width: > 0, Height: > 0 })
                return;

            spriteBatch.Draw(Figure, visible, source, Color.White);
        }

        public override void Dispose()
        {
            Figure?.Dispose();
            Figure = null;

            base.Dispose();
        }
    }

    /// <summary>
    ///     The say-something prompt: a single line and a Send button.
    /// </summary>
    /// <remarks>
    ///     Built here rather than reusing the HUD's <c>ChatInputControl</c>, which is a channel-switching console
    ///     wired to the chat panel and its history. This needs one line of public speech and nothing else, and it
    ///     has to live inside the poker panel so it is not clipped by it.
    /// </remarks>
    private sealed class ChatPromptPanel : UIPanel
    {
        private readonly UITextBox Input;

        /// <summary>Raised with the typed text when the player sends it. Never raised with blank text.</summary>
        public event Action<string>? Submitted;

        public ChatPromptPanel(int width, int sendWidth, int pad)
        {
            Width = width;
            Height = CustomButton.HEIGHT + (pad * 2);
            Background = BuildRecessedPanel(width, CustomButton.HEIGHT + (pad * 2));

            Input = new UITextBox
            {
                X = pad,
                Y = pad + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
                Width = width - (pad * 3) - sendWidth,
                Height = TextRenderer.CHAR_HEIGHT,
                ForegroundColor = LegendColors.White
            };
            AddChild(Input);

            var send = new CustomButton("Send", sendWidth)
            {
                X = width - pad - sendWidth,
                Y = pad
            };
            send.Clicked += Submit;
            AddChild(send);
        }

        /// <param name="maxLength">The most characters the message may carry and still arrive whole.</param>
        public void Open(int maxLength)
        {
            Input.MaxLength = maxLength;
            Input.Text = string.Empty;
            Visible = true;

            //focused on open so the player can just type -- the button press was the decision to speak
            Input.IsFocused = true;
        }

        public void Close()
        {
            //dropped explicitly: a focused box left behind would keep swallowing keystrokes meant for the table
            Input.IsFocused = false;
            Input.Text = string.Empty;
            Visible = false;
        }

        /// <inheritdoc />
        /// <remarks>
        ///     Enter sends. A single-line <see cref="UITextBox" /> deliberately lets Enter bubble up rather than
        ///     handling it, which is what makes this possible from the parent.
        /// </remarks>
        public override void OnKeyDown(KeyDownEvent e)
        {
            if (e.Keycode == Keycode.Enter)
            {
                Submit();
                e.Handled = true;

                return;
            }

            base.OnKeyDown(e);
        }

        private void Submit()
        {
            var text = Input.Text.Trim();

            //an empty send is a mis-click, not a message: close without broadcasting silence
            if (text.Length > 0)
                Submitted?.Invoke(text);

            Close();
        }
    }

    /// <summary>
    ///     A single gold piece travelling from a seat to the pot.
    /// </summary>
    /// <remarks>
    ///     Spawned when a seat's committed total goes UP, which is the only way gold reaches the pot -- calls,
    ///     bets, raises and the blinds all land there. It is a presentation of a number the server already sent,
    ///     never a source of one: the pot readout and the seat's wager line are both painted from the snapshot,
    ///     and this only travels between them.
    /// </remarks>
    private sealed class ChipSlide : UIElement
    {
        private const float DURATION_MS = 520f;
        private const int COIN_SIZE = 10;

        private readonly float FromX;
        private readonly float FromY;
        private readonly float ToX;
        private readonly float ToY;

        private float Elapsed;

        public bool IsExpired => Elapsed >= DURATION_MS;

        public ChipSlide(
            string name,
            int fromX,
            int fromY,
            int toX,
            int toY)
        {
            Name = name;
            FromX = fromX;
            FromY = fromY;
            ToX = toX;
            ToY = toY;
            Width = COIN_SIZE;
            Height = COIN_SIZE;
            IsHitTestVisible = false;
            X = fromX;
            Y = fromY;
        }

        public override void Update(GameTime gameTime)
        {
            Elapsed += (float)gameTime.ElapsedGameTime.TotalMilliseconds;

            var t = Math.Clamp(Elapsed / DURATION_MS, 0f, 1f);

            //eased so the piece leaves the seat briskly and settles into the pot rather than arriving at speed
            var eased = 1f - ((1f - t) * (1f - t));

            X = (int)float.Lerp(FromX, ToX, eased);
            Y = (int)float.Lerp(FromY, ToY, eased);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible || IsExpired)
                return;

            //fades over the last third rather than the whole trip, so it reads as landing rather than dissolving
            var t = Math.Clamp(Elapsed / DURATION_MS, 0f, 1f);
            var alpha = t < 0.66f ? 1f : 1f - ((t - 0.66f) / 0.34f);

            spriteBatch.Draw(
                CoinTexture,
                new Rectangle(ScreenX, ScreenY, COIN_SIZE, COIN_SIZE),
                Color.White * alpha);
        }
    }

    /// <summary>
    ///     A seat's action, drifting toward the middle of the table and fading.
    /// </summary>
    /// <remarks>
    ///     The seat plaque already carries the action as a static line; this is the part that catches the eye when
    ///     it happens. Fold, Check, Call, Bet and Raise all get one -- the two that move gold get a
    ///     <see cref="ChipSlide" /> as well.
    /// </remarks>
    private sealed class ActionFlash : UIElement
    {
        private const float DURATION_MS = 950f;

        /// <summary>How far it travels toward the table's centre over its life.</summary>
        private const int DRIFT = 14;

        private readonly string Text;
        private readonly Color Tint;
        private readonly int OriginX;
        private readonly int OriginY;
        private readonly int DriftX;
        private readonly int DriftY;

        private float Elapsed;

        public bool IsExpired => Elapsed >= DURATION_MS;

        public ActionFlash(
            string name,
            string text,
            Color tint,
            int originX,
            int originY,
            int towardX,
            int towardY)
        {
            Name = name;
            Text = text;
            Tint = tint;
            OriginX = originX;
            OriginY = originY;
            IsHitTestVisible = false;

            //a unit step toward the table centre, so every seat's action drifts inward rather than every one of
            //them drifting up and off the panel
            var dx = towardX - originX;
            var dy = towardY - originY;
            var length = MathF.Max(1f, MathF.Sqrt((dx * dx) + (dy * dy)));

            DriftX = (int)(dx / length * DRIFT);
            DriftY = (int)(dy / length * DRIFT);

            Width = TextRenderer.MeasureWidth(text);
            Height = TextRenderer.CHAR_HEIGHT;
            X = originX;
            Y = originY;
        }

        public override void Update(GameTime gameTime) => Elapsed += (float)gameTime.ElapsedGameTime.TotalMilliseconds;

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible || IsExpired)
                return;

            var t = Math.Clamp(Elapsed / DURATION_MS, 0f, 1f);

            //holds at full strength for the first half so it is readable, then fades
            var alpha = t < 0.5f ? 1f : 1f - ((t - 0.5f) / 0.5f);

            X = OriginX + (int)(DriftX * t);
            Y = OriginY + (int)(DriftY * t);

            TextRenderer.DrawText(
                spriteBatch,
                new Vector2(ScreenX, ScreenY),
                Text,
                Tint,
                false,
                alpha,
                false);
        }
    }
}
