#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg;

/// <summary>
///     The ranked queue bar at the top centre of the world screen: a 160x26 searching line with Leave, a 200x52
///     ready-check box with a countdown and Accept/Decline, or a 160x26 waiting line once accepted. Driven by
///     <see cref="TcgRankedState" />, hidden while the match window is open. Every button only asks: the server
///     answers with a fresh display.
/// </summary>
public sealed class TcgQueueBar : UIPanel
{
    private const int SEARCH_WIDTH = 160;
    private const int SEARCH_HEIGHT = 26;
    private const int READY_WIDTH = 200;
    private const int READY_HEIGHT = 52;
    private const int ACCEPT_WIDTH = 80;
    private const int BUTTON_GAP = 8;
    private const int BOTTOM = 4;
    private const int LEAVE_SIZE = 22;
    private const int PADDING = 8;
    private const int TOP = 4;

    private readonly CustomButton AcceptButton;
    private readonly CustomButton DeclineButton;
    private readonly Func<bool> IsMatchOpen;
    private readonly CustomButton LeaveButton;
    private readonly int ScreenWidth;
    private readonly UILabel Title;

    /// <summary>How far below the top of the screen the bar sits: the world screen raises it past the hall
    /// battle bar while both are visible, so the two never overlap.</summary>
    public int StackBelow { get; set; }

    public TcgQueueBar(int screenWidth, Func<bool> isMatchOpen)
    {
        Name = "TcgQueueBar";
        IsMatchOpen = isMatchOpen;
        ScreenWidth = screenWidth;
        Width = SEARCH_WIDTH;
        Height = SEARCH_HEIGHT;
        X = (screenWidth - SEARCH_WIDTH) / 2;
        Y = TOP;
        Visible = false;
        BackgroundColor = new Color(28, 20, 12);
        BorderColor = LegendColors.Gold;

        Title = new UILabel
        {
            X = PADDING,
            Y = (SEARCH_HEIGHT - 20) / 2,
            Width = SEARCH_WIDTH - (2 * PADDING),
            Height = 20,
            ForegroundColor = LegendColors.Gold,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsHitTestVisible = false
        };

        LeaveButton = new CustomButton("x", LEAVE_SIZE)
        {
            X = SEARCH_WIDTH - LEAVE_SIZE - 2,
            Y = 2
        };

        AcceptButton = new CustomButton("Accept", ACCEPT_WIDTH)
        {
            X = (READY_WIDTH - (ACCEPT_WIDTH + BUTTON_GAP + ACCEPT_WIDTH)) / 2,
            Y = READY_HEIGHT - CustomButton.HEIGHT - BOTTOM
        };

        DeclineButton = new CustomButton("Decline", ACCEPT_WIDTH)
        {
            X = ((READY_WIDTH - (ACCEPT_WIDTH + BUTTON_GAP + ACCEPT_WIDTH)) / 2) + ACCEPT_WIDTH + BUTTON_GAP,
            Y = READY_HEIGHT - CustomButton.HEIGHT - BOTTOM
        };

        LeaveButton.Clicked += () => LeaveClicked?.Invoke();
        AcceptButton.Clicked += () => AcceptClicked?.Invoke();
        DeclineButton.Clicked += () => DeclineClicked?.Invoke();

        AddChild(Title);
        AddChild(LeaveButton);
        AddChild(AcceptButton);
        AddChild(DeclineButton);
    }

    public event Action? AcceptClicked;
    public event Action? DeclineClicked;
    public event Action? LeaveClicked;

    //the last mode and text actually laid out: the bar only rebuilds its strings on change, not every frame
    private TcgQueueMode LastMode = TcgQueueMode.Hidden;
    private string LastText = string.Empty;

    /// <summary>
    ///     Called by the world screen every frame. Not done in Update: a parent panel never updates a hidden child,
    ///     so a hidden bar could never show itself.
    /// </summary>
    public void Sync()
    {
        var ranked = WorldState.TcgRanked;

        Visible = (ranked.Mode != TcgQueueMode.Hidden) && !IsMatchOpen();

        if (!Visible)
            return;

        var now = DateTime.UtcNow;
        var top = TOP + StackBelow;
        var text = ranked.Mode switch
        {
            TcgQueueMode.Searching => ranked.QueueText(now),
            TcgQueueMode.ReadyCheck => $"Match found! {ranked.SecondsLeft(now)} s",
            _ => $"Waiting... {ranked.SecondsLeft(now)}"
        };

        if ((ranked.Mode == LastMode) && string.Equals(text, LastText, StringComparison.Ordinal))
            return;

        LastMode = ranked.Mode;
        LastText = text;

        switch (ranked.Mode)
        {
            case TcgQueueMode.Searching:
                Width = SEARCH_WIDTH;
                Height = SEARCH_HEIGHT;
                X = (ScreenWidth - Width) / 2;
                Y = top;
                Title.Text = text;
                Title.X = PADDING;
                Title.Y = (Height - Title.Height) / 2;
                Title.Width = (Width - LEAVE_SIZE - 2) - PADDING - 4;
                Title.HorizontalAlignment = HorizontalAlignment.Left;
                LeaveButton.Visible = true;
                LeaveButton.X = Width - LEAVE_SIZE - 2;
                LeaveButton.Y = 2;
                AcceptButton.Visible = false;
                DeclineButton.Visible = false;

                break;

            case TcgQueueMode.ReadyCheck:
                Width = READY_WIDTH;
                Height = READY_HEIGHT;
                X = (ScreenWidth - Width) / 2;
                Y = top;
                Title.Text = text;
                Title.X = PADDING;
                Title.Y = 4;
                Title.Width = Width - (2 * PADDING);
                Title.HorizontalAlignment = HorizontalAlignment.Center;
                LeaveButton.Visible = false;
                AcceptButton.Visible = true;
                AcceptButton.X = (Width - (ACCEPT_WIDTH + BUTTON_GAP + ACCEPT_WIDTH)) / 2;
                AcceptButton.Y = Height - CustomButton.HEIGHT - BOTTOM;
                DeclineButton.Visible = true;
                DeclineButton.X = AcceptButton.X + ACCEPT_WIDTH + BUTTON_GAP;
                DeclineButton.Y = Height - CustomButton.HEIGHT - BOTTOM;

                break;

            default:
                Width = SEARCH_WIDTH;
                Height = SEARCH_HEIGHT;
                X = (ScreenWidth - Width) / 2;
                Y = top;
                Title.Text = text;
                Title.X = PADDING;
                Title.Y = (Height - Title.Height) / 2;
                Title.Width = Width - (2 * PADDING);
                Title.HorizontalAlignment = HorizontalAlignment.Center;
                LeaveButton.Visible = false;
                AcceptButton.Visible = false;
                DeclineButton.Visible = false;

                break;
        }
    }
}
