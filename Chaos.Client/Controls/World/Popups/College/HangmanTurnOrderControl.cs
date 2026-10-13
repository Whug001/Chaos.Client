using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Extensions;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The Teacher's Hangman turn-order window: a tick box per name (everyone starts ticked), drag or Up and Down to reorder,
///     Shuffle and Tick all. Start sends the ticked names in order; Cancel (or Esc) cancels the pending game.
/// </summary>
public sealed class HangmanTurnOrderControl : GuildCloakDialogBase
{
    private const int WIDTH = 300;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int CAPTION_TOP = 10;
    private const int HINT_TOP = 28;
    private const int ROWS_TOP = 46;
    private const int ROWS = 10;
    private const int INFO_TOP = ROWS_TOP + (ROWS * TurnOrderRow.HEIGHT) + 6;
    private const int MOVE_TOP = INFO_TOP + TextRenderer.CHAR_HEIGHT + 6;
    private const int BUTTONS_TOP = MOVE_TOP + CustomButton.HEIGHT + 6;
    private const int HEIGHT = BUTTONS_TOP + CustomButton.HEIGHT + 4 + BORDER_BOTTOM_HEIGHT;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private readonly UILabel InfoLabel;
    private readonly TurnOrderRow[] RowViews = new TurnOrderRow[ROWS];
    private readonly CustomButton StartButton;
    private string Message = string.Empty;
    private TurnOrderList Order = new([]);
    private int ScrollTop;

    public event Action<CollegeActionArgs>? ActionRequested;

