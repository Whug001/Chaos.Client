#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tamer;

/// <summary>
///     One offered opponent on the Ranked tab: the name, the tier and step under it, the five species of the defence
///     and the Attack button. An empty offer slot draws nothing. Every number comes from
///     <see cref="TamerRankedLayout" /> and every answer from <see cref="TamerRankedState" />.
/// </summary>
public sealed class TamerRankedOfferRow : UIPanel
{
    private readonly CustomButton AttackButton;
    private readonly UILabel NameLabel;
    private readonly TamerCreaturePortrait[] Portraits = new TamerCreaturePortrait[TamerRankedLayout.OFFER_PORTRAIT_COUNT];
    private readonly UILabel StepLabel;

    public TamerRankedOfferRow(CreatureRenderer creatures, int slot)
    {
        ArgumentNullException.ThrowIfNull(creatures);

        Slot = slot;
        Name = $"TamerRankedOffer{slot}";
        Width = TamerRankedLayout.RIGHT_WIDTH;
        Height = TamerRankedLayout.OFFER_HEIGHT;
        BorderColor = LegendColors.DimGray;
        Visible = false;

        NameLabel = Label(TamerRankedLayout.OFFER_NAME_TOP, LegendColors.White);
        StepLabel = Label(TamerRankedLayout.OFFER_STEP_TOP, LegendColors.Silver);
        AddChild(NameLabel);
        AddChild(StepLabel);

        for (var i = 0; i < Portraits.Length; i++)
        {
            var box = TamerRankedLayout.OfferPortrait(i);

            Portraits[i] = new TamerCreaturePortrait(creatures)
            {
                X = box.X,
                Y = box.Y,
                Width = box.Width,
                Height = box.Height,
                Animate = false,
                IsHitTestVisible = false
            };
            AddChild(Portraits[i]);
        }

        AttackButton = new CustomButton(TamerRankedText.Attack, TamerRankedLayout.ACTION_BUTTON_WIDTH)
        {
            X = TamerRankedLayout.ATTACK_LEFT,
            Y = TamerRankedLayout.ATTACK_TOP
        };
        AttackButton.Clicked += () => AttackClicked?.Invoke(Slot);
        AddChild(AttackButton);
    }

    /// <summary>The offer slot this row shows (0 to 2).</summary>
    public int Slot { get; }

    /// <summary>The Attack button was clicked; the argument is the offer slot.</summary>
    public event Action<int>? AttackClicked;

    private static UILabel Label(int y, Color color)
        => new()
        {
            X = TamerRankedLayout.OFFER_NAME_LEFT,
            Y = y,
            Width = TamerRankedLayout.OFFER_NAME_WIDTH,
            Height = TamerRankedLayout.TEXT_HEIGHT,
            ForegroundColor = color,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };

    /// <summary>Shows the slot's opponent, or hides the row when the slot is empty.</summary>
    public void Bind(TamerRankedState ranked, TamerStableState stable)
    {
        ArgumentNullException.ThrowIfNull(ranked);
        ArgumentNullException.ThrowIfNull(stable);

        if (ranked.OfferAt(Slot) is not { } offer)
        {
            Visible = false;

            return;
        }

        Visible = true;
        NameLabel.Text = offer.Name;
        StepLabel.Text = TamerRankedText.StepName(offer.Step);

        for (var i = 0; i < Portraits.Length; i++)
        {
            var key = (offer.Species is not null) && (i < offer.Species.Count) ? offer.Species[i] : null;

            //the look it was caught with, else its species; a species the catalog lacks has sprite 0, which draws the empty box
            Portraits[i].SpriteId = stable.SpriteOf(key, TamerRankedState.OfferSpriteAt(offer, i));
        }

        AttackButton.Enabled = ranked.CanAttack(Slot, stable, out _);
    }
}
