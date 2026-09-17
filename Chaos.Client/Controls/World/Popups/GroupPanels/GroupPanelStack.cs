#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Data;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
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

    /// <summary>
    ///     The most member panels drawn at once. A larger group still reports its real size in the grab bar; it is
    ///     the column of panels that stops at six, not the count.
    /// </summary>
    private const int MAX_PANELS = 6;

    private const int ARROW_SIZE = 16;

    /// <summary>The grab bar the arrow sits in. Full width, so the column can be dragged while folded away.</summary>
    private const int HEADER_HEIGHT = ARROW_SIZE;

    private const int ARROW_GAP = 1;

    /// <summary>
    ///     Where the grab bar's caption starts: level with the names and the bars below it, not tucked up against
    ///     the collapse arrow. The arrow is narrower than the portrait, so anchoring the caption to the arrow left
    ///     it out of step with every row underneath.
    /// </summary>
    private const int HEADER_TEXT_X = GroupMemberPanel.TEXT_X;

    //scroll.epf frame order: left(0,1), right(2,3), up(4,5), down(6,7), thumb(8), track(9); each pair is
    //[normal, active]. The collapse arrow borrows the scrollbar's own up/down buttons rather than introducing art
    //of its own, which is what every other arrow in this client does.
    private const string SCROLL_EPF = "scroll.epf";
    private const int FRAME_UP_NORMAL = 4;
    private const int FRAME_UP_ACTIVE = 5;
    private const int FRAME_DOWN_NORMAL = 6;
    private const int FRAME_DOWN_ACTIVE = 7;

    /// <summary>
    ///     How far from the viewport's left edge the column sits before the player moves it. Far enough in to
    ///     clear the vines along that edge, which are part of the map art rather than the hud and so are not
    ///     something <see cref="ViewportBounds" /> knows about.
    /// </summary>
    private const int DEFAULT_LEFT_MARGIN = 14;

    /// <summary>
    ///     How far down the viewport the column starts before the player moves it: clear of the system message
    ///     pane, which is three lines pinned to the viewport's top-left corner
    ///     (<c>SystemMessagePaneControl</c>).
    /// </summary>
    private const int DEFAULT_TOP_GAP = (3 * TextRenderer.CHAR_HEIGHT) + 4;

    /// <summary>
    ///     The plate behind the whole column. Opaque, and the same colour the grab bar and the bar tracks are
    ///     already drawn in, so the column reads as one window rather than a bar with rows loose underneath it.
    /// </summary>
    private static readonly Color PlateFill = new(18, 11, 5);

    private static readonly Color PlateBorder = new(54, 34, 16);

    /// <summary>
    ///     How far the plate stands out past the column on every side.
    /// </summary>
    /// <remarks>
    ///     The rows carry no horizontal padding of their own -- the portrait starts on the column's left edge and
    ///     the bars end on its right -- so a plate drawn to the column's own bounds would run its border through
    ///     the portrait and the ends of the bars. The margin is what puts the edge outside the content.
    /// </remarks>
    private const int PLATE_MARGIN = 2;
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

    /// <summary>
    ///     The order the player has dragged the members into, by name. Empty until they move one.
    /// </summary>
    /// <remarks>
    ///     Held by name rather than by index because every packet replaces the whole member list, so an ordering
    ///     stored as positions would be undone by the next snapshot -- which arrives within a second or two. A
    ///     name survives that, and it also means somebody who drops group and rejoins comes back to the slot the
    ///     player put them in.
    /// </remarks>
    private readonly List<string> CustomOrder = [];

    /// <summary>The current group in the order it is drawn: <see cref="CustomOrder" /> applied to the snapshot.</summary>
    private List<GroupMemberSnapshot> Ordered = [];

    /// <summary>Which slot is being dragged, or -1 when none is.</summary>
    private int MemberDragIndex = -1;

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

    /// <summary>
    ///     Whether the column is drawn at all. Turning it off in the F4 settings takes it down outright rather
    ///     than collapsing it: <see cref="Update" /> clears <see cref="UIElement.IsHitTestVisible" /> and
    ///     <see cref="Draw" /> returns before the grab bar, so the header and its arrow go with the panels.
    /// </summary>
    private bool ShouldShow => ClientSettings.GroupPanelEnabled && WorldState.GroupMembers.HasMembers && !Suppressed;

    private bool Collapsed
    {
        get => WorldState.GroupMembers.Collapsed;
        set => WorldState.GroupMembers.Collapsed = value;
    }

    /// <summary>Rebuilds the column when the server sends a new snapshot.</summary>
    private void OnGroupChanged()
    {
        Ordered = ApplyCustomOrder(WorldState.GroupMembers.Members);
        ApplyMembers();
    }

    /// <summary>
    ///     Puts a server snapshot into the order the player arranged, with anyone they have never seen before on
    ///     the end in the order the server sent them.
    /// </summary>
    private List<GroupMemberSnapshot> ApplyCustomOrder(IReadOnlyList<GroupMemberSnapshot> members)
    {
        if (CustomOrder.Count == 0)
            return [..members];

        var remaining = members.ToList();
        var ordered = new List<GroupMemberSnapshot>(members.Count);

        foreach (var name in CustomOrder)
        {
            var index = remaining.FindIndex(member => member.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
                continue;

            ordered.Add(remaining[index]);
            remaining.RemoveAt(index);
        }

        //joiners go to the end rather than anywhere in the middle, so an arrangement does not shuffle itself
        //every time somebody is invited
        ordered.AddRange(remaining);

        return ordered;
    }

    /// <summary>Points the panels at <see cref="Ordered" /> and lays them out.</summary>
    private void ApplyMembers()
    {
        var shown = Math.Min(Ordered.Count, MAX_PANELS);

        //grow to fit. Panels are reused across snapshots so a portrait survives a member's health changing, which
        //is most of what arrives; only a member whose appearance actually changed re-renders
        while (Panels.Count < shown)
        {
            var panel = new GroupMemberPanel(Renderer)
            {
                X = 0,
                Visible = false
            };

            Panels.Add(panel);
            AddChild(panel);
        }

        //a panel past the end of the group, or past the cap, is shown nobody, which is what drops the portrait
        //it was holding
        for (var i = 0; i < Panels.Count; i++)
            Panels[i]
                .Show(i < shown ? Ordered[i] : null);

        Layout();
    }

    /// <summary>
    ///     Stacks the visible panels under the arrow and sizes the column to them.
    /// </summary>
    private void Layout()
    {
        var count = VisibleSlotCount;
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
    ///     Puts the column where this character left it, or parks it down the left of the viewport under the
    ///     message pane if they have never moved it. Only ever done once per session: after that the position is
    ///     whatever the player has dragged it to.
    /// </summary>
    /// <remarks>
    ///     Does not mark itself done until the per-character store is ready, which happens when the player's own
    ///     aisling is first displayed. A group snapshot arriving before that would otherwise park the column at
    ///     the default and never look at the saved position again.
    /// </remarks>
    private void PlaceAtDefault()
    {
        var settings = DataContext.LocalPlayerSettings;

        if (settings.IsInitialized && settings.TryLoadGroupPanelPosition(out var savedX, out var savedY))
        {
            Placed = true;
            X = savedX;
            Y = savedY;

            return;
        }

        X = ViewportBounds.X + DEFAULT_LEFT_MARGIN;
        Y = ViewportBounds.Y + DEFAULT_TOP_GAP;

        //nothing saved is a real answer once the store is up; before that it only means we asked too early
        Placed = settings.IsInitialized;
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

        //the grab bar moves the column; a member panel reorders the group. Collapsed, the bar is all there is
        if (localY < HEADER_HEIGHT)
        {
            Dragging = true;
            DragOffsetX = localX;
            DragOffsetY = localY;
            e.Handled = true;

            return;
        }

        var slot = SlotAt(localY);

        if (slot < 0)
            return;

        MemberDragIndex = slot;
        e.Handled = true;
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (Dragging)
        {
            X = e.ScreenX - DragOffsetX;
            Y = e.ScreenY - DragOffsetY;
            ClampIntoViewport();

            return;
        }

        if (MemberDragIndex < 0)
            return;

        var target = SlotTargetAt(e.ScreenY - ScreenY);

        if ((target < 0) || (target == MemberDragIndex))
            return;

        //reordered as the cursor passes each slot rather than on release, so the others move out of the way and
        //the drag shows its own result. No ghost panel to draw and no drop marker to place
        MoveMember(MemberDragIndex, target);
        MemberDragIndex = target;
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        var wasDragging = Dragging;

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
        MemberDragIndex = -1;

        //saved on release rather than on every move: dragging fires per frame, and this writes a file
        if (wasDragging)
            DataContext.LocalPlayerSettings.SaveGroupPanelPosition(X, Y);
    }

    /// <summary>How many member panels are actually drawn right now.</summary>
    private int VisibleSlotCount => ShouldShow ? Math.Min(Ordered.Count, MAX_PANELS) : 0;

    /// <summary>Y of the first member panel, in the column's own coordinates.</summary>
    private static int FirstSlotTop => HEADER_HEIGHT + ARROW_GAP;

    private static int SlotStride => GroupMemberPanel.PANEL_HEIGHT + PANEL_GAP;

    /// <summary>The slot under <paramref name="localY" />, or -1 if it is the header, a gap, or past the end.</summary>
    private int SlotAt(int localY)
    {
        var count = VisibleSlotCount;

        if (Collapsed || (count == 0) || (localY < FirstSlotTop))
            return -1;

        var index = (localY - FirstSlotTop) / SlotStride;

        return index < count ? index : -1;
    }

    /// <summary>
    ///     Where a drag at <paramref name="localY" /> wants to drop, clamped to the ends so dragging past the top
    ///     or bottom of the column pins to the first or last slot instead of doing nothing.
    /// </summary>
    private int SlotTargetAt(int localY)
    {
        var count = VisibleSlotCount;

        if (Collapsed || (count == 0))
            return -1;

        return Math.Clamp((localY - FirstSlotTop) / SlotStride, 0, count - 1);
    }

    /// <summary>
    ///     Moves a member to another slot and records the result as the player's arrangement.
    /// </summary>
    private void MoveMember(int from, int to)
    {
        if ((from == to) || (from < 0) || (to < 0) || (from >= Ordered.Count) || (to >= Ordered.Count))
            return;

        var member = Ordered[from];
        Ordered.RemoveAt(from);
        Ordered.Insert(to, member);

        //rebuilt from the whole group, not just the six on screen, so members past the cap keep their places
        CustomOrder.Clear();
        CustomOrder.AddRange(Ordered.Select(entry => entry.Name));

        ApplyMembers();
    }

    private static bool OverArrow(int localX, int localY)
        => (localX >= 0) && (localX < ARROW_SIZE) && (localY >= 0) && (localY < ARROW_SIZE);

    public override void ResetInteractionState()
    {
        ArrowHeld = false;
        Dragging = false;
        MemberDragIndex = -1;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!ShouldShow)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        DrawPlate(spriteBatch);
        DrawHeader(spriteBatch);

        foreach (var panel in Panels)
            if (panel.Visible)
                panel.Draw(spriteBatch);
    }

    /// <summary>
    ///     The solid backing behind the column, unless the player has asked to read it against the world.
    /// </summary>
    /// <remarks>
    ///     One plate across the whole column rather than one per member: the gaps between rows are part of the
    ///     window, and filling each row separately would draw a ladder. The grab bar paints its own fill over the
    ///     top of this, which is why the plate does not have to stop below it.
    ///     <para />
    ///     Skipping it is all "transparent" has to do. The portrait crop carries the composite's own transparency
    ///     and every bar has its own dark track under it, so the rows are already drawn to be read against
    ///     whatever is behind them -- that is the look this restores.
    /// </remarks>
    private void DrawPlate(SpriteBatch spriteBatch)
    {
        if (ClientSettings.TransparentGroupPanels)
            return;

        var plate = new Rectangle(
            ScreenX - PLATE_MARGIN,
            ScreenY - PLATE_MARGIN,
            Width + (PLATE_MARGIN * 2),
            Height + (PLATE_MARGIN * 2));

        DrawRect(spriteBatch, plate, PlateFill);
        DrawBorder(spriteBatch, plate, PlateBorder);
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

        //the group's size, not the number of panels: the player is in the group but has no panel of their own,
        //and a group of six reading "Group (5)" is just wrong
        var caption = $"Group ({WorldState.GroupMembers.Size})";

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
