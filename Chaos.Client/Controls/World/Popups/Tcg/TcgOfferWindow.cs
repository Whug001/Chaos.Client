#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg;

/// <summary>An incoming card challenge: who, the stakes, a deck picker, a countdown, Accept and Decline.</summary>
public sealed class TcgOfferWindow : UIPanel
{
    private const int BOX_HEIGHT = 150;
    private const int BOX_WIDTH = 300;
    private const int PADDING = 10;
    private const int ROW = 26;

    private readonly UILabel Title;
    private readonly UILabel StakesLabel;
    private readonly UILabel DeckLabel;
    private readonly UILabel Countdown;
    private List<string> Decks = [];
    private int DeckIndex;
    private uint OfferMs;
    private DateTime ReceivedAt;

    public TcgOfferWindow(int screenWidth, int screenHeight)
    {
        Name = "TcgOffer";
        Width = BOX_WIDTH;
        Height = BOX_HEIGHT;
        X = (screenWidth - BOX_WIDTH) / 2;
        Y = (screenHeight - BOX_HEIGHT) / 2;
        Visible = false;
        UsesControlStack = true;
        BackgroundColor = new Color(28, 20, 12);
        BorderColor = LegendColors.Gold;

        Title = Label(PADDING, LegendColors.Gold);
        StakesLabel = Label(PADDING + ROW, LegendColors.White);
        DeckLabel = Label(PADDING + (2 * ROW), LegendColors.White);
        DeckLabel.X = PADDING + 30;
        DeckLabel.Width = BOX_WIDTH - (2 * PADDING) - 60;
        Countdown = Label(BOX_HEIGHT - CustomButton.HEIGHT - PADDING + 4, LegendColors.White);
        Countdown.Width = 80;

        var prev = new CustomButton("<", 24) { X = PADDING, Y = PADDING + (2 * ROW) };
        var next = new CustomButton(">", 24) { X = BOX_WIDTH - PADDING - 24, Y = PADDING + (2 * ROW) };
        var accept = new CustomButton("Accept", 92) { X = BOX_WIDTH - PADDING - 192, Y = BOX_HEIGHT - CustomButton.HEIGHT - PADDING };
        var decline = new CustomButton("Decline", 92) { X = BOX_WIDTH - PADDING - 92, Y = BOX_HEIGHT - CustomButton.HEIGHT - PADDING };

        prev.Clicked += () => MoveDeck(-1);
        next.Clicked += () => MoveDeck(1);
        accept.Clicked += Accept;
        decline.Clicked += Decline;

        foreach (var control in new UIElement[] { Title, StakesLabel, prev, DeckLabel, next, Countdown, accept, decline })
            AddChild(control);
    }

    public event Action<TcgActionArgs>? Action;

    public void Decline()
    {
        Action?.Invoke(TcgChallengeState.DeclineAction());
        Hide();
    }

    public void Hide()
    {
        if (UsesControlStack)
            InputDispatcher.Instance?.RemoveControl(this);

        Visible = false;
    }

    public void Show()
    {
        if (UsesControlStack)
            InputDispatcher.Instance?.PushControl(this);

        Visible = true;
    }

    public void ShowOffer(string challenger, TcgStakes stakes, int gold, uint expiresMs, IReadOnlyList<string> decks)
    {
        Decks = decks.ToList();
        DeckIndex = 0;
        OfferMs = expiresMs;
        ReceivedAt = DateTime.UtcNow;
        Title.Text = $"{challenger} challenges you to a card match";
        StakesLabel.Text = TcgChallengeState.StakesText(stakes, gold);
        DeckLabel.Text = Decks.Count > 0 ? Decks[0] : "No Ready deck";
        Show();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Decline();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (Visible)
            Countdown.Text = $"{TcgChallengeState.SecondsLeft(OfferMs, ReceivedAt, DateTime.UtcNow)} s";
    }

    private void Accept()
    {
        if (Decks.Count == 0)
            return;

        Action?.Invoke(TcgChallengeState.AcceptAction(Decks[DeckIndex]));
    }

    private UILabel Label(int y, Color color)
        => new()
        {
            X = PADDING,
            Y = y,
            Width = BOX_WIDTH - (2 * PADDING),
            Height = 20,
            ForegroundColor = color,
            IsHitTestVisible = false
        };

    private void MoveDeck(int step)
    {
        if (Decks.Count == 0)
            return;

        DeckIndex = (DeckIndex + step + Decks.Count) % Decks.Count;
        DeckLabel.Text = Decks[DeckIndex];
    }
}
