#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg;

/// <summary>
///     The deck builder (layout A), over the binder: the binder grid with "in deck / owned" on the left, the deck's rows,
///     cost chart, count and first problem on the right. Clicking a card adds a copy (owned only, 3 per card, 40 in all);
///     clicking a row removes one. The deck being built is a <see cref="TcgDeckDraft" />; Save sends it and waits for the
///     server's snapshot before closing. Has no server session of its own.
/// </summary>
public sealed class TcgDeckBuilderControl : FramedDialogPanelBase
{
    private const int CELL_STEP_X = 68;
    private const int CELL_STEP_Y = 94;
    private const int COLUMNS = 4;
    private const int COUNT_TOP = 334;
    private const int CURVE_TOP = 256;
    private const int GRID_TOP = 54;
    private const int LEFT = 20;
    private const int MESSAGE_TOP = 358;
    private const int NAME_BOX_LEFT = LEFT + 36;
    private const int NAME_BOX_WIDTH = 170;
    private const int PAGER_TOP = 334;
    private const int RIGHT_LEFT = 300;
    private const int RIGHT_WIDTH = 300;
    private const int SAVE_TOP = 350;
    private const int TAB_LEFT = 236;
    private const int TAB_STEP = 56;
    private const int TAB_TOP = 26;
    private const int TAB_WIDTH = 52;
    private const int TITLE_TOP = 8;

    private static readonly string[] TabNames = ["All", "Fire", "Water", "Wind", "Earth", "Neutral"];

    private readonly TcgChoicePanel Choice;
    private readonly UILabel CountLabel;
    private readonly TcgCostCurveView Curve;
    private readonly TcgCardFace[] Faces = new TcgCardFace[TcgCollectionState.PAGE_SIZE];
    private readonly UILabel MessageLabel;
    private readonly CustomTextBox NameBox;
    private readonly CustomButton NextButton;
    private readonly UILabel PageLabel;
    private readonly CustomButton PreviousButton;
    private readonly TcgCardFace PreviewFace;
    private readonly UILabel ProblemLabel;
    private readonly TcgDeckRowsView RowsView;
    private readonly CustomButton SaveButton;
    private readonly CustomButton[] TabButtons = new CustomButton[TabNames.Length];
    private bool AwaitingSave;
    private TcgDeckDraft? Draft;
    private int Page;
    private TcgBinderTab Tab = TcgBinderTab.All;

    public TcgDeckBuilderControl(CreatureRenderer creatures)
        : base("_nsett", false)
    {
        Name = "TcgDeckBuilder";
        Visible = false;
        UsesControlStack = true;
        Width = TcgBinderControl.PANEL_WIDTH;
        Height = TcgBinderControl.PANEL_HEIGHT;
        X = TcgBinderControl.PANEL_X;
        Y = TcgBinderControl.PANEL_Y;

        AddChild(Label(0, TITLE_TOP, Width, "Deck Builder", LegendColors.Gold, HorizontalAlignment.Center));
        AddChild(Label(LEFT, TAB_TOP + 5, 34, "Name", LegendColors.White));

        NameBox = new CustomTextBox
        {
            X = NAME_BOX_LEFT,
            Y = TAB_TOP,
            Width = NAME_BOX_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = TcgDeckDraft.MAX_NAME_LENGTH,
            IsSelectable = true,
            IsTabStop = true
        };

        AddChild(NameBox);

        for (var i = 0; i < TabButtons.Length; i++)
        {
            var tab = (TcgBinderTab)i;

            TabButtons[i] = new CustomButton(TabNames[i], TAB_WIDTH)
            {
                X = TAB_LEFT + (i * TAB_STEP),
                Y = TAB_TOP
            };

            TabButtons[i].Clicked += () =>
            {
                Tab = tab;
                Page = 0;
                Refresh();
            };

            AddChild(TabButtons[i]);
        }

        for (var i = 0; i < Faces.Length; i++)
        {
            Faces[i] = new TcgCardFace(creatures, 1)
            {
                X = LEFT + ((i % COLUMNS) * CELL_STEP_X),
                Y = GRID_TOP + ((i / COLUMNS) * CELL_STEP_Y)
            };

            Faces[i].Clicked += OnCardClicked;
            AddChild(Faces[i]);
        }

        PreviousButton = new CustomButton("<", 24)
        {
            X = LEFT,
            Y = PAGER_TOP
        };

        PreviousButton.Clicked += () =>
        {
            Page = Math.Max(0, Page - 1);
            Refresh();
        };

        AddChild(PreviousButton);

        PageLabel = Label(LEFT + 28, PAGER_TOP + 5, 208, string.Empty, LegendColors.White, HorizontalAlignment.Center);
        AddChild(PageLabel);

        NextButton = new CustomButton(">", 24)
        {
            X = LEFT + 240,
            Y = PAGER_TOP
        };

        NextButton.Clicked += () =>
        {
            Page++;
            Refresh();
        };

        AddChild(NextButton);

        RowsView = new TcgDeckRowsView
        {
            X = RIGHT_LEFT,
            Y = GRID_TOP,
            Width = RIGHT_WIDTH,
            Height = 14 * TcgDeckRowsView.ROW_HEIGHT
        };

        RowsView.RowClicked += OnRowClicked;
        AddChild(RowsView);

        Curve = new TcgCostCurveView
        {
            X = RIGHT_LEFT,
            Y = CURVE_TOP,
            Width = RIGHT_WIDTH,
            Height = 72
        };

        AddChild(Curve);

        CountLabel = Label(RIGHT_LEFT, COUNT_TOP, 60, string.Empty, LegendColors.White);
        ProblemLabel = Label(RIGHT_LEFT + 64, COUNT_TOP, RIGHT_WIDTH - 64, string.Empty, LegendColors.Orange);
        MessageLabel = Label(LEFT, MESSAGE_TOP, 270, string.Empty, LegendColors.Orange);
        AddChild(CountLabel);
        AddChild(ProblemLabel);
        AddChild(MessageLabel);

        SaveButton = new CustomButton("Save", 76)
        {
            X = 440,
            Y = SAVE_TOP
        };

        SaveButton.Clicked += Save;
        AddChild(SaveButton);

        var cancelButton = new CustomButton("Cancel", 76)
        {
            X = 524,
            Y = SAVE_TOP
        };

        cancelButton.Clicked += RequestClose;
        AddChild(cancelButton);

        PreviewFace = new TcgCardFace(creatures, 2)
        {
            IsHitTestVisible = false,
            Visible = false
        };

        AddChild(PreviewFace);

        //last, so it draws over everything and swallows clicks meant for what is behind it
        Choice = new TcgChoicePanel(Width, Height);
        AddChild(Choice);
    }

