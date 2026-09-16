#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.GroupPanels;

/// <summary>
///     The column of group-member windows down the left of the viewport, under the message pane, plus the one arrow
///     that folds all of them away.
/// </summary>
/// <remarks>
///     One arrow rather than one per window, and one drag handle rather than one per window: the panels are a
///     single readout of the group split across a member each, and nobody wants to fold or reposition five of them
///     individually. Dragging anywhere on the column moves the whole column and keeps the members in group order,
///     so the leader stays at the top wherever the player puts it.
///     <para />
///     Position and collapsed state are remembered for the session rather than reset on every group change; a
///     player who moved the column or folded it away meant it to stay that way when somebody joins or leaves.
/// </remarks>
public sealed class GroupPanelStack : UIPanel
{
    /// <summary>Vertical space between one member's window and the next.</summary>
    private const int PANEL_GAP = 2;

    private const int ARROW_SIZE = 16;

    /// <summary>The grab bar the arrow sits in. Full width, so the column can be dragged while folded away.</summary>
    private const int HEADER_HEIGHT = ARROW_SIZE;

    private const int ARROW_GAP = 1;
    private const int HEADER_TEXT_X = ARROW_SIZE + 4;

    //scroll.epf frame order: left(0,1), right(2,3), up(4,5), down(6,7), thumb(8), track(9); each pair is
    //[normal, active]. The collapse arrow borrows the scrollbar's own up/down buttons rather than introducing art
    //of its own, which is what every other arrow in this client does.
    private const string SCROLL_EPF = "scroll.epf";
    private const int FRAME_UP_NORMAL = 4;
    private const int FRAME_UP_ACTIVE = 5;
    private const int FRAME_DOWN_NORMAL = 6;
    private const int FRAME_DOWN_ACTIVE = 7;

    /// <summary>How far from the viewport's left edge the column sits before the player moves it.</summary>
    private const int DEFAULT_LEFT_MARGIN = 2;

    /// <summary>
    ///     How far down the viewport the column starts before the player moves it: clear of the system message
    ///     pane, which is three lines pinned to the viewport's top-left corner
    ///     (<c>SystemMessagePaneControl</c>).
    /// </summary>
    private const int DEFAULT_TOP_GAP = (3 * TextRenderer.CHAR_HEIGHT) + 4;

    private static readonly Color HeaderFill = new(18, 11, 5, 220);
    private static readonly Color HeaderBorder = new(54, 34, 16);
    private static readonly Color HeaderText = new(214, 190, 146);
    private static readonly Color Shadow = Color.Black;

    private readonly AislingRenderer Renderer;
    private readonly List<GroupMemberPanel> Panels = [];

    private Rectangle ViewportBounds;
    private bool Placed;
    private bool ArrowHeld;

    private bool Dragging;
    private int DragOffsetX;
    private int DragOffsetY;

    public GroupPanelStack(AislingRenderer renderer, Rectangle viewportBounds)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        Renderer = renderer;
        Name = "GroupPanelStack";
        ViewportBounds = viewportBounds;
        Width = GroupMemberPanel.PANEL_WIDTH;

        //stay Visible so the parent keeps calling Update; whether anything is drawn is decided per frame from
        //WorldState, the same way PollPanel does it
        Visible = true;
        IsHitTestVisible = false;

