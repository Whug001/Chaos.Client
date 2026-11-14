#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tamer;

/// <summary>
///     The feed prompt: a dimmed sheet with a centred box holding the Essence and Feed source buttons, the amount
///     spinner (1 to the most that is useful or held) and the "Level A -> B" line. Confirm sends when the state allows
///     it. A snapshot while the prompt is open re-clamps the amount and closes the prompt if the monster is gone.
/// </summary>
public sealed class TamerFeedPrompt : UIPanel
{
    private const int ACTION_BUTTON_WIDTH = 90;
    private const int ACTION_GAP = 10;
    private const int ACTION_TOP = 122;
    private const int BOX_HEIGHT = 170;
    private const int BOX_WIDTH = 360;
    private const int PADDING = 10;
    private const int RESULT_TOP = 96;
    private const int SOURCE_BUTTON_WIDTH = 130;
    private const int SOURCE_GAP = 10;
    private const int SOURCE_TOP = 32;
    private const int SPINNER_TOP = 64;
    private const int SPINNER_WIDTH = 70;
    private const int TITLE_TOP = 10;

    private readonly UIPanel Box;
    private readonly UILabel TitleLabel;
    private readonly CustomButton EssenceButton;
    private readonly CustomButton FeedButton;
    private readonly CustomNumericSpinner AmountSpinner;
    private readonly UILabel ResultLabel;
    private readonly CustomButton ConfirmButton;
    private Guid MonsterId;
    private TamerStableState? LastState;
    private bool Refreshing;
    private TamerWireFeedSource Source = TamerWireFeedSource.Essence;