    public HangmanTurnOrderControl()
        : base("_nsett", false)
    {
        Name = "HangmanTurnOrder";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Cancel, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        Caption("Hangman turn order", LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        Caption("Tick who plays. Drag to reorder.", LEFT, HINT_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        for (var i = 0; i < RowViews.Length; i++)
        {
            var slot = i;
            var row = new TurnOrderRow(INNER_WIDTH) { X = LEFT, Y = ROWS_TOP + (i * TurnOrderRow.HEIGHT) };
            row.Toggled += () => Edit(() => Order.Toggle(ScrollTop + slot), false);
            row.Selected += () => Edit(() => Order.Select(ScrollTop + slot), false);
            row.Dropped += from => Edit(() => Order.Move(from, ScrollTop + slot), true);
            AddChild(row);
            RowViews[i] = row;
        }

        InfoLabel = Caption(string.Empty, LEFT, INFO_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);
        AddButton("Up", 50, LEFT, MOVE_TOP, () => Edit(() => Order.MoveUp(), true));
        AddButton("Down", 60, LEFT + 56, MOVE_TOP, () => Edit(() => Order.MoveDown(), true));
        AddButton("Shuffle", 70, LEFT + 122, MOVE_TOP, () => Edit(() => Order.Shuffle(Random.Shared), false));
        AddButton("Tick all", 70, LEFT + 198, MOVE_TOP, () => Edit(() => Order.TickAll(), false));
        StartButton = AddButton("Start", 80, LEFT, BUTTONS_TOP, Start);
        AddButton("Cancel", 80, LEFT + 86, BUTTONS_TOP, Cancel);
    }

    public TurnOrderList Current => Order;

    /// <summary>A HangmanOrder message: everyone on the map but the Teacher, and the server's line about the game.</summary>
    public void ShowOrder(IReadOnlyList<string> names, string message)
    {
        if (Order.Rows.Count > 0)
        {
            Order.Reconcile(names);
            ScrollTop = Math.Clamp(ScrollTop, 0, Math.Max(0, Order.Rows.Count - ROWS));
        } else
        {
            Order = new TurnOrderList(names);
            ScrollTop = 0;
        }

        Message = message;
        Refresh();
        Show();
    }

    public void Close()
    {
        Order = new TurnOrderList([]);
        Hide();
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

    public override void OnMouseScroll(MouseScrollEvent e)
    {
        if (Order.Rows.Count <= ROWS)
            return;

        ScrollTop = Math.Clamp(ScrollTop - Math.Sign(e.Delta), 0, Order.Rows.Count - ROWS);
        Refresh();
        e.Handled = true;
    }

    private void Cancel()
    {
        Raise(new CollegeActionArgs { Type = CollegeActionType.WordGameCancel });
        Close();
    }

    private void Edit(Action change, bool followSelection)
    {
        change();

        if (followSelection && (Order.Selected >= 0))
            ScrollTop = Math.Clamp(ScrollTop, Math.Max(0, Order.Selected - ROWS + 1), Order.Selected);

        ScrollTop = Math.Clamp(ScrollTop, 0, Math.Max(0, Order.Rows.Count - ROWS));
        Refresh();
    }

    private void Raise(CollegeActionArgs args) => ActionRequested?.Invoke(args);

    private void Refresh()
    {
        for (var i = 0; i < RowViews.Length; i++)
        {
            var index = ScrollTop + i;

            if (index < Order.Rows.Count)
            {
                var (name, ticked) = Order.Rows[index];
                RowViews[i].Set(index, name, ticked, index == Order.Selected);
            }
            else
                RowViews[i].Clear();
        }

        InfoLabel.Text = Message.Length == 0 ? $"{Order.TickedCount} playing" : $"{Order.TickedCount} playing - {Message}";
        StartButton.Enabled = Order.TickedCount > 0;
    }

    private void Start()
    {
        if (Order.TickedCount == 0)
            return;

        Raise(new CollegeActionArgs { Type = CollegeActionType.HangmanOrder, Names = Order.TickedNames.ToList() });
        Hide();
    }

    /// <summary>One name in the order: its place, a tick box and the name. Drag it onto another row to move it.</summary>
    private sealed class TurnOrderRow : UIPanel
    {
        public const int HEIGHT = CollegeListRow.HEIGHT + 2;
        private const int BOX = 10;
        private const int BOX_X = 24;
        private static readonly Color SelectedColor = new(156, 96, 12, 180);

        private readonly UILabel NameLabel;
        private readonly UILabel NumberLabel;
        private bool Ticked;

        public TurnOrderRow(int width)
        {
            Width = width;
            Height = HEIGHT;
            var textY = (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2;

            NumberLabel = new UILabel { X = 4, Y = textY, Width = 20, Height = TextRenderer.CHAR_HEIGHT, ForegroundColor = LegendColors.Gray, IsHitTestVisible = false };

            NameLabel = new UILabel
            {
                X = BOX_X + BOX + 8, Y = textY, Width = width - BOX_X - BOX - 12, Height = TextRenderer.CHAR_HEIGHT, ForegroundColor = LegendColors.White, IsHitTestVisible = false
            };

            AddChild(NumberLabel);
            AddChild(NameLabel);
        }

        public int Index { get; private set; } = -1;

        public event Action? Toggled;
        public event Action? Selected;
        public event Action<int>? Dropped;

        public void Clear()
        {
            Index = -1;
            Visible = false;
        }

        public void Set(int index, string name, bool ticked, bool selected)
        {
            Index = index;
            Visible = true;
            Ticked = ticked;
            NumberLabel.Text = (index + 1).ToString();
            NameLabel.Text = name;
            NameLabel.ForegroundColor = ticked ? LegendColors.White : LegendColors.Gray;
            BackgroundColor = selected ? SelectedColor : null;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible)
                return;

            base.Draw(spriteBatch);

            var box = new Rectangle(ScreenX + BOX_X, ScreenY + ((HEIGHT - BOX) / 2), BOX, BOX);
            DrawBorder(spriteBatch, box, LegendColors.Silver);

            if (Ticked)
                DrawRect(spriteBatch, new Rectangle(box.X + 2, box.Y + 2, BOX - 4, BOX - 4), LegendColors.Lime);
        }

        public override void OnClick(ClickEvent e)
        {
            if (e.Button != MouseButton.Left)
                return;

            if (e.ScreenX - ScreenX < BOX_X + BOX + 4)
                Toggled?.Invoke();
            else
                Selected?.Invoke();

            e.Handled = true;
        }

        public override void OnMouseDown(MouseDownEvent e) => e.Handled = true;

        public override void OnDragStart(DragStartEvent e)
        {
            if ((e.Button != MouseButton.Left) || (Index < 0))
                return;

            e.Payload = Index;
        }

        public override void OnDragDrop(DragDropEvent e)
        {
            if (e.Payload is int from)
                Dropped?.Invoke(from);

            e.Handled = true;
        }
    }
}
