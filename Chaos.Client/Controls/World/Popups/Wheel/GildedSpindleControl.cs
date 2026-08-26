#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Extensions;
using Chaos.Client.Systems;
using Chaos.Client.Utilities;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups.Wheel;

/// <summary>
///     The Gilded Spindle window: a recessed wheel window with a fixed pointer marker, a progressive-jackpot pot
///     display (shown only for the jackpot-eligible tier), a three-tier stake selector, the Spin button, and a
///     result message line. There is deliberately no payout rail: every wheel segment already reads its own
///     multiplier/BONUS/JACKPOT label on the wheel face itself (see <see cref="WheelControl" />), so a legend
///     restating "1x" as "1x" would be pure redundancy -- unlike the slot machines, whose rail is load-bearing
///     because creature sprites don't self-describe. Reuses the ornate dialog frame (see
///     <see cref="FramedDialogPanelBase" />), the same way <see cref="Slots.SlotMachineControl" /> does. Mounted on
///     WorldScreen.Root, opened by the server's wheel Open display when the player sits at a Spindle.
/// </summary>
/// <remarks>
///     <para>
///         Pure renderer, never a decider: the server picks every stop, the client only animates toward it. This
///         control is driven by explicit calls from <c>WorldScreen.ServerHandlers</c>
///         (<see cref="Show" />/<see cref="OnSpinResult" />/<see cref="RefreshJackpot" />/<see cref="OnRejected" />
///         /<see cref="Hide" />) made right after the corresponding <see cref="WorldState.GildedSpindle" />
///         mutation -- mirroring <see cref="Slots.SlotMachineControl" />'s own reasoning for why an event with a
///         single producer/subscriber pair would only add indirection.
///     </para>
///     <para>
///         <b>The two-stage reveal gate.</b> A main wheel that lands on BONUS already tells the player something
///         good happened -- so unlike the slots, revealing the gold/pot figures the instant the MAIN wheel settles
///         would contradict a bonus round still visibly turning. <see cref="AwaitingResult" /> therefore stays set
///         (and <see cref="RefreshGold" />/<see cref="ApplyPotToDisplay" /> stay withheld) across the ENTIRE
///         sequence -- main spin, the bonus beat, and the bonus spin -- not just the main wheel's own animation.
///         <see cref="RevealDeferredState" /> publishes both figures in one step, immediately before
///         <see cref="ShowResultMessage" /> writes the words, in <see cref="FinishSpin" />, so the numbers already
///         agree with "JACKPOT!" the moment the player reads it.
///     </para>
///     <para>
///         <b>SPIN gating.</b> Mirrors <see cref="Slots.SlotMachineControl" /> exactly: the button re-enables only
///         once <see cref="AwaitingResult" /> has cleared AND <see cref="SpinCooldownRemaining" /> has expired,
///         whichever is later -- see <see cref="Update" /> and <see cref="FinishSpin" />. A bonus round roughly
///         doubles the animation (the bonus wheel gets its own <see cref="MIN_SPIN_SECONDS" /> spin-up before it
///         lands, not an instant ease from a standstill), while <see cref="SERVER_SPIN_COOLDOWN_SECONDS" /> mirrors
///         the server's flat 3000ms rate limit (<c>GildedSpindleScript.SpinCooldown</c>) regardless of which
///         happened. Gating on the later of the two is what stops the button re-enabling mid-bonus-round.
///     </para>
///     <para>
///         Audio is entirely client-side, played from the reveal/landing path -- never driven by the packet. The
///         server answers a spin roughly two seconds before the wheel visibly lands, so a server-triggered sound
///         would announce a jackpot while the wheel was still turning.
///     </para>
/// </remarks>
public sealed class GildedSpindleControl : FramedDialogPanelBase
{
    //── canonical panel size, derived from its content (see SlotMachineControl's own remarks for why). With the
    //   payout rail gone, the widest row is now the footer's stake selector rather than a rail extending past it.
    private const int PANEL_WIDTH =
        CONTENT_LEFT
        + (WHEEL_WINDOW_SIZE > StakeSelectorControl.WIDTH ? WHEEL_WINDOW_SIZE : StakeSelectorControl.WIDTH)
        + CONTENT_RIGHT;

    //expands out to 208 + WHEEL_DIAMETER (every other footer row below is a fixed offset), which is what
    //WHEEL_DIAMETER's own derivation is bounded against -- see that const's remarks.
    private const int PANEL_HEIGHT = MESSAGE_TOP + TextRenderer.CHAR_HEIGHT + 1 + FRAME_BOTTOM_BORDER;

    //FramedDialogPanelBase paints a 47px-tall ornate bottom border over the panel's last 47 rows.
    private const int FRAME_BOTTOM_BORDER = 47;

