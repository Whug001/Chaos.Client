#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Tcg;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg.Match;

public enum TcgDropZone : byte
{
    None,
    Battlefield,
    Mana
}

/// <summary>Hand row, card menu, hand drag, targeting, turn buttons and the concede confirm.</summary>
public sealed partial class TcgMatchWindow
{
    private const int HOVER_LIFT = 8;
    private const int MENU_BUTTON_WIDTH = 56;
    private const int MENU_BUTTON_GAP = 4;
    private const int BUTTON_HEIGHT = 20;
    private const int BUTTON_STEP = 24;

    private static readonly Rectangle ConcedeBox = new(305, 288, 160, 80);
    private static readonly Color DisabledText = new(120, 120, 120);

    private readonly List<TcgCardFace> HandFaces = [];
    private readonly CustomButton PlayButton;
    private readonly CustomButton ManaButton;
    private readonly CustomButton YesButton;
    private readonly CustomButton NoButton;
    private readonly TcgCardFace DragFace;

    private enum TurnButton
    {
        Attack,
        End,
        Pass,
        Done,
        Confirm,
        Cancel,
        Keep
    }

    public static int HandCardIdAt(TcgDisplayArgs board, int x, int y)
    {
        if (board.Players.Count < 2)
            return 0;

        var hand = board.Players[(int)board.YourSeat].Hand;

        for (var i = hand.Count - 1; i >= 0; i--)
        {
            var slot = TcgMatchLayout.HandCard(i, hand.Count);

            if (new Rectangle(slot.X, slot.Y, TcgCardFace.WIDTH, TcgCardFace.HEIGHT).Contains(x, y))
                return hand[i].Id;
        }

        return 0;
    }

    public static TcgDropZone DropZoneAt(int x, int y)
    {
        if (TcgMatchLayout.Battlefield.Contains(x, y))
            return TcgDropZone.Battlefield;

        return TcgMatchLayout.YourGems.Contains(x, y) ? TcgDropZone.Mana : TcgDropZone.None;
    }

    public static List<string> TurnButtonLabels(TcgDisplayArgs latest)
        => TurnButtons(latest).Select(button => button.Label).ToList();

    public static bool AttackEnabled(TcgDisplayArgs latest)
    {
        if ((latest.Players.Count < 2) || (latest.Phase != TcgPhase.Main1) || !TcgMatchRules.YouDecide(latest, TcgDecision.Main))
            return false;

        return latest.Players[(int)latest.YourSeat].Battlefield.Any(creature => TcgMatchRules.CanAttack(latest, creature));
    }

    private static List<(TurnButton Button, string Label, bool Enabled)> TurnButtons(TcgDisplayArgs latest)
    {
        if (latest.Players.Count < 2)
            return [];

        if (TcgMatchRules.YouDecide(latest, TcgDecision.Block))
            return [(TurnButton.Done, "Done", true)];

        if (TcgMatchRules.YouDecide(latest, TcgDecision.DamageOrder))
            return [(TurnButton.Done, "Done", true)];

        //both seats redraw on their own copy: the server addresses the Update to the first seat
        if ((latest.Phase == TcgPhase.Redraw) && (latest.Decision == TcgDecision.Redraw))
            return [(TurnButton.Done, "Keep", true)];

        if (TcgMatchRules.YouDecide(latest, TcgDecision.Main))
            return latest.Phase == TcgPhase.Main1
                ? [(TurnButton.Attack, "Attack", AttackEnabled(latest)), (TurnButton.End, "End phase", true)]
                : [(TurnButton.End, "End turn", true)];

        if (TcgMatchRules.YouDecide(latest, TcgDecision.Respond))
            return [(TurnButton.Pass, "Pass", true)];

        return [];
    }

    private void DrawHand(SpriteBatch batch, TcgDisplayArgs board, TcgPlayerView you)
    {
        while (HandFaces.Count < you.Hand.Count)
            HandFaces.Add(new TcgCardFace(Creatures, 1));

        var dragged = DraggingHandCard();
        var latest = State.Latest;

        for (var i = 0; i < you.Hand.Count; i++)
        {
            var view = you.Hand[i];

            if (dragged == view.Id)
                continue;

            var slot = TcgMatchLayout.HandCard(i, you.Hand.Count);
            var face = HandFaces[i];
            face.Card = Catalog.Get(view.Key);
            face.Foil = false;
            face.FaceDown = false;
            face.FlipProgress = 1f;
            face.Badge = string.Empty;
            face.Owned = latest is null || Playable(latest, view);
            face.Selected = (Input.SelectedCard == view.Id) && (Input.Mode == TcgInputMode.CardMenu);
            face.X = slot.X;
            face.Y = slot.Y - (HoverHandId == view.Id ? HOVER_LIFT : 0);

            var at = new Rectangle(ScreenX + slot.X, ScreenY + face.Y, TcgCardFace.WIDTH, TcgCardFace.HEIGHT);
            DrawRectClipped(batch, at, face.Card is null || !face.Owned ? DisabledBack : new Color(40, 28, 16));
            face.Draw(batch);

            if ((HoverHandId == view.Id) && !face.Selected)
                UIElement.DrawBorder(batch, at, Color.White);

            if ((face.Card is not null) && !TcgCardFace.ArtPresent())
                DrawHandFallback(batch, face.Card, at, face.Owned ? Color.White : Color.Gray);
        }
    }

