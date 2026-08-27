#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering.Utility;
using Chaos.Client.Systems;
using Chaos.Client.Utilities;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups.Slots;

/// <summary>
///     The slot machine window: three reels on the left, a permanent paytable rail beside them (the odds stay
///     visible while playing), a live jackpot figure across the top, and the Spin button in the footer. Reuses
///     the ornate dialog frame (see
///     <see cref="FramedDialogPanelBase" />), the same way <see cref="Market.MarketControl" /> and
///     <see cref="Bank.BankControl" /> do. Mounted on WorldScreen.Root, opened by the server's slot machine Open
///     display when the player sits at a machine's stool.
/// </summary>
/// <remarks>
///     <para>
///         This is a pure renderer, never a decider: every outcome (reel stops, payout, jackpot) is computed by the
///         server. Clicking Spin starts all three reels immediately (so they double as the loading indicator) and
///         sends a spin request; when the server's SpinResult display arrives, the reels land on its chosen stops,
///         staggered left-to-right, no sooner than <see cref="MIN_SPIN_SECONDS" /> after the click.
///     </para>
///     <para>
///         Like <see cref="Bank.BankControl" />, it borrows the <c>_nsett</c> prefab (there is no DAT-backed slots
///         control file, and <c>_nui_slots</c> does not exist in <c>controlFileList.txt</c>) purely for its
///         <c>"OK"</c> button, then overrides Width/Height — the frame art is drawn programmatically by
///         <see cref="FramedDialogPanelBase" /> from the live size.
///     </para>
///     <para>
///         State source: <see cref="WorldState.SlotMachine" /> has no Changed/Cleared event (unlike
///         <see cref="ViewModel.BankState" />). This control is instead driven by explicit calls from
///         <c>WorldScreen.ServerHandlers</c> (<see cref="Show" />/<see cref="OnSpinResult" />/
///         <see cref="RefreshJackpot" />/<see cref="OnRejected" />/<see cref="Hide" />) made right after the
///         corresponding view-model mutation — a deliberate choice: there is exactly one producer of slot machine
///         display packets, so an event would only add an indirection with no second subscriber to justify it.
///     </para>
///     <para>
///         The recessed reel window, per-cell grid, payline highlight, and jackpot display all reuse
///         <see cref="DialogFrame" />'s dlgframe.epf 8-piece border compositing — the same primitive
///         <see cref="CustomButton" />/<see cref="CustomTextBox" /> use for their own recessed frames — rather than
///         inventing a parallel drawing path.
///     </para>
/// </remarks>
public sealed class SlotMachineControl : FramedDialogPanelBase
{
    private const int REEL_COUNT = 3;

    //── canonical panel size ──
    //Both dimensions are DERIVED from the content below rather than hand-picked. That is deliberate: this layout
    //has twice accumulated dead space by pinning a total and letting the interior drift under it -- first by
    //reserving footer space for the full MAX_PAYTABLE_ROWS pool when the shipped catalog runs nine rows, then by
    //keeping a hand-written 420x360 while every interior gap around it was tuned. Deriving the totals means
    //tightening a gap now actually shrinks the window instead of leaving a fresh bare patch where the gap was.
    //
    //Width  = reel block + paytable rail, plus their gap and side margins.
    //Height = the footer's message line (driven by the taller of the reel window and a TYPICAL paytable render)
    //         plus the ornate frame's own bottom border.
    private const int PANEL_WIDTH = RAIL_RIGHT + CONTENT_RIGHT;
    private const int PANEL_HEIGHT = MESSAGE_TOP + TextRenderer.CHAR_HEIGHT + 1 + FRAME_BOTTOM_BORDER;

    //FramedDialogPanelBase paints a 47px-tall ornate bottom border over the panel's last 47 rows (it carries the
    //rivet strip and the Close button). Content has to end above it, so PANEL_HEIGHT budgets for it by name
    //rather than the previous approach of eyeballing a total and trusting the footer to clear it.
    private const int FRAME_BOTTOM_BORDER = 47;

    //the panel is centered horizontally but pinned this far from the top of the screen, matching Market/Bank.
    private const int TOP_MARGIN = 15;

    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private const int CONTENT_LEFT = 20;
    private const int CONTENT_RIGHT = 20;

    //── header ──
    //the jackpot readout spans the machine's whole body (reel window through the end of the paytable rail). It
    //used to span only the reel window's 184px, which left the entire top-right quarter of the panel bare wood
    //above the rail. The title is centered on the PANEL rather than on that body, so it reads as the window's
    //heading rather than as a label belonging to the block beneath it.
    private const int TITLE_TOP = 8;
    private const int JACKPOT_TOP = TITLE_TOP + TextRenderer.CHAR_HEIGHT + 4;
    private const int HEADER_WIDTH = RAIL_RIGHT - REEL_WINDOW_X;

    //the jackpot readout is a recessed display (see BuildRecessedPanel), not a plain label -- it shares the
    //shared custom-control height CustomButton/CustomTextBox use so it reads as a control, not decoration.
    private const int JACKPOT_BOX_HEIGHT = CustomButton.HEIGHT;

    //ReelControl's own Width/Height are fixed by its private CELL_W (56) / CELL_H (52) * 3 visible rows; it exposes
    //no public constants for them, so these mirror that known fixed footprint for our own layout math rather than
    //reaching into ReelControl's internals.
    private const int REEL_CELL_WIDTH = 56;
    private const int REEL_CELL_HEIGHT = 52;
    private const int REEL_STRIP_HEIGHT = REEL_CELL_HEIGHT * 3;

    private const int REEL_GAP = 4;
    private const int REEL_BLOCK_WIDTH = (REEL_CELL_WIDTH * REEL_COUNT) + (REEL_GAP * (REEL_COUNT - 1));

    //the recessed reel-window inset (dark fill + dlgframe border) drawn behind the 3x3 grid, sized a few px
    //larger than the bare reel block on every side so a strip of the recessed panel peeks out around it --
    //without this the reels sat as bare sprites directly on the wood background.
    private const int REEL_WINDOW_PADDING = 4;
    private const int REEL_WINDOW_X = CONTENT_LEFT - REEL_WINDOW_PADDING;
    private const int REEL_WINDOW_WIDTH = REEL_BLOCK_WIDTH + (REEL_WINDOW_PADDING * 2);
    private const int REEL_WINDOW_HEIGHT = REEL_STRIP_HEIGHT + (REEL_WINDOW_PADDING * 2);
    private const int REEL_WINDOW_TOP = JACKPOT_TOP + JACKPOT_BOX_HEIGHT + 6;
    private const int REEL_WINDOW_BOTTOM = REEL_WINDOW_TOP + REEL_WINDOW_HEIGHT;
    private const int REELS_TOP = REEL_WINDOW_TOP + REEL_WINDOW_PADDING;

