#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.GroupPanels;

/// <summary>
///     The whole raid at a glance: one column per party, one health bar per member with the name written over it.
///     Shown in place of <see cref="GroupPanelStack" /> during a raid while the grid view is chosen (Shift+Y toggles it).
/// </summary>
/// <remarks>
///     Only parties with members get a column, and the grid is only as tall as the largest party, so it grows with the
///     raid. The columns are rebuilt into the same four lists whenever the group snapshot changes or the player's own row
///     does (their health is not in the group packet, so it is compared each frame), and drawn from those lists the
///     rest of the time. Dragged by its heading row like the panel column; the position is kept for the session.
/// </remarks>
public sealed class RaidGrid : UIPanel
{
    private const int NAME_CHARS = 10;
    private const int CELL_PAD = 2;
    private const int COLUMN_WIDTH = (NAME_CHARS * TextRenderer.CHAR_WIDTH) + (CELL_PAD * 2);
    private const int COLUMN_GAP = 3;
    private const int HEADER_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int CELL_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int PLATE_MARGIN = 2;
    private const int DEFAULT_LEFT_MARGIN = 14;
    private const int DEFAULT_TOP_GAP = (3 * TextRenderer.CHAR_HEIGHT) + 4;
    private const float ABSENT_FADE = 0.45f;
    private const string LEADER_MARK = "*";

    private static readonly Color PlateFill = new(18, 11, 5);
    private static readonly Color PlateBorder = new(54, 34, 16);
    private static readonly Color HeaderFill = new(18, 11, 5, 220);
    private static readonly Color HeaderText = new(214, 190, 146);
    private static readonly Color NameColor = new(232, 220, 196);
    private static readonly Color LeaderColor = new(255, 214, 102);
    private static readonly Color DeadColor = new(150, 150, 158);
    private static readonly Color DeadFill = new(70, 70, 76);
    private static readonly Color OwnPartyBorder = new(212, 175, 55);
    private static readonly Color CellBorder = new(54, 34, 16);
    private static readonly Color BarTrack = new(18, 11, 5);
    private static readonly Color HealthFill = new(150, 32, 32);
    private static readonly Color Shadow = Color.Black;

    private static readonly string[] PartyHeadings = Enumerable.Range(1, GroupMembersState.PARTY_COUNT)
                                                               .Select(party => $"Party {party}")
                                                               .ToArray();

    /// <summary>One member's cell: the roster entry and the name as drawn, leader mark and truncation applied.</summary>
    private readonly record struct Cell(RaidRosterEntry Entry, string Label);

    private readonly List<Cell>[] Columns = Enumerable.Range(0, GroupMembersState.PARTY_COUNT)
                                                      .Select(_ => new List<Cell>(GroupMembersState.PARTY_SIZE))
                                                      .ToArray();

    private readonly int[] PartySizes = new int[GroupMembersState.PARTY_COUNT];
    private int[] ShownParties = [];

    private bool RosterDirty = true;
    private string LastPlayerName = string.Empty;
    private bool LastPlayerLeads;
    private uint LastPlayerHp;
    private uint LastPlayerMaxHp;

    private Rectangle ViewportBounds;
    private bool Placed;
    private bool Dragging;
    private int DragOffsetX;
    private int DragOffsetY;

    public RaidGrid(Rectangle viewportBounds)
    {
        Name = "RaidGrid";
        ViewportBounds = viewportBounds;
        Width = COLUMN_WIDTH;
        Height = HEADER_HEIGHT;

        //stay Visible so the parent keeps calling Update; whether anything is drawn is decided per frame
        Visible = true;
        IsHitTestVisible = false;

        WorldState.GroupMembers.Changed += OnGroupChanged;
    }

    /// <summary>Held down while a full-screen overlay is up, like the panel column.</summary>
    public bool Suppressed { get; set; }

    /// <summary>Raised with a member's name when their cell is clicked.</summary>
    public event Action<string>? MemberClicked;