    private const int TOP_MARGIN = 15;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private const int CONTENT_LEFT = 20;
    private const int CONTENT_RIGHT = 20;

    //── header ──
    private const int TITLE_TOP = 8;
    private const int POT_TOP = TITLE_TOP + TextRenderer.CHAR_HEIGHT + 4;
    private const int POT_BOX_HEIGHT = CustomButton.HEIGHT;

    //── wheel window ──
    //WheelControl now takes its diameter as a constructor parameter (see its own remarks), so this is a real knob
    //this panel can grow rather than a mirrored copy of an unreachable private const. 180 is derived from the
    //PANEL_HEIGHT budget below: with every other footer row fixed, PANEL_HEIGHT = 208 + WHEEL_DIAMETER (see that
    //const's own derivation), and the requirement is PANEL_HEIGHT <= 400, i.e. WHEEL_DIAMETER <= 192. 180 keeps a
    //12px margin under that ceiling (PANEL_HEIGHT lands at 388) rather than maximising right up to it.
    private const int WHEEL_DIAMETER = 180;
    private const int WHEEL_WINDOW_PADDING = 6;
    private const int WHEEL_WINDOW_SIZE = WHEEL_DIAMETER + (WHEEL_WINDOW_PADDING * 2);

    //centred within the full content span rather than pinned to CONTENT_LEFT: PANEL_WIDTH's own derivation takes
    //the wider of the wheel window and the stake selector, so whenever the selector is the wider of the two (it
    //currently is: 244 vs 192), a left-pinned wheel would leave the extra width as dead space entirely to its
    //right. Centring here is what keeps that space split evenly on both sides instead. When the wheel window is
    //the wider element this reduces to CONTENT_LEFT exactly, i.e. today's left-pinned position, so nothing moves
    //for that case.
    private const int WHEEL_WINDOW_X =
        CONTENT_LEFT + (((PANEL_WIDTH - CONTENT_LEFT - CONTENT_RIGHT) - WHEEL_WINDOW_SIZE) / 2);

    private const int WHEEL_WINDOW_TOP = POT_TOP + POT_BOX_HEIGHT + 10;
    private const int WHEEL_X = WHEEL_WINDOW_X + WHEEL_WINDOW_PADDING;
    private const int WHEEL_TOP = WHEEL_WINDOW_TOP + WHEEL_WINDOW_PADDING;
    private const int WHEEL_WINDOW_BOTTOM = WHEEL_WINDOW_TOP + WHEEL_WINDOW_SIZE;

    //a small downward-pointing marker above the wheel window: WheelControl's own "pointer" is a fixed conceptual
    //point at the top of the wheel with nothing drawn to mark it, so without this a player has no reliable way to
    //tell where a landed segment is actually read from.
    private const int POINTER_WIDTH = 14;
    private const int POINTER_HEIGHT = 10;
    private const int POINTER_X = WHEEL_WINDOW_X + ((WHEEL_WINDOW_SIZE - POINTER_WIDTH) / 2);
    private const int POINTER_TOP = WHEEL_WINDOW_TOP - POINTER_HEIGHT - 2;

    //── footer: stake selector, then a Spin + Gold row, then the result message ──
    private const int FOOTER_GAP = 10;

    //no payout rail to compare against any more -- the wheel window is the only thing above the footer.
    private const int FOOTER_TOP = WHEEL_WINDOW_BOTTOM + FOOTER_GAP;

    private const int SPIN_ROW_GAP = 8;
    private const int SPIN_ROW_TOP = FOOTER_TOP + StakeSelectorControl.HEIGHT + SPIN_ROW_GAP;
    private const int SPIN_BUTTON_WIDTH = 90;
    private const int SPIN_GOLD_GAP = 12;

    private const int MESSAGE_TOP_GAP = 4;
    private const int MESSAGE_TOP = SPIN_ROW_TOP + CustomButton.HEIGHT + MESSAGE_TOP_GAP;

    //── timing ──
    //the main wheel's own spin-up before LandOn. 1.5 + WheelControl.SETTLE_SECONDS(0.6) = 2.1s, matching
    //GildedSpindleScript's "~2.1s plain" cooldown comment.
    private const float MIN_SPIN_SECONDS = 1.5f;

    //the pause between the main wheel settling on BONUS and the bonus wheel appearing.
    private const float BONUS_BEAT_SECONDS = 0.5f;

    //mirrors GildedSpindleScript.SpinCooldown (a flat 3000ms rate limit, longer than the slots' 2500ms because a
    //bonus round roughly doubles the animation: MIN_SPIN_SECONDS(1.5) + settle(0.6) + BONUS_BEAT_SECONDS(0.5) +
    //MIN_SPIN_SECONDS(1.5) + settle(0.6) = 4.7s against a plain spin's 2.1s).
    private const float SERVER_SPIN_COOLDOWN_SECONDS = 3.0f;

