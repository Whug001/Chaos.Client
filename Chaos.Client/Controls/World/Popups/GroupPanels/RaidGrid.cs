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
///     The whole raid at a glance: one column per party, a name and a health bar per member. Shown in place of
///     <see cref="GroupPanelStack" /> during a raid while the grid view is chosen (Shift+Y toggles it).
/// </summary>
/// <remarks>
///     The columns are rebuilt into the same four lists whenever the group snapshot changes or the player's own row
///     does (their health is not in the group packet, so it is compared each frame), and drawn from those lists the
///     rest of the time. Dragged by its header like the panel column; the position is kept for the session.
/// </remarks>
public sealed class RaidGrid : UIPanel
{
    private const int NAME_CHARS = 10;
    private const int CELL_PAD = 2;
    private const int COLUMN_WIDTH = (NAME_CHARS * TextRenderer.CHAR_WIDTH) + (CELL_PAD * 2);
    private const int COLUMN_GAP = 3;
    private const int HEADER_HEIGHT = 16;
    private const int HEADING_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int BAR_HEIGHT = 4;
    private const int CELL_HEIGHT = TextRenderer.CHAR_HEIGHT + BAR_HEIGHT + 4;
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
    private static readonly Color OwnPartyBorder = new(212, 175, 55);
    private static readonly Color CellBorder = new(54, 34, 16);
    private static readonly Color BarTrack = new(18, 11, 5);
    private static readonly Color HealthFill = new(196, 46, 46);
    private static readonly Color Shadow = Color.Black;

    private static readonly string[] PartyHeadings = Enumerable.Range(1, GroupMembersState.PARTY_COUNT)
                                                               .Select(party => $"Party {party}")
                                                               .ToArray();

    /// <summary>One member's cell: the roster entry and the name as drawn, leader mark and truncation applied.</summary>
    private readonly record struct Cell(RaidRosterEntry Entry, string Label);

    private readonly List<Cell>[] Columns = Enumerable.Range(0, GroupMembersState.PARTY_COUNT)
                                                      .Select(_ => new List<Cell>(GroupMembersState.PARTY_SIZE))
                                                      .ToArray();

    private string Caption = string.Empty;
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
        Width = (GroupMembersState.PARTY_COUNT * COLUMN_WIDTH) + ((GroupMembersState.PARTY_COUNT - 1) * COLUMN_GAP);
        Height = HEADER_HEIGHT + HEADING_HEIGHT + (GroupMembersState.PARTY_SIZE * CELL_HEIGHT);

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

    /// <summary>Refills <see cref="Columns" /> if the snapshot or the player's own row has changed since the last fill.</summary>
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

        Caption = $"Raid {WorldState.GroupMembers.Size}/{GroupMembersState.MAX_RAID_SIZE}";
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
        var top = HEADER_HEIGHT + HEADING_HEIGHT;

        if ((localX < 0) || (localY < top))
            return null;

        var column = localX / (COLUMN_WIDTH + COLUMN_GAP);

        if ((column >= GroupMembersState.PARTY_COUNT) || ((localX - (column * (COLUMN_WIDTH + COLUMN_GAP))) >= COLUMN_WIDTH))
            return null;

        var row = (localY - top) / CELL_HEIGHT;
        var cells = Columns[column];

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

        TextRenderer.DrawShadowedText(
            spriteBatch,
            new Vector2(ScreenX + CELL_PAD, ScreenY + ((HEADER_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2)),
            Caption,
            HeaderText,
            Shadow);

        for (var column = 0; column < GroupMembersState.PARTY_COUNT; column++)
        {
            var x = ScreenX + (column * (COLUMN_WIDTH + COLUMN_GAP));
            var ownParty = column + 1 == WorldState.GroupMembers.OwnParty;

            TextRenderer.DrawShadowedText(
                spriteBatch,
                new Vector2(x + CELL_PAD, ScreenY + HEADER_HEIGHT + 1),
                PartyHeadings[column],
                ownParty ? OwnPartyBorder : HeaderText,
                Shadow);

            var cells = Columns[column];

            for (var row = 0; row < cells.Count; row++)
                DrawCell(
                    spriteBatch,
                    cells[row],
                    x,
                    ScreenY + HEADER_HEIGHT + HEADING_HEIGHT + (row * CELL_HEIGHT),
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
            CELL_HEIGHT - 1);

        DrawBorder(spriteBatch, bounds, (ownParty ? OwnPartyBorder : CellBorder) * fade);

        var nameColor = entry.Dead ? DeadColor : entry.IsLeader ? LeaderColor : NameColor;

        TextRenderer.DrawShadowedText(
            spriteBatch,
            new Vector2(x + CELL_PAD, y + 1),
            cell.Label,
            nameColor * fade,
            Shadow * fade);

        var track = new Rectangle(
            x + CELL_PAD,
            y + TextRenderer.CHAR_HEIGHT + 2,
            COLUMN_WIDTH - (CELL_PAD * 2),
            BAR_HEIGHT);

        DrawRect(spriteBatch, track, BarTrack * fade);

        var fillWidth = (int)Math.Round((track.Width - 2) * entry.HealthPercent);

        //one hit point must not read as none
        if ((fillWidth == 0) && (entry.HealthPercent > 0))
            fillWidth = 1;

        if (fillWidth > 0)
            DrawRect(
                spriteBatch,
                new Rectangle(
                    track.X + 1,
                    track.Y + 1,
                    fillWidth,
                    BAR_HEIGHT - 2),
                (entry.Dead ? DeadColor : HealthFill) * fade);
    }

    public override void Dispose()
    {
        WorldState.GroupMembers.Changed -= OnGroupChanged;
        base.Dispose();
    }
}
