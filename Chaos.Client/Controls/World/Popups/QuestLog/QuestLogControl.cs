#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.QuestLog;

/// <summary>
///     The quest log window, drawn from the Legend of Darkness "Quest List" art in setoa.dat: active quests on top, the
///     selected quest's details below, GiveUp and Close underneath. It minimizes to its title bar and drags by the title
///     bar. It only shows <see cref="WorldState.QuestLog" />; the server builds the list and judges give-ups.
/// </summary>
public sealed class QuestLogControl : UIPanel
{
    private const int FULL_WIDTH = 247;
    private const int FULL_HEIGHT = 307;
    private const int MIN_HEIGHT = 32;
    private const int TITLE_BAR_HEIGHT = 26;
    private const int LIST_X = 16;
    private const int LIST_Y = 35;
    private const int LIST_WIDTH = 215;
    private const int LIST_HEIGHT = 90;
    private const int ROW_HEIGHT = 12;
    private const int DETAILS_X = 19;
    private const int DETAILS_Y = 131;
    private const int DETAILS_WIDTH = 188;
    private const int DETAILS_HEIGHT = 140;
    private const int BOTTOM_BUTTON_Y = 282;
    private const int GIVE_UP_X = 71;
    private const int CLOSE_X = 135;
    private const int WIDE_BUTTON_WIDTH = 50;
    private const int WIDE_BUTTON_HEIGHT = 20;
    private const int SMALL_BUTTON_SIZE = 18;
    private const int TITLE_BUTTON_Y = 8;
    private const int FULL_MINIMIZE_X = 196;
    private const int FULL_X_BUTTON_X = 216;
    private const int MIN_RESTORE_X = 186;
    private const int MIN_X_BUTTON_X = 213;
    private const int SCREEN_WIDTH = 640;
    private const int SCREEN_HEIGHT = 480;

    private readonly UIButton CloseButton;
    private readonly UIButton CloseXButton;
    private readonly UILabel DetailsLabel;
    private readonly ScrollViewerControl DetailsViewer;
    private readonly Texture2D FullBackground;
    private readonly UIButton GiveUpButton;
    private readonly ScrollViewerControl ListViewer;
    private readonly UIButton MinimizeButton;
    private readonly Texture2D MinimizedBackground;
    private readonly UIButton RestoreButton;
    private readonly VirtualizedRowList<QuestLogEntryInfo> RowList;
    private int DragOffsetX;
    private int DragOffsetY;
    private bool Dragging;
    private string? ShownKey;

    public bool Minimized { get; private set; }

    /// <summary>The window closed (X, Close or the Q button). The world screen tells the server.</summary>
    public event Action? Closed;

    /// <summary>The confirming second GiveUp click, with the quest key.</summary>
    public event Action<string>? GiveUpRequested;

    public QuestLogControl()
    {
        Name = "QuestLog";
        Visible = false;
        Width = FULL_WIDTH;
        Height = FULL_HEIGHT;

        var ui = UiRenderer.Instance!;
        FullBackground = ui.GetSpfTexture("q_list.spf");
        MinimizedBackground = ui.GetSpfTexture("q_min.spf");
        Background = FullBackground;

        var rowWidth = LIST_WIDTH - ScrollBarControl.DEFAULT_WIDTH;
        var state = WorldState.QuestLog;

        RowList = new VirtualizedRowList<QuestLogEntryInfo>(
            rowWidth,
            LIST_HEIGHT,
            ROW_HEIGHT,
            () => new QuestLogRow(rowWidth, key => state.Select(key)),
            (row, entry, _) => ((QuestLogRow)row).Bind(entry, entry.Key == state.SelectedKey));

        //bound once: the state keeps the same list instance, so later updates only need Invalidate (which keeps the scroll)
        RowList.SetItems(state.Entries);

        ListViewer = new ScrollViewerControl(RowList)
        {
            X = LIST_X,
            Y = LIST_Y,
            Width = LIST_WIDTH,
            Height = LIST_HEIGHT
        };

        AddChild(ListViewer);

        DetailsLabel = new UILabel
        {
            WordWrap = true,
            VerticalAlignment = VerticalAlignment.Top,
            ColorCodesEnabled = true,
            PaddingLeft = 0,
            PaddingTop = 0
        };

        //bar-less: the wheel scrolls long text, like ItemDetailControl's stat pane
        DetailsViewer = new ScrollViewerControl(DetailsLabel)
        {
            X = DETAILS_X,
            Y = DETAILS_Y,
            Width = DETAILS_WIDTH,
            Height = DETAILS_HEIGHT,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden
        };

        AddChild(DetailsViewer);

        GiveUpButton = AddButton("q_gvp.spf", GIVE_UP_X, BOTTOM_BUTTON_Y, WIDE_BUTTON_WIDTH, WIDE_BUTTON_HEIGHT, OnGiveUpClicked);
        GiveUpButton.DisabledTexture = GiveUpButton.PressedTexture;
        CloseButton = AddButton("q_close.spf", CLOSE_X, BOTTOM_BUTTON_Y, WIDE_BUTTON_WIDTH, WIDE_BUTTON_HEIGHT, Close);
        MinimizeButton = AddButton("under_bn.spf", FULL_MINIMIZE_X, TITLE_BUTTON_Y, SMALL_BUTTON_SIZE, SMALL_BUTTON_SIZE, () => SetMinimized(true));
        RestoreButton = AddButton("full_bn.spf", MIN_RESTORE_X, TITLE_BUTTON_Y, SMALL_BUTTON_SIZE, SMALL_BUTTON_SIZE, () => SetMinimized(false));
        CloseXButton = AddButton("x_bn.spf", FULL_X_BUTTON_X, TITLE_BUTTON_Y, SMALL_BUTTON_SIZE, SMALL_BUTTON_SIZE, Close);

        state.Changed += Refresh;
        SetMinimized(false);
        this.CenterOnScreen();
        Refresh();
    }