    //how long the player's own result/rejection message resists being overwritten by an unrelated JackpotAlert.
    private const float RESULT_MESSAGE_HOLD_SECONDS = 3f;

    //how long the on-screen pot takes to count to a newly announced value.
    private const float POT_COUNT_SECONDS = 0.6f;

    //── audio ──
    //shared by every segment-boundary tick AND the moment either wheel commits to landing (played at the LandOn
    //call, the same instant SlotMachineControl plays its own reel-stop clunk -- not at IsSettled, which is 0.6s
    //later). A single id for both is deliberate: the table only lists one "wheel landing" sound distinct from
    //nothing else, so a tick and a landing are meant to sound like the same kind of event.
    private const int SOUND_TICK_OR_LAND = 9;

    private const int SOUND_PAYOUT_LOW = 171; //1x-5x -- the bank's gold-moving sound
    private const int SOUND_PAYOUT_HIGH = 183; //6x-20x -- the lockpicking chest's heavier clunk
    private const int SOUND_BONUS_OPEN = 183; //the bonus round opening, reusing the same heavier sound
    private const int SOUND_JACKPOT = 168; //the level-up fanfare

    //comparison is >= rather than an exact set so a future multiplier anywhere in the high band still gets a
    //sound -- a payout that lands silently would read as a bug, not as tuning.
    private const int HIGH_BAND_MULTIPLIER = 6;

    //fill color behind the recessed panels -- the same near-black CustomButton/CustomTextBox use for their own
    //recessed frames.
    private static readonly SKColor RecessedFillColor = new(10, 8, 5, 255);

    //gold, matching WheelControl's own JackpotBand -- the pointer marker and the pot figure share the hue that
    //already means "jackpot" everywhere else in this feature.
    private static readonly SKColor PointerColor = new(214, 170, 44, 255);

    private readonly SoundSystem SoundSystem;

    private readonly WheelControl MainWheel;
    private readonly WheelControl BonusWheel;
    private readonly StakeSelectorControl StakeSelector;

    private readonly UILabel TitleLabel;
    private readonly UIPanel PotBox;
    private readonly UILabel PotLabel;
    private readonly UILabel GoldLabel;
    private readonly CustomButton SpinButton;
    private readonly UILabel MessageLabel;

    //── reveal-gate / spin state ──

    //True from the moment Spin is clicked until the reveal gate releases it -- see the class remarks. Unlike
    //SlotMachineControl's identical field, this spans a possible bonus round too, not just one settle.
    private bool AwaitingResult;

    private float MainSpinElapsed;
    private bool MainLanded; //latch: has MainWheel.LandOn been called this spin
    private byte? PendingMainStop; //captured from OnSpinResult; the Update loop lands the main wheel on this
    private byte? PendingBonusStop; //captured from OnSpinResult; null means the bonus round never opened

    private bool BonusStarted; //latch: has the bonus wheel been swapped in and told to spin
    private float BonusBeatElapsed;
    private float BonusSpinElapsed;
    private bool BonusLanded; //latch: has BonusWheel.LandOn been called this spin

    //the stake this spin actually ran at, captured at RequestSpin -- used for the InsufficientGold message, which
    //must name the wager that was rejected even if the player has since (however unlikely, given the stake
    //buttons are disabled for the duration) looked at a different tier.
    private int RequestedStake;

    private float ResultMessageHoldRemaining;
    private float SpinCooldownRemaining;

    //── pot count-up (mirrors SlotMachineControl's jackpot readout) ──
    private float DisplayedPot;
    private float PotCountFrom;
    private float PotCountTo;
    private float PotCountElapsed;
    private long RenderedPot = -1;

    /// <summary>
    ///     Raised when the player clicks Spin, carrying the stake index to bet at. WorldScreen wires this to
    ///     <c>ConnectionManager.SendWheelSpin(byte)</c>.
    /// </summary>
    public event Action<byte>? SpinRequested;

    /// <summary>
    ///     Raised whenever the window closes, by any path (the close button, Escape, or a server-pushed Close
    ///     display). WorldScreen wires this to <c>ConnectionManager.SendWheelClose</c> -- mirrors
    ///     <see cref="Slots.SlotMachineControl.Closed" />.
    /// </summary>
    public event Action? Closed;