    private static TcgCollectionState State => WorldState.TcgCollection;

    /// <summary>SaveDeck requests.</summary>
    public event Action<TcgCollectionActionArgs>? ActionRequested;

    /// <summary>The binder or the server closed: close without asking about unsaved changes.</summary>
    public void CloseFromServer() => CloseNow();

    /// <summary>True while a question is asked over the window; hovers behind it stay hidden.</summary>
    public bool HasOpenQuestion => Choice.Visible;

    /// <summary>The keyword under the cursor on the hover preview, for the hover box. Null when nothing qualifies.</summary>
    public string? HoveredKeyword(int mouseX, int mouseY) => Visible && PreviewFace.Visible ? PreviewFace.KeywordAt(mouseX, mouseY) : null;

    /// <summary>Where the 2x hover preview goes, in control coordinates: 12 px right of the cursor, flipping left near the right edge and clamped inside.</summary>
    public static Point PreviewPosition(int mouseX, int mouseY, int width, int height)
    {
        const int GAP = 12;
        var previewWidth = TcgCardFace.WIDTH * 2;
        var previewHeight = TcgCardFace.HEIGHT * 2;
        var x = mouseX + GAP;

        if (x + previewWidth > width)
            x = mouseX - GAP - previewWidth;

        x = Math.Clamp(x, 0, Math.Max(0, width - previewWidth));

        return new Point(x, Math.Clamp(mouseY, 0, Math.Max(0, height - previewHeight)));
    }

    /// <summary>A plain Hide (Escape, Cancel) asks first when there are unsaved changes.</summary>
    public override void Hide()
    {
        if (Visible)
            RequestClose();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            if (Choice.Visible)
                Choice.Cancel();
            else
                RequestClose();

            e.Handled = true;

            return;
        }

        if ((e.Keycode == Keycode.Enter) && NameBox.IsFocused)
        {
            NameBox.IsFocused = false;
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>A new snapshot: finishes a pending save (closing on success) and redraws.</summary>
    public void OnSnapshot()
    {
        if (!Visible)
            return;

        if (AwaitingSave)
        {
            if (State.Message.Length > 0)
            {
                AwaitingSave = false;
                MessageLabel.Text = State.Message;
            } else if (Draft?.IsSavedIn(State.Decks) ?? true)
            {
                AwaitingSave = false;
                CloseNow();

                return;
            }
        }

        Refresh();
    }

    public void Open(TcgDeckDraft draft)
    {
        Draft = draft;
        NameBox.Text = draft.Name;
        Tab = TcgBinderTab.All;
        Page = 0;
        AwaitingSave = false;
        MessageLabel.Text = string.Empty;
        Show();
        Refresh();
    }

    public void Refresh()
    {
        if (!Visible || Draft is not { } draft)
            return;

        for (var i = 0; i < TabButtons.Length; i++)
            TabButtons[i].Selected = (TcgBinderTab)i == Tab;

        var cards = State.Filter(Tab, false);
        Page = Math.Clamp(Page, 0, TcgCollectionState.PageCountOf(cards.Count) - 1);
        var page = TcgCollectionState.PageOf(cards, Page);

        for (var i = 0; i < Faces.Length; i++)
        {
            var face = Faces[i];
            var card = i < page.Count ? page[i] : null;
            face.Visible = card is not null;

            if (card is null)
                continue;

            var owned = State.Copies(card.Key);
            face.Card = card;
            face.Owned = owned > 0;
            var printing = State.DeckFacePrinting(draft.Keys, card.Key);
            face.Style = printing.Style;
            face.Foil = printing.Foil;
            face.Badge = $"{draft.InDeck(card.Key)}/{owned}";
        }

        PreviousButton.Enabled = Page > 0;
        NextButton.Enabled = Page < (TcgCollectionState.PageCountOf(cards.Count) - 1);
        PageLabel.Text = State.PageTextOf(cards, Page);

        RowsView.SetRows(draft.Rows(State.Get));
        Curve.SetCurve(draft.CostCurve(State.Get));
        RefreshFooter(draft);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!Visible)
            return;

        if (Draft is { } draft && (NameBox.Text != draft.Name))
        {
            draft.Name = NameBox.Text;
            RefreshFooter(draft);
        }

        UpdatePreview();
    }