    /// <summary>Shows the window.</summary>
    public void Show() => Visible = true;

    /// <summary>Hides the window without notifying the server. Use <see cref="Close" /> for a user-initiated close.</summary>
    public void Hide() => Visible = false;

    /// <summary>Hides the window and raises <see cref="Closed" />. Does nothing when already hidden.</summary>
    public void Close()
    {
        if (!Visible)
            return;

        WorldState.QuestLog.CancelGiveUp();
        Dragging = false;
        Hide();
        Closed?.Invoke();
    }

    public void SetMinimized(bool minimized)
    {
        Minimized = minimized;
        Background = minimized ? MinimizedBackground : FullBackground;
        Height = minimized ? MIN_HEIGHT : FULL_HEIGHT;

        ListViewer.Visible = !minimized;
        DetailsViewer.Visible = !minimized;
        GiveUpButton.Visible = !minimized;
        CloseButton.Visible = !minimized;
        MinimizeButton.Visible = !minimized;
        RestoreButton.Visible = minimized;
        CloseXButton.X = minimized ? MIN_X_BUTTON_X : FULL_X_BUTTON_X;

        ClampOnScreen();
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        var localY = e.ScreenY - ScreenY;

        //buttons take their own clicks first; anything else on the title bar (the whole bar when minimized) drags
        if ((e.Button == MouseButton.Left) && (Minimized || (localY < TITLE_BAR_HEIGHT)))
        {
            Dragging = true;
            DragOffsetX = e.ScreenX - ScreenX;
            DragOffsetY = localY;
            e.Handled = true;

            return;
        }

        base.OnMouseDown(e);
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (Dragging)
        {
            X = e.ScreenX - DragOffsetX;
            Y = e.ScreenY - DragOffsetY;
            ClampOnScreen();

            return;
        }

        base.OnMouseMove(e);
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if ((e.Button == MouseButton.Left) && Dragging)
        {
            Dragging = false;
            e.Handled = true;

            return;
        }

        base.OnMouseUp(e);
    }

    public override void Dispose()
    {
        WorldState.QuestLog.Changed -= Refresh;

        //the textures belong to UiRenderer's shared cache; drop them so the base Dispose doesn't dispose them
        Background = null;

        foreach (var button in new[] { GiveUpButton, CloseButton, MinimizeButton, RestoreButton, CloseXButton })
        {
            button.NormalTexture = null;
            button.PressedTexture = null;
            button.DisabledTexture = null;
        }

        base.Dispose();
    }

    private UIButton AddButton(string spf, int x, int y, int width, int height, ClickedHandler onClick)
    {
        var ui = UiRenderer.Instance!;

        var button = new UIButton
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            NormalTexture = ui.GetSpfTexture(spf),
            PressedTexture = ui.GetSpfTexture(spf, 1)
        };

        button.Clicked += onClick;
        AddChild(button);

        return button;
    }

    private void ClampOnScreen()
    {
        X = Math.Clamp(X, 0, SCREEN_WIDTH - Width);
        Y = Math.Clamp(Y, 0, SCREEN_HEIGHT - Height);
    }

    private void OnGiveUpClicked()
    {
        if (WorldState.QuestLog.PressGiveUp() is { } key)
            GiveUpRequested?.Invoke(key);
    }

    private void Refresh()
    {
        var state = WorldState.QuestLog;

        RowList.Invalidate();
        DetailsLabel.Text = state.DetailsText();
        GiveUpButton.Enabled = state.CanGiveUp;

        //a newly selected quest starts at the top of its text
        if (state.SelectedKey != ShownKey)
        {
            ShownKey = state.SelectedKey;
            ((IVerticalScrollable)DetailsLabel).VerticalOffset = 0;
        }
    }
}