    public GildedSpindleControl(SoundSystem soundSystem)
        : base("_nsett", false)
    {
        ArgumentNullException.ThrowIfNull(soundSystem);

        SoundSystem = soundSystem;

        Name = "GildedSpindle";
        Visible = false;
        UsesControlStack = true;

        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        this.CenterOnScreen();
        Y = TOP_MARGIN;

        OkButton = CreateButton("OK"); //borrowed _nsett prefab button, re-skinned as Close -- see SlotMachineControl's own remarks on why there is no dedicated control file for this feature

        if (OkButton is not null)
        {
            OkButton.NormalTexture = UiRenderer.Instance!.GetSpfTexture("_nbtn.spf");
            OkButton.PressedTexture = UiRenderer.Instance!.GetSpfTexture("_nbtn.spf", 1);
            OkButton.HoverTexture = null;
            OkButton.SelectedTexture = null;
            OkButton.DisabledTexture = null;

            OkButton.Clicked += Hide;
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

        //── pot display: a recessed readout spanning the full content width, visibility toggled per
        //   RefreshPotVisibility so the two non-eligible tiers never appear to be playing for it. Anchored to
        //   CONTENT_LEFT rather than WHEEL_WINDOW_X -- unlike the wheel window, the pot box was never meant to
        //   track the wheel's own centred position, only to span the full content width, so it must not drift
        //   off-centre (or off content-width) now that WHEEL_WINDOW_X can differ from CONTENT_LEFT ──
        const int potBoxWidth = (PANEL_WIDTH - CONTENT_RIGHT) - CONTENT_LEFT;

        PotBox = new UIPanel
        {
            X = CONTENT_LEFT,
            Y = POT_TOP,
            Width = potBoxWidth,
            Height = POT_BOX_HEIGHT,
            Background = BuildRecessedPanel(potBoxWidth, POT_BOX_HEIGHT),
            IsHitTestVisible = false,
            Visible = false
        };
        AddChild(PotBox);

        PotLabel = new UILabel
        {
            X = 8,
            Y = (POT_BOX_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = potBoxWidth - 16,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.Gold,
            IsHitTestVisible = false
        };
        PotBox.AddChild(PotLabel);

        //── recessed wheel window, added before the wheels so it draws underneath them ──
        var wheelWindow = new UIPanel
        {
            X = WHEEL_WINDOW_X,
            Y = WHEEL_WINDOW_TOP,
            Width = WHEEL_WINDOW_SIZE,
            Height = WHEEL_WINDOW_SIZE,
            Background = BuildRecessedPanel(WHEEL_WINDOW_SIZE, WHEEL_WINDOW_SIZE),
            IsHitTestVisible = false
        };
        AddChild(wheelWindow);

        var pointer = new UIPanel
        {
            X = POINTER_X,
            Y = POINTER_TOP,
            Width = POINTER_WIDTH,
            Height = POINTER_HEIGHT,
            Background = BuildPointerMarker(POINTER_WIDTH, POINTER_HEIGHT),
            IsHitTestVisible = false
        };
        AddChild(pointer);

        //both wheels share the same screen position; only one is ever Visible at a time (see BeginBonusRound).
        MainWheel = new WheelControl(WHEEL_DIAMETER)
        {
            X = WHEEL_X,
            Y = WHEEL_TOP
        };
        MainWheel.SegmentPassed += PlayTickSound;
        AddChild(MainWheel);

        BonusWheel = new WheelControl(WHEEL_DIAMETER)
        {
            X = WHEEL_X,
            Y = WHEEL_TOP,
            Visible = false
        };
        BonusWheel.SegmentPassed += PlayTickSound;
        AddChild(BonusWheel);

        //── footer: stake selector, then Spin + Gold, then the result message ──
        StakeSelector = new StakeSelectorControl
        {
            X = CONTENT_LEFT,
            Y = FOOTER_TOP
        };
        StakeSelector.StakeSelected += OnStakeSelected;
        AddChild(StakeSelector);

        SpinButton = new CustomButton("Spin", SPIN_BUTTON_WIDTH)
        {
            X = CONTENT_LEFT,
            Y = SPIN_ROW_TOP
        };
        SpinButton.Clicked += RequestSpin;
        AddChild(SpinButton);

        //the player's own purse, beside Spin -- turns red the moment the selected wager is unaffordable, the same
        //condition the server answers with an InsufficientGold rejection, said before a spin is spent finding out.
        GoldLabel = new UILabel
        {
            X = CONTENT_LEFT + SPIN_BUTTON_WIDTH + SPIN_GOLD_GAP,
            Y = SPIN_ROW_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
            Width = (PANEL_WIDTH - CONTENT_RIGHT) - (CONTENT_LEFT + SPIN_BUTTON_WIDTH + SPIN_GOLD_GAP),
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };
        AddChild(GoldLabel);

        //live for the whole session, not just while open -- mirrors SlotMachineControl's identical wiring.
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
    ///     Builds a recessed dark-fill panel bordered by dlgframe.epf's 8-piece border -- the same primitive
    ///     <see cref="SlotMachineControl" />'s identically-named helper builds, duplicated here rather than shared
    ///     since the two controls have no common base for it.
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
    ///     Builds a small downward-pointing gold triangle marking the wheel's fixed landing position.
    /// </summary>
    private static Texture2D BuildPointerMarker(int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = PointerColor
        };

        using var path = new SKPath();
        path.MoveTo(0, 0);
        path.LineTo(width, 0);
        path.LineTo(width / 2f, height);
        path.Close();
        surface.Canvas.DrawPath(path, paint);

        using var snapshot = surface.Snapshot();

        return TextureConverter.ToTexture2D(snapshot);
    }

    /// <summary>
    ///     Repaints every child from <see cref="WorldState.GildedSpindle" /> and resets any in-flight spin state
    ///     from a previously displayed machine.
    /// </summary>
    private void RefreshFromViewModel()
    {
        var vm = WorldState.GildedSpindle;
        TitleLabel.Text = vm.MachineName;

        ResetSpinState();

        StakeSelector.SetTiers(vm.Tiers, vm.SelectedTierIndex);
        ApplySelectedTierToWheels();

        //a fresh sit-down opens with usable controls, mirroring SlotMachineControl's identical reasoning: if the
        //server does still hold a cooldown from an earlier session, the first pull earns an honest rejection
        //rather than a dead Spin button with no explanation attached to it.
        SpinCooldownRemaining = 0f;

        RefreshGold();
        SnapPot();

        //an empty tier list is the same condition the server answers Misconfigured for (see ShowMisconfigured) --
        //show that up front rather than opening on a Spin button that will silently do nothing when clicked.
        if (vm.Tiers.Count == 0)
            ShowMisconfigured();
        else
        {
            SetMessage("Spin to play!", LegendColors.White, false);
            SpinButton.Enabled = true;
            StakeSelector.SetInteractable(true);
        }
    }

    /// <summary>
    ///     Shown whenever the panel has (or discovers) an empty tier list -- the same condition
    ///     <c>GildedSpindleScript.Spin</c> answers with <see cref="WheelRejectReason.Misconfigured" />
    ///     (it returns that reason exactly when <c>Config</c> is null, which is also exactly when
    ///     <c>BuildOpenPayload</c> returns no tiers). In practice the stool script refuses to send an Open display
    ///     for a misconfigured machine at all, so reaching this needs the catalog to turn invalid after Open was
    ///     already sent -- an edge case, not a common path. But a Spin button that silently no-ops on click is
    ///     strictly worse than saying so, so this is handled locally (same string <see cref="OnRejected" /> uses
    ///     for <see cref="WheelRejectReason.Misconfigured" />) rather than sending a spin packet just to provoke a
    ///     rejection the server can never actually produce here.
    /// </summary>
    private void ShowMisconfigured()
    {
        SpinButton.Enabled = false;
        SetMessage("The Spindle is out of order.", LegendColors.Red, true);
    }

    /// <summary>
    ///     Clears every field belonging to an in-flight (or just-finished) spin. Safe to call at any time --
    ///     RefreshFromViewModel, RequestSpin, and OnRejected all start from this same clean slate.
    /// </summary>
    private void ResetSpinState()
    {
        AwaitingResult = false;
        MainSpinElapsed = 0f;
        MainLanded = false;
        PendingMainStop = null;
        PendingBonusStop = null;
        BonusStarted = false;
        BonusBeatElapsed = 0f;
        BonusSpinElapsed = 0f;
        BonusLanded = false;
    }

    /// <summary>
    ///     Configures both wheels and the pot display from the currently selected tier. Called on open and every
    ///     time the stake selection changes -- see requirement #3: stake selection redraws everything locally and
    ///     sends no packet.
    /// </summary>
    private void ApplySelectedTierToWheels()
    {
        var vm = WorldState.GildedSpindle;

        MainWheel.Visible = true;
        BonusWheel.Visible = false;

        if (vm.Tiers.Count == 0)
        {
            MainWheel.SetSegments([]);
            BonusWheel.SetSegments([]);
            RefreshPotVisibility(false);

            return;
        }

        var tierIndex = Math.Clamp(vm.SelectedTierIndex, 0, vm.Tiers.Count - 1);
        var tier = vm.Tiers[tierIndex];

        MainWheel.SetSegments(tier.MainWheel);
        BonusWheel.SetSegments(tier.BonusWheel);

        RefreshPotVisibility(tier.IsJackpotEligible);
    }

    /// <summary>
    ///     Shows or hides the pot display. This is the entire implementation of requirement #4 ("labelled so the
    ///     two lower tiers do not appear to be playing for it") -- the box is not merely dimmed or captioned
    ///     differently for an ineligible tier, it is not present at all.
    /// </summary>
    private void RefreshPotVisibility(bool eligible) => PotBox.Visible = eligible;

    /// <summary>
    ///     Fired by <see cref="StakeSelector" /> when the player clicks a different stake button. Updates the
    ///     client-only <see cref="GildedSpindle.SelectedTierIndex" /> and redraws everything from it -- no packet.
    /// </summary>
    private void OnStakeSelected(int index)
    {
        WorldState.GildedSpindle.SelectedTierIndex = index;
        StakeSelector.SetTiers(WorldState.GildedSpindle.Tiers, index); //repaint the gold/white highlight
        ApplySelectedTierToWheels();
        RefreshGold(); //the wager just changed, so the affordability colour has to re-check immediately
    }

    /// <summary>
    ///     The single place the message line is written. Colour is part of the message, not decoration, mirroring
    ///     <see cref="Slots.SlotMachineControl.SetMessage" />.
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
    ///     Repaints the player's gold and the affordability colour. Wired to <c>WorldState.Inventory.GoldChanged</c>
    ///     so this follows a spin's cost/payout live rather than only on open.
    /// </summary>
    private void RefreshGold()
    {
        //reveal gate: a spin's cost/payout lands on the purse while the wheel is still turning.
        if (AwaitingResult)
            return;

        var vm = WorldState.GildedSpindle;
        var gold = WorldState.Inventory.Gold;
        GoldLabel.Text = $"Gold: {gold:N0}";

        var stake = vm.Tiers.Count == 0 ? 0 : vm.Tiers[Math.Clamp(vm.SelectedTierIndex, 0, vm.Tiers.Count - 1)].Stake;
        GoldLabel.ForegroundColor = gold < (uint)Math.Max(0, stake) ? LegendColors.Red : LegendColors.White;
    }

    /// <summary>
    ///     Puts the pot readout at the view model's amount immediately, with no count-up -- used when the panel
    ///     opens, so a fresh sit-down shows its pot rather than counting up to it from the last machine's.
    /// </summary>
    private void SnapPot()
    {
        DisplayedPot = WorldState.GildedSpindle.JackpotAmount;
        PotCountFrom = DisplayedPot;
        PotCountTo = DisplayedPot;
        PotCountElapsed = POT_COUNT_SECONDS; //already "finished"
        WritePotLabel();
    }

    private void WritePotLabel()
    {
        var value = (long)MathF.Round(DisplayedPot);

        if (value == RenderedPot)
            return;

        RenderedPot = value;
        PotLabel.Text = $"Jackpot Pot: {value:N0}";
    }

    /// <summary>
    ///     Publishes the gold and pot figures withheld during the spin -- see the class remarks on the reveal gate.
    ///     Must be called with <see cref="AwaitingResult" /> already false, since that flag is what
    ///     <see cref="RefreshGold" />/<see cref="ApplyPotToDisplay" /> gate on.
    /// </summary>
    private void RevealDeferredState()
    {
        RefreshGold();
        ApplyPotToDisplay();
    }

    /// <summary>
    ///     Call after a JackpotAlert display (<c>WorldState.GildedSpindle.ApplyJackpot</c>) so the pot updates live
    ///     without reopening the panel. Safe to call while hidden.
    /// </summary>
    public void RefreshJackpot()
    {
        var vm = WorldState.GildedSpindle;

        ApplyPotToDisplay();

        if (!AwaitingResult && (ResultMessageHoldRemaining <= 0f) && !string.IsNullOrEmpty(vm.LastJackpotWinner))
            SetMessage($"{vm.LastJackpotWinner} just won the jackpot!", LegendColors.DustyOrange, false);
    }

    /// <summary>
    ///     Moves the on-screen pot toward the view model's, unless a spin (main or bonus) is still turning.
    /// </summary>
    private void ApplyPotToDisplay()
    {
        if (AwaitingResult)
            return;

        var target = WorldState.GildedSpindle.JackpotAmount;

        //count UP to a growing pot; snap DOWN to a reset one (a pot just won should visibly reset, not drain away
        //as though it were still paying out) -- mirrors SlotMachineControl's identical reasoning.
        if (target < DisplayedPot)
            SnapPot();
        else if (Math.Abs(target - DisplayedPot) > 0.5f)
        {
            PotCountFrom = DisplayedPot;
            PotCountTo = target;
            PotCountElapsed = 0f;
        }
    }

    private void TickPotCount(float deltaSeconds)
    {
        if (PotCountElapsed >= POT_COUNT_SECONDS)
            return;

        PotCountElapsed += deltaSeconds;

        var t = Math.Clamp(PotCountElapsed / POT_COUNT_SECONDS, 0f, 1f);
        var eased = 1f - ((1f - t) * (1f - t) * (1f - t)); //cubic ease-out, matching the wheel's own settle curve

        DisplayedPot = t >= 1f ? PotCountTo : PotCountFrom + ((PotCountTo - PotCountFrom) * eased);
        WritePotLabel();
    }

    /// <summary>
    ///     Call after a SpinResult display (<c>WorldState.GildedSpindle.ApplySpinResult</c>) arrives. Captures the
    ///     server's chosen stops; the Update loop lands the wheel(s) on them once each phase's minimum spin
    ///     duration has elapsed.
    /// </summary>
    public void OnSpinResult()
    {
        if (!Visible)
            return;

        var vm = WorldState.GildedSpindle;
        PendingMainStop = vm.LastMainStop;
        PendingBonusStop = vm.LastBonusStop; //byte? -- null means the bonus round never opened this spin

        RefreshJackpot();
    }

    /// <summary>
    ///     Call on a Rejected display. Aborts any in-flight spin to a neutral stop, releases the reveal gate, and
    ///     shows a reason-specific message.
    /// </summary>
    public void OnRejected(WheelRejectReason reason)
    {
        ResetSpinState();

        //no reject reason takes the stake except Cooldown, which means a cooldown from an EARLIER spin genuinely
        //is still running -- keeping that one is what stops an immediate retry from being rejected all over again.
        if (reason is not WheelRejectReason.Cooldown)
            SpinCooldownRemaining = 0f;

        SpinButton.Enabled = SpinCooldownRemaining <= 0f;
        StakeSelector.SetInteractable(true);

        MainWheel.Visible = true;
        BonusWheel.Visible = false;
        MainWheel.LandOn(0);

        //a rejection releases the reveal gate -- there is no result coming, so nothing is left to be spoiled.
        RevealDeferredState();

        var text = reason switch
        {
            WheelRejectReason.NotOccupant      => "Sit at the Spindle to play.",
            WheelRejectReason.Cooldown         => "The wheel is still turning.",
            WheelRejectReason.InsufficientGold => $"You need {RequestedStake:N0} gold at this wager.",
            WheelRejectReason.InvalidStake     => "That wager isn't offered here.",
            WheelRejectReason.MachineBusy      => "Someone else is at the Spindle.",
            WheelRejectReason.Misconfigured    => "The Spindle is out of order.",
            _                                  => "That did not work."
        };

        SetMessage(text, LegendColors.Red, true);
    }

    private void RequestSpin()
    {
        if (AwaitingResult || !Visible)
            return;

        var vm = WorldState.GildedSpindle;

        if (vm.Tiers.Count == 0)
        {
            //there is genuinely nothing to spin -- say so rather than silently ignoring the click. Do NOT send
            //SpinRequested here: there is no tier to bet, so there is nothing honest to ask the server to bill.
            ShowMisconfigured();

            return;
        }

        var tierIndex = Math.Clamp(vm.SelectedTierIndex, 0, vm.Tiers.Count - 1);
        RequestedStake = vm.Tiers[tierIndex].Stake;

        ResetSpinState();
        AwaitingResult = true;
        SpinCooldownRemaining = SERVER_SPIN_COOLDOWN_SECONDS;

        SetMessage(string.Empty, LegendColors.White, false); //a new spin supersedes whatever text was being held

        SpinButton.Enabled = false;
        StakeSelector.SetInteractable(false); //the stake can't change out from under a wheel already turning

        MainWheel.Visible = true;
        BonusWheel.Visible = false;
        MainWheel.StartSpin();

        SpinRequested?.Invoke((byte)tierIndex);
    }

    /// <summary>
    ///     The moment the main wheel settled on BONUS and its beat has elapsed: swaps the visible wheel and starts
    ///     the bonus wheel spinning, and plays the bonus-opening sting. Ungated by the reveal -- the wheel swap
    ///     itself already tells the player a bonus round opened; only the exact payout figure stays hidden a
    ///     moment longer. The bonus wheel already draws its own segments' labels on its face (see
    ///     <see cref="WheelControl.SetSegments" />, called from <see cref="ApplySelectedTierToWheels" /> before this
    ///     spin ever started), so there is nothing further to repaint here now that the payout rail is gone.
    /// </summary>
    private void BeginBonusRound()
    {
        BonusStarted = true;

        MainWheel.Visible = false;
        BonusWheel.Visible = true;

        BonusWheel.StartSpin();
        SoundSystem.PlaySound(SOUND_BONUS_OPEN);
    }

    private void PlayTickSound() => SoundSystem.PlaySound(SOUND_TICK_OR_LAND);

    /// <summary>
    ///     Colour/sound for the just-finished spin's outcome. The sound is chosen by the same switch that picks the
    ///     words, so the two can never disagree about what just happened. A dead spin stays silent -- silence is
    ///     what makes the other three mean anything.
    /// </summary>
    private void ShowResultMessage()
    {
        var vm = WorldState.GildedSpindle;

        var (text, color, sound) = vm switch
        {
            { LastWasJackpot: true } => ($"JACKPOT! {vm.LastPayout:N0} gold!", LegendColors.Gold, SOUND_JACKPOT),
            { LastMultiplier: >= HIGH_BAND_MULTIPLIER, LastPayout: > 0 } => (
                $"{vm.LastLabel} — {vm.LastMultiplier}x — {vm.LastPayout:N0} gold", LegendColors.PastelYellow, SOUND_PAYOUT_HIGH),
            { LastPayout: > 0 } => (
                $"{vm.LastLabel} — {vm.LastMultiplier}x — {vm.LastPayout:N0} gold", LegendColors.PastelYellow, SOUND_PAYOUT_LOW),
            _ => ("No win. Spin again.", LegendColors.Gray, 0)
        };

        SetMessage(text, color, true);

        if (sound > 0)
            SoundSystem.PlaySound(sound);
    }

    /// <summary>
    ///     Ends the spin: releases the reveal gate, re-enables the controls (subject to the cooldown gate), and
    ///     writes the result. Called once the wheel that ends the sequence (main, or bonus if one opened) settles.
    /// </summary>
    private void FinishSpin()
    {
        AwaitingResult = false;
        SpinButton.Enabled = SpinCooldownRemaining <= 0f;
        StakeSelector.SetInteractable(true);

        //the reveal: publish the figures withheld during the spin BEFORE announcing the result, so the gold and
        //pot the player looks at the moment they read "JACKPOT!" already agree with it.
        RevealDeferredState();
        ShowResultMessage();
    }

    /// <summary>
    ///     Drives both wheels and the spin/reveal sequence every frame. Distinct from the inherited GameTime-based
    ///     Update -- like <see cref="WheelControl.Update(float)" />, this takes elapsed seconds directly and must
    ///     be called explicitly from <c>WorldScreen.Update</c>.
    /// </summary>
    public void Update(float deltaSeconds)
    {
        if (!Visible)
            return;

        if (ResultMessageHoldRemaining > 0f)
            ResultMessageHoldRemaining = Math.Max(0f, ResultMessageHoldRemaining - deltaSeconds);

        if (SpinCooldownRemaining > 0f)
        {
            SpinCooldownRemaining = Math.Max(0f, SpinCooldownRemaining - deltaSeconds);

            //the ordinary case: a plain spin's animation finishes well before this, so this is where the button
            //comes back. A bonus round instead finds AwaitingResult still set here and leaves the button disabled.
            if ((SpinCooldownRemaining <= 0f) && !AwaitingResult)
                SpinButton.Enabled = true;
        }

        TickPotCount(deltaSeconds);

        MainWheel.Update(deltaSeconds);
        BonusWheel.Update(deltaSeconds);

        if (!AwaitingResult)
            return;

        if (!MainLanded)
        {
            MainSpinElapsed += deltaSeconds;

            if ((PendingMainStop is { } mainStop) && (MainSpinElapsed >= MIN_SPIN_SECONDS))
            {
                MainWheel.LandOn(mainStop);
                MainLanded = true;
                SoundSystem.PlaySound(SOUND_TICK_OR_LAND);
            }

            return;
        }

        if (!MainWheel.IsSettled)
            return; //still easing toward its stop

        if (PendingBonusStop is null)
        {
            //no bonus round this spin -- the main wheel settling is the whole story.
            FinishSpin();

            return;
        }

        if (!BonusStarted)
        {
            BonusBeatElapsed += deltaSeconds;

            if (BonusBeatElapsed < BONUS_BEAT_SECONDS)
                return;

            BeginBonusRound();

            return;
        }

        if (!BonusLanded)
        {
            BonusSpinElapsed += deltaSeconds;

            if ((PendingBonusStop is { } bonusStop) && (BonusSpinElapsed >= MIN_SPIN_SECONDS))
            {
                BonusWheel.LandOn(bonusStop);
                BonusLanded = true;
                SoundSystem.PlaySound(SOUND_TICK_OR_LAND);
            }

            return;
        }

        if (!BonusWheel.IsSettled)
            return; //still easing toward its stop

        FinishSpin();
    }

    /// <summary>
    ///     Repaints from <see cref="WorldState.GildedSpindle" /> (already populated by ApplyOpen) and shows the
    ///     panel.
    /// </summary>
    public override void Show()
    {
        ResetSpinState();

        base.Show();
        RefreshFromViewModel();
    }

    /// <summary>
    ///     Hides the panel and clears <see cref="WorldState.GildedSpindle" /> so a stale name/tier list/last
    ///     result can never survive onto whatever the player looks at next -- mirrors
    ///     <see cref="Slots.SlotMachineControl.Hide" />.
    /// </summary>
    public override void Hide()
    {
        var wasVisible = Visible;

        ResetSpinState();
        ResultMessageHoldRemaining = 0f;
        SpinCooldownRemaining = 0f;

        base.Hide();
        WorldState.GildedSpindle.Clear();

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
}