    private void DrawHandFallback(SpriteBatch batch, TcgCatalogCardView card, Rectangle at, Color tint)
    {
        DrawTextClipped(batch, new Vector2(at.X + 2, at.Y + 2), Fit(card.Name, at.Width - 14), tint, false);

        var cost = card.TotalCost.ToString();
        DrawTextClipped(batch, new Vector2(at.Right - 2 - TextRenderer.MeasureWidth(cost), at.Y + 2), cost, tint, false);
    }

    private bool Playable(TcgDisplayArgs latest, TcgCardView view)
    {
        var choice = TcgMatchRules.Play(latest, view, Catalog.Get, State.ManaPlacedThisTurn);

        return choice.CanPlay || choice.CanMana;
    }

    private static int? DraggingHandCard()
        => InputDispatcher.Instance is { } dispatcher && dispatcher.IsDragging && (dispatcher.ActiveDragPayload is int id) ? id : null;

    private List<(TurnButton Button, string Label, bool Enabled)> ActiveButtons(TcgDisplayArgs latest)
    {
        if (latest.Players.Count < 2)
            return [];

        switch (Input.Mode)
        {
            case TcgInputMode.Attacking:
            case TcgInputMode.Blocking:
            case TcgInputMode.Ordering:
                return [
                    (TurnButton.Confirm, "Confirm", ConfirmEnabled(latest, Input, Catalog.Get)),
                    (TurnButton.Cancel, "Cancel", true)
                ];
            case TcgInputMode.Redraw:
                return State.NeedsRedraw ? [(TurnButton.Keep, "Keep", true)] : [];
            default:
                return TurnButtons(latest);
        }
    }

    private void DrawButtons(SpriteBatch batch)
    {
        if (State.Latest is not { } latest)
            return;

        var buttons = ActiveButtons(latest);
        var passHighlighted = (latest.Decision == TcgDecision.Respond) && TcgMatchRules.YouDecide(latest, TcgDecision.Respond);

        for (var i = 0; i < buttons.Count; i++)
        {
            var at = ButtonRect(i);
            UIElement.DrawBorderedRect(batch, at, BoxBack, LegendColors.Gold);

            var x = at.X + ((at.Width - TextRenderer.MeasureWidth(buttons[i].Label)) / 2);
            var tint = !buttons[i].Enabled ? DisabledText : (passHighlighted && (buttons[i].Label == "Pass") ? LegendColors.Gold : Color.White);
            DrawTextClipped(batch, new Vector2(x, at.Y + 4), buttons[i].Label, tint, false);
        }

        var concede = At(TcgMatchLayout.Concede);
        UIElement.DrawBorderedRect(batch, concede, BoxBack, LegendColors.Gold);
        DrawTextClipped(batch, new Vector2(concede.X + 4, concede.Y + 2), "X", Color.White, false);
    }

    private Rectangle ButtonRect(int index)
        => new(
            ScreenX + TcgMatchLayout.Buttons.X,
            ScreenY + TcgMatchLayout.Buttons.Y + (index * BUTTON_STEP),
            TcgMatchLayout.Buttons.Width,
            BUTTON_HEIGHT);

    private void DrawConcede(SpriteBatch batch)
    {
        if (Input.Mode != TcgInputMode.ConfirmConcede)
            return;

        var at = new Rectangle(ScreenX + ConcedeBox.X, ScreenY + ConcedeBox.Y, ConcedeBox.Width, ConcedeBox.Height);
        UIElement.DrawBorderedRect(batch, at, BoxBack, LegendColors.Gold);

        const string question = "Concede the match?";
        var x = at.X + ((at.Width - TextRenderer.MeasureWidth(question)) / 2);
        DrawTextClipped(batch, new Vector2(x, at.Y + 8), question, Color.White, false);
    }

