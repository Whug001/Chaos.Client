#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.HallBattle;

/// <summary>
///     A hall battle offer: a challenge to a guild leader (guild, size, ranked or friendly, wager, a forming-mode choice,
///     Accept and Decline) or an invite to a picked member (Join and Decline). Closes itself when the seconds run out.
/// </summary>
public sealed class HallBattleOfferWindow : UIPanel
{
    private const int BOX_HEIGHT = 190;
    private const int BOX_WIDTH = 340;
    private const int PADDING = 10;
    private const int ROW = 26;

    private readonly CustomButton AcceptButton;
    private readonly UILabel Countdown;
    private readonly CustomButton DeclineButton;
    private readonly UILabel Details;
    private readonly CustomButton HandPickButton;
    private readonly CustomButton OpenInviteButton;
    private readonly UILabel Title;
    private bool Challenge;
    private bool OpenInvite;
    private DateTime ReceivedAt;
    private int Seconds;

    public HallBattleOfferWindow(int screenWidth, int screenHeight)
    {
        Name = "HallBattleOffer";
        Width = BOX_WIDTH;
        Height = BOX_HEIGHT;
        X = (screenWidth - BOX_WIDTH) / 2;
        Y = (screenHeight - BOX_HEIGHT) / 2;
        Visible = false;
        UsesControlStack = true;
        BackgroundColor = new Color(28, 20, 12);
        BorderColor = LegendColors.Gold;

        Title = Label(PADDING, LegendColors.Gold);
        Details = Label(PADDING + ROW, LegendColors.White);
        Countdown = Label(PADDING + (2 * ROW), LegendColors.White);

        HandPickButton = new CustomButton("Hand pick", 140) { X = PADDING, Y = PADDING + (3 * ROW) };
        OpenInviteButton = new CustomButton("Open invite", 140) { X = BOX_WIDTH - PADDING - 140, Y = PADDING + (3 * ROW) };
        AcceptButton = new CustomButton("Accept", 92) { X = BOX_WIDTH - PADDING - 192, Y = BOX_HEIGHT - CustomButton.HEIGHT - PADDING };
        DeclineButton = new CustomButton("Decline", 92) { X = BOX_WIDTH - PADDING - 92, Y = BOX_HEIGHT - CustomButton.HEIGHT - PADDING };

        HandPickButton.Clicked += () => ChooseMode(false);
        OpenInviteButton.Clicked += () => ChooseMode(true);
        AcceptButton.Clicked += Accept;
        DeclineButton.Clicked += Decline;

        foreach (var control in new UIElement[] { Title, Details, Countdown, HandPickButton, OpenInviteButton, AcceptButton, DeclineButton })
            AddChild(control);
    }

    public event Action<HallBattleActionArgs>? Action;

    public void Close()
    {
        if (UsesControlStack)
            InputDispatcher.Instance?.RemoveControl(this);

        Visible = false;
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            //Escape only closes the window: no Decline goes out, and the offer can still expire server-side
            Close();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public void ShowOffer(HallBattleDisplayArgs args)
    {
        Challenge = args.OfferKind == HallBattleOfferKind.Challenge;
        Seconds = args.SecondsLeft;
        ReceivedAt = DateTime.UtcNow;
        OpenInvite = false;

        Title.Text = Challenge
            ? $"{args.OfferGuild} challenges your guild to a hall battle"
            : $"Join the hall battle against {args.OfferGuild}";

        Details.Text = DetailsText(args);
        AcceptButton.Caption = Challenge ? "Accept" : "Join";
        HandPickButton.Visible = Challenge;
        OpenInviteButton.Visible = Challenge;
        ChooseMode(false);

        if (!Visible && UsesControlStack)
            InputDispatcher.Instance?.PushControl(this);

        Visible = true;
        Countdown.Text = $"{Seconds} s";
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        var left = Seconds - (int)(DateTime.UtcNow - ReceivedAt).TotalSeconds;

        if (left <= 0)
        {
            Close();

            return;
        }

        Countdown.Text = $"{left} s";
    }

    public static string DetailsText(HallBattleDisplayArgs args)
    {
        var kind = args.Ranked ? "Ranked" : "Friendly";
        var wager = args.Ranked && (args.Wager > 0) ? $", wager {args.Wager:N0} gold each" : string.Empty;

        return $"{args.TeamSize} v {args.TeamSize}, {kind}{wager}";
    }

    private void Accept()
    {
        Action?.Invoke(
            Challenge
                ? new HallBattleActionArgs
                {
                    Action = HallBattleActionKind.AnswerChallenge,
                    Accept = true,
                    Mode = (byte)(OpenInvite ? WarRoomState.FORMING_OPEN_INVITE : WarRoomState.FORMING_HAND_PICK)
                }
                : new HallBattleActionArgs { Action = HallBattleActionKind.AnswerInvite, Accept = true });

        Close();
    }

    private void ChooseMode(bool openInvite)
    {
        OpenInvite = openInvite;
        HandPickButton.Selected = !openInvite;
        OpenInviteButton.Selected = openInvite;
    }

    private void Decline()
    {
        Action?.Invoke(
            new HallBattleActionArgs
            {
                Action = Challenge ? HallBattleActionKind.AnswerChallenge : HallBattleActionKind.AnswerInvite,
                Accept = false
            });

        Close();
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
}
