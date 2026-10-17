#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg;

/// <summary>
///     The ranked queue bar at the top centre of the world screen: searching time with Leave, a ready-check box with
///     a countdown and Accept/Decline, or a waiting line once accepted. Driven by <see cref="TcgRankedState" />,
///     hidden while the match window is open. Every button only asks: the server answers with a fresh display.
/// </summary>
public sealed class TcgQueueBar : UIPanel
{
    private const int BAR_WIDTH = 320;
    private const int BUTTON_WIDTH = 92;
    private const int PADDING = 8;
    private const int SEARCH_HEIGHT = 56;
    private const int READY_HEIGHT = 122;
    private const int TOP = 4;

    private readonly CustomButton AcceptButton;
    private readonly CustomButton DeclineButton;
    private readonly Func<bool> IsMatchOpen;
    private readonly CustomButton LeaveButton;
    private readonly int ScreenWidth;
    private readonly UILabel Sub;
    private readonly UILabel Title;

    public TcgQueueBar(int screenWidth, Func<bool> isMatchOpen)
    {
        Name = "TcgQueueBar";
        IsMatchOpen = isMatchOpen;
        ScreenWidth = screenWidth;
        Width = BAR_WIDTH;
        Height = SEARCH_HEIGHT;
        X = (screenWidth - BAR_WIDTH) / 2;
        Y = TOP;
        Visible = false;
        BackgroundColor = new Color(28, 20, 12);
        BorderColor = LegendColors.Gold;

        Title = new UILabel
        {
            X = PADDING,
            Y = 6,
            Width = BAR_WIDTH - (2 * PADDING),
            Height = 20,
            ForegroundColor = LegendColors.Gold,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false
        };

        Sub = new UILabel
        {
            X = PADDING,
            Y = 30,
            Width = BAR_WIDTH - (2 * PADDING),
            Height = 20,
            ForegroundColor = LegendColors.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false
        };

        LeaveButton = new CustomButton("Leave", BUTTON_WIDTH)
        {
            X = (BAR_WIDTH - BUTTON_WIDTH) / 2,
            Y = SEARCH_HEIGHT - CustomButton.HEIGHT - PADDING
        };

        AcceptButton = new CustomButton("Accept", BUTTON_WIDTH)
        {
            X = (BAR_WIDTH / 2) - BUTTON_WIDTH - 4,
            Y = READY_HEIGHT - CustomButton.HEIGHT - PADDING
        };

        DeclineButton = new CustomButton("Decline", BUTTON_WIDTH)
        {
            X = BAR_WIDTH / 2 + 4,
            Y = READY_HEIGHT - CustomButton.HEIGHT - PADDING
        };

        LeaveButton.Clicked += () => LeaveClicked?.Invoke();
        AcceptButton.Clicked += () => AcceptClicked?.Invoke();
        DeclineButton.Clicked += () => DeclineClicked?.Invoke();

        AddChild(Title);
        AddChild(Sub);
        AddChild(LeaveButton);
        AddChild(AcceptButton);
        AddChild(DeclineButton);
    }

    public event Action? AcceptClicked;
    public event Action? DeclineClicked;
    public event Action? LeaveClicked;

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        var ranked = WorldState.TcgRanked;

        Visible = (ranked.Mode != TcgQueueMode.Hidden) && !IsMatchOpen();

        if (!Visible)
            return;

        var now = DateTime.UtcNow;

        switch (ranked.Mode)
        {
            case TcgQueueMode.Searching:
                Height = SEARCH_HEIGHT;
                X = (ScreenWidth - BAR_WIDTH) / 2;
                Title.Text = ranked.QueueText(now);
                Sub.Visible = false;
                LeaveButton.Visible = true;
                AcceptButton.Visible = false;
                DeclineButton.Visible = false;

                break;

            case TcgQueueMode.ReadyCheck:
                Height = READY_HEIGHT;
                X = (ScreenWidth - BAR_WIDTH) / 2;
                Title.Text = "Ranked match found! Accept";
                Sub.Visible = true;
                Sub.Text = $"{ranked.SecondsLeft(now)} s";
                LeaveButton.Visible = false;
                AcceptButton.Visible = true;
                DeclineButton.Visible = true;

                break;

            default:
                Height = SEARCH_HEIGHT;
                X = (ScreenWidth - BAR_WIDTH) / 2;
                Title.Text = $"Waiting for opponent... {ranked.SecondsLeft(now)}";
                Sub.Visible = false;
                LeaveButton.Visible = false;
                AcceptButton.Visible = false;
                DeclineButton.Visible = false;

                break;
        }
    }
}
