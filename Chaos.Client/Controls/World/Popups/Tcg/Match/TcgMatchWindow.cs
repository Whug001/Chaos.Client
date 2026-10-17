#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Tcg;
using Chaos.Client.Rendering;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg.Match;

/// <summary>
///     The full-screen tavern window for a card match. Draws the newest board (or the board an event animation is
///     showing) while timers, buttons and highlights always follow the newest Update. Clicks and drags become
///     <see cref="TcgActionArgs" /> in the Hand, Combat and Overlays partials.
/// </summary>
public sealed partial class TcgMatchWindow : UIPanel
{
    private readonly CreatureRenderer Creatures;
    private readonly TcgCollectionState Catalog;
    private readonly CustomTextBox SayBox;

    private int HoverHandId;
    private int HoverCreatureId;
    private string? HoverKey;

    public TcgMatchWindow(CreatureRenderer creatures, TcgCollectionState catalog)
    {
        Creatures = creatures;
        Catalog = catalog;
        X = 0;
        Y = 0;
        Width = 640;
        Height = 480;
        ZIndex = 60;
        IsModal = true;
        UsesControlStack = true;
        Visible = false;

        State = new TcgMatchState();
        Queue = new TcgMatchEventQueue();
        Input = new TcgMatchInput(catalog.Get);
        Creature = new TcgBoardCreature(creatures);

        BigCard = new TcgCardFace(creatures, 2)
        {
            Name = "TcgBigCard",
            X = TcgMatchLayout.BigCard.X,
            Y = TcgMatchLayout.BigCard.Y,
            Animate = true,
            IsHitTestVisible = false
        };

        SayBox = new CustomTextBox
        {
            Name = "TcgSay",
            X = TcgMatchLayout.ChatBox.X,
            Y = TcgMatchLayout.ChatBox.Y,
            Width = TcgMatchLayout.ChatBox.Width,
            Height = TcgMatchLayout.ChatBox.Height,
            MaxLength = 80,
            ForegroundColor = Color.White
        };

        AddChild(BigCard);
        AddChild(SayBox);

        PlayButton = new CustomButton("Play", MENU_BUTTON_WIDTH)
        {
            Name = "TcgPlay",
            ZIndex = 5,
            Visible = false
        };
        ManaButton = new CustomButton("Mana", MENU_BUTTON_WIDTH)
        {
            Name = "TcgMana",
            ZIndex = 5,
            Visible = false
        };
        YesButton = new CustomButton("Yes", MENU_BUTTON_WIDTH)
        {
            Name = "TcgConcedeYes",
            ZIndex = 5,
            Visible = false
        };
        NoButton = new CustomButton("No", MENU_BUTTON_WIDTH)
        {
            Name = "TcgConcedeNo",
            ZIndex = 5,
            Visible = false
        };
        DragFace = new TcgCardFace(creatures, 1);

        PlayButton.Clicked += () =>
        {
            if (State.Latest is { } board)
                Raise(Input.ChoosePlay(board));
        };
        ManaButton.Clicked += () => Raise(Input.ChooseMana());
        YesButton.Clicked += () => Raise(Input.ConfirmConcede());
        NoButton.Clicked += () => Input.Cancel();

        AddChild(PlayButton);
        AddChild(ManaButton);
        AddChild(YesButton);
        AddChild(NoButton);
    }

    public TcgMatchState State { get; }
    public TcgMatchEventQueue Queue { get; }
    public TcgMatchInput Input { get; }
    public TcgMatchChat? Chat { get; private set; }
    public TcgCardFace BigCard { get; }
    public TcgBoardCreature Creature { get; }
    public string OpponentName => State.OpponentName;

    public event Action<TcgActionArgs>? Action;
    public event Action<string>? WhisperSent;
    public event Action? Closed;

    public void ShowMatch(TcgDisplayArgs update)
    {
        var previousId = State.Latest?.MatchId;
        var wasVisible = Visible;
        var isNew = !string.Equals(previousId, update.MatchId, StringComparison.Ordinal);
        State.Apply(update, DateTime.UtcNow, CardName);

        if (isNew)
        {
            Queue.Clear(update);
            Chat = new TcgMatchChat(State.OpponentName);
        } else if (!wasVisible)
            Queue.Clear(update);
        else
            Queue.Enqueue(update);

        Input.Sync(update);

        if (!Visible)
        {
            Visible = true;
            InputDispatcher.Instance?.PushControl(this);
        }
    }