    private void DrawTargeting(SpriteBatch batch)
    {
        if ((Input.Mode != TcgInputMode.Targeting) || (State.Latest is not { } latest))
            return;

        var board = Queue.ShownBoard ?? latest;

        if (board.Players.Count < 2)
            return;

        var pulse = 0.55f + (0.45f * MathF.Sin(Environment.TickCount64 / 240f));
        var gold = LegendColors.Gold * pulse;

        foreach (var rect in Input.Targets.Select(id => TargetRect(board, id)))
        {
            if (rect is not { } local)
                continue;

            var at = new Rectangle(ScreenX + local.X, ScreenY + local.Y, local.Width, local.Height);
            UIElement.DrawBorder(batch, at, gold);
            at.Inflate(-1, -1);

            if ((at.Width > 0) && (at.Height > 0))
                UIElement.DrawBorder(batch, at, gold);
        }
    }

    private static Rectangle? TargetRect(TcgDisplayArgs board, int id)
    {
        if (id < 0)
            return id == TcgMatchRules.PlayerTarget(board.YourSeat) ? TcgMatchLayout.YourSeal : TcgMatchLayout.OpponentSeal;

        foreach (var yours in new[] { true, false })
        {
            var creatures = yours
                ? board.Players[(int)board.YourSeat].Battlefield
                : board.Players[(int)TcgMatchRules.Other(board.YourSeat)].Battlefield;

            for (var i = 0; i < creatures.Count; i++)
            {
                if (creatures[i].Id != id)
                    continue;

                var feet = TcgMatchLayout.CreatureSlot(i, creatures.Count, yours);

                return new Rectangle(feet.X - 24, feet.Y - 48, 48, 48);
            }
        }

        return null;
    }

    private int? TargetAt(TcgDisplayArgs board, int x, int y)
    {
        foreach (var id in Input.Targets)
        {
            if (TargetRect(board, id) is { } rect && rect.Contains(x, y))
                return id;
        }

        return null;
    }

    private void DrawDragCard(SpriteBatch batch)
    {
        if ((DraggingHandCard() is not { } id) || (State.Latest is not { } latest) || (latest.Players.Count < 2))
            return;

        if (latest.Players[(int)latest.YourSeat].Hand.Find(card => card.Id == id) is not { } view)
            return;

        DragFace.Card = Catalog.Get(view.Key);
        DragFace.Foil = false;
        DragFace.FaceDown = false;
        DragFace.FlipProgress = 1f;
        DragFace.Badge = string.Empty;
        DragFace.Owned = true;
        DragFace.Selected = false;
        DragFace.X = InputBuffer.MouseX - (TcgCardFace.WIDTH / 2);
        DragFace.Y = InputBuffer.MouseY - (TcgCardFace.HEIGHT / 2);
        DragFace.Draw(batch);
    }

    private void SyncCardMenu()
    {
        var latest = Visible ? State.Latest : null;
        var index = -1;

        if ((latest is not null) && (latest.Players.Count >= 2) && (Input.Mode == TcgInputMode.CardMenu))
            index = latest.Players[(int)latest.YourSeat].Hand.FindIndex(card => card.Id == Input.SelectedCard);

        var show = index >= 0;
        PlayButton.Visible = show;
        ManaButton.Visible = show;

        var concede = Visible && (Input.Mode == TcgInputMode.ConfirmConcede);
        YesButton.Visible = concede;
        NoButton.Visible = concede;

        if (concede)
        {
            YesButton.X = ConcedeBox.X + 10;
            YesButton.Y = ConcedeBox.Y + ConcedeBox.Height - CustomButton.HEIGHT - 8;
            NoButton.X = ConcedeBox.Right - 10 - MENU_BUTTON_WIDTH;
            NoButton.Y = YesButton.Y;
        }

        if (!show || (latest is null))
            return;

        var hand = latest.Players[(int)latest.YourSeat].Hand;
        var view = hand[index];
        var choice = TcgMatchRules.Play(latest, view, Catalog.Get, State.ManaPlacedThisTurn);

        PlayButton.Caption = Catalog.Get(view.Key)?.Type == TcgCardType.Trap ? "Set" : "Play";
        PlayButton.Enabled = choice.CanPlay;
        ManaButton.Enabled = choice.CanMana;

        var slot = TcgMatchLayout.HandCard(index, hand.Count);
        var total = (2 * MENU_BUTTON_WIDTH) + MENU_BUTTON_GAP;
        var left = Math.Clamp(slot.X + ((TcgCardFace.WIDTH - total) / 2), 0, Width - total);

        PlayButton.X = left;
        PlayButton.Y = slot.Y - CustomButton.HEIGHT - 4;
        ManaButton.X = left + MENU_BUTTON_WIDTH + MENU_BUTTON_GAP;
        ManaButton.Y = PlayButton.Y;
    }

    private void Raise(TcgActionArgs? action)
    {
        if (action is not null)
            Action?.Invoke(action);
    }

