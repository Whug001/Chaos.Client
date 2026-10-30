#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Tcg;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg.Match;

public readonly record struct TcgHoverHit(int HandId, int CreatureId, string Key, TcgPrinting Printing);

/// <summary>
///     The full-screen tavern window for a card match. Draws the newest board (or the board an event animation is
///     showing) while timers, buttons and highlights always follow the newest Update. Clicks and drags become
///     <see cref="TcgActionArgs" /> in the Hand, Combat and Overlays partials.
/// </summary>
public sealed partial class TcgMatchWindow : UIPanel
{
    private readonly CreatureRenderer Creatures;
    private readonly TcgCollectionState Catalog;
    private readonly SoundSystem Sounds;
    private TcgEventView? LastSoundEvent;
    private int? LastTickSecond;
    private readonly CustomTextBox SayBox;

    private int HoverHandId;
    private int HoverCreatureId;
    private string? HoverKey;
    private TcgPrinting HoverPrinting;

    public TcgMatchWindow(CreatureRenderer creatures, TcgCollectionState catalog, SoundSystem sounds)
    {
        Creatures = creatures;
        Catalog = catalog;
        Sounds = sounds;
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
    public bool Spectating => State.Spectating;

    public void AcceptSpectatorLine(string from, string text)
    {
        if (string.IsNullOrEmpty(from))
            Chat?.AddNotice(text);
        else
            Chat?.AddSpectatorLine(from, text);
    }

    public event Action<TcgActionArgs>? Action;
    public event Action<string, string>? WhisperSent;
    public event Action? Closed;
    public event Action? SpectateStopped;

    public void ShowMatch(TcgDisplayArgs update)
    {
        var previous = State.Latest;
        var previousId = previous?.MatchId;
        var wasVisible = Visible;
        var isNew = !string.Equals(previousId, update.MatchId, StringComparison.Ordinal);

        if ((previous is null)
            || !string.Equals(previous.MatchId, update.MatchId, StringComparison.Ordinal)
            || (previous.Decision != update.Decision)
            || (previous.Decider != update.Decider))
            LastTickSecond = null;

        State.WrongManaNote = Input.WrongManaNote;
        State.Apply(update, DateTime.UtcNow, CardName);
        SayBox.Visible = !State.Spectating;

        if (State.Spectating)
            SayBox.IsFocused = false;

        if (isNew)
        {
            Queue.Clear(update);
            Chat = new TcgMatchChat(State.OpponentName);
        } else if (!wasVisible)
            Queue.Clear(update);
        else
        {
            Queue.Enqueue(update);
            PlayAppliedSounds(update);
        }

        Input.Sync(update);

        if (!Visible)
        {
            Visible = true;
            InputDispatcher.Instance?.PushControl(this);
        }
    }

    //only on a live update of an open match, so reopening the window or starting a match makes no burst of old sounds
    private void PlayAppliedSounds(TcgDisplayArgs update)
    {
        foreach (var e in update.Events)
            if (TcgSounds.ForAppliedEvent(e) is { } sound)
                Sounds.PlaySound(sound);
    }

    public void EndMatch(TcgDisplayArgs close)
    {
        //a stale close must not clear the showing match's queue or end it
        if ((State.Latest is not null) && !string.Equals(State.Latest.MatchId, close.MatchId, StringComparison.Ordinal))
            return;

        State.End(close, CardName);
        Queue.Clear(State.Latest);

        if (Visible && State.Ended is { } ended)
            Sounds.PlaySound(TcgSounds.ForOutcome(ended.Outcome));
    }

    public void Hide()
    {
        if (!Visible)
            return;

        var stopWatching = State.Spectating && (State.Ended is null);

        if (UsesControlStack)
            InputDispatcher.Instance?.RemoveControl(this);

        Visible = false;
        HoverHandId = 0;
        HoverCreatureId = 0;
        HoverKey = null;
        HoverPrinting = TcgPrinting.Normal;
        TurnBanner = null;
        TurnBannerBoard = null;
        TurnBannerSeenBoard = null;
        TurnBannerElapsedMs = 0;
        LastSoundEvent = null;
        LastTickSecond = null;
        State.Reset();
        Queue.Clear(null);
        Input.Cancel();
        Closed?.Invoke();

        if (stopWatching)
            SpectateStopped?.Invoke();
    }

    public override void Update(GameTime gameTime)
    {
        Queue.Advance(gameTime.ElapsedGameTime.TotalMilliseconds);
        PlayQueuedSound();
        SyncDecisionTick();
        SyncTurnBanner(gameTime.ElapsedGameTime.TotalMilliseconds);
        SyncCardMenu();
        SyncResponseBigCard();

        base.Update(gameTime);
    }

    //TcgEventView is a record, so equal events in a row still differ by reference; events skipped by catch-up are never Current and stay silent
    private void PlayQueuedSound()
    {
        var current = Queue.Current;

        if (ReferenceEquals(current, LastSoundEvent))
            return;

        LastSoundEvent = current;

        if (Visible && (current is not null) && TcgSounds.ForQueuedEvent(current, Catalog.Get) is { } sound)
            Sounds.PlaySound(sound);
    }

    //one tick per second for the last five seconds of the viewer's own decision, never for watchers or the opponent
    private void SyncDecisionTick()
    {
        if (!Visible || (State.Latest is null))
            return;

        //the match is over: the end banner is up and no decision can still be ticking
        if (State.Ended is not null)
            return;

        var left = State.DecisionSecondsLeft(DateTime.UtcNow);

        if (TcgSounds.ShouldTickDecision(State.Latest, left, LastTickSecond))
        {
            Sounds.PlaySound(TcgSounds.COUNTDOWN_TICK);
            LastTickSecond = left;
        }
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

        if ((board is not null) && (board.Stack.Count > 0))
            DrawStack(spriteBatch, board);

        DrawEventAnimation(spriteBatch);
        DrawTurnBanner(spriteBatch);
        DrawColumn(spriteBatch);
        DrawTargeting(spriteBatch);
        if (!State.Spectating)
            DrawConcede(spriteBatch);

        if (!State.Spectating)
            DrawRedraw(spriteBatch);
        DrawHintBanner(spriteBatch);
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
        HoverPrinting = TcgPrinting.Normal;

        if ((Queue.ShownBoard ?? State.Latest) is { } board)
            HoverBoard(board, x, y);

        var pick = BigCardPick(HoverKey, HoverPrinting, State.Latest, State.Spectating);
        ShowBigCard(pick?.Key, pick?.Printing ?? HoverPrinting);
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (State.Spectating)
        {
            if (e.Keycode == Keycode.Escape)
                Hide();
            else if (e.Keycode != Keycode.Enter)
            {
                base.OnKeyDown(e);

                return;
            }

            e.Handled = true;

            return;
        }

        if (e.Keycode == Keycode.Escape)
        {
            if (SayBox.IsFocused)
            {
                SayBox.IsFocused = false;

                if (InputDispatcher.Instance?.ExplicitFocus == SayBox)
                    InputDispatcher.Instance.ClearExplicitFocus();
            } else
                Input.Cancel();

            e.Handled = true;

            return;
        }

        if (e.Keycode == Keycode.Enter)
        {
            if (SayBox.IsFocused && (SayBox.Text.Length > 0))
            {
                var send = TcgChatCommand.Parse(SayBox.Text, OpponentName, WorldState.Chat.RecentWhisperSenders, State.Latest?.Practice ?? false);

                if (send.Notice is not null)
                    Chat?.AddNotice(send.Notice);
                else if (send.Target is not null)
                    WhisperSent?.Invoke(send.Target, send.Message);
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
        var hit = HoverAt(board, Input, State.Spectating, x, y);

        //in targeting mode a creature answering the spell wins over the stack row's preview
        if ((Input.Mode == TcgInputMode.Targeting)
            && (hit is { } answering)
            && ((answering.HandId != 0) || Input.Targets.Contains(answering.CreatureId)))
        {
            HoverHandId = answering.HandId;
            HoverCreatureId = answering.CreatureId;
            HoverKey = answering.Key;
            HoverPrinting = answering.Printing;

            return;
        }

        if ((board.Stack.Count > 0) && (TcgMatchLayout.StackHitAt(x, y, board.Stack.Count) is { } display))
        {
            var item = board.Stack[board.Stack.Count - 1 - display];
            HoverHandId = 0;
            HoverCreatureId = 0;
            HoverKey = item.Key;
            HoverPrinting = item.Printing;

            return;
        }

        if (hit is not { } found)
            return;

        HoverHandId = found.HandId;
        HoverCreatureId = found.CreatureId;
        HoverKey = found.Key;
        HoverPrinting = found.Printing;
    }

    public static TcgHoverHit? HoverAt(TcgDisplayArgs board, TcgMatchInput input, bool spectating, int x, int y)
    {
        if (board.Players.Count < 2)
            return null;

        //spectators get no hand from the server, and a stale one must never show
        if (!spectating)
        {
            var you = board.Players[(int)board.YourSeat];

            for (var i = you.Hand.Count - 1; i >= 0; i--)
            {
                var slot = TcgMatchLayout.HandCard(i, you.Hand.Count);

                if (new Rectangle(slot.X, slot.Y, TcgCardFace.WIDTH, TcgCardFace.HEIGHT).Contains(x, y))
                    return new TcgHoverHit(you.Hand[i].Id, 0, you.Hand[i].Key, you.Hand[i].Printing);
            }
        }

        return HoverCreatures(board, input, board.Players[(int)board.YourSeat].Battlefield, true, x, y)
               ?? HoverCreatures(board, input, board.Players[(int)TcgMatchRules.Other(board.YourSeat)].Battlefield, false, x, y);
    }

    private static TcgHoverHit? HoverCreatures(TcgDisplayArgs board, TcgMatchInput input, List<TcgCreatureView> creatures, bool yours, int x, int y)
    {
        for (var i = creatures.Count - 1; i >= 0; i--)
        {
            var feet = DrawFeet(board, input, creatures[i].Id, yours, i, creatures.Count);

            if (new Rectangle(feet.X - 24, feet.Y - 48, 48, 48).Contains(x, y))
                return new TcgHoverHit(0, creatures[i].Id, creatures[i].Key, creatures[i].Printing);
        }

        return null;
    }
}