    public void EndMatch(TcgDisplayArgs close)
    {
        State.End(close, CardName);
        Queue.Clear(State.Latest);
    }

    public void Hide()
    {
        if (!Visible)
            return;

        if (UsesControlStack)
            InputDispatcher.Instance?.RemoveControl(this);

        Visible = false;
        HoverHandId = 0;
        HoverCreatureId = 0;
        HoverKey = null;
        State.Reset();
        Queue.Clear(null);
        Input.Cancel();
        Closed?.Invoke();
    }

    public override void Update(GameTime gameTime)
    {
        Queue.Advance(gameTime.ElapsedGameTime.TotalMilliseconds);
        SyncCardMenu();
        SyncResponseBigCard();

        base.Update(gameTime);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        var board = Queue.ShownBoard ?? State.Latest;

        AnimatedIds.Clear();

        foreach (var id in AnimatedIdsFor(Queue.Current))
            AnimatedIds.Add(id);

        DestroyedIds.Clear();
        FreshIds.Clear();
        SettledIds(Queue.PastEvents, DestroyedIds, FreshIds);

        DrawBackground(spriteBatch);

        if (board is not null)
            DrawBoard(spriteBatch, board, State.Latest);

        DrawEventAnimation(spriteBatch);
        DrawColumn(spriteBatch);
        DrawTargeting(spriteBatch);
        DrawConcede(spriteBatch);
        DrawRedraw(spriteBatch);
        DrawEndBanner(spriteBatch);

        base.Draw(spriteBatch);

        DrawDragCard(spriteBatch);
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        var x = e.ScreenX - ScreenX;
        var y = e.ScreenY - ScreenY;

        HoverHandId = 0;
        HoverCreatureId = 0;
        HoverKey = null;

        if ((Queue.ShownBoard ?? State.Latest) is { } board)
            HoverBoard(board, x, y);

        if (IsResponseShowing)
            return;

        BigCard.Card = HoverKey is not null ? Catalog.Get(HoverKey) : null;
        BigCard.Foil = State.Latest is not null && IsFoilAnte(State.Latest, HoverKey);
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Input.Cancel();
            e.Handled = true;

            return;
        }

        if (e.Keycode == Keycode.Enter)
        {
            if (SayBox.IsFocused && (SayBox.Text.Length > 0))
            {
                WhisperSent?.Invoke(SayBox.Text);
                SayBox.Text = string.Empty;
            } else
                SayBox.IsFocused = true;

            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public string CardName(string key) => Catalog.Get(key)?.Name ?? key;

    private void HoverBoard(TcgDisplayArgs board, int x, int y)
    {
        if (board.Players.Count < 2)
            return;

        var you = board.Players[(int)board.YourSeat];

        for (var i = you.Hand.Count - 1; i >= 0; i--)
        {
            var slot = TcgMatchLayout.HandCard(i, you.Hand.Count);

            if (new Rectangle(slot.X, slot.Y, TcgCardFace.WIDTH, TcgCardFace.HEIGHT).Contains(x, y))
            {
                HoverHandId = you.Hand[i].Id;
                HoverKey = you.Hand[i].Key;

                return;
            }
        }

        if (HoverCreatures(board, board.Players[(int)board.YourSeat].Battlefield, true, x, y))
            return;

        HoverCreatures(board, board.Players[(int)TcgMatchRules.Other(board.YourSeat)].Battlefield, false, x, y);
    }

    private bool HoverCreatures(TcgDisplayArgs board, List<TcgCreatureView> creatures, bool yours, int x, int y)
    {
        for (var i = creatures.Count - 1; i >= 0; i--)
        {
            var feet = DrawFeet(board, Input, creatures[i].Id, yours, i, creatures.Count);

            if (new Rectangle(feet.X - 24, feet.Y - 48, 48, 48).Contains(x, y))
            {
                HoverCreatureId = creatures[i].Id;
                HoverKey = creatures[i].Key;

                return true;
            }
        }

        return false;
    }
}