        WorldState.GroupMembers.Changed += OnGroupChanged;
    }

    /// <summary>
    ///     Re-anchors to a new viewport -- the two hud layouts have different ones and the <c>/</c> key swaps
    ///     between them. Deliberately does not re-park the column: the position is the player's once they have
    ///     moved it, and a layout swap is not them asking for it back.
    /// </summary>
    public void SetViewportBounds(Rectangle bounds) => ViewportBounds = bounds;

    /// <summary>
    ///     Holds the column down while a full-screen overlay is up. The world map is drawn over the whole window,
    ///     and a floating panel on top of it would sit on the map art.
    /// </summary>
    public bool Suppressed { get; set; }

    private bool ShouldShow => WorldState.GroupMembers.HasMembers && !Suppressed;

    private bool Collapsed
    {
        get => WorldState.GroupMembers.Collapsed;
        set => WorldState.GroupMembers.Collapsed = value;
    }

    /// <summary>Rebuilds the column when the server sends a new snapshot.</summary>
    private void OnGroupChanged()
    {
        var members = WorldState.GroupMembers.Members;

        //grow to fit. Panels are reused across snapshots so a portrait survives a member's health changing, which
        //is most of what arrives; only a member whose appearance actually changed re-renders
        while (Panels.Count < members.Count)
        {
            var panel = new GroupMemberPanel(Renderer)
            {
                X = 0,
                Visible = false
            };

            Panels.Add(panel);
            AddChild(panel);
        }

        //a panel past the end of the group is shown nobody, which is what drops the portrait it was holding
        for (var i = 0; i < Panels.Count; i++)
            Panels[i]
                .Show(i < members.Count ? members[i] : null);

        Layout();
    }

    /// <summary>
    ///     Stacks the visible panels under the arrow and sizes the column to them.
    /// </summary>
    private void Layout()
    {
        var count = ShouldShow ? WorldState.GroupMembers.Members.Count : 0;
        var top = HEADER_HEIGHT + ARROW_GAP;

        for (var i = 0; i < Panels.Count; i++)
        {
            var panel = Panels[i];

            if ((i >= count) || Collapsed)
            {
                panel.Visible = false;

                continue;
            }

            panel.Visible = true;
            panel.Y = top + (i * (GroupMemberPanel.PANEL_HEIGHT + PANEL_GAP));
        }

        Height = Collapsed || (count == 0)
            ? HEADER_HEIGHT
            : top + (count * (GroupMemberPanel.PANEL_HEIGHT + PANEL_GAP)) - PANEL_GAP;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!ShouldShow)
        {
            IsHitTestVisible = false;

            return;
        }

        if (!Placed)
            PlaceAtDefault();

        //a group that grew while collapsed, or shrank, changes the column's height; recomputing is a handful of
        //assignments and saves having to notice every path that could have changed it
        Layout();
        ClampIntoViewport();
        IsHitTestVisible = true;
    }

    /// <summary>
    ///     Parks the column down the left of the viewport, just under the message pane, the first time there is
    ///     something to show. Only ever done once: after that the position is the player's.
    /// </summary>
    private void PlaceAtDefault()
    {
        Placed = true;
        X = ViewportBounds.X + DEFAULT_LEFT_MARGIN;
        Y = ViewportBounds.Y + DEFAULT_TOP_GAP;
    }

    private void ClampIntoViewport()
    {
        //only the left and top are held to, plus enough of the column's own width to grab: a group large enough to
        //run past the bottom of the window must still be draggable, and forcing it back up would fight the player
        //every frame
        X = Math.Clamp(X, ViewportBounds.X, Math.Max(ViewportBounds.X, ViewportBounds.Right - HEADER_HEIGHT));
        Y = Math.Clamp(Y, ViewportBounds.Y, Math.Max(ViewportBounds.Y, ViewportBounds.Bottom - HEADER_HEIGHT));
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        if (!ShouldShow || (e.Button != MouseButton.Left))
            return;

        var localX = e.ScreenX - ScreenX;
        var localY = e.ScreenY - ScreenY;

        if (OverArrow(localX, localY))
        {
            ArrowHeld = true;
            e.Handled = true;

            return;
        }

        Dragging = true;
        DragOffsetX = localX;
        DragOffsetY = localY;
        e.Handled = true;
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (!Dragging)
            return;

        X = e.ScreenX - DragOffsetX;
        Y = e.ScreenY - DragOffsetY;
        ClampIntoViewport();
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        if (ArrowHeld)
        {
            ArrowHeld = false;

            //released on the arrow it was pressed on, the same rule the dispatcher uses to turn a press and a
            //release into a click
            if (OverArrow(e.ScreenX - ScreenX, e.ScreenY - ScreenY))
            {
                Collapsed = !Collapsed;
                Layout();
            }

            e.Handled = true;
        }

        Dragging = false;
    }

    private static bool OverArrow(int localX, int localY)
        => (localX >= 0) && (localX < ARROW_SIZE) && (localY >= 0) && (localY < ARROW_SIZE);

    public override void ResetInteractionState()
    {
        ArrowHeld = false;
        Dragging = false;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!ShouldShow)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        DrawHeader(spriteBatch);

        foreach (var panel in Panels)
            if (panel.Visible)
                panel.Draw(spriteBatch);
    }

    /// <summary>
    ///     The grab bar: the collapse arrow, and the group's size beside it so a folded-away column still says what
    ///     it is.
    /// </summary>
    private void DrawHeader(SpriteBatch spriteBatch)
    {
        var header = new Rectangle(
            ScreenX,
            ScreenY,
            Width,
            HEADER_HEIGHT);

        DrawRect(spriteBatch, header, HeaderFill);
        DrawBorder(spriteBatch, header, HeaderBorder);

        var renderer = UiRenderer.Instance;

        if (renderer is not null)
        {
            //pointing up while the panels are open (click to fold them up), down while they are away
            var frame = Collapsed
                ? ArrowHeld ? FRAME_DOWN_ACTIVE : FRAME_DOWN_NORMAL
                : ArrowHeld ? FRAME_UP_ACTIVE : FRAME_UP_NORMAL;

            DrawTexture(
                spriteBatch,
                renderer.GetEpfTexture(SCROLL_EPF, frame),
                new Vector2(ScreenX, ScreenY),
                Color.White);
        }

        var caption = $"Group ({WorldState.GroupMembers.Members.Count})";

        TextRenderer.DrawShadowedText(
            spriteBatch,
            new Vector2(ScreenX + HEADER_TEXT_X, ScreenY + ((HEADER_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2)),
            caption,
            HeaderText,
            Shadow);
    }

    public override void Dispose()
    {
        WorldState.GroupMembers.Changed -= OnGroupChanged;

        foreach (var panel in Panels)
            panel.Dispose();

        Panels.Clear();
        base.Dispose();
    }
}