    private void CloseNow()
    {
        Choice.Dismiss();
        NameBox.IsFocused = false;
        Draft = null;
        AwaitingSave = false;

        if (Visible)
            base.Hide();
    }

    private static UILabel Label(
        int x,
        int y,
        int width,
        string text,
        Color color,
        HorizontalAlignment alignment = HorizontalAlignment.Left)
        => new()
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            Text = text,
            ForegroundColor = color,
            HorizontalAlignment = alignment,
            IsHitTestVisible = false
        };

    private void UpdatePreview()
    {
        if (Choice.Visible || Draft is not { } draft)
        {
            PreviewFace.Visible = false;

            return;
        }

        var mouseX = InputBuffer.MouseX;
        var mouseY = InputBuffer.MouseY;

        if (PreviewFace.Visible && PreviewFace.ScreenBounds.Contains(mouseX, mouseY))
            return;

        PreviewFace.Visible = false;
        string? key = null;

        foreach (var face in Faces)
        {
            if (face.Visible && face.ScreenBounds.Contains(mouseX, mouseY))
            {
                key = face.Card?.Key;

                break;
            }
        }

        key ??= RowsView.RowAt(mouseX, mouseY)?.Key;

        if (State.Get(key) is not { } card)
            return;

        var printing = State.DeckFacePrinting(draft.Keys, card.Key);
        var position = PreviewPosition(mouseX - ScreenX, mouseY - ScreenY, Width, Height);

        PreviewFace.Card = card;
        PreviewFace.Style = printing.Style;
        PreviewFace.Owned = State.Copies(card.Key) > 0;
        PreviewFace.Foil = printing.Foil;
        PreviewFace.X = position.X;
        PreviewFace.Y = position.Y;
        PreviewFace.Visible = true;
    }

    private void OnCardClicked(TcgCardFace face)
    {
        if (Draft is not { } draft || face.Card is not { } card)
            return;

        var owned = State.Copies(card.Key);

        MessageLabel.Text = draft.TryAdd(card.Key, owned, card.Rarity) ? string.Empty : draft.AddProblem(card.Key, owned, card.Rarity) ?? string.Empty;
        Refresh();
    }

    private void OnRowClicked(TcgDeckRow row)
    {
        if (Draft is not { } draft || !draft.Remove(row.Key))
            return;

        MessageLabel.Text = string.Empty;
        Refresh();
    }

    private void RefreshFooter(TcgDeckDraft draft)
    {
        var problem = draft.FirstProblem(State.Copies, State.Get);

        CountLabel.Text = draft.CountText;
        ProblemLabel.Text = problem;
        ProblemLabel.ForegroundColor = problem == "Ready to play" ? LegendColors.Lime : LegendColors.Orange;
        SaveButton.Enabled = draft.CanSave && !AwaitingSave;
    }

    private void RequestClose()
    {
        if (Draft is not { } draft)
        {
            CloseNow();

            return;
        }

        draft.Name = NameBox.Text;

        if (!draft.IsDirty)
        {
            CloseNow();

            return;
        }

        Choice.Ask(
            "Discard your changes to this deck?",
            ["Discard", "Keep editing"],
            answer =>
            {
                if (answer == 0)
                    CloseNow();
            });
    }

    private void Save()
    {
        if (Draft is not { } draft || AwaitingSave)
            return;

        draft.Name = NameBox.Text;

        if (draft.NameProblem is { } problem)
        {
            MessageLabel.Text = problem;

            return;
        }

        AwaitingSave = true;
        MessageLabel.Text = "Saving...";
        State.ClearMessage();
        RefreshFooter(draft);
        ActionRequested?.Invoke(draft.SaveAction());
    }
}
