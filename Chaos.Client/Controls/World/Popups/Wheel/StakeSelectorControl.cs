#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.ViewModel;
#endregion

namespace Chaos.Client.Controls.World.Popups.Wheel;

/// <summary>
///     A single "Place Bet" header over three stake-cost buttons, laid out horizontally. Selecting a tier
///     is purely local: it raises <see cref="StakeSelected" /> and does nothing else on its own -- no packet, no
///     server round-trip. The owning panel (<see cref="GildedSpindleControl" />) is what actually updates
///     <see cref="GildedSpindle.SelectedTierIndex" /> and redraws the wheel/rail from it.
/// </summary>
/// <remarks>
///     <see cref="GildedSpindle.SelectedTierIndex" />'s own doc comment explains why this can never desync from
///     the server: every tier's price was handed out up front in the Open display, and the server bills exactly
///     whichever index the client sends on the next spin -- there is no server-held "selected stake" for a local
///     click to disagree with.
///     <para>
///         There used to be a per-tier tier-name label above each button, and THAT carried the selection
///         highlight (gold vs white). The name labels were removed as pure noise -- the button caption already
///         shows the stake, which is the informative part -- but that meant the selection highlight needed a new
///         home. It could not simply move onto the button's own caption colour: <see cref="CustomButton" />
///         repaints its caption colour every frame from its Enabled/Pressed state (see
///         <see cref="CustomButton.Draw" />), unconditionally, so anything written to that colour from outside
///         would be stomped the very next frame. <see cref="CustomButton" /> also exposes no selected/toggled
///         visual state of its own (only Enabled and a transient, mouse-held Pressed dim). So the selected
///         button's caption is bracketed instead -- <c>[1,000g]</c> vs <c>1,000g</c> -- a change to the button's
///         own <see cref="CustomButton.Caption" /> text, which <see cref="CustomButton" /> never touches, so it
///         survives every frame.
///     </para>
/// </remarks>
public sealed class StakeSelectorControl : UIPanel
{
    /// <summary>Fixed at three, matching the Gilded Spindle's shipped three-tier design.</summary>
    private const int MAX_TIERS = 3;

    private const int BUTTON_WIDTH = 76;
    private const int COLUMN_GAP = 8;

    /// <summary>Vertical gap between the "Place Bet" header and the button row.</summary>
    private const int HEADER_GAP = 2;

    /// <summary>
    ///     This control's known fixed footprint, exposed so <see cref="GildedSpindleControl" /> can lay out
    ///     around it without duplicating the arithmetic that produces it (the same reasoning
    ///     <see cref="CustomButton.HEIGHT" /> is public for).
    /// </summary>
    public const int WIDTH = (BUTTON_WIDTH * MAX_TIERS) + (COLUMN_GAP * (MAX_TIERS - 1));

    public const int HEIGHT = TextRenderer.CHAR_HEIGHT + HEADER_GAP + CustomButton.HEIGHT;

    /// <summary>
    ///     The Y offset, within this control, of the button row -- exposed so the panel can align its own Spin
    ///     button against the same row as these buttons rather than the header row above them.
    /// </summary>
    public const int BUTTON_ROW_Y = TextRenderer.CHAR_HEIGHT + HEADER_GAP;

    private readonly UILabel HeaderLabel;
    private readonly CustomButton[] Buttons = new CustomButton[MAX_TIERS];

    //the tier this control last painted as selected -- compared against on click so mashing the already-active
    //button doesn't re-fire StakeSelected for no reason. The real source of truth is
    //GildedSpindle.SelectedTierIndex, held by the owning panel; this is only a local echo of it kept in sync by
    //every SetTiers call.
    private int SelectedIndex;

    /// <summary>
    ///     Raised with the newly selected tier's index whenever the player clicks a different stake button. Purely
    ///     local selection -- see the class remarks.
    /// </summary>
    public event Action<int>? StakeSelected;

    public StakeSelectorControl()
    {
        Width = WIDTH;
        Height = HEIGHT;

        HeaderLabel = new UILabel
        {
            X = 0,
            Y = 0,
            Width = WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false,
            Text = "Place Bet"
        };
        AddChild(HeaderLabel);

        for (var i = 0; i < MAX_TIERS; i++)
        {
            var index = i; //captured per-column for the button's Clicked closure
            var columnX = i * (BUTTON_WIDTH + COLUMN_GAP);

            var button = new CustomButton(string.Empty, BUTTON_WIDTH)
            {
                X = columnX,
                Y = BUTTON_ROW_Y,
                Visible = false
            };
            button.Clicked += () => Select(index);
            Buttons[i] = button;
            AddChild(button);
        }
    }

    /// <summary>
    ///     Repaints every populated column from <paramref name="tiers" /> and brackets
    ///     <paramref name="selectedIndex" />'s button caption (e.g. <c>[1,000g]</c>) to mark it as selected. Call
    ///     whenever the tier list changes (a fresh Open) or the selection changes, from whichever side changed it.
    /// </summary>
    public void SetTiers(IReadOnlyList<WheelTierInfo> tiers, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(tiers);

        //a machine offering more than MAX_TIERS tiers has outgrown this fixed three-column layout. Rather than
        //silently dropping the extra tiers -- a player betting at a tier that was never shown -- log loudly, the
        //same way SlotMachineControl's paytable overflow does for its own fixed-size pool.
        if (tiers.Count > MAX_TIERS)
            System.Diagnostics.Debug.WriteLine(
                $"StakeSelectorControl: {tiers.Count} tiers offered, exceeding the {MAX_TIERS}-button layout -- "
                + $"{tiers.Count - MAX_TIERS} tier(s) will not be selectable from this panel.");

        SelectedIndex = tiers.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, tiers.Count - 1);

        for (var i = 0; i < MAX_TIERS; i++)
        {
            if (i >= tiers.Count)
            {
                Buttons[i].Visible = false;

                continue;
            }

            var tier = tiers[i];
            var selected = i == SelectedIndex;

            //the selection highlight used to live on a per-tier name label's colour; now it brackets the button's
            //own caption instead -- see the class remarks for why CustomButton rules out a caption-colour
            //approach.
            Buttons[i].Caption = selected ? $"[{tier.Stake:N0}g]" : $"{tier.Stake:N0}g";
            Buttons[i].Visible = true;
        }
    }

    /// <summary>
    ///     Enables or disables every populated button. The panel calls this <see langword="false" /> for the
    ///     duration of a spin: the stake can't change out from under a wheel that is already turning, since there
    ///     is no server round-trip to re-validate a mid-spin change against.
    /// </summary>
    public void SetInteractable(bool enabled)
    {
        foreach (var button in Buttons)
            if (button.Visible)
                button.Enabled = enabled;
    }

    private void Select(int index)
    {
        if (index == SelectedIndex)
            return;

        StakeSelected?.Invoke(index);
    }
}