    private bool ShouldShow
        => ClientSettings.GroupPanelEnabled && ClientSettings.RaidGridView && WorldState.GroupMembers.IsRaid && !Suppressed;

    public void SetViewportBounds(Rectangle bounds) => ViewportBounds = bounds;

    private void OnGroupChanged() => RosterDirty = true;

    /// <summary>
    ///     Which parties get a column (zero-based, in party order) and how many rows the grid needs, given each party's
    ///     member count.
    /// </summary>
    public static (int[] Parties, int Rows) Fit(IReadOnlyList<int> partySizes)
    {
        var parties = Enumerable.Range(0, partySizes.Count)
                                .Where(party => partySizes[party] > 0)
                                .ToArray();

        return (parties, partySizes.Count == 0 ? 0 : partySizes.Max());
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
        {
            X = ViewportBounds.X + DEFAULT_LEFT_MARGIN;
            Y = ViewportBounds.Y + DEFAULT_TOP_GAP;
            Placed = true;
        }

        RefreshColumns();

        X = Math.Clamp(X, ViewportBounds.X, Math.Max(ViewportBounds.X, ViewportBounds.Right - HEADER_HEIGHT));
        Y = Math.Clamp(Y, ViewportBounds.Y, Math.Max(ViewportBounds.Y, ViewportBounds.Bottom - HEADER_HEIGHT));
        IsHitTestVisible = true;
    }

    /// <summary>
    ///     Refills <see cref="Columns" /> and resizes the grid if the snapshot or the player's own row has changed since
    ///     the last fill.
    /// </summary>
    private void RefreshColumns()
    {
        var self = WorldState.Attributes.Current;
        var playerName = WorldState.PlayerName;
        var playerLeads = WorldState.Group.IsLeader;
        var playerHp = self?.CurrentHp ?? 0;
        var playerMaxHp = self?.MaximumHp ?? 0;

        if (!RosterDirty
            && (playerLeads == LastPlayerLeads)
            && (playerHp == LastPlayerHp)
            && (playerMaxHp == LastPlayerMaxHp)
            && string.Equals(playerName, LastPlayerName, StringComparison.Ordinal))
            return;

        RosterDirty = false;
        LastPlayerName = playerName;
        LastPlayerLeads = playerLeads;
        LastPlayerHp = playerHp;
        LastPlayerMaxHp = playerMaxHp;

        foreach (var column in Columns)
            column.Clear();

        var entries = RaidRoster.Entries(
            WorldState.GroupMembers,
            playerName,
            playerLeads,
            playerHp,
            playerMaxHp);

        foreach (var entry in entries)
        {
            if (entry.Party is < 1 or > GroupMembersState.PARTY_COUNT)
                continue;

            var label = entry.IsLeader ? LEADER_MARK + entry.Name : entry.Name;

            if (label.Length > NAME_CHARS)
                label = label[..NAME_CHARS];

            Columns[entry.Party - 1]
                .Add(new Cell(entry, label));
        }

        for (var party = 0; party < Columns.Length; party++)
            PartySizes[party] = Columns[party].Count;

        (ShownParties, var rows) = Fit(PartySizes);

        var columnCount = Math.Max(1, ShownParties.Length);
        Width = (columnCount * COLUMN_WIDTH) + ((columnCount - 1) * COLUMN_GAP);
        Height = HEADER_HEIGHT + (rows * CELL_HEIGHT);
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        if (!ShouldShow || (e.Button != MouseButton.Left))
            return;

        var localY = e.ScreenY - ScreenY;

        if (localY >= HEADER_HEIGHT)
            return;

        Dragging = true;
        DragOffsetX = e.ScreenX - ScreenX;
        DragOffsetY = localY;
        e.Handled = true;
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (!Dragging)
            return;

        X = e.ScreenX - DragOffsetX;
        Y = e.ScreenY - DragOffsetY;
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (e.Button == MouseButton.Left)
            Dragging = false;
    }

    public override void OnClick(ClickEvent e)
    {
        if (!ShouldShow || (e.Button != MouseButton.Left))
            return;

        if (EntryAt(e.ScreenX - ScreenX, e.ScreenY - ScreenY) is not { } entry)
            return;

        MemberClicked?.Invoke(entry.Name);
        e.Handled = true;
    }