    public TamerFeedPrompt(int width, int height)
    {
        Name = "TamerFeed";
        Width = width;
        Height = height;
        Visible = false;
        BackgroundColor = new Color(0, 0, 0, 150);

        Box = new UIPanel
        {
            X = (width - BOX_WIDTH) / 2,
            Y = (height - BOX_HEIGHT) / 2,
            Width = BOX_WIDTH,
            Height = BOX_HEIGHT,
            BackgroundColor = new Color(28, 20, 12),
            BorderColor = LegendColors.Gold
        };
        AddChild(Box);

        TitleLabel = new UILabel
        {
            X = PADDING,
            Y = TITLE_TOP,
            Width = BOX_WIDTH - (2 * PADDING),
            Height = TextRenderer.CHAR_HEIGHT,
            Text = TamerStableText.Feed,
            ForegroundColor = LegendColors.Gold,
            HorizontalAlignment = HorizontalAlignment.Center,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        Box.AddChild(TitleLabel);

        var sourcesWidth = (2 * SOURCE_BUTTON_WIDTH) + SOURCE_GAP;

        EssenceButton = new CustomButton(TamerStableText.EssenceLabel, SOURCE_BUTTON_WIDTH)
        {
            X = (BOX_WIDTH - sourcesWidth) / 2,
            Y = SOURCE_TOP
        };
        EssenceButton.Clicked += () => ChooseSource(TamerWireFeedSource.Essence);
        Box.AddChild(EssenceButton);

        FeedButton = new CustomButton(TamerStableText.FeedLabel, SOURCE_BUTTON_WIDTH)
        {
            X = EssenceButton.X + SOURCE_BUTTON_WIDTH + SOURCE_GAP,
            Y = SOURCE_TOP
        };
        FeedButton.Clicked += () => ChooseSource(TamerWireFeedSource.Feed);
        Box.AddChild(FeedButton);

        AmountSpinner = new CustomNumericSpinner(SPINNER_WIDTH)
        {
            X = (BOX_WIDTH - SPINNER_WIDTH) / 2,
            Y = SPINNER_TOP
        };
        AmountSpinner.ValueChanged += _ =>
        {
            if (!Refreshing && (LastState is not null))
                Refresh(LastState);
        };
        Box.AddChild(AmountSpinner);

        ResultLabel = new UILabel
        {
            X = PADDING,
            Y = RESULT_TOP,
            Width = BOX_WIDTH - (2 * PADDING),
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        Box.AddChild(ResultLabel);

        ConfirmButton = new CustomButton(TamerStableText.Confirm, ACTION_BUTTON_WIDTH)
        {
            X = (BOX_WIDTH - ((2 * ACTION_BUTTON_WIDTH) + ACTION_GAP)) / 2,
            Y = ACTION_TOP
        };
        ConfirmButton.Clicked += Send;
        Box.AddChild(ConfirmButton);

        var cancel = new CustomButton(TamerStableText.Cancel, ACTION_BUTTON_WIDTH)
        {
            X = ConfirmButton.X + ACTION_BUTTON_WIDTH + ACTION_GAP,
            Y = ACTION_TOP
        };
        cancel.Clicked += Hide;
        Box.AddChild(cancel);
    }

    /// <summary>A feed the state allows. The prompt has already closed.</summary>
    public event Action<TamerActionArgs>? Confirmed;

    /// <summary>
    ///     Opens the prompt on a monster with an amount of 1; the amount limit and preview follow on <see cref="Refresh" />.
    /// </summary>
    public void Open(Guid monsterId)
    {
        MonsterId = monsterId;
        Source = TamerWireFeedSource.Essence;

        //back to an amount of 1 every time it opens (SetRange resets the value to its minimum without raising
        //ValueChanged); Refresh then widens the range to what is useful and held
        AmountSpinner.SetRange(1, 1);
        Visible = true;
    }

    /// <summary>Closes the prompt, cancelling or after a valid send.</summary>
    public void Hide()
    {
        AmountSpinner.ResetInteractionState();
        MonsterId = Guid.Empty;
        Visible = false;
    }

    /// <summary>Re-clamps the amount to what is useful and held, and repaints the preview line.</summary>
    public void Refresh(TamerStableState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!Visible)
            return;

        LastState = state;
        Refreshing = true;

        try
        {
            var monster = state.Monsters.FirstOrDefault(monster => monster.Id == MonsterId);

            if (monster is null)
            {
                Hide();
                return;
            }

            TitleLabel.Text = TamerStableText.FitLines($"{TamerStableText.Feed} {monster.Name}", BOX_WIDTH - (2 * PADDING), 1);
            EssenceButton.Caption = TamerStableText.EssenceLine(state.Essence);
            FeedButton.Caption = TamerStableText.FeedLine(state.FeedItemsHeld);
            EssenceButton.Selected = Source == TamerWireFeedSource.Essence;
            FeedButton.Selected = Source == TamerWireFeedSource.Feed;

            var kept = AmountSpinner.Value;
            var limit = state.FeedLimit(monster, Source);

            AmountSpinner.SetRange(1, Math.Max(1, limit));
            AmountSpinner.Value = kept;

            var amount = AmountSpinner.Value;
            var allowed = state.CanFeed(monster, Source, amount, out var problem);

            ConfirmButton.Enabled = allowed;
            ResultLabel.ForegroundColor = problem is null ? LegendColors.White : LegendColors.Orange;
            ResultLabel.Text = problem is null ? state.FeedPreviewText(monster, Source, amount) : problem;
        } finally
        {
            Refreshing = false;
        }
    }

    private void ChooseSource(TamerWireFeedSource source)
    {
        Source = source;

        if (LastState is not null)
            Refresh(LastState);
    }

    /// <summary>Sends the feed when the state allows it; the prompt has already closed.</summary>
    private void Send()
    {
        if (!Visible)
            return;

        AmountSpinner.Commit();

        if ((LastState is null) || !LastState.CanFeed(
                LastState.Monsters.FirstOrDefault(monster => monster.Id == MonsterId),
                Source,
                AmountSpinner.Value,
                out _))
        {
            if (LastState is not null)
                Refresh(LastState);

            return;
        }

        var action = LastState.FeedAction(MonsterId, Source, AmountSpinner.Value);

        if (action is null)
        {
            Refresh(LastState);
            return;
        }

        Hide();
        Confirmed?.Invoke(action);
    }
}
