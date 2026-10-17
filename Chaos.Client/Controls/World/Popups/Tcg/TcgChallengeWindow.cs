#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg;

/// <summary>
///     Sets up a card challenge: deck, stakes and gold, then Send. After the server confirms the offer it shows a waiting
///     view with a countdown and Cancel.
/// </summary>
public sealed class TcgChallengeWindow : UIPanel
{
    private const int BOX_HEIGHT = 190;
    private const int BOX_WIDTH = 300;
    private const int PADDING = 10;
    private const int ROW = 26;

    private readonly UILabel Title;
    private readonly UILabel DeckLabel;
    private readonly CustomButton PrevDeck;
    private readonly CustomButton NextDeck;
    private readonly CustomButton[] StakeButtons = new CustomButton[3];
    private readonly CustomTextBox GoldBox;
    private readonly UILabel Status;
    private readonly CustomButton SendButton;
    private readonly CustomButton CancelButton;
    private static readonly string[] StakeCaptions = ["None", "Gold", "Card"];
    private List<string> Decks = [];
    private int DeckIndex;
    private DateTime OfferReceivedAt;
    private uint OfferMs;
    private TcgStakes Stakes;
    private string Target = string.Empty;
    private bool Waiting;

    public TcgChallengeWindow(int screenWidth, int screenHeight)
    {
        Name = "TcgChallenge";
        Width = BOX_WIDTH;
        Height = BOX_HEIGHT;
        X = (screenWidth - BOX_WIDTH) / 2;
        Y = (screenHeight - BOX_HEIGHT) / 2;
        Visible = false;
        UsesControlStack = true;
        BackgroundColor = new Color(28, 20, 12);
        BorderColor = LegendColors.Gold;

        Title = Label(PADDING, BOX_WIDTH - (2 * PADDING), LegendColors.Gold);

        PrevDeck = new CustomButton("<", 24) { X = PADDING, Y = PADDING + ROW };
        DeckLabel = Label(PADDING + ROW, BOX_WIDTH - (2 * PADDING) - 60, LegendColors.White);
        DeckLabel.X = PADDING + 30;
        NextDeck = new CustomButton(">", 24) { X = BOX_WIDTH - PADDING - 24, Y = PADDING + ROW };
        PrevDeck.Clicked += () => MoveDeck(-1);
        NextDeck.Clicked += () => MoveDeck(1);

        for (var i = 0; i < StakeButtons.Length; i++)
        {
            var stakes = (TcgStakes)i;
            StakeButtons[i] = new CustomButton(StakeCaptions[i], 86) { X = PADDING + (i * 94), Y = PADDING + (2 * ROW) };
            StakeButtons[i].Clicked += () => PickStakes(stakes);
        }

        GoldBox = new CustomTextBox
        {
            X = PADDING,
            Y = PADDING + (3 * ROW),
            Width = 120,
            Height = CustomButton.HEIGHT,
            HintText = "gold (1,000+)",
            DigitsOnly = true
        };

        Status = Label(PADDING + (4 * ROW), BOX_WIDTH - (2 * PADDING), LegendColors.White);

        SendButton = new CustomButton("Send", 92) { X = BOX_WIDTH - PADDING - 192, Y = BOX_HEIGHT - CustomButton.HEIGHT - PADDING };
        CancelButton = new CustomButton("Cancel", 92) { X = BOX_WIDTH - PADDING - 92, Y = BOX_HEIGHT - CustomButton.HEIGHT - PADDING };
        SendButton.Clicked += Send;
        CancelButton.Clicked += Cancel;

        foreach (var control in new UIElement[] { Title, PrevDeck, DeckLabel, NextDeck, GoldBox, Status, SendButton, CancelButton })
            AddChild(control);

        foreach (var button in StakeButtons)
            AddChild(button);
    }

    /// <summary>Raised with the action to send to the server.</summary>
    public event Action<Chaos.Networking.Entities.Client.TcgActionArgs>? Action;

    public void Cancel()
    {
        if (Waiting)
            Action?.Invoke(TcgChallengeState.CancelAction());

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

    public void ShowSetup(string target, IReadOnlyList<string> decks)
    {
        Target = target;
        Decks = decks.ToList();
        DeckIndex = 0;
        Waiting = false;
        Title.Text = $"Challenge {target} to a card match";
        GoldBox.Text = string.Empty;
        Status.Text = string.Empty;
        PickStakes(TcgStakes.None);
        SetSetupVisible(true);
        Show();
    }

    public void ShowWaiting(TcgStakes stakes, int gold, uint expiresMs)
    {
        Waiting = true;
        OfferMs = expiresMs;
        OfferReceivedAt = DateTime.UtcNow;
        Title.Text = $"Waiting for {Target}...";
        Status.Text = TcgChallengeState.StakesText(stakes, gold);
        SetSetupVisible(false);
        Show();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Cancel();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (Visible && Waiting)
            Title.Text = $"Waiting for {Target}... {TcgChallengeState.SecondsLeft(OfferMs, OfferReceivedAt, DateTime.UtcNow)}";
    }

    private UILabel Label(int y, int width, Color color)
        => new()
        {
            X = PADDING,
            Y = y,
            Width = width,
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

    private void PickStakes(TcgStakes stakes)
    {
        Stakes = stakes;

        for (var i = 0; i < StakeButtons.Length; i++)
            StakeButtons[i].Caption = (TcgStakes)i == stakes ? $"[{StakeCaptions[i]}]" : StakeCaptions[i];

        GoldBox.Visible = stakes == TcgStakes.Gold;
        DeckLabel.Text = Decks.Count > 0 ? Decks[DeckIndex] : string.Empty;
    }

    private void Send()
    {
        if (Decks.Count == 0)
            return;

        var gold = 0;

        if ((Stakes == TcgStakes.Gold) && !TcgChallengeState.TryParseGold(GoldBox.Text, out gold, out var error))
        {
            Status.Text = error;

            return;
        }

        Action?.Invoke(TcgChallengeState.SendAction(Target, Decks[DeckIndex], Stakes, gold));
    }

    private void SetSetupVisible(bool visible)
    {
        PrevDeck.Visible = NextDeck.Visible = DeckLabel.Visible = SendButton.Visible = visible;

        foreach (var button in StakeButtons)
            button.Visible = visible;

        GoldBox.Visible = visible && (Stakes == TcgStakes.Gold);
    }
}