    private void PressTurnButton(TurnButton button, TcgDisplayArgs latest)
    {
        switch (button)
        {
            case TurnButton.Attack:
                if (AttackEnabled(latest))
                    Input.StartAttack(latest);

                break;
            case TurnButton.End:
                Raise(new TcgActionArgs { Type = TcgActionType.EndPhase });

                break;
            case TurnButton.Pass:
                if (TcgMatchRules.YouDecide(latest, TcgDecision.Respond))
                    Raise(new TcgActionArgs { Type = TcgActionType.Pass });

                break;
            case TurnButton.Done:
                var action = Input.Confirm(latest);

                if ((action is not null) && (action.Type == TcgActionType.Redraw))
                    State.MarkRedrawSent();

                Raise(action);

                break;
            case TurnButton.Confirm:
                Raise(Input.Confirm(latest));

                break;
            case TurnButton.Cancel:
                Input.Cancel();

                break;
            case TurnButton.Keep:
                var redraw = Input.Confirm(latest);

                if ((redraw is not null) && (redraw.Type == TcgActionType.Redraw))
                    State.MarkRedrawSent();

                Raise(redraw);

                break;
        }
    }

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        //the end banner leaves only Close and the chat box alive
        if (State.Ended is not null)
        {
            if (CloseAt.Contains(e.ScreenX - ScreenX, e.ScreenY - ScreenY))
                Hide();
            else if (!SayBox.ScreenBounds.Contains(e.ScreenX, e.ScreenY))
                e.Handled = true;

            return;
        }

        var x = e.ScreenX - ScreenX;
        var y = e.ScreenY - ScreenY;

        if (Input.Mode == TcgInputMode.ConfirmConcede)
        {
            e.Handled = true;

            return;
        }

        var latest = State.Latest;

        if ((Input.Mode == TcgInputMode.Targeting) && (latest is not null))
        {
            if (TargetAt(Queue.ShownBoard ?? latest, x, y) is { } target)
                Raise(Input.ChooseTarget(target));

            e.Handled = true;

            return;
        }

        var board = Queue.ShownBoard ?? latest;

        if ((board is not null) && (latest is not null) && (HandleCombatClick(board, latest, x, y) || (HandleRedrawClick(x, y))))
        {
            e.Handled = true;

            return;
        }

        //the redraw overlay hides the hand row: its clicks belong to the overlay, handled above
        if ((board is not null)
            && (latest is not null)
            && (latest.Phase != TcgPhase.Redraw)
            && !State.NeedsRedraw
            && (HandCardIdAt(board, x, y) is var id && (id != 0)))
        {
            Input.ClickHandCard(latest, id);
            e.Handled = true;

            return;
        }

        if (latest is not null)
        {
            var buttons = ActiveButtons(latest);

            for (var i = 0; i < buttons.Count; i++)
            {
                if (!ButtonRect(i).Contains(e.ScreenX, e.ScreenY))
                    continue;

                if (buttons[i].Enabled)
                    PressTurnButton(buttons[i].Button, latest);

                e.Handled = true;

                return;
            }

            if (At(TcgMatchLayout.Concede).Contains(e.ScreenX, e.ScreenY))
            {
                Input.AskConcede();
                e.Handled = true;

                return;
            }
        }

        e.Handled = true;
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        if ((State.Ended is not null) && !SayBox.ScreenBounds.Contains(e.ScreenX, e.ScreenY))
        {
            e.Handled = true;

            return;
        }

        if (e.Button == MouseButton.Right)
        {
            Input.Cancel();
            e.Handled = true;

            return;
        }

        base.OnMouseDown(e);
    }

    public override void OnDragStart(DragStartEvent e)
    {
        if (State.Ended is not null)
            return;

        if ((Queue.ShownBoard ?? State.Latest) is not { } board)
            return;

        var x = e.ScreenX - ScreenX;
        var y = e.ScreenY - ScreenY;

        if (Input.Mode == TcgInputMode.Blocking)
        {
            if (OwnCreatureIdAt(board, Input, x, y) is var blocker && (blocker != 0))
                e.Payload = blocker;

            return;
        }

        if (Input.Mode is TcgInputMode.Ordering or TcgInputMode.Redraw)
            return;

        var id = HandCardIdAt(board, x, y);

        if (id != 0)
            e.Payload = id;
    }

    public override void OnDragDrop(DragDropEvent e)
    {
        if (State.Ended is not null)
        {
            e.Handled = true;

            return;
        }

        if ((e.Payload is not int id) || (State.Latest is not { } latest))
            return;

        if (Input.Mode == TcgInputMode.Blocking)
        {
            HandleBlockDrop(id, e.ScreenX - ScreenX, e.ScreenY - ScreenY);
            e.Handled = true;

            return;
        }

        var zone = DropZoneAt(e.ScreenX - ScreenX, e.ScreenY - ScreenY);

        if (zone == TcgDropZone.None)
            Input.Cancel();
        else
            Raise(Input.DropHandCard(latest, id, zone == TcgDropZone.Mana));

        e.Handled = true;
    }
}