    //── paytable rail: sized to its own content, not to whatever width happened to be left over ──
    private const int RAIL_GAP = 12;
    private const int RAIL_X = CONTENT_LEFT + REEL_BLOCK_WIDTH + RAIL_GAP;

    //the label column only ever has to hold PaytableIconRow's three 16px icons (54px total) in the normal case;
    //the remainder is slack for the plain-text fallback (see RefreshPaytableRows). It used to be 128px -- more
    //than twice what it renders -- and that surplus was most of the rail's reported empty space.
    private const int RAIL_LABEL_WIDTH = 78;
    private const int RAIL_WIDTH = RAIL_LABEL_WIDTH + MULT_COL_GAP + MULT_COL_WIDTH;
    private const int RAIL_RIGHT = RAIL_X + RAIL_WIDTH;

    //grew from 14 to 18 (+4) to fit PaytableIconRow.ICON_SIZE (16px, +1px padding top/bottom) -- a plain text
    //row only needed CHAR_HEIGHT(12)+2, but a legible creature icon needs more room than that.
    private const int PAYTABLE_ROW_HEIGHT = 18;

    //headroom above the 9 rows the shipped catalog defines per machine, plus an explicit "+N more" overflow row
    //rather than a hard, silent cap -- a paytable that vanishes past this without a trace would misrepresent the
    //machine's odds, which is the one thing this panel must never do.
    //
    //The pool is capped at what actually FITS above the frame's bottom border rather than at an aspirational
    //number: REELS_TOP(56) + 11*18 = 254, inside the PANEL_HEIGHT(312) - FRAME_BOTTOM_BORDER(47) = 265 budget.
    //It was 16, which no longer fits now that the panel is derived from the typical case; a 16-row pool would
    //have drawn its tail straight through the frame art. Rows past TYPICAL_PAYTABLE_ROWS still render, just into
    //the footer's vertical space -- the same accepted overlap as before, never a silent drop.
    private const int MAX_PAYTABLE_ROWS = 11;

    //the footer's vertical position is driven by a TYPICAL row count, not the full MAX_PAYTABLE_ROWS pool --
    //reserving worst-case space for a paytable that actually runs 9 rows (every machine in the shipped catalog
    //defines exactly 9 pay rules) is what produced the dead wood expanse below the reels.
    private const int TYPICAL_PAYTABLE_ROWS = 9;
    private const int TYPICAL_PAYTABLE_ROWS_HEIGHT = TYPICAL_PAYTABLE_ROWS * PAYTABLE_ROW_HEIGHT;
    private const int PAYTABLE_TYPICAL_BOTTOM = REELS_TOP + TYPICAL_PAYTABLE_ROWS_HEIGHT;

    //fixed-width right-aligned column for the multiplier/"JACKPOT" text, held to its own column so a long
    //server-authored label can never crowd out the one piece of this rail that must never be ambiguous.
    private const int MULT_COL_WIDTH = 54;
    private const int MULT_COL_GAP = 6;

    //── footer: bet + spin button on one row, result message centered beneath ──
    //sits below the taller of the reel window and a typical paytable render. Math.Max is not a constant
    //expression, hence the ternary -- the point is that this stays const so PANEL_HEIGHT can derive from it.
    private const int FOOTER_GAP = 8;
    private const int FOOTER_TOP = (REEL_WINDOW_BOTTOM > PAYTABLE_TYPICAL_BOTTOM ? REEL_WINDOW_BOTTOM : PAYTABLE_TYPICAL_BOTTOM) + FOOTER_GAP;
    private const int MESSAGE_TOP_GAP = 4;
    private const int MESSAGE_TOP = FOOTER_TOP + CustomButton.HEIGHT + MESSAGE_TOP_GAP;

    //narrowed from 100: "Bet: 1,000" measures well under this, and the surplus only pushed the Spin button
    //rightwards until it met the paytable rail's column.
    private const int BET_LABEL_WIDTH = 84;
    private const int SPIN_BUTTON_WIDTH = 90;

    //── payout audio ──
    //Fired from ShowResultMessage, which runs AFTER RevealDeferredState -- the same reveal gate the gold and
    //jackpot figures sit behind. This is why the outcome sounds are played client-side rather than sent by the
    //server on the SpinResult packet: the server answers roughly two seconds before the reels land, so a
    //server-pushed fanfare would announce a jackpot while the reels were still turning.
    //
    //The shipped catalog pays 1x/3x/7x on its common rows and 21x/70x on its rare ones (identical across all
    //three machines), so the split below falls in the empty 8x-20x gap between those two bands. The comparison
    //is >= rather than an exact set so a future multiplier anywhere in the range still gets a sound: a payout
    //that lands silently would read as a bug, not as tuning.
    private const int SOUND_PAYOUT_COMMON = 171; //the bank's gold-moving sound
    private const int SOUND_PAYOUT_RARE = 183; //the lockpicking chest's heavier clunk
    private const int SOUND_JACKPOT = 168; //the level-up fanfare
    private const int RARE_PAYOUT_MULTIPLIER = 21;

    //one clunk per reel as it lands, so the STAGGER_SECONDS gap between the three stops is audible and not just
    //visual -- the staggered stops are the whole tension mechanism. Fired from the ReelLanded latch in Update.
    //
    //9.mp3 runs ~0.42s, slightly longer than the 0.3s stagger, so SoundSystem's per-id voice stealing fades each
    //clunk as the next one starts. That lands harmlessly here: the fade is 200ms against a clip with only ~120ms
    //left to play, so each click still reaches its natural end with a slight taper rather than being cut short.
    //A noticeably longer clip in this slot would genuinely truncate, which is the thing to re-check if it changes.
    private const int SOUND_REEL_STOP = 9;

    private const float MIN_SPIN_SECONDS = 1.2f;
    private const float STAGGER_SECONDS = 0.3f;

    //mirrors SlotMachineScript.SpinCooldown (2500ms) on the server. The reel animation alone finishes sooner than
    //that -- MIN_SPIN_SECONDS + 2 * STAGGER_SECONDS + ReelControl's 0.45s settle is 2.25s -- and the server starts
    //ITS clock when it takes the bet, which is earlier still than when the client saw the result. So a player
    //clicking the instant the button lit up was landing inside the server's window and eating a spurious
    //"Give the reels a moment." rejection. Gate the controls on this as well as on the reels, measured from the
    //request (the same event the server's own cooldown is anchored to) rather than from the response.
    private const float SERVER_SPIN_COOLDOWN_SECONDS = 2.5f;

    //how long the player's own spin result (win/loss/rejection message) resists being overwritten by an
    //unrelated JackpotAlert broadcast -- long enough to actually read it before someone else's win stomps it.
    private const float RESULT_MESSAGE_HOLD_SECONDS = 3f;