    public override void ResetInteractionState() => Dragging = false;

    private RaidRosterEntry? EntryAt(int localX, int localY)
    {
        if ((localX < 0) || (localY < HEADER_HEIGHT))
            return null;

        var slot = localX / (COLUMN_WIDTH + COLUMN_GAP);

        if ((slot >= ShownParties.Length) || ((localX - (slot * (COLUMN_WIDTH + COLUMN_GAP))) >= COLUMN_WIDTH))
            return null;

        var row = (localY - HEADER_HEIGHT) / CELL_HEIGHT;
        var cells = Columns[ShownParties[slot]];

        return row < cells.Count ? cells[row].Entry : null;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!ShouldShow)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        if (!ClientSettings.TransparentGroupPanels)
        {
            var plate = new Rectangle(
                ScreenX - PLATE_MARGIN,
                ScreenY - PLATE_MARGIN,
                Width + (PLATE_MARGIN * 2),
                Height + (PLATE_MARGIN * 2));

            DrawRect(spriteBatch, plate, PlateFill);
            DrawBorder(spriteBatch, plate, PlateBorder);
        }

        var header = new Rectangle(
            ScreenX,
            ScreenY,
            Width,
            HEADER_HEIGHT);

        DrawRect(spriteBatch, header, HeaderFill);
        DrawBorder(spriteBatch, header, PlateBorder);

        for (var slot = 0; slot < ShownParties.Length; slot++)
        {
            var party = ShownParties[slot];
            var x = ScreenX + (slot * (COLUMN_WIDTH + COLUMN_GAP));
            var ownParty = party + 1 == WorldState.GroupMembers.OwnParty;

            TextRenderer.DrawShadowedText(
                spriteBatch,
                new Vector2(x + CELL_PAD, ScreenY + 1),
                PartyHeadings[party],
                ownParty ? OwnPartyBorder : HeaderText,
                Shadow);

            var cells = Columns[party];

            for (var row = 0; row < cells.Count; row++)
                DrawCell(
                    spriteBatch,
                    cells[row],
                    x,
                    ScreenY + HEADER_HEIGHT + (row * CELL_HEIGHT),
                    ownParty);
        }
    }

    private static void DrawCell(
        SpriteBatch spriteBatch,
        Cell cell,
        int x,
        int y,
        bool ownParty)
    {
        var entry = cell.Entry;
        var fade = entry.Absent ? ABSENT_FADE : 1f;

        var bounds = new Rectangle(
            x,
            y,
            COLUMN_WIDTH,
            CELL_HEIGHT);

        var bar = new Rectangle(
            bounds.X + 1,
            bounds.Y + 1,
            bounds.Width - 2,
            bounds.Height - 2);

        DrawRect(spriteBatch, bar, BarTrack * fade);

        var fillWidth = (int)Math.Round(bar.Width * entry.HealthPercent);

        //one hit point must not read as none
        if ((fillWidth == 0) && (entry.HealthPercent > 0))
            fillWidth = 1;

        if (entry.Dead)
            fillWidth = bar.Width;

        if (fillWidth > 0)
            DrawRect(
                spriteBatch,
                new Rectangle(
                    bar.X,
                    bar.Y,
                    fillWidth,
                    bar.Height),
                (entry.Dead ? DeadFill : HealthFill) * fade);

        DrawBorder(spriteBatch, bounds, (ownParty ? OwnPartyBorder : CellBorder) * fade);

        var nameColor = entry.Dead ? DeadColor : entry.IsLeader ? LeaderColor : NameColor;

        TextRenderer.DrawShadowedText(
            spriteBatch,
            new Vector2(x + CELL_PAD, y + 1),
            cell.Label,
            nameColor * fade,
            Shadow * fade);
    }

    public override void Dispose()
    {
        WorldState.GroupMembers.Changed -= OnGroupChanged;
        base.Dispose();
    }
}