    //the payout celebration (animated payline symbols + pulsing payline outline) runs exactly as long as the
    //message describing it is protected, so the emphasis and the words it emphasises begin and end together.
    private const float CELEBRATION_SECONDS = RESULT_MESSAGE_HOLD_SECONDS;

    //the payline outline is a marker, not an event: dim while idle, dimmer still while the reels turn (nothing
    //can have been won yet), pulsing to full only on a paying spin. It used to sit at full strength permanently,
    //which meant it carried no information at all -- it looked the same on a jackpot as on a dead spin.
    private const float PAYLINE_IDLE_INTENSITY = 0.45f;
    private const float PAYLINE_SPINNING_INTENSITY = 0.22f;
    private const float PAYLINE_PULSE_MIN = 0.55f;
    private const float PAYLINE_PULSE_SECONDS = 0.5f;

    //how long the jackpot readout takes to travel to a newly announced pot. A progressive jackpot that snaps
    //between values reads as a static label that happens to change; counting up is most of what sells it as a
    //pot that is actually growing.
    private const float JACKPOT_COUNT_SECONDS = 0.6f;

    //fill color behind the recessed panels (reel window, jackpot display) -- the same near-black CustomButton
    //and CustomTextBox use for their own dlgframe-bordered fields.
    private static readonly SKColor RecessedFillColor = new(10, 8, 5, 255);

    //warm gold outline for the payline highlight -- keeps the same hue family as the jackpot/gold paytable text
    //so the emphasis reads as "this is the row that pays", not an unrelated decoration.
    private static readonly Color PaylineHighlightColor = new(255, 200, 60, 220);

    //the payout sounds are played straight from here rather than routed back out through WorldScreen: they are
    //driven by the reveal, which only this control knows the timing of.
    private readonly SoundSystem SoundSystem;

    private readonly ReelControl[] Reels = new ReelControl[REEL_COUNT];
    private readonly UILabel TitleLabel;
    private readonly UILabel JackpotLabel;
    private readonly UILabel BetLabel;
    private readonly UILabel MessageLabel;
    private readonly CustomButton SpinButton;

    //the player's own purse, beside the bet. Without it the panel asks for gold every pull while never saying
    //whether there is any left -- the player had to close the window to find out, or spin and eat a rejection.
    private readonly UILabel GoldLabel;

    //the gold outline around the centre row, held as a field (not a plain UIPanel) so its brightness can carry
    //state -- see PAYLINE_IDLE_INTENSITY and ComputePaylineIntensity.
    private readonly PaylineHighlight Payline;

    //pooled paytable rows — the server can send anywhere up to MAX_PAYTABLE_ROWS rows; unused rows stay hidden.
    //Never populated from anything but WorldState.SlotMachine.Paytable, so a content-side tuning edit can never
    //desync what is displayed from what is real.
    //
    //PaytableRows is now the *fallback* label -- shown only for the overflow marker, or when a row's sprites
    //fail to resolve (see RefreshPaytableRows) -- with PaytableIcons the normal, primary rendering. A row is
    //never left with neither visible: a bad sprite id must degrade to readable text, not a silent blank line.
    private readonly UILabel[] PaytableRows = new UILabel[MAX_PAYTABLE_ROWS];

    //the multiplier/"JACKPOT" figure for each pooled row, held in its own fixed-width right-aligned column so it
    //is never a truncation casualty of a long label (see MULT_COL_WIDTH). Unaffected by the label/icon fallback
    //toggle below -- the multiplier must always read the same way regardless of which the rail is showing.
    private readonly UILabel[] PaytableMultLabels = new UILabel[MAX_PAYTABLE_ROWS];

    //the small creature-sprite icons illustrating each row's combination -- see PaytableIconRow's own remarks.
    private readonly PaytableIconRow[] PaytableIcons = new PaytableIconRow[MAX_PAYTABLE_ROWS];

    //true from the moment Spin is clicked until every reel has finished landing (or a Rejected aborts the spin).
    private bool AwaitingResult;
    private float SpinElapsed;

    //latches which reels have already been told to land on their SpinResult stop this spin. Update's stagger
    //check re-evaluates every frame once a reel's threshold has passed; without this latch it called
    //ReelControl.LandOn every one of those frames, which reset ReelControl's settle timer every frame too --
    //the settle animation could never finish, IsSettled never became true, and the Spin button never
    //re-enabled. See the Update method and ReelControl.LandOn's own idempotency guard.
    private readonly bool[] ReelLanded = new bool[REEL_COUNT];

    //counts down from RESULT_MESSAGE_HOLD_SECONDS whenever MessageLabel is set to the player's own outcome (a
    //win/loss result or a rejection reason); RefreshJackpot defers to it so someone else's jackpot broadcast
    //can't erase a message before the player has had a chance to read it.
    private float ResultMessageHoldRemaining;

    //counts down from CELEBRATION_SECONDS while a paying spin is being celebrated. Drives both the payline
    //pulse and the reels' own payline symbol animation.
    private float CelebrationRemaining;

    //counts down from SERVER_SPIN_COOLDOWN_SECONDS from the moment a spin is REQUESTED. The spin controls come
    //back only once this has expired and the reels have settled, whichever is later.
    private float SpinCooldownRemaining;

    //── jackpot count-up ──
    //the figure currently ON SCREEN, eased toward the view model's authoritative pot. Only the display lags;
    //nothing here ever feeds back into WorldState, so the panel can never show a pot the server did not send.
    private float DisplayedJackpot;
    private float JackpotCountFrom;

    //captured when a count-up starts rather than re-read from the view model each frame. A count still running
    //when the next spin begins would otherwise keep easing toward whatever the pot became mid-spin, walking the
    //readout straight past the reveal gate it is supposed to be behind.
    private float JackpotCountTo;
    private float JackpotCountElapsed;
    private long RenderedJackpot = -1; //last value actually written to the label, to avoid a string alloc per frame

    //captured from ApplySpinResult's Stops once available; null until the server answers. The Update loop only
    //starts landing reels once both this is set AND MIN_SPIN_SECONDS has elapsed, whichever comes later.
    private byte[]? PendingStops;

    /// <summary>
    ///     Raised when the player clicks Spin. WorldScreen wires this to <c>ConnectionManager.SendSlotSpin</c>.
    /// </summary>
    public event Action? SpinRequested;

    /// <summary>
    ///     Raised whenever the window closes, by any path (the close button, Escape, or a server-pushed Close
    ///     display). WorldScreen wires this to <c>ConnectionManager.SendSlotClose</c> — mirroring
    ///     <see cref="Bank.BankControl.Closed" />, this fires even when the server is the one that ended the
    ///     session; telling it again is harmless and keeps this control from needing to know why it closed.
    /// </summary>
    public event Action? Closed;

    public SlotMachineControl(CreatureRenderer creatureRenderer, SoundSystem soundSystem)
        : base("_nsett", false)
    {
        ArgumentNullException.ThrowIfNull(creatureRenderer);
        ArgumentNullException.ThrowIfNull(soundSystem);

        SoundSystem = soundSystem;

        Name = "Slots";
        Visible = false;
        UsesControlStack = true; //inherited Show/Hide push/pop the InputDispatcher stack

        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        this.CenterOnScreen();
        Y = TOP_MARGIN;

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        //spans the full panel and centers within it -- the machine's name is the window's heading, so it is
        //centered on the window rather than left-aligned to the reel window's edge.
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

        //── jackpot readout: a recessed display (dark fill + dlgframe border), not a plain label — the headline
        //   number of the whole feature deserves the same visual weight as the reel window below it. ──
        var jackpotBox = new UIPanel
        {
            X = REEL_WINDOW_X,
            Y = JACKPOT_TOP,
            Width = HEADER_WIDTH,
            Height = JACKPOT_BOX_HEIGHT,
            Background = BuildRecessedPanel(HEADER_WIDTH, JACKPOT_BOX_HEIGHT),
            IsHitTestVisible = false
        };
        AddChild(jackpotBox);

        JackpotLabel = new UILabel
        {
            X = 8,
            Y = (JACKPOT_BOX_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = HEADER_WIDTH - 16,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.Gold,
            IsHitTestVisible = false
        };
        jackpotBox.AddChild(JackpotLabel);

        //── recessed reel window: a dark inset panel behind the 3x3 grid, added before the reels so it draws
        //   underneath them. Without this the reels read as nine floating creatures on bare wood. ──
        var reelWindow = new UIPanel
        {
            X = REEL_WINDOW_X,
            Y = REEL_WINDOW_TOP,
            Width = REEL_WINDOW_WIDTH,
            Height = REEL_WINDOW_HEIGHT,
            Background = BuildRecessedPanel(REEL_WINDOW_WIDTH, REEL_WINDOW_HEIGHT),
            IsHitTestVisible = false
        };
        AddChild(reelWindow);

        for (var i = 0; i < Reels.Length; i++)
        {
            Reels[i] = new ReelControl(creatureRenderer)
            {
                X = CONTENT_LEFT + (i * (REEL_CELL_WIDTH + REEL_GAP)),
                Y = REELS_TOP
            };
            AddChild(Reels[i]);
        }

        //── per-cell delineation: thin dividers between reels and between rows, drawn on top of the reels so the
        //   grid reads as three reels of three rows rather than nine floating creatures. Reuses the same
        //   dlgframe.epf edge pieces BankControl's column dividers do. ──
        for (var i = 0; i < (Reels.Length - 1); i++)
            AddChild(BuildReelColumnDivider(Reels[i].X + REEL_CELL_WIDTH));

        for (var row = 1; row < 3; row++)
            AddChild(BuildReelRowDivider(REELS_TOP + (row * REEL_CELL_HEIGHT)));

        //── payline highlight: the centre row is the only row that pays, and nothing marked it before. A gold
        //   outline around the middle row (drawn on top of the reels) makes that unambiguous at a glance, and its
        //   brightness now says WHEN it matters as well as where — see ComputePaylineIntensity. ──
        Payline = new PaylineHighlight
        {
            X = CONTENT_LEFT,
            Y = REELS_TOP + REEL_CELL_HEIGHT,
            Width = REEL_BLOCK_WIDTH,
            Height = REEL_CELL_HEIGHT,
            Background = BuildPaylineHighlight(REEL_BLOCK_WIDTH, REEL_CELL_HEIGHT),
            IsHitTestVisible = false,
            Intensity = PAYLINE_IDLE_INTENSITY
        };
        AddChild(Payline);

        //── paytable rail: pooled rows to the right of the reel block, server-populated only. Split into an
        //   icon column (creature sprites, falling back to a plain-text label -- see RefreshPaytableRows) and a
        //   fixed-width right-aligned multiplier column so a long server-authored label can never crowd out the
        //   payout figure. ──
        const int MULT_COL_X = RAIL_RIGHT - MULT_COL_WIDTH;

        //vertical padding that centers PaytableIconRow.ICON_SIZE within the taller PAYTABLE_ROW_HEIGHT row.
        var iconRowYOffset = (PAYTABLE_ROW_HEIGHT - PaytableIconRow.ICON_SIZE) / 2;

        for (var i = 0; i < PaytableRows.Length; i++)
        {
            var rowY = REELS_TOP + (i * PAYTABLE_ROW_HEIGHT);

            var label = new UILabel
            {
                X = RAIL_X,
                Y = rowY,
                Width = RAIL_LABEL_WIDTH,
                Height = PAYTABLE_ROW_HEIGHT,
                ForegroundColor = LegendColors.White,
                IsHitTestVisible = false,
                Visible = false
            };
            PaytableRows[i] = label;
            AddChild(label);

            var icons = new PaytableIconRow(creatureRenderer)
            {
                X = RAIL_X,
                Y = rowY + iconRowYOffset,
                Visible = false
            };
            PaytableIcons[i] = icons;
            AddChild(icons);

            var mult = new UILabel
            {
                X = MULT_COL_X,
                Y = rowY,
                Width = MULT_COL_WIDTH,
                Height = PAYTABLE_ROW_HEIGHT,
                HorizontalAlignment = HorizontalAlignment.Right,
                ForegroundColor = LegendColors.White,
                IsHitTestVisible = false,
                Visible = false
            };
            PaytableMultLabels[i] = mult;
            AddChild(mult);
        }

        //── footer: bet + spin button on one row, result message centered beneath (see FOOTER_TOP) ──
        BetLabel = new UILabel
        {
            X = CONTENT_LEFT,
            Y = FOOTER_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
            Width = BET_LABEL_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(BetLabel);

        SpinButton = new CustomButton("Spin", SPIN_BUTTON_WIDTH)
        {
            X = CONTENT_LEFT + BET_LABEL_WIDTH,
            Y = FOOTER_TOP
        };
        SpinButton.Clicked += RequestSpin;
        AddChild(SpinButton);

        //right-aligned into the rail's own column, so the footer reads "what a pull costs / spin / what you have"
        //left to right. Turns red once the purse is short of the bet -- the panel's answer to the one question it
        //previously forced the player to close it to ask.
        GoldLabel = new UILabel
        {
            X = RAIL_X,
            Y = FOOTER_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
            Width = RAIL_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(GoldLabel);

        //live for the whole session, not just while open: a spin's cost lands as a gold update, and the label has
        //to follow it mid-spin. Unsubscribed in Dispose, mirroring InventoryPanel.
        WorldState.Inventory.GoldChanged += RefreshGold;

        MessageLabel = new UILabel
        {
            X = CONTENT_LEFT,
            Y = MESSAGE_TOP,
            Width = Width - CONTENT_LEFT - CONTENT_RIGHT,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(MessageLabel);
    }

    /// <summary>
    ///     Builds a recessed dark-fill panel bordered by dlgframe.epf's 8-piece border — the same primitive
    ///     <see cref="CustomButton" />/<see cref="CustomTextBox" /> use for their own recessed frames. Used for both
    ///     the reel window and the jackpot display, so every inset surface on this panel comes from one place
    ///     rather than two lookalikes.
    /// </summary>
    private static Texture2D BuildRecessedPanel(int width, int height)
        => DialogFrame.BuildRecessedTexture(RecessedFillColor, width, height);

    /// <summary>
    ///     Builds a transparent-interior gold border the width/height of the payline row. Border-only (rather than
    ///     a filled tint) so it outlines the centre row without recoloring the creature sprites drawn under it.
    /// </summary>
    private static Texture2D BuildPaylineHighlight(int width, int height)
    {
        const int borderWidth = 2;

        var pixels = new Color[width * height]; //defaults to transparent -- only the border below is written

        ImageUtil.FillRect(pixels, width, height, 0, 0, width, borderWidth, PaylineHighlightColor); //top
        ImageUtil.FillRect(pixels, width, height, 0, height - borderWidth, width, borderWidth, PaylineHighlightColor); //bottom
        ImageUtil.FillRect(pixels, width, height, 0, 0, borderWidth, height, PaylineHighlightColor); //left
        ImageUtil.FillRect(pixels, width, height, width - borderWidth, 0, borderWidth, height, PaylineHighlightColor); //right

        var texture = new Texture2D(TextureConverter.Device, width, height);
        texture.SetData(pixels);

        return texture;
    }

    /// <summary>
    ///     A reel-strip-height vertical divider centered in the <see cref="REEL_GAP" /> that starts at
    ///     <paramref name="gapX" />. Mirrors <see cref="Bank.BankControl" />'s column-divider pattern.
    /// </summary>
    private static CustomSeparator BuildReelColumnDivider(int gapX)
    {
        var divider = new CustomSeparator(SeparatorOrientation.Vertical, REEL_STRIP_HEIGHT)
        {
            Y = REELS_TOP
        };

        divider.X = gapX + ((REEL_GAP - divider.Width) / 2);

        return divider;
    }

    /// <summary>
    ///     A reel-block-width horizontal divider straddling the row boundary at <paramref name="boundaryY" />.
    /// </summary>
    private static CustomSeparator BuildReelRowDivider(int boundaryY)
    {
        var divider = new CustomSeparator(SeparatorOrientation.Horizontal, REEL_BLOCK_WIDTH)
        {
            X = CONTENT_LEFT
        };

        //center the divider's own cropped bevel thickness on the row boundary rather than starting at it.
        divider.Y = boundaryY - (divider.Height / 2);

        return divider;
    }

    /// <summary>
    ///     Repaints every child from <see cref="WorldState.SlotMachine" /> — reel strips, title, jackpot, bet, and
    ///     the paytable rail — and resets any in-flight spin state from a previously displayed machine.
    /// </summary>
    private void RefreshFromViewModel()
    {
        var vm = WorldState.SlotMachine;

        //defensive copy for the sprite lookup table -- Symbols is already owned by the view model, but ToList()
        //here keeps this method's local shape independent of how SetStrip stores it.
        var spriteIds = vm.Symbols
                          .Select(s => s.SpriteId)
                          .ToList();

        for (var i = 0; i < Reels.Length; i++)
            Reels[i].SetStrip(i < vm.Reels.Count ? vm.Reels[i] : [], spriteIds);

        TitleLabel.Text = vm.MachineName;
        BetLabel.Text = $"Bet: {vm.Bet:N0}";

        //cleared BEFORE the refreshes below, not after: both of them defer while a spin is in flight (see the
        //reveal gate on RefreshGold/RefreshJackpot), so a panel reopened while AwaitingResult was still set from
        //an abandoned spin would otherwise skip its own repaint and open showing stale figures.
        AwaitingResult = false;
        SpinElapsed = 0f;
        PendingStops = null;
        Array.Clear(ReelLanded);

        //a fresh sit-down opens with usable controls. If the server does still hold a cooldown against this
        //player (stepping off the stool and back on no longer clears one -- see SlotMachineScript.TryClaim), the
        //first pull earns an honest "Give the reels a moment." That is a better opening state than a dead Spin
        //button with no explanation attached to it.
        SpinCooldownRemaining = 0f;

        //a fresh Open/Show has no result to protect yet, and nothing to celebrate
        SetMessage("Spin to play!", LegendColors.White, false);
        EndCelebration();

        RefreshGold();
        SnapJackpot(); //opening on a machine should show its pot, not count up to it from the last machine's
        RefreshPaytableRows();

        SpinButton.Enabled = true;
    }

    /// <summary>
    ///     Publishes every figure whose update was withheld during the spin — see the reveal gate on
    ///     <see cref="RefreshGold" /> and <see cref="RefreshJackpot" />. Must be called with
    ///     <see cref="AwaitingResult" /> already false, since that flag is what those methods gate on.
    /// </summary>
    /// <remarks>
    ///     The server answers a spin long before the reels finish turning — the payout gold and the new pot both
    ///     land while the client is still animating. Anything that repaints from them the moment they arrive tells
    ///     the player the outcome roughly two seconds before the reels do, which is the entire point of the
    ///     animation. So the panel deliberately shows the state the player had BEFORE the spin until the reels
    ///     reveal it, and publishes everything here, in one step, at the reveal.
    ///     <para>
    ///         The message line already worked this way (<see cref="RefreshJackpot" /> has always refused to write
    ///         it while <see cref="AwaitingResult" /> is set); the gold and jackpot figures are the same rule
    ///         applied to the two numbers that were still leaking.
    ///     </para>
    /// </remarks>
    private void RevealDeferredState()
    {
        RefreshGold();
        ApplyJackpotToDisplay();
    }

    /// <summary>
    ///     The single place the message line is written. Colour is part of the message, not decoration: a win, a
    ///     dead spin and a rejection previously rendered in identical white, so the line's most glanceable
    ///     property carried none of its meaning.
    /// </summary>
    /// <param name="hold">
    ///     Whether this is the player's OWN outcome, and so must resist being overwritten by an unrelated jackpot
    ///     broadcast for <see cref="RESULT_MESSAGE_HOLD_SECONDS" />.
    /// </param>
    private void SetMessage(string text, Color color, bool hold)
    {
        MessageLabel.Text = text;
        MessageLabel.ForegroundColor = color;
        ResultMessageHoldRemaining = hold ? RESULT_MESSAGE_HOLD_SECONDS : 0f;
    }

    /// <summary>
    ///     Repaints the player's gold from <see cref="WorldState.Inventory" />. Wired to its GoldChanged event, so
    ///     this follows a spin's cost and payout live rather than only on open.
    /// </summary>
    private void RefreshGold()
    {
        //reveal gate: the payout is credited while the reels are still turning, so repainting on arrival would
        //announce the win before the reels do. Held until RevealDeferredState publishes it.
        if (AwaitingResult)
            return;

        var gold = WorldState.Inventory.Gold;
        GoldLabel.Text = $"Gold: {gold:N0}";

        //red the moment another pull is unaffordable -- the same condition the server answers with an
        //InsufficientGold rejection, said before the player spends a spin finding out.
        GoldLabel.ForegroundColor = gold < (uint)Math.Max(0, WorldState.SlotMachine.Bet) ? LegendColors.Red : LegendColors.White;
    }

    /// <summary>
    ///     Puts the jackpot readout at the view model's pot immediately, with no count-up. Used when the panel
    ///     opens (or is retargeted): counting up from the previously displayed machine's pot would animate a
    ///     relationship between two unrelated numbers.
    /// </summary>
    private void SnapJackpot()
    {
        DisplayedJackpot = WorldState.SlotMachine.JackpotAmount;
        JackpotCountFrom = DisplayedJackpot;
        JackpotCountTo = DisplayedJackpot;
        JackpotCountElapsed = JACKPOT_COUNT_SECONDS; //already "finished", so TickJackpotCount leaves it alone
        WriteJackpotLabel();
    }

    private void WriteJackpotLabel()
    {
        var value = (long)MathF.Round(DisplayedJackpot);

        if (value == RenderedJackpot)
            return;

        RenderedJackpot = value;
        JackpotLabel.Text = $"Jackpot: {value:N0}";
    }

    /// <summary>
    ///     Begins the payout celebration: the payline outline pulses and each reel animates its payline symbol.
    ///     Emphasis only — the message line and paytable already state what was won, so nothing here is the sole
    ///     carrier of any information.
    /// </summary>
    private void BeginCelebration()
    {
        CelebrationRemaining = CELEBRATION_SECONDS;

        foreach (var reel in Reels)
            reel.StartCelebration();
    }

    private void EndCelebration()
    {
        CelebrationRemaining = 0f;
        Payline.Intensity = PAYLINE_IDLE_INTENSITY;

        foreach (var reel in Reels)
            reel.StopCelebration();
    }

    /// <summary>
    ///     How bright the payline outline should be this frame: pulsing while celebrating a payout, dimmest while
    ///     the reels are still turning (nothing can have been won yet), dim otherwise.
    /// </summary>
    private float ComputePaylineIntensity()
    {
        if (CelebrationRemaining <= 0f)
            return AwaitingResult ? PAYLINE_SPINNING_INTENSITY : PAYLINE_IDLE_INTENSITY;

        //triangle wave, starting at full brightness the instant the win lands and dipping from there -- a pulse
        //that started dim would put the celebration's quietest moment where its loudest one belongs.
        var phase = (CELEBRATION_SECONDS - CelebrationRemaining) / PAYLINE_PULSE_SECONDS;
        var wave = Math.Abs((phase % 2f) - 1f);

        return PAYLINE_PULSE_MIN + ((1f - PAYLINE_PULSE_MIN) * wave);
    }

    private void RefreshPaytableRows()
    {
        var vm = WorldState.SlotMachine;
        var paytable = vm.Paytable;

        //defensive copy for the sprite lookup table, same reasoning as RefreshFromViewModel's own spriteIds --
        //PaytableIconRow.SetSymbols indexes into this by the row's SymbolIndices.
        var spriteIds = vm.Symbols
                          .Select(s => s.SpriteId)
                          .ToList();

        //an overflow reserves the last pooled row for a visible "+N more" marker rather than silently dropping
        //rows past the pool size -- a paytable row that vanishes without a trace is exactly the failure this
        //rail exists to prevent (it must never misrepresent the machine's odds).
        var overflowCount = paytable.Count - MAX_PAYTABLE_ROWS;
        var realRowCount = overflowCount > 0 ? MAX_PAYTABLE_ROWS - 1 : paytable.Count;

        if (overflowCount > 0)
            System.Diagnostics.Debug.WriteLine(
                $"SlotMachineControl: paytable for '{WorldState.SlotMachine.MachineName}' has {paytable.Count} rows, "
                + $"exceeding the {MAX_PAYTABLE_ROWS}-row rail capacity -- {overflowCount} row(s) collapsed into an "
                + "overflow marker. Raise MAX_PAYTABLE_ROWS if this machine's content is intentional.");

        for (var i = 0; i < realRowCount; i++)
        {
            var row = paytable[i];
            var color = row.IsJackpot ? LegendColors.Gold : LegendColors.White;

            //label and multiplier are two separate labels now (see MULT_COL_WIDTH) so a long server-authored
            //label truncates on its own, never crowding out the payout figure.
            PaytableRows[i].Text = row.Label;
            PaytableRows[i].ForegroundColor = color;

            PaytableMultLabels[i].Text = row.IsJackpot ? "JACKPOT" : $"{row.Multiplier}x";
            PaytableMultLabels[i].ForegroundColor = color;
            PaytableMultLabels[i].Visible = true;

            //resolve this row's icons eagerly so AllResolved is accurate before deciding what to show. A row
            //with no SymbolIndices at all (server sent none) is treated the same as an unresolved row -- fall
            //back to the label rather than showing an empty icon strip.
            PaytableIcons[i].SetSymbols(row.SymbolIndices, spriteIds);

            var showIcons = (row.SymbolIndices.Count > 0) && PaytableIcons[i].AllResolved;
            PaytableIcons[i].Visible = showIcons;

            //the label is the fallback: shown whenever the icons are not, so a bad/missing sprite id degrades
            //to readable text instead of a silent blank row.
            PaytableRows[i].Visible = !showIcons;
        }

        if (overflowCount > 0)
        {
            PaytableRows[realRowCount].Text = $"+{overflowCount + 1} more";
            PaytableRows[realRowCount].ForegroundColor = LegendColors.Gray;
            PaytableRows[realRowCount].Visible = true;
            PaytableMultLabels[realRowCount].Visible = false; //the overflow marker has no multiplier of its own
            PaytableIcons[realRowCount].Visible = false; //the overflow marker is text-only, never illustrated
        }

        var usedRows = overflowCount > 0 ? realRowCount + 1 : realRowCount;

        for (var i = usedRows; i < PaytableRows.Length; i++)
        {
            PaytableRows[i].Visible = false;
            PaytableMultLabels[i].Visible = false;
            PaytableIcons[i].Visible = false;
        }
    }

    /// <summary>
    ///     Call after <c>WorldState.SlotMachine.ApplyJackpot</c> (a JackpotAlert display) so the jackpot figure
    ///     updates live without reopening the panel. Safe to call while hidden.
    /// </summary>
    public void RefreshJackpot()
    {
        var vm = WorldState.SlotMachine;

        ApplyJackpotToDisplay();

        //don't stomp an in-progress spin, or a just-shown result/rejection message the player hasn't had time
        //to read yet, with someone else's jackpot announcement. Coloured apart from the player's own gold
        //jackpot line so a broadcast is never mistaken for their own win.
        if (!AwaitingResult && (ResultMessageHoldRemaining <= 0f) && !string.IsNullOrEmpty(vm.LastJackpotWinner))
            SetMessage($"{vm.LastJackpotWinner} just won the jackpot!", LegendColors.DustyOrange, false);
    }

    /// <summary>
    ///     Moves the on-screen pot toward the view model's, unless a spin is still turning.
    /// </summary>
    /// <remarks>
    ///     The reveal gate matters most here on the player's own jackpot: winning it resets the pot to the seed,
    ///     so an ungated readout would collapse from millions to the seed value while the reels were still
    ///     spinning — the loudest possible spoiler. It matters on ordinary spins too, just quietly: the pot ticks
    ///     up by the machine's contribution the instant the server answers, which is a reliable tell that the
    ///     outcome is already decided.
    /// </remarks>
    private void ApplyJackpotToDisplay()
    {
        if (AwaitingResult)
            return;

        var target = WorldState.SlotMachine.JackpotAmount;

        //count UP to a growing pot; snap DOWN to a reset one. A pot that was just won should visibly reset, not
        //drain away over half a second as though it were still paying out.
        if (target < DisplayedJackpot)
            SnapJackpot();
        else if (Math.Abs(target - DisplayedJackpot) > 0.5f)
        {
            JackpotCountFrom = DisplayedJackpot;
            JackpotCountTo = target;
            JackpotCountElapsed = 0f;
        }
    }

    /// <summary>
    ///     Call after <c>WorldState.SlotMachine.ApplySpinResult</c> (a SpinResult display) arrives. Captures the
    ///     server's chosen stops; the Update loop lands the reels on them once the minimum spin duration has
    ///     elapsed, staggered left-to-right.
    /// </summary>
    public void OnSpinResult()
    {
        if (!Visible)
            return;

        //defensive copy -- WorldState.SlotMachine.Stops is owned by the view model. It never mutates an already
        //-assigned array in place today, but aliasing it directly here would silently break the moment it did.
        var stops = WorldState.SlotMachine.Stops;
        PendingStops = stops.Length == 0 ? [] : (byte[])stops.Clone();

        RefreshJackpot();
    }

    /// <summary>
    ///     Call on a Rejected display. Aborts any in-flight spin animation to a neutral stop and shows a
    ///     reason-specific message.
    /// </summary>
    public void OnRejected(SlotRejectReason reason)
    {
        AwaitingResult = false;
        PendingStops = null;
        Array.Clear(ReelLanded);

        //no reject reason takes the bet, so the server started no cooldown for THIS request -- drop the client
        //gate rather than making the player serve a wait the server is not enforcing. The exception is a Cooldown
        //rejection, which means a cooldown from an EARLIER spin genuinely is still running; keeping that one is
        //what stops an immediate retry from being rejected all over again.
        if (reason is not SlotRejectReason.Cooldown)
            SpinCooldownRemaining = 0f;

        SpinButton.Enabled = SpinCooldownRemaining <= 0f;

        foreach (var reel in Reels)
            reel.LandOn(0);

        //a rejection ends any celebration still running from the previous spin, and releases the reveal gate --
        //there is no result coming, so nothing is left to be spoiled by repainting now.
        EndCelebration();
        RevealDeferredState();

        var text = reason switch
        {
            SlotRejectReason.NotOccupant      => "Sit at the machine to play.",
            SlotRejectReason.Cooldown         => "Give the reels a moment.",
            SlotRejectReason.InsufficientGold => $"You need {WorldState.SlotMachine.Bet:N0} gold to play here.",
            SlotRejectReason.MachineBusy      => "Someone else is using that machine.",
            SlotRejectReason.Misconfigured    => "This machine is out of order.",
            _                                 => "That did not work."
        };

        //red, and held: this is the player's own outcome, protected from an unrelated jackpot broadcast the same
        //way ShowResultMessage's win/loss text is.
        SetMessage(text, LegendColors.Red, true);
    }

    private void RequestSpin()
    {
        if (AwaitingResult || !Visible)
            return;

        AwaitingResult = true;
        SpinElapsed = 0f;
        SpinCooldownRemaining = SERVER_SPIN_COOLDOWN_SECONDS;
        PendingStops = null;
        Array.Clear(ReelLanded);
        SetMessage(string.Empty, LegendColors.White, false); //a new spin supersedes whatever text was being held
        EndCelebration(); //and whatever the last one was still celebrating

        SpinButton.Enabled = false;

        foreach (var reel in Reels)
            reel.StartSpin();

        SpinRequested?.Invoke();
    }

    private void ShowResultMessage()
    {
        var vm = WorldState.SlotMachine;

        //gold for the jackpot, warm yellow for an ordinary win, gray for a dead spin. All three used to render
        //in the same white as the idle "Spin to play!" prompt, so the line looked identical whether the player
        //had just won the pot or nothing at all.
        //
        //The sound is chosen by the same switch that picks the words, so the two can never disagree about what
        //just happened. A dead spin stays silent: silence is what makes the other three mean anything.
        var (text, color, sound) = vm switch
        {
            { LastWasJackpot: true } => ($"JACKPOT! {vm.LastPayout:N0} gold!", LegendColors.Gold, SOUND_JACKPOT),
            { LastMultiplier: >= RARE_PAYOUT_MULTIPLIER, LastPayout: > 0 } => (
                $"{vm.LastLabel} — {vm.LastMultiplier}x — {vm.LastPayout:N0} gold", LegendColors.PastelYellow, SOUND_PAYOUT_RARE),
            { LastPayout: > 0 } => (
                $"{vm.LastLabel} — {vm.LastMultiplier}x — {vm.LastPayout:N0} gold", LegendColors.PastelYellow, SOUND_PAYOUT_COMMON),
            _ => ("No win. Spin again.", LegendColors.Gray, 0)
        };

        //held: protects the player's own outcome from an unrelated jackpot broadcast landing at the wrong moment.
        SetMessage(text, color, true);

        if (sound > 0)
            SoundSystem.PlaySound(sound);

        if (vm.LastPayout > 0)
            BeginCelebration();
    }

    /// <summary>
    ///     Drives the reels every frame. This is a distinct method, not an override of the inherited
    ///     GameTime-based <c>Update</c> -- like <see cref="ReelControl.Update(float)" />, it takes elapsed seconds
    ///     directly and must be called explicitly (from <c>WorldScreen.Update</c>). The panel's automatic
    ///     child-update dispatch only reaches the parameterless GameTime overload, which neither this method nor
    ///     the reels override, so nothing would tick them without this explicit call.
    /// </summary>
    public void Update(float deltaSeconds)
    {
        if (!Visible)
            return;

        if (ResultMessageHoldRemaining > 0f)
            ResultMessageHoldRemaining = Math.Max(0f, ResultMessageHoldRemaining - deltaSeconds);

        if (CelebrationRemaining > 0f)
        {
            CelebrationRemaining -= deltaSeconds;

            if (CelebrationRemaining <= 0f)
                EndCelebration();
        }

        if (SpinCooldownRemaining > 0f)
        {
            SpinCooldownRemaining = Math.Max(0f, SpinCooldownRemaining - deltaSeconds);

            //the cooldown outlasts the reel animation, so in the ordinary case this is where the button comes
            //back -- the settle block below finds it still running and leaves it disabled.
            if ((SpinCooldownRemaining <= 0f) && !AwaitingResult)
                SpinButton.Enabled = true;
        }

        Payline.Intensity = ComputePaylineIntensity();
        TickJackpotCount(deltaSeconds);

        if (AwaitingResult)
        {
            SpinElapsed += deltaSeconds;

            if ((PendingStops is not null) && (SpinElapsed >= MIN_SPIN_SECONDS))
            {
                for (var i = 0; i < Reels.Length; i++)
                    if (!ReelLanded[i] && (SpinElapsed >= (MIN_SPIN_SECONDS + (i * STAGGER_SECONDS))))
                    {
                        //call LandOn exactly once per reel per spin -- ReelLanded latches the transition. This
                        //used to fire unconditionally every frame once the stagger threshold passed, which reset
                        //ReelControl's SettleElapsed to 0 every frame; the settle ease could never reach 1 and
                        //IsSettled never became true, so the "all reels settled" check below never fired and the
                        //Spin button never re-enabled. See ReelControl.LandOn for the matching defense-in-depth.
                        //
                        //A reel the server sent no stop for is still LANDED, on 0, rather than skipped. The
                        //server always sends one stop per reel today (SlotConfigValidator forces exactly three
                        //reels), but this loop used to skip surplus reels entirely and leave them spinning
                        //forever -- IsSettled could then never be true for all three, so the reveal below never
                        //ran, AwaitingResult never cleared, and the panel sat with its Spin button permanently
                        //dead until the player stepped off the stool and back on. A short Stops
                        //array should degrade to a wrong-looking reel, never to a panel that cannot be used.
                        Reels[i].LandOn(i < PendingStops.Length ? PendingStops[i] : 0);
                        ReelLanded[i] = true;

                        //one clunk per reel, riding the same latch the landing does -- the latch is what keeps
                        //this from re-firing every frame, exactly as it does LandOn itself. Deliberately NOT
                        //played from OnRejected's LandOn loop: that path stops the reels because no spin is
                        //happening, and a machine that clunks three times on a refusal would be describing a
                        //spin the player did not get.
                        SoundSystem.PlaySound(SOUND_REEL_STOP);
                    }

                if (Reels.All(r => r.IsSettled))
                {
                    AwaitingResult = false;
                    PendingStops = null;

                    //the server's own cooldown is usually still running at this point -- the block at the top of
                    //Update re-enables the button when it expires.
                    SpinButton.Enabled = SpinCooldownRemaining <= 0f;

                    //the reveal: publish the figures withheld during the spin BEFORE announcing the result, so
                    //the gold and pot the player looks at at the moment they read "JACKPOT!" already agree with it.
                    RevealDeferredState();
                    ShowResultMessage();
                }
            }
        }

        foreach (var reel in Reels)
            reel.Update(deltaSeconds);
    }

    /// <summary>
    ///     Eases the on-screen jackpot figure toward the view model's authoritative pot. Always lands exactly on
    ///     it — the count-up is presentation, and must never leave the readout a few gold off what the server said.
    /// </summary>
    private void TickJackpotCount(float deltaSeconds)
    {
        if (JackpotCountElapsed >= JACKPOT_COUNT_SECONDS)
            return;

        JackpotCountElapsed += deltaSeconds;

        var t = Math.Clamp(JackpotCountElapsed / JACKPOT_COUNT_SECONDS, 0f, 1f);
        var eased = 1f - ((1f - t) * (1f - t) * (1f - t)); //cubic ease-out, the curve the reels settle on

        DisplayedJackpot = t >= 1f ? JackpotCountTo : JackpotCountFrom + ((JackpotCountTo - JackpotCountFrom) * eased);
        WriteJackpotLabel();
    }

    /// <summary>
    ///     Repaints from <see cref="WorldState.SlotMachine" /> (already populated by <c>ApplyOpen</c>) and shows the
    ///     panel. Resets local state even if the window is retargeted at a different machine while already open.
    /// </summary>
    public override void Show()
    {
        AwaitingResult = false;
        SpinElapsed = 0f;
        PendingStops = null;
        Array.Clear(ReelLanded);

        base.Show();
        RefreshFromViewModel();
    }

    /// <summary>
    ///     Hides the panel and clears <see cref="WorldState.SlotMachine" /> so a stale name/bet/paytable/reels can
    ///     never survive onto whatever the player looks at next -- the exact problem
    ///     <see cref="Bank.BankControl.Hide" /> solves for the bank window. Fires <see cref="Closed" /> on any
    ///     path that actually hid a visible panel, whether the player clicked Close/Escape or the server pushed a
    ///     Close display.
    /// </summary>
    public override void Hide()
    {
        var wasVisible = Visible;

        AwaitingResult = false;
        PendingStops = null;
        Array.Clear(ReelLanded);
        ResultMessageHoldRemaining = 0f;
        SpinCooldownRemaining = 0f;
        EndCelebration();

        base.Hide();
        WorldState.SlotMachine.Clear();

        if (wasVisible)
            Closed?.Invoke();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Hide();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Dispose()
    {
        WorldState.Inventory.GoldChanged -= RefreshGold;

        base.Dispose();
    }

    /// <summary>
    ///     The payline outline, drawn at a variable brightness. A plain <see cref="UIPanel" /> always draws its
    ///     Background at full <see cref="Color.White" />, so carrying state in the outline's intensity — dim while
    ///     idle, dimmer while spinning, pulsing on a payout — needs this one override and nothing else.
    /// </summary>
    private sealed class PaylineHighlight : UIPanel
    {
        /// <summary>Multiplier applied to the outline's color, 0 (invisible) through 1 (full strength).</summary>
        public float Intensity { get; set; } = 1f;

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible)
                return;

            UpdateClipRect();

            if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
                return;

            //no children and no BackgroundColor/BorderColor are ever set on this panel, so the base's remaining
            //work would be dead weight -- this is the whole of its rendering.
            DrawTexture(
                spriteBatch,
                Background,
                new Vector2(ScreenX, ScreenY),
                Color.White * Math.Clamp(Intensity, 0f, 1f));
        }
    }
}
